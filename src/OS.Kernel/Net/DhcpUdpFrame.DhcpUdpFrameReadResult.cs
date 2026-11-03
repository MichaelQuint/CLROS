namespace OS.Net;

public readonly struct DhcpUdpFrameReadResult
{
    private DhcpUdpFrameReadResult(DhcpUdpFrameStatus status, DhcpMessageStatus dhcpStatus, PacketParseStatus packetStatus, UdpReceiveDispatchStatus udpStatus, EthernetIpv4UdpPacketInfo packetInfo, UdpReceiveDispatchResult udpResult, DhcpMessage message, int payloadOffset, int payloadLength)
    {
        IsInitialized = true;
        Status = status;
        DhcpStatus = dhcpStatus;
        PacketStatus = packetStatus;
        UdpStatus = udpStatus;
        PacketInfo = packetInfo;
        UdpResult = udpResult;
        Message = message;
        PayloadOffset = payloadOffset;
        PayloadLength = payloadLength;
    }

    public DhcpUdpFrameStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == DhcpUdpFrameStatus.Success;
    public DhcpMessageStatus DhcpStatus
    {
        get;
    }
    public PacketParseStatus PacketStatus
    {
        get;
    }
    public UdpReceiveDispatchStatus UdpStatus
    {
        get;
    }
    public EthernetIpv4UdpPacketInfo PacketInfo
    {
        get;
    }
    public UdpReceiveDispatchResult UdpResult
    {
        get;
    }
    public DhcpMessage Message
    {
        get;
    }
    public int PayloadOffset
    {
        get;
    }
    public int PayloadLength
    {
        get;
    }

    public static DhcpUdpFrameReadResult Success(DhcpMessage message, EthernetIpv4UdpPacketInfo packetInfo, UdpReceiveDispatchResult udpResult, int payloadOffset, int payloadLength)
    {
        return new DhcpUdpFrameReadResult(DhcpUdpFrameStatus.Success, DhcpMessageStatus.Success, PacketParseStatus.Success, UdpReceiveDispatchStatus.Success, packetInfo, udpResult, message, payloadOffset, payloadLength);
    }

    public static DhcpUdpFrameReadResult Fail(DhcpUdpFrameStatus status, DhcpMessageStatus dhcpStatus, PacketParseStatus packetStatus, UdpReceiveDispatchStatus udpStatus, EthernetIpv4UdpPacketInfo packetInfo, UdpReceiveDispatchResult udpResult)
    {
        return new DhcpUdpFrameReadResult(status, dhcpStatus, packetStatus, udpStatus, packetInfo, udpResult, default, 0, 0);
    }
}
