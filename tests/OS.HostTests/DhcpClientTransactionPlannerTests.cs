using System.Runtime.CompilerServices;
using OS.Net;
using OS.Std.Binary;

internal static class DhcpClientTransactionPlannerTests
{
    private const uint TransactionId = 0x50617221;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.dhcp.client.transaction writes discover frame", TestWritesDiscoverFrame, ref failures);
        Run("net.dhcp.client.transaction completes offer request ack", TestCompletesOfferRequestAck, ref failures);
        Run("net.dhcp.client.transaction default result is not success", TestDefaultResultIsNotSuccess, ref failures);
        Run("net.dhcp.client.transaction rejects wrong xid", TestRejectsWrongTransactionId, ref failures);
        Run("net.dhcp.client.transaction rejects invalid transition", TestRejectsInvalidTransition, ref failures);
        Run("net.dhcp.client.transaction rejects missing server id", TestRejectsMissingServerIdentifier, ref failures);
        Run("net.dhcp.client.transaction rejects malformed packet", TestRejectsMalformedPacket, ref failures);
        Run("net.dhcp.client.transaction rejects invalid lease config", TestRejectsInvalidLeaseConfiguration, ref failures);
        Run("net.dhcp.client mac validation uses shared helpers", TestMacValidationUsesSharedHelpers, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("DHCP transaction planner host tests failed: " + failures);
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

    private static bool TestWritesDiscoverFrame()
    {
        DhcpClientTransactionPlanner planner = CreatePlanner();
        byte[] discover = new byte[DhcpUdpFrame.DiscoverFrameLength];

        DhcpClientTransactionPlannerStatus status = planner.TryWriteDiscoverFrame(
            discover,
            out DhcpClientTransactionPlannerResult result);
        PacketParseStatus parseStatus = EthernetIpv4UdpPacket.TryParse(
            discover.AsSpan(0, result.BytesWritten),
            out EthernetIpv4UdpPacketInfo info,
            out ReadOnlySpan<byte> payload);

        return status == DhcpClientTransactionPlannerStatus.Success
            && result.Succeeded
            && result.State == DhcpClientState.DiscoverSent
            && planner.State == DhcpClientState.DiscoverSent
            && result.FrameStatus == DhcpUdpFrameStatus.Success
            && result.ClientStatus == DhcpClientStatus.Success
            && result.MessageStatus == DhcpMessageStatus.Success
            && result.PacketStatus == PacketParseStatus.Success
            && result.BytesWritten == DhcpUdpFrame.DiscoverFrameLength
            && parseStatus == PacketParseStatus.Success
            && info.SourceMac == ClientMac()
            && info.DestinationMac == DhcpUdpFrame.BroadcastMac()
            && info.SourceAddress == default
            && info.DestinationAddress == DhcpUdpFrame.BroadcastIpv4()
            && info.SourcePort == DhcpMessage.ClientPort
            && info.DestinationPort == DhcpMessage.ServerPort
            && payload.Length == DhcpUdpFrame.DiscoverPayloadLength
            && payload[DhcpMessage.OptionsOffset + 2] == DhcpMessage.MessageTypeDiscover;
    }

    private static bool TestCompletesOfferRequestAck()
    {
        DhcpClientTransactionPlanner planner = CreatePlanner();
        byte[] discover = new byte[DhcpUdpFrame.DiscoverFrameLength];
        if (planner.TryWriteDiscoverFrame(
                discover,
                out DhcpClientTransactionPlannerResult discoverResult)
            != DhcpClientTransactionPlannerStatus.Success
            || discoverResult.State != DhcpClientState.DiscoverSent)
        {
            return false;
        }

        byte[] offer = CreateServerReplyFrame(
            DhcpMessage.MessageTypeOffer,
            TransactionId,
            includeServerIdentifier: true,
            includeLeaseTime: true,
            corruptMagicCookie: false,
            invalidSubnetMask: false);
        DhcpClientTransactionPlannerStatus offerStatus =
            planner.ReceiveOfferFromEthernetFrame(
                offer,
                out DhcpClientTransactionPlannerResult offerResult);
        if (offerStatus != DhcpClientTransactionPlannerStatus.Success
            || !offerResult.Succeeded
            || offerResult.State != DhcpClientState.OfferReceived
            || planner.OfferedAddress != OfferedAddress()
            || planner.ServerIdentifier != ServerAddress()
            || offerResult.PayloadOffset != DhcpUdpFrame.FrameHeaderLength)
        {
            return false;
        }

        byte[] request = new byte[DhcpUdpFrame.RequestFrameLength];
        DhcpClientTransactionPlannerStatus requestStatus = planner.TryWriteRequestFrame(
            request,
            out DhcpClientTransactionPlannerResult requestResult);
        if (requestStatus != DhcpClientTransactionPlannerStatus.Success
            || requestResult.State != DhcpClientState.RequestSent
            || planner.State != DhcpClientState.RequestSent
            || !RequestFrameContainsExpectedOptions(request, requestResult.BytesWritten))
        {
            return false;
        }

        byte[] ack = CreateServerReplyFrame(
            DhcpMessage.MessageTypeAck,
            TransactionId,
            includeServerIdentifier: true,
            includeLeaseTime: true,
            corruptMagicCookie: false,
            invalidSubnetMask: false);
        DhcpClientTransactionPlannerStatus ackStatus = planner.ReceiveAckFromEthernetFrame(
            ack,
            out DhcpClientTransactionPlannerResult ackResult);

        return ackStatus == DhcpClientTransactionPlannerStatus.Success
            && ackResult.Succeeded
            && ackResult.State == DhcpClientState.Bound
            && planner.State == DhcpClientState.Bound
            && ackResult.LeaseStatus == DhcpLeaseConfigurationStatus.Success
            && ackResult.LeaseConfiguration.HasAddress
            && ackResult.LeaseConfiguration.Address == OfferedAddress()
            && ackResult.LeaseConfiguration.HasServerIdentifier
            && ackResult.LeaseConfiguration.ServerIdentifier == ServerAddress()
            && ackResult.LeaseConfiguration.HasLeaseTime
            && ackResult.LeaseConfiguration.LeaseTimeSeconds == 3600
            && ackResult.LeaseConfiguration.HasSubnetMask
            && ackResult.LeaseConfiguration.SubnetMask == SubnetMask()
            && ackResult.LeaseConfiguration.HasRouter
            && ackResult.LeaseConfiguration.Router == ServerAddress()
            && ackResult.LeaseConfiguration.HasDnsServer
            && ackResult.LeaseConfiguration.DnsServer == DnsAddress()
            && planner.LeaseConfiguration.Address == OfferedAddress();
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        DhcpClientTransactionPlannerResult defaultResult = default;

        DhcpClientTransactionPlanner successPlanner = CreatePlanner();
        byte[] discover = new byte[DhcpUdpFrame.DiscoverFrameLength];
        DhcpClientTransactionPlannerStatus successStatus =
            successPlanner.TryWriteDiscoverFrame(
                discover,
                out DhcpClientTransactionPlannerResult success);

        DhcpClientTransactionPlanner failurePlanner = CreatePlanner();
        byte[] request = new byte[DhcpUdpFrame.RequestFrameLength];
        DhcpClientTransactionPlannerStatus failureStatus =
            failurePlanner.TryWriteRequestFrame(
                request,
                out DhcpClientTransactionPlannerResult failure);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == DhcpClientTransactionPlannerStatus.Success
            && defaultResult.State == DhcpClientState.Initial
            && defaultResult.FrameStatus == DhcpUdpFrameStatus.Success
            && defaultResult.ClientStatus == DhcpClientStatus.Success
            && defaultResult.MessageStatus == DhcpMessageStatus.Success
            && defaultResult.PacketStatus == PacketParseStatus.Success
            && defaultResult.UdpStatus == UdpReceiveDispatchStatus.Success
            && defaultResult.LeaseStatus == DhcpLeaseConfigurationStatus.Success
            && defaultResult.TransactionId == 0
            && defaultResult.ClientMac == default
            && defaultResult.OfferedAddress == default
            && defaultResult.ServerIdentifier == default
            && defaultResult.LeaseConfiguration.Equals(default(DhcpLeaseConfiguration))
            && defaultResult.BytesWritten == 0
            && defaultResult.PayloadOffset == 0
            && defaultResult.PayloadLength == 0
            && successStatus == DhcpClientTransactionPlannerStatus.Success
            && success.IsInitialized
            && success.Succeeded
            && success.Status == DhcpClientTransactionPlannerStatus.Success
            && success.State == DhcpClientState.DiscoverSent
            && success.FrameStatus == DhcpUdpFrameStatus.Success
            && success.ClientStatus == DhcpClientStatus.Success
            && success.MessageStatus == DhcpMessageStatus.Success
            && success.PacketStatus == PacketParseStatus.Success
            && success.TransactionId == TransactionId
            && success.ClientMac == ClientMac()
            && success.BytesWritten == DhcpUdpFrame.DiscoverFrameLength
            && successPlanner.State == DhcpClientState.DiscoverSent
            && failureStatus == DhcpClientTransactionPlannerStatus.InvalidState
            && failure.IsInitialized
            && !failure.Succeeded
            && failure.Status == DhcpClientTransactionPlannerStatus.InvalidState
            && failure.State == DhcpClientState.Initial
            && failure.ClientStatus == DhcpClientStatus.InvalidState
            && failure.MessageStatus == DhcpMessageStatus.Success
            && failure.BytesWritten == 0
            && failurePlanner.State == DhcpClientState.Initial
            && !RuntimeHelpers.IsReferenceOrContainsReferences<DhcpClientTransactionPlannerResult>();
    }

    private static bool TestRejectsWrongTransactionId()
    {
        DhcpClientTransactionPlanner planner = CreatePlanner();
        byte[] discover = new byte[DhcpUdpFrame.DiscoverFrameLength];
        if (planner.TryWriteDiscoverFrame(
                discover,
                out DhcpClientTransactionPlannerResult _)
            != DhcpClientTransactionPlannerStatus.Success)
        {
            return false;
        }

        byte[] offer = CreateServerReplyFrame(
            DhcpMessage.MessageTypeOffer,
            TransactionId + 1,
            includeServerIdentifier: true,
            includeLeaseTime: true,
            corruptMagicCookie: false,
            invalidSubnetMask: false);

        DhcpClientTransactionPlannerStatus status = planner.ReceiveOfferFromEthernetFrame(
            offer,
            out DhcpClientTransactionPlannerResult result);

        return status == DhcpClientTransactionPlannerStatus.WrongTransactionId
            && !result.Succeeded
            && result.ClientStatus == DhcpClientStatus.TransactionIdMismatch
            && result.MessageStatus == DhcpMessageStatus.Success
            && result.State == DhcpClientState.Failed
            && planner.State == DhcpClientState.Failed;
    }

    private static bool TestRejectsInvalidTransition()
    {
        DhcpClientTransactionPlanner planner = CreatePlanner();
        byte[] request = new byte[DhcpUdpFrame.RequestFrameLength];

        DhcpClientTransactionPlannerStatus status = planner.TryWriteRequestFrame(
            request,
            out DhcpClientTransactionPlannerResult result);

        return status == DhcpClientTransactionPlannerStatus.InvalidState
            && !result.Succeeded
            && result.ClientStatus == DhcpClientStatus.InvalidState
            && result.State == DhcpClientState.Initial
            && result.BytesWritten == 0
            && planner.State == DhcpClientState.Initial;
    }

    private static bool TestRejectsMissingServerIdentifier()
    {
        DhcpClientTransactionPlanner planner = CreatePlanner();
        byte[] discover = new byte[DhcpUdpFrame.DiscoverFrameLength];
        if (planner.TryWriteDiscoverFrame(
                discover,
                out DhcpClientTransactionPlannerResult _)
            != DhcpClientTransactionPlannerStatus.Success)
        {
            return false;
        }

        byte[] offer = CreateServerReplyFrame(
            DhcpMessage.MessageTypeOffer,
            TransactionId,
            includeServerIdentifier: false,
            includeLeaseTime: true,
            corruptMagicCookie: false,
            invalidSubnetMask: false);

        DhcpClientTransactionPlannerStatus status = planner.ReceiveOfferFromEthernetFrame(
            offer,
            out DhcpClientTransactionPlannerResult result);

        return status == DhcpClientTransactionPlannerStatus.MissingServerIdentifier
            && !result.Succeeded
            && result.ClientStatus == DhcpClientStatus.MissingServerIdentifier
            && result.MessageStatus == DhcpMessageStatus.Success
            && result.State == DhcpClientState.Failed
            && planner.State == DhcpClientState.Failed;
    }

    private static bool TestRejectsMalformedPacket()
    {
        DhcpClientTransactionPlanner planner = CreatePlanner();
        byte[] discover = new byte[DhcpUdpFrame.DiscoverFrameLength];
        if (planner.TryWriteDiscoverFrame(
                discover,
                out DhcpClientTransactionPlannerResult _)
            != DhcpClientTransactionPlannerStatus.Success)
        {
            return false;
        }

        byte[] offer = CreateServerReplyFrame(
            DhcpMessage.MessageTypeOffer,
            TransactionId,
            includeServerIdentifier: true,
            includeLeaseTime: true,
            corruptMagicCookie: true,
            invalidSubnetMask: false);

        DhcpClientTransactionPlannerStatus status = planner.ReceiveOfferFromEthernetFrame(
            offer,
            out DhcpClientTransactionPlannerResult result);

        return status == DhcpClientTransactionPlannerStatus.MalformedPacket
            && !result.Succeeded
            && result.FrameStatus == DhcpUdpFrameStatus.DhcpMessageFailed
            && result.MessageStatus == DhcpMessageStatus.MissingMagicCookie
            && result.ClientStatus == DhcpClientStatus.Success
            && result.State == DhcpClientState.DiscoverSent
            && planner.State == DhcpClientState.DiscoverSent;
    }

    private static bool TestRejectsInvalidLeaseConfiguration()
    {
        DhcpClientTransactionPlanner planner = CreatePlanner();
        byte[] discover = new byte[DhcpUdpFrame.DiscoverFrameLength];
        if (planner.TryWriteDiscoverFrame(
                discover,
                out DhcpClientTransactionPlannerResult _)
            != DhcpClientTransactionPlannerStatus.Success)
        {
            return false;
        }

        byte[] offer = CreateServerReplyFrame(
            DhcpMessage.MessageTypeOffer,
            TransactionId,
            includeServerIdentifier: true,
            includeLeaseTime: true,
            corruptMagicCookie: false,
            invalidSubnetMask: false);
        if (planner.ReceiveOfferFromEthernetFrame(
                offer,
                out DhcpClientTransactionPlannerResult _)
            != DhcpClientTransactionPlannerStatus.Success)
        {
            return false;
        }

        byte[] request = new byte[DhcpUdpFrame.RequestFrameLength];
        if (planner.TryWriteRequestFrame(
                request,
                out DhcpClientTransactionPlannerResult _)
            != DhcpClientTransactionPlannerStatus.Success)
        {
            return false;
        }

        byte[] ack = CreateServerReplyFrame(
            DhcpMessage.MessageTypeAck,
            TransactionId,
            includeServerIdentifier: true,
            includeLeaseTime: true,
            corruptMagicCookie: false,
            invalidSubnetMask: true);

        DhcpClientTransactionPlannerStatus status = planner.ReceiveAckFromEthernetFrame(
            ack,
            out DhcpClientTransactionPlannerResult result);

        return status == DhcpClientTransactionPlannerStatus.LeaseConfigurationFailed
            && !result.Succeeded
            && result.ClientStatus == DhcpClientStatus.Success
            && result.LeaseStatus == DhcpLeaseConfigurationStatus.InvalidSubnetMask
            && result.State == DhcpClientState.RequestSent
            && planner.State == DhcpClientState.RequestSent
            && planner.LeaseConfiguration.Equals(default(DhcpLeaseConfiguration));
    }

    private static bool TestMacValidationUsesSharedHelpers()
    {
        DhcpClientTransactionPlanner zeroPlanner =
            DhcpClientTransactionPlanner.Create(TransactionId, default);
        DhcpClientTransactionPlannerStatus zeroStatus =
            zeroPlanner.TryWriteDiscoverFrame(
                new byte[DhcpUdpFrame.DiscoverFrameLength],
                out DhcpClientTransactionPlannerResult zeroResult);

        DhcpClientTransactionPlanner broadcastPlanner =
            DhcpClientTransactionPlanner.Create(TransactionId, BroadcastMac());
        DhcpClientTransactionPlannerStatus broadcastStatus =
            broadcastPlanner.TryWriteDiscoverFrame(
                new byte[DhcpUdpFrame.DiscoverFrameLength],
                out DhcpClientTransactionPlannerResult broadcastResult);

        string source = File.ReadAllText(Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "src",
            "OS.Kernel",
            "Net",
            "DhcpClientTransactionPlanner.cs"));

        return zeroStatus == DhcpClientTransactionPlannerStatus.InvalidClientIdentity
            && zeroResult.IsInitialized
            && !zeroResult.Succeeded
            && zeroResult.Status == DhcpClientTransactionPlannerStatus.InvalidClientIdentity
            && zeroResult.ClientMac.IsZero
            && broadcastStatus == DhcpClientTransactionPlannerStatus.InvalidClientIdentity
            && broadcastResult.IsInitialized
            && !broadcastResult.Succeeded
            && broadcastResult.Status == DhcpClientTransactionPlannerStatus.InvalidClientIdentity
            && broadcastResult.ClientMac.IsBroadcast
            && source.Contains("macAddress.IsZero", StringComparison.Ordinal)
            && source.Contains("macAddress.IsBroadcast", StringComparison.Ordinal)
            && !source.Contains("macAddress == default", StringComparison.Ordinal)
            && !source.Contains("Octet0 != 0xFF", StringComparison.Ordinal);
    }

    private static bool RequestFrameContainsExpectedOptions(byte[] frame, int bytesWritten)
    {
        PacketParseStatus parseStatus = EthernetIpv4UdpPacket.TryParse(
            frame.AsSpan(0, bytesWritten),
            out EthernetIpv4UdpPacketInfo info,
            out ReadOnlySpan<byte> payload);
        if (parseStatus != PacketParseStatus.Success
            || info.SourceMac != ClientMac()
            || info.DestinationMac != DhcpUdpFrame.BroadcastMac()
            || payload.Length != DhcpUdpFrame.RequestPayloadLength)
        {
            return false;
        }

        int options = DhcpMessage.OptionsOffset;
        return payload[options] == DhcpMessage.OptionDhcpMessageType
            && payload[options + 2] == DhcpMessage.MessageTypeRequest
            && payload[options + 3] == DhcpMessage.OptionRequestedIpAddress
            && Ipv4At(payload, options + 5) == OfferedAddress()
            && payload[options + 9] == DhcpMessage.OptionServerIdentifier
            && Ipv4At(payload, options + 11) == ServerAddress();
    }

    private static byte[] CreateServerReplyFrame(
        byte messageType,
        uint transactionId,
        bool includeServerIdentifier,
        bool includeLeaseTime,
        bool corruptMagicCookie,
        bool invalidSubnetMask)
    {
        Span<byte> payload = stackalloc byte[320];
        int payloadLength = WriteServerReplyPayload(
            payload,
            messageType,
            transactionId,
            includeServerIdentifier,
            includeLeaseTime,
            corruptMagicCookie,
            invalidSubnetMask);

        byte[] frame = new byte[DhcpUdpFrame.FrameHeaderLength + payloadLength];
        EthernetIpv4UdpPacketInfo info = new EthernetIpv4UdpPacketInfo(
            ClientMac(),
            ServerMac(),
            ServerAddress(),
            DhcpUdpFrame.BroadcastIpv4(),
            DhcpMessage.ServerPort,
            DhcpMessage.ClientPort,
            timeToLive: 64,
            identification: 0x217);

        PacketParseStatus status = EthernetIpv4UdpPacket.TryWrite(
            frame,
            info,
            payload.Slice(0, payloadLength),
            out int bytesWritten);
        if (status != PacketParseStatus.Success || bytesWritten != frame.Length)
        {
            throw new InvalidOperationException("Failed to build DHCP transaction planner fixture.");
        }

        return frame;
    }

    private static int WriteServerReplyPayload(
        Span<byte> payload,
        byte messageType,
        uint transactionId,
        bool includeServerIdentifier,
        bool includeLeaseTime,
        bool corruptMagicCookie,
        bool invalidSubnetMask)
    {
        payload.Clear();
        payload[0] = DhcpMessage.OpBootReply;
        payload[1] = DhcpMessage.HardwareTypeEthernet;
        payload[2] = DhcpMessage.HardwareAddressLengthEthernet;
        EndianPrimitives.WriteUInt32BigEndian(payload.Slice(4, 4), transactionId);
        OfferedAddress().TryWrite(payload, 16);
        ClientMac().TryWrite(payload, DhcpMessage.ClientHardwareAddressOffset);
        EndianPrimitives.WriteUInt32BigEndian(
            payload.Slice(DhcpMessage.MagicCookieOffset, 4),
            corruptMagicCookie ? 0U : DhcpMessage.MagicCookie);

        int offset = DhcpMessage.OptionsOffset;
        payload[offset++] = DhcpMessage.OptionDhcpMessageType;
        payload[offset++] = 1;
        payload[offset++] = messageType;

        if (includeServerIdentifier)
        {
            payload[offset++] = DhcpMessage.OptionServerIdentifier;
            payload[offset++] = 4;
            ServerAddress().TryWrite(payload, offset);
            offset += 4;
        }

        if (includeLeaseTime)
        {
            payload[offset++] = DhcpMessage.OptionIpAddressLeaseTime;
            payload[offset++] = 4;
            EndianPrimitives.WriteUInt32BigEndian(payload.Slice(offset, 4), 3600);
            offset += 4;
        }

        payload[offset++] = DhcpMessage.OptionSubnetMask;
        payload[offset++] = 4;
        (invalidSubnetMask ? new Ipv4Address(255, 0, 255, 0) : SubnetMask())
            .TryWrite(payload, offset);
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

    private static DhcpClientTransactionPlanner CreatePlanner()
    {
        return DhcpClientTransactionPlanner.Create(TransactionId, ClientMac());
    }

    private static Ipv4Address Ipv4At(ReadOnlySpan<byte> packet, int offset)
    {
        Ipv4Address.TryRead(packet, offset, out Ipv4Address address);
        return address;
    }

    private static MacAddress ClientMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x02, 0x17);
    }

    private static MacAddress ServerMac()
    {
        return new MacAddress(0x02, 0x00, 0x00, 0x00, 0x02, 0x67);
    }

    private static MacAddress BroadcastMac()
    {
        return new MacAddress(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF);
    }

    private static Ipv4Address OfferedAddress()
    {
        return new Ipv4Address(192, 0, 2, 217);
    }

    private static Ipv4Address ServerAddress()
    {
        return new Ipv4Address(192, 0, 2, 1);
    }

    private static Ipv4Address SubnetMask()
    {
        return new Ipv4Address(255, 255, 255, 0);
    }

    private static Ipv4Address DnsAddress()
    {
        return new Ipv4Address(198, 51, 100, 53);
    }
}
