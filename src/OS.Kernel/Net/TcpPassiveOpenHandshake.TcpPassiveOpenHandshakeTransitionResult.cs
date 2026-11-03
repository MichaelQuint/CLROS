namespace OS.Net;

public readonly struct TcpPassiveOpenHandshakeTransitionResult
{
    private TcpPassiveOpenHandshakeTransitionResult(TcpPassiveOpenHandshakeStatus status, TcpPassiveOpenHandshakeState state, PacketParseStatus packetStatus, int bytesWritten, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        PacketStatus = packetStatus;
        BytesWritten = bytesWritten;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public TcpPassiveOpenHandshakeStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == TcpPassiveOpenHandshakeStatus.Success;
    public TcpPassiveOpenHandshakeState State
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

    public static TcpPassiveOpenHandshakeTransitionResult Success(TcpPassiveOpenHandshakeState state, PacketParseStatus packetStatus, int bytesWritten, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpPassiveOpenHandshakeTransitionResult(TcpPassiveOpenHandshakeStatus.Success, state, packetStatus, bytesWritten, localNextSequenceNumber, remoteNextSequenceNumber);
    }

    public static TcpPassiveOpenHandshakeTransitionResult Fail(TcpPassiveOpenHandshakeStatus status, TcpPassiveOpenHandshakeState state, PacketParseStatus packetStatus, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpPassiveOpenHandshakeTransitionResult(status, state, packetStatus, 0, localNextSequenceNumber, remoteNextSequenceNumber);
    }
}
