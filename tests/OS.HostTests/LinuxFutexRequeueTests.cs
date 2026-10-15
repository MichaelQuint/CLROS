using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using Model = LinuxThreadServicesTests.Model;
using Table = OS.Kernel.Threading.KernelThreadTable;

internal static unsafe class LinuxFutexRequeueTests
{
    private const ulong Key = LinuxThreadServicesTests.Key, Base = LinuxThreadServicesTests.UserBase;
    private const ulong Source = Base + 0x3100, Target = Base + 0x4100, Timeout = Base + 0x3200;
    private static LinuxSyscallResult Wait(Model m, uint mode, ulong word = Source, ulong timeout = 0, uint bits = uint.MaxValue)
        => m.Call(202, word, mode + (bits == uint.MaxValue ? 0UL : 9UL), 42, timeout, 0, bits);
    private static long Wake(Model m, uint mode, ulong word, uint count = 1, uint bits = uint.MaxValue)
        => m.Call(202, word, mode + (bits == uint.MaxValue ? 1UL : 10UL), count, 0, 0, bits).Value;
    private static LinuxSyscallResult Requeue(Model m, uint mode, ulong wake, ulong move, ulong target = Target, ulong source = Source)
        => m.Call(202, source, mode + 3, wake, move, target);

    [HostTest("linux.futex-requeue moves real private and shared waits behind target FIFO without waking or copying words")]
    private static void FifoAndNamespaces()
    {
        foreach (uint mode in new uint[] { 0, 128 })
        {
            using Model m = new(futex: true); var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var actor = m.Clone();
            m.Put32(Source, 42); m.Put32(Target, 42);
            HostCheck.Equal(LinuxSyscallAction.Blocked, Wait(m, mode, bits: 2).Action); LinuxFutexTests.Park(m, parent);
            HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
            HostCheck.Equal(LinuxSyscallAction.Blocked, Wait(m, mode, Target, bits: 2).Action); LinuxFutexTests.Park(m, child);
            HostCheck.Equal(actor.Value, m.Kernel.Dispatch().Value); var original = m.View(parent).Wait;
            HostCheck.Equal(0L, Requeue(m, mode == 0 ? 128U : 0U, 0, 1).Value);
            HostCheck.Equal(1L, Requeue(m, mode, 0, 1).Value);
            HostCheck.Equal(Table.State.Waiting, m.Kernel.Inspect(parent).State);
            HostCheck.Equal(Table.Completion.None, m.Kernel.Inspect(parent).Completion);
            HostCheck.Equal(original.Sequence, m.View(parent).Wait.Sequence);
            HostCheck.Equal(42U, m.Read32(Source)); HostCheck.Equal(42U, m.Read32(Target));
            HostCheck.True(m.Service->MayChangeUserRange(Key, 2, Source, 4));
            HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Target, 4));
            HostCheck.Equal(0L, Wake(m, mode, Source)); HostCheck.Equal(0L, Wake(m, mode, Target, bits: 1));
            HostCheck.Equal(1L, Wake(m, mode, Target, bits: 2));
            HostCheck.Equal(Table.State.Ready, m.Kernel.Inspect(child).State);
            HostCheck.Equal(Table.State.Waiting, m.Kernel.Inspect(parent).State);
            HostCheck.Equal(1L, Wake(m, mode, Target, bits: 2));
            m.Yield(actor); HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
            HostCheck.Equal(0L, m.Service->CompleteFutexWait(Key, 2).Value);
            HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Target, 4));
            m.Yield(child); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
            HostCheck.Equal(0L, m.Service->CompleteFutexWait(Key, 2).Value);
            HostCheck.True(m.Service->MayChangeUserRange(Key, 2, Target, 4)); LinuxFutexTests.Drain(m);
        }
    }

    [HostTest("linux.futex-requeue reports actual wakes plus moves and retains each completed source borrow until consumed")]
    private static void WakeAndMove()
    {
        using Model m = new(futex: true); var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var actor = m.Clone();
        m.Put32(Source, 42); HostCheck.Equal(LinuxSyscallAction.Blocked, Wait(m, 128).Action); LinuxFutexTests.Park(m, parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(LinuxSyscallAction.Blocked, Wait(m, 128).Action); LinuxFutexTests.Park(m, child);
        HostCheck.Equal(actor.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(2L, Requeue(m, 128, 1, 1).Value);
        HostCheck.Equal(Table.Completion.Woken, m.Kernel.Inspect(parent).Completion);
        HostCheck.Equal(Table.State.Waiting, m.Kernel.Inspect(child).State);
        HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Source, 4));
        HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Target, 4));
        m.Yield(actor); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, m.Service->CompleteFutexWait(Key, 2).Value);
        HostCheck.True(m.Service->MayChangeUserRange(Key, 2, Source, 4));
        // Shutdown cancels the moved wait; actual off-stack reap drops Target.
        LinuxFutexTests.Drain(m);
    }

    [HostTest("linux.futex-requeue preserves actual timed wait sequence and absolute deadline across multiple moves")]
    private static void Deadline()
    {
        using Model m = new(futex: true); var parent = m.Start(); m.Kernel.Dispatch(); var actor = m.Clone();
        m.Put32(Source, 42); m.Put64(Timeout, 0); m.Put64(Timeout + 8, 50);
        HostCheck.Equal(LinuxSyscallAction.Blocked, Wait(m, 128, timeout: Timeout).Action); LinuxFutexTests.Park(m, parent);
        HostCheck.Equal(actor.Value, m.Kernel.Dispatch().Value); var original = m.View(parent);
        HostCheck.Equal(1L, Requeue(m, 128, 0, 1).Value);
        HostCheck.Equal(1L, Requeue(m, 128, 0, 1, Source, Target).Value);
        HostCheck.Equal(1L, Requeue(m, 128, 0, 1).Value);
        HostCheck.Equal(original.Wait.Sequence, m.View(parent).Wait.Sequence);
        HostCheck.Equal(1050UL, m.View(parent).WaitDeadline); HostCheck.Equal(original.WaitDeadline, m.View(parent).WaitDeadline);
        HostCheck.Equal(1U, m.Kernel.Table->DeadlineCount);
        m.Now = 1049; HostCheck.True(m.Service->AdvanceFutexClock(Key, 2)); HostCheck.Equal(Table.State.Waiting, m.Kernel.Inspect(parent).State);
        m.Now = 1050; HostCheck.True(m.Service->AdvanceFutexClock(Key, 2));
        HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Target, 4));
        m.Yield(actor); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(-110L, m.Service->CompleteFutexWait(Key, 2).Value);
        HostCheck.True(m.Service->MayChangeUserRange(Key, 2, Target, 4)); LinuxFutexTests.Drain(m);
    }

    [HostTest("linux.futex-requeue rejects counts pointers realtime and unavailable target capacity before waking")]
    private static void Admission()
    {
        using Model m = new(futex: true, futexCapacity: 1); var parent = m.Start(); m.Kernel.Dispatch(); var actor = m.Clone();
        m.Put32(Source, 42); HostCheck.Equal(LinuxSyscallAction.Blocked, Wait(m, 128).Action); LinuxFutexTests.Park(m, parent);
        HostCheck.Equal(actor.Value, m.Kernel.Dispatch().Value);
        foreach (var item in new[] { (0xffffffffUL, 1UL, Target, -22L), (1UL, 0xffffffffUL, Target, -22L),
            (1UL, 1UL, Target + 1, -22L), (1UL, 1UL, 0UL, -14L),
            (1UL, 1UL, Base + LinuxThreadServicesTests.UserBytes, -14L), (1UL, 1UL, Target, -11L) })
        {
            var result = Requeue(m, 128, item.Item1, item.Item2, item.Item3);
            HostCheck.Equal(LinuxSyscallAction.Return, result.Action); HostCheck.Equal(item.Item4, result.Value);
            HostCheck.Equal(Table.State.Waiting, m.Kernel.Inspect(parent).State);
            HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Source, 4));
            HostCheck.True(m.Service->MayChangeUserRange(Key, 2, Target, 4)); HostCheck.True(!m.Service->Failed);
        }
        HostCheck.Equal(-38L, m.Call(202, Source, 3 + 128 + 256, 1, 1, Target).Value);
        HostCheck.Equal(-38L, m.Call(202, Source, 4 + 128, 1, 1, Target).Value); // CMP_REQUEUE stays unsupported.
        HostCheck.Equal(1L, Requeue(m, 128, 1, 0).Value); LinuxFutexTests.Drain(m);
    }

    [HostTest("linux.futex-requeue same word preserves FIFO and zero wake count never forces a completion")]
    private static void SameWord()
    {
        using Model m = new(futex: true); var parent = m.Start(); m.Kernel.Dispatch(); var child = m.Clone(); var actor = m.Clone();
        m.Put32(Source, 42); HostCheck.Equal(LinuxSyscallAction.Blocked, Wait(m, 128).Action); LinuxFutexTests.Park(m, parent);
        HostCheck.Equal(child.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(LinuxSyscallAction.Blocked, Wait(m, 128).Action); LinuxFutexTests.Park(m, child);
        HostCheck.Equal(actor.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, Requeue(m, 128, 0, 0, Source).Value);
        HostCheck.Equal(2L, Requeue(m, 128, 0, int.MaxValue, Source).Value);
        HostCheck.Equal(Table.State.Waiting, m.Kernel.Inspect(parent).State); HostCheck.Equal(Table.State.Waiting, m.Kernel.Inspect(child).State);
        HostCheck.Equal(1L, Wake(m, 128, Source)); HostCheck.Equal(Table.Completion.Woken, m.Kernel.Inspect(parent).Completion);
        HostCheck.Equal(1L, Requeue(m, 128, 0, int.MaxValue, Source).Value);
        HostCheck.Equal(Table.State.Waiting, m.Kernel.Inspect(child).State); LinuxFutexTests.Drain(m);
    }

    [HostTest("linux.futex-requeue skips completed entries and releases an unused target binding")]
    private static void CompletedAndEmpty()
    {
        using Model m = new(futex: true, futexCapacity: 2); var parent = m.Start(); m.Kernel.Dispatch(); var actor = m.Clone();
        m.Put32(Source, 42); HostCheck.Equal(LinuxSyscallAction.Blocked, Wait(m, 128).Action); LinuxFutexTests.Park(m, parent);
        HostCheck.Equal(actor.Value, m.Kernel.Dispatch().Value); HostCheck.Equal(1L, Wake(m, 128, Source));
        // Repeating against different targets would exhaust two cells if the
        // destination were retained without any actual moved registration.
        for (uint i = 0; i < 4; i++) HostCheck.Equal(0L, Requeue(m, 128, 0, 1, Target + i * 4).Value);
        HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Source, 4));
        HostCheck.True(m.Service->MayChangeUserRange(Key, 2, Target, 16));
        m.Yield(actor); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, m.Service->CompleteFutexWait(Key, 2).Value);
        HostCheck.Equal(0L, Requeue(m, 128, 0, 1).Value); LinuxFutexTests.Drain(m);
    }

    [HostTest("linux.futex-requeue transfers real mapped page mutation guards through wake consumption")]
    private static void MappingLifetime()
    {
        using var memory = new LinuxMemoryServicesTests.Owner(); memory.Begin();
        const ulong start = LinuxMemoryServices.DynamicStart, source = start + 128, target = start + 4096 + 128;
        HostCheck.Equal((long)start, LinuxMemoryServices.Map(memory.State, start, 2 * 4096, 3, 0x22, ulong.MaxValue, 0));
        *(uint*)memory.At(source) = 42;
        using Model m = new(futex: true); m.UseMemory(memory.Memory);
        HostCheck.True(LinuxMemoryServices.AttachMutationGuard(memory.State, m.Service, &LinuxFutexTests.MemoryGuard));
        var parent = m.Start(); m.Kernel.Dispatch(); var actor = m.Clone();
        HostCheck.Equal(LinuxSyscallAction.Blocked, Wait(m, 128, source).Action); LinuxFutexTests.Park(m, parent);
        HostCheck.Equal(actor.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(1L, Requeue(m, 128, 0, 1, target, source).Value);
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(memory.State, start, 4096));
        HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(memory.State, start + 4096, 4096));
        HostCheck.Equal(-12L, LinuxMemoryServices.Protect(memory.State, start + 4096, 4096, 1));
        HostCheck.Equal(1L, Wake(m, 128, target));
        HostCheck.Equal(-12L, LinuxMemoryServices.Unmap(memory.State, start + 4096, 4096));
        m.Yield(actor); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
        HostCheck.Equal(0L, m.Service->CompleteFutexWait(Key, 2).Value);
        HostCheck.Equal(0L, LinuxMemoryServices.Unmap(memory.State, start + 4096, 4096)); LinuxFutexTests.Drain(m, memory.State);
    }

    [HostTest("linux.futex-requeue interrupted waits drop destination backing and retain the original syscall through signal return")]
    private static void Interrupted()
    {
        foreach (bool timed in new[] { false, true })
        {
            using LinuxSignalServicesTests.Owner owner = new(); owner.Running(); var m = owner.Thread;
            owner.Action(Base + 0x100, 0x04000000);
            HostCheck.Equal(0L, owner.Call(13, 10, Base + 0x1200, 0, 8));
            m.Put64(Base + 0x1200, 0); HostCheck.Equal(0L, owner.Call(14, 2, Base + 0x1200, 0, 8));
            var parent = m.Kernel.Table->Current; var actor = m.Clone();
            m.Put32(Source, 42); m.Put64(Timeout, 0); m.Put64(Timeout + 8, 50);
            // Model.Call supplies decoded arguments; signal return reconstructs
            // them from the saved native GPR image, as the real capture does.
            var captured = m.CurrentImage(); captured->Value = 202;
            captured->Gpr[3] = Source; captured->Gpr[2] = 128; captured->Gpr[4] = 42;
            captured->Gpr[7] = timed ? Timeout : 0; captured->Gpr[5] = 0; captured->Gpr[6] = uint.MaxValue;
            HostCheck.Equal(LinuxSyscallAction.Blocked, Wait(m, 128, timeout: timed ? Timeout : 0).Action); LinuxFutexTests.Park(m, parent);
            HostCheck.Equal(actor.Value, m.Kernel.Dispatch().Value); HostCheck.Equal(1L, Requeue(m, 128, 0, 1).Value);
            HostCheck.Equal(0L, owner.Call(234, 101, 101, 10)); HostCheck.Equal(Table.Completion.Interrupted, m.Kernel.Inspect(parent).Completion);
            HostCheck.True(!m.Service->MayChangeUserRange(Key, 2, Target, 4));
            m.Yield(actor); HostCheck.Equal(parent.Value, m.Kernel.Dispatch().Value);
            HostCheck.Equal(-4L, m.Service->CompleteFutexWait(Key, 2).Value);
            HostCheck.True(m.Service->MayChangeUserRange(Key, 2, Target, 4));
            HostCheck.True(!m.Service->CurrentImage(Key, 2, out _));
            HostCheck.Equal(LinuxSignalServices.Delivery.Delivered, m.Service->DeliverPendingSignals(Key, 2));
            m.CurrentImage()->Frame.Rsp += 8;
            HostCheck.Equal(LinuxSyscallAction.RestoreUser, m.Call(15).Action);
            HostCheck.Equal(-4L, m.CurrentImage()->Value); HostCheck.Equal(Source, m.CurrentImage()->Frame.Argument0);
            HostCheck.True(!m.View(parent).Wait.IsValid && m.Kernel.Table->DeadlineCount == 0);
        }
    }
}
