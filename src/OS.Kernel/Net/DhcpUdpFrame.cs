namespace OS.Net;

public static class DhcpUdpFrame
{
    public const int FrameHeaderLength = EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + UdpHeader.HeaderLength;
    public const int DiscoverPayloadLength = DhcpMessage.MinimumMessageLength + 11;
    public const int RequestPayloadLength = DhcpMessage.MinimumMessageLength + 23;
    public const int DiscoverFrameLength = FrameHeaderLength + DiscoverPayloadLength;
    public const int RequestFrameLength = FrameHeaderLength + RequestPayloadLength;
    public static DhcpUdpFrameWriteResult TryWriteDiscover(Span<byte> frame, uint transactionId, MacAddress clientMac, out int bytesWritten)
    {
        return TryWriteClientFrame(frame, transactionId, clientMac, default, default, isRequest: false, identification: 0, out bytesWritten);
    }

    public static DhcpUdpFrameWriteResult TryWriteRequest(Span<byte> frame, uint transactionId, MacAddress clientMac, Ipv4Address requestedAddress, Ipv4Address serverIdentifier, out int bytesWritten)
    {
        return TryWriteClientFrame(frame, transactionId, clientMac, requestedAddress, serverIdentifier, isRequest: true, identification: 0, out bytesWritten);
    }

    public static DhcpUdpFrameReadResult TryParseServerReplyFromReceiveBuffer(ReadOnlySpan<byte> receiveBuffer)
    {
        UdpReceiveDispatchResult udpResult = UdpReceiveDispatcher.Dispatch(receiveBuffer);
        if (!udpResult.Succeeded)
        {
            return DhcpUdpFrameReadResult.Fail(DhcpUdpFrameStatus.UdpReceiveFailed, DhcpMessageStatus.Success, udpResult.UdpPacketStatus, udpResult.Status, default, udpResult);
        }

        EthernetIpv4UdpPacketInfo packetInfo = ToPacketInfo(udpResult);
        if (!IsDhcpServerReply(packetInfo))
        {
            return DhcpUdpFrameReadResult.Fail(DhcpUdpFrameStatus.WrongUdpPorts, DhcpMessageStatus.Success, PacketParseStatus.Success, UdpReceiveDispatchStatus.Success, packetInfo, udpResult);
        }

        ReadOnlySpan<byte> payload = receiveBuffer.Slice(udpResult.UdpPayloadOffset, udpResult.UdpPayloadLength);
        return ParseDhcpServerPayload(payload, packetInfo, udpResult, udpResult.UdpPayloadOffset);
    }

    public static DhcpUdpFrameReadResult TryParseServerReplyFromEthernetFrame(ReadOnlySpan<byte> ethernetFrame)
    {
        PacketParseStatus packetStatus = EthernetIpv4UdpPacket.TryParse(ethernetFrame, out EthernetIpv4UdpPacketInfo packetInfo, out ReadOnlySpan<byte> payload);
        if (packetStatus != PacketParseStatus.Success)
        {
            return DhcpUdpFrameReadResult.Fail(DhcpUdpFrameStatus.PacketFailed, DhcpMessageStatus.Success, packetStatus, MapPacketFailureToUdpStatus(packetStatus), default, default);
        }

        if (!IsDhcpServerReply(packetInfo))
        {
            return DhcpUdpFrameReadResult.Fail(DhcpUdpFrameStatus.WrongUdpPorts, DhcpMessageStatus.Success, PacketParseStatus.Success, UdpReceiveDispatchStatus.Success, packetInfo, default);
        }

        return ParseDhcpServerPayload(payload, packetInfo, default, FrameHeaderLength);
    }

    private static DhcpUdpFrameWriteResult TryWriteClientFrame(Span<byte> frame, uint transactionId, MacAddress clientMac, Ipv4Address requestedAddress, Ipv4Address serverIdentifier, bool isRequest, ushort identification, out int bytesWritten)
    {
        bytesWritten = 0;
        int payloadLength = isRequest ? RequestPayloadLength : DiscoverPayloadLength;
        int requiredLength = FrameHeaderLength + payloadLength;
        if (frame.Length < requiredLength)
        {
            return DhcpUdpFrameWriteResult.Fail(DhcpUdpFrameStatus.PacketFailed, DhcpMessageStatus.BufferTooSmall, PacketParseStatus.BufferTooSmall);
        }

        Span<byte> payload = frame.Slice(FrameHeaderLength, payloadLength);
        DhcpMessageStatus dhcpStatus = isRequest ? DhcpMessage.TryWriteRequest(payload, transactionId, clientMac, requestedAddress, serverIdentifier, out int dhcpBytesWritten) : DhcpMessage.TryWriteDiscover(payload, transactionId, clientMac, out dhcpBytesWritten);
        if (dhcpStatus != DhcpMessageStatus.Success)
        {
            return DhcpUdpFrameWriteResult.Fail(DhcpUdpFrameStatus.DhcpMessageFailed, dhcpStatus, PacketParseStatus.Success);
        }

        EthernetIpv4UdpPacketInfo packetInfo = new EthernetIpv4UdpPacketInfo(BroadcastMac(), clientMac, new Ipv4Address(0, 0, 0, 0), BroadcastIpv4(), DhcpMessage.ClientPort, DhcpMessage.ServerPort, timeToLive: 64, identification: identification);
        PacketParseStatus packetStatus = EthernetIpv4UdpPacket.TryWrite(frame, packetInfo, payload.Slice(0, dhcpBytesWritten), out int packetBytesWritten);
        if (packetStatus != PacketParseStatus.Success)
        {
            return DhcpUdpFrameWriteResult.Fail(DhcpUdpFrameStatus.PacketFailed, DhcpMessageStatus.Success, packetStatus);
        }

        bytesWritten = packetBytesWritten;
        return DhcpUdpFrameWriteResult.Success(dhcpBytesWritten, packetBytesWritten);
    }

    private static DhcpUdpFrameReadResult ParseDhcpServerPayload(ReadOnlySpan<byte> payload, EthernetIpv4UdpPacketInfo packetInfo, UdpReceiveDispatchResult udpResult, int payloadOffset)
    {
        DhcpMessageStatus dhcpStatus = DhcpMessage.TryParse(payload, out DhcpMessage message);
        if (dhcpStatus != DhcpMessageStatus.Success)
        {
            return DhcpUdpFrameReadResult.Fail(DhcpUdpFrameStatus.DhcpMessageFailed, dhcpStatus, PacketParseStatus.Success, UdpReceiveDispatchStatus.Success, packetInfo, udpResult);
        }

        return DhcpUdpFrameReadResult.Success(message, packetInfo, udpResult, payloadOffset, payload.Length);
    }

    private static bool IsDhcpServerReply(in EthernetIpv4UdpPacketInfo packetInfo)
    {
        return packetInfo.SourcePort == DhcpMessage.ServerPort && packetInfo.DestinationPort == DhcpMessage.ClientPort;
    }

    private static EthernetIpv4UdpPacketInfo ToPacketInfo(in UdpReceiveDispatchResult udpResult)
    {
        return new EthernetIpv4UdpPacketInfo(udpResult.EthernetHeader.Destination, udpResult.EthernetHeader.Source, udpResult.SourceAddress, udpResult.DestinationAddress, udpResult.SourcePort, udpResult.DestinationPort, udpResult.Ipv4Header.TimeToLive, udpResult.Ipv4Header.Identification, udpResult.Ipv4Header.FlagsAndFragmentOffset);
    }

    private static UdpReceiveDispatchStatus MapPacketFailureToUdpStatus(PacketParseStatus packetStatus)
    {
        return packetStatus switch
        {
            PacketParseStatus.UnsupportedEtherType => UdpReceiveDispatchStatus.NonIpv4Payload,
            PacketParseStatus.UnsupportedIpProtocol => UdpReceiveDispatchStatus.UnsupportedIpProtocol,
            PacketParseStatus.BufferTooSmall or PacketParseStatus.InvalidLength or PacketParseStatus.BadChecksum or PacketParseStatus.TruncatedPayload => UdpReceiveDispatchStatus.UdpParseFailed,
            _ => UdpReceiveDispatchStatus.UdpParseFailed
        };
    }

    public static MacAddress BroadcastMac()
    {
        return new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
    }

    public static Ipv4Address BroadcastIpv4()
    {
        return new Ipv4Address(255, 255, 255, 255);
    }
}
