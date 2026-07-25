using System.Runtime.CompilerServices;
using OS.Net;
using OS.Std.Binary;

internal static class DhcpClientTests
{
    private const uint TransactionId = 0x10203040;

    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("net.dhcp client transitions discover-offer-request-ack", TestDiscoverOfferRequestAck, ref failures);
        Run("net.dhcp client rejects mismatched xid", TestRejectsMismatchedTransactionId, ref failures);
        Run("net.dhcp client times out discover", TestTimesOutDiscover, ref failures);
        Run("net.dhcp client default result is not success", TestDefaultResultIsNotSuccess, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("DHCP client host tests failed: " + failures);
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

    private static bool TestDiscoverOfferRequestAck()
    {
        DhcpClientTransaction transaction = DhcpClientTransaction.Create(
            TransactionId,
            CreateClientMac());
        Span<byte> discover = stackalloc byte[300];

        DhcpClientStatus discoverStatus = transaction.TryWriteDiscover(
            discover,
            out DhcpClientTransitionResult discoverResult);
        if (discoverStatus != DhcpClientStatus.Success
            || !discoverResult.Succeeded
            || discoverResult.State != DhcpClientState.DiscoverSent
            || transaction.State != DhcpClientState.DiscoverSent
            || discoverResult.BytesWritten != 251
            || discover[DhcpMessage.OptionsOffset + 2] != DhcpMessage.MessageTypeDiscover)
        {
            return false;
        }

        Span<byte> offer = stackalloc byte[320];
        int offerBytes = WriteServerMessage(
            offer,
            DhcpMessage.MessageTypeOffer,
            TransactionId,
            CreateClientMac(),
            OfferedAddress(),
            ServerAddress(),
            includeLeaseTime: true);

        DhcpClientStatus offerStatus = transaction.ReceiveOffer(
            offer.Slice(0, offerBytes),
            out DhcpClientTransitionResult offerResult);
        if (offerStatus != DhcpClientStatus.Success
            || !offerResult.Succeeded
            || offerResult.State != DhcpClientState.OfferReceived
            || transaction.State != DhcpClientState.OfferReceived
            || transaction.OfferedAddress != OfferedAddress()
            || transaction.ServerIdentifier != ServerAddress())
        {
            return false;
        }

        Span<byte> request = stackalloc byte[300];
        DhcpClientStatus requestStatus = transaction.TryWriteRequest(
            request,
            out DhcpClientTransitionResult requestResult);
        if (requestStatus != DhcpClientStatus.Success
            || !requestResult.Succeeded
            || requestResult.State != DhcpClientState.RequestSent
            || transaction.State != DhcpClientState.RequestSent
            || requestResult.BytesWritten != 263
            || request[DhcpMessage.OptionsOffset + 2] != DhcpMessage.MessageTypeRequest
            || Ipv4At(request, DhcpMessage.OptionsOffset + 5) != OfferedAddress()
            || Ipv4At(request, DhcpMessage.OptionsOffset + 11) != ServerAddress())
        {
            return false;
        }

        Span<byte> ack = stackalloc byte[320];
        int ackBytes = WriteServerMessage(
            ack,
            DhcpMessage.MessageTypeAck,
            TransactionId,
            CreateClientMac(),
            OfferedAddress(),
            ServerAddress(),
            includeLeaseTime: true);

        DhcpClientStatus ackStatus = transaction.ReceiveAck(
            ack.Slice(0, ackBytes),
            out DhcpClientTransitionResult ackResult);

        return ackStatus == DhcpClientStatus.Success
            && ackResult.Succeeded
            && ackResult.State == DhcpClientState.Bound
            && transaction.State == DhcpClientState.Bound
            && transaction.Lease.Address == OfferedAddress()
            && transaction.Lease.ServerIdentifier == ServerAddress()
            && transaction.Lease.LeaseTimeSeconds == 3600
            && transaction.Lease.HasSubnetMask
            && transaction.Lease.SubnetMask == new Ipv4Address(255, 255, 255, 0)
            && transaction.Lease.HasRouter
            && transaction.Lease.Router == ServerAddress()
            && transaction.Lease.HasDnsServer
            && transaction.Lease.DnsServer == new Ipv4Address(198, 51, 100, 53);
    }

    private static bool TestRejectsMismatchedTransactionId()
    {
        DhcpClientTransaction transaction = DhcpClientTransaction.Create(
            TransactionId,
            CreateClientMac());
        Span<byte> discover = stackalloc byte[300];
        DhcpClientStatus discoverStatus = transaction.TryWriteDiscover(
            discover,
            out DhcpClientTransitionResult _);
        if (discoverStatus != DhcpClientStatus.Success)
        {
            return false;
        }

        Span<byte> offer = stackalloc byte[320];
        int offerBytes = WriteServerMessage(
            offer,
            DhcpMessage.MessageTypeOffer,
            TransactionId + 1,
            CreateClientMac(),
            OfferedAddress(),
            ServerAddress(),
            includeLeaseTime: true);

        DhcpClientStatus offerStatus = transaction.ReceiveOffer(
            offer.Slice(0, offerBytes),
            out DhcpClientTransitionResult offerResult);

        return offerStatus == DhcpClientStatus.TransactionIdMismatch
            && !offerResult.Succeeded
            && offerResult.State == DhcpClientState.Failed
            && offerResult.MessageStatus == DhcpMessageStatus.Success
            && transaction.State == DhcpClientState.Failed;
    }

    private static bool TestTimesOutDiscover()
    {
        DhcpClientTransaction transaction = DhcpClientTransaction.Create(
            TransactionId,
            CreateClientMac());
        Span<byte> discover = stackalloc byte[300];
        DhcpClientStatus discoverStatus = transaction.TryWriteDiscover(
            discover,
            out DhcpClientTransitionResult _);
        if (discoverStatus != DhcpClientStatus.Success)
        {
            return false;
        }

        DhcpClientStatus timeoutStatus = transaction.Timeout(out DhcpClientTransitionResult timeoutResult);

        return timeoutStatus == DhcpClientStatus.Timeout
            && !timeoutResult.Succeeded
            && timeoutResult.State == DhcpClientState.Failed
            && transaction.State == DhcpClientState.Failed;
    }

    private static bool TestDefaultResultIsNotSuccess()
    {
        DhcpClientTransitionResult defaultResult = default;
        DhcpClientTransitionResult failedResult =
            DhcpClientTransitionResult.Fail(
                DhcpClientStatus.InvalidState,
                DhcpClientState.Initial,
                DhcpMessageStatus.Success);

        return !defaultResult.IsInitialized
            && !defaultResult.Succeeded
            && defaultResult.Status == DhcpClientStatus.Success
            && failedResult.IsInitialized
            && !failedResult.Succeeded
            && failedResult.Status == DhcpClientStatus.InvalidState
            && failedResult.State == DhcpClientState.Initial
            && failedResult.MessageStatus == DhcpMessageStatus.Success
            && failedResult.BytesWritten == 0
            && !RuntimeHelpers.IsReferenceOrContainsReferences<
                DhcpClientTransitionResult>();
    }

    private static int WriteServerMessage(
        Span<byte> packet,
        byte messageType,
        uint transactionId,
        MacAddress clientMac,
        Ipv4Address yourAddress,
        Ipv4Address serverIdentifier,
        bool includeLeaseTime)
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

        packet[offset++] = DhcpMessage.OptionServerIdentifier;
        packet[offset++] = 4;
        serverIdentifier.TryWrite(packet, offset);
        offset += 4;

        if (includeLeaseTime)
        {
            packet[offset++] = DhcpMessage.OptionIpAddressLeaseTime;
            packet[offset++] = 4;
            EndianPrimitives.WriteUInt32BigEndian(packet.Slice(offset, 4), 3600);
            offset += 4;
        }

        packet[offset++] = DhcpMessage.OptionSubnetMask;
        packet[offset++] = 4;
        new Ipv4Address(255, 255, 255, 0).TryWrite(packet, offset);
        offset += 4;

        packet[offset++] = DhcpMessage.OptionRouter;
        packet[offset++] = 4;
        serverIdentifier.TryWrite(packet, offset);
        offset += 4;

        packet[offset++] = DhcpMessage.OptionDomainNameServer;
        packet[offset++] = 4;
        new Ipv4Address(198, 51, 100, 53).TryWrite(packet, offset);
        offset += 4;

        packet[offset++] = DhcpMessage.OptionEnd;
        return offset;
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

    private static Ipv4Address OfferedAddress()
    {
        return new Ipv4Address(192, 0, 2, 42);
    }

    private static Ipv4Address ServerAddress()
    {
        return new Ipv4Address(192, 0, 2, 1);
    }
}
