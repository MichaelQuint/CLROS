using OS.Pal;

namespace OS.Kernel.Threading;
/// <summary>
/// Bounded deadline entries over live caller-owned storage and injected ticks.
/// One canonical owner serializes complete operations, clock/lifetime lookup and
/// wake publication against all accesses including ticks. Owner keys detect
/// accidental misuse, not concurrency or security. Copying a bound queue is
/// unsupported. No allocation, callbacks, hardware timer, sleeping or wake occurs.
/// </summary>

public unsafe partial struct DeadlineQueue
{
    private Entry* _entries;
    private ulong _owner, _now;
    private uint _capacity, _count;
    private bool _initialized;
    public readonly bool IsInitialized => _initialized;
    public readonly bool IsEmpty => _initialized && _count == 0;
    public readonly bool HasPending => _initialized && _count != 0;
    public readonly bool IsFull => _initialized && _count == _capacity;
    public readonly uint Count => _count;
    public readonly uint Capacity => _capacity;
    public readonly ulong ObservedTicks => _now;

    /// <summary>
    /// Borrow a mapped eight-byte-aligned entry buffer with positive fixed capacity.
    /// Buffer/metadata overlap, malformed range and insufficient bytes reject
    /// without writes. Unused slots are not read/cleared on initialization. Keep
    /// storage live until empty Release. Ticks belong to one nonwrapping ulong epoch.
    /// </summary>

    public Status Initialize(ulong owner, Entry* entries, uint storageBytes, uint capacity, ulong now = 0)
    {
        if (_initialized)
            return Status.AlreadyInitialized;
        if (owner == 0)
            return Status.InvalidOwner;
        if (capacity == 0)
            return Status.InvalidCapacity;
        Status range = Range(entries, storageBytes, (ulong)capacity * (uint)sizeof(Entry));
        if (range != Status.Ok)
            return range;
        fixed (DeadlineQueue* self = &this)
            if (Overlap((ulong)entries, storageBytes, (ulong)self, (uint)sizeof(DeadlineQueue)))
                return Status.InvalidBuffer;
        _entries = entries;
        _owner = owner;
        _capacity = capacity;
        _count = 0;
        _now = now;
        _initialized = true;
        return Status.Ok;
    }

    /// <summary>
    /// Insert a currently live identity at checked now+delay. Current identity is
    /// independently supplied by the lifetime owner using the existing upper32
    /// generation/lower32 slot-plus-one shape. Duplicate or pending slot reuse
    /// rejects. Equal deadlines retain insertion order; delay0 is due immediately.
    /// Regressing ticks/wrap and addition overflow reject without advancing time.
    /// This does not mark a thread waiting or obtain a hardware clock snapshot.
    /// </summary>

    public Status Schedule(ulong owner, OSHostHandle identity, OSHostHandle currentIdentity, ulong now, ulong delay) => Set(owner, identity, currentIdentity, now, delay, false);
    /// <summary>
    /// Replace one exact live pending deadline after all admission/arithmetic checks.
    /// A rearm is a new insertion for equal-deadline ordering and works when full.
    /// Failure preserves entries/count/time. The owner serializes cancellation,
    /// expiration and rearm; this method dispatches no wake.
    /// </summary>

    public Status Rearm(ulong owner, OSHostHandle identity, OSHostHandle currentIdentity, ulong now, ulong delay) => Set(owner, identity, currentIdentity, now, delay, true);
    /// <summary>Inspect the earliest stored entry without a clock read, mutation,
    /// liveness admission or wake authority. A stalled tick cannot hide pending entries.</summary>

    public readonly Result Peek(ulong owner)
    {
        Status access = Access(owner);
        if (access != Status.Ok)
            return new(access);
        return _count == 0 ? new(Status.Empty) : new(Status.Ok, _entries[0]);
    }

    /// <summary>
    /// Drain at most limit entries whose deadlines are <= injected now, in stable
    /// order. Destination is live writable caller storage, disjoint from all queue
    /// slots/metadata, with declared bytes sufficient for limit entries. All range,
    /// owner and monotonic checks precede any output/queue write. Failure preserves
    /// both buffers and observed ticks. Success clears released slots and advances
    /// observed ticks even when Count0; HasPending distinguishes pending from empty.
    /// Returned identities need external lifetime/context admission before wake.
    /// </summary>

    public DrainResult Drain(ulong owner, ulong now, Entry* destination, uint destinationBytes, uint limit)
    {
        Status access = Access(owner);
        if (access != Status.Ok)
            return new(access);
        if (now < _now)
            return new(Status.ClockRegression);
        if (limit == 0)
            return new(Status.InvalidLimit);
        Status range = Range(destination, destinationBytes, (ulong)limit * (uint)sizeof(Entry));
        if (range != Status.Ok)
            return new(range);
        fixed (DeadlineQueue* self = &this)
            if (Overlap((ulong)destination, destinationBytes, (ulong)self, (uint)sizeof(DeadlineQueue)) || Overlap((ulong)destination, destinationBytes, (ulong)_entries, (ulong)_capacity * (uint)sizeof(Entry)))
                return new(Status.InvalidBuffer);
        uint take = 0;
        while (take < _count && take < limit && _entries[take].Deadline <= now)
            take++;
        for (uint i = 0; i < take; i++)
            destination[i] = _entries[i];
        for (uint i = take; i < _count; i++)
            _entries[i - take] = _entries[i];
        for (uint i = _count - take; i < _count; i++)
            _entries[i] = default;
        _count -= take;
        _now = now;
        return new(Status.Ok, take);
    }

    /// <summary>Cancel one exact generation, including eviction of a stale stored
    /// value. Retains ordering and clears the released slot; a different generation
    /// never cancels reuse. No lifetime reclamation, clock advance or wake occurs.</summary>

    public Status Cancel(ulong owner, OSHostHandle identity)
    {
        Status access = Access(owner);
        if (access != Status.Ok)
            return access;
        if (!Identity(identity))
            return Status.InvalidIdentity;
        uint found = 0;
        while (found < _count && _entries[found].Identity.Value != identity.Value)
            found++;
        if (found == _count)
            return Status.NotFound;
        Remove(found);
        return Status.Ok;
    }

    /// <summary>End an empty queue's borrow without freeing storage. Nonempty or
    /// wrong-owner failure is nonmutating. A new epoch requires a fresh empty borrow.</summary>

    public Status Release(ulong owner)
    {
        Status access = Access(owner);
        if (access != Status.Ok)
            return access;
        if (_count != 0)
            return Status.NotEmpty;
        this = default;
        return Status.Ok;
    }

    private Status Set(ulong owner, OSHostHandle identity, OSHostHandle currentIdentity, ulong now, ulong delay, bool rearm)
    {
        Status access = Access(owner);
        if (access != Status.Ok)
            return access;
        if (!Identity(identity) || !Identity(currentIdentity))
            return Status.InvalidIdentity;
        if (identity.Value != currentIdentity.Value)
            return Status.StaleIdentity;
        if (now < _now)
            return Status.ClockRegression;
        if (delay > ulong.MaxValue - now)
            return Status.DeadlineOverflow;
        uint found = _count;
        for (uint i = 0; i < _count; i++)
        {
            if (_entries[i].Identity.Value == identity.Value)
                found = i;
            else if ((uint)_entries[i].Identity.Value == (uint)identity.Value)
                return Status.StaleIdentity;
        }

        if (!rearm && found != _count)
            return Status.Duplicate;
        if (rearm && found == _count)
            return Status.NotFound;
        if (!rearm && _count == _capacity)
            return Status.Full;
        ulong deadline = now + delay;
        if (rearm)
            Remove(found);
        uint place = 0;
        while (place < _count && _entries[place].Deadline <= deadline)
            place++;
        for (uint i = _count; i > place; i--)
            _entries[i] = _entries[i - 1];
        _entries[place] = new(identity, deadline);
        _count++;
        _now = now;
        return Status.Ok;
    }

    private void Remove(uint index)
    {
        for (uint i = index + 1; i < _count; i++)
            _entries[i - 1] = _entries[i];
        _entries[--_count] = default;
    }

    private readonly Status Access(ulong owner) => !_initialized ? Status.Uninitialized : owner == 0 || owner != _owner ? Status.WrongOwner : Status.Ok;
    private static bool Identity(OSHostHandle value) => (uint)value.Value != 0 && (uint)(value.Value >> 32) != 0;
    private static Status Range(void* pointer, uint bytes, ulong required)
    {
        ulong address = (ulong)pointer;
        if (sizeof(nuint) != 8 || address == 0 || (address & 7) != 0)
            return Status.InvalidBuffer;
        if (required > bytes)
            return Status.BufferTooSmall;
        if (address > ulong.MaxValue - (bytes - 1UL) || !Canonical(address) || !Canonical(address + bytes - 1))
            return Status.InvalidBuffer;
        return Status.Ok;
    }

    private static bool Canonical(ulong value) => value >> 48 == ((value & (1UL << 47)) == 0 ? 0UL : 0xffffUL);
    private static bool Overlap(ulong a, ulong aBytes, ulong b, ulong bBytes) => a <= b + bBytes - 1 && b <= a + aBytes - 1;
}
