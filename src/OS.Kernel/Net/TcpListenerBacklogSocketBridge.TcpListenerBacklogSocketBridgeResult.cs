using OS.Pal;

namespace OS.Net;
public readonly struct TcpListenerBacklogSocketBridgeResult
{
    private TcpListenerBacklogSocketBridgeResult(TcpListenerBacklogSocketBridgeStatus status, TcpListenerBacklogStatus backlogStatus, OSHostStatus socketStatus, int backlogCount, int backlogCapacity, int backlogEntryIndex, TcpListenerBacklogConnection connection, OSHostIpv4Endpoint localEndpoint, OSHostIpv4Endpoint remoteEndpoint)
    {
        Status = status;
        BacklogStatus = backlogStatus;
        SocketStatus = socketStatus;
        BacklogCount = backlogCount;
        BacklogCapacity = backlogCapacity;
        BacklogEntryIndex = backlogEntryIndex;
        Connection = connection;
        LocalEndpoint = localEndpoint;
        RemoteEndpoint = remoteEndpoint;
        IsInitialized = true;
    }

    public TcpListenerBacklogSocketBridgeStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == TcpListenerBacklogSocketBridgeStatus.Success;
    public TcpListenerBacklogStatus BacklogStatus { get; }
    public OSHostStatus SocketStatus { get; }
    public int BacklogCount { get; }
    public int BacklogCapacity { get; }
    public int BacklogEntryIndex { get; }
    public TcpListenerBacklogConnection Connection { get; }
    public OSHostIpv4Endpoint LocalEndpoint { get; }
    public OSHostIpv4Endpoint RemoteEndpoint { get; }

    public static TcpListenerBacklogSocketBridgeResult Create(TcpListenerBacklogSocketBridgeStatus status, TcpListenerBacklogStatus backlogStatus, OSHostStatus socketStatus, int backlogCount, int backlogCapacity, int backlogEntryIndex, TcpListenerBacklogConnection connection, OSHostIpv4Endpoint localEndpoint, OSHostIpv4Endpoint remoteEndpoint)
    {
        return new TcpListenerBacklogSocketBridgeResult(status, backlogStatus, socketStatus, backlogCount, backlogCapacity, backlogEntryIndex, connection, localEndpoint, remoteEndpoint);
    }
}
