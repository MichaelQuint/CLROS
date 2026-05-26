namespace OS.Net;

public enum TcpConnectionLifecycleState
{
    Free = 0,
    Established = 1,
    Closing = 2,
    Closed = 3
}

public enum TcpConnectionTableStatus
{
    Success = 0,
    InvalidEndpoint = 1,
    InvalidSequenceMetadata = 2,
    InvalidEntryState = 3,
    DuplicateEndpoint = 4,
    TableFull = 5,
    NotFound = 6,
    InvalidLifecycleState = 7
}

public readonly struct TcpConnectionKey
{
    public TcpConnectionKey(
        Ipv4Address localAddress,
        ushort localPort,
        Ipv4Address remoteAddress,
        ushort remotePort)
    {
        LocalAddress = localAddress;
        LocalPort = localPort;
        RemoteAddress = remoteAddress;
        RemotePort = remotePort;
    }

    public Ipv4Address LocalAddress { get; }

    public ushort LocalPort { get; }

    public Ipv4Address RemoteAddress { get; }

    public ushort RemotePort { get; }

    public bool HasValidEndpoint =>
        LocalAddress != default &&
        RemoteAddress != default &&
        LocalPort != 0 &&
        RemotePort != 0;

    public bool HasSameEndpoint(in TcpConnectionKey other)
    {
        return LocalAddress == other.LocalAddress &&
            LocalPort == other.LocalPort &&
            RemoteAddress == other.RemoteAddress &&
            RemotePort == other.RemotePort;
    }
}

public readonly struct TcpConnectionSequenceMetadata
{
    public TcpConnectionSequenceMetadata(
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public uint LocalNextSequenceNumber { get; }

    public uint RemoteNextSequenceNumber { get; }

    public bool HasValidMetadata =>
        LocalNextSequenceNumber != 0 &&
        RemoteNextSequenceNumber != 0;
}

public struct TcpConnectionTableEntry
{
    private TcpConnectionTableEntry(
        TcpConnectionLifecycleState state,
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        State = state;
        Key = key;
        Sequence = sequence;
    }

    public TcpConnectionLifecycleState State { get; }

    public TcpConnectionKey Key { get; }

    public TcpConnectionSequenceMetadata Sequence { get; }

    public bool IsActive =>
        State == TcpConnectionLifecycleState.Established ||
        State == TcpConnectionLifecycleState.Closing;

    public static TcpConnectionTableEntry Create(
        TcpConnectionLifecycleState state,
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(state, key, sequence);
    }

    internal static TcpConnectionTableEntry Established(
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(
            TcpConnectionLifecycleState.Established,
            key,
            sequence);
    }

    internal static TcpConnectionTableEntry Closing(
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(
            TcpConnectionLifecycleState.Closing,
            key,
            sequence);
    }

    internal static TcpConnectionTableEntry Closed(
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        return new TcpConnectionTableEntry(
            TcpConnectionLifecycleState.Closed,
            key,
            sequence);
    }
}

public readonly struct TcpConnectionTableResult
{
    private TcpConnectionTableResult(
        TcpConnectionTableStatus status,
        int count,
        int capacity,
        int requiredCapacity,
        int entryIndex,
        TcpConnectionTableEntry entry)
    {
        IsInitialized = true;
        Status = status;
        Count = count;
        Capacity = capacity;
        RequiredCapacity = requiredCapacity;
        EntryIndex = entryIndex;
        Entry = entry;
    }

    public TcpConnectionTableStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == TcpConnectionTableStatus.Success;

    public int Count { get; }

    public int Capacity { get; }

    public int RequiredCapacity { get; }

    public int EntryIndex { get; }

    public TcpConnectionTableEntry Entry { get; }

    public static TcpConnectionTableResult Create(
        TcpConnectionTableStatus status,
        int count,
        int capacity,
        int requiredCapacity,
        int entryIndex,
        TcpConnectionTableEntry entry)
    {
        return new TcpConnectionTableResult(
            status,
            count,
            capacity,
            requiredCapacity,
            entryIndex,
            entry);
    }
}

public static class TcpConnectionTable
{
    public const int NoEntryIndex = -1;

    public const bool RequiresCallerOwnedEntryStorage = true;
    public const bool AllocatesEntries = false;
    public const bool OwnsTimers = false;
    public const bool DispatchesSockets = false;
    public const bool PerformsNicIo = false;

    public static TcpConnectionTableResult AddEstablished(
        Span<TcpConnectionTableEntry> entries,
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        TcpConnectionTableStatus validationStatus = ValidateKeyAndSequence(key, sequence);
        if (validationStatus != TcpConnectionTableStatus.Success)
        {
            return Result(validationStatus, entries, count: 0, requiredCapacity: 1, NoEntryIndex, default);
        }

        ScanResult scan = Scan(entries, key);
        if (scan.Status != TcpConnectionTableStatus.Success)
        {
            return Result(
                scan.Status,
                entries,
                scan.Count,
                scan.Count + 1,
                scan.MatchingEntryIndex,
                default);
        }

        if (scan.MatchingEntryIndex != NoEntryIndex)
        {
            return Result(
                TcpConnectionTableStatus.DuplicateEndpoint,
                entries,
                scan.Count,
                scan.Count + 1,
                scan.MatchingEntryIndex,
                entries[scan.MatchingEntryIndex]);
        }

        if (scan.FirstReusableEntryIndex == NoEntryIndex)
        {
            return Result(
                TcpConnectionTableStatus.TableFull,
                entries,
                scan.Count,
                scan.Count + 1,
                NoEntryIndex,
                default);
        }

        int entryIndex = scan.FirstReusableEntryIndex;
        entries[entryIndex] = TcpConnectionTableEntry.Established(key, sequence);
        return Result(
            TcpConnectionTableStatus.Success,
            entries,
            scan.Count + 1,
            scan.Count + 1,
            entryIndex,
            entries[entryIndex]);
    }

    public static TcpConnectionTableResult Lookup(
        ReadOnlySpan<TcpConnectionTableEntry> entries,
        TcpConnectionKey key)
    {
        if (!key.HasValidEndpoint)
        {
            return Result(
                TcpConnectionTableStatus.InvalidEndpoint,
                entries,
                count: 0,
                requiredCapacity: 1,
                NoEntryIndex,
                default);
        }

        ScanResult scan = Scan(entries, key);
        if (scan.Status != TcpConnectionTableStatus.Success)
        {
            return Result(scan.Status, entries, scan.Count, scan.Count, scan.MatchingEntryIndex, default);
        }

        if (scan.MatchingEntryIndex == NoEntryIndex)
        {
            return Result(
                TcpConnectionTableStatus.NotFound,
                entries,
                scan.Count,
                requiredCapacity: 1,
                NoEntryIndex,
                default);
        }

        return Result(
            TcpConnectionTableStatus.Success,
            entries,
            scan.Count,
            scan.Count,
            scan.MatchingEntryIndex,
            entries[scan.MatchingEntryIndex]);
    }

    public static TcpConnectionTableResult UpdateSequenceMetadata(
        Span<TcpConnectionTableEntry> entries,
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        if (!sequence.HasValidMetadata)
        {
            return Result(
                TcpConnectionTableStatus.InvalidSequenceMetadata,
                entries,
                count: 0,
                requiredCapacity: 1,
                NoEntryIndex,
                default);
        }

        TcpConnectionTableResult lookup = Lookup(entries, key);
        if (!lookup.Succeeded)
        {
            return lookup;
        }

        TcpConnectionTableEntry current = lookup.Entry;
        entries[lookup.EntryIndex] = current.State == TcpConnectionLifecycleState.Closing
            ? TcpConnectionTableEntry.Closing(current.Key, sequence)
            : TcpConnectionTableEntry.Established(current.Key, sequence);
        return Result(
            TcpConnectionTableStatus.Success,
            entries,
            lookup.Count,
            lookup.RequiredCapacity,
            lookup.EntryIndex,
            entries[lookup.EntryIndex]);
    }

    public static TcpConnectionTableResult MarkClosing(
        Span<TcpConnectionTableEntry> entries,
        TcpConnectionKey key)
    {
        TcpConnectionTableResult lookup = Lookup(entries, key);
        if (!lookup.Succeeded)
        {
            return lookup;
        }

        if (lookup.Entry.State == TcpConnectionLifecycleState.Closing)
        {
            return lookup;
        }

        if (lookup.Entry.State != TcpConnectionLifecycleState.Established)
        {
            return Result(
                TcpConnectionTableStatus.InvalidLifecycleState,
                entries,
                lookup.Count,
                lookup.RequiredCapacity,
                lookup.EntryIndex,
                lookup.Entry);
        }

        entries[lookup.EntryIndex] = TcpConnectionTableEntry.Closing(
            lookup.Entry.Key,
            lookup.Entry.Sequence);
        return Result(
            TcpConnectionTableStatus.Success,
            entries,
            lookup.Count,
            lookup.RequiredCapacity,
            lookup.EntryIndex,
            entries[lookup.EntryIndex]);
    }

    public static TcpConnectionTableResult Remove(
        Span<TcpConnectionTableEntry> entries,
        TcpConnectionKey key)
    {
        TcpConnectionTableResult lookup = Lookup(entries, key);
        if (!lookup.Succeeded)
        {
            return lookup;
        }

        entries[lookup.EntryIndex] = TcpConnectionTableEntry.Closed(
            lookup.Entry.Key,
            lookup.Entry.Sequence);
        return Result(
            TcpConnectionTableStatus.Success,
            entries,
            lookup.Count - 1,
            lookup.Count,
            lookup.EntryIndex,
            entries[lookup.EntryIndex]);
    }

    public static TcpConnectionTableResult Inspect(ReadOnlySpan<TcpConnectionTableEntry> entries)
    {
        ScanResult scan = Scan(entries, default);
        return Result(scan.Status, entries, scan.Count, scan.Count, scan.MatchingEntryIndex, default);
    }

    private static TcpConnectionTableStatus ValidateKeyAndSequence(
        TcpConnectionKey key,
        TcpConnectionSequenceMetadata sequence)
    {
        if (!key.HasValidEndpoint)
        {
            return TcpConnectionTableStatus.InvalidEndpoint;
        }

        return sequence.HasValidMetadata
            ? TcpConnectionTableStatus.Success
            : TcpConnectionTableStatus.InvalidSequenceMetadata;
    }

    private static TcpConnectionTableResult Result(
        TcpConnectionTableStatus status,
        ReadOnlySpan<TcpConnectionTableEntry> entries,
        int count,
        int requiredCapacity,
        int entryIndex,
        TcpConnectionTableEntry entry)
    {
        return TcpConnectionTableResult.Create(
            status,
            count,
            entries.Length,
            requiredCapacity,
            entryIndex,
            entry);
    }

    private static ScanResult Scan(
        ReadOnlySpan<TcpConnectionTableEntry> entries,
        TcpConnectionKey match)
    {
        int count = 0;
        int firstReusableEntryIndex = NoEntryIndex;
        int matchingEntryIndex = NoEntryIndex;

        for (int index = 0; index < entries.Length; index++)
        {
            TcpConnectionTableEntry entry = entries[index];
            if (entry.State == TcpConnectionLifecycleState.Free ||
                entry.State == TcpConnectionLifecycleState.Closed)
            {
                if (firstReusableEntryIndex == NoEntryIndex)
                {
                    firstReusableEntryIndex = index;
                }

                continue;
            }

            if (entry.State != TcpConnectionLifecycleState.Established &&
                entry.State != TcpConnectionLifecycleState.Closing)
            {
                return new ScanResult(
                    TcpConnectionTableStatus.InvalidEntryState,
                    count,
                    firstReusableEntryIndex,
                    matchingEntryIndex);
            }

            if (!entry.Key.HasValidEndpoint || !entry.Sequence.HasValidMetadata)
            {
                return new ScanResult(
                    TcpConnectionTableStatus.InvalidEntryState,
                    count,
                    firstReusableEntryIndex,
                    matchingEntryIndex);
            }

            count++;
            if (match.HasValidEndpoint && entry.Key.HasSameEndpoint(match))
            {
                matchingEntryIndex = index;
            }
        }

        return new ScanResult(
            TcpConnectionTableStatus.Success,
            count,
            firstReusableEntryIndex,
            matchingEntryIndex);
    }

    private readonly struct ScanResult
    {
        public ScanResult(
            TcpConnectionTableStatus status,
            int count,
            int firstReusableEntryIndex,
            int matchingEntryIndex)
        {
            Status = status;
            Count = count;
            FirstReusableEntryIndex = firstReusableEntryIndex;
            MatchingEntryIndex = matchingEntryIndex;
        }

        public TcpConnectionTableStatus Status { get; }

        public int Count { get; }

        public int FirstReusableEntryIndex { get; }

        public int MatchingEntryIndex { get; }
    }
}
