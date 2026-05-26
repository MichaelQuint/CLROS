namespace OS.Net;

using OS.Std.Binary;

public readonly struct EthernetHeader
{
    public const int HeaderLength = 14;
    public const ushort EtherTypeIpv4 = 0x0800;
    public const ushort EtherTypeArp = 0x0806;

    public EthernetHeader(MacAddress destination, MacAddress source, ushort etherType)
    {
        Destination = destination;
        Source = source;
        EtherType = etherType;
    }

    public MacAddress Destination { get; }

    public MacAddress Source { get; }

    public ushort EtherType { get; }

    public static PacketParseStatus TryParse(
        ReadOnlySpan<byte> frame,
        out EthernetHeader header,
        out ReadOnlySpan<byte> payload)
    {
        if (frame.Length < HeaderLength)
        {
            header = default;
            payload = default;
            return PacketParseStatus.BufferTooSmall;
        }

        PacketParseStatus destinationStatus = MacAddress.TryRead(frame, 0, out MacAddress destination);
        PacketParseStatus sourceStatus = MacAddress.TryRead(frame, MacAddress.Length, out MacAddress source);
        if (destinationStatus != PacketParseStatus.Success || sourceStatus != PacketParseStatus.Success)
        {
            header = default;
            payload = default;
            return PacketParseStatus.BufferTooSmall;
        }

        ushort etherType = EndianPrimitives.ReadUInt16BigEndian(frame.Slice(12, 2));
        header = new EthernetHeader(destination, source, etherType);
        payload = frame.Slice(HeaderLength);
        return PacketParseStatus.Success;
    }

    public static PacketParseStatus TryDispatch(
        ReadOnlySpan<byte> frame,
        out EthernetHeader header,
        out EthernetPayloadKind payloadKind,
        out ReadOnlySpan<byte> payload)
    {
        PacketParseStatus status = TryParse(frame, out header, out payload);
        if (status != PacketParseStatus.Success)
        {
            payloadKind = EthernetPayloadKind.Unsupported;
            return status;
        }

        payloadKind = GetPayloadKind(header.EtherType);
        if (payloadKind == EthernetPayloadKind.Unsupported)
        {
            return PacketParseStatus.UnsupportedEtherType;
        }

        return PacketParseStatus.Success;
    }

    public static EthernetPayloadKind GetPayloadKind(ushort etherType)
    {
        return etherType switch
        {
            EtherTypeIpv4 => EthernetPayloadKind.Ipv4,
            EtherTypeArp => EthernetPayloadKind.Arp,
            _ => EthernetPayloadKind.Unsupported
        };
    }

    public static PacketParseStatus TryWrite(Span<byte> frame, in EthernetHeader header)
    {
        if (frame.Length < HeaderLength)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        PacketParseStatus destinationStatus = header.Destination.TryWrite(frame, 0);
        PacketParseStatus sourceStatus = header.Source.TryWrite(frame, MacAddress.Length);
        if (destinationStatus != PacketParseStatus.Success || sourceStatus != PacketParseStatus.Success)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        EndianPrimitives.WriteUInt16BigEndian(frame.Slice(12, 2), header.EtherType);
        return PacketParseStatus.Success;
    }
}
