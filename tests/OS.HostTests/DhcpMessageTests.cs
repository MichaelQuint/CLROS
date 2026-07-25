using System.Runtime.CompilerServices;
using OS.Net;
using OS.Std.Binary;

internal static class DhcpMessageTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.dhcp writes discover message", TestWritesDiscoverMessage, ref failures);
        Run("net.dhcp parses offer options", TestParsesOfferOptions, ref failures);
        Run("net.dhcp writes request message", TestWritesRequestMessage, ref failures);
        Run("net.dhcp rejects malformed packets", TestRejectsMalformedPackets, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("DHCP message host tests failed: " + failures);
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

    private static bool TestWritesDiscoverMessage()
    {
        Span<byte> packet = stackalloc byte[256];
        MacAddress clientMac = CreateClientMac();

        DhcpMessageStatus status = DhcpMessage.TryWriteDiscover(
            packet,
            0x12345678,
            clientMac,
            out int bytesWritten);

        return status == DhcpMessageStatus.Success
            && bytesWritten == 251
            && packet[0] == DhcpMessage.OpBootRequest
            && packet[1] == DhcpMessage.HardwareTypeEthernet
            && packet[2] == DhcpMessage.HardwareAddressLengthEthernet
            && EndianPrimitives.ReadUInt32BigEndian(packet.Slice(4, 4)) == 0x12345678
            && EndianPrimitives.ReadUInt16BigEndian(packet.Slice(10, 2)) == 0x8000
            && ClientMacMatches(packet, clientMac)
            && EndianPrimitives.ReadUInt32BigEndian(packet.Slice(DhcpMessage.MagicCookieOffset, 4)) == DhcpMessage.MagicCookie
            && packet[DhcpMessage.OptionsOffset] == DhcpMessage.OptionDhcpMessageType
            && packet[DhcpMessage.OptionsOffset + 1] == 1
            && packet[DhcpMessage.OptionsOffset + 2] == DhcpMessage.MessageTypeDiscover
            && packet[DhcpMessage.OptionsOffset + 3] == DhcpMessage.OptionParameterRequestList
            && packet[DhcpMessage.OptionsOffset + 4] == 5
            && packet[DhcpMessage.OptionsOffset + 5] == DhcpMessage.OptionSubnetMask
            && packet[DhcpMessage.OptionsOffset + 6] == DhcpMessage.OptionRouter
            && packet[DhcpMessage.OptionsOffset + 7] == DhcpMessage.OptionDomainNameServer
            && packet[DhcpMessage.OptionsOffset + 8] == DhcpMessage.OptionIpAddressLeaseTime
            && packet[DhcpMessage.OptionsOffset + 9] == DhcpMessage.OptionServerIdentifier
            && packet[DhcpMessage.OptionsOffset + 10] == DhcpMessage.OptionEnd;
    }

    private static bool TestParsesOfferOptions()
    {
        Span<byte> packet = stackalloc byte[300];
        int bytesWritten = WriteReply(
            packet,
            DhcpMessage.MessageTypeOffer,
            0xAABBCCDD,
            CreateClientMac(),
            new Ipv4Address(192, 0, 2, 42),
            includeFullOptions: true);

        DhcpMessageStatus status = DhcpMessage.TryParse(
            packet.Slice(0, bytesWritten),
            out DhcpMessage parsed);

        return status == DhcpMessageStatus.Success
            && parsed.MessageType == DhcpMessage.MessageTypeOffer
            && parsed.TransactionId == 0xAABBCCDD
            && parsed.ClientMac == CreateClientMac()
            && parsed.YourAddress == new Ipv4Address(192, 0, 2, 42)
            && parsed.HasServerIdentifier
            && parsed.ServerIdentifier == new Ipv4Address(192, 0, 2, 1)
            && parsed.HasLeaseTime
            && parsed.LeaseTimeSeconds == 3600
            && parsed.HasSubnetMask
            && parsed.SubnetMask == new Ipv4Address(255, 255, 255, 0)
            && parsed.HasRouter
            && parsed.Router == new Ipv4Address(192, 0, 2, 1)
            && parsed.HasDnsServer
            && parsed.DnsServer == new Ipv4Address(198, 51, 100, 53);
    }

    private static bool TestWritesRequestMessage()
    {
        Span<byte> packet = stackalloc byte[300];
        MacAddress clientMac = CreateClientMac();

        DhcpMessageStatus status = DhcpMessage.TryWriteRequest(
            packet,
            0x01020304,
            clientMac,
            new Ipv4Address(192, 0, 2, 42),
            new Ipv4Address(192, 0, 2, 1),
            out int bytesWritten);

        int options = DhcpMessage.OptionsOffset;
        return status == DhcpMessageStatus.Success
            && bytesWritten == 263
            && packet[0] == DhcpMessage.OpBootRequest
            && packet[1] == DhcpMessage.HardwareTypeEthernet
            && packet[2] == DhcpMessage.HardwareAddressLengthEthernet
            && EndianPrimitives.ReadUInt32BigEndian(packet.Slice(4, 4)) == 0x01020304
            && ClientMacMatches(packet, clientMac)
            && packet[options] == DhcpMessage.OptionDhcpMessageType
            && packet[options + 2] == DhcpMessage.MessageTypeRequest
            && packet[options + 3] == DhcpMessage.OptionRequestedIpAddress
            && packet[options + 4] == 4
            && Ipv4At(packet, options + 5) == new Ipv4Address(192, 0, 2, 42)
            && packet[options + 9] == DhcpMessage.OptionServerIdentifier
            && packet[options + 10] == 4
            && Ipv4At(packet, options + 11) == new Ipv4Address(192, 0, 2, 1)
            && packet[bytesWritten - 1] == DhcpMessage.OptionEnd;
    }

    private static bool TestRejectsMalformedPackets()
    {
        Span<byte> packet = stackalloc byte[300];
        int bytesWritten = WriteReply(
            packet,
            DhcpMessage.MessageTypeOffer,
            0x11223344,
            CreateClientMac(),
            new Ipv4Address(192, 0, 2, 42),
            includeFullOptions: false);

        DhcpMessageStatus truncatedStatus = DhcpMessage.TryParse(
            packet.Slice(0, DhcpMessage.MinimumMessageLength - 1),
            out DhcpMessage _);

        packet[DhcpMessage.MagicCookieOffset] = 0;
        DhcpMessageStatus cookieStatus = DhcpMessage.TryParse(
            packet.Slice(0, bytesWritten),
            out DhcpMessage _);
        packet[DhcpMessage.MagicCookieOffset] = 0x63;

        packet[1] = 6;
        DhcpMessageStatus hardwareStatus = DhcpMessage.TryParse(
            packet.Slice(0, bytesWritten),
            out DhcpMessage _);
        packet[1] = DhcpMessage.HardwareTypeEthernet;

        packet[2] = 5;
        DhcpMessageStatus hardwareLengthStatus = DhcpMessage.TryParse(
            packet.Slice(0, bytesWritten),
            out DhcpMessage _);
        packet[2] = DhcpMessage.HardwareAddressLengthEthernet;

        int messageTypeLengthOffset = DhcpMessage.OptionsOffset + 1;
        packet[messageTypeLengthOffset] = 2;
        DhcpMessageStatus malformedOptionStatus = DhcpMessage.TryParse(
            packet.Slice(0, bytesWritten),
            out DhcpMessage _);
        packet[messageTypeLengthOffset] = 1;

        packet[DhcpMessage.OptionsOffset + 2] = DhcpMessage.MessageTypeRequest;
        DhcpMessageStatus unsupportedMessageTypeStatus = DhcpMessage.TryParse(
            packet.Slice(0, bytesWritten),
            out DhcpMessage _);

        return truncatedStatus == DhcpMessageStatus.TruncatedFixedHeader
            && cookieStatus == DhcpMessageStatus.MissingMagicCookie
            && hardwareStatus == DhcpMessageStatus.UnsupportedHardwareType
            && hardwareLengthStatus == DhcpMessageStatus.UnsupportedHardwareAddressLength
            && malformedOptionStatus == DhcpMessageStatus.MalformedOptions
            && unsupportedMessageTypeStatus == DhcpMessageStatus.UnsupportedMessageType;
    }

    private static int WriteReply(
        Span<byte> packet,
        byte messageType,
        uint transactionId,
        MacAddress clientMac,
        Ipv4Address yourAddress,
        bool includeFullOptions)
    {
        packet.Clear();
        packet[0] = DhcpMessage.OpBootReply;
        packet[1] = DhcpMessage.HardwareTypeEthernet;
        packet[2] = DhcpMessage.HardwareAddressLengthEthernet;
        EndianPrimitives.WriteUInt32BigEndian(packet.Slice(4, 4), transactionId);
        yourAddress.TryWrite(packet, 16);
        clientMac.TryWrite(packet, DhcpMessage.ClientHardwareAddressOffset);
        EndianPrimitives.WriteUInt32BigEndian(packet.Slice(DhcpMessage.MagicCookieOffset, 4), DhcpMessage.MagicCookie);

        int offset = DhcpMessage.OptionsOffset;
        packet[offset++] = DhcpMessage.OptionDhcpMessageType;
        packet[offset++] = 1;
        packet[offset++] = messageType;

        if (includeFullOptions)
        {
            packet[offset++] = DhcpMessage.OptionServerIdentifier;
            packet[offset++] = 4;
            new Ipv4Address(192, 0, 2, 1).TryWrite(packet, offset);
            offset += 4;

            packet[offset++] = DhcpMessage.OptionIpAddressLeaseTime;
            packet[offset++] = 4;
            EndianPrimitives.WriteUInt32BigEndian(packet.Slice(offset, 4), 3600);
            offset += 4;

            packet[offset++] = DhcpMessage.OptionSubnetMask;
            packet[offset++] = 4;
            new Ipv4Address(255, 255, 255, 0).TryWrite(packet, offset);
            offset += 4;

            packet[offset++] = DhcpMessage.OptionRouter;
            packet[offset++] = 4;
            new Ipv4Address(192, 0, 2, 1).TryWrite(packet, offset);
            offset += 4;

            packet[offset++] = DhcpMessage.OptionDomainNameServer;
            packet[offset++] = 8;
            new Ipv4Address(198, 51, 100, 53).TryWrite(packet, offset);
            offset += 4;
            new Ipv4Address(198, 51, 100, 54).TryWrite(packet, offset);
            offset += 4;
        }

        packet[offset++] = DhcpMessage.OptionEnd;
        return offset;
    }

    private static bool ClientMacMatches(ReadOnlySpan<byte> packet, MacAddress expected)
    {
        MacAddress.TryRead(packet, DhcpMessage.ClientHardwareAddressOffset, out MacAddress actual);
        return actual == expected;
    }

    private static Ipv4Address Ipv4At(ReadOnlySpan<byte> packet, int offset)
    {
        Ipv4Address.TryRead(packet, offset, out Ipv4Address address);
        return address;
    }

    private static MacAddress CreateClientMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x10);
    }
}
