namespace OS.Net;

public enum TcpPassiveListenerBacklogBridgeStatus
{
    Success = 0,
    PassiveOpenFailed = 1,
    NotEstablished = 2,
    BacklogEnqueueFailed = 3
}

public readonly struct TcpPassiveListenerBacklogBridgeResult
{
    private TcpPassiveListenerBacklogBridgeResult(
        TcpPassiveListenerBacklogBridgeStatus status,
        TcpPassiveIpv4FramePlanStatus passiveStatus,
        TcpPassiveOpenHandshakeStatus tcpStatus,
        PacketParseStatus packetStatus,
        TcpPassiveOpenHandshakeState passiveState,
        TcpListenerBacklogStatus backlogStatus,
        int backlogCount,
        int backlogCapacity,
        int requiredCapacity,
        int entryIndex,
        TcpListenerBacklogConnection connection)
    {
        IsInitialized = true;
        Status = status;
        PassiveStatus = passiveStatus;
        TcpStatus = tcpStatus;
        PacketStatus = packetStatus;
        PassiveState = passiveState;
        BacklogStatus = backlogStatus;
        BacklogCount = backlogCount;
        BacklogCapacity = backlogCapacity;
        RequiredCapacity = requiredCapacity;
        EntryIndex = entryIndex;
        Connection = connection;
    }

    public TcpPassiveListenerBacklogBridgeStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == TcpPassiveListenerBacklogBridgeStatus.Success;

    public TcpPassiveIpv4FramePlanStatus PassiveStatus { get; }

    public TcpPassiveOpenHandshakeStatus TcpStatus { get; }

    public PacketParseStatus PacketStatus { get; }

    public TcpPassiveOpenHandshakeState PassiveState { get; }

    public TcpListenerBacklogStatus BacklogStatus { get; }

    public int BacklogCount { get; }

    public int BacklogCapacity { get; }

    public int RequiredCapacity { get; }

    public int EntryIndex { get; }

    public TcpListenerBacklogConnection Connection { get; }

    public static TcpPassiveListenerBacklogBridgeResult Create(
        TcpPassiveListenerBacklogBridgeStatus status,
        TcpPassiveIpv4FramePlanResult passiveResult,
        TcpListenerBacklogStatus backlogStatus,
        int backlogCount,
        int backlogCapacity,
        int requiredCapacity,
        int entryIndex,
        TcpListenerBacklogConnection connection)
    {
        return new TcpPassiveListenerBacklogBridgeResult(
            status,
            passiveResult.Status,
            passiveResult.TcpStatus,
            passiveResult.PacketStatus,
            passiveResult.State,
            backlogStatus,
            backlogCount,
            backlogCapacity,
            requiredCapacity,
            entryIndex,
            connection);
    }
}

public static class TcpPassiveListenerBacklogBridge
{
    public const bool RequiresCallerOwnedBacklogStorage = true;
    public const bool ParsesPackets = false;
    public const bool ExportsSocketHandles = false;
    public const bool OwnsTimers = false;
    public const bool RetransmitsSegments = false;
    public const bool UsesVirtioNet = false;

    public static TcpPassiveListenerBacklogBridgeStatus EnqueueEstablished(
        Span<TcpListenerBacklogEntry> backlogEntries,
        TcpPassiveIpv4FramePlanResult passiveResult,
        out TcpPassiveListenerBacklogBridgeResult result)
    {
        if (!passiveResult.Succeeded)
        {
            result = TcpPassiveListenerBacklogBridgeResult.Create(
                TcpPassiveListenerBacklogBridgeStatus.PassiveOpenFailed,
                passiveResult,
                TcpListenerBacklogStatus.Success,
                CountBacklogEntries(backlogEntries),
                backlogEntries.Length,
                requiredCapacity: 1,
                TcpListenerBacklog.NoEntryIndex,
                default);
            return result.Status;
        }

        if (passiveResult.State != TcpPassiveOpenHandshakeState.Established)
        {
            result = TcpPassiveListenerBacklogBridgeResult.Create(
                TcpPassiveListenerBacklogBridgeStatus.NotEstablished,
                passiveResult,
                TcpListenerBacklogStatus.Success,
                CountBacklogEntries(backlogEntries),
                backlogEntries.Length,
                requiredCapacity: 1,
                TcpListenerBacklog.NoEntryIndex,
                default);
            return result.Status;
        }

        TcpListenerBacklogConnection connection = new TcpListenerBacklogConnection(
            passiveResult.LocalAddress,
            passiveResult.LocalPort,
            passiveResult.RemoteAddress,
            passiveResult.RemotePort,
            passiveResult.LocalNextSequenceNumber,
            passiveResult.RemoteNextSequenceNumber);
        TcpListenerBacklogResult backlogResult =
            TcpListenerBacklog.Enqueue(backlogEntries, connection);
        if (backlogResult.Status != TcpListenerBacklogStatus.Success)
        {
            result = TcpPassiveListenerBacklogBridgeResult.Create(
                TcpPassiveListenerBacklogBridgeStatus.BacklogEnqueueFailed,
                passiveResult,
                backlogResult.Status,
                backlogResult.Count,
                backlogResult.Capacity,
                backlogResult.RequiredCapacity,
                backlogResult.EntryIndex,
                backlogResult.Connection);
            return result.Status;
        }

        result = TcpPassiveListenerBacklogBridgeResult.Create(
            TcpPassiveListenerBacklogBridgeStatus.Success,
            passiveResult,
            backlogResult.Status,
            backlogResult.Count,
            backlogResult.Capacity,
            backlogResult.RequiredCapacity,
            backlogResult.EntryIndex,
            backlogResult.Connection);
        return result.Status;
    }

    private static int CountBacklogEntries(ReadOnlySpan<TcpListenerBacklogEntry> backlogEntries)
    {
        TcpListenerBacklogResult inspect = TcpListenerBacklog.Inspect(backlogEntries);
        return inspect.Status == TcpListenerBacklogStatus.Success ? inspect.Count : 0;
    }
}
