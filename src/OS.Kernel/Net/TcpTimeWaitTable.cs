namespace OS.Net;
/// <summary>Bounded TIME_WAIT metadata borrowed by one serialized kernel owner.
/// All storage is trusted, mapped, aligned and exclusive through cleanup; never
/// copy a live Owner. No packet buffers, callbacks, NIC or implicit expiry exist.
/// Out values are distinct caller locals outside retained storage. Times are
/// monotonic nanoseconds; successful mutations commit the clock observation.
/// Failures leave metadata unchanged. A fresh nonzero epoch is required when
/// reinitializing a retired owner address; historical epoch reuse is caller error.</summary>

public static unsafe partial class TcpTimeWaitTable
{
    public const uint MaximumEntries = 64;
    public const ulong MinimumDurationNanoseconds = 240_000_000_000;
    /// <summary>Borrow disjoint zeroed owner/entry storage without allocation.
    /// Epoch is a fresh owner-lifetime identity; now may be zero. Failure writes
    /// nothing. Backing remains mapped/exclusive until successful Retire.</summary>

    public static Status Initialize(Owner* owner, Entry* entries, uint capacity, ulong epoch, ulong now)
    {
        if (!Storage(owner, (ulong)sizeof(Owner)) || capacity is < 1 or > MaximumEntries || epoch == 0 || !Storage(entries, (ulong)capacity * (ulong)sizeof(Entry)) || Overlap(owner, (ulong)sizeof(Owner), entries, (ulong)capacity * (ulong)sizeof(Entry)) || !Zero(owner, (ulong)sizeof(Owner)) || !Zero(entries, (ulong)capacity * (ulong)sizeof(Entry)))
            return Status.InvalidStorage;
        owner->Self = (ulong)owner;
        owner->Epoch = epoch;
        owner->Entries = entries;
        owner->CapacityValue = capacity;
        owner->LastNow = now;
        return Status.Success;
    }

    /// <summary>Retain an already-started live TIME_WAIT lifetime unchanged.
    /// Duration is at least240s; deadline-duration must be a past/present start.
    /// Duplicate/full/expired admission does not expire or replace another tuple.</summary>

    public static Status Retain(Owner* owner, TcpConnectionKey key, uint localNext, uint remoteNext, ulong deadline, ulong duration, ulong now, out Handle handle)
    {
        handle = default;
        var status = Validate(owner);
        if (status != Status.Success)
            return status;
        if (now < owner->LastNow)
            return Status.InvalidClock;
        if (!KeyValid(key) || duration < MinimumDurationNanoseconds || deadline < duration || deadline - duration > now)
            return Status.InvalidRecord;
        if (deadline <= now)
            return Status.Expired;
        uint free = MaximumEntries;
        for (uint i = 0; i < owner->CapacityValue; i++)
        {
            if (owner->Entries[i].Generation == 0)
            {
                if (free == MaximumEntries)
                    free = i;
            }
            else if (owner->Entries[i].Value.Key.HasSameEndpoint(key))
                return Status.DuplicateEndpoint;
        }

        if (free == MaximumEntries)
            return Status.Full;
        if (owner->Issued == ~0UL)
            return Status.GenerationExhausted;
        ulong generation = owner->Issued + 1;
        owner->Entries[free].Value = new(key, localNext, remoteNext, deadline, deadline, duration);
        owner->Entries[free].Generation = generation;
        owner->Issued = generation;
        owner->Held++;
        owner->LastNow = now;
        handle = new(owner, (ushort)free, generation);
        return Status.Success;
    }

    /// <summary>Read retained metadata without observing time or expiring it.
    /// Success is not proof that the deadline remains in the future.</summary>

    public static Status Find(Owner* owner, TcpConnectionKey key, out Handle handle, out Record record)
    {
        handle = default;
        record = default;
        var status = Validate(owner);
        if (status != Status.Success)
            return status;
        if (!KeyValid(key))
            return Status.InvalidRecord;
        for (uint i = 0; i < owner->CapacityValue; i++)
            if (owner->Entries[i].Generation != 0 && owner->Entries[i].Value.Key.HasSameEndpoint(key))
            {
                record = owner->Entries[i].Value;
                handle = new(owner, (ushort)i, owner->Entries[i].Generation);
                return Status.Success;
            }

        return Status.NotFound;
    }

    /// <summary>Read only this owner's exact live handle; does not expire it.
    /// Retired/reused slots and other owner epochs cannot confer authority.</summary>

    public static Status Read(Owner* owner, Handle handle, out Record record)
    {
        record = default;
        var status = Validate(owner);
        if (status != Status.Success)
            return status;
        if (handle.Address != (ulong)owner || handle.Epoch != owner->Epoch || handle.Generation == 0 || handle.Index >= owner->CapacityValue || owner->Entries[handle.Index].Generation != handle.Generation)
            return Status.StaleHandle;
        record = owner->Entries[handle.Index].Value;
        return Status.Success;
    }

    /// <summary>Explicitly scrub records whose deadline is at/before now.
    /// Equality expires; regression changes nothing. Never wraps a deadline.</summary>

    public static Status Expire(Owner* owner, ulong now)
    {
        var status = Validate(owner);
        if (status != Status.Success)
            return status;
        if (now < owner->LastNow)
            return Status.InvalidClock;
        for (uint i = 0; i < owner->CapacityValue; i++)
            if (owner->Entries[i].Generation != 0 && now >= owner->Entries[i].Value.DeadlineNanoseconds)
            {
                Clear(owner->Entries + i, (ulong)sizeof(Entry));
                owner->Held--;
            }

        owner->LastNow = now;
        return Status.Success;
    }

    /// <summary>Restart a still-live record ONLY after the serialized caller has
    /// authenticated the exact duplicate FIN. This table does not parse packets
    /// or decide ACK policy. Overflow/expired/stale/regressed requests do nothing.</summary>

    public static Status Restart(Owner* owner, Handle handle, ulong now)
    {
        var status = Read(owner, handle, out var record);
        if (status != Status.Success)
            return status;
        if (now < owner->LastNow || now > ~0UL - record.DurationNanoseconds)
            return Status.InvalidClock;
        if (now >= record.DeadlineNanoseconds)
            return Status.Expired;
        owner->Entries[handle.Index].Value = new(record.Key, record.LocalNextSequenceNumber, record.RemoteNextSequenceNumber, record.OriginalDeadlineNanoseconds, now + record.DurationNanoseconds, record.DurationNanoseconds);
        owner->LastNow = now;
        return Status.Success;
    }

    /// <summary>Explicit abort-only scrub, not normal expiry or transport close.
    /// Keeps epoch/generation/clock history, so later reuse cannot revive handles.</summary>

    public static Status Discard(Owner* owner)
    {
        var status = Validate(owner);
        if (status != Status.Success)
            return status;
        Clear(owner->Entries, (ulong)owner->CapacityValue * (ulong)sizeof(Entry));
        owner->Held = 0;
        return Status.Success;
    }

    /// <summary>Release empty ownership only. All entry bytes are already zero;
    /// clear the owner and end its borrow. Reinitialization requires a fresh epoch.</summary>

    public static Status Retire(Owner* owner)
    {
        var status = Validate(owner);
        if (status != Status.Success)
            return status;
        if (owner->Held != 0)
            return Status.RecordsRemain;
        Clear(owner, (ulong)sizeof(Owner));
        return Status.Success;
    }

    private static Status Validate(Owner* owner)
    {
        if (!Storage(owner, (ulong)sizeof(Owner)) || owner->Self != (ulong)owner || owner->Epoch == 0 || owner->CapacityValue is < 1 or > MaximumEntries || owner->Held > owner->CapacityValue || !Storage(owner->Entries, (ulong)owner->CapacityValue * (ulong)sizeof(Entry)) || Overlap(owner, (ulong)sizeof(Owner), owner->Entries, (ulong)owner->CapacityValue * (ulong)sizeof(Entry)))
            return Status.InvalidState;
        uint held = 0;
        for (uint i = 0; i < owner->CapacityValue; i++)
        {
            var entry = owner->Entries[i];
            if (entry.Generation == 0)
            {
                if (!Zero(owner->Entries + i, (ulong)sizeof(Entry)))
                    return Status.InvalidState;
                continue;
            }

            var record = entry.Value;
            held++;
            if (entry.Generation > owner->Issued || !KeyValid(record.Key) || record.DurationNanoseconds < MinimumDurationNanoseconds || record.OriginalDeadlineNanoseconds < record.DurationNanoseconds || record.DeadlineNanoseconds < record.OriginalDeadlineNanoseconds || record.DeadlineNanoseconds - record.DurationNanoseconds > owner->LastNow)
                return Status.InvalidState;
        }

        return held == owner->Held ? Status.Success : Status.InvalidState;
    }

    private static bool KeyValid(TcpConnectionKey key) => key.HasValidEndpoint && key.LocalAddress.Octet0 is > 0 and < 224 && key.RemoteAddress.Octet0 is > 0 and < 224;
    private static bool Storage(void* address, ulong bytes) => address != null && ((ulong)address & 7) == 0 && bytes <= ~0UL - (ulong)address;
    private static bool Overlap(void* a, ulong size, void* b, ulong other) => (ulong)a < (ulong)b + other && (ulong)b < (ulong)a + size;
    private static bool Zero(void* address, ulong bytes)
    {
        for (ulong i = 0; i < bytes; i++)
            if (((byte*)address)[i] != 0)
                return false;
        return true;
    }

    private static void Clear(void* address, ulong bytes)
    {
        for (ulong i = 0; i < bytes; i++)
            ((byte*)address)[i] = 0;
    }
}
