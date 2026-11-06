namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;
using OS.Std.Net;

public readonly struct IcmpEchoPacket
{
    public const int HeaderLength = 8;
    public const byte TypeEchoReply = 0;
    public const byte TypeEchoRequest = 8;
    public const byte EchoCode = 0;

    public IcmpEchoPacket(
        byte type,
        byte code,
        ushort checksum,
        ushort identifier,
        ushort sequenceNumber)
    {
        Type = type;
        Code = code;
        Checksum = checksum;
        Identifier = identifier;
        SequenceNumber = sequenceNumber;
    }

    public byte Type
    {
        get;
    }

    public byte Code
    {
        get;
    }

    public ushort Checksum
    {
        get;
    }

    public ushort Identifier
    {
        get;
    }

    public ushort SequenceNumber
    {
        get;
    }

    public bool IsEchoRequest => Type == TypeEchoRequest;

    public bool IsEchoReply => Type == TypeEchoReply;

    public static PacketParseStatus TryParse(
        ReadOnlySpan<byte> packet,
        out IcmpEchoPacket echo,
        out ReadOnlySpan<byte> payload)
    {
        if (packet.Length < HeaderLength)
        {
            echo = default;
            payload = default;
            return PacketParseStatus.BufferTooSmall;
        }

        byte type = packet[0];
        byte code = packet[1];
        if (type != TypeEchoRequest && type != TypeEchoReply)
        {
            echo = default;
            payload = default;
            return PacketParseStatus.UnsupportedIcmpType;
        }

        if (code != EchoCode)
        {
            echo = default;
            payload = default;
            return PacketParseStatus.UnsupportedIcmpCode;
        }

        if (!IsChecksumValid(packet))
        {
            echo = default;
            payload = default;
            return PacketParseStatus.BadChecksum;
        }

        echo = new IcmpEchoPacket(
            type,
            code,
            EndianPrimitives.ReadUInt16BigEndian(packet.Slice(2, 2)),
            EndianPrimitives.ReadUInt16BigEndian(packet.Slice(4, 2)),
            EndianPrimitives.ReadUInt16BigEndian(packet.Slice(6, 2)));
        payload = packet.Slice(HeaderLength);
        return PacketParseStatus.Success;
    }

    public static PacketParseStatus TryWriteEchoRequest(
        Span<byte> packet,
        ushort identifier,
        ushort sequenceNumber,
        ReadOnlySpan<byte> payload,
        out int bytesWritten)
    {
        return TryWrite(
            packet,
            TypeEchoRequest,
            EchoCode,
            identifier,
            sequenceNumber,
            payload,
            out bytesWritten);
    }

    public static PacketParseStatus TryWriteEchoReply(
        Span<byte> packet,
        ushort identifier,
        ushort sequenceNumber,
        ReadOnlySpan<byte> payload,
        out int bytesWritten)
    {
        return TryWrite(
            packet,
            TypeEchoReply,
            EchoCode,
            identifier,
            sequenceNumber,
            payload,
            out bytesWritten);
    }

    public static PacketParseStatus TryWrite(
        Span<byte> packet,
        byte type,
        byte code,
        ushort identifier,
        ushort sequenceNumber,
        ReadOnlySpan<byte> payload,
        out int bytesWritten)
    {
        bytesWritten = 0;

        if (type != TypeEchoRequest && type != TypeEchoReply)
        {
            return PacketParseStatus.UnsupportedIcmpType;
        }

        if (code != EchoCode)
        {
            return PacketParseStatus.UnsupportedIcmpCode;
        }

        if (payload.Length > ushort.MaxValue - HeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        int packetLength = HeaderLength + payload.Length;
        if (packet.Length < packetLength)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        packet[0] = type;
        packet[1] = code;
        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(2, 2), 0);
        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(4, 2), identifier);
        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(6, 2), sequenceNumber);
        BufferOperationResult copy =
            BufferOperations.MemoryCopy(payload, packet.Slice(HeaderLength, payload.Length));
        if (!copy.Succeeded)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        ushort checksum = ComputeChecksum(packet.Slice(0, packetLength));
        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(2, 2), checksum);
        bytesWritten = packetLength;
        return PacketParseStatus.Success;
    }

    public static ushort ComputeChecksum(ReadOnlySpan<byte> packet)
    {
        return InternetChecksum.Compute(packet);
    }

    public static bool IsChecksumValid(ReadOnlySpan<byte> packet)
    {
        return InternetChecksum.IsValid(packet);
    }
}
