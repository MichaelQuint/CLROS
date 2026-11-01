namespace OS.Net;
using OS.Drivers;
using OS.Std.Binary;

public readonly struct TcpReceiveDispatchResult
{
    private TcpReceiveDispatchResult(TcpReceiveDispatchStatus status, TcpReceiveDispatchAction action, NetworkReceiveDispatchResult networkResult, PacketParseStatus tcpPacketStatus, TcpEstablishedStreamStatus streamStatus, TcpConnectionCloseStatus closeStatus, TcpConnectionCloseState closeState, Ipv4TransportChecksumStatus checksumStatus, TcpHeader tcpHeader, int tcpSegmentOffset, int tcpSegmentLength, int payloadBytes, int ackBytesWritten, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        IsInitialized = true;
        Status = status;
        Action = action;
        NetworkResult = networkResult;
        TcpPacketStatus = tcpPacketStatus;
        StreamStatus = streamStatus;
        CloseStatus = closeStatus;
        CloseState = closeState;
        ChecksumStatus = checksumStatus;
        TcpHeader = tcpHeader;
        TcpSegmentOffset = tcpSegmentOffset;
        TcpSegmentLength = tcpSegmentLength;
        PayloadBytes = payloadBytes;
        AckBytesWritten = ackBytesWritten;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public bool IsInitialized { get; }
    public TcpReceiveDispatchStatus Status { get; }
    public bool Succeeded => IsInitialized && Status == TcpReceiveDispatchStatus.Success;
    public TcpReceiveDispatchAction Action { get; }
    public NetworkReceiveDispatchResult NetworkResult { get; }
    public NetworkReceiveDispatchStatus NetworkStatus => NetworkResult.Status;
    public VirtioNetReceiveFrameClassifierStatus ClassifierStatus => NetworkResult.ClassifierStatus;
    public PacketParseStatus ClassifierPacketStatus => NetworkResult.ClassifierPacketStatus;
    public PacketParseStatus NetworkPacketStatus => NetworkResult.PayloadPacketStatus;
    public PacketParseStatus TcpPacketStatus { get; }
    public TcpEstablishedStreamStatus StreamStatus { get; }
    public TcpConnectionCloseStatus CloseStatus { get; }
    public TcpConnectionCloseState CloseState { get; }
    public Ipv4TransportChecksumStatus ChecksumStatus { get; }
    public EthernetPayloadKind PayloadKind => NetworkResult.PayloadKind;
    public Ipv4Header Ipv4Header => NetworkResult.Ipv4Header;
    public TcpHeader TcpHeader { get; }
    public int TcpSegmentOffset { get; }
    public int TcpSegmentLength { get; }
    public int PayloadBytes { get; }
    public int AckBytesWritten { get; }
    public uint LocalNextSequenceNumber { get; }
    public uint RemoteNextSequenceNumber { get; }

    public static TcpReceiveDispatchResult Success(TcpReceiveDispatchAction action, NetworkReceiveDispatchResult networkResult, TcpHeader tcpHeader, int tcpSegmentOffset, int tcpSegmentLength, int payloadBytes, int ackBytesWritten, TcpConnectionCloseState closeState, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpReceiveDispatchResult(TcpReceiveDispatchStatus.Success, action, networkResult, PacketParseStatus.Success, TcpEstablishedStreamStatus.Success, TcpConnectionCloseStatus.Success, closeState, Ipv4TransportChecksumStatus.Success, tcpHeader, tcpSegmentOffset, tcpSegmentLength, payloadBytes, ackBytesWritten, localNextSequenceNumber, remoteNextSequenceNumber);
    }

    public static TcpReceiveDispatchResult Fail(TcpReceiveDispatchStatus status, NetworkReceiveDispatchResult networkResult, PacketParseStatus tcpPacketStatus, TcpEstablishedStreamStatus streamStatus, TcpConnectionCloseStatus closeStatus, TcpConnectionCloseState closeState, Ipv4TransportChecksumStatus checksumStatus, TcpHeader tcpHeader, int tcpSegmentOffset, int tcpSegmentLength, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new TcpReceiveDispatchResult(status, TcpReceiveDispatchAction.None, networkResult, tcpPacketStatus, streamStatus, closeStatus, closeState, checksumStatus, tcpHeader, tcpSegmentOffset, tcpSegmentLength, payloadBytes: 0, ackBytesWritten: 0, localNextSequenceNumber, remoteNextSequenceNumber);
    }
}
