namespace OS.Net;

public static partial class TcpListenerBacklog
{
    public const int NoEntryIndex = -1;
    public const bool RequiresCallerOwnedEntryStorage = true;
    public const bool ParsesPackets = false;
    public const bool AllocatesEntries = false;
    public const bool ExportsSocketHandles = false;
    public static TcpListenerBacklogResult Enqueue(Span<TcpListenerBacklogEntry> entries, TcpListenerBacklogConnection connection)
    {
        if (!connection.HasValidEndpoint)
        {
            return Result(TcpListenerBacklogStatus.InvalidEndpoint, entries, count: 0, requiredCapacity: 1, NoEntryIndex, connection);
        }

        ScanResult scan = Scan(entries, connection);
        if (scan.Status != TcpListenerBacklogStatus.Success)
        {
            return Result(scan.Status, entries, scan.Count, scan.Count + 1, scan.MatchingEntryIndex, connection);
        }

        if (scan.MatchingEntryIndex != NoEntryIndex)
        {
            return Result(TcpListenerBacklogStatus.DuplicateEndpoint, entries, scan.Count, scan.Count + 1, scan.MatchingEntryIndex, connection);
        }

        if (scan.FirstFreeEntryIndex == NoEntryIndex)
        {
            return Result(TcpListenerBacklogStatus.BacklogFull, entries, scan.Count, scan.Count + 1, NoEntryIndex, connection);
        }

        if (scan.MaximumEnqueueOrder == ulong.MaxValue)
        {
            return Result(TcpListenerBacklogStatus.EnqueueOrderExhausted, entries, scan.Count, scan.Count + 1, NoEntryIndex, connection);
        }

        int entryIndex = scan.FirstFreeEntryIndex;
        entries[entryIndex] = TcpListenerBacklogEntry.Pending(connection, scan.MaximumEnqueueOrder + 1);
        return Result(TcpListenerBacklogStatus.Success, entries, scan.Count + 1, scan.Count + 1, entryIndex, connection);
    }

    public static TcpListenerBacklogResult Accept(Span<TcpListenerBacklogEntry> entries)
    {
        ScanResult scan = Scan(entries, default);
        if (scan.Status != TcpListenerBacklogStatus.Success)
        {
            return Result(scan.Status, entries, scan.Count, requiredCapacity: 1, scan.OldestEntryIndex, default);
        }

        if (scan.OldestEntryIndex == NoEntryIndex)
        {
            return Result(TcpListenerBacklogStatus.BacklogEmpty, entries, count: 0, requiredCapacity: 1, NoEntryIndex, default);
        }

        int entryIndex = scan.OldestEntryIndex;
        TcpListenerBacklogConnection connection = entries[entryIndex].Connection;
        entries[entryIndex] = default;
        return Result(TcpListenerBacklogStatus.Success, entries, scan.Count - 1, scan.Count, entryIndex, connection);
    }

    public static TcpListenerBacklogResult Inspect(ReadOnlySpan<TcpListenerBacklogEntry> entries)
    {
        ScanResult scan = Scan(entries, default);
        TcpListenerBacklogStatus status = scan.Status;
        return TcpListenerBacklogResult.Create(status, scan.Count, entries.Length, scan.Count, scan.OldestEntryIndex, scan.OldestEntryIndex == NoEntryIndex ? default : entries[scan.OldestEntryIndex].Connection);
    }

    private static TcpListenerBacklogResult Result(TcpListenerBacklogStatus status, ReadOnlySpan<TcpListenerBacklogEntry> entries, int count, int requiredCapacity, int entryIndex, TcpListenerBacklogConnection connection)
    {
        return TcpListenerBacklogResult.Create(status, count, entries.Length, requiredCapacity, entryIndex, connection);
    }

    private static ScanResult Scan(ReadOnlySpan<TcpListenerBacklogEntry> entries, TcpListenerBacklogConnection match)
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
                return new ScanResult(TcpListenerBacklogStatus.InvalidEntryState, count, firstFreeEntryIndex, matchingEntryIndex, oldestEntryIndex, maximumOrder);
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

        return new ScanResult(TcpListenerBacklogStatus.Success, count, firstFreeEntryIndex, matchingEntryIndex, oldestEntryIndex, maximumOrder);
    }
}
