using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class VirtioNetHeaderTests
{

    [HostTest("drivers.virtionet.header writes zero tx header", HostLane.Fast, 0)]
    private static bool TestWritesZeroedTxHeader()
    {
        byte[] header = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + 2];
        Array.Fill(header, (byte)0xCC);

        VirtioNetHeaderStatus status =
            VirtioNetHeader.TryWriteNoOffloadTxHeader(header, out int bytesWritten);
        VirtioNetHeaderParseResult parseResult =
            VirtioNetHeader.TryParse(header.AsSpan(0, bytesWritten));

        bool zeroed = true;
        for (int index = 0; index < bytesWritten; index++)
        {
            zeroed &= header[index] == 0;
        }

        return HostCheck.Result(status == VirtioNetHeaderStatus.Success
            && bytesWritten == VirtioNetHeader.HeaderLengthWithoutMergeableBuffers
            && zeroed
            && header[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers] == 0xCC
            && header[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + 1] == 0xCC
            && parseResult.Succeeded
            && parseResult.IsInitialized
            && parseResult.Header.ValidateNoOffload() == VirtioNetHeaderStatus.Success);
    }

    [HostTest("drivers.virtionet.header parses rx frame metadata", HostLane.Fast, 1)]
    private static bool TestParsesRxFrameMetadata()
    {
        byte[] receiveBuffer = CreateReceivedFrame(payloadBytes: 4);

        VirtioNetReceivedFrameParseResult result =
            VirtioNetHeader.TryParseReceivedFrameNoOffload(receiveBuffer);

        VirtioNetReceivedFrameMetadata metadata = result.Metadata;
        return HostCheck.Result(result.Succeeded
            && result.IsInitialized
            && result.Status == VirtioNetHeaderStatus.Success
            && result.Header.Flags == 0
            && result.Header.GsoType == VirtioNetHeader.GsoTypeNone
            && result.Header.HeaderLength == 0
            && result.Header.GsoSize == 0
            && result.Header.ChecksumStart == 0
            && result.Header.ChecksumOffset == 0
            && metadata.VirtioHeaderLength == VirtioNetHeader.HeaderLengthWithoutMergeableBuffers
            && metadata.EthernetFrameOffset == VirtioNetHeader.HeaderLengthWithoutMergeableBuffers
            && metadata.EthernetFrameLength == EthernetHeader.HeaderLength + 4
            && metadata.EthernetPayloadOffset ==
                VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength
            && metadata.EthernetPayloadLength == 4
            && metadata.EthernetHeader.Destination == DestinationMac
            && metadata.EthernetHeader.Source == SourceMac
            && metadata.EthernetHeader.EtherType == EthernetHeader.EtherTypeIpv4);
    }

    [HostTest("drivers.virtionet.header rejects truncated header", HostLane.Fast, 2)]
    private static bool TestRejectsTruncatedHeader()
    {
        byte[] truncated = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers - 1];

        VirtioNetHeaderParseResult headerResult = VirtioNetHeader.TryParse(truncated);
        VirtioNetReceivedFrameParseResult frameResult =
            VirtioNetHeader.TryParseReceivedFrameNoOffload(truncated);

        return HostCheck.Result(!headerResult.Succeeded
            && headerResult.Status == VirtioNetHeaderStatus.BufferTooSmall
            && headerResult.BytesConsumed == truncated.Length
            && !frameResult.Succeeded
            && frameResult.Status == VirtioNetHeaderStatus.BufferTooSmall);
    }

    [HostTest("drivers.virtionet.header rejects missing ethernet header", HostLane.Fast, 3)]
    private static bool TestRejectsMissingEthernetHeader()
    {
        byte[] missingEthernet =
            new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength - 1];

        VirtioNetReceivedFrameParseResult result =
            VirtioNetHeader.TryParseReceivedFrameNoOffload(missingEthernet);

        return HostCheck.Result(!result.Succeeded
            && result.Status == VirtioNetHeaderStatus.MissingEthernetHeader);
    }

    [HostTest("drivers.virtionet.header rejects offload and gso", HostLane.Fast, 4)]
    private static bool TestRejectsOffloadAndGso()
    {
        byte[] header = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers];
        header[0] = VirtioNetHeader.FlagNeedsChecksum;
        header[1] = VirtioNetHeader.GsoTypeTcpV4;
        EndianPrimitives.WriteUInt16LittleEndian(header.AsSpan(2, sizeof(ushort)), 54);
        EndianPrimitives.WriteUInt16LittleEndian(header.AsSpan(4, sizeof(ushort)), 1460);
        EndianPrimitives.WriteUInt16LittleEndian(header.AsSpan(6, sizeof(ushort)), 34);
        EndianPrimitives.WriteUInt16LittleEndian(header.AsSpan(8, sizeof(ushort)), 16);

        VirtioNetHeaderParseResult parsed = VirtioNetHeader.TryParse(header);
        if (!parsed.Succeeded ||
            parsed.Header.Flags != VirtioNetHeader.FlagNeedsChecksum ||
            parsed.Header.GsoType != VirtioNetHeader.GsoTypeTcpV4 ||
            parsed.Header.HeaderLength != 54 ||
            parsed.Header.GsoSize != 1460 ||
            parsed.Header.ChecksumStart != 34 ||
            parsed.Header.ChecksumOffset != 16 ||
            parsed.Header.ValidateNoOffload() != VirtioNetHeaderStatus.UnsupportedChecksumOffload)
        {
            return HostCheck.Result(false);
        }

        byte[] checksumOffload = CreateReceivedFrame(payloadBytes: 0);
        checksumOffload[0] = VirtioNetHeader.FlagNeedsChecksum;
        EndianPrimitives.WriteUInt16LittleEndian(checksumOffload.AsSpan(6, sizeof(ushort)), 34);
        EndianPrimitives.WriteUInt16LittleEndian(checksumOffload.AsSpan(8, sizeof(ushort)), 16);
        VirtioNetReceivedFrameParseResult checksumResult =
            VirtioNetHeader.TryParseReceivedFrameNoOffload(checksumOffload);

        byte[] gso = CreateReceivedFrame(payloadBytes: 0);
        gso[1] = VirtioNetHeader.GsoTypeTcpV4;
        EndianPrimitives.WriteUInt16LittleEndian(gso.AsSpan(2, sizeof(ushort)), 54);
        EndianPrimitives.WriteUInt16LittleEndian(gso.AsSpan(4, sizeof(ushort)), 1460);
        VirtioNetReceivedFrameParseResult gsoResult =
            VirtioNetHeader.TryParseReceivedFrameNoOffload(gso);

        byte[] offloadFlags = CreateReceivedFrame(payloadBytes: 0);
        offloadFlags[0] = VirtioNetHeader.FlagDataValid;
        VirtioNetReceivedFrameParseResult offloadFlagsResult =
            VirtioNetHeader.TryParseReceivedFrameNoOffload(offloadFlags);

        return HostCheck.Result(!checksumResult.Succeeded
            && checksumResult.Status == VirtioNetHeaderStatus.UnsupportedChecksumOffload
            && !gsoResult.Succeeded
            && gsoResult.Status == VirtioNetHeaderStatus.UnsupportedGenericSegmentationOffload
            && !offloadFlagsResult.Succeeded
            && offloadFlagsResult.Status == VirtioNetHeaderStatus.UnsupportedOffloadFlags);
    }

    [HostTest("drivers.virtionet.header default results are not success", HostLane.Fast, 5)]
    private static bool TestDefaultResultsAreNotSuccess()
    {
        VirtioNetHeaderParseResult header = default;
        VirtioNetReceivedFrameParseResult frame = default;

        return HostCheck.Result(!header.IsInitialized
            && !header.Succeeded
            && header.Status == VirtioNetHeaderStatus.Success
            && header.BytesConsumed == 0
            && !frame.IsInitialized
            && !frame.Succeeded
            && frame.Status == VirtioNetHeaderStatus.Success
            && frame.Metadata.VirtioHeaderLength == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetHeaderParseResult>()
            && !RuntimeHelpers.IsReferenceOrContainsReferences<VirtioNetReceivedFrameParseResult>());
    }

    private static byte[] CreateReceivedFrame(int payloadBytes)
    {
        byte[] buffer = new byte[
            VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
            EthernetHeader.HeaderLength +
            payloadBytes];
        EthernetHeader ethernet = new EthernetHeader(
            DestinationMac,
            SourceMac,
            EthernetHeader.EtherTypeIpv4);

        PacketParseStatus status = EthernetHeader.TryWrite(
            buffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers),
            ethernet);
        if (status != PacketParseStatus.Success)
        {
            throw new InvalidOperationException("Failed to build deterministic Ethernet header.");
        }

        for (int index = 0; index < payloadBytes; index++)
        {
            buffer[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers +
                EthernetHeader.HeaderLength +
                index] = (byte)(0xA0 + index);
        }

        return buffer;
    }

    private static MacAddress DestinationMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x02);

    private static MacAddress SourceMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x01);
}
