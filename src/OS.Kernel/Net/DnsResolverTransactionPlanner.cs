namespace OS.Net;

using OS.Std.Binary;

public enum DnsResolverTransactionPlanStatus
{
    Success = 0,
    InvalidLocalIdentity = 1,
    InvalidServerIdentity = 2,
    InvalidQueryName = 3,
    QueryFrameFailed = 4,
    UdpReceiveFailed = 5,
    WrongServer = 6,
    WrongLocalAddress = 7,
    WrongUdpPorts = 8,
    WrongTransaction = 9,
    Nxdomain = 10,
    NoAnswer = 11,
    UnsupportedAnswerType = 12,
    MalformedResponse = 13,
    ResolverParseFailed = 14,
    InvalidState = 15
}

public readonly struct DnsResolverTransactionPlannerInfo
{
    public DnsResolverTransactionPlannerInfo(
        MacAddress localMac,
        MacAddress nextHopMac,
        Ipv4Address localAddress,
        Ipv4Address dnsServerAddress,
        ushort clientPort,
        ushort transactionId,
        ushort identification = 0)
    {
        LocalMac = localMac;
        NextHopMac = nextHopMac;
        LocalAddress = localAddress;
        DnsServerAddress = dnsServerAddress;
        ClientPort = clientPort;
        TransactionId = transactionId;
        Identification = identification;
    }

    public MacAddress LocalMac { get; }

    public MacAddress NextHopMac { get; }

    public Ipv4Address LocalAddress { get; }

    public Ipv4Address DnsServerAddress { get; }

    public ushort ClientPort { get; }

    public ushort TransactionId { get; }

    public ushort Identification { get; }

    public DnsUdpQueryFrameInfo ToQueryFrameInfo()
    {
        return new DnsUdpQueryFrameInfo(
            LocalMac,
            NextHopMac,
            LocalAddress,
            DnsServerAddress,
            ClientPort,
            Identification);
    }
}

public readonly struct DnsResolverTransactionPlanResult
{
    private DnsResolverTransactionPlanResult(
        DnsResolverTransactionPlanStatus status,
        DnsUdpQueryFrameResult queryFrameResult,
        UdpReceiveDispatchResult udpResult,
        DnsUdpReceiveFrameResult receiveFrameResult,
        DnsResolverStatus resolverStatus,
        DnsMessageStatus messageStatus,
        PacketParseStatus packetStatus,
        UdpReceiveDispatchStatus udpStatus,
        ushort transactionId,
        int queryNameLength,
        uint queryNameFingerprint,
        Ipv4Address answerAddress,
        uint answerTtlSeconds,
        int bytesWritten,
        int responseSize,
        Ipv4Address sourceServer)
    {
        IsInitialized = true;
        Status = status;
        QueryFrameResult = queryFrameResult;
        UdpResult = udpResult;
        ReceiveFrameResult = receiveFrameResult;
        ResolverStatus = resolverStatus;
        MessageStatus = messageStatus;
        PacketStatus = packetStatus;
        UdpStatus = udpStatus;
        TransactionId = transactionId;
        QueryNameLength = queryNameLength;
        QueryNameFingerprint = queryNameFingerprint;
        AnswerAddress = answerAddress;
        AnswerTtlSeconds = answerTtlSeconds;
        BytesWritten = bytesWritten;
        ResponseSize = responseSize;
        SourceServer = sourceServer;
    }

    public DnsResolverTransactionPlanStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == DnsResolverTransactionPlanStatus.Success;

    public DnsUdpQueryFrameResult QueryFrameResult { get; }

    public UdpReceiveDispatchResult UdpResult { get; }

    public DnsUdpReceiveFrameResult ReceiveFrameResult { get; }

    public DnsResolverStatus ResolverStatus { get; }

    public DnsMessageStatus MessageStatus { get; }

    public PacketParseStatus PacketStatus { get; }

    public UdpReceiveDispatchStatus UdpStatus { get; }

    public ushort TransactionId { get; }

    public int QueryNameLength { get; }

    public uint QueryNameFingerprint { get; }

    public Ipv4Address AnswerAddress { get; }

    public uint AnswerTtlSeconds { get; }

    public int BytesWritten { get; }

    public int ResponseSize { get; }

    public Ipv4Address SourceServer { get; }

    public static DnsResolverTransactionPlanResult QuerySuccess(
        DnsUdpQueryFrameResult queryFrameResult,
        ushort transactionId,
        int queryNameLength,
        uint queryNameFingerprint)
    {
        return new DnsResolverTransactionPlanResult(
            DnsResolverTransactionPlanStatus.Success,
            queryFrameResult,
            default,
            default,
            queryFrameResult.ResolverStatus,
            queryFrameResult.MessageStatus,
            queryFrameResult.PacketStatus,
            UdpReceiveDispatchStatus.Success,
            transactionId,
            queryNameLength,
            queryNameFingerprint,
            default,
            0,
            queryFrameResult.BytesWritten,
            0,
            default);
    }

    public static DnsResolverTransactionPlanResult ReceiveSuccess(
        UdpReceiveDispatchResult udpResult,
        DnsUdpReceiveFrameResult receiveFrameResult,
        ushort transactionId,
        int queryNameLength,
        uint queryNameFingerprint,
        uint answerTtlSeconds)
    {
        return new DnsResolverTransactionPlanResult(
            DnsResolverTransactionPlanStatus.Success,
            default,
            udpResult,
            receiveFrameResult,
            receiveFrameResult.ResolverStatus,
            receiveFrameResult.MessageStatus,
            receiveFrameResult.PacketStatus,
            receiveFrameResult.UdpStatus,
            transactionId,
            queryNameLength,
            queryNameFingerprint,
            receiveFrameResult.ResolvedAddress,
            answerTtlSeconds,
            0,
            receiveFrameResult.DnsPayloadLength,
            udpResult.SourceAddress);
    }

    public static DnsResolverTransactionPlanResult Fail(
        DnsResolverTransactionPlanStatus status,
        DnsUdpQueryFrameResult queryFrameResult,
        UdpReceiveDispatchResult udpResult,
        DnsUdpReceiveFrameResult receiveFrameResult,
        DnsResolverStatus resolverStatus,
        DnsMessageStatus messageStatus,
        PacketParseStatus packetStatus,
        UdpReceiveDispatchStatus udpStatus,
        ushort transactionId,
        int queryNameLength,
        uint queryNameFingerprint,
        int responseSize,
        Ipv4Address sourceServer)
    {
        return new DnsResolverTransactionPlanResult(
            status,
            queryFrameResult,
            udpResult,
            receiveFrameResult,
            resolverStatus,
            messageStatus,
            packetStatus,
            udpStatus,
            transactionId,
            queryNameLength,
            queryNameFingerprint,
            default,
            0,
            0,
            responseSize,
            sourceServer);
    }
}

public struct DnsResolverTransactionPlanner
{
    private const uint FingerprintOffset = 2166136261;
    private const uint FingerprintPrime = 16777619;

    private readonly DnsResolverTransactionPlannerInfo _info;
    private DnsResolverTransaction _transaction;
    private int _queryNameLength;
    private uint _queryNameFingerprint;

    private DnsResolverTransactionPlanner(DnsResolverTransactionPlannerInfo info)
    {
        _info = info;
        _transaction = DnsResolverTransaction.Create(info.TransactionId);
        _queryNameLength = 0;
        _queryNameFingerprint = 0;
    }

    public DnsResolverState State => _transaction.State;

    public Ipv4Address ResolvedAddress => _transaction.ResolvedAddress;

    public ushort TransactionId => _info.TransactionId;

    public int QueryNameLength => _queryNameLength;

    public uint QueryNameFingerprint => _queryNameFingerprint;

    public static DnsResolverTransactionPlanner Create(DnsResolverTransactionPlannerInfo info)
    {
        return new DnsResolverTransactionPlanner(info);
    }

    public DnsResolverTransactionPlanStatus TryWriteQueryFrame(
        Span<byte> frame,
        ReadOnlySpan<char> queryName,
        out DnsResolverTransactionPlanResult result)
    {
        DnsResolverTransactionPlanStatus identityStatus = ValidateIdentity();
        if (identityStatus != DnsResolverTransactionPlanStatus.Success)
        {
            result = CreateFailure(identityStatus);
            return result.Status;
        }

        DnsUdpQueryFrameResult queryResult = DnsUdpFrame.TryWriteQueryFrame(
            frame,
            _info.ToQueryFrameInfo(),
            queryName,
            ref _transaction);
        if (!queryResult.Succeeded)
        {
            DnsResolverTransactionPlanStatus status = MapQueryFailure(queryResult);
            result = DnsResolverTransactionPlanResult.Fail(
                status,
                queryResult,
                default,
                default,
                queryResult.ResolverStatus,
                queryResult.MessageStatus,
                queryResult.PacketStatus,
                UdpReceiveDispatchStatus.Success,
                _info.TransactionId,
                _queryNameLength,
                _queryNameFingerprint,
                0,
                default);
            return result.Status;
        }

        _queryNameLength = queryName.Length;
        _queryNameFingerprint = ComputeQueryNameFingerprint(queryName);
        result = DnsResolverTransactionPlanResult.QuerySuccess(
            queryResult,
            _info.TransactionId,
            _queryNameLength,
            _queryNameFingerprint);
        return result.Status;
    }

    public DnsResolverTransactionPlanStatus ReceiveResponse(
        ReadOnlySpan<byte> receiveBuffer,
        out DnsResolverTransactionPlanResult result)
    {
        DnsResolverTransactionPlanStatus identityStatus = ValidateIdentity();
        if (identityStatus != DnsResolverTransactionPlanStatus.Success)
        {
            result = CreateFailure(identityStatus);
            return result.Status;
        }

        UdpReceiveDispatchResult udpResult = UdpReceiveDispatcher.Dispatch(receiveBuffer);
        if (!udpResult.Succeeded)
        {
            result = DnsResolverTransactionPlanResult.Fail(
                DnsResolverTransactionPlanStatus.UdpReceiveFailed,
                default,
                udpResult,
                default,
                DnsResolverStatus.Success,
                DnsMessageStatus.Success,
                udpResult.UdpPacketStatus,
                udpResult.Status,
                _info.TransactionId,
                _queryNameLength,
                _queryNameFingerprint,
                0,
                default);
            return result.Status;
        }

        if (udpResult.SourceAddress != _info.DnsServerAddress)
        {
            result = DnsResolverTransactionPlanResult.Fail(
                DnsResolverTransactionPlanStatus.WrongServer,
                default,
                udpResult,
                default,
                DnsResolverStatus.Success,
                DnsMessageStatus.Success,
                PacketParseStatus.Success,
                udpResult.Status,
                _info.TransactionId,
                _queryNameLength,
                _queryNameFingerprint,
                udpResult.UdpPayloadLength,
                udpResult.SourceAddress);
            return result.Status;
        }

        if (udpResult.DestinationAddress != _info.LocalAddress)
        {
            result = DnsResolverTransactionPlanResult.Fail(
                DnsResolverTransactionPlanStatus.WrongLocalAddress,
                default,
                udpResult,
                default,
                DnsResolverStatus.Success,
                DnsMessageStatus.Success,
                PacketParseStatus.Success,
                udpResult.Status,
                _info.TransactionId,
                _queryNameLength,
                _queryNameFingerprint,
                udpResult.UdpPayloadLength,
                udpResult.SourceAddress);
            return result.Status;
        }

        DnsUdpReceiveFrameResult receiveResult = DnsUdpFrame.ReceiveResponse(
            receiveBuffer,
            DnsUdpFrame.DnsServerPort,
            _info.ClientPort,
            ref _transaction);
        if (!receiveResult.Succeeded)
        {
            DnsResolverTransactionPlanStatus status = MapReceiveFailure(
                receiveBuffer,
                udpResult,
                receiveResult);
            result = DnsResolverTransactionPlanResult.Fail(
                status,
                default,
                udpResult,
                receiveResult,
                receiveResult.ResolverStatus,
                receiveResult.MessageStatus,
                receiveResult.PacketStatus,
                receiveResult.UdpStatus,
                _info.TransactionId,
                _queryNameLength,
                _queryNameFingerprint,
                receiveResult.DnsPayloadLength,
                udpResult.SourceAddress);
            return result.Status;
        }

        uint answerTtlSeconds = 0;
        ReadOnlySpan<byte> dnsPayload = receiveBuffer.Slice(
            udpResult.UdpPayloadOffset,
            udpResult.UdpPayloadLength);
        if (!TryReadFirstAAnswerTtl(dnsPayload, out answerTtlSeconds))
        {
            result = DnsResolverTransactionPlanResult.Fail(
                DnsResolverTransactionPlanStatus.MalformedResponse,
                default,
                udpResult,
                receiveResult,
                receiveResult.ResolverStatus,
                receiveResult.MessageStatus,
                PacketParseStatus.InvalidLength,
                receiveResult.UdpStatus,
                _info.TransactionId,
                _queryNameLength,
                _queryNameFingerprint,
                receiveResult.DnsPayloadLength,
                udpResult.SourceAddress);
            return result.Status;
        }

        result = DnsResolverTransactionPlanResult.ReceiveSuccess(
            udpResult,
            receiveResult,
            _info.TransactionId,
            _queryNameLength,
            _queryNameFingerprint,
            answerTtlSeconds);
        return result.Status;
    }

    private DnsResolverTransactionPlanResult CreateFailure(DnsResolverTransactionPlanStatus status)
    {
        return DnsResolverTransactionPlanResult.Fail(
            status,
            default,
            default,
            default,
            DnsResolverStatus.Success,
            DnsMessageStatus.Success,
            PacketParseStatus.Success,
            UdpReceiveDispatchStatus.Success,
            _info.TransactionId,
            _queryNameLength,
            _queryNameFingerprint,
            0,
            default);
    }

    private DnsResolverTransactionPlanStatus ValidateIdentity()
    {
        if (_info.LocalMac.IsZero
            || _info.LocalMac.IsBroadcast
            || _info.LocalAddress == default
            || _info.ClientPort == 0)
        {
            return DnsResolverTransactionPlanStatus.InvalidLocalIdentity;
        }

        if (_info.NextHopMac.IsZero
            || _info.NextHopMac.IsBroadcast
            || _info.DnsServerAddress == default)
        {
            return DnsResolverTransactionPlanStatus.InvalidServerIdentity;
        }

        return DnsResolverTransactionPlanStatus.Success;
    }

    private static DnsResolverTransactionPlanStatus MapQueryFailure(
        in DnsUdpQueryFrameResult result)
    {
        if (result.ResolverStatus == DnsResolverStatus.InvalidState)
        {
            return DnsResolverTransactionPlanStatus.InvalidState;
        }

        if (result.MessageStatus == DnsMessageStatus.InvalidLabel)
        {
            return DnsResolverTransactionPlanStatus.InvalidQueryName;
        }

        return DnsResolverTransactionPlanStatus.QueryFrameFailed;
    }

    private static DnsResolverTransactionPlanStatus MapReceiveFailure(
        ReadOnlySpan<byte> receiveBuffer,
        in UdpReceiveDispatchResult udpResult,
        in DnsUdpReceiveFrameResult receiveResult)
    {
        if (receiveResult.Status == DnsUdpFrameStatus.UdpDispatchFailed)
        {
            return DnsResolverTransactionPlanStatus.UdpReceiveFailed;
        }

        if (receiveResult.Status == DnsUdpFrameStatus.WrongUdpPorts)
        {
            return DnsResolverTransactionPlanStatus.WrongUdpPorts;
        }

        if (receiveResult.ResolverStatus == DnsResolverStatus.InvalidState)
        {
            return DnsResolverTransactionPlanStatus.InvalidState;
        }

        if (receiveResult.ResolverStatus == DnsResolverStatus.TransactionIdMismatch)
        {
            return DnsResolverTransactionPlanStatus.WrongTransaction;
        }

        if (receiveResult.ResolverStatus == DnsResolverStatus.QuestionMismatch
            || receiveResult.ResolverStatus == DnsResolverStatus.AnswerNameMismatch)
        {
            return DnsResolverTransactionPlanStatus.ResolverParseFailed;
        }

        return receiveResult.MessageStatus switch
        {
            DnsMessageStatus.NoAnswer => DnsResolverTransactionPlanStatus.NoAnswer,
            DnsMessageStatus.UnsupportedType => DnsResolverTransactionPlanStatus.UnsupportedAnswerType,
            DnsMessageStatus.ResponseCodeFailure when IsNxdomain(receiveBuffer, udpResult) =>
                DnsResolverTransactionPlanStatus.Nxdomain,
            DnsMessageStatus.BufferTooSmall or
                DnsMessageStatus.NotResponse or
                DnsMessageStatus.UnsupportedOpcode or
                DnsMessageStatus.TruncatedResponse or
                DnsMessageStatus.UnsupportedQuestionCount or
                DnsMessageStatus.TruncatedQuestion or
                DnsMessageStatus.TruncatedAnswer or
                DnsMessageStatus.CompressionPointerLoop or
                DnsMessageStatus.CompressionPointerOutOfRange or
                DnsMessageStatus.InvalidLength or
                DnsMessageStatus.InvalidLabel =>
                DnsResolverTransactionPlanStatus.MalformedResponse,
            _ => DnsResolverTransactionPlanStatus.ResolverParseFailed
        };
    }

    private static bool IsNxdomain(
        ReadOnlySpan<byte> receiveBuffer,
        in UdpReceiveDispatchResult udpResult)
    {
        if (udpResult.UdpPayloadLength < DnsMessage.HeaderLength)
        {
            return false;
        }

        ReadOnlySpan<byte> dnsPayload = receiveBuffer.Slice(
            udpResult.UdpPayloadOffset,
            udpResult.UdpPayloadLength);
        ushort flags = EndianPrimitives.ReadUInt16BigEndian(dnsPayload.Slice(2, 2));
        return (flags & DnsMessage.ResponseCodeMask) == 3;
    }

    private static bool TryReadFirstAAnswerTtl(ReadOnlySpan<byte> message, out uint ttl)
    {
        ttl = 0;
        if (message.Length < DnsMessage.HeaderLength)
        {
            return false;
        }

        if (!TrySkipName(message, DnsMessage.HeaderLength, out int offset))
        {
            return false;
        }

        if (message.Length - offset < 4)
        {
            return false;
        }

        offset += 4;
        if (!TrySkipName(message, offset, out offset))
        {
            return false;
        }

        if (message.Length - offset < 10)
        {
            return false;
        }

        ushort type = EndianPrimitives.ReadUInt16BigEndian(message.Slice(offset, 2));
        ushort recordClass = EndianPrimitives.ReadUInt16BigEndian(message.Slice(offset + 2, 2));
        uint answerTtl = EndianPrimitives.ReadUInt32BigEndian(message.Slice(offset + 4, 4));
        ushort dataLength = EndianPrimitives.ReadUInt16BigEndian(message.Slice(offset + 8, 2));
        if (type != DnsMessage.TypeA || recordClass != DnsMessage.ClassIn || dataLength != 4)
        {
            return false;
        }

        ttl = answerTtl;
        return true;
    }

    private static bool TrySkipName(ReadOnlySpan<byte> message, int offset, out int nextOffset)
    {
        nextOffset = offset;
        if ((uint)offset >= (uint)message.Length)
        {
            return false;
        }

        int cursor = offset;
        int consumedEnd = offset;
        int expandedLength = 0;
        int jumps = 0;
        bool jumped = false;

        while (true)
        {
            if ((uint)cursor >= (uint)message.Length)
            {
                return false;
            }

            byte length = message[cursor];
            byte labelKind = (byte)(length & 0xC0);
            if (labelKind == 0)
            {
                cursor++;
                if (length == 0)
                {
                    if (!jumped)
                    {
                        consumedEnd = cursor;
                    }

                    nextOffset = consumedEnd;
                    return expandedLength + 1 <= DnsMessage.MaxNameLength;
                }

                if (length > DnsMessage.MaxLabelLength || message.Length - cursor < length)
                {
                    return false;
                }

                expandedLength += length + 1;
                if (expandedLength > DnsMessage.MaxNameLength)
                {
                    return false;
                }

                cursor += length;
                if (!jumped)
                {
                    consumedEnd = cursor;
                }

                continue;
            }

            if (labelKind != 0xC0 || message.Length - cursor < 2)
            {
                return false;
            }

            int pointerOffset = ((length & 0x3F) << 8) | message[cursor + 1];
            if ((uint)pointerOffset >= (uint)message.Length || pointerOffset == cursor)
            {
                return false;
            }

            if (!jumped)
            {
                consumedEnd = cursor + 2;
                jumped = true;
            }

            jumps++;
            if (jumps > 16)
            {
                return false;
            }

            cursor = pointerOffset;
        }
    }

    private static uint ComputeQueryNameFingerprint(ReadOnlySpan<char> queryName)
    {
        uint fingerprint = FingerprintOffset;
        for (int index = 0; index < queryName.Length; index++)
        {
            fingerprint ^= queryName[index];
            fingerprint *= FingerprintPrime;
        }

        return fingerprint;
    }
}
