using OS.Pal;

namespace OS.Kernel.Threading;

/// <summary>
/// Fixed-capacity FIFO of generation-tagged thread identities in borrowed storage.
/// One canonical queue value and its live buffer belong to one external owner.
/// That owner serializes each entire operation, including lifetime/context lookup,
/// against interrupts and other accesses. The owner key detects accidental misuse;
/// it is not a lock or security capability. Copying an initialized queue is unsupported.
/// No allocation, callback, hardware instruction or runnable-thread execution occurs.
/// </summary>
public unsafe struct ReadyQueue
{
    public enum Status : byte
    {
        Ok, Uninitialized, AlreadyInitialized, InvalidOwner, WrongOwner,
        InvalidBuffer, InvalidCapacity, BufferTooSmall, InvalidIdentity,
        StaleIdentity, NotRunnable, ContextNotReady, Duplicate, Empty, Full, NotFound, NotEmpty
    }

    /// <summary>Read/removal result by value; failure has no identity. A default
    /// result is not success evidence. No output reference can alias queue storage.</summary>
    public readonly struct Result
    {
        public readonly Status Code;
        public readonly OSHostHandle Identity;
        public bool Succeeded => Code == Status.Ok && Identity.IsValid;
        internal Result(Status code, OSHostHandle identity = default) { Code = code; Identity = identity; }
    }

    private OSHostHandle* _entries;
    private ulong _owner;
    private uint _capacity, _head, _count;
    private bool _initialized;

    public readonly bool IsInitialized => _initialized;
    public readonly uint Capacity => _capacity;
    public readonly uint Count => _count;
    public readonly bool IsEmpty => _initialized && _count == 0;
    public readonly bool IsFull => _initialized && _count == _capacity;

    /// <summary>
    /// Borrow an eight-byte-aligned, mapped handle buffer without clearing unused
    /// bytes. Capacity must be positive and fit within its declared byte length.
    /// The owner keeps buffer and queue metadata live until Release; overlap
    /// between their declared ranges is rejected.
    /// Null, wrapping and noncanonical ranges reject before access. Failure changes
    /// neither queue nor buffer; an initialized queue cannot be rebound.
    /// </summary>
    public Status Initialize(ulong owner, OSHostHandle* entries, uint storageBytes, uint capacity)
    {
        if (_initialized) return Status.AlreadyInitialized;
        if (owner == 0) return Status.InvalidOwner;
        if (capacity == 0) return Status.InvalidCapacity;
        ulong address = (ulong)entries;
        if (sizeof(nuint) != 8 || address == 0 || (address & 7) != 0) return Status.InvalidBuffer;
        if ((ulong)capacity * (uint)sizeof(OSHostHandle) > storageBytes) return Status.BufferTooSmall;
        if (address > ulong.MaxValue - (storageBytes - 1UL) ||
            !Canonical(address) || !Canonical(address + storageBytes - 1)) return Status.InvalidBuffer;
        fixed (ReadyQueue* self = &this)
            if (Overlap(address, storageBytes, (ulong)self, (uint)sizeof(ReadyQueue))) return Status.InvalidBuffer;
        _entries = entries; _owner = owner; _capacity = capacity;
        _head = 0; _count = 0; _initialized = true;
        return Status.Ok;
    }

    /// <summary>
    /// Admit only a currently live, runnable identity with a complete structurally
    /// validated context. The external lifetime owner supplies currentIdentity and
    /// trusted MXCSR mask independently, and pins identity/context/runnable state
    /// until removal. Identity uses OSHostThreadTable's generation-high32 and
    /// slot-plus-one-low32 shape. The queue does not issue or authenticate handles.
    /// Context overlap with queue metadata or any queue slot rejects. Duplicate
    /// identity or slot reuse while an older generation is queued rejects.
    /// Every fallible check precedes the entry/count update; failure is nonmutating.
    /// Whole-operation external serialization is required, including publication.
    /// </summary>
    public Status Enqueue(ulong owner, OSHostHandle identity, OSHostHandle currentIdentity,
        bool runnable, X64ThreadContext* context, uint contextBytes, uint observedMxcsrMask)
    {
        Status access = Access(owner);
        if (access != Status.Ok) return access;
        if (!Identity(identity) || !Identity(currentIdentity)) return Status.InvalidIdentity;
        if (identity.Value != currentIdentity.Value) return Status.StaleIdentity;
        if (!runnable) return Status.NotRunnable;
        if (X64ThreadContext.Validate(context, contextBytes, observedMxcsrMask) != X64ThreadContext.Status.Ok)
            return Status.ContextNotReady;
        fixed (ReadyQueue* self = &this)
            if (Overlap((ulong)context, contextBytes, (ulong)self, (uint)sizeof(ReadyQueue)) ||
                Overlap((ulong)context, contextBytes, (ulong)_entries, (ulong)_capacity * (uint)sizeof(OSHostHandle)))
                return Status.InvalidBuffer;
        for (uint i = 0; i < _count; i++)
        {
            ulong queued = _entries[Index(i)].Value;
            if (queued == identity.Value) return Status.Duplicate;
            if ((uint)queued == (uint)identity.Value) return Status.StaleIdentity;
        }
        if (_count == _capacity) return Status.Full;
        _entries[Index(_count)] = identity;
        _count++;
        return Status.Ok;
    }

    /// <summary>
    /// Inspect the FIFO head identity for an external lifetime lookup, without
    /// granting execution or liveness authority. Empty/failure returns no identity
    /// and leaves storage unchanged. The owner still serializes this read.
    /// </summary>
    public readonly Result Peek(ulong owner)
    {
        Status access = Access(owner);
        if (access != Status.Ok) return new(access);
        if (_count == 0) return new(Status.Empty);
        return new(Status.Ok, _entries[_head]);
    }

    /// <summary>
    /// Remove the head only after its identity matches the independently supplied
    /// live identity. Stale admission fails without consuming the head. Success
    /// clears the released entry; failure returns no identity. The lifetime owner
    /// decides what runs and when its identity/context may be reclaimed.
    /// </summary>
    public Result Dequeue(ulong owner, OSHostHandle currentIdentity)
    {
        Status access = Access(owner);
        if (access != Status.Ok) return new(access);
        if (!Identity(currentIdentity)) return new(Status.InvalidIdentity);
        if (_count == 0) return new(Status.Empty);
        OSHostHandle first = _entries[_head];
        if (first.Value != currentIdentity.Value) return new(Status.StaleIdentity);
        _entries[_head] = default;
        _head = _head + 1 == _capacity ? 0 : _head + 1;
        _count--;
        if (_count == 0) _head = 0;
        return new(Status.Ok, first);
    }

    /// <summary>
    /// Cancel one exact queued identity, retaining FIFO order and clearing its last
    /// occupied slot. The owner may evict an old generation after failed liveness
    /// admission; a different generation never cancels a reused slot. This does
    /// not terminate a thread or reclaim its resources. Rejection is nonmutating.
    /// </summary>
    public Status Cancel(ulong owner, OSHostHandle identity)
    {
        Status access = Access(owner);
        if (access != Status.Ok) return access;
        if (!Identity(identity)) return Status.InvalidIdentity;
        uint found = 0;
        while (found < _count && _entries[Index(found)].Value != identity.Value) found++;
        if (found == _count) return Status.NotFound;
        for (uint i = found; i + 1 < _count; i++) _entries[Index(i)] = _entries[Index(i + 1)];
        _entries[Index(_count - 1)] = default;
        _count--;
        if (_count == 0) _head = 0;
        return Status.Ok;
    }

    /// <summary>
    /// End the borrow only when empty. Success clears queue metadata, allowing
    /// reinitialization; the buffer remains caller-owned. Wrong owner/nonempty
    /// rejection preserves all state and pins. No allocation is freed here.
    /// </summary>
    public Status Release(ulong owner)
    {
        Status access = Access(owner);
        if (access != Status.Ok) return access;
        if (_count != 0) return Status.NotEmpty;
        this = default;
        return Status.Ok;
    }

    private readonly Status Access(ulong owner) => !_initialized ? Status.Uninitialized :
        owner == 0 || owner != _owner ? Status.WrongOwner : Status.Ok;
    private readonly uint Index(uint logical) => (uint)(((ulong)_head + logical) % _capacity);
    private static bool Identity(OSHostHandle value) => (uint)value.Value != 0 && (uint)(value.Value >> 32) != 0;
    private static bool Canonical(ulong value) => value >> 48 == ((value & (1UL << 47)) == 0 ? 0UL : 0xffffUL);
    private static bool Overlap(ulong first, ulong firstBytes, ulong second, ulong secondBytes) =>
        first <= second + secondBytes - 1 && second <= first + firstBytes - 1;
}
