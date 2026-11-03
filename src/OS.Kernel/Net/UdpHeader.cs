namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public readonly struct UdpHeader
{
    public const int HeaderLength = 8;

    public UdpHeader(ushort sourcePort, ushort destinationPort, ushort length, ushort checksum)
    {
        SourcePort = sourcePort;
        DestinationPort = destinationPort;
        Length = length;
        Checksum = checksum;
    }

    public ushort SourcePort
    {
        get;
    }

    public ushort DestinationPort
    {
        get;
    }

    public ushort Length
    {
        get;
    }

    public ushort Checksum
    {
        get;
    }

    public static PacketParseStatus TryParse(
        ReadOnlySpan<byte> datagram,
        out UdpHeader header,
        out ReadOnlySpan<byte> payload)
    {
        if (datagram.Length < HeaderLength)
        {
            header = default;
            payload = default;
            return PacketParseStatus.BufferTooSmall;
        }

        ushort length = EndianPrimitives.ReadUInt16BigEndian(datagram.Slice(4, 2));
        if (length < HeaderLength)
        {
            header = default;
            payload = default;
            return PacketParseStatus.InvalidLength;
        }

        if (datagram.Length < length)
        {
            header = default;
            payload = default;
            return PacketParseStatus.TruncatedPayload;
        }

        if (datagram.Length != length)
        {
            header = default;
            payload = default;
            return PacketParseStatus.InvalidLength;
        }

        header = new UdpHeader(
            EndianPrimitives.ReadUInt16BigEndian(datagram.Slice(0, 2)),
            EndianPrimitives.ReadUInt16BigEndian(datagram.Slice(2, 2)),
            length,
            EndianPrimitives.ReadUInt16BigEndian(datagram.Slice(6, 2)));
        payload = datagram.Slice(HeaderLength, length - HeaderLength);
        return PacketParseStatus.Success;
    }

    public static PacketParseStatus TryWrite(
        Span<byte> datagram,
        ushort sourcePort,
        ushort destinationPort,
        ReadOnlySpan<byte> payload,
        out int bytesWritten)
    {
        bytesWritten = 0;

        if (payload.Length > ushort.MaxValue - HeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        int length = HeaderLength + payload.Length;
        if (datagram.Length < length)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        EndianPrimitives.WriteUInt16BigEndian(datagram.Slice(0, 2), sourcePort);
        EndianPrimitives.WriteUInt16BigEndian(datagram.Slice(2, 2), destinationPort);
        EndianPrimitives.WriteUInt16BigEndian(datagram.Slice(4, 2), (ushort)length);
        EndianPrimitives.WriteUInt16BigEndian(datagram.Slice(6, 2), 0);
        BufferOperationResult copy =
            BufferOperations.MemoryCopy(payload, datagram.Slice(HeaderLength, payload.Length));
        if (!copy.Succeeded)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        bytesWritten = length;
        return PacketParseStatus.Success;
    }
}
