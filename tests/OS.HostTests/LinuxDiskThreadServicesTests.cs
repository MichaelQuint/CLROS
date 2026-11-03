using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;

internal static unsafe partial class LinuxDiskThreadServicesTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong Path = Base + 0x1200, Output = Base + 0x2000;
    [UnmanagedCallersOnly]
    private static byte PumpClock(void* context, ulong* now)
    {
        var c = (PumpContext*)context;
        c->ClockCalls++;
        if (c->ClockCalls == c->FailClockAt)
            return 0;
        *now = c->Regress && c->ClockCalls == 2 ? c->Now - 1 : c->Now;
        c->Now += c->Increment;
        return 1;
    }

    [UnmanagedCallersOnly]
    private static byte PumpFlush(void* context)
    {
        var c = (PumpContext*)context;
        var d = c->Device;
        c->Bad |= d->Submitted < c->Submitted || d->Submitted - c->Submitted > 1 || d->Completed < c->Completed || d->Completed - c->Completed > 1 || d->Copied < c->Copied || d->Copied - c->Copied > 1 || d->Submitted - d->Completed > 1;
        c->Submitted = d->Submitted;
        c->Completed = d->Completed;
        c->Copied = d->Copied;
        c->Flushes++;
        return c->Flushes == c->FailFlushAt ? (byte)0 : (byte)1;
    }

    // Read existing scalar/token fields without copying a live retained owner.
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "DiskOperation")]
    private static extern ref Fds.DiskToken SlotDiskOperation(ref LinuxThreadServices.Slot slot);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Index")]
    private static extern ref uint DiskIndex(ref Fds.DiskToken token);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Generation")]
    private static extern ref ulong DiskGeneration(ref Fds.DiskToken token);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Operation")]
    private static extern ref Fs.OperationToken RequestOperation(ref Fds.DiskRequest request);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "OriginalFd")]
    private static extern ref Fds.Token RequestFd(ref Fds.DiskRequest request);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "IoActive")]
    private static extern ref bool MountIoActive(ref Fs.Mount mount);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "IoIndex")]
    private static extern ref uint MountIoIndex(ref Fs.Mount mount);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "IoGeneration")]
    private static extern ref ulong MountIoGeneration(ref Fs.Mount mount);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "IoToken")]
    private static extern ref Block.ReadToken MountIoToken(ref Fs.Mount mount);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Owner")]
    private static extern ref ulong FsOwner(ref Fs.OperationToken token);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Epoch")]
    private static extern ref ulong FsEpoch(ref Fs.OperationToken token);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Generation")]
    private static extern ref ulong FsGeneration(ref Fs.OperationToken token);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Index")]
    private static extern ref uint FsIndex(ref Fs.OperationToken token);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "Waiting")]
    private static extern ref byte OperationWaiting(ref Fs.Operation operation);
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "RequestSector")]
    private static extern ref ulong RequestSector(ref Block.State device);
    private static (Fds.DiskToken Disk, Fs.OperationToken FileSystem, Block.ReadToken Physical, Fds.Token Fd) PendingRead(Owner o)
    {
        var slot = o.Thread.View(o.Main);
        var parked = o.Thread.Kernel.Inspect(o.Main);
        HostCheck.True(slot.WaitSource == 4 && slot.Wait.Sequence != 0 && parked.State == KernelThreadTable.State.Waiting && parked.Completion == KernelThreadTable.Completion.None);
        var token = SlotDiskOperation(ref *o.Thread.Slots);
        var observed = Fds.PollDisk(o.Files.State, (ulong)o.Thread.Service, token);
        HostCheck.True(observed.Progress == Fds.DiskProgress.Pending && observed.Worker.Value == o.Main.Value);
        uint index = DiskIndex(ref token);
        HostCheck.True(index < Fds.DiskSlots);
        var request = o.Requests + index;
        var fs = RequestOperation(ref *request);
        var fd = RequestFd(ref *request);
        HostCheck.True(Fds.IsCurrent(o.Files.State, fd) && fs.IsInitialized && FsOwner(ref fs) == (ulong)o.Storage.Mount && FsEpoch(ref fs) == o.Storage.Input.Epoch && MountIoActive(ref *o.Storage.Mount) && MountIoIndex(ref *o.Storage.Mount) == FsIndex(ref fs) && MountIoGeneration(ref *o.Storage.Mount) == FsGeneration(ref fs));
        byte dataIo = (byte)typeof(Fs).GetField("DataIo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!.GetRawConstantValue()!;
        HostCheck.Equal(dataIo, OperationWaiting(ref o.Storage.Input.Operations[FsIndex(ref fs)]));
        var physical = MountIoToken(ref *o.Storage.Mount);
        HostCheck.True(o.Storage.Device->ReadPending && o.Storage.Device->Submitted == o.Storage.Device->Completed + 1);
        HostCheck.Equal((ulong)o.Storage.Disk.Data(10), RequestSector(ref *o.Storage.Device));
        // Copy rejects or reports Pending before touching an empty destination.
        // A valid Fs.Poll would advance I/O, so it is deliberately not used here.
        HostCheck.Equal(Block.Status.Pending, Block.Copy(o.Storage.Device, physical, Span<byte>.Empty));
        return (token, fs, physical, fd);
    }

    [HostTest("linux.disk-thread omitted access leaves one exact real pending data request while explicit quantum advances")]
    private static void DefaultOneStepPending()
    {
        using Owner o = new(large: true);
        uint fd = o.Open();
        o.Storage.CompleteOnPoll = true;
        new Span<byte>(o.Thread.At(Output), 8192).Fill(0xa5);
        uint writes = o.Thread.Writes;
        o.Begin(0, fd, Output, 8192);
        o.Park();
        ulong submitted = o.Storage.Device->Submitted, polls = o.Storage.Device->Polls;
        PumpContext incomplete = new()
        {
            Device = o.Storage.Device,
            FailClockAt = 1,
            FailFlushAt = 1
        };
        foreach (bool clockOnly in new[]
        {
            false,
            true
        }

        )
        {
            var invalid = new LinuxThreadServices.DiskPumpAccess
            {
                Context = &incomplete,
                Clock = clockOnly ? &PumpClock : null,
                Flush = clockOnly ? null : &PumpFlush
            };
            HostCheck.True(!o.Thread.Service->PollDiskReadiness(Key, 2, invalid));
            HostCheck.True(incomplete.ClockCalls == 0 && incomplete.Flushes == 0 && o.Storage.Device->Submitted == submitted && o.Storage.Device->Polls == polls);
        }

        HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2)); // Access entirely omitted.
        var first = PendingRead(o);
        HostCheck.True(o.Storage.Device->Submitted == submitted + 1 && o.Storage.Device->Completed == submitted && o.Storage.Device->Copied == submitted && o.Storage.Device->Polls == polls && o.Thread.Writes == writes && new ReadOnlySpan<byte>(o.Thread.At(Output), 8192).IndexOfAnyExcept((byte)0xa5) < 0);
        PumpContext context = new()
        {
            Device = o.Storage.Device,
            Submitted = submitted + 1,
            Completed = submitted,
            Copied = submitted
        };
        var access = new LinuxThreadServices.DiskPumpAccess
        {
            Context = &context,
            Clock = &PumpClock,
            Flush = &PumpFlush
        };
        HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
        HostCheck.True(context.Flushes == 4 && context.ClockCalls == 4 && !context.Bad && o.Storage.Device->Submitted == submitted + 3 && o.Storage.Device->Completed == submitted + 2 && o.Storage.Device->Copied == submitted + 2 && o.Thread.Writes == writes && new ReadOnlySpan<byte>(o.Thread.At(Output), 8192).IndexOfAnyExcept((byte)0xa5) < 0);
        for (int i = 0; i < 32 && o.Thread.Kernel.Inspect(o.Main).Completion == KernelThreadTable.Completion.None; i++)
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
        HostCheck.Equal(8192L, o.Consume());
        o.Begin(17, fd, Output, 8);
        o.Park();
        HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2));
        var next = PendingRead(o);
        HostCheck.True(DiskIndex(ref first.Disk) == DiskIndex(ref next.Disk) && DiskGeneration(ref first.Disk) < DiskGeneration(ref next.Disk) && FsIndex(ref first.FileSystem) == FsIndex(ref next.FileSystem) && FsGeneration(ref first.FileSystem) < FsGeneration(ref next.FileSystem));
        ulong before = o.Storage.Device->Submitted, completed = o.Storage.Device->Completed, copied = o.Storage.Device->Copied;
        HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PollDisk(o.Files.State, (ulong)o.Thread.Service, first.Disk).Progress);
        HostCheck.Equal(Fs.Status.StaleToken, Fs.Poll(o.Storage.Mount, first.FileSystem));
        HostCheck.Equal(Block.Status.StaleToken, Block.Copy(o.Storage.Device, first.Physical, Span<byte>.Empty));
        HostCheck.True(o.Storage.Device->ReadPending && o.Storage.Device->Submitted == before && o.Storage.Device->Completed == completed && o.Storage.Device->Copied == copied);
        for (int i = 0; i < 16 && o.Thread.Kernel.Inspect(o.Main).Completion == KernelThreadTable.Completion.None; i++)
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
        HostCheck.Equal(8L, o.Consume());
    }

    [HostTest("linux.disk-thread a real pending block token does not hide a closed fd or interrupted wait winner")]
    private static void DefaultOneStepRevocation()
    {
        foreach (bool close in new[]
        {
            false,
            true
        }

        )
        {
            using Owner o = new(large: true);
            uint fd = o.Open();
            HostCheck.Equal(7L, o.Run(72, fd, 0, 7));
            var child = o.Thread.Clone();
            uint writes = o.Thread.Writes;
            new Span<byte>(o.Thread.At(Output), 8192).Fill(0x69);
            o.Begin(0, fd, Output, 8192);
            o.Park();
            HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value);
            ulong submitted = o.Storage.Device->Submitted;
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2));
            var pending = PendingRead(o);
            if (close)
            {
                HostCheck.Equal(0L, o.Thread.DescriptorCall(3, fd).Value);
                HostCheck.True(!Fds.IsCurrent(o.Files.State, pending.Fd) && !o.May(Output, 8192));
            }
            else
            {
                HostCheck.True(o.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, o.Thread.View(o.Main).Wait).Succeeded);
                HostCheck.True(Fds.IsCurrent(o.Files.State, pending.Fd));
                HostCheck.Equal(KernelThreadTable.Completion.Interrupted, o.Thread.Kernel.Inspect(o.Main).Completion);
            }

            HostCheck.True(o.Storage.Device->ReadPending && o.Storage.Device->Submitted == submitted + 1 && o.Storage.Device->Completed == submitted && o.Storage.Device->Copied == submitted);
            if (close)
                o.Ready();
            o.Thread.Yield(child);
            HostCheck.Equal(close ? -9L : -4L, o.Consume());
            for (int i = 0; i < 64; i++)
            {
                o.Storage.Tick();
                HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2));
            }

            HostCheck.True(o.Storage.Device->Submitted == submitted + 1 && o.Storage.Device->Copied == submitted && o.Thread.Writes == writes && new ReadOnlySpan<byte>(o.Thread.At(Output), 8192).IndexOfAnyExcept((byte)0x69) < 0);
            HostCheck.Equal(Fds.DiskProgress.Rejected, Fds.PollDisk(o.Files.State, (ulong)o.Thread.Service, pending.Disk).Progress);
            if (close)
            {
                o.PutPath("/app/wwwroot/index.html");
                o.Begin(2, Path, 0x88100);
                o.Park();
                o.Ready();
                HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value);
                o.Thread.Yield(child);
                HostCheck.Equal((long)fd, o.Consume());
                HostCheck.True(!Fds.IsCurrent(o.Files.State, pending.Fd)); // Same number, a new generation.
            }
        }
    }

    [HostTest("linux.disk-thread bounded quantum reduces outer passes with identical real sixteen-sector completion and no early user copy")]
    private static void ImmediateQuantum()
    {
        foreach (bool quantum in new[]
        {
            false,
            true
        }

        )
        {
            using Owner o = new(large: true);
            uint fd = o.Open();
            o.Storage.CompleteOnPoll = true;
            new Span<byte>(o.Thread.At(Output), 8192).Fill(0xa5);
            o.Begin(17, fd, Output, 8192);
            o.Park();
            ulong submitted = o.Storage.Device->Submitted;
            PumpContext context = new()
            {
                Device = o.Storage.Device,
                Submitted = submitted,
                Completed = submitted,
                Copied = submitted
            };
            LinuxThreadServices.DiskPumpAccess access = quantum ? new()
            {
                Context = &context,
                Clock = &PumpClock,
                Flush = &PumpFlush
            }

            : default;
            int outer = 0;
            while (o.Thread.Kernel.Inspect(o.Main).Completion == KernelThreadTable.Completion.None && outer < 100)
            {
                uint flushed = context.Flushes;
                HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
                outer++;
                HostCheck.True(context.Flushes - flushed <= 4);
                HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 8192).IndexOfAnyExcept((byte)0xa5) < 0);
            }

            HostCheck.Equal(quantum ? 8 : 32, outer);
            HostCheck.Equal(submitted + 16, o.Storage.Device->Submitted);
            HostCheck.Equal(submitted + 16, o.Storage.Device->Completed);
            HostCheck.Equal(submitted + 16, o.Storage.Device->Copied);
            HostCheck.True(!context.Bad);
            if (quantum)
                HostCheck.Equal(32U, context.Flushes);
            HostCheck.Equal(8192L, o.Consume());
            for (int i = 0; i < 8192; i++)
                HostCheck.Equal(unchecked((byte)(i * 31 ^ (i >> 8) ^ 0x5a)), o.Thread.At(Output)[i]);
            HostCheck.Equal(0L, o.Run(8, fd, 0, 1));
            Console.WriteLine($"DISK_QUANTUM_WORK enabled={quantum} outerPasses={outer} submitted=16 completed=16 copied=16 peakOutstanding=1");
        }
    }

    [HostTest("linux.disk-thread diagnostic phases count every real primitive and one exclusive yield without early user copy")]
    private static void QuantumMeasurements()
    {
        using Owner o = new(large: true);
        uint fd = o.Open();
        o.Storage.CompleteOnPoll = true;
        new Span<byte>(o.Thread.At(Output), 8192).Fill(0xa5);
        o.Begin(17, fd, Output, 8192);
        o.Park();
        ulong submitted = o.Storage.Device->Submitted;
        PumpContext context = new()
        {
            Device = o.Storage.Device,
            Submitted = submitted,
            Completed = submitted,
            Copied = submitted,
            Now = 100,
            Increment = 100
        };
        LinuxThreadServices.DiskPumpMeasurements measurements = default;
        var access = new LinuxThreadServices.DiskPumpAccess
        {
            Context = &context,
            Clock = &PumpClock,
            Flush = &PumpFlush,
            Measurements = &measurements
        };
        while (o.Thread.Kernel.Inspect(o.Main).Completion == KernelThreadTable.Completion.None && measurements.Calls < 100)
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
        HostCheck.True(measurements.Calls == 8 && measurements.Steps == 32 && measurements.DiskReady == 1 && measurements.StepLimit == 7 && measurements.OtherReady == 0 && measurements.NoAdvance == 0 && measurements.Elapsed == 0 && measurements.Failures == 0 && measurements.FlushedPastBudget == 0);
        HostCheck.True(measurements.PumpNanoseconds == 3200 && measurements.ScanNanoseconds == 3200 && measurements.FlushNanoseconds == 3200 && context.Flushes == 32 && !context.Bad);
        HostCheck.True(o.Storage.Device->Submitted == submitted + 16 && o.Storage.Device->Completed == submitted + 16 && o.Storage.Device->Copied == submitted + 16 && new ReadOnlySpan<byte>(o.Thread.At(Output), 8192).IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.Equal(8192L, o.Consume());
    }

    [HostTest("linux.disk-thread diagnostic overflow and failed clock or flush retain the original request without an extra submit")]
    private static void QuantumMeasurementFailures()
    {
        foreach (bool clockFailure in new[]
        {
            false,
            true
        }

        )
        {
            using Owner o = new(large: true);
            uint fd = o.Open();
            o.Storage.CompleteOnPoll = true;
            o.Begin(17, fd, Output, 8192);
            o.Park();
            ulong submitted = o.Storage.Device->Submitted;
            PumpContext context = new()
            {
                Device = o.Storage.Device,
                Submitted = submitted,
                Completed = submitted,
                Copied = submitted,
                Now = 100,
                Increment = 100
            };
            LinuxThreadServices.DiskPumpMeasurements measurements = new()
            {
                Calls = ulong.MaxValue
            };
            var access = new LinuxThreadServices.DiskPumpAccess
            {
                Context = &context,
                Clock = &PumpClock,
                Flush = &PumpFlush,
                Measurements = &measurements
            };
            HostCheck.True(!o.Thread.Service->PollDiskReadiness(Key, 2, access));
            HostCheck.True(context.ClockCalls == 0 && context.Flushes == 0 && o.Storage.Device->Submitted == submitted);
            measurements = default;
            if (clockFailure)
                context.FailClockAt = 2;
            else
                context.FailFlushAt = 1;
            HostCheck.True(!o.Thread.Service->PollDiskReadiness(Key, 2, access));
            HostCheck.True(measurements.Calls == 1 && measurements.Steps == 1 && measurements.Failures == 1 && measurements.DiskReady + measurements.OtherReady + measurements.NoAdvance + measurements.Elapsed + measurements.StepLimit == 0 && o.Storage.Device->Submitted == submitted + 1 && o.Storage.Device->Copied == submitted);
        }
    }

    [HostTest("linux.disk-thread quantum yields on delayed completion and elapsed budget while cancellation revokes copy")]
    private static void QuantumYields()
    {
        foreach (bool elapsed in new[]
        {
            false,
            true
        }

        )
        {
            using Owner o = new();
            uint fd = o.Open();
            o.Begin(17, fd, Output, 700);
            o.Park();
            ulong submitted = o.Storage.Device->Submitted;
            PumpContext context = new()
            {
                Device = o.Storage.Device,
                Submitted = submitted,
                Completed = submitted,
                Copied = submitted,
                Increment = elapsed ? 5000000UL : 0
            };
            var access = new LinuxThreadServices.DiskPumpAccess
            {
                Context = &context,
                Clock = &PumpClock,
                Flush = &PumpFlush
            };
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
            HostCheck.Equal(elapsed ? 1U : 2U, context.Flushes);
            HostCheck.Equal(submitted + 1, o.Storage.Device->Submitted);
            HostCheck.Equal(submitted, o.Storage.Device->Completed);
            HostCheck.Equal(submitted, o.Storage.Device->Copied);
            context.Increment = 0;
            uint before = context.Flushes;
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
            HostCheck.Equal(before + 1, context.Flushes);
            HostCheck.Equal(submitted, o.Storage.Device->Completed);
            HostCheck.True(!context.Bad && o.Thread.Kernel.Inspect(o.Main).Completion == KernelThreadTable.Completion.None);
            // Cancellation occurs at a real serialized invocation boundary;
            // callbacks never manufacture IRQ-off reentrancy.
            HostCheck.True(o.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, o.Thread.View(o.Main).Wait).Succeeded);
            new Span<byte>(o.Thread.At(Output), 700).Fill(0x69);
            o.Storage.Tick();
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
            o.Ready();
            HostCheck.Equal(-4L, o.Consume());
            HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 700).IndexOfAnyExcept((byte)0x69) < 0);
        }
    }

    [HostTest("linux.disk-thread quantum clock and flush failures stop before another step and preserve teardown")]
    private static void QuantumCallbackFailures()
    {
        for (int mode = 0; mode < 4; mode++)
        {
            using Owner o = new();
            uint fd = o.Open();
            o.Begin(17, fd, Output, 700);
            o.Park();
            new Span<byte>(o.Thread.At(Output), 700).Fill(0x77);
            ulong submitted = o.Storage.Device->Submitted;
            PumpContext context = new()
            {
                Device = o.Storage.Device,
                Submitted = submitted,
                Completed = submitted,
                Copied = submitted,
                Now = 10,
                FailClockAt = mode == 0 ? 1U : mode == 1 ? 2U : 0,
                FailFlushAt = mode == 2 ? 1U : 0,
                Regress = mode == 3
            };
            HostCheck.True(!o.Thread.Service->PollDiskReadiness(Key, 2, new() { Context = &context, Clock = &PumpClock, Flush = &PumpFlush }));
            HostCheck.Equal(mode == 0 ? submitted : submitted + 1, o.Storage.Device->Submitted);
            HostCheck.Equal(mode == 0 ? 0U : 1U, context.Flushes);
            HostCheck.Equal(submitted, o.Storage.Device->Completed);
            HostCheck.Equal(submitted, o.Storage.Device->Copied);
            HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 700).IndexOfAnyExcept((byte)0x77) < 0 && !o.May(Output, 700));
        }
    }

    [HostTest("linux.disk-thread quantum preserves an interrupted wait winner and exiting worker while retaining pending owners")]
    private static void QuantumWaitWinner()
    {
        foreach (bool shutdown in new[]
        {
            false,
            true
        }

        )
        {
            using Owner o = new();
            uint fd = o.Open();
            o.Begin(17, fd, Output, 700);
            o.Park();
            ulong submitted = o.Storage.Device->Submitted;
            PumpContext context = new()
            {
                Device = o.Storage.Device,
                Submitted = submitted,
                Completed = submitted,
                Copied = submitted
            };
            var access = new LinuxThreadServices.DiskPumpAccess
            {
                Context = &context,
                Clock = &PumpClock,
                Flush = &PumpFlush
            };
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
            o.Storage.Tick();
            if (shutdown)
                HostCheck.True(o.Thread.Service->Shutdown(Key, 2, 7));
            else
                HostCheck.True(o.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, o.Thread.View(o.Main).Wait).Succeeded);
            uint before = context.Flushes;
            new Span<byte>(o.Thread.At(Output), 700).Fill(0x69);
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
            HostCheck.Equal(before + 1, context.Flushes);
            HostCheck.Equal(submitted + 1, o.Storage.Device->Submitted);
            HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(Output), 700).IndexOfAnyExcept((byte)0x69) < 0);
            if (shutdown)
                HostCheck.True(o.Thread.View(o.Main).Exiting);
            else
            {
                HostCheck.Equal(KernelThreadTable.Completion.Interrupted, o.Thread.Kernel.Inspect(o.Main).Completion);
                HostCheck.Equal(-4L, o.Consume());
            }
        }
    }

    [HostTest("linux.disk-thread quantum retains round robin competing requests and yields to an already ready worker")]
    private static void QuantumCompeting()
    {
        const ulong firstOutput = Base + 0x1000;
        HostCheck.True(firstOutput + 16384 <= Base + LinuxThreadServicesTests.UserBytes);
        using Owner o = new(large: true);
        uint first = o.Open(), second = o.Open();
        var child = o.Thread.Clone();
        o.Storage.CompleteOnPoll = true;
        o.Begin(17, first, firstOutput, 8192);
        o.Park();
        HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value);
        o.Begin(17, second, firstOutput + 8192, 8192);
        HostCheck.True(o.Thread.Service->CommitDiskWait(Key, 2, o.Thread.Kernel.Capture(child), out bool handoff) && handoff);
        o.Thread.Kernel.Confirm();
        new Span<byte>(o.Thread.At(firstOutput), 16384).Fill(0x77);
        ulong submitted = o.Storage.Device->Submitted;
        PumpContext context = new()
        {
            Device = o.Storage.Device,
            Submitted = submitted,
            Completed = submitted,
            Copied = submitted
        };
        var access = new LinuxThreadServices.DiskPumpAccess
        {
            Context = &context,
            Clock = &PumpClock,
            Flush = &PumpFlush
        };
        for (int i = 0; i < 256; i++)
        {
            bool ready = o.Thread.Kernel.Inspect(o.Main).Completion != KernelThreadTable.Completion.None || o.Thread.Kernel.Inspect(child).Completion != KernelThreadTable.Completion.None;
            uint before = context.Flushes;
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
            HostCheck.True(context.Flushes - before <= (ready ? 1U : 4U));
            if (o.Thread.Kernel.Inspect(o.Main).Completion != KernelThreadTable.Completion.None && o.Thread.Kernel.Inspect(child).Completion != KernelThreadTable.Completion.None)
                break;
        }

        HostCheck.Equal(submitted + 32, o.Storage.Device->Submitted);
        HostCheck.Equal(submitted + 32, o.Storage.Device->Completed);
        HostCheck.Equal(submitted + 32, o.Storage.Device->Copied);
        HostCheck.True(!context.Bad);
        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(firstOutput), 16384).IndexOfAnyExcept((byte)0x77) < 0);
        // Round robin need not finish the older operation first. Consume the
        // actual ready queue order rather than prescribing its scheduler order.
        OSHostHandle previous = default;
        for (int i = 0; i < 2; i++)
        {
            var current = o.Thread.Kernel.Dispatch();
            HostCheck.True((current.Value == o.Main.Value || current.Value == child.Value) && current.Value != previous.Value);
            var result = o.Thread.Service->CompleteDiskWait(Key, 2);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action);
            HostCheck.Equal(8192L, result.Value);
            previous = current;
            if (i == 0)
                o.Thread.Yield(current);
        }

        HostCheck.True(new ReadOnlySpan<byte>(o.Thread.At(firstOutput), 8192).SequenceEqual(new ReadOnlySpan<byte>(o.Thread.At(firstOutput + 8192), 8192)));
    }

    [HostTest("linux.disk-thread quantum yields after one step to a ready worker without a disk token")]
    private static void QuantumRunnablePeer()
    {
        using Owner o = new(large: true);
        uint fd = o.Open();
        var child = o.Thread.Clone();
        o.Storage.CompleteOnPoll = true;
        o.Begin(17, fd, Output, 8192);
        o.Park();
        HostCheck.Equal(1U, o.Thread.Kernel.Table->ReadyCount);
        ulong submitted = o.Storage.Device->Submitted;
        PumpContext context = new()
        {
            Device = o.Storage.Device,
            Submitted = submitted,
            Completed = submitted,
            Copied = submitted
        };
        var access = new LinuxThreadServices.DiskPumpAccess
        {
            Context = &context,
            Clock = &PumpClock,
            Flush = &PumpFlush
        };
        int outer = 0;
        while (o.Thread.Kernel.Inspect(o.Main).Completion == KernelThreadTable.Completion.None && outer < 100)
        {
            uint before = context.Flushes;
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2, access));
            outer++;
            HostCheck.Equal(before + 1, context.Flushes);
        }

        HostCheck.Equal(32, outer);
        HostCheck.Equal(submitted + 16, o.Storage.Device->Copied);
        HostCheck.True(!context.Bad);
        HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value);
        o.Thread.Yield(child);
        HostCheck.Equal(8192L, o.Consume());
    }

    [HostTest("linux.disk-thread gateway carries stat open lstat seek pread and openat through real waits")]
    private static void GatewayDiskWaits()
    {
        using Owner o = new();
        o.PutPath("/app/wwwroot/index.html");
        byte* scratch = stackalloc byte[64];
        LinuxSyscallGateState gate = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new() { UserRoot = 0x200000, MaximumCalls = 64, MaximumWrite = 64, Scratch = scratch, ScratchBytes = 64, FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true, EnableDescriptors = true, EnableThreads = true }, o.Thread.GatewayAccess));
        HostCheck.Equal(0L, GatewayDiskRun(o, ref gate, 4, Path, Output));
        HostCheck.Equal(700UL, o.Thread.Read64(Output + 48));
        uint fd = (uint)GatewayDiskRun(o, ref gate, 2, Path);
        HostCheck.Equal(3U, fd);
        HostCheck.Equal(0L, GatewayDiskRun(o, ref gate, 6, Path, Output));
        HostCheck.Equal(700UL, o.Thread.Read64(Output + 48));
        HostCheck.Equal(11L, GatewayDiskRun(o, ref gate, 8, fd, 11));
        HostCheck.Equal(5L, GatewayDiskRun(o, ref gate, 17, fd, Output, 5, 2));
        for (int i = 0; i < 5; i++)
            HostCheck.Equal(unchecked((byte)((i + 2) * 31 ^ ((i + 2) >> 8) ^ 0x5a)), o.Thread.At(Output)[i]);
        HostCheck.Equal(4L, GatewayDiskRun(o, ref gate, 257, 0xffffff9c, Path));
        HostCheck.Equal(11L, GatewayDiskRun(o, ref gate, 8, fd, 0, 1)); // pread did not advance the shared cursor.
        HostCheck.True(gate.Calls == 7 && gate.TotalCalls == 7 && gate.BytesWritten == 0 && gate.TotalBytesWritten == 0);
        // Ordinary stdout still accounts only its actual successful prefix.
        var image = o.Thread.CurrentImage();
        var frame = image->Frame;
        frame.Number = 1;
        frame.Argument0 = 1;
        frame.Argument1 = Output;
        frame.Argument2 = 5;
        frame.Argument3 = frame.Argument4 = frame.Argument5 = 0;
        image->Frame = frame;
        var write = LinuxSyscallGateway.Dispatch(ref gate, ref frame);
        HostCheck.True(write.Action == LinuxSyscallAction.Return && write.Value == 5 && gate.Calls == 8 && gate.TotalCalls == 8);
        HostCheck.True(gate.BytesWritten == 5 && gate.TotalBytesWritten == 5 && Fds.Audit(o.Files.State));
    }

    private static long GatewayDiskRun(Owner o, ref LinuxSyscallGateState gate, ulong number, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
    {
        var image = o.Thread.CurrentImage();
        var frame = image->Frame;
        frame.Number = number;
        frame.Argument0 = a0;
        frame.Argument1 = a1;
        frame.Argument2 = a2;
        frame.Argument3 = a3;
        frame.Argument4 = frame.Argument5 = 0;
        image->Frame = frame;
        ulong calls = gate.TotalCalls;
        HostCheck.Equal(LinuxSyscallAction.Blocked, LinuxSyscallGateway.Dispatch(ref gate, ref frame).Action);
        HostCheck.True(gate.Status == LinuxSyscallGateStatus.Blocked && gate.TotalCalls == calls + 1);
        var staged = o.Thread.View(o.Main);
        HostCheck.True(staged.WaitSource == 4 && staged.Wait.Sequence != 0 && o.Thread.Kernel.Inspect(o.Main).State == KernelThreadTable.State.Parking);
        HostCheck.Equal(LinuxSyscallAction.Rejected, LinuxSyscallGateway.Dispatch(ref gate, ref frame).Action);
        o.Park();
        o.Ready();
        if (!o.Thread.Kernel.Table->Current.IsValid)
            HostCheck.Equal(o.Main.Value, o.Thread.Kernel.Dispatch().Value);
        var completion = o.Thread.Service->CompleteDiskWait(Key, 2);
        HostCheck.Equal(LinuxSyscallAction.Return, completion.Action);
        var returned = LinuxSyscallGateway.CompleteBlocked(ref gate, completion);
        HostCheck.True(returned.Action == LinuxSyscallAction.Return && returned.Value == completion.Value && gate.Status == LinuxSyscallGateStatus.Active && gate.TotalCalls == calls + 1);
        HostCheck.Equal(LinuxSyscallAction.Rejected, LinuxSyscallGateway.CompleteBlocked(ref gate, completion).Action);
        HostCheck.True(gate.TotalCalls == calls + 1 && o.Thread.View(o.Main).WaitSource == 0);
        return returned.Value;
    }

    [HostTest("linux.disk-thread pins each supported deferred output and leaves copied paths and disjoint bytes mutable")]
    private static void OutputKinds()
    {
        foreach (ulong number in new ulong[]
        {
            0,
            17,
            4,
            6
        }

        )
        {
            using Owner o = new();
            uint fd = o.Open();
            o.PutPath("/app/wwwroot/index.html");
            o.Begin(number, number is 4 or 6 ? Path : fd, Output, 5);
            uint bytes = number is 4 or 6 ? 144U : 5U;
            HostCheck.True(!o.May(Output, 1) && !o.May(Output + bytes - 1, 1));
            HostCheck.True(o.May(Output - 1, 1) && o.May(Output + bytes, 1) && o.May(Path, 256));
            o.Park();
            HostCheck.True(!o.May(Output, bytes));
            o.Ready();
            HostCheck.True(!o.May(Output, bytes));
            HostCheck.Equal(number is 4 or 6 ? 0L : 5L, o.Consume());
            HostCheck.True(o.May(Output, bytes));
        }

        using Owner immediate = new();
        uint file = immediate.Open();
        HostCheck.Equal(0L, immediate.Run(5, file, Output));
        HostCheck.True(immediate.May(Output, 144));
        HostCheck.Equal(-38L, immediate.Thread.Call(217, file, Output, 128).Value); // No getdents64 admission.
        HostCheck.True(immediate.May(Output, 128));
        immediate.PutPath("/app/wwwroot/index.html");
        immediate.Begin(2, Path);
        HostCheck.True(immediate.May(Path, 256) && immediate.May(Output, 8192));
    }

    [HostTest("linux.disk-thread holds ready output through actual completion and permits another worker nonoverlapping mutations")]
    private static void OtherWorker()
    {
        using Owner o = new();
        uint fd = o.Open();
        var child = o.Thread.Clone();
        o.Begin(17, fd, Output, 5);
        o.Park();
        HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value);
        HostCheck.True(!o.May(Output, 4096) && o.May(Output + 4096, 4096));
        o.Ready();
        HostCheck.True(!o.May(Output, 5));
        o.Thread.Yield(child);
        HostCheck.Equal(5L, o.Consume());
        HostCheck.True(o.May(Output, 5));
    }

    [HostTest("linux.disk-thread actual memory callback rejects unmap protect discard fixed replace and shrinking while disk output is retained")]
    private static void MappingMutations()
    {
        using Owner o = new();
        uint fd = o.Open();
        var memory = o.Files.Memory;
        const ulong start = LinuxMemoryServices.DynamicStart + 0x10000;
        HostCheck.Equal((long)start, LinuxMemoryServices.Map(memory.State, start, 3 * 4096, 3, 0x100022, ulong.MaxValue, 0));
        o.Thread.UseMemory(memory.Memory);
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard));
        try
        {
            var child = o.Thread.Clone();
            o.Begin(17, fd, start + 4096 + 8, 5);
            o.Park();
            HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value);
            byte[] before = memory.Tables();
            uint backed = memory.State->BackedPages;
            HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(memory.State, start + 4096, 4096));
            HostCheck.Equal(-12L, LinuxMemoryServices.Protect(memory.State, start + 4096, 4096, 1));
            HostCheck.Equal(-12L, LinuxMemoryServices.Advise(memory.State, start + 4096, 4096, 4));
            HostCheck.Equal(-12L, LinuxMemoryServices.Map(memory.State, start + 4096, 4096, 3, 0x32, ulong.MaxValue, 0));
            HostCheck.Equal(-12L, LinuxMemoryServices.Remap(memory.State, start, 3 * 4096, 4096, 0));
            HostCheck.True(before.AsSpan().SequenceEqual(memory.Tables()));
            HostCheck.Equal(backed, memory.State->BackedPages);
            HostCheck.Equal(0L, LinuxMemoryServices.Protect(memory.State, start, 4096, 1));
            HostCheck.Equal(0L, LinuxMemoryServices.Advise(memory.State, start + 8192, 4096, 4));
            HostCheck.True(!LinuxMemoryServices.DetachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard));
            var wait = o.Thread.View(o.Main).Wait;
            HostCheck.True(o.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, wait).Succeeded);
            o.Thread.Yield(child);
            HostCheck.Equal(-4L, o.Consume());
            HostCheck.True(o.May(start + 4096, 4096));
            HostCheck.Equal(0L, LinuxMemoryServices.Unmap(memory.State, start + 4096, 4096));
        }
        finally
        {
            HostCheck.True(LinuxMemoryServices.DetachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard));
        }
    }

    [HostTest("linux.disk-thread cancel revokes the mapping pin before orphan drain and prevents a late user copy")]
    private static void Interrupted()
    {
        foreach (bool ready in new[]
        {
            false,
            true
        }

        )
        {
            using Owner o = new();
            uint fd = o.Open();
            uint writes = o.Thread.Writes;
            o.Begin(17, fd, Output, 5);
            o.Park();
            if (ready)
                o.Ready();
            if (!ready)
                HostCheck.True(o.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, o.Thread.View(o.Main).Wait).Succeeded);
            if (ready)
            {
                // Woken disk completion has already won. A later signal cannot
                // replace it or grant an early pin release.
                HostCheck.Equal(KernelThreadTable.Status.AlreadyCompleted, o.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, o.Thread.View(o.Main).Wait).Code);
                HostCheck.True(!o.May(Output, 5));
                HostCheck.Equal(5L, o.Consume());
            }
            else
            {
                HostCheck.Equal(-4L, o.Consume());
                HostCheck.True(o.May(Output, 5));
                for (int i = 0; i < 64; i++)
                {
                    o.Storage.Tick();
                    HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2));
                }

                HostCheck.Equal(writes, o.Thread.Writes);
            }
        }
    }

    [HostTest("linux.disk-thread closed original fd retains pin until its error consumes without a late copy")]
    private static void ClosePending()
    {
        using Owner o = new();
        uint fd = o.Open();
        var child = o.Thread.Clone();
        uint writes = o.Thread.Writes;
        o.Begin(17, fd, Output, 5);
        o.Park();
        HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, o.Thread.DescriptorCall(3, fd).Value);
        HostCheck.True(!o.May(Output, 5));
        o.Ready();
        o.Thread.Yield(child);
        HostCheck.Equal(-9L, o.Consume());
        HostCheck.True(o.May(Output, 5));
        HostCheck.Equal(writes, o.Thread.Writes);
    }

    [HostTest("linux.disk-thread initial writable prefix preserves short reads and EOF while rejecting newly mapped output authority")]
    private static void InitialPrefix()
    {
        using Owner o = new();
        uint fd = o.Open();
        ulong tail = Base + LinuxThreadServicesTests.UserBytes - 4;
        HostCheck.Equal(4L, o.Run(17, fd, tail, 32, 696)); // Exact4B remaining, inaccessible requested tail.
        uint writes = o.Thread.Writes;
        HostCheck.Equal(-14L, o.Run(17, fd, tail, 32, 695));
        HostCheck.Equal(writes, o.Thread.Writes);
        HostCheck.Equal(0L, o.Run(17, fd, 0, 8192, 700));
        HostCheck.Equal(-14L, o.Run(4, Path, tail));
        HostCheck.Equal(writes, o.Thread.Writes);
        var memory = o.Files.Memory;
        o.Thread.UseMemory(memory.Memory);
        const ulong absent = LinuxMemoryServices.DynamicStart + 0x20000;
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard));
        try
        {
            var child = o.Thread.Clone();
            writes = o.Thread.Writes;
            o.Begin(17, fd, absent, 5);
            o.Park();
            HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value);
            HostCheck.True(o.May(absent, 4096));
            HostCheck.Equal((long)absent, LinuxMemoryServices.Map(memory.State, absent, 4096, 3, 0x100022, ulong.MaxValue, 0));
            new Span<byte>(memory.At(absent), 4096).Fill(0xa5);
            o.Ready();
            o.Thread.Yield(child);
            HostCheck.Equal(-14L, o.Consume());
            HostCheck.Equal(writes, o.Thread.Writes);
            HostCheck.True(new ReadOnlySpan<byte>(memory.At(absent), 4096).IndexOfAnyExcept((byte)0xa5) < 0);
        }
        finally
        {
            HostCheck.True(LinuxMemoryServices.DetachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard));
        }
    }

    [HostTest("linux.disk-thread copy ceiling pins at most8192 and zero requested bytes create no output pin")]
    private static void OutputBounds()
    {
        using Owner o = new();
        uint fd = o.Open();
        o.Begin(17, fd, Output, 65536);
        HostCheck.True(!o.May(Output + 8191, 1) && o.May(Output + 8192, 1));
        o.Park();
        o.Ready();
        HostCheck.Equal(700L, o.Consume());
        o.Begin(17, fd, 0, 0);
        HostCheck.True(o.May(Output, 8192));
        o.Park();
        o.Ready();
        HostCheck.Equal(0L, o.Consume());
    }

    [HostTest("linux.disk-thread process exit keeps output pinned until actual cancellation commit and permits safe orphan cleanup")]
    private static void ExitPending()
    {
        using Owner o = new();
        uint fd = o.Open();
        uint writes = o.Thread.Writes;
        o.Begin(17, fd, Output, 5);
        HostCheck.True(o.Thread.Service->Shutdown(Key, 2, 7));
        HostCheck.True(!o.May(Output, 5));
        o.Park();
        HostCheck.True(ThreadCurrent(o));
        HostCheck.True(o.Thread.Service->CommitExit(Key, 2, o.Thread.Kernel.Capture(o.Main), out _));
        HostCheck.True(o.May(Output, 5));
        o.Thread.Kernel.Confirm();
        HostCheck.True(o.Thread.Service->Reap(Key, 2, o.Main, o.Thread.Kernel.SupervisorRsp, out _));
        for (int i = 0; i < 64; i++)
        {
            o.Storage.Tick();
            HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2));
        }

        HostCheck.Equal(writes, o.Thread.Writes);
    }

    [HostTest("linux.disk-thread early completion remains pinned until real Parking capture and actual copy")]
    private static void CompletionBeforePark()
    {
        using Owner o = new();
        uint fd = o.Open();
        o.Begin(17, fd, Output, 5);
        HostCheck.Equal(KernelThreadTable.State.Parking, o.Thread.Kernel.Inspect(o.Main).State);
        o.Ready();
        HostCheck.True(!o.May(Output, 5));
        HostCheck.True(o.Thread.Service->CommitDiskWait(Key, 2, o.Thread.Kernel.Capture(o.Main), out bool handoff) && !handoff);
        HostCheck.Equal(KernelThreadTable.State.Running, o.Thread.Kernel.Inspect(o.Main).State);
        HostCheck.Equal(5L, o.Consume());
        HostCheck.True(o.May(Output, 5));
    }

    [HostTest("linux.disk-thread owner SIGTERM interrupts source4 without replacing an earlier completion or issuing a late copy")]
    private static void OwnerSignal()
    {
        foreach (bool ready in new[]
        {
            false,
            true
        }

        )
        {
            using Owner o = new(signals: true);
            var m = o.Thread;
            uint fd = o.Open();
            *(Signals.Disposition*)m.At(Path) = new()
            {
                Handler = Base + 0x180,
                Flags = 0x04000004,
                Restorer = Base + 0x200
            };
            HostCheck.Equal(0L, m.Call(13, 15, Path, 0, 8).Value);
            m.Put64(Path, 0);
            HostCheck.Equal(0L, m.Call(14, 2, Path, 0, 8).Value);
            *(ushort*)m.At(Base + 0xfe) = 0x050f;
            uint writes = m.Writes;
            o.Begin(17, fd, Output, 5);
            o.Park();
            if (ready)
                o.Ready();
            HostCheck.True(m.Service->RequestTermination(Key, 2, o.Main));
            HostCheck.True(!o.May(Output, 5));
            HostCheck.Equal(ready ? 5L : -4L, o.Consume());
            HostCheck.True(o.May(Output, 5));
            HostCheck.Equal(ready ? writes + 1 : writes, m.Writes);
            HostCheck.Equal(Signals.Delivery.Delivered, m.Service->DeliverPendingSignals(Key, 2));
            ulong frame = m.CurrentImage()->Frame.Rsp;
            HostCheck.Equal(15U, m.Read32(frame + 312));
            HostCheck.Equal(128U, m.Read32(frame + 320));
            HostCheck.Equal(0U, m.Read32(frame + 328));
            m.CurrentImage()->Frame.Rsp = frame + 8;
            HostCheck.Equal(LinuxSyscallAction.RestoreUser, m.Call(15).Action);
        }
    }

    private static bool ThreadCurrent(Owner o) => o.Thread.Kernel.Table->Current.Value == o.Main.Value;
}
