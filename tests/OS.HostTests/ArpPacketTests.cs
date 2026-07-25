using System.Runtime.CompilerServices;
using OS.Net;

internal static class ArpPacketTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.packet.arp.request-round-trip", TestArpRequestRoundTrip, ref failures);
        Run("net.packet.arp.reply-round-trip", TestArpReplyRoundTrip, ref failures);
        Run("net.packet.arp.reject-unsupported-hardware-type", TestUnsupportedHardwareType, ref failures);
        Run("net.packet.arp.reject-unsupported-protocol-type", TestUnsupportedProtocolType, ref failures);
        Run("net.packet.arp.reject-invalid-address-lengths", TestInvalidAddressLengths, ref failures);
        Run("net.packet.arp.reject-unsupported-opcode", TestUnsupportedOperation, ref failures);
        Run("net.packet.arp.reject-truncated-packet", TestTruncatedPacket, ref failures);
        Run("net.packet.ethernet.dispatch-arp-and-ipv4", TestEthernetDispatchRecognizesArpAndIpv4, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("ARP packet host tests failed: " + failures);
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

    private static bool TestArpRequestRoundTrip()
    {
        ArpPacket request = CreateRequest();
        byte[] packet = new byte[ArpPacket.PacketLength];
        byte[] expected =
        {
            0x00, 0x01,
            0x08, 0x00,
            0x06,
            0x04,
            0x00, 0x01,
            0x02, 0x00, 0x00, 0x00, 0x00, 0x01,
            0xC0, 0x00, 0x02, 0x0A,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0xC0, 0x00, 0x02, 0x01
        };

        PacketParseStatus writeStatus = ArpPacket.TryWrite(packet, request, out int bytesWritten);
        if (writeStatus != PacketParseStatus.Success
            || bytesWritten != expected.Length
            || !packet.SequenceEqual(expected))
        {
            return false;
        }

        PacketParseStatus parseStatus = ArpPacket.TryParse(packet, out ArpPacket parsed);
        return parseStatus == PacketParseStatus.Success
            && parsed.Operation == request.Operation
            && parsed.SenderMac == request.SenderMac
            && parsed.SenderAddress == request.SenderAddress
            && parsed.TargetMac == request.TargetMac
            && parsed.TargetAddress == request.TargetAddress;
    }

    private static bool TestArpReplyRoundTrip()
    {
        ArpPacket reply = CreateReply();
        byte[] packet = new byte[ArpPacket.PacketLength];
        byte[] expected =
        {
            0x00, 0x01,
            0x08, 0x00,
            0x06,
            0x04,
            0x00, 0x02,
            0x02, 0x00, 0x00, 0x00, 0x00, 0x02,
            0xC0, 0x00, 0x02, 0x01,
            0x02, 0x00, 0x00, 0x00, 0x00, 0x01,
            0xC0, 0x00, 0x02, 0x0A
        };

        PacketParseStatus writeStatus = ArpPacket.TryWrite(packet, reply, out int bytesWritten);
        if (writeStatus != PacketParseStatus.Success
            || bytesWritten != expected.Length
            || !packet.SequenceEqual(expected))
        {
            return false;
        }

        PacketParseStatus parseStatus = ArpPacket.TryParse(packet, out ArpPacket parsed);
        return parseStatus == PacketParseStatus.Success
            && parsed.Operation == reply.Operation
            && parsed.SenderMac == reply.SenderMac
            && parsed.SenderAddress == reply.SenderAddress
            && parsed.TargetMac == reply.TargetMac
            && parsed.TargetAddress == reply.TargetAddress;
    }

    private static bool TestUnsupportedHardwareType()
    {
        byte[] packet = CreateRequestBytes();
        packet[0] = 0x00;
        packet[1] = 0x06;

        PacketParseStatus status = ArpPacket.TryParse(packet, out ArpPacket _);

        return status == PacketParseStatus.UnsupportedHardwareType;
    }

    private static bool TestUnsupportedProtocolType()
    {
        byte[] packet = CreateRequestBytes();
        packet[2] = 0x86;
        packet[3] = 0xDD;

        PacketParseStatus status = ArpPacket.TryParse(packet, out ArpPacket _);

        return status == PacketParseStatus.UnsupportedProtocolType;
    }

    private static bool TestInvalidAddressLengths()
    {
        byte[] invalidHardwareLength = CreateRequestBytes();
        invalidHardwareLength[4] = 0x05;
        PacketParseStatus hardwareStatus = ArpPacket.TryParse(invalidHardwareLength, out ArpPacket _);

        byte[] invalidProtocolLength = CreateRequestBytes();
        invalidProtocolLength[5] = 0x10;
        PacketParseStatus protocolStatus = ArpPacket.TryParse(invalidProtocolLength, out ArpPacket _);

        return hardwareStatus == PacketParseStatus.UnsupportedHardwareAddressLength
            && protocolStatus == PacketParseStatus.UnsupportedProtocolAddressLength;
    }

    private static bool TestUnsupportedOperation()
    {
        byte[] packet = CreateRequestBytes();
        packet[6] = 0x00;
        packet[7] = 0x03;

        PacketParseStatus status = ArpPacket.TryParse(packet, out ArpPacket _);

        return status == PacketParseStatus.UnsupportedOperation;
    }

    private static bool TestTruncatedPacket()
    {
        byte[] packet = CreateRequestBytes();

        PacketParseStatus status = ArpPacket.TryParse(packet.AsSpan(0, packet.Length - 1), out ArpPacket _);

        return status == PacketParseStatus.BufferTooSmall;
    }

    private static bool TestEthernetDispatchRecognizesArpAndIpv4()
    {
        byte[] arpFrame = CreateArpFrame();
        PacketParseStatus arpStatus = EthernetHeader.TryDispatch(
            arpFrame,
            out EthernetHeader arpHeader,
            out EthernetPayloadKind arpKind,
            out ReadOnlySpan<byte> arpPayload);

        byte[] ipv4Frame = CreateIpv4Frame();
        PacketParseStatus ipv4Status = EthernetHeader.TryDispatch(
            ipv4Frame,
            out EthernetHeader ipv4Header,
            out EthernetPayloadKind ipv4Kind,
            out ReadOnlySpan<byte> ipv4Payload);

        return arpStatus == PacketParseStatus.Success
            && arpHeader.EtherType == EthernetHeader.EtherTypeArp
            && arpKind == EthernetPayloadKind.Arp
            && arpPayload.Length == ArpPacket.PacketLength
            && ipv4Status == PacketParseStatus.Success
            && ipv4Header.EtherType == EthernetHeader.EtherTypeIpv4
            && ipv4Kind == EthernetPayloadKind.Ipv4
            && ipv4Payload.Length == Ipv4Header.MinHeaderLength + UdpHeader.HeaderLength + 4;
    }

    private static ArpPacket CreateRequest()
    {
        return new ArpPacket(
            ArpPacket.OperationRequest,
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x01),
            new Ipv4Address(192, 0, 2, 10),
            new MacAddress(0x00, 0x00, 0x00, 0x00, 0x00, 0x00),
            new Ipv4Address(192, 0, 2, 1));
    }

    private static ArpPacket CreateReply()
    {
        return new ArpPacket(
            ArpPacket.OperationReply,
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x02),
            new Ipv4Address(192, 0, 2, 1),
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x01),
            new Ipv4Address(192, 0, 2, 10));
    }

    private static byte[] CreateRequestBytes()
    {
        byte[] packet = new byte[ArpPacket.PacketLength];
        PacketParseStatus status = ArpPacket.TryWrite(packet, CreateRequest(), out int bytesWritten);
        if (status != PacketParseStatus.Success || bytesWritten != packet.Length)
        {
            throw new InvalidOperationException("Failed to build deterministic ARP request.");
        }

        return packet;
    }

    private static byte[] CreateArpFrame()
    {
        byte[] frame = new byte[EthernetHeader.HeaderLength + ArpPacket.PacketLength];
        EthernetHeader header = new EthernetHeader(
            new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF),
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x01),
            EthernetHeader.EtherTypeArp);

        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(frame, header);
        PacketParseStatus arpStatus = ArpPacket.TryWrite(
            frame.AsSpan(EthernetHeader.HeaderLength, ArpPacket.PacketLength),
            CreateRequest(),
            out int arpBytesWritten);

        if (ethernetStatus != PacketParseStatus.Success
            || arpStatus != PacketParseStatus.Success
            || arpBytesWritten != ArpPacket.PacketLength)
        {
            throw new InvalidOperationException("Failed to build deterministic ARP frame.");
        }

        return frame;
    }

    private static byte[] CreateIpv4Frame()
    {
        EthernetIpv4UdpPacketInfo info = new EthernetIpv4UdpPacketInfo(
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x02),
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x01),
            new Ipv4Address(192, 0, 2, 10),
            new Ipv4Address(198, 51, 100, 20),
            sourcePort: 12345,
            destinationPort: 8080,
            timeToLive: 64,
            identification: 0x1234);
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67 };
        byte[] frame = new byte[EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + UdpHeader.HeaderLength + payload.Length];

        PacketParseStatus status = EthernetIpv4UdpPacket.TryWrite(frame, info, payload, out int bytesWritten);
        if (status != PacketParseStatus.Success || bytesWritten != frame.Length)
        {
            throw new InvalidOperationException("Failed to build deterministic IPv4 frame.");
        }

        return frame;
    }
}
