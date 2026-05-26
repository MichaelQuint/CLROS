namespace OS.Net;

using OS.Std.Binary;

public static class EthernetIpv4UdpPacket
{
    public static PacketParseStatus TryParse(
        ReadOnlySpan<byte> frame,
        out EthernetIpv4UdpPacketInfo info,
        out ReadOnlySpan<byte> payload)
    {
        PacketParseStatus ethernetStatus = EthernetHeader.TryParse(frame, out EthernetHeader ethernet, out ReadOnlySpan<byte> ethernetPayload);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            info = default;
            payload = default;
            return ethernetStatus;
        }

        if (ethernet.EtherType != EthernetHeader.EtherTypeIpv4)
        {
            info = default;
            payload = default;
            return PacketParseStatus.UnsupportedEtherType;
        }

        PacketParseStatus ipv4Status = Ipv4Header.TryParse(ethernetPayload, out Ipv4Header ipv4, out ReadOnlySpan<byte> ipv4Payload);
        if (ipv4Status != PacketParseStatus.Success)
        {
            info = default;
            payload = default;
            return ipv4Status;
        }

        if (ipv4.Protocol != Ipv4Header.ProtocolUdp)
        {
            info = default;
            payload = default;
            return PacketParseStatus.UnsupportedIpProtocol;
        }

        PacketParseStatus udpStatus = UdpHeader.TryParse(ipv4Payload, out UdpHeader udp, out ReadOnlySpan<byte> udpPayload);
        if (udpStatus != PacketParseStatus.Success)
        {
            info = default;
            payload = default;
            return udpStatus;
        }

        info = new EthernetIpv4UdpPacketInfo(
            ethernet.Destination,
            ethernet.Source,
            ipv4.Source,
            ipv4.Destination,
            udp.SourcePort,
            udp.DestinationPort,
            ipv4.TimeToLive,
            ipv4.Identification,
            ipv4.FlagsAndFragmentOffset);
        payload = udpPayload;
        return PacketParseStatus.Success;
    }

    public static PacketParseStatus TryWrite(
        Span<byte> frame,
        in EthernetIpv4UdpPacketInfo info,
        ReadOnlySpan<byte> payload,
        out int bytesWritten)
    {
        bytesWritten = 0;

        if (payload.Length > ushort.MaxValue - Ipv4Header.MinHeaderLength - UdpHeader.HeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        int udpLength = UdpHeader.HeaderLength + payload.Length;
        int ipv4Length = Ipv4Header.MinHeaderLength + udpLength;
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

        Ipv4Header ipv4 = Ipv4Header.Create(
            info.SourceAddress,
            info.DestinationAddress,
            Ipv4Header.ProtocolUdp,
            (ushort)udpLength,
            info.TimeToLive == 0 ? (byte)64 : info.TimeToLive,
            info.Identification,
            info.FlagsAndFragmentOffset);
        PacketParseStatus ipv4Status = Ipv4Header.TryWrite(
            frame.Slice(EthernetHeader.HeaderLength, Ipv4Header.MinHeaderLength),
            ipv4,
            out int ipv4BytesWritten);
        if (ipv4Status != PacketParseStatus.Success)
        {
            return ipv4Status;
        }

        PacketParseStatus udpStatus = UdpHeader.TryWrite(
            frame.Slice(EthernetHeader.HeaderLength + ipv4BytesWritten, udpLength),
            info.SourcePort,
            info.DestinationPort,
            payload,
            out int udpBytesWritten);
        if (udpStatus != PacketParseStatus.Success)
        {
            return udpStatus;
        }

        Span<byte> udpDatagram = frame.Slice(
            EthernetHeader.HeaderLength + ipv4BytesWritten,
            udpBytesWritten);
        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(
            ipv4.Source,
            ipv4.Destination,
            Ipv4TransportChecksum.ProtocolUdp,
            udpDatagram,
            out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success)
        {
            return PacketParseStatus.InvalidLength;
        }

        ushort checksum = checksumResult.Checksum == 0
            ? ushort.MaxValue
            : checksumResult.Checksum;
        EndianPrimitives.WriteUInt16BigEndian(
            udpDatagram.Slice(6, sizeof(ushort)),
            checksum);

        bytesWritten = EthernetHeader.HeaderLength + ipv4BytesWritten + udpBytesWritten;
        return PacketParseStatus.Success;
    }
}
