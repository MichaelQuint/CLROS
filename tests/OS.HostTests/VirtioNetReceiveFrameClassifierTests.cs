using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class VirtioNetReceiveFrameClassifierTests
{
    private const ushort UnsupportedEtherType = 0x86DD;

    [HostTest("drivers.virtionet.rx classifies arp frame", HostLane.Fast, 0)]
    private static bool TestClassifiesArpFrame()
    {
        byte[] frame = CreateReceiveBuffer(EthernetHeader.EtherTypeArp, payloadBytes: 28);

        VirtioNetReceiveFrameClassificationResult result =
            VirtioNetReceiveFrameClassifier.Classify(frame);

        return HostCheck.Result(result.Succeeded
            && result.Status == VirtioNetReceiveFrameClassifierStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Arp
            && result.EthernetHeader.EtherType == EthernetHeader.EtherTypeArp
            && result.EthernetHeader.Destination == DestinationMac
            && result.EthernetHeader.Source == SourceMac
            && result.VirtioMetadata.VirtioHeaderLength == VirtioNetHeader.HeaderLengthWithoutMergeableBuffers
            && result.VirtioMetadata.EthernetFrameOffset == VirtioNetHeader.HeaderLengthWithoutMergeableBuffers
            && result.VirtioMetadata.EthernetPayloadLength == 28
            && result.PayloadOffset == ExpectedPayloadOffset
            && result.PayloadLength == 28);
    }

    [HostTest("drivers.virtionet.rx classifies ipv4 frame", HostLane.Fast, 1)]
    private static bool TestClassifiesIpv4Frame()
    {
        byte[] frame = CreateReceiveBuffer(EthernetHeader.EtherTypeIpv4, payloadBytes: 20);

        VirtioNetReceiveFrameClassificationResult result =
            VirtioNetReceiveFrameClassifier.Classify(frame);

        return HostCheck.Result(result.Succeeded
            && result.Status == VirtioNetReceiveFrameClassifierStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Ipv4
            && result.EthernetHeader.EtherType == EthernetHeader.EtherTypeIpv4
            && result.PayloadOffset == ExpectedPayloadOffset
            && result.PayloadLength == 20);
    }

    [HostTest("drivers.virtionet.rx rejects truncated virtio header", HostLane.Fast, 2)]
    private static bool TestRejectsTruncatedVirtioHeader()
    {
        byte[] frame = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1];

        VirtioNetReceiveFrameClassificationResult result =
            VirtioNetReceiveFrameClassifier.Classify(frame);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioNetReceiveFrameClassifierStatus.BufferTooSmall
            && result.PacketStatus == PacketParseStatus.BufferTooSmall
            && result.PayloadKind == EthernetPayloadKind.Unsupported
            && result.PayloadOffset == 0
            && result.PayloadLength == 0);
    }

    [HostTest("drivers.virtionet.rx rejects unsupported offload", HostLane.Fast, 3)]
    private static bool TestRejectsUnsupportedOffload()
    {
        byte[] frame = CreateReceiveBuffer(EthernetHeader.EtherTypeIpv4, payloadBytes: 0);
        frame[0] = VirtioNetHeader.FlagNeedsChecksum;
        EndianPrimitives.WriteUInt16LittleEndian(frame.AsSpan(6, sizeof(ushort)), 34);
        EndianPrimitives.WriteUInt16LittleEndian(frame.AsSpan(8, sizeof(ushort)), 16);

        VirtioNetReceiveFrameClassificationResult result =
            VirtioNetReceiveFrameClassifier.Classify(frame);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioNetReceiveFrameClassifierStatus.UnsupportedChecksumOffload
            && result.PacketStatus == PacketParseStatus.UnsupportedOperation
            && result.PayloadKind == EthernetPayloadKind.Unsupported
            && result.PayloadOffset == 0
            && result.PayloadLength == 0);
    }

    [HostTest("drivers.virtionet.rx rejects missing ethernet header", HostLane.Fast, 4)]
    private static bool TestRejectsMissingEthernetHeader()
    {
        byte[] frame =
            new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength - 1];

        VirtioNetReceiveFrameClassificationResult result =
            VirtioNetReceiveFrameClassifier.Classify(frame);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioNetReceiveFrameClassifierStatus.MissingEthernetHeader
            && result.PacketStatus == PacketParseStatus.BufferTooSmall
            && result.PayloadKind == EthernetPayloadKind.Unsupported
            && result.PayloadOffset == 0
            && result.PayloadLength == 0);
    }

    [HostTest("drivers.virtionet.rx reports unsupported payload", HostLane.Fast, 5)]
    private static bool TestReportsUnsupportedPayload()
    {
        byte[] frame = CreateReceiveBuffer(UnsupportedEtherType, payloadBytes: 8);

        VirtioNetReceiveFrameClassificationResult result =
            VirtioNetReceiveFrameClassifier.Classify(frame);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioNetReceiveFrameClassifierStatus.UnsupportedPayload
            && result.PacketStatus == PacketParseStatus.UnsupportedEtherType
            && result.PayloadKind == EthernetPayloadKind.Unsupported
            && result.EthernetHeader.EtherType == UnsupportedEtherType
            && result.VirtioMetadata.EthernetFrameLength == EthernetHeader.HeaderLength + 8
            && result.PayloadOffset == ExpectedPayloadOffset
            && result.PayloadLength == 8);
    }

    [HostTest("drivers.virtionet.rx classifier default result is not success", HostLane.Fast, 6)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        VirtioNetReceiveFrameClassificationResult result = default;

        return HostCheck.Result(!result.IsInitialized
            && !result.Succeeded
            && result.Status == VirtioNetReceiveFrameClassifierStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.PayloadKind == EthernetPayloadKind.Unsupported
            && result.PayloadOffset == 0
            && result.PayloadLength == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                VirtioNetReceiveFrameClassificationResult>());
    }

    private static byte[] CreateReceiveBuffer(ushort etherType, int payloadBytes)
    {
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            payloadBytes];

        EthernetHeader ethernet = new EthernetHeader(
            DestinationMac,
            SourceMac,
            etherType);

        PacketParseStatus status = EthernetHeader.TryWrite(
            buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            ethernet);
        if (status != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic Ethernet frame.");
        }

        for (int index = 0; index < payloadBytes; index++)
        {
            buffer[ExpectedPayloadOffset + index] = (byte)(0x50 + index);
        }

        return buffer;
    }

    private static int ExpectedPayloadOffset =>
        VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength;

    private static MacAddress DestinationMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x22);

    private static MacAddress SourceMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x11);
}
