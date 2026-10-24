using System.Runtime.InteropServices;
using System.Text;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Fds = OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices;
using Fs = OS.Storage.ReadOnlyWebRootFileSystem;
using Block = OS.Storage.ContinuingReadOnlyBlockService;
using Signals = OS.Kernel.Compatibility.LinuxX64.LinuxSignalServices;

internal static unsafe class LinuxDiskThreadServicesTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong Path = Base + 0x1200, Output = Base + 0x2000;
    internal sealed class Owner : IDisposable
    {
        internal readonly LinuxThreadServicesTests.Model Thread = new(futex: true);
        internal readonly LinuxDescriptorServicesTests.Owner Files = new(descriptors: 32, descriptions: 32);
        internal readonly ReadOnlyWebRootFileSystemTests.Fixture Storage = new(slots: 8);
        internal readonly Fds.DiskState* Disk = (Fds.DiskState*)NativeMemory.AllocZeroed((nuint)sizeof(Fds.DiskState));
        private readonly Fds.DiskBinding* bindings = (Fds.DiskBinding*)NativeMemory.AllocZeroed(8, (nuint)sizeof(Fds.DiskBinding));
        private readonly Fds.DiskRequest* requests = (Fds.DiskRequest*)NativeMemory.AllocZeroed(8, (nuint)sizeof(Fds.DiskRequest));
        internal readonly OSHostHandle Main;
        private const ulong SignalKey = 0x64736b7369676e6c;
        private Signals.State* signalState;
        internal Owner(bool signals = false)
        {
            Storage.MountReady();
            Files.Admission.User = Thread.DescriptorAccess; Files.Admission.CopyToUser = Thread.UserCopyOut; Files.Begin();
            HostCheck.True(Fds.AttachDisk(Files.State, Disk, new() { Mount = Storage.Mount, Bindings = bindings, Requests = requests,
                Epoch = Storage.Input.Epoch, FirmwareExited = true, SingleOwnerValidated = true }));
            HostCheck.True(Thread.Service->EnablePipes(Key, 2, Files.State));
            if (signals)
            {
                signalState = (Signals.State*)NativeMemory.AlignedAlloc(4096, 4096);
                new Span<byte>(signalState, 4096).Clear();
                HostCheck.True(Signals.Initialize(signalState, 4096, SignalKey, 2, new()
                { User = Thread.Access, CopyToUser = Thread.UserCopyOut, Capacity = 3, FirmwareExited = true, SingleOwnerValidated = true }));
                HostCheck.True(Thread.Service->EnableSignals(Key, 2, signalState));
            }
            Main = Thread.Start(); HostCheck.Equal(Main.Value, Thread.Kernel.Dispatch().Value);
        }
        internal void PutPath(string value)
        { Encoding.ASCII.GetBytes(value + '\0').CopyTo(new Span<byte>(Thread.At(Path), value.Length + 1)); }
        internal bool May(ulong start, ulong bytes) => Thread.Service->MayChangeUserRange(Key, 2, start, bytes);
        internal void Begin(ulong number, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
            => HostCheck.Equal(LinuxSyscallAction.Blocked, Thread.DescriptorCall(number, a0, a1, a2, a3).Action);
        internal void Park()
        {
            HostCheck.True(Thread.Service->CommitDiskWait(Key, 2, Thread.Kernel.Capture(Main), out bool handoff));
            if (handoff) Thread.Kernel.Confirm();
        }
        internal void Ready()
        {
            for (int i = 0; i < 100000; i++)
            {
                Storage.Tick(); HostCheck.True(Thread.Service->PollDiskReadiness(Key, 2));
                if (Thread.Kernel.Inspect(Main).Completion != KernelThreadTable.Completion.None) return;
            }
            HostCheck.True(false, "draft_disk_wait_progress");
        }
        internal long Consume()
        {
            if (!Thread.Kernel.Table->Current.IsValid) HostCheck.Equal(Main.Value, Thread.Kernel.Dispatch().Value);
            HostCheck.Equal(Main.Value, Thread.Kernel.Table->Current.Value);
            var result = Thread.Service->CompleteDiskWait(Key, 2);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action); return result.Value;
        }
        internal long Run(ulong number, ulong a0, ulong a1 = 0, ulong a2 = 0, ulong a3 = 0)
        {
            var result = Thread.DescriptorCall(number, a0, a1, a2, a3);
            if (result.Action == LinuxSyscallAction.Return) return result.Value;
            HostCheck.Equal(LinuxSyscallAction.Blocked, result.Action); Park(); Ready(); return Consume();
        }
        internal uint Open(string value = "/app/wwwroot/index.html")
        { PutPath(value); long fd = Run(2, Path, 0x88100); HostCheck.True(fd >= 3); return (uint)fd; }
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
                        HostCheck.True(Thread.Service->CommitDiskWait(Key, 2, Thread.Kernel.Capture(current), out bool handoff));
                        if (handoff) Thread.Kernel.Confirm();
                        continue;
                    }
                    HostCheck.True(Thread.Service->CommitExit(Key, 2, Thread.Kernel.Capture(current), out _));
                    Thread.Kernel.Confirm(); HostCheck.True(Thread.Service->Reap(Key, 2, current, Thread.Kernel.SupervisorRsp, out _));
                }
                // Real block/FS retirement precedes orphan metadata scrub and
                // process detach. A failed reset must retain all native owners.
                HostCheck.Equal(Block.Status.Success, Block.Stop(Storage.Device));
                HostCheck.Equal(Fs.Status.Success, Fs.Stop(Storage.Mount));
                for (int i = 0; i < 64; i++) HostCheck.True(Thread.Service->PollDiskReadiness(Key, 2));
                HostCheck.True(Thread.Service->Release(Key, 2)); HostCheck.True(Fds.End(Files.State));
                if (signalState != null) HostCheck.True(Signals.End(signalState, SignalKey, 2));
            }
            finally
            { if (signalState != null) NativeMemory.AlignedFree(signalState); NativeMemory.Free(Disk); NativeMemory.Free(bindings); NativeMemory.Free(requests); Files.Dispose(); Thread.Dispose(); Storage.Dispose(); }
        }
    }

    [HostTest("linux.disk-thread pins each supported deferred output and leaves copied paths and disjoint bytes mutable")]
    private static void OutputKinds()
    {
        foreach (ulong number in new ulong[] { 0, 17, 4, 6 })
        {
            using Owner o = new(); uint fd = o.Open(); o.PutPath("/app/wwwroot/index.html");
            o.Begin(number, number is 4 or 6 ? Path : fd, Output, 5);
            uint bytes = number is 4 or 6 ? 144U : 5U;
            HostCheck.True(!o.May(Output, 1) && !o.May(Output + bytes - 1, 1));
            HostCheck.True(o.May(Output - 1, 1) && o.May(Output + bytes, 1) && o.May(Path, 256));
            o.Park(); HostCheck.True(!o.May(Output, bytes)); o.Ready(); HostCheck.True(!o.May(Output, bytes));
            HostCheck.Equal(number is 4 or 6 ? 0L : 5L, o.Consume()); HostCheck.True(o.May(Output, bytes));
        }
        using Owner immediate = new(); uint file = immediate.Open();
        HostCheck.Equal(0L, immediate.Run(5, file, Output)); HostCheck.True(immediate.May(Output, 144));
        HostCheck.Equal(-38L, immediate.Thread.Call(217, file, Output, 128).Value); // No getdents64 admission.
        HostCheck.True(immediate.May(Output, 128));
        immediate.PutPath("/app/wwwroot/index.html"); immediate.Begin(2, Path);
        HostCheck.True(immediate.May(Path, 256) && immediate.May(Output, 8192));
    }

    [HostTest("linux.disk-thread holds ready output through actual completion and permits another worker nonoverlapping mutations")]
    private static void OtherWorker()
    {
        using Owner o = new(); uint fd = o.Open(); var child = o.Thread.Clone();
        o.Begin(17, fd, Output, 5); o.Park(); HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value);
        HostCheck.True(!o.May(Output, 4096) && o.May(Output + 4096, 4096));
        o.Ready(); HostCheck.True(!o.May(Output, 5)); o.Thread.Yield(child);
        HostCheck.Equal(5L, o.Consume()); HostCheck.True(o.May(Output, 5));
    }

    [HostTest("linux.disk-thread actual memory callback rejects unmap protect discard fixed replace and shrinking while disk output is retained")]
    private static void MappingMutations()
    {
        using Owner o = new(); uint fd = o.Open(); var memory = o.Files.Memory;
        const ulong start = LinuxMemoryServices.DynamicStart + 0x10000;
        HostCheck.Equal((long)start, LinuxMemoryServices.Map(memory.State, start, 3 * 4096, 3, 0x100022, ulong.MaxValue, 0));
        o.Thread.UseMemory(memory.Memory);
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard));
        try
        {
            var child = o.Thread.Clone(); o.Begin(17, fd, start + 4096 + 8, 5); o.Park();
            HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value);
            byte[] before = memory.Tables(); uint backed = memory.State->BackedPages;
            HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(memory.State, start + 4096, 4096));
            HostCheck.Equal(-12L, LinuxMemoryServices.Protect(memory.State, start + 4096, 4096, 1));
            HostCheck.Equal(-12L, LinuxMemoryServices.Advise(memory.State, start + 4096, 4096, 4));
            HostCheck.Equal(-12L, LinuxMemoryServices.Map(memory.State, start + 4096, 4096, 3, 0x32, ulong.MaxValue, 0));
            HostCheck.Equal(-12L, LinuxMemoryServices.Remap(memory.State, start, 3 * 4096, 4096, 0));
            HostCheck.True(before.AsSpan().SequenceEqual(memory.Tables())); HostCheck.Equal(backed, memory.State->BackedPages);
            HostCheck.Equal(0L, LinuxMemoryServices.Protect(memory.State, start, 4096, 1));
            HostCheck.Equal(0L, LinuxMemoryServices.Advise(memory.State, start + 8192, 4096, 4));
            HostCheck.True(!LinuxMemoryServices.DetachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard));
            var wait = o.Thread.View(o.Main).Wait;
            HostCheck.True(o.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, wait).Succeeded);
            o.Thread.Yield(child); HostCheck.Equal(-4L, o.Consume());
            HostCheck.True(o.May(start + 4096, 4096));
            HostCheck.Equal(0L, LinuxMemoryServices.Unmap(memory.State, start + 4096, 4096));
        }
        finally { HostCheck.True(LinuxMemoryServices.DetachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard)); }
    }

    [HostTest("linux.disk-thread cancel revokes the mapping pin before orphan drain and prevents a late user copy")]
    private static void Interrupted()
    {
        foreach (bool ready in new[] { false, true })
        {
            using Owner o = new(); uint fd = o.Open(); uint writes = o.Thread.Writes;
            o.Begin(17, fd, Output, 5); o.Park();
            if (ready) o.Ready();
            if (!ready) HostCheck.True(o.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, o.Thread.View(o.Main).Wait).Succeeded);
            if (ready)
            {
                // Woken disk completion has already won. A later signal cannot
                // replace it or grant an early pin release.
                HostCheck.Equal(KernelThreadTable.Status.AlreadyCompleted,
                    o.Thread.Kernel.Table->Interrupt(KernelThreadTableTests.Key, 2, o.Thread.View(o.Main).Wait).Code);
                HostCheck.True(!o.May(Output, 5)); HostCheck.Equal(5L, o.Consume());
            }
            else
            {
                HostCheck.Equal(-4L, o.Consume()); HostCheck.True(o.May(Output, 5));
                for (int i = 0; i < 64; i++) { o.Storage.Tick(); HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2)); }
                HostCheck.Equal(writes, o.Thread.Writes);
            }
        }
    }

    [HostTest("linux.disk-thread closed original fd retains pin until its error consumes without a late copy")]
    private static void ClosePending()
    {
        using Owner o = new(); uint fd = o.Open(); var child = o.Thread.Clone(); uint writes = o.Thread.Writes;
        o.Begin(17, fd, Output, 5); o.Park(); HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, o.Thread.DescriptorCall(3, fd).Value); HostCheck.True(!o.May(Output, 5));
        o.Ready(); o.Thread.Yield(child); HostCheck.Equal(-9L, o.Consume());
        HostCheck.True(o.May(Output, 5)); HostCheck.Equal(writes, o.Thread.Writes);
    }

    [HostTest("linux.disk-thread initial writable prefix preserves short reads and EOF while rejecting newly mapped output authority")]
    private static void InitialPrefix()
    {
        using Owner o = new(); uint fd = o.Open(); ulong tail = Base + LinuxThreadServicesTests.UserBytes - 4;
        HostCheck.Equal(4L, o.Run(17, fd, tail, 32, 696)); // Exact4B remaining, inaccessible requested tail.
        uint writes = o.Thread.Writes; HostCheck.Equal(-14L, o.Run(17, fd, tail, 32, 695));
        HostCheck.Equal(writes, o.Thread.Writes); HostCheck.Equal(0L, o.Run(17, fd, 0, 8192, 700));
        HostCheck.Equal(-14L, o.Run(4, Path, tail)); HostCheck.Equal(writes, o.Thread.Writes);
        var memory = o.Files.Memory; o.Thread.UseMemory(memory.Memory);
        const ulong absent = LinuxMemoryServices.DynamicStart + 0x20000;
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard));
        try
        {
            var child = o.Thread.Clone(); writes = o.Thread.Writes; o.Begin(17, fd, absent, 5); o.Park();
            HostCheck.Equal(child.Value, o.Thread.Kernel.Dispatch().Value); HostCheck.True(o.May(absent, 4096));
            HostCheck.Equal((long)absent, LinuxMemoryServices.Map(memory.State, absent, 4096, 3, 0x100022, ulong.MaxValue, 0));
            new Span<byte>(memory.At(absent), 4096).Fill(0xa5); o.Ready(); o.Thread.Yield(child);
            HostCheck.Equal(-14L, o.Consume()); HostCheck.Equal(writes, o.Thread.Writes);
            HostCheck.True(new ReadOnlySpan<byte>(memory.At(absent), 4096).IndexOfAnyExcept((byte)0xa5) < 0);
        }
        finally { HostCheck.True(LinuxMemoryServices.DetachMutationGuard(memory.State, o.Thread.Service, &LinuxFutexTests.MemoryGuard)); }
    }

    [HostTest("linux.disk-thread copy ceiling pins at most8192 and zero requested bytes create no output pin")]
    private static void OutputBounds()
    {
        using Owner o = new(); uint fd = o.Open();
        o.Begin(17, fd, Output, 65536); HostCheck.True(!o.May(Output + 8191, 1) && o.May(Output + 8192, 1));
        o.Park(); o.Ready(); HostCheck.Equal(700L, o.Consume());
        o.Begin(17, fd, 0, 0); HostCheck.True(o.May(Output, 8192)); o.Park(); o.Ready(); HostCheck.Equal(0L, o.Consume());
    }

    [HostTest("linux.disk-thread process exit keeps output pinned until actual cancellation commit and permits safe orphan cleanup")]
    private static void ExitPending()
    {
        using Owner o = new(); uint fd = o.Open(); uint writes = o.Thread.Writes;
        o.Begin(17, fd, Output, 5); HostCheck.True(o.Thread.Service->Shutdown(Key, 2, 7));
        HostCheck.True(!o.May(Output, 5)); o.Park();
        HostCheck.True(ThreadCurrent(o)); HostCheck.True(o.Thread.Service->CommitExit(Key, 2, o.Thread.Kernel.Capture(o.Main), out _));
        HostCheck.True(o.May(Output, 5)); o.Thread.Kernel.Confirm();
        HostCheck.True(o.Thread.Service->Reap(Key, 2, o.Main, o.Thread.Kernel.SupervisorRsp, out _));
        for (int i = 0; i < 64; i++) { o.Storage.Tick(); HostCheck.True(o.Thread.Service->PollDiskReadiness(Key, 2)); }
        HostCheck.Equal(writes, o.Thread.Writes);
    }
    [HostTest("linux.disk-thread early completion remains pinned until real Parking capture and actual copy")]
    private static void CompletionBeforePark()
    {
        using Owner o = new(); uint fd = o.Open(); o.Begin(17, fd, Output, 5);
        HostCheck.Equal(KernelThreadTable.State.Parking, o.Thread.Kernel.Inspect(o.Main).State);
        o.Ready(); HostCheck.True(!o.May(Output, 5));
        HostCheck.True(o.Thread.Service->CommitDiskWait(Key, 2, o.Thread.Kernel.Capture(o.Main), out bool handoff) && !handoff);
        HostCheck.Equal(KernelThreadTable.State.Running, o.Thread.Kernel.Inspect(o.Main).State);
        HostCheck.Equal(5L, o.Consume()); HostCheck.True(o.May(Output, 5));
    }

    [HostTest("linux.disk-thread owner SIGTERM interrupts source4 without replacing an earlier completion or issuing a late copy")]
    private static void OwnerSignal()
    {
        foreach (bool ready in new[] { false, true })
        {
            using Owner o = new(signals: true); var m = o.Thread; uint fd = o.Open();
            *(Signals.Disposition*)m.At(Path) = new() { Handler = Base + 0x180, Flags = 0x04000004, Restorer = Base + 0x200 };
            HostCheck.Equal(0L, m.Call(13, 15, Path, 0, 8).Value);
            m.Put64(Path, 0); HostCheck.Equal(0L, m.Call(14, 2, Path, 0, 8).Value);
            *(ushort*)m.At(Base + 0xfe) = 0x050f; uint writes = m.Writes;
            o.Begin(17, fd, Output, 5); o.Park(); if (ready) o.Ready();
            HostCheck.True(m.Service->RequestTermination(Key, 2, o.Main));
            HostCheck.True(!o.May(Output, 5)); HostCheck.Equal(ready ? 5L : -4L, o.Consume());
            HostCheck.True(o.May(Output, 5)); HostCheck.Equal(ready ? writes + 1 : writes, m.Writes);
            HostCheck.Equal(Signals.Delivery.Delivered, m.Service->DeliverPendingSignals(Key, 2));
            ulong frame = m.CurrentImage()->Frame.Rsp;
            HostCheck.Equal(15U, m.Read32(frame + 312)); HostCheck.Equal(128U, m.Read32(frame + 320));
            HostCheck.Equal(0U, m.Read32(frame + 328));
            m.CurrentImage()->Frame.Rsp = frame + 8;
            HostCheck.Equal(LinuxSyscallAction.RestoreUser, m.Call(15).Action);
        }
    }
    private static bool ThreadCurrent(Owner o) => o.Thread.Kernel.Table->Current.Value == o.Main.Value;
}
