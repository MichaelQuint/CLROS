namespace OS.Net;

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

    public DhcpClientState State
    {
        get; private set;
    }
    public uint TransactionId
    {
        get;
    }
    public MacAddress ClientMac
    {
        get;
    }
    public Ipv4Address OfferedAddress
    {
        get; private set;
    }
    public Ipv4Address ServerIdentifier
    {
        get; private set;
    }
    public DhcpClientLease Lease
    {
        get; private set;
    }

    public static DhcpClientTransaction Create(uint transactionId, MacAddress clientMac)
    {
        return new DhcpClientTransaction(transactionId, clientMac);
    }

    public DhcpClientStatus TryWriteDiscover(Span<byte> message, out DhcpClientTransitionResult result)
    {
        if (State != DhcpClientState.Initial)
        {
            result = DhcpClientTransitionResult.Fail(DhcpClientStatus.InvalidState, State, DhcpMessageStatus.Success);
            return result.Status;
        }

        DhcpMessageStatus writeStatus = DhcpMessage.TryWriteDiscover(message, TransactionId, ClientMac, out int bytesWritten);
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

    public DhcpClientStatus ReceiveOffer(ReadOnlySpan<byte> message, out DhcpClientTransitionResult result)
    {
        if (State != DhcpClientState.DiscoverSent)
        {
            result = DhcpClientTransitionResult.Fail(DhcpClientStatus.InvalidState, State, DhcpMessageStatus.Success);
            return result.Status;
        }

        DhcpMessageStatus parseStatus = DhcpMessage.TryParse(message, out DhcpMessage offer);
        if (parseStatus != DhcpMessageStatus.Success)
        {
            return Fail(DhcpClientStatus.MessageParseFailed, parseStatus, out result);
        }

        DhcpClientStatus validationStatus = ValidateIncomingMessage(offer, DhcpMessage.MessageTypeOffer);
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

    public DhcpClientStatus TryWriteRequest(Span<byte> message, out DhcpClientTransitionResult result)
    {
        if (State != DhcpClientState.OfferReceived)
        {
            result = DhcpClientTransitionResult.Fail(DhcpClientStatus.InvalidState, State, DhcpMessageStatus.Success);
            return result.Status;
        }

        DhcpMessageStatus writeStatus = DhcpMessage.TryWriteRequest(message, TransactionId, ClientMac, OfferedAddress, ServerIdentifier, out int bytesWritten);
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

    public DhcpClientStatus ReceiveAck(ReadOnlySpan<byte> message, out DhcpClientTransitionResult result)
    {
        if (State != DhcpClientState.RequestSent)
        {
            result = DhcpClientTransitionResult.Fail(DhcpClientStatus.InvalidState, State, DhcpMessageStatus.Success);
            return result.Status;
        }

        DhcpMessageStatus parseStatus = DhcpMessage.TryParse(message, out DhcpMessage ack);
        if (parseStatus != DhcpMessageStatus.Success)
        {
            return Fail(DhcpClientStatus.MessageParseFailed, parseStatus, out result);
        }

        DhcpClientStatus validationStatus = ValidateIncomingMessage(ack, DhcpMessage.MessageTypeAck);
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

        Lease = new DhcpClientLease(ack.YourAddress, ack.ServerIdentifier, ack.LeaseTimeSeconds, ack.HasSubnetMask, ack.SubnetMask, ack.HasRouter, ack.Router, ack.HasDnsServer, ack.DnsServer);
        State = DhcpClientState.Bound;
        result = DhcpClientTransitionResult.Success(State, 0);
        return result.Status;
    }

    public DhcpClientStatus Timeout(out DhcpClientTransitionResult result)
    {
        if (State != DhcpClientState.DiscoverSent && State != DhcpClientState.RequestSent)
        {
            result = DhcpClientTransitionResult.Fail(DhcpClientStatus.InvalidState, State, DhcpMessageStatus.Success);
            return result.Status;
        }

        State = DhcpClientState.Failed;
        result = DhcpClientTransitionResult.Fail(DhcpClientStatus.Timeout, State, DhcpMessageStatus.Success);
        return result.Status;
    }

    private DhcpClientStatus ValidateIncomingMessage(in DhcpMessage message, byte expectedMessageType)
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

    private DhcpClientStatus Fail(DhcpClientStatus status, DhcpMessageStatus messageStatus, out DhcpClientTransitionResult result)
    {
        State = DhcpClientState.Failed;
        result = DhcpClientTransitionResult.Fail(status, State, messageStatus);
        return status;
    }

    private static DhcpClientStatus MapWriteStatus(DhcpMessageStatus status)
    {
        return status == DhcpMessageStatus.BufferTooSmall ? DhcpClientStatus.BufferTooSmall : DhcpClientStatus.MessageWriteFailed;
    }
}
