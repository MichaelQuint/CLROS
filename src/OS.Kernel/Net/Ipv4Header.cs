namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Net;

public readonly struct Ipv4Header
{
    public const int MinHeaderLength = 20;
    public const byte Version = 4;
    public const byte ProtocolIcmp = 1;
    public const byte ProtocolUdp = 17;

    public Ipv4Header(
        Ipv4Address source,
        Ipv4Address destination,
        byte protocol,
        ushort totalLength,
        byte timeToLive,
        ushort identification,
        ushort flagsAndFragmentOffset,
        byte typeOfService,
        ushort headerChecksum,
        byte headerLength)
    {
        Source = source;
        Destination = destination;
        Protocol = protocol;
        TotalLength = totalLength;
        TimeToLive = timeToLive;
        Identification = identification;
        FlagsAndFragmentOffset = flagsAndFragmentOffset;
        TypeOfService = typeOfService;
        HeaderChecksum = headerChecksum;
        HeaderLength = headerLength;
    }

    public Ipv4Address Source
    {
        get;
    }

    public Ipv4Address Destination
    {
        get;
    }

    public byte Protocol
    {
        get;
    }

    public ushort TotalLength
    {
        get;
    }

    public byte TimeToLive
    {
        get;
    }

    public ushort Identification
    {
        get;
    }

    public ushort FlagsAndFragmentOffset
    {
        get;
    }

    public byte TypeOfService
    {
        get;
    }

    public ushort HeaderChecksum
    {
        get;
    }

    public byte HeaderLength
    {
        get;
    }

    internal static Ipv4Header Create(
        Ipv4Address source,
        Ipv4Address destination,
        byte protocol,
        ushort payloadLength,
        byte timeToLive,
        ushort identification,
        ushort flagsAndFragmentOffset)
    {
        return new Ipv4Header(
            source,
            destination,
            protocol,
            (ushort)(MinHeaderLength + payloadLength),
            timeToLive,
            identification,
            flagsAndFragmentOffset,
            typeOfService: 0,
            headerChecksum: 0,
            headerLength: MinHeaderLength);
    }

    public static PacketParseStatus TryParse(
        ReadOnlySpan<byte> packet,
        out Ipv4Header header,
        out ReadOnlySpan<byte> payload)
    {
        if (packet.Length < MinHeaderLength)
        {
            header = default;
            payload = default;
            return PacketParseStatus.BufferTooSmall;
        }

        byte version = (byte)(packet[0] >> 4);
        if (version != Version)
        {
            header = default;
            payload = default;
            return PacketParseStatus.UnsupportedIpVersion;
        }

        int headerLength = (packet[0] & 0x0F) * 4;
        if (headerLength != MinHeaderLength)
        {
            header = default;
            payload = default;
            return PacketParseStatus.UnsupportedIpHeaderLength;
        }

        if (packet.Length < headerLength)
        {
            header = default;
            payload = default;
            return PacketParseStatus.BufferTooSmall;
        }

        ushort totalLength = EndianPrimitives.ReadUInt16BigEndian(packet.Slice(2, 2));
        if (totalLength < headerLength)
        {
            header = default;
            payload = default;
            return PacketParseStatus.InvalidLength;
        }

        if (packet.Length < totalLength)
        {
            header = default;
            payload = default;
            return PacketParseStatus.TruncatedPayload;
        }

        if (!IsHeaderChecksumValid(packet.Slice(0, headerLength)))
        {
            header = default;
            payload = default;
            return PacketParseStatus.BadChecksum;
        }

        PacketParseStatus sourceStatus = Ipv4Address.TryRead(packet, 12, out Ipv4Address source);
        PacketParseStatus destinationStatus = Ipv4Address.TryRead(packet, 16, out Ipv4Address destination);
        if (sourceStatus != PacketParseStatus.Success || destinationStatus != PacketParseStatus.Success)
        {
            header = default;
            payload = default;
            return PacketParseStatus.BufferTooSmall;
        }

        header = new Ipv4Header(
            source,
            destination,
            packet[9],
            totalLength,
            packet[8],
            EndianPrimitives.ReadUInt16BigEndian(packet.Slice(4, 2)),
            EndianPrimitives.ReadUInt16BigEndian(packet.Slice(6, 2)),
            packet[1],
            EndianPrimitives.ReadUInt16BigEndian(packet.Slice(10, 2)),
            (byte)headerLength);
        payload = packet.Slice(headerLength, totalLength - headerLength);
        return PacketParseStatus.Success;
    }

    public static PacketParseStatus TryWrite(Span<byte> packet, in Ipv4Header header, out int bytesWritten)
    {
        bytesWritten = 0;

        if (header.HeaderLength != MinHeaderLength || header.TotalLength < MinHeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        if (packet.Length < header.HeaderLength)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        packet[0] = (byte)((Version << 4) | (MinHeaderLength / 4));
        packet[1] = header.TypeOfService;
        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(2, 2), header.TotalLength);
        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(4, 2), header.Identification);
        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(6, 2), header.FlagsAndFragmentOffset);
        packet[8] = header.TimeToLive;
        packet[9] = header.Protocol;
        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(10, 2), 0);
        header.Source.TryWrite(packet, 12);
        header.Destination.TryWrite(packet, 16);

        ushort checksum = ComputeHeaderChecksum(packet.Slice(0, MinHeaderLength));
        EndianPrimitives.WriteUInt16BigEndian(packet.Slice(10, 2), checksum);
        bytesWritten = MinHeaderLength;
        return PacketParseStatus.Success;
    }

    public static ushort ComputeHeaderChecksum(ReadOnlySpan<byte> header)
    {
        return InternetChecksum.Compute(header);
    }

    public static bool IsHeaderChecksumValid(ReadOnlySpan<byte> header)
    {
        return InternetChecksum.IsValid(header);
    }
}
