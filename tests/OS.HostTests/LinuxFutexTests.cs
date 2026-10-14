using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using OS.Pal;
using Model = LinuxThreadServicesTests.Model;
using Service = OS.Kernel.Compatibility.LinuxX64.LinuxThreadServices;

internal static unsafe class LinuxFutexTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong Word = Base + 0x3100, OtherWord = Base + 0x3104, Timeout = Base + 0x3200;
    private static LinuxSyscallResult Call(Model m, ulong op, uint value, ulong timeout = 0, uint bitset = uint.MaxValue, ulong address = Word)
        => m.Call(202, address, op, value, timeout, 0, bitset);
    internal static void Park(Model m, OSHostHandle thread)
    {
        HostCheck.True(!m.Service->CurrentImage(Key, 2, out _));
        HostCheck.True(m.Service->CommitFutexWait(Key, 2, m.Kernel.Capture(thread), out bool handoff) && handoff);
        m.Kernel.Confirm(); HostCheck.Equal(KernelThreadTable.State.Waiting, m.Kernel.Inspect(thread).State);
    }
    internal static void Drain(Model m, LinuxMemoryServices.State* guarded = null)
    {
        HostCheck.True(m.Service->Shutdown(Key, 2, 9));
        while (m.Service->ActiveThreads != 0)
        {
            var thread = m.Kernel.Table->Current;
            if (!thread.IsValid) thread = m.Kernel.Dispatch();
            HostCheck.True(m.Service->CommitExit(Key, 2, m.Kernel.Capture(thread), out _)); m.Kernel.Confirm();
            HostCheck.True(m.Service->Reap(Key, 2, thread, m.Kernel.SupervisorRsp, out _));
        }
        HostCheck.True(m.Service->MayChangeUserRange(Key, 2, Word, 4096));
        if (guarded != null) HostCheck.True(LinuxMemoryServices.DetachMutationGuard(guarded, m.Service, &MemoryGuard));
        HostCheck.True(m.Service->Release(Key, 2));
        HostCheck.True(new ReadOnlySpan<byte>(m.FutexCells, sizeof(Service.FutexCell) * 4).IndexOfAnyExcept((byte)0) == -1);
    }

    [System.Runtime.InteropServices.UnmanagedCallersOnly]
    internal static byte MemoryGuard(void* context, ulong address, ulong bytes)
        => ((Service*)context)->MayChangeUserRange(Key, 2, address, bytes) ? (byte)1 : (byte)0;

    [HostTest("linux.futex actual memory guard retains woken backing until consumption and allows unchanged remap prefixes")]
    private static void MemoryOwnership()
    {
        using var memory = new LinuxMemoryServicesTests.Owner(); memory.Begin();
        const ulong start = LinuxMemoryServices.DynamicStart, word = start + 4096 + 128;
        HostCheck.Equal((long)start, LinuxMemoryServices.Map(memory.State, start, 4 * 4096, 3, 0x22, ulong.MaxValue, 0));
        *(uint*)memory.At(word) = 42;
        using Model m = new(futex: true); m.UseMemory(memory.Memory);
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(memory.State, m.Service, &MemoryGuard));
        var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone();
        HostCheck.Equal(LinuxSyscallAction.Blocked, Call(m, 128, 42, address: word).Action); Park(m, parent); m.Kernel.Dispatch();
        HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(memory.State, start + 4096, 4096));
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(memory.State, start + 4096, 4096, 1));
        HostCheck.Equal(-12L, LinuxMemoryServices.Advise(memory.State, start + 4096, 4096, 4));
        HostCheck.Equal(-12L, LinuxMemoryServices.Map(memory.State, start + 4096, 4096, 3, 0x32, ulong.MaxValue, 0));
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(memory.State, start + 3 * 4096, 4096));
        HostCheck.Equal((long)start, LinuxMemoryServices.Remap(memory.State, start, 3 * 4096, 4 * 4096, 0));
        HostCheck.Equal((long)start, LinuxMemoryServices.Remap(memory.State, start, 4 * 4096, 3 * 4096, 0));
        HostCheck.Equal(-12L, LinuxMemoryServices.Remap(memory.State, start, 3 * 4096, 4096, 0));
        HostCheck.Equal(42U, *(uint*)memory.At(word));
        HostCheck.True(!LinuxMemoryServices.DetachMutationGuard(memory.State, m.Service, &MemoryGuard));
        HostCheck.Equal(1L, Call(m, 129, 1, address: word).Value);
        HostCheck.True(!LinuxMemoryServices.DetachMutationGuard(memory.State, m.Service, &MemoryGuard));
        HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(memory.State, start + 4096, 4096));
        m.Yield(child); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, m.Service->CompleteFutexWait(Key, 2).Value);
        HostCheck.Equal(0L, LinuxMemoryServices.Protect(memory.State, start + 4096, 4096, 1));
        HostCheck.True(LinuxMemoryServices.Audit(memory.State)); Drain(m, memory.State);
        HostCheck.True(!memory.State->HasMutationGuard);
    }

    [HostTest("linux.futex bitsets wake matching actual workers and retain backing until completion consumption")]
    private static void BitsetsAndReferences()
    {
        using Model m = new(futex: true); var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var other = m.Clone();
        m.Put32(Word, 42);
        HostCheck.Equal(LinuxSyscallAction.Blocked, Call(m, 128 + 9, 42, bitset: 1).Action); Park(m, parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(LinuxSyscallAction.Blocked, Call(m, 128 + 9, 42, bitset: 2).Action); Park(m, child);
        HostCheck.Equal(other.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, Call(m, 128 + 10, 1, bitset: 4).Value);
        HostCheck.Equal(1L, Call(m, 128 + 10, 1, bitset: 2).Value);
        HostCheck.Equal(KernelThreadTable.State.Waiting, m.Kernel.Inspect(parent).State);
        HostCheck.Equal(KernelThreadTable.State.Ready, m.Kernel.Inspect(child).State);
        HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Base + 0x3000, 4096));
        HostCheck.True(m.Service->MayChangeUserRange(Key, 2, Base + 0x4000, 4096));
        m.Yield(other); HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        var completed = m.Service->CompleteFutexWait(Key, 2);
        HostCheck.Equal(LinuxSyscallAction.Return, completed.Action); HostCheck.Equal(0L, completed.Value);
        HostCheck.Equal(0L, m.CurrentImage()->Value); HostCheck.True(!m.View(child).Wait.IsValid);
        HostCheck.Equal(LinuxSyscallAction.Rejected, m.Service->CompleteFutexWait(Key, 2).Action);
        HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Word, 4));
        HostCheck.Equal(1L, Call(m, 128 + 10, 1, bitset: 1).Value);
        HostCheck.Equal(0L, Call(m, 128 + 1, 9).Value);
        m.Yield(child); HostCheck.Equal(other.Value, m.Kernel.Dispatch().Value); m.Yield(other);
        HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, m.Service->CompleteFutexWait(Key, 2).Value);
        HostCheck.True(m.Service->MayChangeUserRange(Key, 2, Word, 4)); Drain(m);
    }

    [HostTest("linux.futex clear tid wakes only the shared key even when the best effort clear faults")]
    private static void ClearTidAndNamespaces()
    {
        foreach (bool failClear in new[] { false, true })
        {
            using Model m = new(futex: true); var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var privateWaiter = m.Clone();
            m.Put32(Word, 102); HostCheck.Equal(LinuxSyscallAction.Blocked, Call(m, 0, 102).Action); Park(m, parent);
            HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value); HostCheck.Equal(102L, m.Call(218, Word).Value); m.Yield(child);
            HostCheck.Equal(privateWaiter.Value, m.Kernel.Dispatch().Value);
            HostCheck.Equal(LinuxSyscallAction.Blocked, Call(m, 128, 102).Action); Park(m, privateWaiter);
            HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value); if (failClear) m.FailWrite(Word);
            HostCheck.Equal(LinuxSyscallAction.ThreadExited, m.Call(60).Action);
            HostCheck.True(m.Service->CommitExit(Key, 2, m.Kernel.Capture(child), out long clear));
            HostCheck.Equal(failClear ? -14L : 0L, clear); HostCheck.Equal(failClear ? 102U : 0U, m.Read32(Word));
            HostCheck.Equal(KernelThreadTable.Completion.Woken, m.Kernel.Inspect(parent).Completion);
            HostCheck.Equal(KernelThreadTable.State.Waiting, m.Kernel.Inspect(privateWaiter).State);
            m.Kernel.Confirm(); HostCheck.True(m.Service->Reap(Key, 2, child, m.Kernel.SupervisorRsp, out _));
            HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value); HostCheck.Equal(0L, m.Service->CompleteFutexWait(Key, 2).Value);
            HostCheck.Equal(0L, Call(m, 1, 1).Value); HostCheck.Equal(1L, Call(m, 128 + 1, 1).Value);
            m.Yield(parent); HostCheck.Equal(privateWaiter.Value, m.Kernel.Dispatch().Value);
            HostCheck.Equal(0L, m.Service->CompleteFutexWait(Key, 2).Value); HostCheck.True(!m.Service->Failed); Drain(m);
        }
    }

    [HostTest("linux.futex relative absolute and pre park deadlines share real monotonic nanoseconds without early returns")]
    private static void Deadlines()
    {
        using Model m = new(futex: true); var parent = m.Start(); m.Kernel.Dispatch(); m.Put32(Word, 42);
        m.Put64(Timeout, 0); m.Put64(Timeout + 8, 50);
        HostCheck.Equal(LinuxSyscallAction.Blocked, Call(m, 128, 42, Timeout).Action); Park(m, parent);
        m.Now = 1049; HostCheck.True(m.Service->AdvanceFutexClock(Key, 2));
        HostCheck.Equal(KernelThreadTable.State.Waiting, m.Kernel.Inspect(parent).State); HostCheck.Equal(0U, m.Kernel.Table->ReadyCount);
        m.Now = 1050; HostCheck.True(m.Service->AdvanceFutexClock(Key, 2)); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(-110L, m.Service->CompleteFutexWait(Key, 2).Value); HostCheck.Equal(-110L, m.CurrentImage()->Value);
        m.Put64(Timeout + 8, 1050); HostCheck.Equal(-110L, Call(m, 128 + 9, 42, Timeout, 1).Value);
        HostCheck.True(!m.View(parent).Wait.IsValid); HostCheck.Equal(0U, m.Kernel.Table->DeadlineCount);
        m.Put64(Timeout + 8, 0); HostCheck.Equal(-11L, Call(m, 128, 43, Timeout).Value);
        m.Put64(Timeout + 8, 1); HostCheck.Equal(LinuxSyscallAction.Blocked, Call(m, 128, 42, Timeout).Action);
        var good = m.Kernel.Capture(parent); var bad = good; bad.ResumeRip = m.Kernel.Code - 1;
        HostCheck.True(!m.Service->CommitFutexWait(Key, 2, bad, out _));
        HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Word, 4));
        m.Now = 1051; HostCheck.True(m.Service->AdvanceFutexClock(Key, 2));
        HostCheck.True(m.Service->CommitFutexWait(Key, 2, good, out bool handoff) && !handoff);
        HostCheck.True(!m.Kernel.Table->HandoffPending); HostCheck.Equal(-110L, m.Service->CompleteFutexWait(Key, 2).Value);
        m.Now = 1050; HostCheck.True(!m.Service->AdvanceFutexClock(Key, 2)); HostCheck.Equal(1051UL, m.Kernel.Table->ObservedTicks);
        Drain(m);
    }

    [HostTest("linux.futex rejects malformed and exhausted waits and preserves exact legacy wake count behavior")]
    private static void RejectionsAndCapacity()
    {
        using Model m = new(futex: true, futexCapacity: 1); var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone();
        m.Put32(Word, 42); m.Put32(OtherWord, 42);
        HostCheck.Equal(-22L, Call(m, 128 + 9, 42, bitset: 0).Value);
        HostCheck.Equal(-22L, Call(m, 128, 42, address: Word + 1).Value);
        HostCheck.Equal(-38L, Call(m, 128 + 6, 42).Value);
        HostCheck.Equal(-38L, Call(m, 128 + 256 + 9, 42).Value);
        HostCheck.Equal(0L, Call(m, 128 + 1, 1, address: Base + 0x10000).Value);
        HostCheck.Equal(-14L, Call(m, 1, 1, address: Base + 0x10000).Value);
        HostCheck.Equal(-14L, Call(m, 0, 43, address: Base + 0x100).Value);
        HostCheck.Equal(-14L, Call(m, 1, 1, address: Base + 0x100).Value);
        HostCheck.Equal(-11L, Call(m, 128, 43, address: Base + 0x100).Value);
        HostCheck.Equal(0L, Call(m, 128 + 1, 1, address: Base + 0x100).Value);
        m.Put64(Timeout, ulong.MaxValue); m.Put64(Timeout + 8, 0);
        HostCheck.Equal(-22L, Call(m, 128, 42, Timeout).Value);
        m.Put64(Timeout, 0); m.Put64(Timeout + 8, 1_000_000_000);
        HostCheck.Equal(-22L, Call(m, 128, 42, Timeout).Value);
        HostCheck.Equal(-14L, Call(m, 128, 42, Base + 0x10000).Value);
        HostCheck.Equal(LinuxSyscallAction.Blocked, Call(m, 128, 42).Action); Park(m, parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(-11L, Call(m, 128, 42, address: OtherWord).Value); HostCheck.True(!m.View(child).Wait.IsValid);
        HostCheck.Equal(1L, Call(m, 128 + 1, 0).Value); // Legacy zero/negative int count wakes one.
        HostCheck.Equal(0L, Call(m, 128 + 1, uint.MaxValue).Value);
        m.Yield(child); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, m.Service->CompleteFutexWait(Key, 2).Value);
        HostCheck.Equal(LinuxSyscallAction.Blocked, Call(m, 128, 42).Action); Park(m, parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(1L, Call(m, 128 + 1, uint.MaxValue).Value);
        m.Yield(child); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, m.Service->CompleteFutexWait(Key, 2).Value); Drain(m);
    }

    [HostTest("linux.futex stale backing never redirects a wake and shutdown drains every retained registration")]
    private static void StaleBackingAndShutdown()
    {
        using Model m = new(futex: true); var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone();
        m.Put32(Word, 42); HostCheck.Equal(LinuxSyscallAction.Blocked, Call(m, 128, 42).Action); Park(m, parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value); m.ChangeMappingLifetime(2);
        HostCheck.Equal(LinuxSyscallAction.Rejected, Call(m, 128 + 1, 1).Action); HostCheck.True(m.Service->Failed);
        HostCheck.Equal(KernelThreadTable.State.Waiting, m.Kernel.Inspect(parent).State);
        HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Word, 4));
        Drain(m);
        using Model cancelled = new(futex: true); var a = cancelled.Start(); cancelled.Kernel.Dispatch(); var b = cancelled.Clone();
        cancelled.Put32(Word, 42); HostCheck.Equal(LinuxSyscallAction.Blocked, Call(cancelled, 128, 42).Action); Park(cancelled, a);
        HostCheck.Equal(b.Value, cancelled.Kernel.Dispatch().Value);
        HostCheck.Equal(LinuxSyscallAction.Blocked, Call(cancelled, 128, 42).Action); Park(cancelled, b);
        HostCheck.True(!cancelled.Kernel.Table->Current.IsValid); Drain(cancelled);
    }

    [HostTest("linux.futex admission rejects metadata aliases copied owners and late enable without mutation")]
    private static void Admission()
    {
        using Model m = new(); var plan = m.FutexPlan(); byte[] before = m.Kernel.Snapshot();
        HostCheck.True(!m.Service->EnableFutex(Key + 1, 2, plan)); m.Kernel.Same(before);
        HostCheck.True(!m.Service->EnableFutex(Key, 0x202, plan)); m.Kernel.Same(before);
        var bad = plan; bad.Cells = (Service.FutexCell*)m.Slots;
        HostCheck.True(!m.Service->EnableFutex(Key, 2, bad)); m.Kernel.Same(before);
        bad = plan; bad.Capacity = 0; HostCheck.True(!m.Service->EnableFutex(Key, 2, bad)); m.Kernel.Same(before);
        Service copy = *m.Service; HostCheck.True(!copy.EnableFutex(Key, 2, plan)); m.Kernel.Same(before);
        HostCheck.True(m.Service->EnableFutex(Key, 2, plan)); var parent = m.Start(); m.Kernel.Dispatch();
        HostCheck.True(!m.Service->EnableFutex(Key, 2, plan));
        HostCheck.True(!m.Service->MayChangeUserRange(Key + 1, 2, Word, 4));
        HostCheck.True(!m.Service->MayChangeUserRange(Key, 0x202, Word, 4));
        HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, ulong.MaxValue, 4));
        m.FailClock(true); HostCheck.Equal(LinuxSyscallAction.Rejected, Call(m, 128, 42).Action);
        HostCheck.Equal(KernelThreadTable.State.Running, m.Kernel.Inspect(parent).State);
        HostCheck.True(!m.View(parent).Wait.IsValid); Drain(m);
    }
}
