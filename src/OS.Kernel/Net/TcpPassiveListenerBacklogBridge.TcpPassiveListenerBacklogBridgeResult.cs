namespace OS.Net;
public readonly struct TcpPassiveListenerBacklogBridgeResult
{
    private TcpPassiveListenerBacklogBridgeResult(TcpPassiveListenerBacklogBridgeStatus status, TcpPassiveIpv4FramePlanStatus passiveStatus, TcpPassiveOpenHandshakeStatus tcpStatus, PacketParseStatus packetStatus, TcpPassiveOpenHandshakeState passiveState, TcpListenerBacklogStatus backlogStatus, int backlogCount, int backlogCapacity, int requiredCapacity, int entryIndex, TcpListenerBacklogConnection connection)
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

    public static TcpPassiveListenerBacklogBridgeResult Create(TcpPassiveListenerBacklogBridgeStatus status, TcpPassiveIpv4FramePlanResult passiveResult, TcpListenerBacklogStatus backlogStatus, int backlogCount, int backlogCapacity, int requiredCapacity, int entryIndex, TcpListenerBacklogConnection connection)
    {
        return new TcpPassiveListenerBacklogBridgeResult(status, passiveResult.Status, passiveResult.TcpStatus, passiveResult.PacketStatus, passiveResult.State, backlogStatus, backlogCount, backlogCapacity, requiredCapacity, entryIndex, connection);
    }
}
