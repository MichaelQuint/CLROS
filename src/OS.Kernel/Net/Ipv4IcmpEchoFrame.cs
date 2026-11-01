namespace OS.Net;
public static class Ipv4IcmpEchoFrame
{
    public const int HeaderLength = EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + IcmpEchoPacket.HeaderLength;
    private const byte DefaultTimeToLive = 64;
    public static PacketParseStatus TryParse(ReadOnlySpan<byte> frame, out Ipv4IcmpEchoFrameInfo info, out IcmpEchoPacket echo, out ReadOnlySpan<byte> payload)
    {
        PacketParseStatus ethernetStatus = EthernetHeader.TryParse(frame, out EthernetHeader ethernet, out ReadOnlySpan<byte> ethernetPayload);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            info = default;
            echo = default;
            payload = default;
            return ethernetStatus;
        }

        if (ethernet.EtherType != EthernetHeader.EtherTypeIpv4)
        {
            info = default;
            echo = default;
            payload = default;
            return PacketParseStatus.UnsupportedEtherType;
        }

        PacketParseStatus ipv4Status = Ipv4Header.TryParse(ethernetPayload, out Ipv4Header ipv4, out ReadOnlySpan<byte> ipv4Payload);
        if (ipv4Status != PacketParseStatus.Success)
        {
            info = default;
            echo = default;
            payload = default;
            return ipv4Status;
        }

        if (ipv4.Protocol != Ipv4Header.ProtocolIcmp)
        {
            info = default;
            echo = default;
            payload = default;
            return PacketParseStatus.UnsupportedIpProtocol;
        }

        PacketParseStatus icmpStatus = IcmpEchoPacket.TryParse(ipv4Payload, out echo, out ReadOnlySpan<byte> icmpPayload);
        if (icmpStatus != PacketParseStatus.Success)
        {
            info = default;
            payload = default;
            return icmpStatus;
        }

        info = new Ipv4IcmpEchoFrameInfo(ethernet.Destination, ethernet.Source, ipv4.Source, ipv4.Destination, ipv4.TimeToLive, ipv4.Identification, ipv4.FlagsAndFragmentOffset);
        payload = icmpPayload;
        return PacketParseStatus.Success;
    }

    public static PacketParseStatus TryWriteEchoRequest(Span<byte> frame, in Ipv4IcmpEchoFrameInfo info, ushort identifier, ushort sequenceNumber, ReadOnlySpan<byte> payload, out int bytesWritten)
    {
        return TryWrite(frame, info, IcmpEchoPacket.TypeEchoRequest, identifier, sequenceNumber, payload, out bytesWritten);
    }

    public static PacketParseStatus TryWriteEchoReply(Span<byte> frame, in Ipv4IcmpEchoFrameInfo info, ushort identifier, ushort sequenceNumber, ReadOnlySpan<byte> payload, out int bytesWritten)
    {
        return TryWrite(frame, info, IcmpEchoPacket.TypeEchoReply, identifier, sequenceNumber, payload, out bytesWritten);
    }

    public static PacketParseStatus TryWrite(Span<byte> frame, in Ipv4IcmpEchoFrameInfo info, byte icmpType, ushort identifier, ushort sequenceNumber, ReadOnlySpan<byte> payload, out int bytesWritten)
    {
        bytesWritten = 0;
        if (icmpType != IcmpEchoPacket.TypeEchoRequest && icmpType != IcmpEchoPacket.TypeEchoReply)
        {
            return PacketParseStatus.UnsupportedIcmpType;
        }

        if (payload.Length > ushort.MaxValue - Ipv4Header.MinHeaderLength - IcmpEchoPacket.HeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        int icmpLength = IcmpEchoPacket.HeaderLength + payload.Length;
        int ipv4Length = Ipv4Header.MinHeaderLength + icmpLength;
        int frameLength = EthernetHeader.HeaderLength + ipv4Length;
        if (frame.Length < frameLength)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        EthernetHeader ethernet = new EthernetHeader(info.DestinationMac, info.SourceMac, EthernetHeader.EtherTypeIpv4);
        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(frame, ethernet);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            return ethernetStatus;
        }

        Ipv4Header ipv4 = Ipv4Header.Create(info.SourceAddress, info.DestinationAddress, Ipv4Header.ProtocolIcmp, (ushort)icmpLength, info.TimeToLive == 0 ? DefaultTimeToLive : info.TimeToLive, info.Identification, info.FlagsAndFragmentOffset);
        PacketParseStatus ipv4Status = Ipv4Header.TryWrite(frame.Slice(EthernetHeader.HeaderLength, Ipv4Header.MinHeaderLength), ipv4, out int ipv4BytesWritten);
        if (ipv4Status != PacketParseStatus.Success)
        {
            return ipv4Status;
        }

        PacketParseStatus icmpStatus = IcmpEchoPacket.TryWrite(frame.Slice(EthernetHeader.HeaderLength + ipv4BytesWritten, icmpLength), icmpType, IcmpEchoPacket.EchoCode, identifier, sequenceNumber, payload, out int icmpBytesWritten);
        if (icmpStatus != PacketParseStatus.Success)
        {
            return icmpStatus;
        }

        bytesWritten = EthernetHeader.HeaderLength + ipv4BytesWritten + icmpBytesWritten;
        return PacketParseStatus.Success;
    }
}
