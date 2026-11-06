namespace OS.Net;

using OS.Drivers;

public static class IcmpReceiveDispatcher
{
    public static IcmpReceiveDispatchResult Dispatch(ReadOnlySpan<byte> receiveBuffer)
    {
        NetworkReceiveDispatchResult networkResult = NetworkReceiveDispatcher.Dispatch(receiveBuffer);
        if (!networkResult.Succeeded)
        {
            return IcmpReceiveDispatchResult.Fail(MapNetworkFailure(networkResult.Status), networkResult, networkResult.PayloadPacketStatus);
        }

        if (networkResult.PayloadKind != EthernetPayloadKind.Ipv4)
        {
            return IcmpReceiveDispatchResult.Fail(IcmpReceiveDispatchStatus.NonIpv4Payload, networkResult, PacketParseStatus.UnsupportedEtherType);
        }

        if (networkResult.Ipv4Header.Protocol != Ipv4Header.ProtocolIcmp)
        {
            return IcmpReceiveDispatchResult.Fail(IcmpReceiveDispatchStatus.UnsupportedIpProtocol, networkResult, PacketParseStatus.UnsupportedIpProtocol);
        }

        ReadOnlySpan<byte> ipv4Payload = receiveBuffer.Slice(networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength);
        PacketParseStatus icmpStatus = IcmpEchoPacket.TryParse(ipv4Payload, out IcmpEchoPacket echoPacket, out ReadOnlySpan<byte> echoPayload);
        if (icmpStatus != PacketParseStatus.Success)
        {
            return IcmpReceiveDispatchResult.Fail(IcmpReceiveDispatchStatus.IcmpParseFailed, networkResult, icmpStatus);
        }

        return IcmpReceiveDispatchResult.Success(networkResult, echoPacket, networkResult.Ipv4PayloadOffset + IcmpEchoPacket.HeaderLength, echoPayload.Length);
    }

    private static IcmpReceiveDispatchStatus MapNetworkFailure(NetworkReceiveDispatchStatus status)
    {
        return status switch
        {
            NetworkReceiveDispatchStatus.ClassifierFailed => IcmpReceiveDispatchStatus.ClassifierFailed,
            NetworkReceiveDispatchStatus.Ipv4ParseFailed => IcmpReceiveDispatchStatus.Ipv4ParseFailed,
            NetworkReceiveDispatchStatus.UnsupportedPayload or NetworkReceiveDispatchStatus.ArpParseFailed => IcmpReceiveDispatchStatus.NonIpv4Payload,
            _ => IcmpReceiveDispatchStatus.ClassifierFailed
        };
    }
}
