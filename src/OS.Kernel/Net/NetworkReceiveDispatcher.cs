namespace OS.Net;

using OS.Drivers;

public enum NetworkReceiveDispatchStatus
{
    Success = 0,
    ClassifierFailed = 1,
    ArpParseFailed = 2,
    Ipv4ParseFailed = 3,
    UnsupportedPayload = 4
}

public static class NetworkReceiveDispatcher
{
    public static NetworkReceiveDispatchResult Dispatch(ReadOnlySpan<byte> receiveBuffer)
    {
        VirtioNetReceiveFrameClassificationResult classifierResult =
            VirtioNetReceiveFrameClassifier.Classify(receiveBuffer);
        if (!classifierResult.Succeeded)
        {
            NetworkReceiveDispatchStatus status =
                classifierResult.Status == VirtioNetReceiveFrameClassifierStatus.UnsupportedPayload
                    ? NetworkReceiveDispatchStatus.UnsupportedPayload
                    : NetworkReceiveDispatchStatus.ClassifierFailed;

            return NetworkReceiveDispatchResult.Fail(
                status,
                classifierResult,
                classifierResult.PacketStatus);
        }

        ReadOnlySpan<byte> payload = receiveBuffer.Slice(
            classifierResult.PayloadOffset,
            classifierResult.PayloadLength);

        if (classifierResult.PayloadKind == EthernetPayloadKind.Arp)
        {
            PacketParseStatus arpStatus = ArpPacket.TryParse(
                payload,
                out ArpPacket arpPacket);
            if (arpStatus != PacketParseStatus.Success)
            {
                return NetworkReceiveDispatchResult.Fail(
                    NetworkReceiveDispatchStatus.ArpParseFailed,
                    classifierResult,
                    arpStatus);
            }

            return NetworkReceiveDispatchResult.Arp(
                classifierResult,
                arpPacket);
        }

        if (classifierResult.PayloadKind == EthernetPayloadKind.Ipv4)
        {
            PacketParseStatus ipv4Status = Ipv4Header.TryParse(
                payload,
                out Ipv4Header ipv4Header,
                out ReadOnlySpan<byte> ipv4Payload);
            if (ipv4Status != PacketParseStatus.Success)
            {
                return NetworkReceiveDispatchResult.Fail(
                    NetworkReceiveDispatchStatus.Ipv4ParseFailed,
                    classifierResult,
                    ipv4Status);
            }

            return NetworkReceiveDispatchResult.Ipv4(
                classifierResult,
                ipv4Header,
                classifierResult.PayloadOffset + ipv4Header.HeaderLength,
                ipv4Payload.Length);
        }

        return NetworkReceiveDispatchResult.Fail(
            NetworkReceiveDispatchStatus.UnsupportedPayload,
            classifierResult,
            classifierResult.PacketStatus);
    }
}

public readonly struct NetworkReceiveDispatchResult
{
    private NetworkReceiveDispatchResult(
        NetworkReceiveDispatchStatus status,
        VirtioNetReceiveFrameClassificationResult classifierResult,
        PacketParseStatus payloadPacketStatus,
        ArpPacket arpPacket,
        Ipv4Header ipv4Header,
        int ipv4PayloadOffset,
        int ipv4PayloadLength)
    {
        IsInitialized = true;
        Status = status;
        ClassifierResult = classifierResult;
        PayloadPacketStatus = payloadPacketStatus;
        ArpPacket = arpPacket;
        Ipv4Header = ipv4Header;
        Ipv4PayloadOffset = ipv4PayloadOffset;
        Ipv4PayloadLength = ipv4PayloadLength;
    }

    public NetworkReceiveDispatchStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == NetworkReceiveDispatchStatus.Success;

    public VirtioNetReceiveFrameClassificationResult ClassifierResult { get; }

    public VirtioNetReceiveFrameClassifierStatus ClassifierStatus =>
        ClassifierResult.Status;

    public PacketParseStatus ClassifierPacketStatus =>
        ClassifierResult.PacketStatus;

    public PacketParseStatus PayloadPacketStatus { get; }

    public EthernetHeader EthernetHeader => ClassifierResult.EthernetHeader;

    public EthernetPayloadKind PayloadKind => ClassifierResult.PayloadKind;

    public int PayloadOffset => ClassifierResult.PayloadOffset;

    public int PayloadLength => ClassifierResult.PayloadLength;

    public ArpPacket ArpPacket { get; }

    public Ipv4Header Ipv4Header { get; }

    public int Ipv4PayloadOffset { get; }

    public int Ipv4PayloadLength { get; }

    public static NetworkReceiveDispatchResult Arp(
        VirtioNetReceiveFrameClassificationResult classifierResult,
        ArpPacket arpPacket)
    {
        return new NetworkReceiveDispatchResult(
            NetworkReceiveDispatchStatus.Success,
            classifierResult,
            PacketParseStatus.Success,
            arpPacket,
            default,
            0,
            0);
    }

    public static NetworkReceiveDispatchResult Ipv4(
        VirtioNetReceiveFrameClassificationResult classifierResult,
        Ipv4Header ipv4Header,
        int ipv4PayloadOffset,
        int ipv4PayloadLength)
    {
        return new NetworkReceiveDispatchResult(
            NetworkReceiveDispatchStatus.Success,
            classifierResult,
            PacketParseStatus.Success,
            default,
            ipv4Header,
            ipv4PayloadOffset,
            ipv4PayloadLength);
    }

    public static NetworkReceiveDispatchResult Fail(
        NetworkReceiveDispatchStatus status,
        VirtioNetReceiveFrameClassificationResult classifierResult,
        PacketParseStatus payloadPacketStatus)
    {
        return new NetworkReceiveDispatchResult(
            status,
            classifierResult,
            payloadPacketStatus,
            default,
            default,
            0,
            0);
    }
}
