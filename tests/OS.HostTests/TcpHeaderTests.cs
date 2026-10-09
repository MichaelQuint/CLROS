using System.Runtime.CompilerServices;
using OS.Net;

internal static class TcpHeaderTests
{

    [HostTest("net.packet.tcp.header-round-trip", HostLane.Fast, 0)]
    private static bool TestHeaderRoundTrip()
    {
        byte[] options = { 0x02, 0x04, 0x05, 0xb4 };
        byte[] payload = { 0x47, 0x45, 0x54 };
        byte[] segment = new byte[TcpHeader.MinHeaderLength + options.Length + payload.Length];

        PacketParseStatus writeStatus = TcpHeader.TryWrite(
            segment,
            sourcePort: 49152,
            destinationPort: 80,
            sequenceNumber: 0x01020304,
            acknowledgementNumber: 0x05060708,
            flags: 0x18,
            windowSize: 0x4000,
            checksum: 0x1234,
            urgentPointer: 0,
            options,
            payload,
            out int bytesWritten);

        PacketParseStatus parseStatus = TcpHeader.TryParse(
            segment,
            out TcpHeader header,
            out ReadOnlySpan<byte> parsedOptions,
            out ReadOnlySpan<byte> parsedPayload);

        return HostCheck.Result(writeStatus == PacketParseStatus.Success
            && bytesWritten == segment.Length
            && parseStatus == PacketParseStatus.Success
            && header.SourcePort == 49152
            && header.DestinationPort == 80
            && header.SequenceNumber == 0x01020304
            && header.AcknowledgementNumber == 0x05060708
            && header.HeaderLength == 24
            && header.Flags == 0x18
            && header.WindowSize == 0x4000
            && header.Checksum == 0x1234
            && header.UrgentPointer == 0
            && parsedOptions.SequenceEqual(options)
            && parsedPayload.SequenceEqual(payload)
            && segment[12] == 0x60);
    }

    [HostTest("net.packet.tcp.rejects-invalid-data-offset", HostLane.Fast, 1)]
    private static bool TestRejectsInvalidDataOffset()
    {
        byte[] segment = new byte[TcpHeader.MinHeaderLength];
        segment[12] = 0x40;

        byte[] options = { 0x01, 0x01 };
        byte[] destination = new byte[TcpHeader.MinHeaderLength + options.Length];

        PacketParseStatus parseStatus = TcpHeader.TryParse(
            segment,
            out TcpHeader _,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> _);
        PacketParseStatus writeStatus = TcpHeader.TryWrite(
            destination,
            1,
            2,
            3,
            4,
            flags: 0x02,
            windowSize: 1024,
            checksum: 0,
            urgentPointer: 0,
            options,
            ReadOnlySpan<byte>.Empty,
            out int bytesWritten);

        return HostCheck.Result(parseStatus == PacketParseStatus.InvalidLength
            && writeStatus == PacketParseStatus.InvalidLength
            && bytesWritten == 0);
    }

    [HostTest("net.packet.tcp.rejects-truncated-options", HostLane.Fast, 2)]
    private static bool TestRejectsTruncatedOptions()
    {
        byte[] segment = new byte[TcpHeader.MinHeaderLength + 2];
        segment[12] = 0x60;

        PacketParseStatus parseStatus = TcpHeader.TryParse(
            segment,
            out TcpHeader header,
            out ReadOnlySpan<byte> options,
            out ReadOnlySpan<byte> payload);

        return HostCheck.Result(parseStatus == PacketParseStatus.TruncatedPayload
            && header.HeaderLength == 0
            && options.Length == 0
            && payload.Length == 0);
    }

    [HostTest("net.tcp header payloads use shared buffer operations", HostLane.Fast, 3)]
    private static bool TestHeaderPayloadsUseSharedBufferOperations()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Net",
            "TcpHeader.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains(
                "BufferOperations.MemoryCopy(options, segment.Slice(MinHeaderLength, options.Length))",
                StringComparison.Ordinal)
            && source.Contains(
                "BufferOperations.MemoryCopy(payload, segment.Slice(headerLength, payload.Length))",
                StringComparison.Ordinal)
            && source.Contains("if (!optionsCopy.Succeeded)", StringComparison.Ordinal)
            && source.Contains("if (!payloadCopy.Succeeded)", StringComparison.Ordinal)
            && !source.Contains(
                "options.CopyTo(segment.Slice(MinHeaderLength, options.Length))",
                StringComparison.Ordinal)
            && !source.Contains(
                "payload.CopyTo(segment.Slice(headerLength, payload.Length))",
                StringComparison.Ordinal));
    }
}
