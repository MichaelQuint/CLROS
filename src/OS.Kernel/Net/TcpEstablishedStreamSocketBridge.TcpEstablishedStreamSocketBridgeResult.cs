using OS.Pal;

namespace OS.Net;
public readonly struct TcpEstablishedStreamSocketBridgeResult
{
    private TcpEstablishedStreamSocketBridgeResult(TcpEstablishedStreamSocketBridgeStatus status, TcpEstablishedStreamStatus receiveStatus, OSHostStatus socketStatus, OSHostSocketHandle socket, int payloadBytesAccepted, int payloadBytesQueued, int unreadBytes, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        IsInitialized = true;
        Status = status;
        ReceiveStatus = receiveStatus;
        SocketStatus = socketStatus;
        Socket = socket;
        PayloadBytesAccepted = payloadBytesAccepted;
        PayloadBytesQueued = payloadBytesQueued;
        UnreadBytes = unreadBytes;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public TcpEstablishedStreamSocketBridgeStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == TcpEstablishedStreamSocketBridgeStatus.Success;
    public TcpEstablishedStreamStatus ReceiveStatus { get; }
    public OSHostStatus SocketStatus { get; }
    public OSHostSocketHandle Socket { get; }
    public int PayloadBytesAccepted { get; }
    public int PayloadBytesQueued { get; }
    public int UnreadBytes { get; }
    public uint LocalNextSequenceNumber { get; }
    public uint RemoteNextSequenceNumber { get; }

    public static TcpEstablishedStreamSocketBridgeResult Create(TcpEstablishedStreamSocketBridgeStatus status, TcpEstablishedStreamStatus receiveStatus, OSHostStatus socketStatus, OSHostSocketHandle socket, int payloadBytesAccepted, int payloadBytesQueued, int unreadBytes, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpEstablishedStreamSocketBridgeResult(status, receiveStatus, socketStatus, socket, payloadBytesAccepted, payloadBytesQueued, unreadBytes, localNextSequenceNumber, remoteNextSequenceNumber);
    }
}
