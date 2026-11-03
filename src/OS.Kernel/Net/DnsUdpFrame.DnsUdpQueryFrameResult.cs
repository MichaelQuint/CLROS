namespace OS.Net;

public readonly struct DnsUdpQueryFrameResult
{
    private DnsUdpQueryFrameResult(DnsUdpFrameStatus status, DnsResolverStatus resolverStatus, DnsMessageStatus messageStatus, PacketParseStatus packetStatus, DnsResolverTransitionResult resolverResult, int bytesWritten, int dnsPayloadOffset, int dnsPayloadLength)
    {
        IsInitialized = true;
        Status = status;
        ResolverStatus = resolverStatus;
        MessageStatus = messageStatus;
        PacketStatus = packetStatus;
        ResolverResult = resolverResult;
        BytesWritten = bytesWritten;
        DnsPayloadOffset = dnsPayloadOffset;
        DnsPayloadLength = dnsPayloadLength;
    }

    public DnsUdpFrameStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == DnsUdpFrameStatus.Success;
    public DnsResolverStatus ResolverStatus
    {
        get;
    }
    public DnsMessageStatus MessageStatus
    {
        get;
    }
    public PacketParseStatus PacketStatus
    {
        get;
    }
    public DnsResolverTransitionResult ResolverResult
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int DnsPayloadOffset
    {
        get;
    }
    public int DnsPayloadLength
    {
        get;
    }

    public static DnsUdpQueryFrameResult Success(DnsResolverTransitionResult resolverResult, int bytesWritten, int dnsPayloadOffset, int dnsPayloadLength)
    {
        return new DnsUdpQueryFrameResult(DnsUdpFrameStatus.Success, DnsResolverStatus.Success, DnsMessageStatus.Success, PacketParseStatus.Success, resolverResult, bytesWritten, dnsPayloadOffset, dnsPayloadLength);
    }

    public static DnsUdpQueryFrameResult Fail(DnsUdpFrameStatus status, DnsResolverStatus resolverStatus, DnsMessageStatus messageStatus, PacketParseStatus packetStatus)
    {
        return new DnsUdpQueryFrameResult(status, resolverStatus, messageStatus, packetStatus, default, 0, 0, 0);
    }

    public static DnsUdpQueryFrameResult Fail(DnsUdpFrameStatus status, DnsResolverStatus resolverStatus, DnsMessageStatus messageStatus, DnsResolverTransitionResult resolverResult)
    {
        return new DnsUdpQueryFrameResult(status, resolverStatus, messageStatus, PacketParseStatus.Success, resolverResult, 0, 0, 0);
    }

    public static DnsUdpQueryFrameResult Fail(DnsUdpFrameStatus status, DnsResolverStatus resolverStatus, DnsMessageStatus messageStatus, PacketParseStatus packetStatus, DnsResolverTransitionResult resolverResult)
    {
        return new DnsUdpQueryFrameResult(status, resolverStatus, messageStatus, packetStatus, resolverResult, 0, 0, 0);
    }
}
