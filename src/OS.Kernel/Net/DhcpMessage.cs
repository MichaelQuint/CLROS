namespace OS.Net;

using OS.Std.Binary;

public enum DhcpMessageStatus
{
    Success = 0,
    BufferTooSmall,
    TruncatedFixedHeader,
    MissingMagicCookie,
    UnsupportedOperation,
    UnsupportedHardwareType,
    UnsupportedHardwareAddressLength,
    MalformedOptions,
    UnsupportedMessageType
}

public readonly struct DhcpMessage
{
    public const ushort ServerPort = 67;
    public const ushort ClientPort = 68;

    public const int FixedHeaderLength = 236;
    public const int MagicCookieOffset = FixedHeaderLength;
    public const int OptionsOffset = FixedHeaderLength + 4;
    public const int MinimumMessageLength = OptionsOffset;
    public const int ClientHardwareAddressOffset = 28;
    public const int ClientHardwareAddressFieldLength = 16;

    public const uint MagicCookie = 0x63825363;

    public const byte OpBootRequest = 1;
    public const byte OpBootReply = 2;

    public const byte HardwareTypeEthernet = 1;
    public const byte HardwareAddressLengthEthernet = MacAddress.Length;

    public const byte OptionPad = 0;
    public const byte OptionSubnetMask = 1;
    public const byte OptionRouter = 3;
    public const byte OptionDomainNameServer = 6;
    public const byte OptionDnsServer = OptionDomainNameServer;
    public const byte OptionHostName = 12;
    public const byte OptionDomainName = 15;
    public const byte OptionBroadcastAddress = 28;
    public const byte OptionRequestedIpAddress = 50;
    public const byte OptionIpAddressLeaseTime = 51;
    public const byte OptionDhcpMessageType = 53;
    public const byte OptionServerIdentifier = 54;
    public const byte OptionParameterRequestList = 55;
    public const byte OptionMaximumDhcpMessageSize = 57;
    public const byte OptionRenewalTime = 58;
    public const byte OptionRebindingTime = 59;
    public const byte OptionClientIdentifier = 61;
    public const byte OptionEnd = 255;

    public const byte MessageTypeDiscover = 1;
    public const byte MessageTypeOffer = 2;
    public const byte MessageTypeRequest = 3;
    public const byte MessageTypeDecline = 4;
    public const byte MessageTypeAck = 5;
    public const byte MessageTypeNak = 6;
    public const byte MessageTypeRelease = 7;
    public const byte MessageTypeInform = 8;

    private const ushort BroadcastFlag = 0x8000;
    private const int DiscoverOptionsLength = 11;
    private const int RequestOptionsLength = 23;

    public DhcpMessage(
        byte messageType,
        uint transactionId,
        MacAddress clientMac,
        Ipv4Address yourAddress,
        bool hasServerIdentifier,
        Ipv4Address serverIdentifier,
        bool hasLeaseTime,
        uint leaseTimeSeconds,
        bool hasSubnetMask,
        Ipv4Address subnetMask,
        bool hasRouter,
        Ipv4Address router,
        bool hasDnsServer,
        Ipv4Address dnsServer)
    {
        MessageType = messageType;
        TransactionId = transactionId;
        ClientMac = clientMac;
        YourAddress = yourAddress;
        HasServerIdentifier = hasServerIdentifier;
        ServerIdentifier = serverIdentifier;
        HasLeaseTime = hasLeaseTime;
        LeaseTimeSeconds = leaseTimeSeconds;
        HasSubnetMask = hasSubnetMask;
        SubnetMask = subnetMask;
        HasRouter = hasRouter;
        Router = router;
        HasDnsServer = hasDnsServer;
        DnsServer = dnsServer;
    }

    public byte MessageType { get; }

    public uint TransactionId { get; }

    public MacAddress ClientMac { get; }

    public Ipv4Address YourAddress { get; }

    public bool HasServerIdentifier { get; }

    public Ipv4Address ServerIdentifier { get; }

    public bool HasLeaseTime { get; }

    public uint LeaseTimeSeconds { get; }

    public bool HasSubnetMask { get; }

    public Ipv4Address SubnetMask { get; }

    public bool HasRouter { get; }

    public Ipv4Address Router { get; }

    public bool HasDnsServer { get; }

    public Ipv4Address DnsServer { get; }

    public static DhcpMessageStatus TryWriteDiscover(
        Span<byte> message,
        uint transactionId,
        MacAddress clientMac,
        out int bytesWritten)
    {
        return TryWriteClientMessage(
            message,
            MessageTypeDiscover,
            transactionId,
            clientMac,
            default,
            default,
            includeRequestOptions: false,
            out bytesWritten);
    }

    public static DhcpMessageStatus TryWriteRequest(
        Span<byte> message,
        uint transactionId,
        MacAddress clientMac,
        Ipv4Address requestedAddress,
        Ipv4Address serverIdentifier,
        out int bytesWritten)
    {
        return TryWriteClientMessage(
            message,
            MessageTypeRequest,
            transactionId,
            clientMac,
            requestedAddress,
            serverIdentifier,
            includeRequestOptions: true,
            out bytesWritten);
    }

    public static DhcpMessageStatus TryParse(ReadOnlySpan<byte> message, out DhcpMessage parsed)
    {
        parsed = default;

        if (message.Length < MinimumMessageLength)
        {
            return DhcpMessageStatus.TruncatedFixedHeader;
        }

        if (message[0] != OpBootReply)
        {
            return DhcpMessageStatus.UnsupportedOperation;
        }

        if (message[1] != HardwareTypeEthernet)
        {
            return DhcpMessageStatus.UnsupportedHardwareType;
        }

        if (message[2] != HardwareAddressLengthEthernet)
        {
            return DhcpMessageStatus.UnsupportedHardwareAddressLength;
        }

        uint magicCookie = EndianPrimitives.ReadUInt32BigEndian(message.Slice(MagicCookieOffset, 4));
        if (magicCookie != MagicCookie)
        {
            return DhcpMessageStatus.MissingMagicCookie;
        }

        uint transactionId = EndianPrimitives.ReadUInt32BigEndian(message.Slice(4, 4));
        PacketParseStatus clientMacStatus = MacAddress.TryRead(message, ClientHardwareAddressOffset, out MacAddress clientMac);
        PacketParseStatus yourAddressStatus = Ipv4Address.TryRead(message, 16, out Ipv4Address yourAddress);
        if (clientMacStatus != PacketParseStatus.Success || yourAddressStatus != PacketParseStatus.Success)
        {
            return DhcpMessageStatus.TruncatedFixedHeader;
        }

        byte messageType = 0;
        bool hasServerIdentifier = false;
        Ipv4Address serverIdentifier = default;
        bool hasLeaseTime = false;
        uint leaseTimeSeconds = 0;
        bool hasSubnetMask = false;
        Ipv4Address subnetMask = default;
        bool hasRouter = false;
        Ipv4Address router = default;
        bool hasDnsServer = false;
        Ipv4Address dnsServer = default;

        DhcpMessageStatus optionsStatus = ParseOptions(
            message.Slice(OptionsOffset),
            ref messageType,
            ref hasServerIdentifier,
            ref serverIdentifier,
            ref hasLeaseTime,
            ref leaseTimeSeconds,
            ref hasSubnetMask,
            ref subnetMask,
            ref hasRouter,
            ref router,
            ref hasDnsServer,
            ref dnsServer);
        if (optionsStatus != DhcpMessageStatus.Success)
        {
            return optionsStatus;
        }

        if (!IsSupportedServerMessageType(messageType))
        {
            return DhcpMessageStatus.UnsupportedMessageType;
        }

        parsed = new DhcpMessage(
            messageType,
            transactionId,
            clientMac,
            yourAddress,
            hasServerIdentifier,
            serverIdentifier,
            hasLeaseTime,
            leaseTimeSeconds,
            hasSubnetMask,
            subnetMask,
            hasRouter,
            router,
            hasDnsServer,
            dnsServer);
        return DhcpMessageStatus.Success;
    }

    private static DhcpMessageStatus TryWriteClientMessage(
        Span<byte> message,
        byte messageType,
        uint transactionId,
        MacAddress clientMac,
        Ipv4Address requestedAddress,
        Ipv4Address serverIdentifier,
        bool includeRequestOptions,
        out int bytesWritten)
    {
        bytesWritten = 0;

        int messageLength = MinimumMessageLength + (includeRequestOptions ? RequestOptionsLength : DiscoverOptionsLength);
        if (message.Length < messageLength)
        {
            return DhcpMessageStatus.BufferTooSmall;
        }

        Span<byte> fixedHeader = message.Slice(0, FixedHeaderLength);
        fixedHeader.Clear();

        message[0] = OpBootRequest;
        message[1] = HardwareTypeEthernet;
        message[2] = HardwareAddressLengthEthernet;
        EndianPrimitives.WriteUInt32BigEndian(message.Slice(4, 4), transactionId);
        EndianPrimitives.WriteUInt16BigEndian(message.Slice(10, 2), BroadcastFlag);
        clientMac.TryWrite(message, ClientHardwareAddressOffset);
        EndianPrimitives.WriteUInt32BigEndian(message.Slice(MagicCookieOffset, 4), MagicCookie);

        Span<byte> options = message.Slice(OptionsOffset, includeRequestOptions ? RequestOptionsLength : DiscoverOptionsLength);
        int offset = 0;
        WriteMessageTypeOption(options, messageType, ref offset);

        if (includeRequestOptions)
        {
            options[offset++] = OptionRequestedIpAddress;
            options[offset++] = 4;
            requestedAddress.TryWrite(options, offset);
            offset += 4;

            options[offset++] = OptionServerIdentifier;
            options[offset++] = 4;
            serverIdentifier.TryWrite(options, offset);
            offset += 4;
        }

        options[offset++] = OptionParameterRequestList;
        options[offset++] = 5;
        options[offset++] = OptionSubnetMask;
        options[offset++] = OptionRouter;
        options[offset++] = OptionDomainNameServer;
        options[offset++] = OptionIpAddressLeaseTime;
        options[offset++] = OptionServerIdentifier;
        options[offset++] = OptionEnd;

        bytesWritten = messageLength;
        return DhcpMessageStatus.Success;
    }

    private static void WriteMessageTypeOption(Span<byte> options, byte messageType, ref int offset)
    {
        options[offset++] = OptionDhcpMessageType;
        options[offset++] = 1;
        options[offset++] = messageType;
    }

    private static DhcpMessageStatus ParseOptions(
        ReadOnlySpan<byte> options,
        ref byte messageType,
        ref bool hasServerIdentifier,
        ref Ipv4Address serverIdentifier,
        ref bool hasLeaseTime,
        ref uint leaseTimeSeconds,
        ref bool hasSubnetMask,
        ref Ipv4Address subnetMask,
        ref bool hasRouter,
        ref Ipv4Address router,
        ref bool hasDnsServer,
        ref Ipv4Address dnsServer)
    {
        int offset = 0;
        while (offset < options.Length)
        {
            byte option = options[offset++];
            if (option == OptionPad)
            {
                continue;
            }

            if (option == OptionEnd)
            {
                return DhcpMessageStatus.Success;
            }

            if (offset >= options.Length)
            {
                return DhcpMessageStatus.MalformedOptions;
            }

            byte length = options[offset++];
            if (options.Length - offset < length)
            {
                return DhcpMessageStatus.MalformedOptions;
            }

            ReadOnlySpan<byte> value = options.Slice(offset, length);
            DhcpMessageStatus status = ParseOptionValue(
                option,
                value,
                ref messageType,
                ref hasServerIdentifier,
                ref serverIdentifier,
                ref hasLeaseTime,
                ref leaseTimeSeconds,
                ref hasSubnetMask,
                ref subnetMask,
                ref hasRouter,
                ref router,
                ref hasDnsServer,
                ref dnsServer);
            if (status != DhcpMessageStatus.Success)
            {
                return status;
            }

            offset += length;
        }

        return DhcpMessageStatus.MalformedOptions;
    }

    private static DhcpMessageStatus ParseOptionValue(
        byte option,
        ReadOnlySpan<byte> value,
        ref byte messageType,
        ref bool hasServerIdentifier,
        ref Ipv4Address serverIdentifier,
        ref bool hasLeaseTime,
        ref uint leaseTimeSeconds,
        ref bool hasSubnetMask,
        ref Ipv4Address subnetMask,
        ref bool hasRouter,
        ref Ipv4Address router,
        ref bool hasDnsServer,
        ref Ipv4Address dnsServer)
    {
        switch (option)
        {
            case OptionDhcpMessageType:
                if (value.Length != 1)
                {
                    return DhcpMessageStatus.MalformedOptions;
                }

                messageType = value[0];
                return DhcpMessageStatus.Success;

            case OptionServerIdentifier:
                if (value.Length != 4)
                {
                    return DhcpMessageStatus.MalformedOptions;
                }

                Ipv4Address.TryRead(value, 0, out serverIdentifier);
                hasServerIdentifier = true;
                return DhcpMessageStatus.Success;

            case OptionIpAddressLeaseTime:
                if (value.Length != 4)
                {
                    return DhcpMessageStatus.MalformedOptions;
                }

                leaseTimeSeconds = EndianPrimitives.ReadUInt32BigEndian(value);
                hasLeaseTime = true;
                return DhcpMessageStatus.Success;

            case OptionSubnetMask:
                if (value.Length != 4)
                {
                    return DhcpMessageStatus.MalformedOptions;
                }

                Ipv4Address.TryRead(value, 0, out subnetMask);
                hasSubnetMask = true;
                return DhcpMessageStatus.Success;

            case OptionRouter:
                if (!IsIpv4AddressListLength(value.Length))
                {
                    return DhcpMessageStatus.MalformedOptions;
                }

                Ipv4Address.TryRead(value, 0, out router);
                hasRouter = true;
                return DhcpMessageStatus.Success;

            case OptionDomainNameServer:
                if (!IsIpv4AddressListLength(value.Length))
                {
                    return DhcpMessageStatus.MalformedOptions;
                }

                Ipv4Address.TryRead(value, 0, out dnsServer);
                hasDnsServer = true;
                return DhcpMessageStatus.Success;

            default:
                return DhcpMessageStatus.Success;
        }
    }

    private static bool IsIpv4AddressListLength(int length)
    {
        return length >= 4 && (length & 3) == 0;
    }

    private static bool IsSupportedServerMessageType(byte messageType)
    {
        return messageType == MessageTypeOffer || messageType == MessageTypeAck;
    }
}
