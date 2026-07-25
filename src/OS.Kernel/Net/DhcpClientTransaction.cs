namespace OS.Net;

public enum DhcpClientState
{
    Initial = 0,
    DiscoverSent = 1,
    OfferReceived = 2,
    RequestSent = 3,
    Bound = 4,
    Failed = 5
}

public enum DhcpClientStatus
{
    Success = 0,
    InvalidState = 1,
    BufferTooSmall = 2,
    MessageWriteFailed = 3,
    MessageParseFailed = 4,
    UnexpectedMessageType = 5,
    TransactionIdMismatch = 6,
    ClientHardwareAddressMismatch = 7,
    MissingServerIdentifier = 8,
    MissingOfferedAddress = 9,
    ServerIdentifierMismatch = 10,
    OfferedAddressMismatch = 11,
    MissingLeaseTime = 12,
    InvalidLeaseTime = 13,
    Timeout = 14
}

public readonly struct DhcpClientTransitionResult
{
    private DhcpClientTransitionResult(
        DhcpClientStatus status,
        DhcpClientState state,
        DhcpMessageStatus messageStatus,
        int bytesWritten)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        MessageStatus = messageStatus;
        BytesWritten = bytesWritten;
    }

    public DhcpClientStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == DhcpClientStatus.Success;

    public DhcpClientState State { get; }

    public DhcpMessageStatus MessageStatus { get; }

    public int BytesWritten { get; }

    public static DhcpClientTransitionResult Success(DhcpClientState state, int bytesWritten)
    {
        return new DhcpClientTransitionResult(
            DhcpClientStatus.Success,
            state,
            DhcpMessageStatus.Success,
            bytesWritten);
    }

    public static DhcpClientTransitionResult Fail(
        DhcpClientStatus status,
        DhcpClientState state,
        DhcpMessageStatus messageStatus)
    {
        return new DhcpClientTransitionResult(status, state, messageStatus, 0);
    }
}

public readonly struct DhcpClientLease
{
    public DhcpClientLease(
        Ipv4Address address,
        Ipv4Address serverIdentifier,
        uint leaseTimeSeconds,
        bool hasSubnetMask,
        Ipv4Address subnetMask,
        bool hasRouter,
        Ipv4Address router,
        bool hasDnsServer,
        Ipv4Address dnsServer)
    {
        Address = address;
        ServerIdentifier = serverIdentifier;
        LeaseTimeSeconds = leaseTimeSeconds;
        HasSubnetMask = hasSubnetMask;
        SubnetMask = subnetMask;
        HasRouter = hasRouter;
        Router = router;
        HasDnsServer = hasDnsServer;
        DnsServer = dnsServer;
    }

    public Ipv4Address Address { get; }

    public Ipv4Address ServerIdentifier { get; }

    public uint LeaseTimeSeconds { get; }

    public bool HasSubnetMask { get; }

    public Ipv4Address SubnetMask { get; }

    public bool HasRouter { get; }

    public Ipv4Address Router { get; }

    public bool HasDnsServer { get; }

    public Ipv4Address DnsServer { get; }
}

public struct DhcpClientTransaction
{
    private DhcpClientTransaction(uint transactionId, MacAddress clientMac)
    {
        TransactionId = transactionId;
        ClientMac = clientMac;
        State = DhcpClientState.Initial;
        OfferedAddress = default;
        ServerIdentifier = default;
        Lease = default;
    }

    public DhcpClientState State { get; private set; }

    public uint TransactionId { get; }

    public MacAddress ClientMac { get; }

    public Ipv4Address OfferedAddress { get; private set; }

    public Ipv4Address ServerIdentifier { get; private set; }

    public DhcpClientLease Lease { get; private set; }

    public static DhcpClientTransaction Create(uint transactionId, MacAddress clientMac)
    {
        return new DhcpClientTransaction(transactionId, clientMac);
    }

    public DhcpClientStatus TryWriteDiscover(
        Span<byte> message,
        out DhcpClientTransitionResult result)
    {
        if (State != DhcpClientState.Initial)
        {
            result = DhcpClientTransitionResult.Fail(
                DhcpClientStatus.InvalidState,
                State,
                DhcpMessageStatus.Success);
            return result.Status;
        }

        DhcpMessageStatus writeStatus = DhcpMessage.TryWriteDiscover(
            message,
            TransactionId,
            ClientMac,
            out int bytesWritten);
        if (writeStatus != DhcpMessageStatus.Success)
        {
            DhcpClientStatus status = MapWriteStatus(writeStatus);
            result = DhcpClientTransitionResult.Fail(status, State, writeStatus);
            return status;
        }

        State = DhcpClientState.DiscoverSent;
        result = DhcpClientTransitionResult.Success(State, bytesWritten);
        return result.Status;
    }

    public DhcpClientStatus ReceiveOffer(
        ReadOnlySpan<byte> message,
        out DhcpClientTransitionResult result)
    {
        if (State != DhcpClientState.DiscoverSent)
        {
            result = DhcpClientTransitionResult.Fail(
                DhcpClientStatus.InvalidState,
                State,
                DhcpMessageStatus.Success);
            return result.Status;
        }

        DhcpMessageStatus parseStatus = DhcpMessage.TryParse(message, out DhcpMessage offer);
        if (parseStatus != DhcpMessageStatus.Success)
        {
            return Fail(DhcpClientStatus.MessageParseFailed, parseStatus, out result);
        }

        DhcpClientStatus validationStatus = ValidateIncomingMessage(
            offer,
            DhcpMessage.MessageTypeOffer);
        if (validationStatus != DhcpClientStatus.Success)
        {
            return Fail(validationStatus, DhcpMessageStatus.Success, out result);
        }

        if (offer.HasLeaseTime && offer.LeaseTimeSeconds == 0)
        {
            return Fail(DhcpClientStatus.InvalidLeaseTime, DhcpMessageStatus.Success, out result);
        }

        OfferedAddress = offer.YourAddress;
        ServerIdentifier = offer.ServerIdentifier;
        State = DhcpClientState.OfferReceived;
        result = DhcpClientTransitionResult.Success(State, 0);
        return result.Status;
    }

    public DhcpClientStatus TryWriteRequest(
        Span<byte> message,
        out DhcpClientTransitionResult result)
    {
        if (State != DhcpClientState.OfferReceived)
        {
            result = DhcpClientTransitionResult.Fail(
                DhcpClientStatus.InvalidState,
                State,
                DhcpMessageStatus.Success);
            return result.Status;
        }

        DhcpMessageStatus writeStatus = DhcpMessage.TryWriteRequest(
            message,
            TransactionId,
            ClientMac,
            OfferedAddress,
            ServerIdentifier,
            out int bytesWritten);
        if (writeStatus != DhcpMessageStatus.Success)
        {
            DhcpClientStatus status = MapWriteStatus(writeStatus);
            result = DhcpClientTransitionResult.Fail(status, State, writeStatus);
            return status;
        }

        State = DhcpClientState.RequestSent;
        result = DhcpClientTransitionResult.Success(State, bytesWritten);
        return result.Status;
    }

    public DhcpClientStatus ReceiveAck(
        ReadOnlySpan<byte> message,
        out DhcpClientTransitionResult result)
    {
        if (State != DhcpClientState.RequestSent)
        {
            result = DhcpClientTransitionResult.Fail(
                DhcpClientStatus.InvalidState,
                State,
                DhcpMessageStatus.Success);
            return result.Status;
        }

        DhcpMessageStatus parseStatus = DhcpMessage.TryParse(message, out DhcpMessage ack);
        if (parseStatus != DhcpMessageStatus.Success)
        {
            return Fail(DhcpClientStatus.MessageParseFailed, parseStatus, out result);
        }

        DhcpClientStatus validationStatus = ValidateIncomingMessage(
            ack,
            DhcpMessage.MessageTypeAck);
        if (validationStatus != DhcpClientStatus.Success)
        {
            return Fail(validationStatus, DhcpMessageStatus.Success, out result);
        }

        if (ack.ServerIdentifier != ServerIdentifier)
        {
            return Fail(DhcpClientStatus.ServerIdentifierMismatch, DhcpMessageStatus.Success, out result);
        }

        if (ack.YourAddress != OfferedAddress)
        {
            return Fail(DhcpClientStatus.OfferedAddressMismatch, DhcpMessageStatus.Success, out result);
        }

        if (!ack.HasLeaseTime)
        {
            return Fail(DhcpClientStatus.MissingLeaseTime, DhcpMessageStatus.Success, out result);
        }

        if (ack.LeaseTimeSeconds == 0)
        {
            return Fail(DhcpClientStatus.InvalidLeaseTime, DhcpMessageStatus.Success, out result);
        }

        Lease = new DhcpClientLease(
            ack.YourAddress,
            ack.ServerIdentifier,
            ack.LeaseTimeSeconds,
            ack.HasSubnetMask,
            ack.SubnetMask,
            ack.HasRouter,
            ack.Router,
            ack.HasDnsServer,
            ack.DnsServer);
        State = DhcpClientState.Bound;
        result = DhcpClientTransitionResult.Success(State, 0);
        return result.Status;
    }

    public DhcpClientStatus Timeout(out DhcpClientTransitionResult result)
    {
        if (State != DhcpClientState.DiscoverSent && State != DhcpClientState.RequestSent)
        {
            result = DhcpClientTransitionResult.Fail(
                DhcpClientStatus.InvalidState,
                State,
                DhcpMessageStatus.Success);
            return result.Status;
        }

        State = DhcpClientState.Failed;
        result = DhcpClientTransitionResult.Fail(
            DhcpClientStatus.Timeout,
            State,
            DhcpMessageStatus.Success);
        return result.Status;
    }

    private DhcpClientStatus ValidateIncomingMessage(
        in DhcpMessage message,
        byte expectedMessageType)
    {
        if (message.MessageType != expectedMessageType)
        {
            return DhcpClientStatus.UnexpectedMessageType;
        }

        if (message.TransactionId != TransactionId)
        {
            return DhcpClientStatus.TransactionIdMismatch;
        }

        if (message.ClientMac != ClientMac)
        {
            return DhcpClientStatus.ClientHardwareAddressMismatch;
        }

        if (!message.HasServerIdentifier)
        {
            return DhcpClientStatus.MissingServerIdentifier;
        }

        if (message.YourAddress == default)
        {
            return DhcpClientStatus.MissingOfferedAddress;
        }

        return DhcpClientStatus.Success;
    }

    private DhcpClientStatus Fail(
        DhcpClientStatus status,
        DhcpMessageStatus messageStatus,
        out DhcpClientTransitionResult result)
    {
        State = DhcpClientState.Failed;
        result = DhcpClientTransitionResult.Fail(status, State, messageStatus);
        return status;
    }

    private static DhcpClientStatus MapWriteStatus(DhcpMessageStatus status)
    {
        return status == DhcpMessageStatus.BufferTooSmall
            ? DhcpClientStatus.BufferTooSmall
            : DhcpClientStatus.MessageWriteFailed;
    }
}
