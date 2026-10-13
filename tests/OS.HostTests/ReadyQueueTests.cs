using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using OS.Kernel.Threading;
using OS.Pal;

internal static unsafe class ReadyQueueTests
{
    private const ulong Key = 0x514f574e4552;
    private static OSHostHandle Id(uint slot, uint generation = 1) => new(((ulong)generation << 32) | slot);
    private sealed class Owner : IDisposable
    {
        public readonly byte* Buffer = (byte*)NativeMemory.AlignedAlloc(16384, 16);
        public OSHostHandle* Entries => (OSHostHandle*)(Buffer + 16);
        public X64ThreadContext* Context => (X64ThreadContext*)(Buffer + 8192);
        public Owner()
        {
            new Span<byte>(Buffer, 16384).Fill(0xa5);
            HostCheck.Equal(X64ThreadContext.Status.Ok,
                X64ThreadContext.Initialize(Context, 688, 0x400100, 0x70000108, 0xffbf));
        }
        public void Dispose() => NativeMemory.AlignedFree(Buffer);
        public byte[] Snapshot() => new ReadOnlySpan<byte>(Buffer, 16384).ToArray();
        public ReadyQueue Queue(uint capacity)
        {
            ReadyQueue queue = default;
            HostCheck.Equal(ReadyQueue.Status.Ok, queue.Initialize(Key, Entries, capacity * 8, capacity));
            return queue; // Transfer this sole test owner before any operation.
        }
        public ReadyQueue.Status Add(ref ReadyQueue queue, OSHostHandle id) =>
            queue.Enqueue(Key, id, id, true, Context, 688, 0xffbf);
    }
    private static byte[] State(ref ReadyQueue queue)
    {
        fixed (ReadyQueue* value = &queue) return new ReadOnlySpan<byte>(value, sizeof(ReadyQueue)).ToArray();
    }
    private static void Same(byte[] state, byte[] buffer, ref ReadyQueue queue, Owner owner)
    {
        HostCheck.True(state.SequenceEqual(State(ref queue)));
        HostCheck.True(buffer.SequenceEqual(owner.Snapshot()));
    }
    private static void Take(ref ReadyQueue queue, OSHostHandle expected)
    {
        ReadyQueue.Result first = queue.Peek(Key);
        HostCheck.True(first.Succeeded); HostCheck.Equal(expected.Value, first.Identity.Value);
        ReadyQueue.Result value = queue.Dequeue(Key, expected);
        HostCheck.True(value.Succeeded); HostCheck.Equal(expected.Value, value.Identity.Value);
    }

    [HostTest("m09.002", HostLane.Fast, 0)]
    private static void StatesAndRanges()
    {
        HostCheck.True(!RuntimeHelpers.IsReferenceOrContainsReferences<ReadyQueue>());
        using Owner owner = new();
        ReadyQueue queue = default;
        HostCheck.True(!queue.IsInitialized && !queue.IsEmpty && !queue.IsFull && queue.Count == 0 && queue.Capacity == 0);
        HostCheck.True(!default(ReadyQueue.Result).Succeeded);
        HostCheck.Equal(ReadyQueue.Status.Uninitialized, queue.Peek(Key).Code);
        HostCheck.Equal(0UL, queue.Peek(Key).Identity.Value);
        foreach (var bad in new (ulong Owner, ulong Address, uint Bytes, uint Capacity, ReadyQueue.Status Status)[]
        {
            (0, (ulong)owner.Entries, 8, 1, ReadyQueue.Status.InvalidOwner),
            (Key, (ulong)owner.Entries, 8, 0, ReadyQueue.Status.InvalidCapacity),
            (Key, 0, 8, 1, ReadyQueue.Status.InvalidBuffer),
            (Key, (ulong)owner.Entries + 1, 8, 1, ReadyQueue.Status.InvalidBuffer),
            (Key, (ulong)owner.Entries, 7, 1, ReadyQueue.Status.BufferTooSmall),
            (Key, (ulong)owner.Entries, uint.MaxValue, uint.MaxValue, ReadyQueue.Status.BufferTooSmall),
            (Key, ulong.MaxValue - 7, 16, 1, ReadyQueue.Status.InvalidBuffer),
            (Key, 0x0000800000000000, 8, 1, ReadyQueue.Status.InvalidBuffer),
            (Key, 0x00007ffffffffff8, 16, 1, ReadyQueue.Status.InvalidBuffer)
        })
        {
            byte[] state = State(ref queue), buffer = owner.Snapshot();
            HostCheck.Equal(bad.Status, queue.Initialize(bad.Owner, (OSHostHandle*)bad.Address, bad.Bytes, bad.Capacity));
            Same(state, buffer, ref queue, owner);
        }
        HostCheck.Equal(ReadyQueue.Status.Ok, queue.Initialize(Key, owner.Entries, 8, 1));
        HostCheck.True(queue.IsInitialized && queue.IsEmpty && !queue.IsFull && queue.Capacity == 1);
        byte[] initialized = State(ref queue), unchanged = owner.Snapshot();
        HostCheck.Equal(ReadyQueue.Status.AlreadyInitialized, queue.Initialize(9, null, 0, 0));
        Same(initialized, unchanged, ref queue, owner);
    }

    [HostTest("m09.002 FIFO full empty and wraparound preserve guards and clear released slots", HostLane.Fast, 1)]
    private static void Fifo()
    {
        using Owner owner = new(); ReadyQueue queue = owner.Queue(3);
        for (uint i = 1; i <= 3; i++) HostCheck.Equal(ReadyQueue.Status.Ok, owner.Add(ref queue, Id(i)));
        HostCheck.True(queue.IsFull && queue.Count == 3);
        byte[] state = State(ref queue), buffer = owner.Snapshot();
        HostCheck.Equal(ReadyQueue.Status.Full, owner.Add(ref queue, Id(4))); Same(state, buffer, ref queue, owner);
        Take(ref queue, Id(1)); HostCheck.Equal(0UL, owner.Entries[0].Value);
        HostCheck.Equal(ReadyQueue.Status.Ok, owner.Add(ref queue, Id(4)));
        Take(ref queue, Id(2)); Take(ref queue, Id(3)); Take(ref queue, Id(4));
        HostCheck.True(queue.IsEmpty && !queue.IsFull && queue.Count == 0);
        for (int i = 0; i < 3; i++) HostCheck.Equal(0UL, owner.Entries[i].Value);
        state = State(ref queue); buffer = owner.Snapshot();
        ReadyQueue.Result empty = queue.Dequeue(Key, Id(1));
        HostCheck.Equal(ReadyQueue.Status.Empty, empty.Code);
        HostCheck.Equal(0UL, empty.Identity.Value); HostCheck.Equal(ReadyQueue.Status.Empty, queue.Peek(Key).Code);
        Same(state, buffer, ref queue, owner);
        HostCheck.True(new ReadOnlySpan<byte>(owner.Buffer, 16).ToArray().All(b => b == 0xa5));
        HostCheck.True(new ReadOnlySpan<byte>(owner.Buffer + 40, 8152).ToArray().All(b => b == 0xa5));
    }

    [HostTest("m09.002 repeated capacity boundaries drain refill and generation reuse", HostLane.Fast, 2)]
    private static void Repeated()
    {
        foreach (uint capacity in new uint[] { 1, 2, 3, 17, 257 })
        {
            using Owner owner = new(); ReadyQueue queue = owner.Queue(capacity);
            for (uint generation = 1; generation <= 20; generation++)
            {
                for (uint slot = 1; slot <= capacity; slot++)
                    HostCheck.Equal(ReadyQueue.Status.Ok, owner.Add(ref queue, Id(slot, generation)));
                HostCheck.True(queue.IsFull);
                for (uint slot = 1; slot <= capacity; slot++) Take(ref queue, Id(slot, generation));
                HostCheck.True(queue.IsEmpty);
            }
        }
    }

    [HostTest("m09.002 invalid stale duplicate and reused identities fail without publication", HostLane.Fast, 3)]
    private static void IdentityAdmission()
    {
        using Owner owner = new(); ReadyQueue queue = owner.Queue(3);
        foreach (OSHostHandle invalid in new OSHostHandle[] { default, new(1), new(1UL << 32) })
        {
            byte[] state = State(ref queue), buffer = owner.Snapshot();
            HostCheck.Equal(ReadyQueue.Status.InvalidIdentity, owner.Add(ref queue, invalid));
            ReadyQueue.Result none = queue.Dequeue(Key, invalid);
            HostCheck.Equal(ReadyQueue.Status.InvalidIdentity, none.Code);
            HostCheck.Equal(0UL, none.Identity.Value); HostCheck.Equal(ReadyQueue.Status.InvalidIdentity, queue.Cancel(Key, invalid));
            Same(state, buffer, ref queue, owner);
        }
        byte[] before = State(ref queue), bytes = owner.Snapshot();
        HostCheck.Equal(ReadyQueue.Status.StaleIdentity, queue.Enqueue(Key, Id(1), Id(1, 2), true, owner.Context, 688, 0xffbf));
        Same(before, bytes, ref queue, owner);
        HostCheck.Equal(ReadyQueue.Status.Ok, owner.Add(ref queue, Id(1)));
        before = State(ref queue); bytes = owner.Snapshot();
        HostCheck.Equal(ReadyQueue.Status.Duplicate, owner.Add(ref queue, Id(1)));
        HostCheck.Equal(ReadyQueue.Status.StaleIdentity, owner.Add(ref queue, Id(1, 2)));
        ReadyQueue.Result stale = queue.Dequeue(Key, Id(1, 2));
        HostCheck.Equal(ReadyQueue.Status.StaleIdentity, stale.Code);
        HostCheck.Equal(0UL, stale.Identity.Value); Same(before, bytes, ref queue, owner);
        HostCheck.Equal(ReadyQueue.Status.Ok, queue.Cancel(Key, Id(1)));
        HostCheck.Equal(ReadyQueue.Status.Ok, owner.Add(ref queue, Id(1, 2)));
        HostCheck.Equal(ReadyQueue.Status.NotFound, queue.Cancel(Key, Id(1)));
        Take(ref queue, Id(1, 2));
    }

    [HostTest("m09.002 cancellation keeps FIFO across wrapped head middle tail and missing generations", HostLane.Fast, 4)]
    private static void Cancellation()
    {
        using Owner owner = new(); ReadyQueue queue = owner.Queue(5);
        for (uint i = 1; i <= 5; i++) HostCheck.Equal(ReadyQueue.Status.Ok, owner.Add(ref queue, Id(i)));
        Take(ref queue, Id(1)); Take(ref queue, Id(2));
        HostCheck.Equal(ReadyQueue.Status.Ok, owner.Add(ref queue, Id(6)));
        HostCheck.Equal(ReadyQueue.Status.Ok, owner.Add(ref queue, Id(7)));
        HostCheck.Equal(ReadyQueue.Status.Ok, queue.Cancel(Key, Id(5)));
        HostCheck.Equal(ReadyQueue.Status.Ok, queue.Cancel(Key, Id(3)));
        HostCheck.Equal(ReadyQueue.Status.Ok, queue.Cancel(Key, Id(7)));
        byte[] state = State(ref queue), buffer = owner.Snapshot();
        HostCheck.Equal(ReadyQueue.Status.NotFound, queue.Cancel(Key, Id(4, 2)));
        Same(state, buffer, ref queue, owner); Take(ref queue, Id(4)); Take(ref queue, Id(6));
        for (int i = 0; i < 5; i++) HostCheck.Equal(0UL, owner.Entries[i].Value);
    }

    [HostTest("m09.002 incomplete preparatory context never publishes and serialized observers see complete operations", HostLane.Fast, 5)]
    private static void Publication()
    {
        using Owner owner = new(); ReadyQueue queue = owner.Queue(2);
        byte[] complete = new ReadOnlySpan<byte>(owner.Context, 688).ToArray();
        new Span<byte>(owner.Context, 688).Clear();
        // Host requests at every eight-byte preparatory checkpoint are serviced
        // only outside the owner's whole-operation serialization window.
        for (int offset = 16; offset < 688; offset += 8)
        {
            complete.AsSpan(offset, Math.Min(8, 688 - offset)).CopyTo(new Span<byte>((byte*)owner.Context + offset, Math.Min(8, 688 - offset)));
            byte[] state = State(ref queue), buffer = owner.Snapshot();
            HostCheck.Equal(ReadyQueue.Status.ContextNotReady, owner.Add(ref queue, Id(1)));
            ReadyQueue.Result partial = queue.Peek(Key);
            HostCheck.Equal(ReadyQueue.Status.Empty, partial.Code);
            HostCheck.Equal(0UL, partial.Identity.Value); Same(state, buffer, ref queue, owner);
        }
        complete.AsSpan(0, 16).CopyTo(new Span<byte>(owner.Context, 16));
        byte[] before = State(ref queue), bytes = owner.Snapshot();
        HostCheck.Equal(ReadyQueue.Status.NotRunnable, queue.Enqueue(Key, Id(1), Id(1), false, owner.Context, 688, 0xffbf));
        Same(before, bytes, ref queue, owner);
        HostCheck.Equal(ReadyQueue.Status.Ok, owner.Add(ref queue, Id(1)));
        ReadyQueue.Result visible = queue.Peek(Key);
        HostCheck.True(visible.Succeeded); HostCheck.Equal(Id(1).Value, visible.Identity.Value);
        // Interrupted dequeue preparation with a stale lookup cannot consume.
        before = State(ref queue); bytes = owner.Snapshot();
        visible = queue.Dequeue(Key, Id(1, 2));
        HostCheck.Equal(ReadyQueue.Status.StaleIdentity, visible.Code);
        HostCheck.Equal(0UL, visible.Identity.Value); Same(before, bytes, ref queue, owner);
        Take(ref queue, Id(1));
    }

    [HostTest("m09.002 owner guard and context admission failures preserve entire queue and storage", HostLane.Fast, 6)]
    private static void OwnerAndContext()
    {
        using Owner owner = new(); ReadyQueue queue = owner.Queue(2);
        HostCheck.Equal(ReadyQueue.Status.Ok, owner.Add(ref queue, Id(1)));
        byte[] state = State(ref queue), buffer = owner.Snapshot();
        foreach (ulong wrong in new ulong[] { 0, Key + 1 })
        {
            HostCheck.Equal(ReadyQueue.Status.WrongOwner, queue.Enqueue(wrong, Id(2), Id(2), true, (X64ThreadContext*)1, 0, 0));
            ReadyQueue.Result output = queue.Peek(wrong);
            HostCheck.Equal(ReadyQueue.Status.WrongOwner, output.Code); HostCheck.Equal(0UL, output.Identity.Value);
            output = queue.Dequeue(wrong, Id(1));
            HostCheck.Equal(ReadyQueue.Status.WrongOwner, output.Code); HostCheck.Equal(0UL, output.Identity.Value);
            HostCheck.Equal(ReadyQueue.Status.WrongOwner, queue.Cancel(wrong, Id(1)));
            HostCheck.Equal(ReadyQueue.Status.WrongOwner, queue.Release(wrong)); Same(state, buffer, ref queue, owner);
        }
        HostCheck.Equal(ReadyQueue.Status.ContextNotReady, queue.Enqueue(Key, Id(2), Id(2), true, null, 688, 0xffbf));
        HostCheck.Equal(ReadyQueue.Status.ContextNotReady, queue.Enqueue(Key, Id(2), Id(2), true, owner.Context, 687, 0xffbf));
        HostCheck.Equal(ReadyQueue.Status.ContextNotReady, queue.Enqueue(Key, Id(2), Id(2), true, owner.Context, 688, 0xffff));
        Same(state, buffer, ref queue, owner);
        HostCheck.Equal(ReadyQueue.Status.NotEmpty, queue.Release(Key)); Same(state, buffer, ref queue, owner);
        Take(ref queue, Id(1)); HostCheck.Equal(ReadyQueue.Status.Ok, queue.Release(Key));
        HostCheck.True(!queue.IsInitialized && !queue.IsEmpty && !queue.IsFull && queue.Capacity == 0);
        HostCheck.Equal(ReadyQueue.Status.Ok, queue.Initialize(Key + 1, owner.Entries, 8, 1));
        HostCheck.Equal(ReadyQueue.Status.WrongOwner, queue.Peek(Key).Code);
    }

    [HostTest("m09.002 warmed queue operations allocate no managed storage", HostLane.Fast, 7)]
    private static void NoAllocation()
    {
        using Owner owner = new(); ReadyQueue queue = owner.Queue(2);
        for (int i = 0; i < 500; i++)
        {
            owner.Add(ref queue, Id(1)); queue.Peek(Key); queue.Dequeue(Key, Id(1));
            owner.Add(ref queue, Id(2)); queue.Cancel(Key, Id(2));
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        bool passed = true;
        for (int i = 0; i < 1000; i++)
        {
            passed &= owner.Add(ref queue, Id(1)) == ReadyQueue.Status.Ok;
            passed &= queue.Peek(Key).Succeeded;
            passed &= queue.Dequeue(Key, Id(1)).Succeeded;
            passed &= owner.Add(ref queue, Id(2)) == ReadyQueue.Status.Ok;
            passed &= queue.Cancel(Key, Id(2)) == ReadyQueue.Status.Ok;
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        HostCheck.True(passed); HostCheck.Equal(0L, allocated);
    }

    [HostTest("m09.002 context and metadata aliases reject before publication and value outputs preserve entries", HostLane.Fast, 8)]
    private static void Aliases()
    {
        using Owner owner = new(); ReadyQueue queue = default;
        byte[] state = State(ref queue), buffer = owner.Snapshot();
        ReadyQueue* metadata = &queue;
        HostCheck.Equal(ReadyQueue.Status.InvalidBuffer, queue.Initialize(Key, (OSHostHandle*)metadata, (uint)sizeof(ReadyQueue), 1));
        Same(state, buffer, ref queue, owner);
        foreach (uint offset in new uint[] { 0, 176, 680 })
        {
            queue = default;
            HostCheck.Equal(ReadyQueue.Status.Ok, queue.Initialize(Key, (OSHostHandle*)((byte*)owner.Context + offset), 8, 1));
            state = State(ref queue); buffer = owner.Snapshot();
            HostCheck.Equal(ReadyQueue.Status.InvalidBuffer, owner.Add(ref queue, Id(1)));
            Same(state, buffer, ref queue, owner);
        }
        queue = default;
        HostCheck.Equal(ReadyQueue.Status.Ok, queue.Initialize(Key, (OSHostHandle*)((byte*)owner.Context + 688), 8, 1));
        HostCheck.Equal(ReadyQueue.Status.Ok, owner.Add(ref queue, Id(1)));
        state = State(ref queue); buffer = owner.Snapshot();
        ReadyQueue.Result failed = queue.Peek(Key + 1);
        HostCheck.Equal(ReadyQueue.Status.WrongOwner, failed.Code);
        failed = queue.Dequeue(Key, Id(1, 2));
        HostCheck.Equal(ReadyQueue.Status.StaleIdentity, failed.Code);
        Same(state, buffer, ref queue, owner); Take(ref queue, Id(1));
        HostCheck.Equal(X64ThreadContext.Status.Ok, X64ThreadContext.Validate(owner.Context, 688, 0xffbf));
    }
}
