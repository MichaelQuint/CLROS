namespace OS.Drivers;

using OS.Net;

public enum VirtioNetReceiveFrameClassifierStatus
{
    Success = 0,
    BufferTooSmall = 1,
    MissingEthernetHeader = 2,
    UnsupportedChecksumOffload = 3,
    UnsupportedOffloadFlags = 4,
    UnsupportedGenericSegmentationOffload = 5,
    UnsupportedPayload = 6
}

public static class VirtioNetReceiveFrameClassifier
{
    public static VirtioNetReceiveFrameClassificationResult Classify(
        ReadOnlySpan<byte> receiveBuffer)
    {
        VirtioNetReceivedFrameParseResult virtioResult =
            VirtioNetHeader.TryParseReceivedFrameNoOffload(receiveBuffer);
        if (!virtioResult.Succeeded)
        {
            VirtioNetReceiveFrameClassifierStatus status =
                MapVirtioStatus(virtioResult.Status);
            return VirtioNetReceiveFrameClassificationResult.Fail(
                status,
                MapVirtioPacketStatus(virtioResult.Status));
        }

        VirtioNetReceivedFrameMetadata metadata = virtioResult.Metadata;
        ReadOnlySpan<byte> ethernetFrame = receiveBuffer.Slice(
            metadata.EthernetFrameOffset,
            metadata.EthernetFrameLength);

        PacketParseStatus packetStatus = EthernetHeader.TryDispatch(
            ethernetFrame,
            out EthernetHeader ethernetHeader,
            out EthernetPayloadKind payloadKind,
            out ReadOnlySpan<byte> payload);

        if (packetStatus != PacketParseStatus.Success)
        {
            VirtioNetReceiveFrameClassifierStatus status =
                packetStatus == PacketParseStatus.UnsupportedEtherType
                    ? VirtioNetReceiveFrameClassifierStatus.UnsupportedPayload
                    : VirtioNetReceiveFrameClassifierStatus.MissingEthernetHeader;

            return VirtioNetReceiveFrameClassificationResult.Create(
                status,
                packetStatus,
                metadata,
                ethernetHeader,
                payloadKind,
                metadata.EthernetPayloadOffset,
                payload.Length);
        }

        return VirtioNetReceiveFrameClassificationResult.Create(
            VirtioNetReceiveFrameClassifierStatus.Success,
            PacketParseStatus.Success,
            metadata,
            ethernetHeader,
            payloadKind,
            metadata.EthernetPayloadOffset,
            payload.Length);
    }

    private static VirtioNetReceiveFrameClassifierStatus MapVirtioStatus(
        VirtioNetHeaderStatus status)
    {
        return status switch
        {
            VirtioNetHeaderStatus.Success =>
                VirtioNetReceiveFrameClassifierStatus.Success,
            VirtioNetHeaderStatus.BufferTooSmall =>
                VirtioNetReceiveFrameClassifierStatus.BufferTooSmall,
            VirtioNetHeaderStatus.MissingEthernetHeader =>
                VirtioNetReceiveFrameClassifierStatus.MissingEthernetHeader,
            VirtioNetHeaderStatus.UnsupportedChecksumOffload =>
                VirtioNetReceiveFrameClassifierStatus.UnsupportedChecksumOffload,
            VirtioNetHeaderStatus.UnsupportedOffloadFlags =>
                VirtioNetReceiveFrameClassifierStatus.UnsupportedOffloadFlags,
            VirtioNetHeaderStatus.UnsupportedGenericSegmentationOffload =>
                VirtioNetReceiveFrameClassifierStatus.UnsupportedGenericSegmentationOffload,
            _ => VirtioNetReceiveFrameClassifierStatus.MissingEthernetHeader
        };
    }

    private static PacketParseStatus MapVirtioPacketStatus(
        VirtioNetHeaderStatus status)
    {
        return status switch
        {
            VirtioNetHeaderStatus.BufferTooSmall or
                VirtioNetHeaderStatus.MissingEthernetHeader =>
                    PacketParseStatus.BufferTooSmall,
            VirtioNetHeaderStatus.UnsupportedChecksumOffload or
                VirtioNetHeaderStatus.UnsupportedOffloadFlags or
                VirtioNetHeaderStatus.UnsupportedGenericSegmentationOffload =>
                    PacketParseStatus.UnsupportedOperation,
            _ => PacketParseStatus.Success
        };
    }
}

public readonly struct VirtioNetReceiveFrameClassificationResult
{
    private VirtioNetReceiveFrameClassificationResult(
        VirtioNetReceiveFrameClassifierStatus status,
        PacketParseStatus packetStatus,
        VirtioNetReceivedFrameMetadata virtioMetadata,
        EthernetHeader ethernetHeader,
        EthernetPayloadKind payloadKind,
        int payloadOffset,
        int payloadLength)
    {
        IsInitialized = true;
        Status = status;
        PacketStatus = packetStatus;
        VirtioMetadata = virtioMetadata;
        EthernetHeader = ethernetHeader;
        PayloadKind = payloadKind;
        PayloadOffset = payloadOffset;
        PayloadLength = payloadLength;
    }

    public VirtioNetReceiveFrameClassifierStatus Status { get; }

    public bool IsInitialized { get; }

    public PacketParseStatus PacketStatus { get; }

    public VirtioNetReceivedFrameMetadata VirtioMetadata { get; }

    public EthernetHeader EthernetHeader { get; }

    public EthernetPayloadKind PayloadKind { get; }

    public int PayloadOffset { get; }

    public int PayloadLength { get; }

    public bool Succeeded =>
        IsInitialized && Status == VirtioNetReceiveFrameClassifierStatus.Success;

    public static VirtioNetReceiveFrameClassificationResult Create(
        VirtioNetReceiveFrameClassifierStatus status,
        PacketParseStatus packetStatus,
        VirtioNetReceivedFrameMetadata virtioMetadata,
        EthernetHeader ethernetHeader,
        EthernetPayloadKind payloadKind,
        int payloadOffset,
        int payloadLength)
    {
        return new VirtioNetReceiveFrameClassificationResult(
            status,
            packetStatus,
            virtioMetadata,
            ethernetHeader,
            payloadKind,
            payloadOffset,
            payloadLength);
    }

    public static VirtioNetReceiveFrameClassificationResult Fail(
        VirtioNetReceiveFrameClassifierStatus status,
        PacketParseStatus packetStatus)
    {
        return new VirtioNetReceiveFrameClassificationResult(
            status,
            packetStatus,
            default,
            default,
            EthernetPayloadKind.Unsupported,
            0,
            0);
    }
}
