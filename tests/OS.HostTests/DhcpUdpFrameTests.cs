using System.Runtime.CompilerServices;
using OS.Drivers;
using OS.Net;
using OS.Std.Binary;

internal static class DhcpUdpFrameTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.dhcp.udp writes discover broadcast frame", TestWritesDiscoverBroadcastFrame, ref failures);
        Run("net.dhcp.udp writes request broadcast frame", TestWritesRequestBroadcastFrame, ref failures);
        Run("net.dhcp.udp extracts server reply payload", TestExtractsServerReplyPayload, ref failures);
        Run("net.dhcp.udp rejects wrong udp ports", TestRejectsWrongUdpPorts, ref failures);
        Run("net.dhcp.udp reports short frame buffer", TestReportsShortFrameBuffer, ref failures);
        Run("net.dhcp.udp reports malformed reply frame", TestReportsMalformedReplyFrame, ref failures);
        Run("net.dhcp.udp write default result is not success", TestWriteDefaultResultIsNotSuccess, ref failures);
        Run("net.dhcp.udp read default result is not success", TestReadDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("DHCP UDP frame host tests failed: " + failures);
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

    private static bool TestWritesDiscoverBroadcastFrame()
    {
        byte[] frame = new byte[DhcpUdpFrame.DiscoverFrameLength];

        DhcpUdpFrameWriteResult result = DhcpUdpFrame.TryWriteDiscover(
            frame,
            0x12345678,
            ClientMac(),
            out int bytesWritten);

        PacketParseStatus parseStatus = EthernetIpv4UdpPacket.TryParse(
            frame.AsSpan(0, bytesWritten),
            out EthernetIpv4UdpPacketInfo info,
            out ReadOnlySpan<byte> payload);

        return result.Succeeded
            && result.Status == DhcpUdpFrameStatus.Success
            && result.DhcpStatus == DhcpMessageStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.DhcpPayloadLength == DhcpUdpFrame.DiscoverPayloadLength
            && result.BytesWritten == DhcpUdpFrame.DiscoverFrameLength
            && bytesWritten == DhcpUdpFrame.DiscoverFrameLength
            && parseStatus == PacketParseStatus.Success
            && IsClientBroadcastPacket(info)
            && payload.Length == DhcpUdpFrame.DiscoverPayloadLength
            && payload[0] == DhcpMessage.OpBootRequest
            && EndianPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4)) == 0x12345678
            && payload[DhcpMessage.OptionsOffset] == DhcpMessage.OptionDhcpMessageType
            && payload[DhcpMessage.OptionsOffset + 2] == DhcpMessage.MessageTypeDiscover;
    }

    private static bool TestWritesRequestBroadcastFrame()
    {
        byte[] frame = new byte[DhcpUdpFrame.RequestFrameLength];
        Ipv4Address requestedAddress = new Ipv4Address(192, 0, 2, 44);
        Ipv4Address serverIdentifier = ServerAddress();

        DhcpUdpFrameWriteResult result = DhcpUdpFrame.TryWriteRequest(
            frame,
            0xAABBCCDD,
            ClientMac(),
            requestedAddress,
            serverIdentifier,
            out int bytesWritten);

        PacketParseStatus parseStatus = EthernetIpv4UdpPacket.TryParse(
            frame.AsSpan(0, bytesWritten),
            out EthernetIpv4UdpPacketInfo info,
            out ReadOnlySpan<byte> payload);

        int options = DhcpMessage.OptionsOffset;
        return result.Succeeded
            && result.DhcpPayloadLength == DhcpUdpFrame.RequestPayloadLength
            && result.BytesWritten == DhcpUdpFrame.RequestFrameLength
            && bytesWritten == DhcpUdpFrame.RequestFrameLength
            && parseStatus == PacketParseStatus.Success
            && IsClientBroadcastPacket(info)
            && payload.Length == DhcpUdpFrame.RequestPayloadLength
            && payload[0] == DhcpMessage.OpBootRequest
            && EndianPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4)) == 0xAABBCCDD
            && payload[options] == DhcpMessage.OptionDhcpMessageType
            && payload[options + 2] == DhcpMessage.MessageTypeRequest
            && payload[options + 3] == DhcpMessage.OptionRequestedIpAddress
            && Ipv4At(payload, options + 5) == requestedAddress
            && payload[options + 9] == DhcpMessage.OptionServerIdentifier
            && Ipv4At(payload, options + 11) == serverIdentifier;
    }

    private static bool TestExtractsServerReplyPayload()
    {
        byte[] frame = CreateServerReplyFrame(
            sourcePort: DhcpMessage.ServerPort,
            destinationPort: DhcpMessage.ClientPort,
            corruptMagicCookie: false,
            out int dhcpPayloadLength);
        byte[] receiveBuffer = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + frame.Length];
        frame.CopyTo(receiveBuffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers));

        DhcpUdpFrameReadResult rxResult =
            DhcpUdpFrame.TryParseServerReplyFromReceiveBuffer(receiveBuffer);
        DhcpUdpFrameReadResult frameResult =
            DhcpUdpFrame.TryParseServerReplyFromEthernetFrame(frame);

        return rxResult.Succeeded
            && rxResult.Status == DhcpUdpFrameStatus.Success
            && rxResult.DhcpStatus == DhcpMessageStatus.Success
            && rxResult.PacketStatus == PacketParseStatus.Success
            && rxResult.UdpStatus == UdpReceiveDispatchStatus.Success
            && rxResult.PayloadOffset == VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + DhcpUdpFrame.FrameHeaderLength
            && rxResult.PayloadLength == dhcpPayloadLength
            && receiveBuffer[rxResult.PayloadOffset] == DhcpMessage.OpBootReply
            && rxResult.PacketInfo.SourcePort == DhcpMessage.ServerPort
            && rxResult.PacketInfo.DestinationPort == DhcpMessage.ClientPort
            && rxResult.Message.MessageType == DhcpMessage.MessageTypeOffer
            && rxResult.Message.TransactionId == 0x10203040
            && rxResult.Message.ClientMac == ClientMac()
            && rxResult.Message.YourAddress == OfferedAddress()
            && rxResult.Message.HasServerIdentifier
            && rxResult.Message.ServerIdentifier == ServerAddress()
            && frameResult.Succeeded
            && frameResult.PayloadOffset == DhcpUdpFrame.FrameHeaderLength
            && frameResult.PayloadLength == dhcpPayloadLength
            && frameResult.Message.MessageType == DhcpMessage.MessageTypeOffer;
    }

    private static bool TestRejectsWrongUdpPorts()
    {
        byte[] frame = CreateServerReplyFrame(
            sourcePort: DhcpMessage.ClientPort,
            destinationPort: DhcpMessage.ServerPort,
            corruptMagicCookie: false,
            out int _);

        DhcpUdpFrameReadResult result =
            DhcpUdpFrame.TryParseServerReplyFromEthernetFrame(frame);

        return !result.Succeeded
            && result.Status == DhcpUdpFrameStatus.WrongUdpPorts
            && result.DhcpStatus == DhcpMessageStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.UdpStatus == UdpReceiveDispatchStatus.Success
            && result.PacketInfo.SourcePort == DhcpMessage.ClientPort
            && result.PacketInfo.DestinationPort == DhcpMessage.ServerPort
            && result.PayloadLength == 0;
    }

    private static bool TestReportsShortFrameBuffer()
    {
        byte[] frame = new byte[DhcpUdpFrame.DiscoverFrameLength - 1];

        DhcpUdpFrameWriteResult result = DhcpUdpFrame.TryWriteDiscover(
            frame,
            0x01020304,
            ClientMac(),
            out int bytesWritten);

        return !result.Succeeded
            && result.Status == DhcpUdpFrameStatus.PacketFailed
            && result.DhcpStatus == DhcpMessageStatus.BufferTooSmall
            && result.PacketStatus == PacketParseStatus.BufferTooSmall
            && result.DhcpPayloadLength == 0
            && result.BytesWritten == 0
            && bytesWritten == 0;
    }

    private static bool TestReportsMalformedReplyFrame()
    {
        byte[] frame = CreateServerReplyFrame(
            sourcePort: DhcpMessage.ServerPort,
            destinationPort: DhcpMessage.ClientPort,
            corruptMagicCookie: true,
            out int _);

        DhcpUdpFrameReadResult result =
            DhcpUdpFrame.TryParseServerReplyFromEthernetFrame(frame);

        return !result.Succeeded
            && result.Status == DhcpUdpFrameStatus.DhcpMessageFailed
            && result.DhcpStatus == DhcpMessageStatus.MissingMagicCookie
            && result.PacketStatus == PacketParseStatus.Success
            && result.UdpStatus == UdpReceiveDispatchStatus.Success
            && result.PayloadOffset == 0
            && result.PayloadLength == 0;
    }

    private static bool TestWriteDefaultResultIsNotSuccess()
    {
        DhcpUdpFrameWriteResult defaultResult = default;
        byte[] frame = new byte[DhcpUdpFrame.DiscoverFrameLength];
        DhcpUdpFrameWriteResult success = DhcpUdpFrame.TryWriteDiscover(
            frame,
            0x12345678,
            ClientMac(),
            out int bytesWritten);
        DhcpUdpFrameWriteResult failure = DhcpUdpFrame.TryWriteDiscover(
            new byte[DhcpUdpFrame.DiscoverFrameLength - 1],
            0x01020304,
            ClientMac(),
            out int failureBytesWritten);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == DhcpUdpFrameStatus.Success
            && defaultResult.DhcpStatus == DhcpMessageStatus.Success
            && defaultResult.PacketStatus == PacketParseStatus.Success
            && defaultResult.DhcpPayloadLength == 0
            && defaultResult.BytesWritten == 0
            && success.IsInitialized
            && success.Succeeded
            && success.Status == DhcpUdpFrameStatus.Success
            && success.DhcpStatus == DhcpMessageStatus.Success
            && success.PacketStatus == PacketParseStatus.Success
            && success.DhcpPayloadLength == DhcpUdpFrame.DiscoverPayloadLength
            && success.BytesWritten == DhcpUdpFrame.DiscoverFrameLength
            && bytesWritten == DhcpUdpFrame.DiscoverFrameLength
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == DhcpUdpFrameStatus.PacketFailed
            && failure.DhcpStatus == DhcpMessageStatus.BufferTooSmall
            && failure.PacketStatus == PacketParseStatus.BufferTooSmall
            && failure.DhcpPayloadLength == 0
            && failure.BytesWritten == 0
            && failureBytesWritten == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<DhcpUdpFrameWriteResult>();
    }

    private static bool TestReadDefaultResultIsNotSuccess()
    {
        DhcpUdpFrameReadResult defaultResult = default;
        byte[] frame = CreateServerReplyFrame(
            sourcePort: DhcpMessage.ServerPort,
            destinationPort: DhcpMessage.ClientPort,
            corruptMagicCookie: false,
            out int dhcpPayloadLength);
        byte[] receiveBuffer = new byte[VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + frame.Length];
        frame.CopyTo(receiveBuffer.AsSpan(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers));

        DhcpUdpFrameReadResult receiveSuccess =
            DhcpUdpFrame.TryParseServerReplyFromReceiveBuffer(receiveBuffer);
        DhcpUdpFrameReadResult frameSuccess =
            DhcpUdpFrame.TryParseServerReplyFromEthernetFrame(frame);
        byte[] wrongPortFrame = CreateServerReplyFrame(
            sourcePort: DhcpMessage.ClientPort,
            destinationPort: DhcpMessage.ServerPort,
            corruptMagicCookie: false,
            out int _);
        DhcpUdpFrameReadResult failure =
            DhcpUdpFrame.TryParseServerReplyFromEthernetFrame(wrongPortFrame);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == DhcpUdpFrameStatus.Success
            && defaultResult.DhcpStatus == DhcpMessageStatus.Success
            && defaultResult.PacketStatus == PacketParseStatus.Success
            && defaultResult.UdpStatus == UdpReceiveDispatchStatus.Success
            && !defaultResult.UdpResult.IsInitialized
            && defaultResult.PayloadOffset == 0
            && defaultResult.PayloadLength == 0
            && receiveSuccess.IsInitialized
            && receiveSuccess.Succeeded
            && receiveSuccess.UdpResult.IsInitialized
            && receiveSuccess.PayloadOffset == VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + DhcpUdpFrame.FrameHeaderLength
            && receiveSuccess.PayloadLength == dhcpPayloadLength
            && receiveSuccess.Message.MessageType == DhcpMessage.MessageTypeOffer
            && frameSuccess.IsInitialized
            && frameSuccess.Succeeded
            && !frameSuccess.UdpResult.IsInitialized
            && frameSuccess.PayloadOffset == DhcpUdpFrame.FrameHeaderLength
            && frameSuccess.PayloadLength == dhcpPayloadLength
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == DhcpUdpFrameStatus.WrongUdpPorts
            && failure.DhcpStatus == DhcpMessageStatus.Success
            && failure.PacketStatus == PacketParseStatus.Success
            && failure.UdpStatus == UdpReceiveDispatchStatus.Success
            && failure.PacketInfo.SourcePort == DhcpMessage.ClientPort
            && failure.PacketInfo.DestinationPort == DhcpMessage.ServerPort
            && failure.PayloadOffset == 0
            && failure.PayloadLength == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<DhcpUdpFrameReadResult>();
    }

    private static byte[] CreateServerReplyFrame(
        ushort sourcePort,
        ushort destinationPort,
        bool corruptMagicCookie,
        out int dhcpPayloadLength)
    {
        Span<byte> payload = stackalloc byte[300];
        dhcpPayloadLength = WriteReplyPayload(payload, corruptMagicCookie);

        byte[] frame = new byte[DhcpUdpFrame.FrameHeaderLength + dhcpPayloadLength];
        EthernetIpv4UdpPacketInfo info = new EthernetIpv4UdpPacketInfo(
            ClientMac(),
            ServerMac(),
            ServerAddress(),
            BroadcastIpv4(),
            sourcePort,
            destinationPort,
            timeToLive: 64,
            identification: 0x3344);

        PacketParseStatus status = EthernetIpv4UdpPacket.TryWrite(
            frame,
            info,
            payload.Slice(0, dhcpPayloadLength),
            out int bytesWritten);
        if (status != PacketParseStatus.Success || bytesWritten != frame.Length)
        {
            throw new InvalidOperationException("Failed to build deterministic DHCP reply frame.");
        }

        return frame;
    }

    private static int WriteReplyPayload(Span<byte> payload, bool corruptMagicCookie)
    {
        payload.Clear();
        payload[0] = DhcpMessage.OpBootReply;
        payload[1] = DhcpMessage.HardwareTypeEthernet;
        payload[2] = DhcpMessage.HardwareAddressLengthEthernet;
        EndianPrimitives.WriteUInt32BigEndian(payload.Slice(4, 4), 0x10203040);
        OfferedAddress().TryWrite(payload, 16);
        ClientMac().TryWrite(payload, DhcpMessage.ClientHardwareAddressOffset);
        EndianPrimitives.WriteUInt32BigEndian(
            payload.Slice(DhcpMessage.MagicCookieOffset, 4),
            corruptMagicCookie ? 0 : DhcpMessage.MagicCookie);

        int offset = DhcpMessage.OptionsOffset;
        payload[offset++] = DhcpMessage.OptionDhcpMessageType;
        payload[offset++] = 1;
        payload[offset++] = DhcpMessage.MessageTypeOffer;

        payload[offset++] = DhcpMessage.OptionServerIdentifier;
        payload[offset++] = 4;
        ServerAddress().TryWrite(payload, offset);
        offset += 4;

        payload[offset++] = DhcpMessage.OptionIpAddressLeaseTime;
        payload[offset++] = 4;
        EndianPrimitives.WriteUInt32BigEndian(payload.Slice(offset, 4), 3600);
        offset += 4;

        payload[offset++] = DhcpMessage.OptionSubnetMask;
        payload[offset++] = 4;
        new Ipv4Address(255, 255, 255, 0).TryWrite(payload, offset);
        offset += 4;

        payload[offset++] = DhcpMessage.OptionRouter;
        payload[offset++] = 4;
        ServerAddress().TryWrite(payload, offset);
        offset += 4;

        payload[offset++] = DhcpMessage.OptionDomainNameServer;
        payload[offset++] = 4;
        DnsAddress().TryWrite(payload, offset);
        offset += 4;

        payload[offset++] = DhcpMessage.OptionEnd;
        return offset;
    }

    private static bool IsClientBroadcastPacket(in EthernetIpv4UdpPacketInfo info)
    {
        return info.DestinationMac == DhcpUdpFrame.BroadcastMac()
            && info.SourceMac == ClientMac()
            && info.SourceAddress == new Ipv4Address(0, 0, 0, 0)
            && info.DestinationAddress == DhcpUdpFrame.BroadcastIpv4()
            && info.SourcePort == DhcpMessage.ClientPort
            && info.DestinationPort == DhcpMessage.ServerPort;
    }

    private static Ipv4Address Ipv4At(ReadOnlySpan<byte> packet, int offset)
    {
        Ipv4Address.TryRead(packet, offset, out Ipv4Address address);
        return address;
    }

    private static MacAddress ClientMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x68);
    }

    private static MacAddress ServerMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x00, 0x67);
    }

    private static Ipv4Address OfferedAddress()
    {
        return new Ipv4Address(192, 0, 2, 68);
    }

    private static Ipv4Address ServerAddress()
    {
        return new Ipv4Address(192, 0, 2, 1);
    }

    private static Ipv4Address DnsAddress()
    {
        return new Ipv4Address(198, 51, 100, 53);
    }

    private static Ipv4Address BroadcastIpv4()
    {
        return new Ipv4Address(255, 255, 255, 255);
    }
}
