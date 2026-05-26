namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public readonly struct TcpHeader
{
    public const int MinHeaderLength = 20;
    public const int MaxHeaderLength = 60;

    public TcpHeader(
        ushort sourcePort,
        ushort destinationPort,
        uint sequenceNumber,
        uint acknowledgementNumber,
        byte headerLength,
        byte flags,
        ushort windowSize,
        ushort checksum,
        ushort urgentPointer)
    {
        SourcePort = sourcePort;
        DestinationPort = destinationPort;
        SequenceNumber = sequenceNumber;
        AcknowledgementNumber = acknowledgementNumber;
        HeaderLength = headerLength;
        Flags = flags;
        WindowSize = windowSize;
        Checksum = checksum;
        UrgentPointer = urgentPointer;
    }

    public ushort SourcePort { get; }

    public ushort DestinationPort { get; }

    public uint SequenceNumber { get; }

    public uint AcknowledgementNumber { get; }

    public byte HeaderLength { get; }

    public byte Flags { get; }

    public ushort WindowSize { get; }

    public ushort Checksum { get; }

    public ushort UrgentPointer { get; }

    public static PacketParseStatus TryParse(
        ReadOnlySpan<byte> segment,
        out TcpHeader header,
        out ReadOnlySpan<byte> options,
        out ReadOnlySpan<byte> payload)
    {
        if (segment.Length < MinHeaderLength)
        {
            header = default;
            options = default;
            payload = default;
            return PacketParseStatus.BufferTooSmall;
        }

        int headerLength = (segment[12] >> 4) * 4;
        if (headerLength < MinHeaderLength || headerLength > MaxHeaderLength)
        {
            header = default;
            options = default;
            payload = default;
            return PacketParseStatus.InvalidLength;
        }

        if (segment.Length < headerLength)
        {
            header = default;
            options = default;
            payload = default;
            return PacketParseStatus.TruncatedPayload;
        }

        header = new TcpHeader(
            EndianPrimitives.ReadUInt16BigEndian(segment.Slice(0, 2)),
            EndianPrimitives.ReadUInt16BigEndian(segment.Slice(2, 2)),
            EndianPrimitives.ReadUInt32BigEndian(segment.Slice(4, 4)),
            EndianPrimitives.ReadUInt32BigEndian(segment.Slice(8, 4)),
            (byte)headerLength,
            segment[13],
            EndianPrimitives.ReadUInt16BigEndian(segment.Slice(14, 2)),
            EndianPrimitives.ReadUInt16BigEndian(segment.Slice(16, 2)),
            EndianPrimitives.ReadUInt16BigEndian(segment.Slice(18, 2)));
        options = segment.Slice(MinHeaderLength, headerLength - MinHeaderLength);
        payload = segment.Slice(headerLength);
        return PacketParseStatus.Success;
    }

    public static PacketParseStatus TryWrite(
        Span<byte> segment,
        ushort sourcePort,
        ushort destinationPort,
        uint sequenceNumber,
        uint acknowledgementNumber,
        byte flags,
        ushort windowSize,
        ushort checksum,
        ushort urgentPointer,
        ReadOnlySpan<byte> options,
        ReadOnlySpan<byte> payload,
        out int bytesWritten)
    {
        bytesWritten = 0;

        if ((options.Length % 4) != 0 || options.Length > MaxHeaderLength - MinHeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        int headerLength = MinHeaderLength + options.Length;
        int segmentLength = headerLength + payload.Length;
        if (segment.Length < segmentLength)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        EndianPrimitives.WriteUInt16BigEndian(segment.Slice(0, 2), sourcePort);
        EndianPrimitives.WriteUInt16BigEndian(segment.Slice(2, 2), destinationPort);
        EndianPrimitives.WriteUInt32BigEndian(segment.Slice(4, 4), sequenceNumber);
        EndianPrimitives.WriteUInt32BigEndian(segment.Slice(8, 4), acknowledgementNumber);
        segment[12] = (byte)((headerLength / 4) << 4);
        segment[13] = flags;
        EndianPrimitives.WriteUInt16BigEndian(segment.Slice(14, 2), windowSize);
        EndianPrimitives.WriteUInt16BigEndian(segment.Slice(16, 2), checksum);
        EndianPrimitives.WriteUInt16BigEndian(segment.Slice(18, 2), urgentPointer);
        BufferOperationResult optionsCopy =
            BufferOperations.MemoryCopy(options, segment.Slice(MinHeaderLength, options.Length));
        if (!optionsCopy.Succeeded)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        BufferOperationResult payloadCopy =
            BufferOperations.MemoryCopy(payload, segment.Slice(headerLength, payload.Length));
        if (!payloadCopy.Succeeded)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        bytesWritten = segmentLength;
        return PacketParseStatus.Success;
    }
}
