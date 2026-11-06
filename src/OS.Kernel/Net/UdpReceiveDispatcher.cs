namespace OS.Net;

using OS.Drivers;

public static class UdpReceiveDispatcher
{
    public static UdpReceiveDispatchResult Dispatch(ReadOnlySpan<byte> receiveBuffer)
    {
        NetworkReceiveDispatchResult networkResult = NetworkReceiveDispatcher.Dispatch(receiveBuffer);
        if (!networkResult.Succeeded)
        {
            return UdpReceiveDispatchResult.Fail(MapNetworkFailure(networkResult.Status), networkResult, networkResult.PayloadPacketStatus);
        }

        if (networkResult.PayloadKind != EthernetPayloadKind.Ipv4)
        {
            return UdpReceiveDispatchResult.Fail(UdpReceiveDispatchStatus.NonIpv4Payload, networkResult, PacketParseStatus.UnsupportedEtherType);
        }

        if (networkResult.Ipv4Header.Protocol != Ipv4Header.ProtocolUdp)
        {
            return UdpReceiveDispatchResult.Fail(UdpReceiveDispatchStatus.UnsupportedIpProtocol, networkResult, PacketParseStatus.UnsupportedIpProtocol);
        }

        ReadOnlySpan<byte> ipv4Payload = receiveBuffer.Slice(networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength);
        PacketParseStatus udpStatus = UdpHeader.TryParse(ipv4Payload, out UdpHeader udpHeader, out ReadOnlySpan<byte> udpPayload);
        if (udpStatus != PacketParseStatus.Success)
        {
            return UdpReceiveDispatchResult.Fail(UdpReceiveDispatchStatus.UdpParseFailed, networkResult, udpStatus);
        }

        if (udpHeader.Checksum != 0)
        {
            Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(networkResult.Ipv4Header.Source, networkResult.Ipv4Header.Destination, Ipv4TransportChecksum.ProtocolUdp, ipv4Payload, out Ipv4TransportChecksumResult checksumResult);
            if (checksumStatus != Ipv4TransportChecksumStatus.Success || !checksumResult.IsValid)
            {
                return UdpReceiveDispatchResult.Fail(UdpReceiveDispatchStatus.BadUdpChecksum, networkResult, PacketParseStatus.BadChecksum);
            }
        }

        return UdpReceiveDispatchResult.Success(networkResult, udpHeader, networkResult.Ipv4PayloadOffset + UdpHeader.HeaderLength, udpPayload.Length);
    }

    private static UdpReceiveDispatchStatus MapNetworkFailure(NetworkReceiveDispatchStatus status)
    {
        return status switch
        {
            NetworkReceiveDispatchStatus.ClassifierFailed => UdpReceiveDispatchStatus.ClassifierFailed,
            NetworkReceiveDispatchStatus.Ipv4ParseFailed => UdpReceiveDispatchStatus.Ipv4ParseFailed,
            NetworkReceiveDispatchStatus.UnsupportedPayload or NetworkReceiveDispatchStatus.ArpParseFailed => UdpReceiveDispatchStatus.NonIpv4Payload,
            _ => UdpReceiveDispatchStatus.ClassifierFailed
        };
    }
}
