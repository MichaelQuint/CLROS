namespace OS.Net;

public enum DnsUdpFrameStatus
{
    Success = 0,
    BufferTooSmall = 1,
    ResolverWriteFailed = 2,
    PacketWriteFailed = 3,
    UdpDispatchFailed = 4,
    WrongUdpPorts = 5,
    ResolverParseFailed = 6
}

public readonly struct DnsUdpQueryFrameInfo
{
    public DnsUdpQueryFrameInfo(
        MacAddress localMac,
        MacAddress destinationMac,
        Ipv4Address localAddress,
        Ipv4Address dnsServerAddress,
        ushort sourcePort,
        ushort identification = 0)
    {
        LocalMac = localMac;
        DestinationMac = destinationMac;
        LocalAddress = localAddress;
        DnsServerAddress = dnsServerAddress;
        SourcePort = sourcePort;
        Identification = identification;
    }

    public MacAddress LocalMac { get; }

    public MacAddress DestinationMac { get; }

    public Ipv4Address LocalAddress { get; }

    public Ipv4Address DnsServerAddress { get; }

    public ushort SourcePort { get; }

    public ushort Identification { get; }
}

public static class DnsUdpFrame
{
    public const ushort DnsServerPort = 53;
    public const int QueryFrameHeaderLength =
        EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + UdpHeader.HeaderLength;

    public static DnsUdpQueryFrameResult TryWriteQueryFrame(
        Span<byte> frame,
        in DnsUdpQueryFrameInfo info,
        ReadOnlySpan<char> hostName,
        ref DnsResolverTransaction transaction)
    {
        if (frame.Length < QueryFrameHeaderLength)
        {
            return DnsUdpQueryFrameResult.Fail(
                DnsUdpFrameStatus.BufferTooSmall,
                DnsResolverStatus.BufferTooSmall,
                DnsMessageStatus.BufferTooSmall,
                PacketParseStatus.BufferTooSmall);
        }

        DnsResolverTransaction working = transaction;
        DnsResolverStatus resolverStatus = working.TryWriteQuery(
            frame.Slice(QueryFrameHeaderLength),
            hostName,
            out DnsResolverTransitionResult resolverResult);
        if (resolverStatus != DnsResolverStatus.Success)
        {
            return DnsUdpQueryFrameResult.Fail(
                DnsUdpFrameStatus.ResolverWriteFailed,
                resolverStatus,
                resolverResult.MessageStatus,
                resolverResult);
        }

        EthernetIpv4UdpPacketInfo packetInfo = new EthernetIpv4UdpPacketInfo(
            info.DestinationMac,
            info.LocalMac,
            info.LocalAddress,
            info.DnsServerAddress,
            info.SourcePort,
            DnsServerPort,
            timeToLive: 64,
            identification: info.Identification);

        ReadOnlySpan<byte> dnsPayload = frame.Slice(
            QueryFrameHeaderLength,
            resolverResult.BytesWritten);
        PacketParseStatus packetStatus = EthernetIpv4UdpPacket.TryWrite(
            frame,
            packetInfo,
            dnsPayload,
            out int bytesWritten);
        if (packetStatus != PacketParseStatus.Success)
        {
            return DnsUdpQueryFrameResult.Fail(
                DnsUdpFrameStatus.PacketWriteFailed,
                resolverStatus,
                resolverResult.MessageStatus,
                packetStatus,
                resolverResult);
        }

        transaction = working;
        return DnsUdpQueryFrameResult.Success(
            resolverResult,
            bytesWritten,
            QueryFrameHeaderLength,
            resolverResult.BytesWritten);
    }

    public static DnsUdpReceiveFrameResult ReceiveResponse(
        ReadOnlySpan<byte> receiveBuffer,
        ushort expectedDnsServerPort,
        ushort expectedClientPort,
        ref DnsResolverTransaction transaction)
    {
        UdpReceiveDispatchResult udpResult = UdpReceiveDispatcher.Dispatch(receiveBuffer);
        if (!udpResult.Succeeded)
        {
            return DnsUdpReceiveFrameResult.Fail(
                DnsUdpFrameStatus.UdpDispatchFailed,
                udpResult,
                udpResult.UdpPacketStatus);
        }

        if (udpResult.SourcePort != expectedDnsServerPort
            || udpResult.DestinationPort != expectedClientPort)
        {
            return DnsUdpReceiveFrameResult.Fail(
                DnsUdpFrameStatus.WrongUdpPorts,
                udpResult,
                PacketParseStatus.Success);
        }

        ReadOnlySpan<byte> dnsPayload = receiveBuffer.Slice(
            udpResult.UdpPayloadOffset,
            udpResult.UdpPayloadLength);
        DnsResolverStatus resolverStatus = transaction.ReceiveResponse(
            dnsPayload,
            out DnsResolverTransitionResult resolverResult);
        if (resolverStatus != DnsResolverStatus.Success)
        {
            return DnsUdpReceiveFrameResult.Fail(
                DnsUdpFrameStatus.ResolverParseFailed,
                udpResult,
                resolverStatus,
                resolverResult.MessageStatus,
                resolverResult);
        }

        return DnsUdpReceiveFrameResult.Success(
            udpResult,
            resolverResult);
    }
}

public readonly struct DnsUdpQueryFrameResult
{
    private DnsUdpQueryFrameResult(
        DnsUdpFrameStatus status,
        DnsResolverStatus resolverStatus,
        DnsMessageStatus messageStatus,
        PacketParseStatus packetStatus,
        DnsResolverTransitionResult resolverResult,
        int bytesWritten,
        int dnsPayloadOffset,
        int dnsPayloadLength)
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

    public DnsUdpFrameStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == DnsUdpFrameStatus.Success;

    public DnsResolverStatus ResolverStatus { get; }

    public DnsMessageStatus MessageStatus { get; }

    public PacketParseStatus PacketStatus { get; }

    public DnsResolverTransitionResult ResolverResult { get; }

    public int BytesWritten { get; }

    public int DnsPayloadOffset { get; }

    public int DnsPayloadLength { get; }

    public static DnsUdpQueryFrameResult Success(
        DnsResolverTransitionResult resolverResult,
        int bytesWritten,
        int dnsPayloadOffset,
        int dnsPayloadLength)
    {
        return new DnsUdpQueryFrameResult(
            DnsUdpFrameStatus.Success,
            DnsResolverStatus.Success,
            DnsMessageStatus.Success,
            PacketParseStatus.Success,
            resolverResult,
            bytesWritten,
            dnsPayloadOffset,
            dnsPayloadLength);
    }

    public static DnsUdpQueryFrameResult Fail(
        DnsUdpFrameStatus status,
        DnsResolverStatus resolverStatus,
        DnsMessageStatus messageStatus,
        PacketParseStatus packetStatus)
    {
        return new DnsUdpQueryFrameResult(
            status,
            resolverStatus,
            messageStatus,
            packetStatus,
            default,
            0,
            0,
            0);
    }

    public static DnsUdpQueryFrameResult Fail(
        DnsUdpFrameStatus status,
        DnsResolverStatus resolverStatus,
        DnsMessageStatus messageStatus,
        DnsResolverTransitionResult resolverResult)
    {
        return new DnsUdpQueryFrameResult(
            status,
            resolverStatus,
            messageStatus,
            PacketParseStatus.Success,
            resolverResult,
            0,
            0,
            0);
    }

    public static DnsUdpQueryFrameResult Fail(
        DnsUdpFrameStatus status,
        DnsResolverStatus resolverStatus,
        DnsMessageStatus messageStatus,
        PacketParseStatus packetStatus,
        DnsResolverTransitionResult resolverResult)
    {
        return new DnsUdpQueryFrameResult(
            status,
            resolverStatus,
            messageStatus,
            packetStatus,
            resolverResult,
            0,
            0,
            0);
    }
}

public readonly struct DnsUdpReceiveFrameResult
{
    private DnsUdpReceiveFrameResult(
        DnsUdpFrameStatus status,
        UdpReceiveDispatchResult udpResult,
        PacketParseStatus packetStatus,
        DnsResolverStatus resolverStatus,
        DnsMessageStatus messageStatus,
        DnsResolverTransitionResult resolverResult,
        bool resolverInvoked)
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

    public static DnsUdpReceiveFrameResult Success(
        UdpReceiveDispatchResult udpResult,
        DnsResolverTransitionResult resolverResult)
    {
        return new DnsUdpReceiveFrameResult(
            DnsUdpFrameStatus.Success,
            udpResult,
            PacketParseStatus.Success,
            DnsResolverStatus.Success,
            DnsMessageStatus.Success,
            resolverResult,
            resolverInvoked: true);
    }

    public static DnsUdpReceiveFrameResult Fail(
        DnsUdpFrameStatus status,
        UdpReceiveDispatchResult udpResult,
        PacketParseStatus packetStatus)
    {
        return new DnsUdpReceiveFrameResult(
            status,
            udpResult,
            packetStatus,
            DnsResolverStatus.Success,
            DnsMessageStatus.Success,
            default,
            resolverInvoked: false);
    }

    public static DnsUdpReceiveFrameResult Fail(
        DnsUdpFrameStatus status,
        UdpReceiveDispatchResult udpResult,
        DnsResolverStatus resolverStatus,
        DnsMessageStatus messageStatus,
        DnsResolverTransitionResult resolverResult)
    {
        return new DnsUdpReceiveFrameResult(
            status,
            udpResult,
            PacketParseStatus.Success,
            resolverStatus,
            messageStatus,
            resolverResult,
            resolverInvoked: true);
    }
}
