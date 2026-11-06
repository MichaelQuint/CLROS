namespace OS.Net;

using OS.Drivers;

public readonly struct UdpReceiveDispatchResult
{
    private UdpReceiveDispatchResult(UdpReceiveDispatchStatus status, NetworkReceiveDispatchResult networkResult, PacketParseStatus udpPacketStatus, UdpHeader udpHeader, int udpPayloadOffset, int udpPayloadLength)
    {
        IsInitialized = true;
        Status = status;
        NetworkResult = networkResult;
        UdpPacketStatus = udpPacketStatus;
        UdpHeader = udpHeader;
        UdpPayloadOffset = udpPayloadOffset;
        UdpPayloadLength = udpPayloadLength;
    }

    public UdpReceiveDispatchStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == UdpReceiveDispatchStatus.Success;
    public NetworkReceiveDispatchResult NetworkResult
    {
        get;
    }
    public NetworkReceiveDispatchStatus NetworkStatus => NetworkResult.Status;
    public VirtioNetReceiveFrameClassifierStatus ClassifierStatus => NetworkResult.ClassifierStatus;
    public PacketParseStatus ClassifierPacketStatus => NetworkResult.ClassifierPacketStatus;
    public PacketParseStatus NetworkPacketStatus => NetworkResult.PayloadPacketStatus;
    public PacketParseStatus UdpPacketStatus
    {
        get;
    }
    public EthernetPayloadKind PayloadKind => NetworkResult.PayloadKind;
    public EthernetHeader EthernetHeader => NetworkResult.EthernetHeader;
    public Ipv4Header Ipv4Header => NetworkResult.Ipv4Header;
    public Ipv4Address SourceAddress => NetworkResult.Ipv4Header.Source;
    public Ipv4Address DestinationAddress => NetworkResult.Ipv4Header.Destination;
    public UdpHeader UdpHeader
    {
        get;
    }
    public ushort SourcePort => UdpHeader.SourcePort;
    public ushort DestinationPort => UdpHeader.DestinationPort;
    public int UdpPayloadOffset
    {
        get;
    }
    public int UdpPayloadLength
    {
        get;
    }

    public static UdpReceiveDispatchResult Success(NetworkReceiveDispatchResult networkResult, UdpHeader udpHeader, int udpPayloadOffset, int udpPayloadLength)
    {
        return new UdpReceiveDispatchResult(UdpReceiveDispatchStatus.Success, networkResult, PacketParseStatus.Success, udpHeader, udpPayloadOffset, udpPayloadLength);
    }

    public static UdpReceiveDispatchResult Fail(UdpReceiveDispatchStatus status, NetworkReceiveDispatchResult networkResult, PacketParseStatus udpPacketStatus)
    {
        return new UdpReceiveDispatchResult(status, networkResult, udpPacketStatus, default, 0, 0);
    }
}
