namespace OS.Net;

using OS.Drivers;

public enum UdpReceiveDispatchStatus
{
    Success = 0,
    ClassifierFailed = 1,
    Ipv4ParseFailed = 2,
    NonIpv4Payload = 3,
    UnsupportedIpProtocol = 4,
    UdpParseFailed = 5,
    BadUdpChecksum = 6
}

public static class UdpReceiveDispatcher
{
    public static UdpReceiveDispatchResult Dispatch(ReadOnlySpan<byte> receiveBuffer)
    {
        NetworkReceiveDispatchResult networkResult =
            NetworkReceiveDispatcher.Dispatch(receiveBuffer);
        if (!networkResult.Succeeded)
        {
            return UdpReceiveDispatchResult.Fail(
                MapNetworkFailure(networkResult.Status),
                networkResult,
                networkResult.PayloadPacketStatus);
        }

        if (networkResult.PayloadKind != EthernetPayloadKind.Ipv4)
        {
            return UdpReceiveDispatchResult.Fail(
                UdpReceiveDispatchStatus.NonIpv4Payload,
                networkResult,
                PacketParseStatus.UnsupportedEtherType);
        }

        if (networkResult.Ipv4Header.Protocol != Ipv4Header.ProtocolUdp)
        {
            return UdpReceiveDispatchResult.Fail(
                UdpReceiveDispatchStatus.UnsupportedIpProtocol,
                networkResult,
                PacketParseStatus.UnsupportedIpProtocol);
        }

        ReadOnlySpan<byte> ipv4Payload = receiveBuffer.Slice(
            networkResult.Ipv4PayloadOffset,
            networkResult.Ipv4PayloadLength);

        PacketParseStatus udpStatus = UdpHeader.TryParse(
            ipv4Payload,
            out UdpHeader udpHeader,
            out ReadOnlySpan<byte> udpPayload);
        if (udpStatus != PacketParseStatus.Success)
        {
            return UdpReceiveDispatchResult.Fail(
                UdpReceiveDispatchStatus.UdpParseFailed,
                networkResult,
                udpStatus);
        }

        if (udpHeader.Checksum != 0)
        {
            Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
                networkResult.Ipv4Header.Source,
                networkResult.Ipv4Header.Destination,
                Ipv4TransportChecksum.ProtocolUdp,
                ipv4Payload,
                out Ipv4TransportChecksumResult checksumResult);
            if (checksumStatus != Ipv4TransportChecksumStatus.Success || !checksumResult.IsValid)
            {
                return UdpReceiveDispatchResult.Fail(
                    UdpReceiveDispatchStatus.BadUdpChecksum,
                    networkResult,
                    PacketParseStatus.BadChecksum);
            }
        }

        return UdpReceiveDispatchResult.Success(
            networkResult,
            udpHeader,
            networkResult.Ipv4PayloadOffset + UdpHeader.HeaderLength,
            udpPayload.Length);
    }

    private static UdpReceiveDispatchStatus MapNetworkFailure(NetworkReceiveDispatchStatus status)
    {
        return status switch
        {
            NetworkReceiveDispatchStatus.ClassifierFailed =>
                UdpReceiveDispatchStatus.ClassifierFailed,
            NetworkReceiveDispatchStatus.Ipv4ParseFailed =>
                UdpReceiveDispatchStatus.Ipv4ParseFailed,
            NetworkReceiveDispatchStatus.UnsupportedPayload or
                NetworkReceiveDispatchStatus.ArpParseFailed =>
                    UdpReceiveDispatchStatus.NonIpv4Payload,
            _ => UdpReceiveDispatchStatus.ClassifierFailed
        };
    }
}

public readonly struct UdpReceiveDispatchResult
{
    private UdpReceiveDispatchResult(
        UdpReceiveDispatchStatus status,
        NetworkReceiveDispatchResult networkResult,
        PacketParseStatus udpPacketStatus,
        UdpHeader udpHeader,
        int udpPayloadOffset,
        int udpPayloadLength)
    {
        IsInitialized = true;
        Status = status;
        NetworkResult = networkResult;
        UdpPacketStatus = udpPacketStatus;
        UdpHeader = udpHeader;
        UdpPayloadOffset = udpPayloadOffset;
        UdpPayloadLength = udpPayloadLength;
    }

    public UdpReceiveDispatchStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == UdpReceiveDispatchStatus.Success;

    public NetworkReceiveDispatchResult NetworkResult { get; }

    public NetworkReceiveDispatchStatus NetworkStatus => NetworkResult.Status;

    public VirtioNetReceiveFrameClassifierStatus ClassifierStatus =>
        NetworkResult.ClassifierStatus;

    public PacketParseStatus ClassifierPacketStatus =>
        NetworkResult.ClassifierPacketStatus;

    public PacketParseStatus NetworkPacketStatus =>
        NetworkResult.PayloadPacketStatus;

    public PacketParseStatus UdpPacketStatus { get; }

    public EthernetPayloadKind PayloadKind => NetworkResult.PayloadKind;

    public EthernetHeader EthernetHeader => NetworkResult.EthernetHeader;

    public Ipv4Header Ipv4Header => NetworkResult.Ipv4Header;

    public Ipv4Address SourceAddress => NetworkResult.Ipv4Header.Source;

    public Ipv4Address DestinationAddress => NetworkResult.Ipv4Header.Destination;

    public UdpHeader UdpHeader { get; }

    public ushort SourcePort => UdpHeader.SourcePort;

    public ushort DestinationPort => UdpHeader.DestinationPort;

    public int UdpPayloadOffset { get; }

    public int UdpPayloadLength { get; }

    public static UdpReceiveDispatchResult Success(
        NetworkReceiveDispatchResult networkResult,
        UdpHeader udpHeader,
        int udpPayloadOffset,
        int udpPayloadLength)
    {
        return new UdpReceiveDispatchResult(
            UdpReceiveDispatchStatus.Success,
            networkResult,
            PacketParseStatus.Success,
            udpHeader,
            udpPayloadOffset,
            udpPayloadLength);
    }

    public static UdpReceiveDispatchResult Fail(
        UdpReceiveDispatchStatus status,
        NetworkReceiveDispatchResult networkResult,
        PacketParseStatus udpPacketStatus)
    {
        return new UdpReceiveDispatchResult(
            status,
            networkResult,
            udpPacketStatus,
            default,
            0,
            0);
    }
}
