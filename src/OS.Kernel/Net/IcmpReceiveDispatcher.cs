namespace OS.Net;

using OS.Drivers;

public enum IcmpReceiveDispatchStatus
{
    Success = 0,
    ClassifierFailed = 1,
    Ipv4ParseFailed = 2,
    NonIpv4Payload = 3,
    UnsupportedIpProtocol = 4,
    IcmpParseFailed = 5
}

public static class IcmpReceiveDispatcher
{
    public static IcmpReceiveDispatchResult Dispatch(ReadOnlySpan<byte> receiveBuffer)
    {
        NetworkReceiveDispatchResult networkResult =
            NetworkReceiveDispatcher.Dispatch(receiveBuffer);
        if (!networkResult.Succeeded)
        {
            return IcmpReceiveDispatchResult.Fail(
                MapNetworkFailure(networkResult.Status),
                networkResult,
                networkResult.PayloadPacketStatus);
        }

        if (networkResult.PayloadKind != EthernetPayloadKind.Ipv4)
        {
            return IcmpReceiveDispatchResult.Fail(
                IcmpReceiveDispatchStatus.NonIpv4Payload,
                networkResult,
                PacketParseStatus.UnsupportedEtherType);
        }

        if (networkResult.Ipv4Header.Protocol != Ipv4Header.ProtocolIcmp)
        {
            return IcmpReceiveDispatchResult.Fail(
                IcmpReceiveDispatchStatus.UnsupportedIpProtocol,
                networkResult,
                PacketParseStatus.UnsupportedIpProtocol);
        }

        ReadOnlySpan<byte> ipv4Payload = receiveBuffer.Slice(
            networkResult.Ipv4PayloadOffset,
            networkResult.Ipv4PayloadLength);

        PacketParseStatus icmpStatus = IcmpEchoPacket.TryParse(
            ipv4Payload,
            out IcmpEchoPacket echoPacket,
            out ReadOnlySpan<byte> echoPayload);
        if (icmpStatus != PacketParseStatus.Success)
        {
            return IcmpReceiveDispatchResult.Fail(
                IcmpReceiveDispatchStatus.IcmpParseFailed,
                networkResult,
                icmpStatus);
        }

        return IcmpReceiveDispatchResult.Success(
            networkResult,
            echoPacket,
            networkResult.Ipv4PayloadOffset + IcmpEchoPacket.HeaderLength,
            echoPayload.Length);
    }

    private static IcmpReceiveDispatchStatus MapNetworkFailure(NetworkReceiveDispatchStatus status)
    {
        return status switch
        {
            NetworkReceiveDispatchStatus.ClassifierFailed =>
                IcmpReceiveDispatchStatus.ClassifierFailed,
            NetworkReceiveDispatchStatus.Ipv4ParseFailed =>
                IcmpReceiveDispatchStatus.Ipv4ParseFailed,
            NetworkReceiveDispatchStatus.UnsupportedPayload or
                NetworkReceiveDispatchStatus.ArpParseFailed =>
                    IcmpReceiveDispatchStatus.NonIpv4Payload,
            _ => IcmpReceiveDispatchStatus.ClassifierFailed
        };
    }
}

public readonly struct IcmpReceiveDispatchResult
{
    private IcmpReceiveDispatchResult(
        IcmpReceiveDispatchStatus status,
        NetworkReceiveDispatchResult networkResult,
        PacketParseStatus icmpPacketStatus,
        IcmpEchoPacket echoPacket,
        int echoPayloadOffset,
        int echoPayloadLength)
    {
        IsInitialized = true;
        Status = status;
        NetworkResult = networkResult;
        IcmpPacketStatus = icmpPacketStatus;
        EchoPacket = echoPacket;
        EchoPayloadOffset = echoPayloadOffset;
        EchoPayloadLength = echoPayloadLength;
    }

    public IcmpReceiveDispatchStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == IcmpReceiveDispatchStatus.Success;

    public NetworkReceiveDispatchResult NetworkResult { get; }

    public NetworkReceiveDispatchStatus NetworkStatus => NetworkResult.Status;

    public VirtioNetReceiveFrameClassifierStatus ClassifierStatus =>
        NetworkResult.ClassifierStatus;

    public PacketParseStatus ClassifierPacketStatus =>
        NetworkResult.ClassifierPacketStatus;

    public PacketParseStatus NetworkPacketStatus =>
        NetworkResult.PayloadPacketStatus;

    public PacketParseStatus IcmpPacketStatus { get; }

    public EthernetPayloadKind PayloadKind => NetworkResult.PayloadKind;

    public EthernetHeader EthernetHeader => NetworkResult.EthernetHeader;

    public Ipv4Header Ipv4Header => NetworkResult.Ipv4Header;

    public Ipv4Address SourceAddress => NetworkResult.Ipv4Header.Source;

    public Ipv4Address DestinationAddress => NetworkResult.Ipv4Header.Destination;

    public IcmpEchoPacket EchoPacket { get; }

    public int EchoPayloadOffset { get; }

    public int EchoPayloadLength { get; }

    public static IcmpReceiveDispatchResult Success(
        NetworkReceiveDispatchResult networkResult,
        IcmpEchoPacket echoPacket,
        int echoPayloadOffset,
        int echoPayloadLength)
    {
        return new IcmpReceiveDispatchResult(
            IcmpReceiveDispatchStatus.Success,
            networkResult,
            PacketParseStatus.Success,
            echoPacket,
            echoPayloadOffset,
            echoPayloadLength);
    }

    public static IcmpReceiveDispatchResult Fail(
        IcmpReceiveDispatchStatus status,
        NetworkReceiveDispatchResult networkResult,
        PacketParseStatus icmpPacketStatus)
    {
        return new IcmpReceiveDispatchResult(
            status,
            networkResult,
            icmpPacketStatus,
            default,
            0,
            0);
    }
}
