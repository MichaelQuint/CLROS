using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe class LinuxWritableDiskThreadServicesTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, User = LinuxThreadServicesTests.UserBase;
    private const ulong Path = User + 0x1200, Vector = User + 0x1400, Output = User + 0x2000, Payload = User + 0x3000;

    [HostTest("writable source5 actual waits drive all28 measured descriptor calls with original wire values and independent file bytes")]
    private static void MechanicalCalls()
    {
        using var f = new Fixture(); byte[] elf = OS.Tooling.LinuxWritableDiskProofFixture.CreateElf();
        elf.CopyTo(new Span<byte>(f.Thread.At(User), elf.Length)); int completed = 0;
        foreach (var call in OS.Tooling.LinuxWritableDiskProofFixture.Calls())
        {
            if (call.Number is 1 or 231) continue;
            HostCheck.Equal(call.Result, f.Run(call.Number, call.A0, call.A1, call.A2, call.A3, call.A4, call.A5)); completed++;
            if (call.Role == "read.reopened")
            {
                byte[] expected = OS.Tooling.LinuxWritableDiskProofFixture.ExpectedPayload();
                HostCheck.True(new ReadOnlySpan<byte>(f.Thread.At(OS.Tooling.LinuxWritableDiskProofFixture.Output), expected.Length).SequenceEqual(expected));
            }
        }
        HostCheck.True(completed == 28 && f.Parked > 8 && !f.Disk->RequestActive && !f.Provider->RequestActive);
    }

    [HostTest("writable source5 pins original read output through early readiness and consumes only after real Parking capture")]
    private static void EarlyRead()
    {
        using var f = new Fixture(); uint fd = f.Open(); f.Begin(17, fd, Output, 16);
        HostCheck.True(!f.May(Output, 16) && f.May(Output - 1, 1) && f.May(Output + 16, 1));
        f.Ready(); HostCheck.Equal(KernelThreadTable.State.Parking, f.Thread.Kernel.Inspect(f.Main).State);
        HostCheck.True(!f.May(Output, 16));
        HostCheck.True(f.Thread.Service->CommitWritableDiskWait(Key, 2, f.Thread.Kernel.Capture(f.Main), out bool handoff) && !handoff);
        HostCheck.Equal(16L, f.Consume()); HostCheck.True(f.May(Output, 16));
        HostCheck.Equal(0L, f.Run(8, fd, 0, 1));
    }

    [HostTest("writable source5 fresh fstat pins its exact cross page wire through early readiness without advancing the file cursor")]
    private static void StatEarlyReady()
    {
        using var f = new Fixture(); uint fd = f.Open("/data/stat5.bin", W.Create | W.Exclusive | 2);
        f.PutWrite(new byte[1000]); HostCheck.Equal(1000L, f.Run(328, fd, Vector, 1, 7, 0, W.RwfNoAppend));
        HostCheck.Equal(23L, f.Run(8, fd, 23));
        const ulong address = Output + 4096 - 80;
        var bytes = new Span<byte>(f.Thread.At(address - 1), 146); bytes.Fill(0xa5); uint copies = f.Thread.Writes;
        f.Begin(5, fd, address);
        HostCheck.True(!f.May(address, 144) && !f.May(address + 80, 1) &&
            f.May(address - 1, 1) && f.May(address + 144, 1));
        f.Ready(); HostCheck.Equal(KernelThreadTable.State.Parking, f.Thread.Kernel.Inspect(f.Main).State);
        HostCheck.Equal(copies, f.Thread.Writes); HostCheck.True(bytes.IndexOfAnyExcept((byte)0xa5) < 0);
        HostCheck.True(f.Thread.Service->CommitWritableDiskWait(Key, 2, f.Thread.Kernel.Capture(f.Main), out bool handoff) && !handoff);
        HostCheck.Equal(0L, f.Consume()); HostCheck.True(f.May(address, 144));
        var wire = bytes.Slice(1, 144);
        HostCheck.True(BinaryPrimitives.ReadUInt64LittleEndian(wire[8..]) != 0);
        HostCheck.Equal(0x8000U, BinaryPrimitives.ReadUInt32LittleEndian(wire[24..]) & 0xf000U);
        HostCheck.Equal(1007L, BinaryPrimitives.ReadInt64LittleEndian(wire[48..]));
        // This original fixture has one512B sector per allocated cluster.
        HostCheck.Equal(2L, BinaryPrimitives.ReadInt64LittleEndian(wire[64..]));
        HostCheck.True(bytes[0] == 0xa5 && bytes[145] == 0xa5 && f.Thread.Writes == copies + 1);
        HostCheck.Equal(23L, f.Run(8, fd, 0, 1));
    }

    [HostTest("writable source5 fstat cancellation and fd reuse revoke metadata output without late copy or cursor changes")]
    private static void StatCancellationAndReuse()
    {
        foreach (bool reuse in new[] { false, true })
        {
            using var f = new Fixture(); uint fd = f.Open(); var bytes = new Span<byte>(f.Thread.At(Output), 144); bytes.Fill(0xa5);
            OSHostHandle child = reuse ? f.Thread.Clone() : default; uint copies = f.Thread.Writes;
            f.Begin(5, fd, Output); f.Park(); HostCheck.True(!f.May(Output, 144));
            if (reuse)
            {
                HostCheck.Equal(child.Value, f.Thread.Kernel.Dispatch().Value); HostCheck.Equal(0L, f.Run(3, fd));
                f.PutPath("/app/fixture.data"); HostCheck.Equal((long)fd, f.Run(2, Path, 0x88100));
                f.Ready(); f.Thread.Yield(child);
            }
            else HostCheck.True(f.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, f.Thread.View(f.Main).Wait).Succeeded);
            HostCheck.Equal(reuse ? -9L : -4L, f.Consume()); HostCheck.True(f.May(Output, 144)); f.Drain();
            HostCheck.True(f.Thread.Writes == copies && bytes.IndexOfAnyExcept((byte)0xa5) < 0);
            HostCheck.True(f.Thread.Service->CurrentImage(Key, 2, out _));
            if (!reuse) HostCheck.Equal(0L, f.Run(8, fd, 0, 1));
        }
    }

    [HostTest("writable source5 interrupted pending read revokes original output without restart or late copy and drains independently")]
    private static void InterruptedRead()
    {
        using var f = new Fixture(); uint fd = f.Open(); uint copies = f.Thread.Writes;
        f.Begin(17, fd, Output, 16); f.Park();
        HostCheck.True(f.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, f.Thread.View(f.Main).Wait).Succeeded);
        HostCheck.Equal(-4L, f.Consume()); HostCheck.True(f.May(Output, 16));
        HostCheck.True(f.Thread.Service->CurrentImage(Key, 2, out _)); // No restart decision may replay work.
        f.Drain(); HostCheck.Equal(copies, f.Thread.Writes); HostCheck.Equal(0L, f.Run(8, fd, 0, 1));
    }

    [HostTest("writable source5 already-ready write and flush win a prior signal completion without mutation replay")]
    private static void ReadyMutationWins()
    {
        using var f = new Fixture(); uint fd = f.Open(flags: 2); byte[] value = [7, 8, 9]; f.PutWrite(value);
        foreach (ulong number in new ulong[] { 328, 74 })
        {
            f.Begin(number, fd, number == 328 ? Vector : 0, number == 328 ? 1UL : 0, 0, 0, number == 328 ? W.RwfNoAppend : 0);
            f.Park(); f.AdapterReady(); ulong writes = f.Storage.Device.State->SubmittedBytes;
            HostCheck.True(f.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, f.Thread.View(f.Main).Wait).Succeeded);
            HostCheck.Equal(number == 328 ? 3L : 0L, f.Consume());
            HostCheck.Equal(writes, f.Storage.Device.State->SubmittedBytes);
            HostCheck.True(f.Thread.Service->CurrentImage(Key, 2, out _));
        }
    }

    [HostTest("writable source5 peer close and reused fd retain original read pin until EBADF with no late output")]
    private static void CloseAndReuse()
    {
        using var f = new Fixture(); uint fd = f.Open(); var child = f.Thread.Clone(); uint copies = f.Thread.Writes;
        f.Begin(17, fd, Output, 16); f.Park(); HostCheck.Equal(child.Value, f.Thread.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, f.Run(3, fd)); f.PutPath("/app/fixture.data"); HostCheck.Equal((long)fd, f.Run(2, Path, 0x88100));
        HostCheck.True(!f.May(Output, 16)); f.Ready(); f.Thread.Yield(child);
        HostCheck.Equal(-9L, f.Consume()); HostCheck.True(f.May(Output, 16)); HostCheck.Equal(copies, f.Thread.Writes);
    }

    [HostTest("writable source5 failed user copy leaves original global cursor unchanged and releases only its captured pin")]
    private static void CopyFailure()
    {
        using var f = new Fixture(); uint fd = f.Open(); f.Begin(0, fd, Output, 16); f.Park(); f.Ready();
        f.Thread.FailWrite(Output); HostCheck.Equal(-14L, f.Consume()); f.Thread.FailWrite(ulong.MaxValue);
        HostCheck.True(f.May(Output, 16)); HostCheck.Equal(0L, f.Run(8, fd, 0, 1));
        HostCheck.Equal(16L, f.Run(0, fd, Output, 16)); HostCheck.Equal(16L, f.Run(8, fd, 0, 1));
    }

    [HostTest("writable source5 process exit revokes copy only at actual exit commit and keeps orphan backing until drain")]
    private static void ExitPending()
    {
        using var f = new Fixture(); uint fd = f.Open(); uint copies = f.Thread.Writes;
        f.Begin(17, fd, Output, 16); HostCheck.True(f.Thread.Service->Shutdown(Key, 2, 7));
        HostCheck.True(!f.May(Output, 16)); f.Park();
        HostCheck.True(f.Thread.Service->CommitExit(Key, 2, f.Thread.Kernel.Capture(f.Main), out _));
        HostCheck.True(f.May(Output, 16)); f.Thread.Kernel.Confirm();
        HostCheck.True(f.Thread.Service->Reap(Key, 2, f.Main, f.Thread.Kernel.SupervisorRsp, out _));
        HostCheck.True(!f.Thread.Service->Release(Key, 2)); f.Drain(); HostCheck.Equal(copies, f.Thread.Writes);
    }

    [HostTest("writable source5 copied scheduler inventory authenticates retained queues supervisor and exact live worker backing without effects")]
    private static void SchedulerInventory()
    {
        using var f = new Fixture(); var table = f.Thread.Kernel.Table; var before = f.Thread.Kernel.Snapshot();
        HostCheck.Equal(15U, table->RetainedExtentCount(KernelThreadTableTests.Key, 2));
        var expected = new (ulong Start, ulong Bytes)[] {
            ((ulong)table, (ulong)sizeof(KernelThreadTable)), ((ulong)f.Thread.Kernel.Slots, 3UL * (ulong)sizeof(KernelThreadSlot)),
            ((ulong)f.Thread.Kernel.Ready, 24), ((ulong)f.Thread.Kernel.Deadlines, 3UL * (ulong)sizeof(DeadlineQueue.Entry)),
            (f.Thread.Kernel.Owner.ContextArena, f.Thread.Kernel.Owner.ContextArenaBytes), (f.Thread.Kernel.Code, 0x10000),
            (f.Thread.Kernel.Owner.SwitchStackGuardStart, f.Thread.Kernel.Owner.SwitchStackBytes),
            ((ulong)f.Thread.Kernel.Supervisor.Context, f.Thread.Kernel.Supervisor.ContextBytes),
            (f.Thread.Kernel.Supervisor.StackGuardStart, f.Thread.Kernel.Supervisor.StackBytes),
            ((ulong)f.Thread.Kernel.Context(0), 688), (f.Thread.Kernel.Stack(0), 0x3000) };
        for (uint i = 0; i < expected.Length; i++)
        {
            HostCheck.Equal(KernelThreadTable.Status.Ok, table->InspectRetainedExtent(KernelThreadTableTests.Key, 2, i, out ulong start, out ulong bytes));
            HostCheck.True(start == expected[i].Start && bytes == expected[i].Bytes);
        }
        for (uint i = 11; i < 15; i++)
        { HostCheck.Equal(KernelThreadTable.Status.Ok, table->InspectRetainedExtent(KernelThreadTableTests.Key, 2, i, out ulong start, out ulong bytes)); HostCheck.True(start == 0 && bytes == 0); }
        HostCheck.Equal(0U, table->RetainedExtentCount(1, 2)); HostCheck.Equal(0U, table->RetainedExtentCount(KernelThreadTableTests.Key, 0x202));
        var copied = *table; HostCheck.Equal(0U, copied.RetainedExtentCount(KernelThreadTableTests.Key, 2));
        HostCheck.Equal(KernelThreadTable.Status.InvalidStorage, table->InspectRetainedExtent(KernelThreadTableTests.Key, 2, 15, out ulong bad, out ulong length));
        HostCheck.True(bad == 0 && length == 0); f.Thread.Kernel.Same(before);
    }

    [HostTest("writable source5 admission rejects provider input over actual scheduler ready and deadline backing before thread attachment")]
    private static void SchedulerAlias()
    {
        using var f = new Fixture(aliasScheduler: true);
        HostCheck.True(f.Rejected && f.Thread.Service->ActiveThreads == 0 && !f.Disk->RequestActive);
    }

    [HostTest("writable attachment preserves genuine anonymous futex wait wake and completion on the actual admitted user pool")]
    private static void UserPoolFutex()
    {
        using var f = new Fixture(); var memory = f.Files.Memory; f.Thread.UseMemory(memory.Memory);
        const ulong address = LinuxMemoryServices.DynamicStart + 0x20000;
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(memory.State, f.Thread.Service, &LinuxFutexTests.MemoryGuard));
        try
        {
            HostCheck.Equal((long)address, LinuxMemoryServices.Map(memory.State, address, 4096, 3, 0x100022, ulong.MaxValue, 0));
            *(uint*)memory.At(address) = 17; var child = f.Thread.Clone();
            HostCheck.Equal(LinuxSyscallAction.Blocked, f.Thread.Call(202, address, 128, 17).Action);
            LinuxFutexTests.Park(f.Thread, f.Main); HostCheck.Equal(child.Value, f.Thread.Kernel.Dispatch().Value);
            HostCheck.Equal(1L, f.Thread.Call(202, address, 129, 1).Value); f.Thread.Yield(child);
            HostCheck.Equal(f.Main.Value, f.Thread.Kernel.Dispatch().Value);
            var result = f.Thread.Service->CompleteFutexWait(Key, 2);
            HostCheck.True(result.Action == LinuxSyscallAction.Return && result.Value == 0 && f.May(address, 4096));
        }
        finally { HostCheck.True(LinuxMemoryServices.DetachMutationGuard(memory.State, f.Thread.Service, &LinuxFutexTests.MemoryGuard)); }
    }

    [HostTest("writable gateway admits measured positioned write sync and unlink through genuine source5 blocked completions")]
    private static void GatewayCalls()
    {
        using var f = new Fixture(); uint fd = f.Open("/data/gateway.bin", 2 | W.Create | W.Exclusive); f.PutWrite([31, 32, 33]);
        byte* scratch = stackalloc byte[64]; LinuxSyscallGateState gate = default;
        HostCheck.Equal(LinuxSyscallGateStatus.Active, LinuxSyscallGateway.Begin(ref gate, new()
        {
            UserRoot = 0x200000, MaximumCalls = 16, MaximumWrite = 64, Scratch = scratch, ScratchBytes = 64,
            FirmwareExited = true, OwnedBoundaryValidated = true, EnableStartup = true, EnableDescriptors = true, EnableThreads = true
        }, f.Thread.GatewayAccess));
        foreach (ulong number in new ulong[] { 328, 74, 75, 87 })
        {
            if (number == 87) { HostCheck.Equal(0L, f.Run(3, fd)); f.PutPath("/data/gateway.bin"); }
            var image = f.Thread.CurrentImage(); var frame = image->Frame;
            frame.Number = number; frame.Argument0 = number == 87 ? Path : fd; frame.Argument1 = number == 328 ? Vector : 0;
            frame.Argument2 = number == 328 ? 1UL : 0; frame.Argument3 = frame.Argument4 = 0; frame.Argument5 = number == 328 ? W.RwfNoAppend : 0;
            image->Frame = frame; ulong calls = gate.TotalCalls;
            HostCheck.Equal(LinuxSyscallAction.Blocked, LinuxSyscallGateway.Dispatch(ref gate, ref frame).Action);
            HostCheck.True(gate.Status == LinuxSyscallGateStatus.Blocked && gate.TotalCalls == calls + 1 && f.Thread.View(f.Main).WaitSource == 5);
            f.Park(); f.Ready();
            if (!f.Thread.Kernel.Table->Current.IsValid) HostCheck.Equal(f.Main.Value, f.Thread.Kernel.Dispatch().Value);
            var completion = f.Thread.Service->CompleteWritableDiskWait(Key, 2);
            var done = LinuxSyscallGateway.CompleteBlocked(ref gate, completion);
            HostCheck.True(done.Action == LinuxSyscallAction.Return && done.Value == (number == 328 ? 3 : 0) && gate.TotalCalls == calls + 1);
            HostCheck.Equal(LinuxSyscallAction.Rejected, LinuxSyscallGateway.CompleteBlocked(ref gate, completion).Action);
        }
    }

    [HostTest("writable source5 failed actual flush returns EIO then retains dirty provider tree after real exit and refuses release")]
    private static void DirtyFlush()
    {
        using var f = new Fixture(); uint fd = f.Open(flags: 2); f.PutWrite([71, 72]); f.Begin(328, fd, Vector, 1, 5, 0, W.RwfNoAppend); f.Park();
        bool failed = false;
        for (int i = 0; i < 100000 && !failed; i++)
        {
            HostCheck.True(f.Thread.Service->PollWritableDiskReadiness(Key, 2));
            if (!f.Storage.Device.Hardware->Pending) continue;
            if (f.Storage.Device.Hardware->Type == 4) { f.Storage.Device.Complete(status: 1); failed = true; }
            else f.Storage.Device.Complete();
        }
        HostCheck.True(failed); f.RetainedFailure = true; f.Ready(); HostCheck.Equal(-5L, f.Consume());
        HostCheck.True(f.Storage.State->Dirty && f.Disk->RequestActive && f.Provider->RequestActive && !F.DetachWritableDisk(f.Files.State));
        HostCheck.True(f.Thread.Service->CurrentImage(Key, 2, out _));
    }

    [HostTest("writable source5 actual mapping guard preserves a retained output lifetime and never expands a previously absent copy ceiling")]
    private static void MappingLifetime()
    {
        foreach (bool initial in new[] { true, false })
        {
            using var f = new Fixture(); uint fd = f.Open(); var memory = f.Files.Memory; f.Thread.UseMemory(memory.Memory);
            const ulong address = LinuxMemoryServices.DynamicStart + 0x20000;
            HostCheck.True(LinuxMemoryServices.AttachMutationGuard(memory.State, f.Thread.Service, &LinuxFutexTests.MemoryGuard));
            bool detached = false;
            try
            {
                if (initial) HostCheck.Equal((long)address, LinuxMemoryServices.Map(memory.State, address, 4096, 3, 0x100022, ulong.MaxValue, 0));
                var child = f.Thread.Clone(); uint copies = f.Thread.Writes; f.Begin(17, fd, address, 16); f.Park();
                HostCheck.Equal(child.Value, f.Thread.Kernel.Dispatch().Value);
                if (initial)
                {
                    HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(memory.State, address, 4096));
                    HostCheck.Equal(-12L, LinuxMemoryServices.Protect(memory.State, address, 4096, 1));
                    HostCheck.Equal(-12L, LinuxMemoryServices.Map(memory.State, address, 4096, 3, 0x32, ulong.MaxValue, 0));
                }
                else
                {
                    HostCheck.True(f.May(address, 4096));
                    HostCheck.Equal((long)address, LinuxMemoryServices.Map(memory.State, address, 4096, 3, 0x100022, ulong.MaxValue, 0));
                }
                new Span<byte>(memory.At(address), 4096).Fill(0xa5); f.Ready();
                if (initial) HostCheck.Equal(-12L, LinuxMemoryServices.Protect(memory.State, address, 4096, 1));
                // This model resolves dynamic pages for admission/futex but
                // deliberately copies only its original static user window.
                // Both original copy capability and captured ceiling must hold.
                f.Thread.Yield(child); HostCheck.Equal(-14L, f.Consume());
                HostCheck.Equal(copies, f.Thread.Writes);
                HostCheck.True(new ReadOnlySpan<byte>(memory.At(address), 4096).IndexOfAnyExcept((byte)0xa5) < 0);
                HostCheck.True(f.May(address, 4096)); HostCheck.Equal(0L, LinuxMemoryServices.Protect(memory.State, address, 4096, 1));
            }
            finally { detached = LinuxMemoryServices.DetachMutationGuard(memory.State, f.Thread.Service, &LinuxFutexTests.MemoryGuard); }
            HostCheck.True(detached);
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly LinuxThreadServicesTests.Model Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files = new(descriptors: 32, descriptions: 32);
        internal readonly ContinuingFat32MutationTests.Fixture Storage = new();
        internal readonly W.State* Provider = (W.State*)NativeMemory.AllocZeroed((nuint)sizeof(W.State));
        private readonly W.Descriptor* descriptors = (W.Descriptor*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Descriptor));
        private readonly W.Description* descriptions = (W.Description*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Description));
        private readonly byte* input;
        private readonly W.ProcessSlot* processes = (W.ProcessSlot*)NativeMemory.AllocZeroed(4, (nuint)sizeof(W.ProcessSlot));
        private readonly W.RangeLock* locks = (W.RangeLock*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.RangeLock));
        private readonly W.RangeLock* scratch = (W.RangeLock*)NativeMemory.AllocZeroed(65, (nuint)sizeof(W.RangeLock));
        internal readonly F.WritableDiskState* Disk = (F.WritableDiskState*)NativeMemory.AllocZeroed((nuint)sizeof(F.WritableDiskState));
        private readonly F.WritableDiskBinding* bindings = (F.WritableDiskBinding*)NativeMemory.AllocZeroed(32, (nuint)sizeof(F.WritableDiskBinding));
        internal readonly OSHostHandle Main;
        internal uint Parked;
        internal bool RetainedFailure;
        internal readonly bool Rejected;
        private readonly bool borrowedInput;
        private ulong Owner => (ulong)Thread.Service;
        internal Fixture(bool aliasScheduler = false)
        {
            borrowedInput = aliasScheduler; input = aliasScheduler ? (byte*)Thread.Kernel.Ready : (byte*)NativeMemory.AllocZeroed(W.IoBytes);
            if (aliasScheduler) new Span<byte>(input, (int)W.IoBytes).Clear(); // No live workers; model of an invalid offered borrow.
            Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 48)] = 0xff;
            Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 49)] = 0xff; Storage.Mount();
            HostCheck.True(W.Start(Provider, new() { Engine = Storage.State, Epoch = Storage.Input.Epoch, Descriptors = descriptors,
                Descriptions = descriptions, DescriptorCount = 32, DescriptionCount = 32, Input = input, InputBytes = W.IoBytes, ExclusiveOwnerValidated = true }));
            HostCheck.True(W.EnableLocks(Provider, new() { Processes = processes, ProcessCount = 4, Locks = locks, LockCount = 32,
                Scratch = scratch, ScratchCount = 65, PrimaryPid = 101 }));
            Files.Admission.User = Thread.DescriptorAccess; Files.Admission.CopyToUser = Thread.UserCopyOut; Files.Begin();
            HostCheck.True(F.AttachWritableDisk(Files.State, Disk, new() { Provider = Provider, Process = W.PrimaryProcess(Provider),
                Bindings = bindings, BindingCount = 32, Epoch = Storage.Input.Epoch, Search = new(2, 64, 256), FirmwareExited = true, SingleOwnerValidated = true }));
            if (aliasScheduler)
            {
                uint calls = Storage.Device.Hardware->Calls; var before = Thread.Kernel.Snapshot();
                HostCheck.True(!Thread.Service->EnablePipes(Key, 2, Files.State)); Thread.Kernel.Same(before);
                HostCheck.Equal(calls, Storage.Device.Hardware->Calls); Rejected = true; return;
            }
            HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State)); Main = Thread.Start();
            HostCheck.Equal(Main.Value, Thread.Kernel.Dispatch().Value);
        }
        internal void PutPath(string path) => Encoding.ASCII.GetBytes(path + '\0').CopyTo(new Span<byte>(Thread.At(Path), path.Length + 1));
        internal void PutWrite(byte[] bytes)
        { bytes.CopyTo(new Span<byte>(Thread.At(Payload), bytes.Length)); *(ulong*)Thread.At(Vector) = Payload; *(ulong*)(Thread.At(Vector) + 8) = (ulong)bytes.Length; }
        internal bool May(ulong start, ulong bytes) => Thread.Service->MayChangeUserRange(Key, 2, start, bytes);
        private LinuxSyscallResult Call(ulong number, ulong a0, ulong a1, ulong a2, ulong a3, ulong a4, ulong a5)
        {
            var image = Thread.CurrentImage(); var frame = image->Frame;
            frame.Number = number; frame.Argument0 = a0; frame.Argument1 = a1; frame.Argument2 = a2;
            frame.Argument3 = a3; frame.Argument4 = a4; frame.Argument5 = a5; image->Frame = frame;
            var result = Thread.Service->DispatchDescriptor(Key, 2, frame);
            if (result.Action == LinuxSyscallAction.Return) image->Value = result.Value;
            return result;
        }
        internal void Begin(ulong number, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
            => HostCheck.Equal(LinuxSyscallAction.Blocked, Call(number, a0, a1, a2, a3, a4, a5).Action);
        internal long Run(ulong number, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        {
            var result = Call(number, a0, a1, a2, a3, a4, a5);
            if (result.Action == LinuxSyscallAction.Return) return result.Value;
            HostCheck.Equal(LinuxSyscallAction.Blocked, result.Action); Park(); Ready(); return Consume();
        }
        internal uint Open(string path = "/data/DATA.BIN", uint flags = 0)
        { PutPath(path); long fd = Run(2, Path, flags, (flags & W.Create) != 0 ? 0x180UL : 0); HostCheck.True(fd >= 3); return (uint)fd; }
        internal void Park()
        {
            HostCheck.True(Thread.Service->CommitWritableDiskWait(Key, 2, Thread.Kernel.Capture(Main), out bool handoff)); Parked++;
            if (handoff) Thread.Kernel.Confirm();
        }
        private void CompleteHardware() { if (Storage.Device.Hardware->Pending) Storage.Device.Complete(); }
        internal void Ready()
        {
            for (int i = 0; i < 100000; i++)
            {
                HostCheck.True(Thread.Service->PollWritableDiskReadiness(Key, 2)); CompleteHardware();
                if (Thread.Kernel.Inspect(Main).Completion != KernelThreadTable.Completion.None) return;
            }
            throw new InvalidOperationException("source5 did not complete");
        }
        internal void AdapterReady()
        {
            for (int i = 0; i < 100000; i++)
            {
                var result = F.PumpWritableDiskOne(Files.State, Owner); CompleteHardware();
                if (result.Progress == F.WritableDiskProgress.Ready) return;
                HostCheck.True(result.Progress != F.WritableDiskProgress.Rejected);
            }
            throw new InvalidOperationException("adapter did not complete");
        }
        internal long Consume()
        {
            if (!Thread.Kernel.Table->Current.IsValid) HostCheck.Equal(Main.Value, Thread.Kernel.Dispatch().Value);
            var result = Thread.Service->CompleteWritableDiskWait(Key, 2); HostCheck.Equal(LinuxSyscallAction.Return, result.Action); return result.Value;
        }
        internal void Drain()
        {
            for (int i = 0; i < 100000 && Disk->RequestActive; i++)
            { HostCheck.True(Thread.Service->PollWritableDiskReadiness(Key, 2)); CompleteHardware(); }
            HostCheck.True(!Disk->RequestActive);
        }
        public void Dispose()
        {
            try
            {
                HostCheck.True(Thread.Service->Shutdown(Key, 2, 7));
                while (Thread.Service->ActiveThreads != 0)
                {
                    var current = Thread.Kernel.Table->Current;
                    if (!current.IsValid) current = Thread.Kernel.Dispatch();
                    if (Thread.Kernel.Inspect(current).State == KernelThreadTable.State.Parking)
                    {
                        HostCheck.True(Thread.Service->CommitWritableDiskWait(Key, 2, Thread.Kernel.Capture(current), out bool handoff));
                        if (handoff) Thread.Kernel.Confirm(); continue;
                    }
                    HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(current), out _));
                    Thread.Kernel.Confirm(); HostCheck.True(Thread.Service->Reap(Key, 2, current, Thread.Kernel.SupervisorRsp, out _));
                }
                if (RetainedFailure)
                {
                    HostCheck.True(!Thread.Service->Release(Key, 2) && !F.DetachWritableDisk(Files.State) && Disk->RequestActive && Storage.State->Dirty);
                    // Discard this failed host model only after Storage.Dispose's
                    // real fake-device reset; this is not successful retirement.
                    return;
                }
                if (!Rejected) Drain(); HostCheck.True(Thread.Service->Release(Key, 2)); HostCheck.True(F.End(Files.State));
                HostCheck.Equal(W.Progress.Retired, W.Detach(Provider).Phase);
            }
            finally
            {
                Storage.Dispose(); Files.Dispose(); Thread.Dispose(); NativeMemory.Free(Disk); NativeMemory.Free(bindings);
                NativeMemory.Free(Provider); NativeMemory.Free(descriptors); NativeMemory.Free(descriptions); if (!borrowedInput) NativeMemory.Free(input);
                NativeMemory.Free(processes); NativeMemory.Free(locks); NativeMemory.Free(scratch);
            }
        }
    }
}
