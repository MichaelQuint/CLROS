namespace OS.Net;

using OS.Drivers;

public readonly struct IcmpReceiveDispatchResult
{
    private IcmpReceiveDispatchResult(IcmpReceiveDispatchStatus status, NetworkReceiveDispatchResult networkResult, PacketParseStatus icmpPacketStatus, IcmpEchoPacket echoPacket, int echoPayloadOffset, int echoPayloadLength)
    {
        IsInitialized = true;
        Status = status;
        NetworkResult = networkResult;
        IcmpPacketStatus = icmpPacketStatus;
        EchoPacket = echoPacket;
        EchoPayloadOffset = echoPayloadOffset;
        EchoPayloadLength = echoPayloadLength;
    }

    public IcmpReceiveDispatchStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == IcmpReceiveDispatchStatus.Success;
    public NetworkReceiveDispatchResult NetworkResult
    {
        get;
    }
    public NetworkReceiveDispatchStatus NetworkStatus => NetworkResult.Status;
    public VirtioNetReceiveFrameClassifierStatus ClassifierStatus => NetworkResult.ClassifierStatus;
    public PacketParseStatus ClassifierPacketStatus => NetworkResult.ClassifierPacketStatus;
    public PacketParseStatus NetworkPacketStatus => NetworkResult.PayloadPacketStatus;
    public PacketParseStatus IcmpPacketStatus
    {
        get;
    }
    public EthernetPayloadKind PayloadKind => NetworkResult.PayloadKind;
    public EthernetHeader EthernetHeader => NetworkResult.EthernetHeader;
    public Ipv4Header Ipv4Header => NetworkResult.Ipv4Header;
    public Ipv4Address SourceAddress => NetworkResult.Ipv4Header.Source;
    public Ipv4Address DestinationAddress => NetworkResult.Ipv4Header.Destination;
    public IcmpEchoPacket EchoPacket
    {
        get;
    }
    public int EchoPayloadOffset
    {
        get;
    }
    public int EchoPayloadLength
    {
        get;
    }

    public static IcmpReceiveDispatchResult Success(NetworkReceiveDispatchResult networkResult, IcmpEchoPacket echoPacket, int echoPayloadOffset, int echoPayloadLength)
    {
        return new IcmpReceiveDispatchResult(IcmpReceiveDispatchStatus.Success, networkResult, PacketParseStatus.Success, echoPacket, echoPayloadOffset, echoPayloadLength);
    }

    public static IcmpReceiveDispatchResult Fail(IcmpReceiveDispatchStatus status, NetworkReceiveDispatchResult networkResult, PacketParseStatus icmpPacketStatus)
    {
        return new IcmpReceiveDispatchResult(status, networkResult, icmpPacketStatus, default, 0, 0);
    }
}
