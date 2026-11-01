namespace OS.Net;
public readonly struct DnsUdpReceiveFrameResult
{
    private DnsUdpReceiveFrameResult(DnsUdpFrameStatus status, UdpReceiveDispatchResult udpResult, PacketParseStatus packetStatus, DnsResolverStatus resolverStatus, DnsMessageStatus messageStatus, DnsResolverTransitionResult resolverResult, bool resolverInvoked)
    {
        IsInitialized = true;
        Status = status;
        UdpResult = udpResult;
        PacketStatus = packetStatus;
        ResolverStatus = resolverStatus;
        MessageStatus = messageStatus;
        ResolverResult = resolverResult;
        ResolverInvoked = resolverInvoked;
    }

    public DnsUdpFrameStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == DnsUdpFrameStatus.Success;
    public UdpReceiveDispatchResult UdpResult { get; }
    public UdpReceiveDispatchStatus UdpStatus => UdpResult.Status;
    public PacketParseStatus PacketStatus { get; }
    public PacketParseStatus UdpPacketStatus => UdpResult.UdpPacketStatus;
    public DnsResolverStatus ResolverStatus { get; }
    public DnsMessageStatus MessageStatus { get; }
    public DnsResolverTransitionResult ResolverResult { get; }
    public bool ResolverInvoked { get; }
    public ushort SourcePort => UdpResult.SourcePort;
    public ushort DestinationPort => UdpResult.DestinationPort;
    public int DnsPayloadOffset => UdpResult.UdpPayloadOffset;
    public int DnsPayloadLength => UdpResult.UdpPayloadLength;
    public Ipv4Address ResolvedAddress => ResolverResult.Address;

    public static DnsUdpReceiveFrameResult Success(UdpReceiveDispatchResult udpResult, DnsResolverTransitionResult resolverResult)
    {
        return new DnsUdpReceiveFrameResult(DnsUdpFrameStatus.Success, udpResult, PacketParseStatus.Success, DnsResolverStatus.Success, DnsMessageStatus.Success, resolverResult, resolverInvoked: true);
    }

    public static DnsUdpReceiveFrameResult Fail(DnsUdpFrameStatus status, UdpReceiveDispatchResult udpResult, PacketParseStatus packetStatus)
    {
        return new DnsUdpReceiveFrameResult(status, udpResult, packetStatus, DnsResolverStatus.Success, DnsMessageStatus.Success, default, resolverInvoked: false);
    }

    public static DnsUdpReceiveFrameResult Fail(DnsUdpFrameStatus status, UdpReceiveDispatchResult udpResult, DnsResolverStatus resolverStatus, DnsMessageStatus messageStatus, DnsResolverTransitionResult resolverResult)
    {
        return new DnsUdpReceiveFrameResult(status, udpResult, PacketParseStatus.Success, resolverStatus, messageStatus, resolverResult, resolverInvoked: true);
    }
}
