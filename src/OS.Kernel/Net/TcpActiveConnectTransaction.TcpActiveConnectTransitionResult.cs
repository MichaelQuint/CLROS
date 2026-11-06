namespace OS.Net;

public readonly struct TcpActiveConnectTransitionResult
{
    private TcpActiveConnectTransitionResult(TcpActiveConnectStatus status, TcpActiveConnectState state, PacketParseStatus packetStatus, int bytesWritten, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        PacketStatus = packetStatus;
        BytesWritten = bytesWritten;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public TcpActiveConnectStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == TcpActiveConnectStatus.Success;
    public TcpActiveConnectState State
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

    public static TcpActiveConnectTransitionResult Success(TcpActiveConnectState state, PacketParseStatus packetStatus, int bytesWritten, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpActiveConnectTransitionResult(TcpActiveConnectStatus.Success, state, packetStatus, bytesWritten, localNextSequenceNumber, remoteNextSequenceNumber);
    }

    public static TcpActiveConnectTransitionResult Fail(TcpActiveConnectStatus status, TcpActiveConnectState state, PacketParseStatus packetStatus, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpActiveConnectTransitionResult(status, state, packetStatus, 0, localNextSequenceNumber, remoteNextSequenceNumber);
    }
}
