namespace OS.Net;
public readonly struct TcpEstablishedStreamReceiveResult
{
    private TcpEstablishedStreamReceiveResult(TcpEstablishedStreamStatus status, PacketParseStatus packetStatus, int payloadBytesAccepted, int unreadBytes, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        IsInitialized = true;
        Status = status;
        PacketStatus = packetStatus;
        PayloadBytesAccepted = payloadBytesAccepted;
        UnreadBytes = unreadBytes;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public bool IsInitialized { get; }
    public TcpEstablishedStreamStatus Status { get; }
    public bool Succeeded => IsInitialized && Status == TcpEstablishedStreamStatus.Success;
    public PacketParseStatus PacketStatus { get; }
    public int PayloadBytesAccepted { get; }
    public int UnreadBytes { get; }
    public uint LocalNextSequenceNumber { get; }
    public uint RemoteNextSequenceNumber { get; }

    public static TcpEstablishedStreamReceiveResult Success(int payloadBytesAccepted, int unreadBytes, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpEstablishedStreamReceiveResult(TcpEstablishedStreamStatus.Success, PacketParseStatus.Success, payloadBytesAccepted, unreadBytes, localNextSequenceNumber, remoteNextSequenceNumber);
    }

    public static TcpEstablishedStreamReceiveResult Fail(TcpEstablishedStreamStatus status, PacketParseStatus packetStatus, int unreadBytes, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpEstablishedStreamReceiveResult(status, packetStatus, payloadBytesAccepted: 0, unreadBytes, localNextSequenceNumber, remoteNextSequenceNumber);
    }
}
