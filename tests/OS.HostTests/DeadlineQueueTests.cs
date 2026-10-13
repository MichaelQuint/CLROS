using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Threading;
using OS.Pal;

internal static unsafe class DeadlineQueueTests
{
    private const ulong Key = 0x54494d4552;
    private static OSHostHandle Id(uint slot, uint generation = 1) => new(((ulong)generation << 32) | slot);
    private sealed class Owner : IDisposable
    {
        public readonly byte* Buffer = (byte*)NativeMemory.AlignedAlloc(16384, 16);
        public DeadlineQueue.Entry* Entries => (DeadlineQueue.Entry*)(Buffer + 16);
        public DeadlineQueue.Entry* Output => (DeadlineQueue.Entry*)(Buffer + 8192);
        public Owner() => new Span<byte>(Buffer, 16384).Fill(0xa5);
        public void Dispose() => NativeMemory.AlignedFree(Buffer);
        public byte[] Snapshot() => new ReadOnlySpan<byte>(Buffer, 16384).ToArray();
        public DeadlineQueue Queue(uint capacity, ulong now = 0)
        {
            DeadlineQueue queue = default;
            HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Initialize(Key, Entries, capacity * 16, capacity, now));
            return queue; // Transfer sole test ownership before any operation.
        }
        public DeadlineQueue.DrainResult Drain(ref DeadlineQueue queue, ulong now, uint limit) =>
            queue.Drain(Key, now, Output, limit * 16, limit);
    }
    private static byte[] State(ref DeadlineQueue queue)
    {
        fixed (DeadlineQueue* value = &queue) return new ReadOnlySpan<byte>(value, sizeof(DeadlineQueue)).ToArray();
    }
    private static void Same(byte[] state, byte[] buffer, ref DeadlineQueue queue, Owner owner)
    {
        HostCheck.True(state.SequenceEqual(State(ref queue))); HostCheck.True(buffer.SequenceEqual(owner.Snapshot()));
    }
    private static void Add(ref DeadlineQueue queue, uint slot, ulong deadline, uint generation = 1, ulong now = 0) =>
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Schedule(Key, Id(slot, generation), Id(slot, generation), now, deadline - now));

    [HostTest("m09.003", HostLane.Fast, 0)]
    private static void Storage()
    {
        HostCheck.Equal(16, sizeof(DeadlineQueue.Entry));
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<DeadlineQueue>() &&
            !RuntimeHelpers.IsReferenceOrContainsReferences<DeadlineQueue.Entry>());
        HostCheck.True(!default(DeadlineQueue.Result).Succeeded && !default(DeadlineQueue.DrainResult).Succeeded);
        using Owner owner = new(); DeadlineQueue queue = default;
        HostCheck.True(!queue.IsInitialized && !queue.IsEmpty && !queue.HasPending && !queue.IsFull);
        HostCheck.Equal(DeadlineQueue.Status.Uninitialized, queue.Peek(Key).Code);
        foreach (var bad in new (ulong Owner, ulong Address, uint Bytes, uint Capacity, DeadlineQueue.Status Code)[]
        {
            (0, (ulong)owner.Entries, 16, 1, DeadlineQueue.Status.InvalidOwner),
            (Key, (ulong)owner.Entries, 16, 0, DeadlineQueue.Status.InvalidCapacity),
            (Key, 0, 16, 1, DeadlineQueue.Status.InvalidBuffer),
            (Key, (ulong)owner.Entries + 1, 16, 1, DeadlineQueue.Status.InvalidBuffer),
            (Key, (ulong)owner.Entries, 15, 1, DeadlineQueue.Status.BufferTooSmall),
            (Key, (ulong)owner.Entries, uint.MaxValue, uint.MaxValue, DeadlineQueue.Status.BufferTooSmall),
            (Key, ulong.MaxValue - 7, 16, 1, DeadlineQueue.Status.InvalidBuffer),
            (Key, 0x0000800000000000, 16, 1, DeadlineQueue.Status.InvalidBuffer),
            (Key, 0x00007ffffffffff8, 16, 1, DeadlineQueue.Status.InvalidBuffer)
        })
        {
            byte[] state = State(ref queue), buffer = owner.Snapshot();
            HostCheck.Equal(bad.Code, queue.Initialize(bad.Owner, (DeadlineQueue.Entry*)bad.Address, bad.Bytes, bad.Capacity));
            Same(state, buffer, ref queue, owner);
        }
        DeadlineQueue* metadata = &queue;
        HostCheck.Equal(DeadlineQueue.Status.InvalidBuffer, queue.Initialize(Key, (DeadlineQueue.Entry*)metadata, (uint)sizeof(DeadlineQueue), 1));
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Initialize(Key, owner.Entries, 16, 1, 5));
        HostCheck.True(queue.IsInitialized && queue.IsEmpty && queue.ObservedTicks == 5);
        HostCheck.Equal(DeadlineQueue.Status.AlreadyInitialized, queue.Initialize(0, null, 0, 0));
    }

    [HostTest("m09.003 ordered bounded expiration never early and equal deadlines are stable", HostLane.Fast, 1)]
    private static void Order()
    {
        using Owner owner = new(); DeadlineQueue queue = owner.Queue(4);
        Add(ref queue, 1, 30); Add(ref queue, 2, 10); Add(ref queue, 3, 20); Add(ref queue, 4, 20);
        HostCheck.True(queue.IsFull); HostCheck.Equal(10UL, queue.Peek(Key).Value.Deadline);
        byte[] untouchedOutput = new ReadOnlySpan<byte>(owner.Output, 64).ToArray();
        DeadlineQueue.DrainResult result = owner.Drain(ref queue, 9, 2);
        HostCheck.True(result.Succeeded && result.Count == 0 && queue.HasPending && queue.Count == 4);
        HostCheck.True(untouchedOutput.SequenceEqual(new ReadOnlySpan<byte>(owner.Output, 64).ToArray()));
        foreach (var step in new (ulong Now, uint Slot)[] { (10, 2), (20, 3), (20, 4), (30, 1) })
        {
            result = owner.Drain(ref queue, step.Now, 1);
            HostCheck.True(result.Succeeded && result.Count == 1);
            HostCheck.Equal(Id(step.Slot).Value, owner.Output[0].Identity.Value);
            HostCheck.True(owner.Output[0].Deadline <= step.Now);
        }
        HostCheck.True(queue.IsEmpty && !queue.HasPending);
        for (int i = 0; i < 4; i++) HostCheck.Equal(0UL, owner.Entries[i].Identity.Value);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Buffer, 16).ToArray().All(b => b == 0xa5));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Buffer + 80, 8112).ToArray().All(b => b == 0xa5));
    }

    [HostTest("m09.003 full rearm preserves deterministic equal-deadline insertion and cancellation order", HostLane.Fast, 2)]
    private static void Rearm()
    {
        using Owner owner = new(); DeadlineQueue queue = owner.Queue(3);
        Add(ref queue, 1, 5); Add(ref queue, 2, 5); Add(ref queue, 3, 5);
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Rearm(Key, Id(1), Id(1), 0, 5));
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Rearm(Key, Id(2), Id(2), 0, 5));
        HostCheck.True(queue.IsFull);
        DeadlineQueue.DrainResult result = owner.Drain(ref queue, 5, 3);
        HostCheck.True(result.Succeeded && result.Count == 3);
        HostCheck.True(new uint[] { 3, 1, 2 }.Select(i => Id(i).Value).SequenceEqual(new ulong[]
            { owner.Output[0].Identity.Value, owner.Output[1].Identity.Value, owner.Output[2].Identity.Value }));
        Add(ref queue, 1, 10, now: 5); Add(ref queue, 2, 20, now: 5); Add(ref queue, 3, 30, now: 5);
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(2)));
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(1)));
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(3)));
        HostCheck.True(queue.IsEmpty);
    }

    [HostTest("m09.003 repeated capacity drain refill rearm and reuse agree with independently sorted expectations", HostLane.Fast, 3)]
    private static void Repeated()
    {
        foreach (uint capacity in new uint[] { 1, 3, 17, 257 })
        {
            using Owner owner = new(); DeadlineQueue queue = owner.Queue(capacity);
            for (uint generation = 1; generation <= 10; generation++)
            {
                ulong now = generation * 100UL;
                for (uint slot = 1; slot <= capacity; slot++) Add(ref queue, slot, now + slot % 3, generation, now);
                uint[] expected = Enumerable.Range(1, (int)capacity).Select(i => (uint)i).OrderBy(i => i % 3).ToArray();
                DeadlineQueue.DrainResult result = owner.Drain(ref queue, now + 2, capacity);
                HostCheck.True(result.Succeeded && result.Count == capacity && queue.IsEmpty);
                for (int i = 0; i < expected.Length; i++) HostCheck.Equal(Id(expected[i], generation).Value, owner.Output[i].Identity.Value);
            }
        }
    }

    [HostTest("m09.003 invalid duplicate stale full and missing rearm fail without changing entries or time", HostLane.Fast, 4)]
    private static void IdentityAdmission()
    {
        using Owner owner = new(); DeadlineQueue queue = owner.Queue(1);
        foreach (OSHostHandle invalid in new OSHostHandle[] { default, new(1), new(1UL << 32) })
        {
            byte[] state = State(ref queue), buffer = owner.Snapshot();
            HostCheck.Equal(DeadlineQueue.Status.InvalidIdentity, queue.Schedule(Key, invalid, invalid, 0, 1));
            HostCheck.Equal(DeadlineQueue.Status.InvalidIdentity, queue.Cancel(Key, invalid)); Same(state, buffer, ref queue, owner);
        }
        Add(ref queue, 1, 5);
        byte[] before = State(ref queue), bytes = owner.Snapshot();
        HostCheck.Equal(DeadlineQueue.Status.Duplicate, queue.Schedule(Key, Id(1), Id(1), 1, 1));
        HostCheck.Equal(DeadlineQueue.Status.Full, queue.Schedule(Key, Id(2), Id(2), 1, 1));
        HostCheck.Equal(DeadlineQueue.Status.StaleIdentity, queue.Schedule(Key, Id(1), Id(1, 2), 1, 1));
        HostCheck.Equal(DeadlineQueue.Status.StaleIdentity, queue.Schedule(Key, Id(1, 2), Id(1, 2), 1, 1));
        HostCheck.Equal(DeadlineQueue.Status.StaleIdentity, queue.Rearm(Key, Id(1), Id(1, 2), 1, 1));
        HostCheck.Equal(DeadlineQueue.Status.NotFound, queue.Rearm(Key, Id(2), Id(2), 1, 1));
        HostCheck.Equal(DeadlineQueue.Status.NotFound, queue.Cancel(Key, Id(1, 2))); Same(before, bytes, ref queue, owner);
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(1)));
        Add(ref queue, 1, 6, 2); HostCheck.Equal(DeadlineQueue.Status.NotFound, queue.Cancel(Key, Id(1)));
        HostCheck.Equal(Id(1, 2).Value, queue.Peek(Key).Value.Identity.Value);
    }

    [HostTest("m09.003 nonwrapping epoch and deadline overflow never expire early or mutate on failure", HostLane.Fast, 5)]
    private static void Boundaries()
    {
        using Owner owner = new(); DeadlineQueue queue = owner.Queue(2, ulong.MaxValue - 4);
        Add(ref queue, 1, ulong.MaxValue, now: ulong.MaxValue - 4);
        byte[] state = State(ref queue), buffer = owner.Snapshot();
        HostCheck.Equal(DeadlineQueue.Status.DeadlineOverflow, queue.Schedule(Key, Id(2), Id(2), ulong.MaxValue - 4, 5));
        HostCheck.Equal(DeadlineQueue.Status.DeadlineOverflow, queue.Rearm(Key, Id(1), Id(1), ulong.MaxValue - 4, 5));
        HostCheck.Equal(DeadlineQueue.Status.ClockRegression, queue.Schedule(Key, Id(2), Id(2), ulong.MaxValue - 5, 0));
        Same(state, buffer, ref queue, owner);
        HostCheck.True(owner.Drain(ref queue, ulong.MaxValue - 1, 1).Count == 0 && queue.HasPending);
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(1)));
        Add(ref queue, 1, ulong.MaxValue, 2, ulong.MaxValue);
        DeadlineQueue.DrainResult result = owner.Drain(ref queue, ulong.MaxValue, 1);
        HostCheck.True(result.Succeeded && result.Count == 1); HostCheck.Equal(ulong.MaxValue, owner.Output[0].Deadline);
        state = State(ref queue); buffer = owner.Snapshot();
        HostCheck.Equal(DeadlineQueue.Status.ClockRegression, owner.Drain(ref queue, 0, 1).Code); Same(state, buffer, ref queue, owner);
    }

    [HostTest("m09.003 malformed and aliased drain buffers preserve complete queue output and observed clock", HostLane.Fast, 6)]
    private static void OutputAdmission()
    {
        using Owner owner = new(); DeadlineQueue queue = owner.Queue(2); Add(ref queue, 1, 1);
        DeadlineQueue* metadata = &queue;
        foreach (var bad in new (ulong Address, uint Bytes, uint Limit, DeadlineQueue.Status Code)[]
        {
            ((ulong)owner.Output, 16, 0, DeadlineQueue.Status.InvalidLimit),
            (0, 16, 1, DeadlineQueue.Status.InvalidBuffer),
            ((ulong)owner.Output + 1, 16, 1, DeadlineQueue.Status.InvalidBuffer),
            ((ulong)owner.Output, 15, 1, DeadlineQueue.Status.BufferTooSmall),
            ((ulong)owner.Output, 16, uint.MaxValue, DeadlineQueue.Status.BufferTooSmall),
            (ulong.MaxValue - 7, 16, 1, DeadlineQueue.Status.InvalidBuffer),
            (0x0000800000000000, 16, 1, DeadlineQueue.Status.InvalidBuffer),
            ((ulong)owner.Entries, 16, 1, DeadlineQueue.Status.InvalidBuffer),
            ((ulong)(owner.Entries + 1), 16, 1, DeadlineQueue.Status.InvalidBuffer),
            ((ulong)metadata, (uint)sizeof(DeadlineQueue), 1, DeadlineQueue.Status.InvalidBuffer)
        })
        {
            byte[] state = State(ref queue), buffer = owner.Snapshot();
            DeadlineQueue.DrainResult result = queue.Drain(Key, 1, (DeadlineQueue.Entry*)bad.Address, bad.Bytes, bad.Limit);
            HostCheck.Equal(bad.Code, result.Code); HostCheck.True(!result.Succeeded && result.Count == 0);
            Same(state, buffer, ref queue, owner);
        }
        DeadlineQueue.DrainResult adjacent = queue.Drain(Key, 1, owner.Entries + 2, 16, 1);
        HostCheck.True(adjacent.Succeeded && adjacent.Count == 1);
        HostCheck.Equal(Id(1).Value, owner.Entries[2].Identity.Value);
    }

    [HostTest("m09.003 serialized cancellation at expiration and stalled ticks retain pending truth", HostLane.Fast, 7)]
    private static void TickAndCancel()
    {
        using Owner owner = new(); DeadlineQueue queue = owner.Queue(2); Add(ref queue, 1, 10);
        for (int i = 0; i < 20; i++)
        {
            DeadlineQueue.DrainResult pending = owner.Drain(ref queue, 5, 1);
            HostCheck.True(pending.Succeeded && pending.Count == 0 && queue.HasPending && !queue.IsEmpty);
            HostCheck.Equal(10UL, queue.Peek(Key).Value.Deadline);
        }
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Rearm(Key, Id(1), Id(1), 5, 0));
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(1)));
        HostCheck.True(owner.Drain(ref queue, 5, 1).Count == 0);
        Add(ref queue, 1, 5, 2, 5);
        HostCheck.True(owner.Drain(ref queue, 5, 1).Count == 1);
        HostCheck.Equal(DeadlineQueue.Status.NotFound, queue.Cancel(Key, Id(1, 2)));
    }

    [HostTest("m09.003 wrong owner and premature release preserve state and value results", HostLane.Fast, 8)]
    private static void Ownership()
    {
        using Owner owner = new(); DeadlineQueue queue = owner.Queue(1); Add(ref queue, 1, 1);
        byte[] state = State(ref queue), buffer = owner.Snapshot();
        foreach (ulong wrong in new ulong[] { 0, Key + 1 })
        {
            HostCheck.Equal(DeadlineQueue.Status.WrongOwner, queue.Schedule(wrong, Id(2), Id(2), 2, 0));
            HostCheck.Equal(DeadlineQueue.Status.WrongOwner, queue.Rearm(wrong, Id(1), Id(1), 2, 0));
            HostCheck.Equal(DeadlineQueue.Status.WrongOwner, queue.Drain(wrong, 2, (DeadlineQueue.Entry*)1, 0, 0).Code);
            HostCheck.Equal(DeadlineQueue.Status.WrongOwner, queue.Peek(wrong).Code);
            HostCheck.Equal(DeadlineQueue.Status.WrongOwner, queue.Cancel(wrong, Id(1)));
            HostCheck.Equal(DeadlineQueue.Status.WrongOwner, queue.Release(wrong)); Same(state, buffer, ref queue, owner);
        }
        HostCheck.Equal(DeadlineQueue.Status.NotEmpty, queue.Release(Key)); Same(state, buffer, ref queue, owner);
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Cancel(Key, Id(1)));
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Release(Key)); HostCheck.True(!queue.IsInitialized && !queue.IsEmpty);
        HostCheck.Equal(DeadlineQueue.Status.Ok, queue.Initialize(Key + 1, owner.Entries, 16, 1));
        HostCheck.Equal(DeadlineQueue.Status.WrongOwner, queue.Peek(Key).Code);
    }

    [HostTest("m09.003 warmed deadline operations allocate no managed storage", HostLane.Fast, 9)]
    private static void NoAllocation()
    {
        using Owner owner = new(); DeadlineQueue queue = owner.Queue(2);
        for (ulong now = 0; now < 500; now++)
        {
            queue.Schedule(Key, Id(1), Id(1), now, 0); owner.Drain(ref queue, now, 1);
            queue.Schedule(Key, Id(2), Id(2), now, 2); queue.Rearm(Key, Id(2), Id(2), now, 1); queue.Cancel(Key, Id(2));
        }
        long before = GC.GetAllocatedBytesForCurrentThread(); bool passed = true;
        for (ulong now = 500; now < 1500; now++)
        {
            passed &= queue.Schedule(Key, Id(1), Id(1), now, 0) == DeadlineQueue.Status.Ok;
            passed &= queue.Peek(Key).Succeeded;
            passed &= owner.Drain(ref queue, now, 1).Succeeded;
            passed &= queue.Schedule(Key, Id(2), Id(2), now, 2) == DeadlineQueue.Status.Ok;
            passed &= queue.Rearm(Key, Id(2), Id(2), now, 1) == DeadlineQueue.Status.Ok;
            passed &= queue.Cancel(Key, Id(2)) == DeadlineQueue.Status.Ok;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(passed); HostCheck.Equal(0L, allocated);
    }
}
