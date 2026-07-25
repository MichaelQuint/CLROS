using System.Runtime.CompilerServices;
using OS.Net;

internal static class NetworkPacketTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("network packets round-trip ethernet ipv4 udp bytes", TestRoundTripEthernetIpv4UdpBytes, ref failures);
        Run("network packets round-trip ethernet ipv4 udp checksum", TestRoundTripEthernetIpv4UdpChecksum, ref failures);
        Run("network packets reject invalid ethernet frame length", TestInvalidEthernetFrameLength, ref failures);
        Run("network packets reject unsupported ethertype", TestUnsupportedEtherType, ref failures);
        Run("network packets reject unsupported ipv4 protocol", TestUnsupportedIpv4Protocol, ref failures);
        Run("network packets reject ipv4 options", TestUnsupportedIpv4Options, ref failures);
        Run("network packets reject bad ipv4 checksum", TestBadIpv4Checksum, ref failures);
        Run("network packets reject truncated ipv4 payload", TestTruncatedIpv4Payload, ref failures);
        Run("network packets reject truncated udp payload", TestTruncatedUdpPayload, ref failures);
        Run("net.macaddress exposes zero broadcast helpers", TestMacAddressZeroBroadcastHelpers, ref failures);
        Run(
            "net.packet udp icmp payloads use shared buffer operations",
            TestUdpAndIcmpPayloadCopiesUseSharedBufferOperations,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Network packet host tests failed: " + failures);
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

    private static bool TestRoundTripEthernetIpv4UdpBytes()
    {
        EthernetIpv4UdpPacketInfo info = CreateInfo();
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67 };
        byte[] frame = new byte[EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + UdpHeader.HeaderLength + payload.Length];

        PacketParseStatus writeStatus = EthernetIpv4UdpPacket.TryWrite(frame, info, payload, out int bytesWritten);
        byte[] expected =
        {
            0x02, 0x00, 0x00, 0x00, 0x00, 0x02,
            0x02, 0x00, 0x00, 0x00, 0x00, 0x01,
            0x08, 0x00,
            0x45, 0x00, 0x00, 0x20,
            0x12, 0x34, 0x00, 0x00,
            0x40, 0x11, 0x7C, 0x47,
            0xC0, 0x00, 0x02, 0x0A,
            0xC6, 0x33, 0x64, 0x14,
            0x30, 0x39, 0x1F, 0x90,
            0x00, 0x0C, 0xE4, 0xE9,
            0x70, 0x69, 0x6E, 0x67
        };

        if (writeStatus != PacketParseStatus.Success
            || bytesWritten != expected.Length
            || !frame.AsSpan(0, bytesWritten).SequenceEqual(expected))
        {
            return false;
        }

        PacketParseStatus parseStatus = EthernetIpv4UdpPacket.TryParse(
            frame,
            out EthernetIpv4UdpPacketInfo parsedInfo,
            out ReadOnlySpan<byte> parsedPayload);

        return parseStatus == PacketParseStatus.Success
            && parsedInfo.DestinationMac == info.DestinationMac
            && parsedInfo.SourceMac == info.SourceMac
            && parsedInfo.SourceAddress == info.SourceAddress
            && parsedInfo.DestinationAddress == info.DestinationAddress
            && parsedInfo.SourcePort == info.SourcePort
            && parsedInfo.DestinationPort == info.DestinationPort
            && parsedInfo.TimeToLive == info.TimeToLive
            && parsedInfo.Identification == info.Identification
            && parsedInfo.FlagsAndFragmentOffset == info.FlagsAndFragmentOffset
            && parsedPayload.SequenceEqual(payload);
    }

    private static bool TestRoundTripEthernetIpv4UdpChecksum()
    {
        EthernetIpv4UdpPacketInfo info = CreateInfo();
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67 };
        byte[] frame = new byte[
            EthernetHeader.HeaderLength +
            Ipv4Header.MinHeaderLength +
            UdpHeader.HeaderLength +
            payload.Length];

        PacketParseStatus writeStatus = EthernetIpv4UdpPacket.TryWrite(
            frame,
            info,
            payload,
            out int bytesWritten);
        PacketParseStatus ipv4Status = Ipv4Header.TryParse(
            frame.AsSpan(EthernetHeader.HeaderLength, Ipv4Header.MinHeaderLength + UdpHeader.HeaderLength + payload.Length),
            out Ipv4Header ipv4,
            out ReadOnlySpan<byte> udpDatagram);
        PacketParseStatus udpStatus = UdpHeader.TryParse(
            udpDatagram,
            out UdpHeader udp,
            out ReadOnlySpan<byte> parsedPayload);
        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4.Source,
            ipv4.Destination,
            Ipv4TransportChecksum.ProtocolUdp,
            udpDatagram,
            out Ipv4TransportChecksumResult checksumResult);

        return writeStatus == PacketParseStatus.Success
            && bytesWritten == frame.Length
            && ipv4Status == PacketParseStatus.Success
            && udpStatus == PacketParseStatus.Success
            && udp.Checksum == 0xE4E9
            && checksumStatus == Ipv4TransportChecksumStatus.Success
            && checksumResult.IsValid
            && parsedPayload.SequenceEqual(payload);
    }

    private static bool TestInvalidEthernetFrameLength()
    {
        byte[] frame = new byte[EthernetHeader.HeaderLength - 1];

        PacketParseStatus status = EthernetIpv4UdpPacket.TryParse(
            frame,
            out EthernetIpv4UdpPacketInfo _,
            out ReadOnlySpan<byte> _);

        return status == PacketParseStatus.BufferTooSmall;
    }

    private static bool TestUnsupportedEtherType()
    {
        byte[] frame = CreateFrame();
        frame[12] = 0x86;
        frame[13] = 0xDD;

        PacketParseStatus status = EthernetIpv4UdpPacket.TryParse(
            frame,
            out EthernetIpv4UdpPacketInfo _,
            out ReadOnlySpan<byte> _);

        return status == PacketParseStatus.UnsupportedEtherType;
    }

    private static bool TestUnsupportedIpv4Protocol()
    {
        byte[] frame = CreateFrame();
        frame[EthernetHeader.HeaderLength + 9] = 6;
        WriteIpv4Checksum(frame);

        PacketParseStatus status = EthernetIpv4UdpPacket.TryParse(
            frame,
            out EthernetIpv4UdpPacketInfo _,
            out ReadOnlySpan<byte> _);

        return status == PacketParseStatus.UnsupportedIpProtocol;
    }

    private static bool TestUnsupportedIpv4Options()
    {
        byte[] frame = CreateFrame();
        int ipv4Offset = EthernetHeader.HeaderLength;
        frame[ipv4Offset] = 0x46;
        frame[ipv4Offset + 2] = 0x00;
        frame[ipv4Offset + 3] = 0x24;
        WriteIpv4Checksum(frame);

        PacketParseStatus status = EthernetIpv4UdpPacket.TryParse(
            frame,
            out EthernetIpv4UdpPacketInfo _,
            out ReadOnlySpan<byte> _);

        return status == PacketParseStatus.UnsupportedIpHeaderLength;
    }

    private static bool TestBadIpv4Checksum()
    {
        byte[] frame = CreateFrame();
        frame[EthernetHeader.HeaderLength + 8] ^= 0x01;

        PacketParseStatus status = EthernetIpv4UdpPacket.TryParse(
            frame,
            out EthernetIpv4UdpPacketInfo _,
            out ReadOnlySpan<byte> _);

        return status == PacketParseStatus.BadChecksum;
    }

    private static bool TestTruncatedIpv4Payload()
    {
        byte[] frame = CreateFrame();

        PacketParseStatus status = EthernetIpv4UdpPacket.TryParse(
            frame.AsSpan(0, frame.Length - 1),
            out EthernetIpv4UdpPacketInfo _,
            out ReadOnlySpan<byte> _);

        return status == PacketParseStatus.TruncatedPayload;
    }

    private static bool TestTruncatedUdpPayload()
    {
        byte[] frame = CreateFrame();
        int udpLengthOffset = EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + 4;
        frame[udpLengthOffset] = 0x00;
        frame[udpLengthOffset + 1] = 0x0D;

        PacketParseStatus status = EthernetIpv4UdpPacket.TryParse(
            frame,
            out EthernetIpv4UdpPacketInfo _,
            out ReadOnlySpan<byte> _);

        return status == PacketParseStatus.TruncatedPayload;
    }

    private static bool TestMacAddressZeroBroadcastHelpers()
    {
        MacAddress zero = default;
        MacAddress allZero = new MacAddress(0, 0, 0, 0, 0, 0);
        MacAddress broadcast = new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
        MacAddress local = new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x01);
        MacAddress multicast = new MacAddress(0x01, 0x00, 0x5E, 0x00, 0x00, 0x01);
        MacAddress partialBroadcast = new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE);

        return zero.IsZero
            && allZero.IsZero
            && !zero.IsBroadcast
            && broadcast.IsBroadcast
            && !broadcast.IsZero
            && !local.IsZero
            && !local.IsBroadcast
            && !multicast.IsZero
            && !multicast.IsBroadcast
            && !partialBroadcast.IsZero
            && !partialBroadcast.IsBroadcast;
    }

    private static bool TestUdpAndIcmpPayloadCopiesUseSharedBufferOperations()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string udpSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Net",
            "UdpHeader.cs"));
        string icmpSource = File.ReadAllText(Path.Combine(
            root,
            "src",
            "OS.Kernel",
            "Net",
            "IcmpEchoPacket.cs"));

        return udpSource.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && udpSource.Contains(
                "BufferOperations.MemoryCopy(payload, datagram.Slice(HeaderLength, payload.Length))",
                StringComparison.Ordinal)
            && !udpSource.Contains(
                "payload.CopyTo(datagram.Slice(HeaderLength, payload.Length))",
                StringComparison.Ordinal)
            && icmpSource.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && icmpSource.Contains(
                "BufferOperations.MemoryCopy(payload, packet.Slice(HeaderLength, payload.Length))",
                StringComparison.Ordinal)
            && !icmpSource.Contains(
                "payload.CopyTo(packet.Slice(HeaderLength, payload.Length))",
                StringComparison.Ordinal);
    }

    private static EthernetIpv4UdpPacketInfo CreateInfo()
    {
        return new EthernetIpv4UdpPacketInfo(
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x02),
            new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x01),
            new Ipv4Address(192, 0, 2, 10),
            new Ipv4Address(198, 51, 100, 20),
            sourcePort: 12345,
            destinationPort: 8080,
            timeToLive: 64,
            identification: 0x1234);
    }

    private static byte[] CreateFrame()
    {
        EthernetIpv4UdpPacketInfo info = CreateInfo();
        byte[] payload = { 0x70, 0x69, 0x6E, 0x67 };
        byte[] frame = new byte[EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + UdpHeader.HeaderLength + payload.Length];

        PacketParseStatus status = EthernetIpv4UdpPacket.TryWrite(frame, info, payload, out int bytesWritten);
        if (status != PacketParseStatus.Success || bytesWritten != frame.Length)
        {
            throw new InvalidOperationException("Failed to build deterministic test frame.");
        }

        return frame;
    }

    private static void WriteIpv4Checksum(byte[] frame)
    {
        int offset = EthernetHeader.HeaderLength;
        frame[offset + 10] = 0;
        frame[offset + 11] = 0;
        ushort checksum = Ipv4Header.ComputeHeaderChecksum(frame.AsSpan(offset, Ipv4Header.MinHeaderLength));
        frame[offset + 10] = (byte)(checksum >> 8);
        frame[offset + 11] = (byte)checksum;
    }
}
