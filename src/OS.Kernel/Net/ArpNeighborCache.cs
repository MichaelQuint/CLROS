namespace OS.Net;

public enum ArpNeighborCacheEntryState
{
    Empty = 0,
    Resolved = 1
}

public enum ArpNeighborCacheStatus
{
    Success = 0,
    InvalidAddress = 1,
    InvalidMacAddress = 2,
    NotFound = 3,
    CapacityExceeded = 4,
    InvalidTtl = 5,
    InvalidTick = 6
}

public readonly struct ArpNeighborCacheEntry
{
    private ArpNeighborCacheEntry(
        Ipv4Address address,
        MacAddress macAddress,
        ArpNeighborCacheEntryState state,
        ulong lastUpdatedTick)
    {
        Address = address;
        MacAddress = macAddress;
        State = state;
        LastUpdatedTick = lastUpdatedTick;
    }

    public Ipv4Address Address { get; }

    public MacAddress MacAddress { get; }

    public ArpNeighborCacheEntryState State { get; }

    public ulong LastUpdatedTick { get; }

    public bool IsActive => State != ArpNeighborCacheEntryState.Empty;

    public static ArpNeighborCacheEntry Resolved(
        Ipv4Address address,
        MacAddress macAddress,
        ulong lastUpdatedTick)
    {
        return new ArpNeighborCacheEntry(
            address,
            macAddress,
            ArpNeighborCacheEntryState.Resolved,
            lastUpdatedTick);
    }
}

public readonly struct ArpNeighborCacheLookupResult
{
    private ArpNeighborCacheLookupResult(
        ArpNeighborCacheStatus status,
        ArpNeighborCacheEntry entry,
        int count,
        int capacity)
    {
        IsInitialized = true;
        Status = status;
        Entry = entry;
        Count = count;
        Capacity = capacity;
    }

    public bool IsInitialized { get; }

    public ArpNeighborCacheStatus Status { get; }

    public bool Succeeded => IsInitialized && Status == ArpNeighborCacheStatus.Success;

    public ArpNeighborCacheEntry Entry { get; }

    public int Count { get; }

    public int Capacity { get; }

    public static ArpNeighborCacheLookupResult Success(
        ArpNeighborCacheEntry entry,
        int count,
        int capacity)
    {
        return new ArpNeighborCacheLookupResult(
            ArpNeighborCacheStatus.Success,
            entry,
            count,
            capacity);
    }

    public static ArpNeighborCacheLookupResult Fail(
        ArpNeighborCacheStatus status,
        int count,
        int capacity)
    {
        return new ArpNeighborCacheLookupResult(status, default, count, capacity);
    }
}

public readonly struct ArpNeighborCacheMutationResult
{
    private ArpNeighborCacheMutationResult(
        ArpNeighborCacheStatus status,
        int count,
        int capacity,
        int removedCount,
        bool updatedExisting)
    {
        IsInitialized = true;
        Status = status;
        Count = count;
        Capacity = capacity;
        RemovedCount = removedCount;
        UpdatedExisting = updatedExisting;
    }

    public bool IsInitialized { get; }

    public ArpNeighborCacheStatus Status { get; }

    public bool Succeeded => IsInitialized && Status == ArpNeighborCacheStatus.Success;

    public int Count { get; }

    public int Capacity { get; }

    public int RemovedCount { get; }

    public bool UpdatedExisting { get; }

    public static ArpNeighborCacheMutationResult Success(
        int count,
        int capacity,
        int removedCount,
        bool updatedExisting)
    {
        return new ArpNeighborCacheMutationResult(
            ArpNeighborCacheStatus.Success,
            count,
            capacity,
            removedCount,
            updatedExisting);
    }

    public static ArpNeighborCacheMutationResult Fail(
        ArpNeighborCacheStatus status,
        int count,
        int capacity)
    {
        return new ArpNeighborCacheMutationResult(
            status,
            count,
            capacity,
            removedCount: 0,
            updatedExisting: false);
    }
}

public ref struct ArpNeighborCache
{
    private Span<ArpNeighborCacheEntry> _entries;
    private int _count;

    private ArpNeighborCache(Span<ArpNeighborCacheEntry> entries)
    {
        _entries = entries;
        _count = 0;
        ClearStorage();
    }

    public int Count => _count;

    public int Capacity => _entries.Length;

    public static ArpNeighborCache Create(Span<ArpNeighborCacheEntry> entries)
    {
        return new ArpNeighborCache(entries);
    }

    public ArpNeighborCacheStatus TryLookup(
        Ipv4Address address,
        out ArpNeighborCacheLookupResult result)
    {
        ArpNeighborCacheStatus validationStatus = ValidateAddress(address);
        if (validationStatus != ArpNeighborCacheStatus.Success)
        {
            result = ArpNeighborCacheLookupResult.Fail(validationStatus, Count, Capacity);
            return validationStatus;
        }

        int index = FindIndex(address);
        if (index < 0)
        {
            result = ArpNeighborCacheLookupResult.Fail(
                ArpNeighborCacheStatus.NotFound,
                Count,
                Capacity);
            return result.Status;
        }

        result = ArpNeighborCacheLookupResult.Success(_entries[index], Count, Capacity);
        return result.Status;
    }

    public ArpNeighborCacheStatus InsertOrUpdate(
        Ipv4Address address,
        MacAddress macAddress,
        ulong currentTick,
        out ArpNeighborCacheMutationResult result)
    {
        ArpNeighborCacheStatus validationStatus = ValidateAddress(address);
        if (validationStatus != ArpNeighborCacheStatus.Success)
        {
            result = ArpNeighborCacheMutationResult.Fail(validationStatus, Count, Capacity);
            return validationStatus;
        }

        validationStatus = ValidateMacAddress(macAddress);
        if (validationStatus != ArpNeighborCacheStatus.Success)
        {
            result = ArpNeighborCacheMutationResult.Fail(validationStatus, Count, Capacity);
            return validationStatus;
        }

        int index = FindIndex(address);
        if (index >= 0)
        {
            _entries[index] = ArpNeighborCacheEntry.Resolved(address, macAddress, currentTick);
            result = ArpNeighborCacheMutationResult.Success(
                Count,
                Capacity,
                removedCount: 0,
                updatedExisting: true);
            return result.Status;
        }

        if (_count >= _entries.Length)
        {
            result = ArpNeighborCacheMutationResult.Fail(
                ArpNeighborCacheStatus.CapacityExceeded,
                Count,
                Capacity);
            return result.Status;
        }

        _entries[_count] = ArpNeighborCacheEntry.Resolved(address, macAddress, currentTick);
        _count++;
        result = ArpNeighborCacheMutationResult.Success(
            Count,
            Capacity,
            removedCount: 0,
            updatedExisting: false);
        return result.Status;
    }

    public ArpNeighborCacheStatus Remove(
        Ipv4Address address,
        out ArpNeighborCacheMutationResult result)
    {
        ArpNeighborCacheStatus validationStatus = ValidateAddress(address);
        if (validationStatus != ArpNeighborCacheStatus.Success)
        {
            result = ArpNeighborCacheMutationResult.Fail(validationStatus, Count, Capacity);
            return validationStatus;
        }

        int index = FindIndex(address);
        if (index < 0)
        {
            result = ArpNeighborCacheMutationResult.Fail(
                ArpNeighborCacheStatus.NotFound,
                Count,
                Capacity);
            return result.Status;
        }

        RemoveAt(index);
        result = ArpNeighborCacheMutationResult.Success(
            Count,
            Capacity,
            removedCount: 1,
            updatedExisting: false);
        return result.Status;
    }

    public ArpNeighborCacheStatus Clear(out ArpNeighborCacheMutationResult result)
    {
        int removedCount = _count;
        ClearStorage();
        _count = 0;
        result = ArpNeighborCacheMutationResult.Success(
            Count,
            Capacity,
            removedCount,
            updatedExisting: false);
        return result.Status;
    }

    public ArpNeighborCacheStatus PurgeStale(
        ulong currentTick,
        ulong ttlTicks,
        out ArpNeighborCacheMutationResult result)
    {
        if (ttlTicks == 0)
        {
            result = ArpNeighborCacheMutationResult.Fail(
                ArpNeighborCacheStatus.InvalidTtl,
                Count,
                Capacity);
            return result.Status;
        }

        for (int index = 0; index < _count; index++)
        {
            if (currentTick < _entries[index].LastUpdatedTick)
            {
                result = ArpNeighborCacheMutationResult.Fail(
                    ArpNeighborCacheStatus.InvalidTick,
                    Count,
                    Capacity);
                return result.Status;
            }
        }

        int removedCount = 0;
        int cursor = 0;
        while (cursor < _count)
        {
            ulong age = currentTick - _entries[cursor].LastUpdatedTick;
            if (age >= ttlTicks)
            {
                RemoveAt(cursor);
                removedCount++;
                continue;
            }

            cursor++;
        }

        result = ArpNeighborCacheMutationResult.Success(
            Count,
            Capacity,
            removedCount,
            updatedExisting: false);
        return result.Status;
    }

    private void ClearStorage()
    {
        for (int index = 0; index < _entries.Length; index++)
        {
            _entries[index] = default;
        }
    }

    private void RemoveAt(int index)
    {
        for (int cursor = index; cursor < _count - 1; cursor++)
        {
            _entries[cursor] = _entries[cursor + 1];
        }

        _count--;
        _entries[_count] = default;
    }

    private int FindIndex(Ipv4Address address)
    {
        for (int index = 0; index < _count; index++)
        {
            if (_entries[index].Address == address)
            {
                return index;
            }
        }

        return -1;
    }

    private static ArpNeighborCacheStatus ValidateAddress(Ipv4Address address)
    {
        return address == default
            ? ArpNeighborCacheStatus.InvalidAddress
            : ArpNeighborCacheStatus.Success;
    }

    private static ArpNeighborCacheStatus ValidateMacAddress(MacAddress macAddress)
    {
        return macAddress.IsZero || macAddress.IsBroadcast
            ? ArpNeighborCacheStatus.InvalidMacAddress
            : ArpNeighborCacheStatus.Success;
    }
}
