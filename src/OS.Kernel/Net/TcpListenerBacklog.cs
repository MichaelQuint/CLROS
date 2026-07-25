namespace OS.Net;

public enum TcpListenerBacklogEntryState
{
    Free = 0,
    PendingAccept = 1
}

public enum TcpListenerBacklogStatus
{
    Success = 0,
    InvalidEndpoint = 1,
    InvalidEntryState = 2,
    DuplicateEndpoint = 3,
    BacklogFull = 4,
    BacklogEmpty = 5,
    EnqueueOrderExhausted = 6
}

public readonly struct TcpListenerBacklogConnection
{
    public TcpListenerBacklogConnection(
        Ipv4Address localAddress,
        ushort localPort,
        Ipv4Address remoteAddress,
        ushort remotePort,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        LocalAddress = localAddress;
        LocalPort = localPort;
        RemoteAddress = remoteAddress;
        RemotePort = remotePort;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public Ipv4Address LocalAddress { get; }

    public ushort LocalPort { get; }

    public Ipv4Address RemoteAddress { get; }

    public ushort RemotePort { get; }

    public uint LocalNextSequenceNumber { get; }

    public uint RemoteNextSequenceNumber { get; }

    public bool HasValidEndpoint =>
        LocalAddress != default &&
        RemoteAddress != default &&
        LocalPort != 0 &&
        RemotePort != 0;

    public bool HasSameEndpoint(in TcpListenerBacklogConnection other)
    {
        return LocalAddress == other.LocalAddress &&
            LocalPort == other.LocalPort &&
            RemoteAddress == other.RemoteAddress &&
            RemotePort == other.RemotePort;
    }
}

public struct TcpListenerBacklogEntry
{
    private TcpListenerBacklogEntry(
        TcpListenerBacklogEntryState state,
        TcpListenerBacklogConnection connection,
        ulong enqueueOrder)
    {
        State = state;
        Connection = connection;
        EnqueueOrder = enqueueOrder;
    }

    public TcpListenerBacklogEntryState State { get; }

    public TcpListenerBacklogConnection Connection { get; }

    public ulong EnqueueOrder { get; }

    public bool IsPendingAccept => State == TcpListenerBacklogEntryState.PendingAccept;

    internal static TcpListenerBacklogEntry Pending(
        TcpListenerBacklogConnection connection,
        ulong enqueueOrder)
    {
        return new TcpListenerBacklogEntry(
            TcpListenerBacklogEntryState.PendingAccept,
            connection,
            enqueueOrder);
    }
}

public readonly struct TcpListenerBacklogResult
{
    private TcpListenerBacklogResult(
        TcpListenerBacklogStatus status,
        int count,
        int capacity,
        int requiredCapacity,
        int entryIndex,
        TcpListenerBacklogConnection connection)
    {
        IsInitialized = true;
        Status = status;
        Count = count;
        Capacity = capacity;
        RequiredCapacity = requiredCapacity;
        EntryIndex = entryIndex;
        Connection = connection;
    }

    public TcpListenerBacklogStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == TcpListenerBacklogStatus.Success;

    public int Count { get; }

    public int Capacity { get; }

    public int RequiredCapacity { get; }

    public int EntryIndex { get; }

    public TcpListenerBacklogConnection Connection { get; }

    public static TcpListenerBacklogResult Create(
        TcpListenerBacklogStatus status,
        int count,
        int capacity,
        int requiredCapacity,
        int entryIndex,
        TcpListenerBacklogConnection connection)
    {
        return new TcpListenerBacklogResult(
            status,
            count,
            capacity,
            requiredCapacity,
            entryIndex,
            connection);
    }
}

public static class TcpListenerBacklog
{
    public const int NoEntryIndex = -1;

    public const bool RequiresCallerOwnedEntryStorage = true;
    public const bool ParsesPackets = false;
    public const bool AllocatesEntries = false;
    public const bool ExportsSocketHandles = false;

    public static TcpListenerBacklogResult Enqueue(
        Span<TcpListenerBacklogEntry> entries,
        TcpListenerBacklogConnection connection)
    {
        if (!connection.HasValidEndpoint)
        {
            return Result(
                TcpListenerBacklogStatus.InvalidEndpoint,
                entries,
                count: 0,
                requiredCapacity: 1,
                NoEntryIndex,
                connection);
        }

        ScanResult scan = Scan(entries, connection);
        if (scan.Status != TcpListenerBacklogStatus.Success)
        {
            return Result(
                scan.Status,
                entries,
                scan.Count,
                scan.Count + 1,
                scan.MatchingEntryIndex,
                connection);
        }

        if (scan.MatchingEntryIndex != NoEntryIndex)
        {
            return Result(
                TcpListenerBacklogStatus.DuplicateEndpoint,
                entries,
                scan.Count,
                scan.Count + 1,
                scan.MatchingEntryIndex,
                connection);
        }

        if (scan.FirstFreeEntryIndex == NoEntryIndex)
        {
            return Result(
                TcpListenerBacklogStatus.BacklogFull,
                entries,
                scan.Count,
                scan.Count + 1,
                NoEntryIndex,
                connection);
        }

        if (scan.MaximumEnqueueOrder == ulong.MaxValue)
        {
            return Result(
                TcpListenerBacklogStatus.EnqueueOrderExhausted,
                entries,
                scan.Count,
                scan.Count + 1,
                NoEntryIndex,
                connection);
        }

        int entryIndex = scan.FirstFreeEntryIndex;
        entries[entryIndex] = TcpListenerBacklogEntry.Pending(
            connection,
            scan.MaximumEnqueueOrder + 1);

        return Result(
            TcpListenerBacklogStatus.Success,
            entries,
            scan.Count + 1,
            scan.Count + 1,
            entryIndex,
            connection);
    }

    public static TcpListenerBacklogResult Accept(Span<TcpListenerBacklogEntry> entries)
    {
        ScanResult scan = Scan(entries, default);
        if (scan.Status != TcpListenerBacklogStatus.Success)
        {
            return Result(
                scan.Status,
                entries,
                scan.Count,
                requiredCapacity: 1,
                scan.OldestEntryIndex,
                default);
        }

        if (scan.OldestEntryIndex == NoEntryIndex)
        {
            return Result(
                TcpListenerBacklogStatus.BacklogEmpty,
                entries,
                count: 0,
                requiredCapacity: 1,
                NoEntryIndex,
                default);
        }

        int entryIndex = scan.OldestEntryIndex;
        TcpListenerBacklogConnection connection = entries[entryIndex].Connection;
        entries[entryIndex] = default;

        return Result(
            TcpListenerBacklogStatus.Success,
            entries,
            scan.Count - 1,
            scan.Count,
            entryIndex,
            connection);
    }

    public static TcpListenerBacklogResult Inspect(ReadOnlySpan<TcpListenerBacklogEntry> entries)
    {
        ScanResult scan = Scan(entries, default);
        TcpListenerBacklogStatus status = scan.Status;
        return TcpListenerBacklogResult.Create(
            status,
            scan.Count,
            entries.Length,
            scan.Count,
            scan.OldestEntryIndex,
            scan.OldestEntryIndex == NoEntryIndex ? default : entries[scan.OldestEntryIndex].Connection);
    }

    private static TcpListenerBacklogResult Result(
        TcpListenerBacklogStatus status,
        ReadOnlySpan<TcpListenerBacklogEntry> entries,
        int count,
        int requiredCapacity,
        int entryIndex,
        TcpListenerBacklogConnection connection)
    {
        return TcpListenerBacklogResult.Create(
            status,
            count,
            entries.Length,
            requiredCapacity,
            entryIndex,
            connection);
    }

    private static ScanResult Scan(
        ReadOnlySpan<TcpListenerBacklogEntry> entries,
        TcpListenerBacklogConnection match)
    {
        int count = 0;
        int firstFreeEntryIndex = NoEntryIndex;
        int matchingEntryIndex = NoEntryIndex;
        int oldestEntryIndex = NoEntryIndex;
        ulong oldestOrder = ulong.MaxValue;
        ulong maximumOrder = 0;

        for (int index = 0; index < entries.Length; index++)
        {
            TcpListenerBacklogEntry entry = entries[index];
            if (entry.State == TcpListenerBacklogEntryState.Free)
            {
                if (firstFreeEntryIndex == NoEntryIndex)
                {
                    firstFreeEntryIndex = index;
                }

                continue;
            }

            if (entry.State != TcpListenerBacklogEntryState.PendingAccept)
            {
                return new ScanResult(
                    TcpListenerBacklogStatus.InvalidEntryState,
                    count,
                    firstFreeEntryIndex,
                    matchingEntryIndex,
                    oldestEntryIndex,
                    maximumOrder);
            }

            count++;
            if (match.HasValidEndpoint && entry.Connection.HasSameEndpoint(match))
            {
                matchingEntryIndex = index;
            }

            if (entry.EnqueueOrder < oldestOrder)
            {
                oldestOrder = entry.EnqueueOrder;
                oldestEntryIndex = index;
            }

            if (entry.EnqueueOrder > maximumOrder)
            {
                maximumOrder = entry.EnqueueOrder;
            }
        }

        return new ScanResult(
            TcpListenerBacklogStatus.Success,
            count,
            firstFreeEntryIndex,
            matchingEntryIndex,
            oldestEntryIndex,
            maximumOrder);
    }

    private readonly struct ScanResult
    {
        public ScanResult(
            TcpListenerBacklogStatus status,
            int count,
            int firstFreeEntryIndex,
            int matchingEntryIndex,
            int oldestEntryIndex,
            ulong maximumEnqueueOrder)
        {
            Status = status;
            Count = count;
            FirstFreeEntryIndex = firstFreeEntryIndex;
            MatchingEntryIndex = matchingEntryIndex;
            OldestEntryIndex = oldestEntryIndex;
            MaximumEnqueueOrder = maximumEnqueueOrder;
        }

        public TcpListenerBacklogStatus Status { get; }

        public int Count { get; }

        public int FirstFreeEntryIndex { get; }

        public int MatchingEntryIndex { get; }

        public int OldestEntryIndex { get; }

        public ulong MaximumEnqueueOrder { get; }
    }
}
