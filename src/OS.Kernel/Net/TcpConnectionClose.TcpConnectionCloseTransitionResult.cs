namespace OS.Net;

public readonly struct TcpConnectionCloseTransitionResult
{
    private TcpConnectionCloseTransitionResult(TcpConnectionCloseStatus status, TcpConnectionCloseState state, PacketParseStatus packetStatus, int bytesWritten, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        PacketStatus = packetStatus;
        BytesWritten = bytesWritten;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public TcpConnectionCloseStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == TcpConnectionCloseStatus.Success;
    public TcpConnectionCloseState State
    {
        get;
    }
    public PacketParseStatus PacketStatus
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public uint LocalNextSequenceNumber
    {
        get;
    }
    public uint RemoteNextSequenceNumber
    {
        get;
    }

    public static TcpConnectionCloseTransitionResult Success(TcpConnectionCloseState state, PacketParseStatus packetStatus, int bytesWritten, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpConnectionCloseTransitionResult(TcpConnectionCloseStatus.Success, state, packetStatus, bytesWritten, localNextSequenceNumber, remoteNextSequenceNumber);
    }

    public static TcpConnectionCloseTransitionResult Fail(TcpConnectionCloseStatus status, TcpConnectionCloseState state, PacketParseStatus packetStatus, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpConnectionCloseTransitionResult(status, state, packetStatus, bytesWritten: 0, localNextSequenceNumber, remoteNextSequenceNumber);
    }
}
