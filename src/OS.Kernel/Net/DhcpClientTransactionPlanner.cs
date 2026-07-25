namespace OS.Net;

public enum DhcpClientTransactionPlannerStatus
{
    Success = 0,
    InvalidClientIdentity = 1,
    InvalidState = 2,
    FrameBufferTooSmall = 3,
    FrameWriteFailed = 4,
    FrameReadFailed = 5,
    UdpReceiveFailed = 6,
    WrongUdpPorts = 7,
    MalformedPacket = 8,
    UnexpectedMessageType = 9,
    WrongTransactionId = 10,
    ClientHardwareAddressMismatch = 11,
    MissingServerIdentifier = 12,
    MissingOfferedAddress = 13,
    ServerIdentifierMismatch = 14,
    OfferedAddressMismatch = 15,
    MissingLeaseTime = 16,
    InvalidLeaseTime = 17,
    LeaseConfigurationFailed = 18,
    Timeout = 19
}

public readonly struct DhcpClientTransactionPlannerResult
{
    private DhcpClientTransactionPlannerResult(
        DhcpClientTransactionPlannerStatus status,
        DhcpClientState state,
        DhcpUdpFrameStatus frameStatus,
        DhcpClientStatus clientStatus,
        DhcpMessageStatus messageStatus,
        PacketParseStatus packetStatus,
        UdpReceiveDispatchStatus udpStatus,
        DhcpLeaseConfigurationStatus leaseStatus,
        uint transactionId,
        MacAddress clientMac,
        Ipv4Address offeredAddress,
        Ipv4Address serverIdentifier,
        DhcpLeaseConfiguration leaseConfiguration,
        int bytesWritten,
        int payloadOffset,
        int payloadLength)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        FrameStatus = frameStatus;
        ClientStatus = clientStatus;
        MessageStatus = messageStatus;
        PacketStatus = packetStatus;
        UdpStatus = udpStatus;
        LeaseStatus = leaseStatus;
        TransactionId = transactionId;
        ClientMac = clientMac;
        OfferedAddress = offeredAddress;
        ServerIdentifier = serverIdentifier;
        LeaseConfiguration = leaseConfiguration;
        BytesWritten = bytesWritten;
        PayloadOffset = payloadOffset;
        PayloadLength = payloadLength;
    }

    public DhcpClientTransactionPlannerStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == DhcpClientTransactionPlannerStatus.Success;

    public DhcpClientState State { get; }

    public DhcpUdpFrameStatus FrameStatus { get; }

    public DhcpClientStatus ClientStatus { get; }

    public DhcpMessageStatus MessageStatus { get; }

    public PacketParseStatus PacketStatus { get; }

    public UdpReceiveDispatchStatus UdpStatus { get; }

    public DhcpLeaseConfigurationStatus LeaseStatus { get; }

    public uint TransactionId { get; }

    public MacAddress ClientMac { get; }

    public Ipv4Address OfferedAddress { get; }

    public Ipv4Address ServerIdentifier { get; }

    public DhcpLeaseConfiguration LeaseConfiguration { get; }

    public int BytesWritten { get; }

    public int PayloadOffset { get; }

    public int PayloadLength { get; }

    public static DhcpClientTransactionPlannerResult Success(
        DhcpClientState state,
        uint transactionId,
        MacAddress clientMac,
        Ipv4Address offeredAddress,
        Ipv4Address serverIdentifier,
        DhcpLeaseConfiguration leaseConfiguration,
        int bytesWritten,
        int payloadOffset,
        int payloadLength)
    {
        return new DhcpClientTransactionPlannerResult(
            DhcpClientTransactionPlannerStatus.Success,
            state,
            DhcpUdpFrameStatus.Success,
            DhcpClientStatus.Success,
            DhcpMessageStatus.Success,
            PacketParseStatus.Success,
            UdpReceiveDispatchStatus.Success,
            DhcpLeaseConfigurationStatus.Success,
            transactionId,
            clientMac,
            offeredAddress,
            serverIdentifier,
            leaseConfiguration,
            bytesWritten,
            payloadOffset,
            payloadLength);
    }

    public static DhcpClientTransactionPlannerResult Fail(
        DhcpClientTransactionPlannerStatus status,
        DhcpClientState state,
        DhcpUdpFrameStatus frameStatus,
        DhcpClientStatus clientStatus,
        DhcpMessageStatus messageStatus,
        PacketParseStatus packetStatus,
        UdpReceiveDispatchStatus udpStatus,
        DhcpLeaseConfigurationStatus leaseStatus,
        uint transactionId,
        MacAddress clientMac,
        Ipv4Address offeredAddress,
        Ipv4Address serverIdentifier,
        int bytesWritten,
        int payloadOffset,
        int payloadLength)
    {
        return new DhcpClientTransactionPlannerResult(
            status,
            state,
            frameStatus,
            clientStatus,
            messageStatus,
            packetStatus,
            udpStatus,
            leaseStatus,
            transactionId,
            clientMac,
            offeredAddress,
            serverIdentifier,
            default,
            bytesWritten,
            payloadOffset,
            payloadLength);
    }
}

public struct DhcpClientTransactionPlanner
{
    private DhcpClientTransaction _transaction;

    private DhcpClientTransactionPlanner(DhcpClientTransaction transaction)
    {
        _transaction = transaction;
    }

    public DhcpClientState State => _transaction.State;

    public uint TransactionId => _transaction.TransactionId;

    public MacAddress ClientMac => _transaction.ClientMac;

    public Ipv4Address OfferedAddress => _transaction.OfferedAddress;

    public Ipv4Address ServerIdentifier => _transaction.ServerIdentifier;

    public DhcpLeaseConfiguration LeaseConfiguration { get; private set; }

    public static DhcpClientTransactionPlanner Create(
        uint transactionId,
        MacAddress clientMac)
    {
        return new DhcpClientTransactionPlanner(
            DhcpClientTransaction.Create(transactionId, clientMac));
    }

    public DhcpClientTransactionPlannerStatus TryWriteDiscoverFrame(
        Span<byte> frame,
        out DhcpClientTransactionPlannerResult result)
    {
        if (!IsValidClientMac(ClientMac))
        {
            result = Fail(DhcpClientTransactionPlannerStatus.InvalidClientIdentity);
            return result.Status;
        }

        DhcpClientTransaction pending = _transaction;
        Span<byte> message = stackalloc byte[DhcpUdpFrame.DiscoverPayloadLength];
        DhcpClientStatus clientStatus = pending.TryWriteDiscover(
            message,
            out DhcpClientTransitionResult transitionResult);
        if (clientStatus != DhcpClientStatus.Success)
        {
            result = Fail(
                MapClientStatus(clientStatus),
                DhcpUdpFrameStatus.Success,
                clientStatus,
                transitionResult.MessageStatus,
                PacketParseStatus.Success,
                UdpReceiveDispatchStatus.Success,
                DhcpLeaseConfigurationStatus.Success,
                transitionResult.BytesWritten);
            return result.Status;
        }

        DhcpUdpFrameWriteResult frameResult = DhcpUdpFrame.TryWriteDiscover(
            frame,
            TransactionId,
            ClientMac,
            out int bytesWritten);
        if (!frameResult.Succeeded)
        {
            result = Fail(
                MapFrameWriteStatus(frameResult),
                frameResult.Status,
                clientStatus,
                frameResult.DhcpStatus,
                frameResult.PacketStatus,
                UdpReceiveDispatchStatus.Success,
                DhcpLeaseConfigurationStatus.Success,
                bytesWritten);
            return result.Status;
        }

        _transaction = pending;
        result = DhcpClientTransactionPlannerResult.Success(
            _transaction.State,
            TransactionId,
            ClientMac,
            OfferedAddress,
            ServerIdentifier,
            LeaseConfiguration,
            frameResult.BytesWritten,
            0,
            0);
        return result.Status;
    }

    public DhcpClientTransactionPlannerStatus TryWriteRequestFrame(
        Span<byte> frame,
        out DhcpClientTransactionPlannerResult result)
    {
        if (!IsValidClientMac(ClientMac))
        {
            result = Fail(DhcpClientTransactionPlannerStatus.InvalidClientIdentity);
            return result.Status;
        }

        Ipv4Address offeredAddress = OfferedAddress;
        Ipv4Address serverIdentifier = ServerIdentifier;
        DhcpClientTransaction pending = _transaction;
        Span<byte> message = stackalloc byte[DhcpUdpFrame.RequestPayloadLength];
        DhcpClientStatus clientStatus = pending.TryWriteRequest(
            message,
            out DhcpClientTransitionResult transitionResult);
        if (clientStatus != DhcpClientStatus.Success)
        {
            result = Fail(
                MapClientStatus(clientStatus),
                DhcpUdpFrameStatus.Success,
                clientStatus,
                transitionResult.MessageStatus,
                PacketParseStatus.Success,
                UdpReceiveDispatchStatus.Success,
                DhcpLeaseConfigurationStatus.Success,
                transitionResult.BytesWritten);
            return result.Status;
        }

        DhcpUdpFrameWriteResult frameResult = DhcpUdpFrame.TryWriteRequest(
            frame,
            TransactionId,
            ClientMac,
            offeredAddress,
            serverIdentifier,
            out int bytesWritten);
        if (!frameResult.Succeeded)
        {
            result = Fail(
                MapFrameWriteStatus(frameResult),
                frameResult.Status,
                clientStatus,
                frameResult.DhcpStatus,
                frameResult.PacketStatus,
                UdpReceiveDispatchStatus.Success,
                DhcpLeaseConfigurationStatus.Success,
                bytesWritten);
            return result.Status;
        }

        _transaction = pending;
        result = DhcpClientTransactionPlannerResult.Success(
            _transaction.State,
            TransactionId,
            ClientMac,
            OfferedAddress,
            ServerIdentifier,
            LeaseConfiguration,
            frameResult.BytesWritten,
            0,
            0);
        return result.Status;
    }

    public DhcpClientTransactionPlannerStatus ReceiveOfferFromEthernetFrame(
        ReadOnlySpan<byte> ethernetFrame,
        out DhcpClientTransactionPlannerResult result)
    {
        DhcpUdpFrameReadResult frameResult =
            DhcpUdpFrame.TryParseServerReplyFromEthernetFrame(ethernetFrame);
        return ReceiveServerReply(ethernetFrame, frameResult, isAck: false, out result);
    }

    public DhcpClientTransactionPlannerStatus ReceiveAckFromEthernetFrame(
        ReadOnlySpan<byte> ethernetFrame,
        out DhcpClientTransactionPlannerResult result)
    {
        DhcpUdpFrameReadResult frameResult =
            DhcpUdpFrame.TryParseServerReplyFromEthernetFrame(ethernetFrame);
        return ReceiveServerReply(ethernetFrame, frameResult, isAck: true, out result);
    }

    public DhcpClientTransactionPlannerStatus ReceiveOfferFromReceiveBuffer(
        ReadOnlySpan<byte> receiveBuffer,
        out DhcpClientTransactionPlannerResult result)
    {
        DhcpUdpFrameReadResult frameResult =
            DhcpUdpFrame.TryParseServerReplyFromReceiveBuffer(receiveBuffer);
        return ReceiveServerReply(receiveBuffer, frameResult, isAck: false, out result);
    }

    public DhcpClientTransactionPlannerStatus ReceiveAckFromReceiveBuffer(
        ReadOnlySpan<byte> receiveBuffer,
        out DhcpClientTransactionPlannerResult result)
    {
        DhcpUdpFrameReadResult frameResult =
            DhcpUdpFrame.TryParseServerReplyFromReceiveBuffer(receiveBuffer);
        return ReceiveServerReply(receiveBuffer, frameResult, isAck: true, out result);
    }

    public DhcpClientTransactionPlannerStatus Timeout(
        out DhcpClientTransactionPlannerResult result)
    {
        DhcpClientStatus clientStatus = _transaction.Timeout(
            out DhcpClientTransitionResult transitionResult);
        if (clientStatus != DhcpClientStatus.Success)
        {
            result = Fail(
                MapClientStatus(clientStatus),
                DhcpUdpFrameStatus.Success,
                clientStatus,
                transitionResult.MessageStatus,
                PacketParseStatus.Success,
                UdpReceiveDispatchStatus.Success,
                DhcpLeaseConfigurationStatus.Success,
                transitionResult.BytesWritten);
            return result.Status;
        }

        result = Fail(DhcpClientTransactionPlannerStatus.Timeout);
        return result.Status;
    }

    private DhcpClientTransactionPlannerStatus ReceiveServerReply(
        ReadOnlySpan<byte> packetSource,
        in DhcpUdpFrameReadResult frameResult,
        bool isAck,
        out DhcpClientTransactionPlannerResult result)
    {
        if (!frameResult.Succeeded)
        {
            result = Fail(
                MapFrameReadStatus(frameResult),
                frameResult.Status,
                DhcpClientStatus.Success,
                frameResult.DhcpStatus,
                frameResult.PacketStatus,
                frameResult.UdpStatus,
                DhcpLeaseConfigurationStatus.Success,
                0,
                frameResult.PayloadOffset,
                frameResult.PayloadLength);
            return result.Status;
        }

        ReadOnlySpan<byte> dhcpPayload = packetSource.Slice(
            frameResult.PayloadOffset,
            frameResult.PayloadLength);
        DhcpClientTransaction pending = _transaction;
        DhcpClientStatus clientStatus = isAck
            ? pending.ReceiveAck(dhcpPayload, out DhcpClientTransitionResult transitionResult)
            : pending.ReceiveOffer(dhcpPayload, out transitionResult);
        if (clientStatus != DhcpClientStatus.Success)
        {
            _transaction = pending;
            result = Fail(
                MapClientStatus(clientStatus),
                frameResult.Status,
                clientStatus,
                transitionResult.MessageStatus,
                frameResult.PacketStatus,
                frameResult.UdpStatus,
                DhcpLeaseConfigurationStatus.Success,
                transitionResult.BytesWritten,
                frameResult.PayloadOffset,
                frameResult.PayloadLength);
            return result.Status;
        }

        DhcpLeaseConfiguration leaseConfiguration = LeaseConfiguration;
        DhcpLeaseConfigurationStatus leaseStatus = DhcpLeaseConfigurationStatus.Success;
        if (isAck)
        {
            leaseStatus = DhcpLeaseConfiguration.TryCreate(
                pending.Lease,
                out leaseConfiguration);
            if (leaseStatus != DhcpLeaseConfigurationStatus.Success)
            {
                result = Fail(
                    DhcpClientTransactionPlannerStatus.LeaseConfigurationFailed,
                    frameResult.Status,
                    clientStatus,
                    transitionResult.MessageStatus,
                    frameResult.PacketStatus,
                    frameResult.UdpStatus,
                    leaseStatus,
                    transitionResult.BytesWritten,
                    frameResult.PayloadOffset,
                    frameResult.PayloadLength);
                return result.Status;
            }
        }

        _transaction = pending;
        LeaseConfiguration = leaseConfiguration;
        result = DhcpClientTransactionPlannerResult.Success(
            _transaction.State,
            TransactionId,
            ClientMac,
            OfferedAddress,
            ServerIdentifier,
            LeaseConfiguration,
            transitionResult.BytesWritten,
            frameResult.PayloadOffset,
            frameResult.PayloadLength);
        return result.Status;
    }

    private DhcpClientTransactionPlannerResult Fail(
        DhcpClientTransactionPlannerStatus status)
    {
        return Fail(
            status,
            DhcpUdpFrameStatus.Success,
            DhcpClientStatus.Success,
            DhcpMessageStatus.Success,
            PacketParseStatus.Success,
            UdpReceiveDispatchStatus.Success,
            DhcpLeaseConfigurationStatus.Success,
            0);
    }

    private DhcpClientTransactionPlannerResult Fail(
        DhcpClientTransactionPlannerStatus status,
        DhcpUdpFrameStatus frameStatus,
        DhcpClientStatus clientStatus,
        DhcpMessageStatus messageStatus,
        PacketParseStatus packetStatus,
        UdpReceiveDispatchStatus udpStatus,
        DhcpLeaseConfigurationStatus leaseStatus,
        int bytesWritten,
        int payloadOffset = 0,
        int payloadLength = 0)
    {
        return DhcpClientTransactionPlannerResult.Fail(
            status,
            State,
            frameStatus,
            clientStatus,
            messageStatus,
            packetStatus,
            udpStatus,
            leaseStatus,
            TransactionId,
            ClientMac,
            OfferedAddress,
            ServerIdentifier,
            bytesWritten,
            payloadOffset,
            payloadLength);
    }

    private static DhcpClientTransactionPlannerStatus MapFrameWriteStatus(
        in DhcpUdpFrameWriteResult result)
    {
        if (result.PacketStatus == PacketParseStatus.BufferTooSmall
            || result.DhcpStatus == DhcpMessageStatus.BufferTooSmall)
        {
            return DhcpClientTransactionPlannerStatus.FrameBufferTooSmall;
        }

        return result.Status == DhcpUdpFrameStatus.DhcpMessageFailed
            ? DhcpClientTransactionPlannerStatus.MalformedPacket
            : DhcpClientTransactionPlannerStatus.FrameWriteFailed;
    }

    private static DhcpClientTransactionPlannerStatus MapFrameReadStatus(
        in DhcpUdpFrameReadResult result)
    {
        return result.Status switch
        {
            DhcpUdpFrameStatus.WrongUdpPorts =>
                DhcpClientTransactionPlannerStatus.WrongUdpPorts,
            DhcpUdpFrameStatus.UdpReceiveFailed =>
                DhcpClientTransactionPlannerStatus.UdpReceiveFailed,
            DhcpUdpFrameStatus.PacketFailed =>
                DhcpClientTransactionPlannerStatus.FrameReadFailed,
            DhcpUdpFrameStatus.DhcpMessageFailed =>
                DhcpClientTransactionPlannerStatus.MalformedPacket,
            _ => DhcpClientTransactionPlannerStatus.FrameReadFailed
        };
    }

    private static DhcpClientTransactionPlannerStatus MapClientStatus(
        DhcpClientStatus status)
    {
        return status switch
        {
            DhcpClientStatus.Success =>
                DhcpClientTransactionPlannerStatus.Success,
            DhcpClientStatus.InvalidState =>
                DhcpClientTransactionPlannerStatus.InvalidState,
            DhcpClientStatus.BufferTooSmall =>
                DhcpClientTransactionPlannerStatus.FrameBufferTooSmall,
            DhcpClientStatus.MessageWriteFailed or DhcpClientStatus.MessageParseFailed =>
                DhcpClientTransactionPlannerStatus.MalformedPacket,
            DhcpClientStatus.UnexpectedMessageType =>
                DhcpClientTransactionPlannerStatus.UnexpectedMessageType,
            DhcpClientStatus.TransactionIdMismatch =>
                DhcpClientTransactionPlannerStatus.WrongTransactionId,
            DhcpClientStatus.ClientHardwareAddressMismatch =>
                DhcpClientTransactionPlannerStatus.ClientHardwareAddressMismatch,
            DhcpClientStatus.MissingServerIdentifier =>
                DhcpClientTransactionPlannerStatus.MissingServerIdentifier,
            DhcpClientStatus.MissingOfferedAddress =>
                DhcpClientTransactionPlannerStatus.MissingOfferedAddress,
            DhcpClientStatus.ServerIdentifierMismatch =>
                DhcpClientTransactionPlannerStatus.ServerIdentifierMismatch,
            DhcpClientStatus.OfferedAddressMismatch =>
                DhcpClientTransactionPlannerStatus.OfferedAddressMismatch,
            DhcpClientStatus.MissingLeaseTime =>
                DhcpClientTransactionPlannerStatus.MissingLeaseTime,
            DhcpClientStatus.InvalidLeaseTime =>
                DhcpClientTransactionPlannerStatus.InvalidLeaseTime,
            DhcpClientStatus.Timeout =>
                DhcpClientTransactionPlannerStatus.Timeout,
            _ => DhcpClientTransactionPlannerStatus.MalformedPacket
        };
    }

    private static bool IsValidClientMac(MacAddress macAddress)
    {
        return !macAddress.IsZero && !macAddress.IsBroadcast;
    }
}
