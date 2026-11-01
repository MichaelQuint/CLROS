namespace OS.Net;
public readonly struct TcpClientHandshakeTransitionResult
{
    private TcpClientHandshakeTransitionResult(TcpClientHandshakeStatus status, TcpClientHandshakeState state, PacketParseStatus packetStatus, int bytesWritten, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        PacketStatus = packetStatus;
        BytesWritten = bytesWritten;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public TcpClientHandshakeStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == TcpClientHandshakeStatus.Success;
    public TcpClientHandshakeState State { get; }
    public PacketParseStatus PacketStatus { get; }
    public int BytesWritten { get; }
    public uint LocalNextSequenceNumber { get; }
    public uint RemoteNextSequenceNumber { get; }

    public static TcpClientHandshakeTransitionResult Success(TcpClientHandshakeState state, PacketParseStatus packetStatus, int bytesWritten, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpClientHandshakeTransitionResult(TcpClientHandshakeStatus.Success, state, packetStatus, bytesWritten, localNextSequenceNumber, remoteNextSequenceNumber);
    }

    public static TcpClientHandshakeTransitionResult Fail(TcpClientHandshakeStatus status, TcpClientHandshakeState state, PacketParseStatus packetStatus, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpClientHandshakeTransitionResult(status, state, packetStatus, 0, localNextSequenceNumber, remoteNextSequenceNumber);
    }
}
