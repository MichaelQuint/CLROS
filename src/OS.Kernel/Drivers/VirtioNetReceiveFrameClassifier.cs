namespace OS.Drivers;
using OS.Net;

public static class VirtioNetReceiveFrameClassifier
{
    public static VirtioNetReceiveFrameClassificationResult Classify(ReadOnlySpan<byte> receiveBuffer) => ClassifyParsed(receiveBuffer, VirtioNetHeader.TryParseReceivedFrameNoOffload(receiveBuffer));
    public static VirtioNetReceiveFrameClassificationResult ClassifyModern(ReadOnlySpan<byte> receiveBuffer) => ClassifyParsed(receiveBuffer, VirtioNetHeader.TryParseModernReceivedFrameNoOffload(receiveBuffer));
    private static VirtioNetReceiveFrameClassificationResult ClassifyParsed(ReadOnlySpan<byte> receiveBuffer, VirtioNetReceivedFrameParseResult virtioResult)
    {
        if (!virtioResult.Succeeded)
        {
            VirtioNetReceiveFrameClassifierStatus status = MapVirtioStatus(virtioResult.Status);
            return VirtioNetReceiveFrameClassificationResult.Fail(status, MapVirtioPacketStatus(virtioResult.Status));
        }

        VirtioNetReceivedFrameMetadata metadata = virtioResult.Metadata;
        ReadOnlySpan<byte> ethernetFrame = receiveBuffer.Slice(metadata.EthernetFrameOffset, metadata.EthernetFrameLength);
        PacketParseStatus packetStatus = EthernetHeader.TryDispatch(ethernetFrame, out EthernetHeader ethernetHeader, out EthernetPayloadKind payloadKind, out ReadOnlySpan<byte> payload);
        if (packetStatus != PacketParseStatus.Success)
        {
            VirtioNetReceiveFrameClassifierStatus status = packetStatus == PacketParseStatus.UnsupportedEtherType ? VirtioNetReceiveFrameClassifierStatus.UnsupportedPayload : VirtioNetReceiveFrameClassifierStatus.MissingEthernetHeader;
            return VirtioNetReceiveFrameClassificationResult.Create(status, packetStatus, metadata, ethernetHeader, payloadKind, metadata.EthernetPayloadOffset, payload.Length);
        }

        return VirtioNetReceiveFrameClassificationResult.Create(VirtioNetReceiveFrameClassifierStatus.Success, PacketParseStatus.Success, metadata, ethernetHeader, payloadKind, metadata.EthernetPayloadOffset, payload.Length);
    }

    private static VirtioNetReceiveFrameClassifierStatus MapVirtioStatus(VirtioNetHeaderStatus status)
    {
        return status switch
        {
            VirtioNetHeaderStatus.Success => VirtioNetReceiveFrameClassifierStatus.Success,
            VirtioNetHeaderStatus.BufferTooSmall => VirtioNetReceiveFrameClassifierStatus.BufferTooSmall,
            VirtioNetHeaderStatus.MissingEthernetHeader => VirtioNetReceiveFrameClassifierStatus.MissingEthernetHeader,
            VirtioNetHeaderStatus.UnsupportedChecksumOffload => VirtioNetReceiveFrameClassifierStatus.UnsupportedChecksumOffload,
            VirtioNetHeaderStatus.UnsupportedOffloadFlags => VirtioNetReceiveFrameClassifierStatus.UnsupportedOffloadFlags,
            VirtioNetHeaderStatus.UnsupportedGenericSegmentationOffload => VirtioNetReceiveFrameClassifierStatus.UnsupportedGenericSegmentationOffload,
            VirtioNetHeaderStatus.UnsupportedReceiveBufferCount => VirtioNetReceiveFrameClassifierStatus.UnsupportedReceiveBufferCount,
            _ => VirtioNetReceiveFrameClassifierStatus.MissingEthernetHeader
        };
    }

    private static PacketParseStatus MapVirtioPacketStatus(VirtioNetHeaderStatus status)
    {
        return status switch
        {
            VirtioNetHeaderStatus.BufferTooSmall or VirtioNetHeaderStatus.MissingEthernetHeader => PacketParseStatus.BufferTooSmall,
            VirtioNetHeaderStatus.UnsupportedChecksumOffload or VirtioNetHeaderStatus.UnsupportedOffloadFlags or VirtioNetHeaderStatus.UnsupportedGenericSegmentationOffload or VirtioNetHeaderStatus.UnsupportedReceiveBufferCount => PacketParseStatus.UnsupportedOperation,
            _ => PacketParseStatus.Success
        };
    }
}
