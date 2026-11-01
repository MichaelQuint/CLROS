namespace OS.Net;
public static class DnsUdpFrame
{
    public const ushort DnsServerPort = 53;
    public const int QueryFrameHeaderLength = EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + UdpHeader.HeaderLength;
    public static DnsUdpQueryFrameResult TryWriteQueryFrame(Span<byte> frame, in DnsUdpQueryFrameInfo info, ReadOnlySpan<char> hostName, ref DnsResolverTransaction transaction)
    {
        if (frame.Length < QueryFrameHeaderLength)
        {
            return DnsUdpQueryFrameResult.Fail(DnsUdpFrameStatus.BufferTooSmall, DnsResolverStatus.BufferTooSmall, DnsMessageStatus.BufferTooSmall, PacketParseStatus.BufferTooSmall);
        }

        DnsResolverTransaction working = transaction;
        DnsResolverStatus resolverStatus = working.TryWriteQuery(frame.Slice(QueryFrameHeaderLength), hostName, out DnsResolverTransitionResult resolverResult);
        if (resolverStatus != DnsResolverStatus.Success)
        {
            return DnsUdpQueryFrameResult.Fail(DnsUdpFrameStatus.ResolverWriteFailed, resolverStatus, resolverResult.MessageStatus, resolverResult);
        }

        EthernetIpv4UdpPacketInfo packetInfo = new EthernetIpv4UdpPacketInfo(info.DestinationMac, info.LocalMac, info.LocalAddress, info.DnsServerAddress, info.SourcePort, DnsServerPort, timeToLive: 64, identification: info.Identification);
        ReadOnlySpan<byte> dnsPayload = frame.Slice(QueryFrameHeaderLength, resolverResult.BytesWritten);
        PacketParseStatus packetStatus = EthernetIpv4UdpPacket.TryWrite(frame, packetInfo, dnsPayload, out int bytesWritten);
        if (packetStatus != PacketParseStatus.Success)
        {
            return DnsUdpQueryFrameResult.Fail(DnsUdpFrameStatus.PacketWriteFailed, resolverStatus, resolverResult.MessageStatus, packetStatus, resolverResult);
        }

        transaction = working;
        return DnsUdpQueryFrameResult.Success(resolverResult, bytesWritten, QueryFrameHeaderLength, resolverResult.BytesWritten);
    }

    public static DnsUdpReceiveFrameResult ReceiveResponse(ReadOnlySpan<byte> receiveBuffer, ushort expectedDnsServerPort, ushort expectedClientPort, ref DnsResolverTransaction transaction)
    {
        UdpReceiveDispatchResult udpResult = UdpReceiveDispatcher.Dispatch(receiveBuffer);
        if (!udpResult.Succeeded)
        {
            return DnsUdpReceiveFrameResult.Fail(DnsUdpFrameStatus.UdpDispatchFailed, udpResult, udpResult.UdpPacketStatus);
        }

        if (udpResult.SourcePort != expectedDnsServerPort || udpResult.DestinationPort != expectedClientPort)
        {
            return DnsUdpReceiveFrameResult.Fail(DnsUdpFrameStatus.WrongUdpPorts, udpResult, PacketParseStatus.Success);
        }

        ReadOnlySpan<byte> dnsPayload = receiveBuffer.Slice(udpResult.UdpPayloadOffset, udpResult.UdpPayloadLength);
        DnsResolverStatus resolverStatus = transaction.ReceiveResponse(dnsPayload, out DnsResolverTransitionResult resolverResult);
        if (resolverStatus != DnsResolverStatus.Success)
        {
            return DnsUdpReceiveFrameResult.Fail(DnsUdpFrameStatus.ResolverParseFailed, udpResult, resolverStatus, resolverResult.MessageStatus, resolverResult);
        }

        return DnsUdpReceiveFrameResult.Success(udpResult, resolverResult);
    }
}
