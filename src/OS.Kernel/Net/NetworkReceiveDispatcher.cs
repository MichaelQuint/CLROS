namespace OS.Net;

using OS.Drivers;

public static class NetworkReceiveDispatcher
{
    public static NetworkReceiveDispatchResult Dispatch(ReadOnlySpan<byte> receiveBuffer)
    {
        VirtioNetReceiveFrameClassificationResult classifierResult = VirtioNetReceiveFrameClassifier.Classify(receiveBuffer);
        if (!classifierResult.Succeeded)
        {
            NetworkReceiveDispatchStatus status = classifierResult.Status == VirtioNetReceiveFrameClassifierStatus.UnsupportedPayload ? NetworkReceiveDispatchStatus.UnsupportedPayload : NetworkReceiveDispatchStatus.ClassifierFailed;
            return NetworkReceiveDispatchResult.Fail(status, classifierResult, classifierResult.PacketStatus);
        }

        ReadOnlySpan<byte> payload = receiveBuffer.Slice(classifierResult.PayloadOffset, classifierResult.PayloadLength);
        if (classifierResult.PayloadKind == EthernetPayloadKind.Arp)
        {
            PacketParseStatus arpStatus = ArpPacket.TryParse(payload, out ArpPacket arpPacket);
            if (arpStatus != PacketParseStatus.Success)
            {
                return NetworkReceiveDispatchResult.Fail(NetworkReceiveDispatchStatus.ArpParseFailed, classifierResult, arpStatus);
            }

            return NetworkReceiveDispatchResult.Arp(classifierResult, arpPacket);
        }

        if (classifierResult.PayloadKind == EthernetPayloadKind.Ipv4)
        {
            PacketParseStatus ipv4Status = Ipv4Header.TryParse(payload, out Ipv4Header ipv4Header, out ReadOnlySpan<byte> ipv4Payload);
            if (ipv4Status != PacketParseStatus.Success)
            {
                return NetworkReceiveDispatchResult.Fail(NetworkReceiveDispatchStatus.Ipv4ParseFailed, classifierResult, ipv4Status);
            }

            return NetworkReceiveDispatchResult.Ipv4(classifierResult, ipv4Header, classifierResult.PayloadOffset + ipv4Header.HeaderLength, ipv4Payload.Length);
        }

        return NetworkReceiveDispatchResult.Fail(NetworkReceiveDispatchStatus.UnsupportedPayload, classifierResult, classifierResult.PacketStatus);
    }
}
