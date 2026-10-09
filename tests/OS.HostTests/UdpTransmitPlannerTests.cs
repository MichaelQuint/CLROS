using System.Runtime.CompilerServices;
using OS.Net;

internal static class UdpTransmitPlannerTests
{

    [HostTest("net.udp.tx writes ethernet ipv4 udp frame", HostLane.Fast, 0)]
    private static bool TestWritesEthernetIpv4UdpFrame()
    {
        byte[] payload = { 0x64, 0x61, 0x74, 0x61 };
        byte[] frame = new byte[UdpTransmitPlanner.HeaderBytes + payload.Length];

        UdpTransmitPlanStatus status = UdpTransmitPlanner.TryPlan(
            LocalMac,
            LocalAddress(),
            RemoteMac,
            RemoteAddress(),
            sourcePort: 49152,
            destinationPort: 53,
            timeToLive: 48,
            identification: 0x1133,
            payload,
            frame,
            out UdpTransmitPlanResult result);
        if (status != UdpTransmitPlanStatus.Success
            || !result.Succeeded
            || result.PacketStatus != PacketParseStatus.Success
            || result.BytesWritten != frame.Length)
        {
            return HostCheck.Result(false);
        }

        PacketParseStatus parseStatus = EthernetIpv4UdpPacket.TryParse(
            frame,
            out EthernetIpv4UdpPacketInfo parsed,
            out ReadOnlySpan<byte> parsedPayload);

        return HostCheck.Result(parseStatus == PacketParseStatus.Success
            && parsed.DestinationMac == RemoteMac
            && parsed.SourceMac == LocalMac
            && parsed.SourceAddress == LocalAddress()
            && parsed.DestinationAddress == RemoteAddress()
            && parsed.SourcePort == 49152
            && parsed.DestinationPort == 53
            && parsed.TimeToLive == 48
            && parsed.Identification == 0x1133
            && parsedPayload.SequenceEqual(payload));
    }

    [HostTest("net.udp.tx writes nonzero checksum", HostLane.Fast, 1)]
    private static bool TestWritesNonZeroChecksum()
    {
        byte[] payload = { 0x63, 0x6B };
        byte[] frame = new byte[UdpTransmitPlanner.HeaderBytes + payload.Length];

        UdpTransmitPlanStatus status = UdpTransmitPlanner.TryPlan(
            LocalMac,
            LocalAddress(),
            RemoteMac,
            RemoteAddress(),
            sourcePort: 49152,
            destinationPort: 53,
            timeToLive: 48,
            identification: 0x1134,
            payload,
            frame,
            out UdpTransmitPlanResult result);
        PacketParseStatus parseStatus = TryParseUdpDatagram(
            frame,
            out UdpHeader udp,
            out ReadOnlySpan<byte> parsedPayload,
            out ReadOnlySpan<byte> _,
            out Ipv4Header _);

        return HostCheck.Result(status == UdpTransmitPlanStatus.Success
            && result.Succeeded
            && result.BytesWritten == frame.Length
            && parseStatus == PacketParseStatus.Success
            && udp.Checksum != 0
            && parsedPayload.SequenceEqual(payload));
    }

    [HostTest("net.udp.tx checksum validates with pseudo header", HostLane.Fast, 2)]
    private static bool TestChecksumValidatesWithPseudoHeader()
    {
        byte[] payload = { 0x64, 0x61, 0x74, 0x61 };
        byte[] frame = new byte[UdpTransmitPlanner.HeaderBytes + payload.Length];

        UdpTransmitPlanStatus status = UdpTransmitPlanner.TryPlan(
            LocalMac,
            LocalAddress(),
            RemoteMac,
            RemoteAddress(),
            sourcePort: 49152,
            destinationPort: 53,
            timeToLive: 48,
            identification: 0x1135,
            payload,
            frame,
            out UdpTransmitPlanResult result);
        PacketParseStatus parseStatus = TryParseUdpDatagram(
            frame,
            out UdpHeader udp,
            out ReadOnlySpan<byte> parsedPayload,
            out ReadOnlySpan<byte> udpDatagram,
            out Ipv4Header ipv4);
        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4.Source,
            ipv4.Destination,
            Ipv4TransportChecksum.ProtocolUdp,
            udpDatagram,
            out Ipv4TransportChecksumResult checksumResult);

        return HostCheck.Result(status == UdpTransmitPlanStatus.Success
            && result.Succeeded
            && result.BytesWritten == frame.Length
            && parseStatus == PacketParseStatus.Success
            && udp.Checksum != 0
            && parsedPayload.SequenceEqual(payload)
            && checksumStatus == Ipv4TransportChecksumStatus.Success
            && checksumResult.IsValid
            && checksumResult.TransportLength == UdpHeader.HeaderLength + payload.Length);
    }

    [HostTest("net.udp.tx preserves addressing metadata", HostLane.Fast, 3)]
    private static bool TestPreservesAddressingMetadata()
    {
        byte[] payload = { 0x01, 0x02 };
        byte[] frame = new byte[UdpTransmitPlanner.HeaderBytes + payload.Length];

        UdpTransmitPlanStatus status = UdpTransmitPlanner.TryPlan(
            LocalMac,
            LocalAddress(),
            RemoteMac,
            RemoteAddress(),
            sourcePort: 5353,
            destinationPort: 5355,
            timeToLive: 0,
            identification: 0xCAFE,
            payload,
            frame,
            out UdpTransmitPlanResult result);

        return HostCheck.Result(status == UdpTransmitPlanStatus.Success
            && result.LocalMac == LocalMac
            && result.LocalAddress == LocalAddress()
            && result.DestinationMac == RemoteMac
            && result.DestinationAddress == RemoteAddress()
            && result.SourcePort == 5353
            && result.DestinationPort == 5355
            && result.TimeToLive == 64
            && result.Identification == 0xCAFE
            && result.PayloadLength == payload.Length
            && result.BytesWritten == frame.Length);
    }

    [HostTest("net.udp.tx rejects invalid local identity", HostLane.Fast, 4)]
    private static bool TestRejectsInvalidLocalIdentity()
    {
        byte[] payload = { 0xAA };
        byte[] frame = CreatePoisonedFrame(payload.Length);

        UdpTransmitPlanStatus zeroMacStatus = UdpTransmitPlanner.TryPlan(
            default,
            LocalAddress(),
            RemoteMac,
            RemoteAddress(),
            sourcePort: 49152,
            destinationPort: 53,
            timeToLive: 64,
            identification: 0x2222,
            payload,
            frame,
            out UdpTransmitPlanResult zeroMacResult);
        UdpTransmitPlanStatus zeroPortStatus = UdpTransmitPlanner.TryPlan(
            LocalMac,
            LocalAddress(),
            RemoteMac,
            RemoteAddress(),
            sourcePort: 0,
            destinationPort: 53,
            timeToLive: 64,
            identification: 0x2222,
            payload,
            frame,
            out UdpTransmitPlanResult zeroPortResult);

        return HostCheck.Result(zeroMacStatus == UdpTransmitPlanStatus.InvalidLocalIdentity
            && zeroMacResult.PacketStatus == PacketParseStatus.Success
            && zeroMacResult.BytesWritten == 0
            && zeroPortStatus == UdpTransmitPlanStatus.InvalidLocalIdentity
            && zeroPortResult.SourcePort == 0
            && frame[0] == PoisonByte
            && frame[^1] == PoisonByte);
    }

    [HostTest("net.udp.tx rejects invalid destination identity", HostLane.Fast, 5)]
    private static bool TestRejectsInvalidDestinationIdentity()
    {
        byte[] payload = { 0xAA };
        byte[] frame = CreatePoisonedFrame(payload.Length);

        UdpTransmitPlanStatus zeroMacStatus = UdpTransmitPlanner.TryPlan(
            LocalMac,
            LocalAddress(),
            default,
            RemoteAddress(),
            sourcePort: 49152,
            destinationPort: 53,
            timeToLive: 64,
            identification: 0x3333,
            payload,
            frame,
            out UdpTransmitPlanResult zeroMacResult);
        UdpTransmitPlanStatus zeroPortStatus = UdpTransmitPlanner.TryPlan(
            LocalMac,
            LocalAddress(),
            RemoteMac,
            RemoteAddress(),
            sourcePort: 49152,
            destinationPort: 0,
            timeToLive: 64,
            identification: 0x3333,
            payload,
            frame,
            out UdpTransmitPlanResult zeroPortResult);

        return HostCheck.Result(zeroMacStatus == UdpTransmitPlanStatus.InvalidDestinationIdentity
            && zeroMacResult.PacketStatus == PacketParseStatus.Success
            && zeroMacResult.BytesWritten == 0
            && zeroPortStatus == UdpTransmitPlanStatus.InvalidDestinationIdentity
            && zeroPortResult.DestinationPort == 0
            && frame[0] == PoisonByte
            && frame[^1] == PoisonByte);
    }

    [HostTest("net.udp.tx rejects short frame buffer", HostLane.Fast, 6)]
    private static bool TestRejectsShortFrameBuffer()
    {
        byte[] payload = { 0x10, 0x20, 0x30 };
        byte[] frame = CreatePoisonedFrame(payload.Length);
        Span<byte> shortFrame = frame.AsSpan(0, frame.Length - 1);

        UdpTransmitPlanStatus status = UdpTransmitPlanner.TryPlan(
            LocalMac,
            LocalAddress(),
            RemoteMac,
            RemoteAddress(),
            sourcePort: 49152,
            destinationPort: 53,
            timeToLive: 64,
            identification: 0x4444,
            payload,
            shortFrame,
            out UdpTransmitPlanResult result);

        return HostCheck.Result(status == UdpTransmitPlanStatus.FrameBufferTooSmall
            && !result.Succeeded
            && result.PacketStatus == PacketParseStatus.BufferTooSmall
            && result.BytesWritten == 0
            && result.PayloadLength == payload.Length
            && frame[0] == PoisonByte
            && frame[^1] == PoisonByte);
    }

    [HostTest("net.udp.tx rejects oversized payload", HostLane.Fast, 7)]
    private static bool TestRejectsOversizedPayload()
    {
        byte[] payload = new byte[UdpTransmitPlanner.MaximumPayloadBytes + 1];
        byte[] frame = CreatePoisonedFrame(payloadLength: 0);

        UdpTransmitPlanStatus status = UdpTransmitPlanner.TryPlan(
            LocalMac,
            LocalAddress(),
            RemoteMac,
            RemoteAddress(),
            sourcePort: 49152,
            destinationPort: 53,
            timeToLive: 64,
            identification: 0x5555,
            payload,
            frame,
            out UdpTransmitPlanResult result);

        return HostCheck.Result(status == UdpTransmitPlanStatus.PayloadTooLarge
            && !result.Succeeded
            && result.PacketStatus == PacketParseStatus.InvalidLength
            && result.BytesWritten == 0
            && result.PayloadLength == payload.Length
            && result.LocalMac == LocalMac
            && result.DestinationMac == RemoteMac
            && frame[0] == PoisonByte
            && frame[^1] == PoisonByte);
    }

    [HostTest("net.udp.tx default result is not success", HostLane.Fast, 8)]
    private static bool TestDefaultResultIsNotSuccess()
    {
        UdpTransmitPlanResult defaultResult = default;
        UdpTransmitPlanResult success = UdpTransmitPlanResult.Success(
            PacketParseStatus.Success,
            bytesWritten: UdpTransmitPlanner.HeaderBytes + 4,
            LocalMac,
            LocalAddress(),
            RemoteMac,
            RemoteAddress(),
            sourcePort: 49152,
            destinationPort: 53,
            timeToLive: 64,
            identification: 0x7788,
            payloadLength: 4);
        UdpTransmitPlanResult failure = UdpTransmitPlanResult.Fail(
            UdpTransmitPlanStatus.FrameBufferTooSmall,
            PacketParseStatus.BufferTooSmall,
            LocalMac,
            LocalAddress(),
            RemoteMac,
            RemoteAddress(),
            sourcePort: 49152,
            destinationPort: 53,
            timeToLive: 64,
            identification: 0x7788,
            payloadLength: 4);

        return HostCheck.Result(!defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == UdpTransmitPlanStatus.Success
            && success.IsInitialized
            && success.Succeeded
            && success.PacketStatus == PacketParseStatus.Success
            && success.BytesWritten == UdpTransmitPlanner.HeaderBytes + 4
            && success.LocalMac == LocalMac
            && success.LocalAddress == LocalAddress()
            && success.DestinationMac == RemoteMac
            && success.DestinationAddress == RemoteAddress()
            && success.SourcePort == 49152
            && success.DestinationPort == 53
            && success.TimeToLive == 64
            && success.Identification == 0x7788
            && success.PayloadLength == 4
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == UdpTransmitPlanStatus.FrameBufferTooSmall
            && failure.PacketStatus == PacketParseStatus.BufferTooSmall
            && failure.BytesWritten == 0
            && failure.PayloadLength == 4
            && !RuntimeHelpers.IsReferenceOrContainsReferences<UdpTransmitPlanResult>());
    }

    private static byte[] CreatePoisonedFrame(int payloadLength)
    {
        byte[] frame = new byte[UdpTransmitPlanner.HeaderBytes + payloadLength];
        Array.Fill(frame, PoisonByte);
        return frame;
    }

    private static PacketParseStatus TryParseUdpDatagram(
        ReadOnlySpan<byte> frame,
        out UdpHeader udp,
        out ReadOnlySpan<byte> payload,
        out ReadOnlySpan<byte> udpDatagram,
        out Ipv4Header ipv4)
    {
        PacketParseStatus ipv4Status = Ipv4Header.TryParse(
            frame.Slice(EthernetHeader.HeaderLength),
            out ipv4,
            out udpDatagram);
        if (ipv4Status != PacketParseStatus.Success)
        {
            udp = default;
            payload = default;
            return ipv4Status;
        }

        return UdpHeader.TryParse(udpDatagram, out udp, out payload);
    }

    private const byte PoisonByte = 0xA5;

    private static MacAddress LocalMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x44);

    private static MacAddress RemoteMac =>
        new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x33);

    private static Ipv4Address LocalAddress()
    {
        return new Ipv4Address(192, 0, 2, 44);
    }

    private static Ipv4Address RemoteAddress()
    {
        return new Ipv4Address(198, 51, 100, 33);
    }
}
