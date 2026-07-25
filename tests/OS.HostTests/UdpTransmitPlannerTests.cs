using System.Runtime.CompilerServices;
using OS.Net;

internal static class UdpTransmitPlannerTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.udp.tx writes ethernet ipv4 udp frame", TestWritesEthernetIpv4UdpFrame, ref failures);
        Run("net.udp.tx writes nonzero checksum", TestWritesNonZeroChecksum, ref failures);
        Run("net.udp.tx checksum validates with pseudo header", TestChecksumValidatesWithPseudoHeader, ref failures);
        Run("net.udp.tx preserves addressing metadata", TestPreservesAddressingMetadata, ref failures);
        Run("net.udp.tx rejects invalid local identity", TestRejectsInvalidLocalIdentity, ref failures);
        Run("net.udp.tx rejects invalid destination identity", TestRejectsInvalidDestinationIdentity, ref failures);
        Run("net.udp.tx rejects short frame buffer", TestRejectsShortFrameBuffer, ref failures);
        Run("net.udp.tx rejects oversized payload", TestRejectsOversizedPayload, ref failures);
        Run("net.udp.tx default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("UDP transmit planner host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

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
            return false;
        }

        PacketParseStatus parseStatus = EthernetIpv4UdpPacket.TryParse(
            frame,
            out EthernetIpv4UdpPacketInfo parsed,
            out ReadOnlySpan<byte> parsedPayload);

        return parseStatus == PacketParseStatus.Success
            && parsed.DestinationMac == RemoteMac
            && parsed.SourceMac == LocalMac
            && parsed.SourceAddress == LocalAddress()
            && parsed.DestinationAddress == RemoteAddress()
            && parsed.SourcePort == 49152
            && parsed.DestinationPort == 53
            && parsed.TimeToLive == 48
            && parsed.Identification == 0x1133
            && parsedPayload.SequenceEqual(payload);
    }

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

        return status == UdpTransmitPlanStatus.Success
            && result.Succeeded
            && result.BytesWritten == frame.Length
            && parseStatus == PacketParseStatus.Success
            && udp.Checksum != 0
            && parsedPayload.SequenceEqual(payload);
    }

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

        return status == UdpTransmitPlanStatus.Success
            && result.Succeeded
            && result.BytesWritten == frame.Length
            && parseStatus == PacketParseStatus.Success
            && udp.Checksum != 0
            && parsedPayload.SequenceEqual(payload)
            && checksumStatus == Ipv4TransportChecksumStatus.Success
            && checksumResult.IsValid
            && checksumResult.TransportLength == UdpHeader.HeaderLength + payload.Length;
    }

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

        return status == UdpTransmitPlanStatus.Success
            && result.LocalMac == LocalMac
            && result.LocalAddress == LocalAddress()
            && result.DestinationMac == RemoteMac
            && result.DestinationAddress == RemoteAddress()
            && result.SourcePort == 5353
            && result.DestinationPort == 5355
            && result.TimeToLive == 64
            && result.Identification == 0xCAFE
            && result.PayloadLength == payload.Length
            && result.BytesWritten == frame.Length;
    }

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

        return zeroMacStatus == UdpTransmitPlanStatus.InvalidLocalIdentity
            && zeroMacResult.PacketStatus == PacketParseStatus.Success
            && zeroMacResult.BytesWritten == 0
            && zeroPortStatus == UdpTransmitPlanStatus.InvalidLocalIdentity
            && zeroPortResult.SourcePort == 0
            && frame[0] == PoisonByte
            && frame[^1] == PoisonByte;
    }

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

        return zeroMacStatus == UdpTransmitPlanStatus.InvalidDestinationIdentity
            && zeroMacResult.PacketStatus == PacketParseStatus.Success
            && zeroMacResult.BytesWritten == 0
            && zeroPortStatus == UdpTransmitPlanStatus.InvalidDestinationIdentity
            && zeroPortResult.DestinationPort == 0
            && frame[0] == PoisonByte
            && frame[^1] == PoisonByte;
    }

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

        return status == UdpTransmitPlanStatus.FrameBufferTooSmall
            && !result.Succeeded
            && result.PacketStatus == PacketParseStatus.BufferTooSmall
            && result.BytesWritten == 0
            && result.PayloadLength == payload.Length
            && frame[0] == PoisonByte
            && frame[^1] == PoisonByte;
    }

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

        return status == UdpTransmitPlanStatus.PayloadTooLarge
            && !result.Succeeded
            && result.PacketStatus == PacketParseStatus.InvalidLength
            && result.BytesWritten == 0
            && result.PayloadLength == payload.Length
            && result.LocalMac == LocalMac
            && result.DestinationMac == RemoteMac
            && frame[0] == PoisonByte
            && frame[^1] == PoisonByte;
    }

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

        return !defaultResult.IsInitialized
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
            && !RuntimeHelpers.IsReferenceOrContainsReferences<UdpTransmitPlanResult>();
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
