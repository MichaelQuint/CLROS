using OS.Kernel.Threading;
using OS.Pal;
using M = KernelThreadTableTests.Model;
using S = OS.Kernel.Threading.WaitAddress.Status;
using T = OS.Kernel.Threading.KernelThreadTable.State;
using C = OS.Kernel.Threading.KernelThreadTable.Completion;

internal static unsafe class WaitAddressTests
{
    private const ulong Key = 0x7761697461646472;
    private static WaitAddress* Owner(M m) => (WaitAddress*)(m.Memory + 0x6000);
    private static WaitAddress.Slot* Slots(M m) => (WaitAddress.Slot*)(m.Memory + 0x7000);
    private static WaitAddress.Registration* Registrations(M m) => (WaitAddress.Registration*)(m.Memory + 0x8000);
    // Distinct words deliberately collide under the reference's (address>>3)&63.
    private static uint* Word(M m, int index = 0) => (uint*)(m.Memory + 0x9000 + index * 512);
    private static S Initialize(M m, uint capacity = 2, uint registrations = 3, uint generation = 1, ulong order = 0) =>
        Owner(m)->Initialize(Key, 2, m.Table, KernelThreadTableTests.Key, Slots(m), capacity * (uint)sizeof(WaitAddress.Slot), capacity,
            Registrations(m), registrations * (uint)sizeof(WaitAddress.Registration), registrations, generation, order);
    private static void Setup(M m, uint capacity = 2, uint registrations = 3, uint generation = 1, ulong order = 0)
    { *Owner(m) = default; *Word(m) = 42; *Word(m, 1) = 42; HostCheck.Equal(S.Ok, Initialize(m, capacity, registrations, generation, order)); }
    private static WaitAddress.Binding Bind(M m, int index = 0, ulong lifetime = 1)
    { var r = Owner(m)->Bind(Key, 2, Word(m, index), lifetime); HostCheck.True(r.Succeeded); return r.Address; }
    private static WaitAddress.Result Begin(M m, WaitAddress.Binding binding, OSHostHandle thread, bool timed = false, ulong delay = 0, ulong now = 0, uint bitset = uint.MaxValue) =>
        Owner(m)->Begin(Key, 2, binding, thread, 42, now, timed, delay, bitset);
    private static KernelThreadWait Park(M m, WaitAddress.Binding binding, OSHostHandle thread, bool timed = false, ulong delay = 0, uint bitset = uint.MaxValue)
    {
        var begin = Begin(m, binding, thread, timed, delay, bitset: bitset); HostCheck.True(begin.Succeeded);
        HostCheck.True(m.Table->Park(KernelThreadTableTests.Key, 2, begin.Wait, m.Capture(thread)).Succeeded); m.Confirm(); return begin.Wait;
    }

    [HostTest("wait.address owner storage alias and copy rejection preserves all state")]
    private static void Admission()
    {
        using M m = new(); *Owner(m) = default; byte[] before = m.Snapshot();
        HostCheck.Equal(S.InvalidCapacity, Initialize(m, 0)); m.Same(before);
        HostCheck.Equal(S.InvalidCapacity, Initialize(m, registrations: 65)); m.Same(before);
        HostCheck.Equal(S.InvalidCapacity, Initialize(m, generation: 0)); m.Same(before);
        HostCheck.Equal(S.Overlap, Owner(m)->Initialize(Key, 2, m.Table, KernelThreadTableTests.Key,
            (WaitAddress.Slot*)Owner(m), 512, 2, Registrations(m), 512, 2)); m.Same(before);
        HostCheck.Equal(S.ThreadFailure, Owner(m)->Initialize(Key, 2, m.Table, 5, Slots(m), 512, 2, Registrations(m), 512, 2)); m.Same(before);
        Setup(m); before = m.Snapshot();
        HostCheck.Equal(S.AlreadyInitialized, Initialize(m)); m.Same(before);
        HostCheck.Equal(S.WrongOwner, Owner(m)->Sweep(Key + 1, 2).Code); m.Same(before);
        HostCheck.Equal(S.InterruptsEnabled, Owner(m)->Sweep(Key, 0x202).Code); m.Same(before);
        WaitAddress copied = *Owner(m); HostCheck.Equal(S.CopiedOwner, copied.Sweep(Key, 2).Code); m.Same(before);
        HostCheck.True(!default(WaitAddress.Result).Succeeded && !default(WaitAddress.Binding).IsValid);
    }

    [HostTest("wait.address bindings reject malformed duplicate and foreign lifetime aliases")]
    private static void BindingAdmission()
    {
        using M m = new(); using M n = new(); Setup(m, 1); Setup(n);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.InvalidAddress, Owner(m)->Bind(Key, 2, null, 1).Code); m.Same(before);
        HostCheck.Equal(S.InvalidAddress, Owner(m)->Bind(Key, 2, (uint*)((byte*)Word(m) + 1), 1).Code); m.Same(before);
        HostCheck.Equal(S.InvalidAddress, Owner(m)->Bind(Key, 2, Word(m), 0).Code); m.Same(before);
        HostCheck.Equal(S.Overlap, Owner(m)->Bind(Key, 2, (uint*)Slots(m), 1).Code); m.Same(before);
        var binding = Bind(m); var foreign = Bind(n); before = m.Snapshot();
        HostCheck.Equal(S.AlreadyBound, Owner(m)->Bind(Key, 2, Word(m), 2).Code); m.Same(before);
        HostCheck.Equal(S.Full, Owner(m)->Bind(Key, 2, Word(m, 1), 1).Code); m.Same(before);
        HostCheck.Equal(S.InvalidBinding, Owner(m)->Wake(Key, 2, foreign, 1).Code); m.Same(before);
        HostCheck.Equal(S.InvalidBinding, Owner(m)->Unbind(Key, 2, default)); m.Same(before);
        HostCheck.Equal(S.Busy, Owner(m)->Release(Key, 2)); m.Same(before);
        HostCheck.Equal(S.Ok, Owner(m)->Unbind(Key, 2, binding));
        HostCheck.Equal(42U, *Word(m)); HostCheck.Equal(S.Ok, Owner(m)->Release(Key, 2));
    }

    [HostTest("wait.address changed value and invalid waits never publish a registration")]
    private static void CompareAndFailures()
    {
        using M m = new(); Setup(m); var binding = Bind(m); var a = m.Create(0).Identity; m.Dispatch();
        *Word(m) = 43; byte[] before = m.Snapshot();
        HostCheck.Equal(S.ValueChanged, Begin(m, binding, a).Code); m.Same(before);
        *Word(m) = 42; before = m.Snapshot();
        HostCheck.Equal(S.TimedOut, Begin(m, binding, a, true).Code); m.Same(before);
        var overflow = Begin(m, binding, a, true, ulong.MaxValue, 1);
        HostCheck.Equal(S.ThreadFailure, overflow.Code); HostCheck.Equal(KernelThreadTable.Status.DeadlineOverflow, overflow.ThreadStatus); m.Same(before);
        HostCheck.Equal(S.ThreadFailure, Begin(m, binding, default).Code); m.Same(before);
        HostCheck.Equal(KernelThreadTable.Status.Ok, m.Table->Advance(KernelThreadTableTests.Key, 2, 2)); before = m.Snapshot();
        HostCheck.Equal(KernelThreadTable.Status.ClockRegression, Begin(m, binding, a, now: 1).ThreadStatus); m.Same(before);
        HostCheck.Equal(0U, Owner(m)->Wake(Key, 2, binding, uint.MaxValue).Count);
    }

    [HostTest("wait.address wake before park resumes in place without a lost completion")]
    private static void BeforePark()
    {
        using M m = new(); Setup(m); var binding = Bind(m); var a = m.Create(0).Identity; m.Dispatch();
        var begin = Begin(m, binding, a); HostCheck.True(begin.Succeeded);
        HostCheck.Equal(S.Busy, Owner(m)->Unbind(Key, 2, binding));
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, binding, 1).Count);
        HostCheck.True(m.Table->Park(KernelThreadTableTests.Key, 2, begin.Wait, m.Capture(a)).Succeeded);
        HostCheck.Equal(T.Running, m.Inspect(a).State); HostCheck.Equal(C.Woken, m.Inspect(a).Completion);
        HostCheck.True(!m.Table->HandoffPending); HostCheck.Equal(0U, m.Table->DeadlineCount);
    }

    [HostTest("wait.address exact keys isolate collisions and wake one all in registration order")]
    private static void WakeOrdering()
    {
        using M m = new(); Setup(m); var x = Bind(m); var y = Bind(m, 1);
        HostCheck.Equal(((ulong)Word(m) >> 3) & 63, ((ulong)Word(m, 1) >> 3) & 63);
        var a = m.Create(0).Identity; var b = m.Create(1).Identity; var c = m.Create(2).Identity;
        HostCheck.Equal(a.Value, m.Dispatch().Value); Park(m, x, a);
        HostCheck.Equal(b.Value, m.Dispatch().Value); Park(m, y, b);
        HostCheck.Equal(c.Value, m.Dispatch().Value); Park(m, x, c);
        HostCheck.Equal(0U, Owner(m)->Wake(Key, 2, x, 0).Count);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, x, 1).Count);
        HostCheck.Equal(C.Woken, m.Inspect(a).Completion); HostCheck.Equal(T.Waiting, m.Inspect(c).State);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, x, uint.MaxValue).Count);
        HostCheck.Equal(T.Waiting, m.Inspect(b).State); HostCheck.Equal(C.Woken, m.Inspect(c).Completion);
        HostCheck.Equal(0U, Owner(m)->Wake(Key, 2, x, uint.MaxValue).Count);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, y, uint.MaxValue).Count);
        HostCheck.Equal(a.Value, m.Dispatch().Value); m.Exit(a);
        HostCheck.Equal(c.Value, m.Dispatch().Value); m.Exit(c);
        HostCheck.Equal(b.Value, m.Dispatch().Value);
    }

    [HostTest("wait.address requeue moves one oldest live wait behind target FIFO and preserves bitsets")]
    private static void RequeueOrdering()
    {
        using M m = new(); Setup(m); var source = Bind(m); var destination = Bind(m, 1);
        var a = m.Create(0).Identity; var b = m.Create(1).Identity; var c = m.Create(2).Identity;
        HostCheck.Equal(a.Value, m.Dispatch().Value); var original = Park(m, source, a, bitset: 2);
        HostCheck.Equal(b.Value, m.Dispatch().Value); Park(m, destination, b, bitset: 2);
        HostCheck.Equal(c.Value, m.Dispatch().Value); Park(m, source, c, bitset: 1);
        byte[] scheduler = new ReadOnlySpan<byte>(m.Memory, 0x6000).ToArray();
        var moved = Owner(m)->RequeueOne(Key, 2, source, destination);
        HostCheck.True(moved.Succeeded); HostCheck.Equal(1U, moved.Count);
        HostCheck.Equal(destination.Value, moved.Address.Value); HostCheck.Equal(destination.Lifetime, moved.Address.Lifetime);
        HostCheck.Equal(original.Thread.Value, moved.Wait.Thread.Value); HostCheck.Equal(original.Sequence, moved.Wait.Sequence);
        HostCheck.True(scheduler.AsSpan().SequenceEqual(new ReadOnlySpan<byte>(m.Memory, 0x6000)));
        HostCheck.Equal(42U, *Word(m)); HostCheck.Equal(42U, *Word(m, 1));
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, destination, 1, 2).Count);
        HostCheck.Equal(C.Woken, m.Inspect(b).Completion); HostCheck.Equal(T.Waiting, m.Inspect(a).State);
        HostCheck.Equal(0U, Owner(m)->Wake(Key, 2, destination, uint.MaxValue, 1).Count);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, source, uint.MaxValue).Count);
        HostCheck.Equal(C.Woken, m.Inspect(c).Completion); HostCheck.Equal(T.Waiting, m.Inspect(a).State);
        HostCheck.Equal(S.Ok, Owner(m)->Unbind(Key, 2, source));
        HostCheck.Equal(S.Busy, Owner(m)->Unbind(Key, 2, destination));
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, destination, 1, 2).Count);
        HostCheck.Equal(b.Value, m.Dispatch().Value); m.Exit(b);
        HostCheck.Equal(c.Value, m.Dispatch().Value); m.Exit(c); HostCheck.Equal(a.Value, m.Dispatch().Value);
    }

    [HostTest("wait.address requeue skips completed entries and retains original deadline cancellation and binding lifetime")]
    private static void RequeueDeadlineAndCancel()
    {
        foreach (bool cancel in new[] { false, true })
        {
            using M m = new(); Setup(m); var source = Bind(m); var destination = Bind(m, 1, 7);
            var a = m.Create(0).Identity; var b = m.Create(1).Identity;
            m.Dispatch(); var completed = Park(m, source, a);
            m.Dispatch(); var original = Park(m, source, b, true, 10);
            HostCheck.True(m.Table->Wake(KernelThreadTableTests.Key, 2, completed).Succeeded);
            HostCheck.Equal(KernelThreadTable.Status.Ok, m.Table->Advance(KernelThreadTableTests.Key, 2, 4));
            var moved = Owner(m)->RequeueOne(Key, 2, source, destination);
            HostCheck.True(moved.Succeeded); HostCheck.Equal(1U, moved.Count);
            HostCheck.Equal(original.Thread.Value, moved.Wait.Thread.Value); HostCheck.Equal(original.Sequence, moved.Wait.Sequence);
            HostCheck.Equal(7UL, moved.Address.Lifetime); HostCheck.Equal(1U, m.Table->DeadlineCount);
            HostCheck.Equal(4UL, m.Table->ObservedTicks); HostCheck.Equal(C.None, m.Inspect(b).Completion);
            HostCheck.Equal(S.Ok, Owner(m)->Unbind(Key, 2, source)); // Also prunes the older completed registration.
            HostCheck.Equal(S.Busy, Owner(m)->Unbind(Key, 2, destination));
            HostCheck.Equal(KernelThreadTable.Status.Ok, m.Table->Advance(KernelThreadTableTests.Key, 2, 9));
            HostCheck.Equal(T.Waiting, m.Inspect(b).State);
            if (cancel) HostCheck.True(m.Table->Cancel(KernelThreadTableTests.Key, 2, b).Succeeded);
            else HostCheck.Equal(KernelThreadTable.Status.Ok, m.Table->Advance(KernelThreadTableTests.Key, 2, 10));
            HostCheck.Equal(cancel ? C.Cancelled : C.Deadline, m.Inspect(b).Completion);
            HostCheck.Equal(original.Sequence, m.Inspect(b).Wait.Sequence); HostCheck.Equal(0U, m.Table->DeadlineCount);
            HostCheck.Equal(0U, Owner(m)->Wake(Key, 2, destination, 1).Count);
            HostCheck.Equal(S.Ok, Owner(m)->Unbind(Key, 2, destination));
        }
    }

    [HostTest("wait.address requeue rejects invalid same stale foreign and copied ownership without pruning")]
    private static void RequeueAdmission()
    {
        using M m = new(); using M n = new(); Setup(m); Setup(n);
        var source = Bind(m); var stale = Bind(m, 1); var foreign = Bind(n);
        HostCheck.Equal(S.Ok, Owner(m)->Unbind(Key, 2, stale)); var destination = Bind(m, 1, 2);
        var a = m.Create(0).Identity; var b = m.Create(1).Identity;
        m.Dispatch(); var completed = Park(m, source, a); m.Dispatch(); Park(m, source, b);
        HostCheck.True(m.Table->Wake(KernelThreadTableTests.Key, 2, completed).Succeeded);
        byte[] before = m.Snapshot(); byte[] foreignBefore = n.Snapshot();
        foreach (var invalid in new[] { (default(WaitAddress.Binding), destination, S.InvalidBinding),
            (source, default(WaitAddress.Binding), S.InvalidBinding), (source, source, S.InvalidBinding),
            (foreign, destination, S.InvalidBinding), (source, foreign, S.InvalidBinding),
            (stale, destination, S.StaleBinding), (source, stale, S.StaleBinding) })
        {
            var result = Owner(m)->RequeueOne(Key, 2, invalid.Item1, invalid.Item2);
            HostCheck.Equal(invalid.Item3, result.Code);
            HostCheck.True(!result.Address.IsValid && !result.Wait.IsValid && result.Count == 0); m.Same(before);
        }
        HostCheck.Equal(S.WrongOwner, Owner(m)->RequeueOne(Key + 1, 2, source, destination).Code); m.Same(before);
        HostCheck.Equal(S.InterruptsEnabled, Owner(m)->RequeueOne(Key, 0x202, source, destination).Code); m.Same(before);
        WaitAddress copied = *Owner(m);
        HostCheck.Equal(S.CopiedOwner, copied.RequeueOne(Key, 2, source, destination).Code); m.Same(before); n.Same(foreignBefore);
    }

    [HostTest("wait.address requeue allows the last FIFO order and leaves empty completed or exhausted sources untouched")]
    private static void RequeueExhaustion()
    {
        using M m = new(); Setup(m, order: ulong.MaxValue - 2); var source = Bind(m); var destination = Bind(m, 1);
        var a = m.Create(0).Identity; m.Dispatch(); var original = Park(m, source, a);
        var last = Owner(m)->RequeueOne(Key, 2, source, destination);
        HostCheck.True(last.Succeeded); HostCheck.Equal(1U, last.Count); HostCheck.Equal(original.Sequence, last.Wait.Sequence);
        byte[] before = m.Snapshot(); var exhausted = Owner(m)->RequeueOne(Key, 2, destination, source);
        HostCheck.Equal(S.IdentityExhausted, exhausted.Code);
        HostCheck.True(!exhausted.Address.IsValid && !exhausted.Wait.IsValid && exhausted.Count == 0); m.Same(before);
        var empty = Owner(m)->RequeueOne(Key, 2, source, destination);
        HostCheck.True(empty.Succeeded && empty.Count == 0 && !empty.Wait.IsValid); m.Same(before);
        HostCheck.True(m.Table->Wake(KernelThreadTableTests.Key, 2, original).Succeeded); before = m.Snapshot();
        empty = Owner(m)->RequeueOne(Key, 2, destination, source);
        HostCheck.True(empty.Succeeded && empty.Count == 0 && !empty.Wait.IsValid); m.Same(before);
        HostCheck.Equal(C.Woken, m.Inspect(a).Completion);
    }

    [HostTest("wait.address timeout and wake have exactly one winner in either order")]
    private static void DeadlineRace()
    {
        foreach (bool wakeFirst in new[] { false, true })
        {
            using M m = new(); Setup(m); var binding = Bind(m); var a = m.Create(0).Identity; m.Dispatch(); Park(m, binding, a, true, 2);
            if (wakeFirst) HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, binding, 1).Count);
            HostCheck.Equal(KernelThreadTable.Status.Ok, m.Table->Advance(KernelThreadTableTests.Key, 2, 2));
            HostCheck.Equal(wakeFirst ? 0U : 1U, Owner(m)->Sweep(Key, 2).Count);
            HostCheck.Equal(0U, Owner(m)->Wake(Key, 2, binding, 1).Count);
            HostCheck.Equal(wakeFirst ? C.Woken : C.Deadline, m.Inspect(a).Completion);
            HostCheck.Equal(1U, m.Table->ReadyCount); HostCheck.Equal(0U, m.Table->DeadlineCount);
        }
    }

    [HostTest("wait.address full registration and deadline capacity reject without mutation")]
    private static void Capacity()
    {
        foreach (bool registrationFull in new[] { false, true })
        {
            using M m = new(2, 2, 1); Setup(m, registrations: registrationFull ? 1U : 2U); var binding = Bind(m);
            var a = m.Create(0).Identity; var b = m.Create(1).Identity; m.Dispatch(); Park(m, binding, a, true, 5); m.Dispatch();
            byte[] before = m.Snapshot(); var rejected = Begin(m, binding, b, true, 1);
            HostCheck.Equal(registrationFull ? S.Full : S.ThreadFailure, rejected.Code);
            if (!registrationFull) HostCheck.Equal(KernelThreadTable.Status.DeadlineFull, rejected.ThreadStatus);
            m.Same(before); HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, binding, uint.MaxValue).Count);
        }
    }

    [HostTest("wait.address ready pressure retains completion after registration release")]
    private static void ReadyPressure()
    {
        using M m = new(2, 1, 2); Setup(m); var binding = Bind(m); var a = m.Create(0).Identity; m.Dispatch();
        var b = m.Create(1).Identity; Park(m, binding, a);
        var wake = Owner(m)->Wake(Key, 2, binding, 1); HostCheck.True(wake.Succeeded); HostCheck.Equal(1U, wake.Count);
        HostCheck.Equal(T.Pending, m.Inspect(a).State); HostCheck.Equal(C.Woken, m.Inspect(a).Completion);
        HostCheck.Equal(S.Ok, Owner(m)->Unbind(Key, 2, binding));
        HostCheck.Equal(b.Value, m.Dispatch().Value); HostCheck.Equal(KernelThreadTable.Status.Ok, m.Table->Flush(KernelThreadTableTests.Key, 2));
        m.Exit(b); HostCheck.Equal(a.Value, m.Dispatch().Value); HostCheck.Equal(C.Woken, m.Inspect(a).Completion);
    }

    [HostTest("wait.address cancellation exit and address reuse reject stale bindings")]
    private static void CancelAndReuse()
    {
        using M m = new(); Setup(m); var binding = Bind(m); var a = m.Create(0).Identity; m.Dispatch(); Park(m, binding, a, true, 2);
        HostCheck.True(m.Table->Cancel(KernelThreadTableTests.Key, 2, a).Succeeded);
        HostCheck.Equal(1U, Owner(m)->Sweep(Key, 2).Count); HostCheck.Equal(C.Cancelled, m.Inspect(a).Completion);
        HostCheck.Equal(0U, m.Table->DeadlineCount); HostCheck.Equal(S.Ok, Owner(m)->Unbind(Key, 2, binding));
        var next = Bind(m, lifetime: 2); HostCheck.True(binding.Value != next.Value);
        byte[] before = m.Snapshot(); HostCheck.Equal(S.StaleBinding, Owner(m)->Wake(Key, 2, binding, 1).Code); m.Same(before);
        m.Dispatch(); m.Exit(a); HostCheck.True(m.Table->Reap(KernelThreadTableTests.Key, 2, a, m.SupervisorRsp).Succeeded);
        m.Prepare(0); var fresh = m.Create(0).Identity; HostCheck.True(a.Value != fresh.Value);
        HostCheck.Equal(0U, Owner(m)->Wake(Key, 2, next, uint.MaxValue).Count); HostCheck.Equal(T.Ready, m.Inspect(fresh).State);
    }

    [HostTest("wait.address successive waits reject an earlier completion on the same live thread")]
    private static void WaitSequence()
    {
        using M m = new(); Setup(m); var binding = Bind(m); var a = m.Create(0).Identity; m.Dispatch(); var old = Park(m, binding, a);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, binding, 1).Count); m.Dispatch();
        var next = Begin(m, binding, a); HostCheck.True(next.Succeeded && next.Wait.Sequence != old.Sequence);
        HostCheck.Equal(KernelThreadTable.Status.StaleWait, m.Table->Wake(KernelThreadTableTests.Key, 2, old).Code);
        HostCheck.Equal(C.None, m.Inspect(a).Completion);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, binding, 1).Count);
        HostCheck.True(m.Table->Park(KernelThreadTableTests.Key, 2, next.Wait, m.Capture(a)).Succeeded);
        HostCheck.Equal(T.Running, m.Inspect(a).State);
    }

    [HostTest("wait.address last order is wakeable and exhausted generations never wrap")]
    private static void Exhaustion()
    {
        using M m = new(); Setup(m, 1, generation: uint.MaxValue, order: ulong.MaxValue - 1);
        var binding = Bind(m); var a = m.Create(0).Identity; m.Dispatch(); var last = Begin(m, binding, a); HostCheck.True(last.Succeeded);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, binding, 1).Count);
        HostCheck.True(m.Table->Park(KernelThreadTableTests.Key, 2, last.Wait, m.Capture(a)).Succeeded);
        byte[] before = m.Snapshot(); HostCheck.Equal(S.IdentityExhausted, Begin(m, binding, a).Code); m.Same(before);
        HostCheck.Equal(S.Ok, Owner(m)->Unbind(Key, 2, binding)); before = m.Snapshot();
        HostCheck.Equal(S.Full, Owner(m)->Bind(Key, 2, Word(m), 2).Code); m.Same(before);
    }

    [HostTest("wait.address warmed bind wake and unbind operations allocate no managed storage")]
    private static void Allocation()
    {
        using M m = new(); Setup(m);
        for (int i = 0; i < 100; i++) { var b = Owner(m)->Bind(Key, 2, Word(m), 1); Owner(m)->Wake(Key, 2, b.Address, 1); Owner(m)->Unbind(Key, 2, b.Address); }
        long before = GC.GetAllocatedBytesForCurrentThread(); bool good = true;
        for (int i = 0; i < 1000; i++)
        { var b = Owner(m)->Bind(Key, 2, Word(m), 1); good &= b.Succeeded; good &= Owner(m)->Wake(Key, 2, b.Address, 1).Count == 0; good &= Owner(m)->Unbind(Key, 2, b.Address) == S.Ok; }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before; HostCheck.True(good); HostCheck.Equal(0L, allocated);
    }

    [HostTest("wait.address nonzero bitsets select FIFO matches and invalid masks leave registrations untouched")]
    private static void Bitsets()
    {
        using M m = new(); Setup(m); var binding = Bind(m);
        var a = m.Create(0).Identity; var b = m.Create(1).Identity; var c = m.Create(2).Identity;
        foreach (var entry in new[] { (a, 1U), (b, 2U), (c, 3U) })
        {
            HostCheck.Equal(entry.Item1.Value, m.Dispatch().Value);
            byte[] before = m.Snapshot();
            HostCheck.Equal(S.InvalidBitset, Owner(m)->Begin(Key, 2, binding, entry.Item1, 42, 0, bitset: 0).Code); m.Same(before);
            var wait = Owner(m)->Begin(Key, 2, binding, entry.Item1, 42, 0, bitset: entry.Item2);
            HostCheck.True(wait.Succeeded);
            HostCheck.True(m.Table->Park(KernelThreadTableTests.Key, 2, wait.Wait, m.Capture(entry.Item1)).Succeeded); m.Confirm();
        }
        byte[] pending = m.Snapshot();
        HostCheck.Equal(S.InvalidBitset, Owner(m)->Wake(Key, 2, binding, uint.MaxValue, 0).Code); m.Same(pending);
        HostCheck.Equal(0U, Owner(m)->Wake(Key, 2, binding, uint.MaxValue, 4).Count);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, binding, 1, 2).Count);
        HostCheck.Equal(C.Woken, m.Inspect(b).Completion); HostCheck.Equal(T.Waiting, m.Inspect(a).State);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, binding, 1, 1).Count);
        HostCheck.Equal(C.Woken, m.Inspect(a).Completion); HostCheck.Equal(T.Waiting, m.Inspect(c).State);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, binding, uint.MaxValue).Count);
        HostCheck.Equal(b.Value, m.Dispatch().Value); m.Exit(b);
        HostCheck.Equal(a.Value, m.Dispatch().Value); m.Exit(a); HostCheck.Equal(c.Value, m.Dispatch().Value);
    }

    [HostTest("wait.address tags separate namespaces for the same word across wake and reuse")]
    private static void Tags()
    {
        using M m = new(); Setup(m); var ordinary = Bind(m);
        var tagged = Owner(m)->Bind(Key, 2, Word(m), 1, 128); HostCheck.True(tagged.Succeeded);
        var a = m.Create(0).Identity; var b = m.Create(1).Identity;
        m.Dispatch(); Park(m, ordinary, a); m.Dispatch(); Park(m, tagged.Address, b);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, tagged.Address, uint.MaxValue).Count);
        HostCheck.Equal(T.Waiting, m.Inspect(a).State); HostCheck.Equal(C.Woken, m.Inspect(b).Completion);
        HostCheck.Equal(S.Ok, Owner(m)->Unbind(Key, 2, tagged.Address));
        var next = Owner(m)->Bind(Key, 2, Word(m), 2, 128); HostCheck.True(next.Succeeded);
        HostCheck.Equal(S.StaleBinding, Owner(m)->Wake(Key, 2, tagged.Address, 1).Code);
        HostCheck.Equal(0U, Owner(m)->Wake(Key, 2, next.Address, uint.MaxValue).Count);
        HostCheck.Equal(1U, Owner(m)->Wake(Key, 2, ordinary, 1).Count);
    }

    [HostTest("wait.address owner reinitialization at the same address rejects the prior lifetime token")]
    private static void OwnerReuse()
    {
        using M m = new(); Setup(m); var old = Bind(m);
        HostCheck.Equal(S.Ok, Owner(m)->Unbind(Key, 2, old)); HostCheck.Equal(S.Ok, Owner(m)->Release(Key, 2));
        HostCheck.Equal(S.Ok, Owner(m)->Initialize(Key + 1, 2, m.Table, KernelThreadTableTests.Key,
            Slots(m), 2 * (uint)sizeof(WaitAddress.Slot), 2, Registrations(m), 3 * (uint)sizeof(WaitAddress.Registration), 3));
        var fresh = Owner(m)->Bind(Key + 1, 2, Word(m), 1); HostCheck.True(fresh.Succeeded);
        HostCheck.Equal(old.Value, fresh.Address.Value); // The owner key must disambiguate matching slot/generation/backing.
        byte[] before = m.Snapshot(); HostCheck.Equal(S.InvalidBinding, Owner(m)->Wake(Key + 1, 2, old, 1).Code); m.Same(before);
        HostCheck.Equal(0U, Owner(m)->Wake(Key + 1, 2, fresh.Address, 1).Count);
    }
}
