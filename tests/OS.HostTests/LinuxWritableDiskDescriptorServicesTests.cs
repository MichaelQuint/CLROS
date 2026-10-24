using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Pal;
using C = OS.Storage.ContinuingFat32Mutation;
using B = OS.Storage.WritableBlockSession;
using W = OS.Kernel.Compatibility.LinuxX64.LinuxContinuingWritableDescriptorServices;
using F = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;

internal static unsafe class LinuxWritableDiskDescriptorServicesTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, User = LinuxThreadServicesTests.UserBase;
    private const ulong Path = User + 0x1200, Output = User + 0x2000;
    private delegate bool RetainTree(F.State* state, ulong address, ulong bytes);
    private static readonly RetainTree Tree = typeof(F).GetMethod("WritableDiskCanRetainStorage", BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<RetainTree>();
    private static byte Value(int i) => unchecked((byte)(i * 17 + 5));

    [HostTest("writable global binding shares package fd namespace and commits only its independent common read cursor")]
    private static void NamespaceAndCursors()
    {
        using var f = new Owner(); f.PutPath("/app/fixture.data"); HostCheck.Equal(3L, f.Core(2, Path, 0x88100));
        uint first = f.Open(), second = f.Open(); HostCheck.True(first == 4 && second == 5);
        HostCheck.Equal(16L, f.Run(f.Call(0, first, Output, 16))); HostCheck.True(f.Bytes(16).SequenceEqual(Enumerable.Range(0, 16).Select(Value)));
        HostCheck.Equal(16L, f.Run(f.Call(8, first, 0, 1)));
        HostCheck.Equal(8L, f.Run(f.Call(17, first, Output, 8, 500))); HostCheck.True(f.Bytes(8).SequenceEqual(Enumerable.Range(500, 8).Select(Value)));
        HostCheck.Equal(16L, f.Run(f.Call(8, first, 0, 1)));
        HostCheck.Equal(8L, f.Run(f.Call(0, second, Output, 8))); HostCheck.True(f.Bytes(8).SequenceEqual(Enumerable.Range(0, 8).Select(Value)));
        HostCheck.Equal(8L, f.Run(f.Call(8, second, 0, 1)));
        HostCheck.Equal(-95L, f.Core(72, first, 0, 0)); HostCheck.Equal(-95L, f.Core(72, first, 1030, 0));
        uint writes = f.Thread.Writes; HostCheck.Equal(-95L, f.Core(0, first, Output, 8)); HostCheck.Equal(writes, f.Thread.Writes);
        var privateHandle = W.Capture(f.Provider, 3).Descriptor;
        HostCheck.Equal(0L, W.Seek(f.Provider, privateHandle, 0, 1).Value);
    }

    [HostTest("writable create reserves global fd and description before media effects while ordinary package and pipe allocations skip them")]
    private static void Reservations()
    {
        using (var f = new Owner())
        {
            f.PutPath("/data/new-global-file.bin"); var call = f.Call(2, Path, 2 | W.Create | W.Exclusive | W.CloseOnExec, 0x180);
            var started = f.Begin(call); HostCheck.Equal(F.WritableDiskProgress.Pending, started.Progress);
            HostCheck.True(f.Disk->RequestActive && !F.Capture(f.Files.State, 3, out _) && f.Files.State->ActiveDescriptors == 3);
            f.PutPath("/app/fixture.data"); HostCheck.Equal(4L, f.Core(2, Path, 0x88100));
            HostCheck.Equal(0L, f.Core(293, Output, F.NonBlock));
            HostCheck.True(f.Thread.Read32(Output) == 5 && f.Thread.Read32(Output + 4) == 6);
            HostCheck.Equal(F.WritableDiskProgress.Ready, f.Ready(started.Token).Progress);
            HostCheck.Equal(3L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, started.Token, call).Value);
            HostCheck.True(F.Capture(f.Files.State, 3, out _) && !f.Disk->RequestActive && f.Storage.State->VolumeRevision > 1);
            HostCheck.Equal(0L, f.Run(f.Call(0, 3, Output, 1)));
        }
        foreach (bool fdFull in new[] { true, false })
        {
            using var f = new Owner(descriptors: fdFull ? 3U : 32U, descriptions: 3);
            f.PutPath("/data/must-not-create.bin"); uint calls = f.Hardware->Calls;
            HostCheck.Equal(fdFull ? -24L : -23L, f.Run(f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180)));
            HostCheck.True(!f.Provider->RequestActive && !f.Disk->RequestActive && f.Hardware->Calls == calls && f.Storage.State->VolumeRevision == 1);
        }
    }

    [HostTest("writable failed final user copy or captured output ceiling cannot advance the common cursor and a fresh read retries exact bytes")]
    private static void FailedCopy()
    {
        using var f = new Owner(); uint fd = f.Open(); new Span<byte>(f.Thread.At(Output), 64).Fill(0xa5);
        f.Thread.FailWrite(Output); HostCheck.Equal(-14L, f.Run(f.Call(0, fd, Output, 37)));
        HostCheck.True(f.Bytes(37).All(value => value == 0xa5)); HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
        f.Thread.FailWrite(0); var call = f.Call(0, fd, Output, 37); var begun = f.Begin(call); f.Ready(begun.Token);
        uint writes = f.Thread.Writes;
        HostCheck.Equal(-14L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call, 8).Value);
        HostCheck.Equal(writes, f.Thread.Writes); HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
        HostCheck.Equal(37L, f.Run(f.Call(0, fd, Output, 37)));
        HostCheck.True(f.Bytes(37).SequenceEqual(Enumerable.Range(0, 37).Select(Value)) && f.Thread.At(Output)[37] == 0xa5);
        HostCheck.Equal(37L, f.Run(f.Call(8, fd, 0, 1)));
    }

    [HostTest("writable closed pending and held reads preserve exact live error waiter and reject late bytes after the global fd is reused")]
    private static void CloseAndReuse()
    {
        foreach (bool held in new[] { false, true })
        {
            using var f = new Owner(); uint fd = f.Open(); var call = f.Call(0, fd, Output, 37); var begun = f.Begin(call);
            if (held) f.Ready(begun.Token); else { f.Pump(); HostCheck.True(f.Hardware->Pending); }
            HostCheck.True(F.Capture(f.Files.State, fd, out var old)); HostCheck.Equal(0L, f.Core(3, fd));
            f.PutPath("/app/fixture.data"); HostCheck.Equal((long)fd, f.Core(2, Path, 0x88100));
            HostCheck.True(!F.IsCurrent(f.Files.State, old)); new Span<byte>(f.Thread.At(Output), 37).Fill(0xa5); uint writes = f.Thread.Writes;
            var ready = f.Ready(begun.Token); HostCheck.True(ready.Worker.Value == f.Worker.Value && ready.Value == -9);
            HostCheck.Equal(-9L, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value);
            HostCheck.True(f.Bytes(37).All(value => value == 0xa5) && f.Thread.Writes == writes && F.Audit(f.Files.State));
            HostCheck.Equal(0L, f.Core(8, fd, 0, 1));
        }
    }

    [HostTest("writable explicit cancellation transfers a pending read to a no wake orphan while known ready open results win live interruption")]
    private static void CancelAndReady()
    {
        using var f = new Owner(); uint fd = f.Open(); var call = f.Call(0, fd, Output, 8); var begun = f.Begin(call);
        f.Pump(); HostCheck.True(f.Hardware->Pending); uint writes = f.Thread.Writes;
        var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true);
        HostCheck.True(cancelled.Progress == F.WritableDiskProgress.Draining && !cancelled.Worker.IsValid && f.Disk->RequestActive);
        HostCheck.Equal(F.WritableDiskProgress.Rejected, F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Progress);
        f.Drain(); HostCheck.Equal(F.WritableDiskProgress.Rejected, F.PollWritableDisk(f.Files.State, f.ThreadOwner, begun.Token).Progress);
        HostCheck.True(f.Thread.Writes == writes); HostCheck.Equal(0L, f.Run(f.Call(8, fd, 0, 1)));
        f.PutPath("/data/DATA.BIN"); call = f.Call(2, Path, 0); begun = f.Begin(call); f.Ready(begun.Token); uint calls = f.Hardware->Calls;
        var winner = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, false);
        HostCheck.True(winner.Progress == F.WritableDiskProgress.Ready && winner.Worker.Value == f.Worker.Value);
        HostCheck.True(F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value >= 3 && f.Hardware->Calls == calls);
        f.PutPath("/data/known-created.bin"); call = f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180); begun = f.Begin(call); f.Ready(begun.Token);
        calls = f.Hardware->Calls; HostCheck.True(f.Storage.State->VolumeRevision > 1 && f.Storage.Device.State->SubmittedBytes > 0);
        winner = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, false);
        HostCheck.True(winner.Progress == F.WritableDiskProgress.Ready && winner.Worker.Value == f.Worker.Value);
        HostCheck.True(F.CompleteWritableDisk(f.Files.State, f.ThreadOwner, f.Worker, begun.Token, call).Value >= 3 && f.Hardware->Calls == calls);
    }

    [HostTest("writable cancelled open reservation stays unavailable until drain and abandoned completed creation is neither published nor replayed")]
    private static void CancelOpenReservation()
    {
        using (var f = new Owner())
        {
            f.PutPath("/data/not-created.bin"); var begun = f.Begin(f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180));
            var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true);
            HostCheck.True(cancelled.Progress == F.WritableDiskProgress.Draining && !cancelled.Worker.IsValid && f.Disk->RequestActive);
            f.PutPath("/app/fixture.data"); HostCheck.Equal(4L, f.Core(2, Path, 0x88100)); f.Drain();
            HostCheck.Equal(3L, f.Core(2, Path, 0x88100));
            HostCheck.True(f.Storage.State->VolumeRevision == 1 && f.Storage.Device.State->SubmittedBytes == 0);
        }
        using (var f = new Owner())
        {
            f.PutPath("/data/abandoned-created.bin"); var begun = f.Begin(f.Call(2, Path, 2 | W.Create | W.Exclusive, 0x180)); f.Ready(begun.Token);
            ulong writes = f.Storage.Device.State->SubmittedBytes, revision = f.Storage.State->VolumeRevision;
            HostCheck.True(writes > 0 && revision > 1);
            var cancelled = F.CancelWritableDisk(f.Files.State, f.ThreadOwner, begun.Token, true); HostCheck.True(!cancelled.Worker.IsValid);
            f.Drain(); HostCheck.True(!F.Capture(f.Files.State, 3, out _)); HostCheck.Equal(3U, f.Open("/data/abandoned-created.bin"));
            HostCheck.True(f.Storage.Device.State->SubmittedBytes == writes && f.Storage.State->VolumeRevision == revision);
        }
    }

    [HostTest("writable root directory and unsupported paths flags mode and legacy stat return explicit errors before unowned effects")]
    private static void FiniteNamespace()
    {
        using var f = new Owner(); uint root = f.Open("/data"); uint calls = f.Hardware->Calls;
        HostCheck.Equal(-21L, f.Run(f.Call(0, root, Output, 1))); HostCheck.Equal(-29L, f.Run(f.Call(8, root, 0, 0)));
        f.PutPath("/data/../private/sentinel"); HostCheck.Equal(-13L, f.Run(f.Call(2, Path, 0)));
        f.PutPath("/data/nested/file.bin"); HostCheck.Equal(-95L, f.Run(f.Call(2, Path, 0)));
        f.PutPath("/data/mode.bin"); HostCheck.Equal(-95L, f.Run(f.Call(2, Path, 2 | W.Create, 0x1ff)));
        HostCheck.Equal(-95L, f.Run(f.Call(2, Path, W.DirectoryOnly)));
        HostCheck.True(f.Hardware->Calls == calls && f.Storage.State->VolumeRevision == 1);
        HostCheck.Equal(-95L, f.Core(5, root, Output)); HostCheck.Equal(-95L, f.Core(72, root, 4, F.NonBlock));
        uint file = f.Open(flags: W.NoFollow | W.CloseOnExec); HostCheck.Equal(1L, f.Core(72, file, 1));
        HostCheck.Equal((long)(W.NoFollow | W.LargeFile), f.Core(72, file, 3));
    }

    [HostTest("writable attachment full tree query excludes adapter provider DMA and observer backing and survives original block stop without copy authority")]
    private static void TreeAliases()
    {
        using var f = new Owner(observe: true); byte external = 0x93; ulong address = (ulong)&external;
        HostCheck.True(Tree(f.Files.State, address, 1)); uint calls = f.Hardware->Calls;
        var a = f.Storage.Input; var b = f.Storage.Device.Input;
        foreach (var extent in new (ulong Start, ulong Bytes)[] {
            ((ulong)f.Disk, (ulong)sizeof(F.WritableDiskState)), ((ulong)f.Bindings, f.Files.Admission.DescriptionCount * (ulong)sizeof(F.WritableDiskBinding)),
            ((ulong)f.Provider, (ulong)sizeof(W.State)), ((ulong)f.ProviderInput, W.IoBytes),
            ((ulong)f.Slots, 32UL * (ulong)sizeof(W.Descriptor)), ((ulong)f.Descriptions, 32UL * (ulong)sizeof(W.Description)),
            ((ulong)f.Processes, 4UL * (ulong)sizeof(W.ProcessSlot)), ((ulong)f.Locks, 32UL * (ulong)sizeof(W.RangeLock)), ((ulong)f.Scratch, 65UL * (ulong)sizeof(W.RangeLock)),
            ((ulong)f.Storage.State, (ulong)sizeof(C.State)), ((ulong)a.Before, C.JournalChanges * 512UL), ((ulong)a.After, C.JournalChanges * 512UL),
            ((ulong)f.Storage.Device.State, (ulong)sizeof(B.State)), ((ulong)f.Storage.Device.Data, 4096),
            ((ulong)f.Storage.Device.Observation, (ulong)sizeof(B.Observation)), (b.Common.VirtualStart, b.Common.Bytes),
            ((ulong)f.Files.State, (ulong)sizeof(F.State)), ((ulong)f.Files.Admission.Scratch, f.Files.Admission.ScratchBytes) })
        {
            HostCheck.True(!Tree(f.Files.State, extent.Start, extent.Bytes) && !Tree(f.Files.State, extent.Start - 1, 2) &&
                !Tree(f.Files.State, extent.Start + extent.Bytes - 1, 2) && !Tree(f.Files.State, extent.Start - 8, extent.Bytes + 16));
        }
        HostCheck.True(!Tree(f.Files.State, ulong.MaxValue - 1, 8) && f.Hardware->Calls == calls && external == 0x93);
        HostCheck.Equal(B.Status.Success, B.Stop(f.Storage.Device.State));
        HostCheck.True(Tree(f.Files.State, address, 1) && !W.CanBorrowStorage(f.Provider, f.ProviderAdmission.Epoch, address, 1));
    }

    [HostTest("writable global attachment validates original epoch primary and disjoint metadata before changing any caller storage")]
    private static void Admission()
    {
        using var f = new Owner(attach: false, observe: true); byte[] state = new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState)).ToArray();
        var a = f.Admission; a.Epoch++; HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, a));
        a = f.Admission; a.Process = default; HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, a));
        a = f.Admission; a.Bindings = (F.WritableDiskBinding*)f.ProviderInput; HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, a));
        HostCheck.True(!F.AttachWritableDisk(f.Files.State, (F.WritableDiskState*)f.Storage.Input.Before, f.Admission));
        HostCheck.True(state.SequenceEqual(new ReadOnlySpan<byte>(f.Disk, sizeof(F.WritableDiskState)).ToArray()));
        HostCheck.True(F.AttachWritableDisk(f.Files.State, f.Disk, f.Admission)); f.Attached = true;
        HostCheck.True(!F.AttachWritableDisk(f.Files.State, f.Disk, f.Admission) && F.Audit(f.Files.State));
    }

    private sealed class Owner : IDisposable
    {
        internal readonly LinuxThreadServicesTests.Model Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files;
        internal readonly ContinuingFat32MutationTests.Fixture Storage;
        internal readonly W.State* Provider = (W.State*)NativeMemory.AllocZeroed((nuint)sizeof(W.State));
        internal readonly W.Descriptor* Slots = (W.Descriptor*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Descriptor));
        internal readonly W.Description* Descriptions = (W.Description*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.Description));
        internal readonly byte* ProviderInput = (byte*)NativeMemory.AllocZeroed(W.IoBytes);
        internal readonly W.ProcessSlot* Processes = (W.ProcessSlot*)NativeMemory.AllocZeroed(4, (nuint)sizeof(W.ProcessSlot));
        internal readonly W.RangeLock* Locks = (W.RangeLock*)NativeMemory.AllocZeroed(32, (nuint)sizeof(W.RangeLock));
        internal readonly W.RangeLock* Scratch = (W.RangeLock*)NativeMemory.AllocZeroed(65, (nuint)sizeof(W.RangeLock));
        internal readonly F.WritableDiskState* Disk = (F.WritableDiskState*)NativeMemory.AllocZeroed((nuint)sizeof(F.WritableDiskState));
        internal readonly F.WritableDiskBinding* Bindings;
        internal readonly List<F.WritableDiskToken> Tokens = [];
        internal readonly W.Admission ProviderAdmission;
        internal OSHostHandle Worker;
        internal bool Attached;
        internal ulong ThreadOwner => (ulong)Thread.Service;
        internal WritableBlockSessionTests.Hardware* Hardware => Storage.Device.Hardware;
        internal F.WritableDiskAdmission Admission => new() { Provider = Provider, Process = W.PrimaryProcess(Provider), Bindings = Bindings,
            BindingCount = Files.Admission.DescriptionCount, Epoch = ProviderAdmission.Epoch, Search = new(2, 64, 256), FirmwareExited = true, SingleOwnerValidated = true };
        internal Owner(bool attach = true, bool observe = false, uint descriptors = 32, uint descriptions = 32)
        {
            Files = new(descriptors, descriptions); Bindings = (F.WritableDiskBinding*)NativeMemory.AllocZeroed(descriptions, (nuint)sizeof(F.WritableDiskBinding));
            Storage = new(observe: observe); Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 48)] = 0xff;
            Storage.Volatile[(int)(Storage.Input.PartitionFirst * 512 + 49)] = 0xff; Storage.Mount();
            ProviderAdmission = new() { Engine = Storage.State, Epoch = Storage.Input.Epoch, Descriptors = Slots, Descriptions = Descriptions,
                DescriptorCount = 32, DescriptionCount = 32, Input = ProviderInput, InputBytes = W.IoBytes, ExclusiveOwnerValidated = true };
            HostCheck.True(W.Start(Provider, ProviderAdmission));
            HostCheck.True(W.EnableLocks(Provider, new() { Processes = Processes, ProcessCount = 4, Locks = Locks, LockCount = 32,
                Scratch = Scratch, ScratchCount = 65, PrimaryPid = 101 }));
            Files.Admission.User = Thread.DescriptorAccess; Files.Admission.CopyToUser = Thread.UserCopyOut; Files.Begin();
            HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State));
            if (attach) { HostCheck.True(F.AttachWritableDisk(Files.State, Disk, Admission)); Attached = true; }
            Worker = Thread.Start(); HostCheck.Equal(Worker.Value, Thread.Kernel.Dispatch().Value);
        }
        internal LinuxSyscallFrame Call(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0, ulong a4 = 0, ulong a5 = 0)
        { var call = Thread.CurrentImage()->Frame; call.Number = number; call.Argument0 = a0; call.Argument1 = a1; call.Argument2 = a2; call.Argument3 = a3; call.Argument4 = a4; call.Argument5 = a5; return call; }
        internal void PutPath(string path) { byte[] value = Encoding.ASCII.GetBytes(path + '\0'); value.CopyTo(new Span<byte>(Thread.At(Path), value.Length)); }
        internal byte[] Bytes(int bytes) => new ReadOnlySpan<byte>(Thread.At(Output), bytes).ToArray();
        internal F.WritableDiskResult Begin(LinuxSyscallFrame call)
        { var begun = F.BeginWritableDisk(Files.State, ThreadOwner, Worker, call); if (begun.Token.IsActive) Tokens.Add(begun.Token); return begun; }
        internal F.WritableDiskResult Pump() => F.PumpWritableDiskOne(Files.State, ThreadOwner);
        internal void CompleteHardware() { if (Hardware->Pending) Storage.Device.Complete(); }
        internal F.WritableDiskResult Ready(F.WritableDiskToken token)
        {
            for (int i = 0; i < 100000; i++)
            {
                var result = Pump(); CompleteHardware();
                if (result.Progress is F.WritableDiskProgress.Ready or F.WritableDiskProgress.RecoveryRequired or F.WritableDiskProgress.Rejected) return result;
            }
            throw new InvalidOperationException("writable global request did not complete");
        }
        internal long Run(LinuxSyscallFrame call)
        {
            var begun = Begin(call); if (begun.Progress == F.WritableDiskProgress.Immediate) return begun.Value;
            HostCheck.True(begun.Progress is F.WritableDiskProgress.Pending or F.WritableDiskProgress.Ready);
            HostCheck.Equal(F.WritableDiskProgress.Ready, Ready(begun.Token).Progress);
            var result = F.CompleteWritableDisk(Files.State, ThreadOwner, Worker, begun.Token, call);
            HostCheck.Equal(F.WritableDiskProgress.Immediate, result.Progress); return result.Value;
        }
        internal uint Open(string path = "/data/DATA.BIN", uint flags = 0)
        { PutPath(path); long value = Run(Call(2, Path, flags)); HostCheck.True(value >= 0); return (uint)value; }
        internal long Core(ulong number, ulong a0 = 0, ulong a1 = 0, ulong a2 = 0)
        { var result = Thread.DescriptorCall(number, a0, a1, a2); HostCheck.Equal(LinuxSyscallAction.Return, result.Action); return result.Value; }
        internal void Drain()
        {
            for (int i = 0; i < 100000 && Disk->RequestActive; i++)
            { var result = Pump(); CompleteHardware(); HostCheck.True(result.Progress != F.WritableDiskProgress.RecoveryRequired); }
            HostCheck.True(!Disk->RequestActive);
        }
        public void Dispose()
        {
            try
            {
                if (Attached)
                {
                    foreach (var token in Tokens) F.CancelWritableDisk(Files.State, ThreadOwner, token, true);
                    Drain();
                    for (uint fd = 3; fd < Files.Admission.DescriptorCount; fd++) if (F.Capture(Files.State, fd, out _)) HostCheck.Equal(0L, Core(3, fd));
                    HostCheck.True(F.DetachWritableDisk(Files.State));
                }
                HostCheck.True(Thread.Service->Shutdown(Key, 2, 7));
                HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(Worker), out long clear) && clear == 0);
                Thread.Kernel.Confirm(); HostCheck.True(Thread.Service->Reap(Key, 2, Worker, Thread.Kernel.SupervisorRsp, out _));
                HostCheck.True(Thread.Service->Release(Key, 2)); HostCheck.True(F.End(Files.State));
                HostCheck.Equal(W.Progress.Retired, W.Detach(Provider).Phase);
            }
            finally
            {
                Storage.Dispose(); Files.Dispose(); Thread.Dispose(); NativeMemory.Free(Disk); NativeMemory.Free(Bindings);
                NativeMemory.Free(Provider); NativeMemory.Free(Slots); NativeMemory.Free(Descriptions); NativeMemory.Free(ProviderInput);
                NativeMemory.Free(Processes); NativeMemory.Free(Locks); NativeMemory.Free(Scratch);
            }
        }
    }
}
