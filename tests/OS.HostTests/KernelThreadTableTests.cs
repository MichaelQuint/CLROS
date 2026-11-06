using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using OS.Kernel.Threading;
using OS.Pal;
using S = OS.Kernel.Threading.KernelThreadTable.Status;
using T = OS.Kernel.Threading.KernelThreadTable.State;
using W = OS.Kernel.Threading.KernelThreadTable.WaitKind;
using C = OS.Kernel.Threading.KernelThreadTable.Completion;

internal static unsafe partial class KernelThreadTableTests
{
    internal const ulong Key = 0x746872656164;
    [UnsafeAccessor(UnsafeAccessorKind.Method, Name = "InspectRetainedStorage")]
    private static extern S InspectStorage(ref KernelThreadTable table, ulong key, uint index, out ulong start, out ulong bytes, out uint count);
    [HostTest("kernel.thread structural retained inventory authenticates original key self and actual queued worker lifetimes without effects")]
    private static void StructuralRetainedInventory()
    {
        using Model m = new();
        var live = m.Create(0).Identity;
        byte[] before = m.Snapshot();
        for (uint i = 0; i < 15; i++)
        {
            HostCheck.Equal(S.Ok, InspectStorage(ref *m.Table, Key, i, out ulong start, out ulong bytes, out uint count));
            HostCheck.Equal(15U, count);
            HostCheck.Equal(S.Ok, m.Table->InspectRetainedExtent(Key, 2, i, out ulong expected, out ulong expectedBytes));
            HostCheck.True(start == expected && bytes == expectedBytes);
            if (i is 9 or 10)
                HostCheck.True(bytes != 0);
            if (i >= 11)
                HostCheck.True(start == 0 && bytes == 0);
        }

        HostCheck.Equal(S.InvalidStorage, InspectStorage(ref *m.Table, Key, 15, out ulong outside, out ulong length, out uint actual));
        HostCheck.True(outside == 0 && length == 0 && actual == 15);
        HostCheck.Equal(S.WrongOwner, InspectStorage(ref *m.Table, Key + 1, 0, out outside, out length, out actual));
        HostCheck.True(outside == 0 && length == 0 && actual == 0);
        KernelThreadTable copied = *m.Table;
        HostCheck.Equal(S.CopiedOwner, InspectStorage(ref copied, Key, 0, out outside, out length, out actual));
        HostCheck.True(outside == 0 && length == 0 && actual == 0);
        m.Same(before);
        HostCheck.Equal(live.Value, m.Dispatch().Value);
        m.Exit(live);
        HostCheck.Equal(S.Ok, m.Table->Reap(Key, 2, live, m.SupervisorRsp).Code);
        HostCheck.Equal(S.Ok, InspectStorage(ref *m.Table, Key, 9, out outside, out length, out actual));
        HostCheck.True(outside == 0 && length == 0 && actual == 15);
        HostCheck.Equal(S.Ok, m.Table->Release(Key, 2, m.SupervisorRsp));
        HostCheck.Equal(S.Uninitialized, InspectStorage(ref *m.Table, Key, 0, out outside, out length, out actual));
        HostCheck.True(outside == 0 && length == 0 && actual == 0);
    }

    [HostTest("kernel.thread initialization rejects owner storage and alias failures atomically")]
    private static void InitializeFailures()
    {
        using Model m = new(initialize: false);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.InvalidCapacity, m.Bind(capacity: 0));
        m.Same(before);
        HostCheck.Equal(S.WrongOwner, m.Bind(key: 0));
        m.Same(before);
        HostCheck.Equal(S.InterruptsEnabled, m.Bind(flags: 0x202));
        m.Same(before);
        HostCheck.Equal(S.InvalidStorage, m.Bind(slots: (KernelThreadSlot*)((byte*)m.Slots + 1)));
        m.Same(before);
        HostCheck.Equal(S.Overlap, m.Bind(readyBuffer: (OSHostHandle*)m.Slots));
        m.Same(before);
        var supervisor = m.Supervisor;
        m.Supervisor.Context = (X64ThreadContext*)((byte*)m.Supervisor.Context + 8);
        HostCheck.Equal(S.InvalidContext, m.Bind());
        m.Supervisor = supervisor;
        m.Same(before);
        m.Owner.CpuCount = 2;
        HostCheck.Equal(S.InvalidContext, m.Bind());
        m.Owner.CpuCount = 1;
        m.Same(before);
        HostCheck.Equal(S.Ok, m.Bind());
        before = m.Snapshot();
        HostCheck.Equal(S.AlreadyInitialized, m.Bind());
        m.Same(before);
        HostCheck.True(!default(KernelThreadTable.Result).Succeeded);
    }

    [HostTest("kernel.thread create publishes complete bindings and rolls back all resource failures")]
    private static void CreateFailures()
    {
        using Model m = new(3, 1, 3);
        var a = m.Create(0);
        HostCheck.True(a.Succeeded);
        HostCheck.Equal(m.Code + 0x400, m.Inspect(a.Identity).Entry);
        HostCheck.Equal(0xabcd0000UL, m.Inspect(a.Identity).Argument);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.InvalidContext, m.Table->Create(Key, 2, null, 688, m.Stack(1), 0x3000, m.Code + 0x100, m.Code + 0x400, 0).Code);
        m.Same(before);
        HostCheck.Equal(S.InvalidContext, m.Table->Create(Key, 2, m.Context(1), 688, 0, 0x3000, m.Code + 0x100, m.Code + 0x400, 0).Code);
        m.Same(before);
        HostCheck.Equal(S.InvalidEntry, m.Table->Create(Key, 2, m.Context(1), 688, m.Stack(1), 0x3000, m.Code + 0x100, 0, 0).Code);
        m.Same(before);
        HostCheck.Equal(S.Overlap, m.Table->Create(Key, 2, m.Context(0), 688, m.Stack(0), 0x3000, m.Code + 0x100, m.Code + 0x400, 0).Code);
        m.Same(before);
        HostCheck.Equal(S.ReadyFull, m.Create(1).Code);
        m.Same(before);
        KernelThreadTable copied = *m.Table;
        HostCheck.Equal(S.CopiedOwner, copied.Dispatch(Key, 2).Code);
        m.Same(before);
        HostCheck.Equal(S.WrongOwner, m.Table->Dispatch(Key + 1, 2).Code);
        m.Same(before);
        HostCheck.Equal(S.InterruptsEnabled, m.Table->Dispatch(Key, 0x202).Code);
        m.Same(before);
        HostCheck.Equal(S.Ok, m.Table->Dispatch(Key, 2).Code);
        var b = m.Create(1);
        HostCheck.True(b.Succeeded); // No slot was consumed by the rejected publications.
        HostCheck.Equal(2U, (uint)b.Identity.Value);
    }

    [HostTest("kernel.thread next create identity is read only matches publication and advances after real reap")]
    private static void NextCreateIdentity()
    {
        using Model m = new(generation: 7);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.Ok, m.Table->InspectNextCreateIdentity(Key, 2, out var first));
        m.Same(before);
        HostCheck.Equal(0x700000001UL, first.Value);
        HostCheck.Equal(S.Ok, m.Table->InspectNextCreateIdentity(Key, 2, out var repeated));
        m.Same(before);
        HostCheck.Equal(first.Value, repeated.Value); // Inspection reserves nothing.
        var a = m.Create(0);
        HostCheck.True(a.Succeeded);
        HostCheck.Equal(first.Value, a.Identity.Value);
        before = m.Snapshot();
        HostCheck.Equal(S.Ok, m.Table->InspectNextCreateIdentity(Key, 2, out var second));
        m.Same(before);
        HostCheck.Equal(0x700000002UL, second.Value);
        var b = m.Create(1);
        HostCheck.True(b.Succeeded);
        HostCheck.Equal(second.Value, b.Identity.Value);
        HostCheck.Equal(a.Identity.Value, m.Dispatch().Value);
        m.Exit(a.Identity, 17);
        HostCheck.True(m.Table->Reap(Key, 2, a.Identity, m.SupervisorRsp).Succeeded);
        before = m.Snapshot();
        HostCheck.Equal(S.Ok, m.Table->InspectNextCreateIdentity(Key, 2, out var reused));
        m.Same(before);
        HostCheck.Equal(0x800000001UL, reused.Value); // Reaped hole precedes the still-live second slot.
        m.Prepare(0);
        var next = m.Create(0);
        HostCheck.True(next.Succeeded);
        HostCheck.Equal(reused.Value, next.Identity.Value);
    }

    [HostTest("kernel.thread next create identity failures clear output without changing owner or buffers")]
    private static void NextCreateIdentityFailures()
    {
        using Model m = new();
        byte[] before = m.Snapshot();
        OSHostHandle identity = new(ulong.MaxValue);
        HostCheck.Equal(S.WrongOwner, m.Table->InspectNextCreateIdentity(Key + 1, 2, out identity));
        HostCheck.Equal(0UL, identity.Value);
        m.Same(before);
        identity = new(ulong.MaxValue);
        HostCheck.Equal(S.InterruptsEnabled, m.Table->InspectNextCreateIdentity(Key, 0x202, out identity));
        HostCheck.Equal(0UL, identity.Value);
        m.Same(before);
        identity = new(ulong.MaxValue);
        KernelThreadTable copy = *m.Table;
        byte[] copyBefore = new ReadOnlySpan<byte>(&copy, sizeof(KernelThreadTable)).ToArray();
        HostCheck.Equal(S.CopiedOwner, copy.InspectNextCreateIdentity(Key, 2, out identity));
        HostCheck.Equal(0UL, identity.Value);
        m.Same(before);
        HostCheck.True(new ReadOnlySpan<byte>(&copy, sizeof(KernelThreadTable)).SequenceEqual(copyBefore));
        using Model uninitialized = new(initialize: false);
        before = uninitialized.Snapshot();
        identity = new(ulong.MaxValue);
        HostCheck.Equal(S.Uninitialized, uninitialized.Table->InspectNextCreateIdentity(Key, 2, out identity));
        HostCheck.Equal(0UL, identity.Value);
        uninitialized.Same(before);
        using Model pressure = new(2, 1, 2);
        HostCheck.True(pressure.Create(0).Succeeded);
        before = pressure.Snapshot();
        identity = new(ulong.MaxValue);
        HostCheck.Equal(S.ReadyFull, pressure.Table->InspectNextCreateIdentity(Key, 2, out identity));
        HostCheck.Equal(0UL, identity.Value);
        pressure.Same(before);
        using Model full = new(1, 1, 1, generation: uint.MaxValue);
        var a = full.Create(0);
        HostCheck.True(a.Succeeded);
        before = full.Snapshot();
        identity = new(ulong.MaxValue);
        HostCheck.Equal(S.Full, full.Table->InspectNextCreateIdentity(Key, 2, out identity));
        HostCheck.Equal(0UL, identity.Value);
        full.Same(before);
        full.Dispatch();
        full.Exit(a.Identity);
        HostCheck.True(full.Table->Reap(Key, 2, a.Identity, full.SupervisorRsp).Succeeded);
        before = full.Snapshot();
        identity = new(ulong.MaxValue);
        HostCheck.Equal(S.Full, full.Table->InspectNextCreateIdentity(Key, 2, out identity));
        HostCheck.Equal(0UL, identity.Value);
        full.Same(before); // Retired generations never become prospective identities.
    }

    [HostTest("kernel.thread handoffs pin execution until supervisor confirmation and scrub only at reap")]
    private static void HandoffAndReap()
    {
        using Model m = new(2, 2, 2);
        var a = m.Create(0);
        var b = m.Create(1);
        HostCheck.True(a.Succeeded && b.Succeeded);
        HostCheck.Equal(a.Identity.Value, m.Dispatch().Value);
        HostCheck.Equal(S.Busy, m.Table->Dispatch(Key, 2).Code);
        var captured = m.Capture(a.Identity);
        HostCheck.True(m.Table->Yield(Key, 2, a.Identity, captured).Succeeded);
        HostCheck.True(m.Table->HandoffPending);
        HostCheck.Equal(S.Busy, m.Table->Dispatch(Key, 2).Code);
        HostCheck.Equal(S.NotSupervisor, m.Table->ConfirmSupervisor(Key, 2, m.Stack(0) + 5000));
        HostCheck.Equal(S.NotSupervisor, m.Table->Reap(Key, 2, a.Identity, m.SupervisorRsp).Code);
        m.Confirm();
        HostCheck.Equal(b.Identity.Value, m.Dispatch().Value);
        HostCheck.True(m.Table->Exit(Key, 2, b.Identity, m.Capture(b.Identity), 42).Succeeded);
        HostCheck.Equal(T.Exiting, m.Inspect(b.Identity).State);
        HostCheck.Equal(S.NotSupervisor, m.Table->Reap(Key, 2, b.Identity, m.SupervisorRsp).Code);
        m.Confirm();
        HostCheck.Equal(T.Exited, m.Inspect(b.Identity).State);
        HostCheck.Equal(42, m.Inspect(b.Identity).ExitValue);
        HostCheck.Equal(S.Ok, m.Table->Pin(Key, 2, b.Identity, true));
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.Pinned, m.Table->Reap(Key, 2, b.Identity, m.SupervisorRsp).Code);
        m.Same(before);
        HostCheck.Equal(S.Ok, m.Table->Pin(Key, 2, b.Identity, false));
        var reaped = m.Table->Reap(Key, 2, b.Identity, m.SupervisorRsp);
        HostCheck.True(reaped.Succeeded);
        HostCheck.True(new ReadOnlySpan<byte>(reaped.Context.Context, 688).ToArray().All(v => v == 0));
        HostCheck.True(new ReadOnlySpan<byte>((void*)(m.Stack(1) + 4096), 4096).ToArray().All(v => v == 0));
        HostCheck.Equal((byte)0xa5, *(byte*)m.Stack(1));
        HostCheck.Equal((byte)0xa5, *(byte*)(m.Stack(1) + 8192));
        HostCheck.Equal(S.StaleIdentity, m.Inspect(b.Identity).Code);
        HostCheck.Equal(a.Identity.Value, m.Dispatch().Value);
        m.Exit(a.Identity);
        HostCheck.True(m.Table->Reap(Key, 2, a.Identity, m.SupervisorRsp).Succeeded);
        HostCheck.Equal(S.Ok, m.Table->Release(Key, 2, m.SupervisorRsp));
    }

    [HostTest("kernel.thread wake before park and after park cannot complete a later wait")]
    private static void WaitIdentity()
    {
        using Model m = new(1, 1, 1);
        var a = m.Create(0);
        m.Dispatch();
        var first = m.Table->BeginWait(Key, 2, a.Identity, W.Park, 10);
        HostCheck.True(first.Succeeded);
        HostCheck.Equal(S.Ok, m.Table->Wake(Key, 2, first.Wait).Code);
        var resumed = m.Table->Park(Key, 2, first.Wait, m.Capture(a.Identity));
        HostCheck.Equal(T.Running, resumed.State);
        HostCheck.Equal(C.Woken, resumed.Completion);
        HostCheck.True(!m.Table->HandoffPending);
        HostCheck.Equal(0U, m.Table->ReadyCount);
        var second = m.Table->BeginWait(Key, 2, a.Identity, W.Park, 10);
        HostCheck.True(second.Wait.Sequence > first.Wait.Sequence);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.StaleWait, m.Table->Wake(Key, 2, first.Wait).Code);
        m.Same(before);
        HostCheck.True(m.Table->Park(Key, 2, second.Wait, m.Capture(a.Identity)).Succeeded);
        m.Confirm();
        HostCheck.Equal(T.Waiting, m.Inspect(a.Identity).State);
        HostCheck.True(m.Table->Wake(Key, 2, second.Wait).Succeeded);
        HostCheck.Equal(S.AlreadyCompleted, m.Table->Wake(Key, 2, second.Wait).Code);
        HostCheck.Equal(a.Identity.Value, m.Dispatch().Value);
        var third = m.Table->BeginWait(Key, 2, a.Identity, W.Park, 11);
        HostCheck.Equal(S.StaleWait, m.Table->Wake(Key, 2, second.Wait).Code);
        HostCheck.True(third.Wait.Sequence > second.Wait.Sequence);
    }

    [HostTest("kernel.thread deadline and cancellation have one winner across ready exhaustion")]
    private static void CompletionWinner()
    {
        foreach (bool cancelFirst in new[]
        {
            false,
            true
        }

        )
        {
            using Model m = new(2, 1, 2);
            var a = m.Create(0);
            m.Dispatch();
            var sleep = m.Table->Sleep(Key, 2, a.Identity, m.Capture(a.Identity), 0, 2);
            HostCheck.True(sleep.Succeeded);
            m.Confirm();
            var b = m.Create(1);
            HostCheck.True(b.Succeeded);
            if (cancelFirst)
                HostCheck.Equal(S.Pending, m.Table->Cancel(Key, 2, a.Identity).Code);
            HostCheck.Equal(S.Ok, m.Table->Advance(Key, 2, 2));
            if (!cancelFirst)
                HostCheck.True(m.Table->Cancel(Key, 2, a.Identity).Succeeded);
            HostCheck.Equal(T.Pending, m.Inspect(a.Identity).State);
            HostCheck.Equal(cancelFirst ? C.Cancelled : C.Deadline, m.Inspect(a.Identity).Completion);
            HostCheck.Equal(0U, m.Table->DeadlineCount);
            HostCheck.Equal(S.AlreadyCompleted, m.Table->Wake(Key, 2, sleep.Wait).Code);
            HostCheck.Equal(S.ReadyFull, m.Table->Flush(Key, 2));
            HostCheck.Equal(b.Identity.Value, m.Dispatch().Value);
            HostCheck.Equal(S.Ok, m.Table->Flush(Key, 2));
            m.Exit(b.Identity);
            var resumed = m.Table->Dispatch(Key, 2);
            HostCheck.Equal(a.Identity.Value, resumed.Identity.Value);
            HostCheck.True(resumed.CancellationRequested);
            HostCheck.Equal(cancelFirst ? C.Cancelled : C.Deadline, resumed.Completion);
        }
    }

    [HostTest("kernel.thread signal interruption preserves exact wait winners and never requests cancellation")]
    private static void InterruptedWaits()
    {
        for (int variant = 0; variant < 5; variant++)
        {
            using Model m = new(1, 1, 1);
            var a = m.Create(0);
            m.Dispatch();
            var wait = m.Table->BeginWait(Key, 2, a.Identity, W.Sleep, 10, 5);
            HostCheck.True(wait.Succeeded);
            if (variant == 0)
            {
                HostCheck.True(m.Table->Interrupt(Key, 2, wait.Wait).Succeeded);
                var inPlace = m.Table->Park(Key, 2, wait.Wait, m.Capture(a.Identity));
                HostCheck.True(inPlace.State == T.Running && inPlace.Completion == C.Interrupted && !inPlace.CancellationRequested && !m.Table->HandoffPending);
            }
            else
            {
                HostCheck.True(m.Table->Park(Key, 2, wait.Wait, m.Capture(a.Identity)).Succeeded);
                m.Confirm();
                if (variant == 2)
                    HostCheck.True(m.Table->Wake(Key, 2, wait.Wait).Succeeded);
                if (variant == 3)
                    HostCheck.Equal(S.Ok, m.Table->Advance(Key, 2, 15));
                if (variant == 4)
                    HostCheck.True(m.Table->Cancel(Key, 2, a.Identity).Succeeded);
                var interrupted = m.Table->Interrupt(Key, 2, wait.Wait);
                HostCheck.Equal(variant == 1 ? S.Ok : S.AlreadyCompleted, interrupted.Code);
                HostCheck.Equal(a.Identity.Value, m.Dispatch().Value);
            }

            var observed = m.Inspect(a.Identity);
            HostCheck.Equal(variant == 2 ? C.Woken : variant == 3 ? C.Deadline : variant == 4 ? C.Cancelled : C.Interrupted, observed.Completion);
            HostCheck.Equal(variant == 4, observed.CancellationRequested);
            HostCheck.Equal(0U, m.Table->DeadlineCount);
            byte[] before = m.Snapshot();
            HostCheck.Equal(S.AlreadyCompleted, m.Table->Interrupt(Key, 2, wait.Wait).Code);
            m.Same(before);
        }
    }

    [HostTest("kernel.thread interrupt admission stale sequences joins and full ready queue retain one completion")]
    private static void InterruptAdmission()
    {
        using Model m = new(2, 1, 2);
        var a = m.Create(0);
        m.Dispatch();
        var wait = m.Table->BeginWait(Key, 2, a.Identity, W.Sleep, 0, 2);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.WrongOwner, m.Table->Interrupt(Key + 1, 2, wait.Wait).Code);
        m.Same(before);
        HostCheck.Equal(S.InterruptsEnabled, m.Table->Interrupt(Key, 0x202, wait.Wait).Code);
        m.Same(before);
        KernelThreadTable copy = *m.Table;
        HostCheck.Equal(S.CopiedOwner, copy.Interrupt(Key, 2, wait.Wait).Code);
        m.Same(before);
        HostCheck.True(m.Table->Park(Key, 2, wait.Wait, m.Capture(a.Identity)).Succeeded);
        m.Confirm();
        var b = m.Create(1);
        HostCheck.True(b.Succeeded);
        HostCheck.Equal(S.Pending, m.Table->Interrupt(Key, 2, wait.Wait).Code);
        HostCheck.Equal(T.Pending, m.Inspect(a.Identity).State);
        HostCheck.Equal(S.Ok, m.Table->Advance(Key, 2, 2));
        HostCheck.Equal(C.Interrupted, m.Inspect(a.Identity).Completion);
        HostCheck.Equal(S.ReadyFull, m.Table->Flush(Key, 2));
        HostCheck.Equal(b.Identity.Value, m.Dispatch().Value);
        HostCheck.Equal(S.Ok, m.Table->Flush(Key, 2));
        m.Exit(b.Identity);
        HostCheck.Equal(a.Identity.Value, m.Dispatch().Value);
        var later = m.Table->BeginWait(Key, 2, a.Identity, W.Park, 2);
        HostCheck.True(later.Succeeded);
        before = m.Snapshot();
        HostCheck.Equal(S.StaleWait, m.Table->Interrupt(Key, 2, wait.Wait).Code);
        m.Same(before);
        using Model joined = new();
        var waiter = joined.Create(0);
        var target = joined.Create(1);
        joined.Dispatch();
        var join = joined.Table->BeginWait(Key, 2, waiter.Identity, W.Join, 0, target: target.Identity);
        HostCheck.True(join.Succeeded);
        before = joined.Snapshot();
        HostCheck.Equal(S.WrongState, joined.Table->Interrupt(Key, 2, join.Wait).Code);
        joined.Same(before);
    }

    [HostTest("kernel.thread joins reject cycles and second waiter and retain exit until reap")]
    private static void JoinLifetime()
    {
        using Model m = new();
        var a = m.Create(0);
        var b = m.Create(1);
        var c = m.Create(2);
        HostCheck.Equal(a.Identity.Value, m.Dispatch().Value);
        var join = m.Table->BeginWait(Key, 2, a.Identity, W.Join, 0, target: b.Identity);
        HostCheck.True(join.Succeeded);
        HostCheck.True(m.Table->Park(Key, 2, join.Wait, m.Capture(a.Identity)).Succeeded);
        m.Confirm();
        HostCheck.Equal(b.Identity.Value, m.Dispatch().Value);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.SelfJoin, m.Table->BeginWait(Key, 2, b.Identity, W.Join, 0, target: b.Identity).Code);
        m.Same(before);
        HostCheck.Equal(S.JoinCycle, m.Table->BeginWait(Key, 2, b.Identity, W.Join, 0, target: a.Identity).Code);
        m.Same(before);
        HostCheck.True(m.Table->Yield(Key, 2, b.Identity, m.Capture(b.Identity)).Succeeded);
        m.Confirm();
        HostCheck.Equal(c.Identity.Value, m.Dispatch().Value);
        HostCheck.Equal(S.JoinBusy, m.Table->BeginWait(Key, 2, c.Identity, W.Join, 0, target: b.Identity).Code);
        HostCheck.True(m.Table->Cancel(Key, 2, a.Identity).Succeeded);
        HostCheck.True(!m.Inspect(b.Identity).CancellationRequested);
        join = m.Table->BeginWait(Key, 2, c.Identity, W.Join, 0, target: b.Identity);
        HostCheck.True(join.Succeeded);
        HostCheck.True(m.Table->Park(Key, 2, join.Wait, m.Capture(c.Identity)).Succeeded);
        m.Confirm();
        HostCheck.Equal(b.Identity.Value, m.Dispatch().Value);
        m.Exit(b.Identity, 42);
        HostCheck.Equal(a.Identity.Value, m.Dispatch().Value);
        m.Exit(a.Identity, -1);
        var winner = m.Table->Dispatch(Key, 2);
        HostCheck.Equal(c.Identity.Value, winner.Identity.Value);
        HostCheck.Equal(C.Joined, winner.Completion);
        HostCheck.Equal(42, winner.JoinedExitValue);
        for (int i = 0; i < 2; i++)
        {
            var completed = m.Table->BeginWait(Key, 2, c.Identity, W.Join, 0, target: b.Identity);
            HostCheck.True(completed.Succeeded);
            HostCheck.Equal(C.Joined, completed.Completion);
            HostCheck.Equal(42, completed.JoinedExitValue);
            HostCheck.Equal(T.Running, m.Inspect(c.Identity).State);
        }

        m.Exit(c.Identity);
        HostCheck.True(m.Table->Reap(Key, 2, b.Identity, m.SupervisorRsp).Succeeded);
        HostCheck.Equal(S.StaleIdentity, m.Table->Inspect(Key, 2, b.Identity).Code);
    }

    [HostTest("kernel.thread join timeout releases registration and leaves target running")]
    private static void JoinTimeout()
    {
        using Model m = new();
        var a = m.Create(0);
        var b = m.Create(1);
        var c = m.Create(2);
        m.Dispatch();
        var join = m.Table->BeginWait(Key, 2, a.Identity, W.Join, 0, 2, b.Identity, true);
        HostCheck.True(m.Table->Park(Key, 2, join.Wait, m.Capture(a.Identity)).Succeeded);
        m.Confirm();
        HostCheck.Equal(b.Identity.Value, m.Dispatch().Value);
        HostCheck.Equal(S.Ok, m.Table->Advance(Key, 2, 2));
        HostCheck.Equal(T.Running, m.Inspect(b.Identity).State);
        HostCheck.True(!m.Inspect(b.Identity).CancellationRequested);
        HostCheck.Equal(C.Deadline, m.Inspect(a.Identity).Completion);
        HostCheck.True(m.Table->Yield(Key, 2, b.Identity, m.Capture(b.Identity)).Succeeded);
        m.Confirm();
        HostCheck.Equal(c.Identity.Value, m.Dispatch().Value);
        HostCheck.True(m.Table->BeginWait(Key, 2, c.Identity, W.Join, 2, target: b.Identity).Succeeded);
    }

    [HostTest("kernel.thread generation reuse and sequence exhaustion fail closed")]
    private static void IdentityExhaustion()
    {
        using (Model m = new(1, 1, 1))
        {
            var a = m.Create(0);
            m.Dispatch();
            var wait = m.Table->BeginWait(Key, 2, a.Identity, W.Park, 0);
            HostCheck.True(m.Table->Wake(Key, 2, wait.Wait).Succeeded);
            HostCheck.True(m.Table->Park(Key, 2, wait.Wait, m.Capture(a.Identity)).Succeeded);
            m.Exit(a.Identity);
            HostCheck.True(m.Table->Reap(Key, 2, a.Identity, m.SupervisorRsp).Succeeded);
            m.Prepare(0);
            var next = m.Create(0);
            HostCheck.True(next.Succeeded);
            HostCheck.Equal((uint)a.Identity.Value, (uint)next.Identity.Value);
            HostCheck.Equal((uint)(a.Identity.Value >> 32) + 1, (uint)(next.Identity.Value >> 32));
            HostCheck.Equal(S.StaleIdentity, m.Inspect(a.Identity).Code);
            HostCheck.Equal(S.StaleIdentity, m.Table->Wake(Key, 2, wait.Wait).Code);
        }

        using (Model m = new(1, 1, 1, sequence: ulong.MaxValue))
        {
            var a = m.Create(0);
            m.Dispatch();
            byte[] before = m.Snapshot();
            HostCheck.Equal(S.IdentityExhausted, m.Table->BeginWait(Key, 2, a.Identity, W.Park, 0).Code);
            m.Same(before);
            HostCheck.True(m.Table->Sleep(Key, 2, a.Identity, m.Capture(a.Identity), 0, 0).Succeeded);
            HostCheck.Equal(0U, m.Table->DeadlineCount);
        }

        using (Model m = new(1, 1, 1, generation: uint.MaxValue))
        {
            var a = m.Create(0);
            m.Dispatch();
            m.Exit(a.Identity);
            HostCheck.True(m.Table->Reap(Key, 2, a.Identity, m.SupervisorRsp).Succeeded);
            HostCheck.Equal(S.Full, m.Create(1).Code); // Retired rather than wrapping generation to zero/one.
        }
    }

    [HostTest("kernel.thread deadline capacity clock regression and overflow preserve running state")]
    private static void DeadlineAdmission()
    {
        using Model m = new(2, 2, 1);
        var a = m.Create(0);
        var b = m.Create(1);
        m.Dispatch();
        HostCheck.True(m.Table->Sleep(Key, 2, a.Identity, m.Capture(a.Identity), 10, 5).Succeeded);
        m.Confirm();
        HostCheck.Equal(b.Identity.Value, m.Dispatch().Value);
        var captured = m.Capture(b.Identity);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.DeadlineFull, m.Table->Sleep(Key, 2, b.Identity, captured, 10, 1).Code);
        m.Same(before);
        HostCheck.Equal(S.ClockRegression, m.Table->Sleep(Key, 2, b.Identity, captured, 9, 1).Code);
        m.Same(before);
        HostCheck.Equal(S.DeadlineOverflow, m.Table->Sleep(Key, 2, b.Identity, captured, ulong.MaxValue, 1).Code);
        m.Same(before);
        HostCheck.Equal(b.Identity.Value, m.Table->Current.Value);
        HostCheck.Equal(1U, m.Table->DeadlineCount);
    }

    [HostTest("kernel.thread cancellation before entry and parked cancellation are deferred until safe exit")]
    private static void DeferredCancellation()
    {
        using Model m = new(1, 1, 1);
        var a = m.Create(0);
        HostCheck.True(m.Table->Cancel(Key, 2, a.Identity).Succeeded);
        var start = m.Table->Dispatch(Key, 2);
        HostCheck.True(start.CancellationRequested);
        byte[] before = m.Snapshot();
        HostCheck.Equal(S.Cancelled, m.Table->BeginWait(Key, 2, a.Identity, W.Park, 0).Code);
        m.Same(before);
        HostCheck.Equal(T.Running, m.Inspect(a.Identity).State); // Adapter must take its cooperative exit path; no stack is freed here.
        m.Exit(a.Identity, -1);
        HostCheck.True(m.Table->Reap(Key, 2, a.Identity, m.SupervisorRsp).Succeeded);
        HostCheck.Equal(S.Ok, m.Table->Release(Key, 2, m.SupervisorRsp));
    }

    [HostTest("kernel.thread joined exit and deadline retain one winner and never replace the joiner exit")]
    private static void JoinedExitIsDistinct()
    {
        foreach (bool exitFirst in new[]
        {
            false,
            true
        }

        )
        {
            using Model m = new(3, 1, 3);
            var a = m.Create(0);
            m.Dispatch();
            var b = m.Create(1);
            var join = m.Table->BeginWait(Key, 2, a.Identity, W.Join, 0, 2, b.Identity, true);
            HostCheck.True(m.Table->Park(Key, 2, join.Wait, m.Capture(a.Identity)).Succeeded);
            m.Confirm();
            HostCheck.Equal(b.Identity.Value, m.Dispatch().Value);
            HostCheck.True(m.Create(2).Succeeded); // Force the first completion into retained Pending.
            if (!exitFirst)
                HostCheck.Equal(S.Ok, m.Table->Advance(Key, 2, 2));
            m.Exit(b.Identity, 17);
            HostCheck.Equal(S.Ok, m.Table->Advance(Key, 2, 2));
            HostCheck.True(m.Table->Cancel(Key, 2, a.Identity).Succeeded);
            var pending = m.Inspect(a.Identity);
            HostCheck.Equal(T.Pending, pending.State);
            HostCheck.Equal(exitFirst ? C.Joined : C.Deadline, pending.Completion);
            HostCheck.Equal(exitFirst ? 17 : 0, pending.JoinedExitValue);
            var c = m.Dispatch();
            HostCheck.Equal(S.Ok, m.Table->Flush(Key, 2));
            m.Exit(c);
            HostCheck.Equal(a.Identity.Value, m.Dispatch().Value);
            m.Exit(a.Identity, 23);
            HostCheck.Equal(23, m.Inspect(a.Identity).ExitValue);
            var reaped = m.Table->Reap(Key, 2, a.Identity, m.SupervisorRsp);
            HostCheck.True(reaped.Succeeded);
            HostCheck.Equal(23, reaped.ExitValue);
            HostCheck.Equal(exitFirst ? 17 : 0, reaped.JoinedExitValue);
        }
    }

    [HostTest("kernel.thread lookup and external lifetime pins allocate no managed memory")]
    private static void AllocationFreeOperations()
    {
        using Model m = new(1, 1, 1);
        var a = m.Create(0);
        m.Inspect(a.Identity);
        m.Table->Pin(Key, 2, a.Identity, true);
        m.Table->Pin(Key, 2, a.Identity, false);
        bool valid = true;
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            valid &= m.Table->Inspect(Key, 2, a.Identity).Succeeded;
            valid &= m.Table->Pin(Key, 2, a.Identity, true) == S.Ok;
            valid &= m.Table->Pin(Key, 2, a.Identity, false) == S.Ok;
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(valid);
        HostCheck.Equal(0L, allocated);
        HostCheck.Equal(0U, m.Inspect(a.Identity).Pins);
    }
}
