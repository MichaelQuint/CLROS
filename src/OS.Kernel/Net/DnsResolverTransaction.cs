namespace OS.Net;
public struct DnsResolverTransaction
{
    private const uint FingerprintOffset = 2166136261;
    private const uint FingerprintPrime = 16777619;
    private int _questionLength;
    private int _questionNameLength;
    private uint _questionFingerprint;
    private uint _questionNameFingerprint;
    private DnsResolverTransaction(ushort transactionId)
    {
        TransactionId = transactionId;
        State = DnsResolverState.Initial;
        ResolvedAddress = default;
        _questionLength = 0;
        _questionNameLength = 0;
        _questionFingerprint = 0;
        _questionNameFingerprint = 0;
    }

    public DnsResolverState State { get; private set; }
    public ushort TransactionId { get; }
    public Ipv4Address ResolvedAddress { get; private set; }

    public static DnsResolverTransaction Create(ushort transactionId)
    {
        return new DnsResolverTransaction(transactionId);
    }

    public DnsResolverStatus TryWriteQuery(Span<byte> message, ReadOnlySpan<char> hostName, out DnsResolverTransitionResult result)
    {
        if (State != DnsResolverState.Initial)
        {
            result = DnsResolverTransitionResult.Fail(DnsResolverStatus.InvalidState, State, DnsMessageStatus.Success);
            return result.Status;
        }

        DnsMessageStatus writeStatus = DnsMessage.TryWriteQuery(message, TransactionId, hostName, out int bytesWritten);
        if (writeStatus != DnsMessageStatus.Success)
        {
            DnsResolverStatus status = MapWriteStatus(writeStatus);
            result = DnsResolverTransitionResult.Fail(status, State, writeStatus);
            return status;
        }

        _questionLength = bytesWritten - DnsMessage.HeaderLength;
        _questionNameLength = _questionLength - 4;
        _questionFingerprint = ComputeFingerprint(message.Slice(DnsMessage.HeaderLength, _questionLength));
        _questionNameFingerprint = ComputeFingerprint(message.Slice(DnsMessage.HeaderLength, _questionNameLength));
        State = DnsResolverState.QueryWritten;
        result = DnsResolverTransitionResult.Success(State, bytesWritten, default);
        return result.Status;
    }

    public DnsResolverStatus ReceiveResponse(ReadOnlySpan<byte> message, out DnsResolverTransitionResult result)
    {
        if (State != DnsResolverState.QueryWritten)
        {
            result = DnsResolverTransitionResult.Fail(DnsResolverStatus.InvalidState, State, DnsMessageStatus.Success);
            return result.Status;
        }

        DnsMessageStatus parseStatus = DnsMessage.TryParseAResponse(message, TransactionId, out Ipv4Address address);
        if (parseStatus != DnsMessageStatus.Success)
        {
            DnsResolverStatus status = MapParseStatus(parseStatus);
            State = DnsResolverState.Failed;
            result = DnsResolverTransitionResult.Fail(status, State, parseStatus);
            return status;
        }

        DnsResolverStatus consistencyStatus = ValidateResponseConsistency(message);
        if (consistencyStatus != DnsResolverStatus.Success)
        {
            State = DnsResolverState.Failed;
            result = DnsResolverTransitionResult.Fail(consistencyStatus, State, DnsMessageStatus.Success);
            return consistencyStatus;
        }

        ResolvedAddress = address;
        State = DnsResolverState.Resolved;
        result = DnsResolverTransitionResult.Success(State, 0, address);
        return result.Status;
    }

    public DnsResolverStatus Timeout(out DnsResolverTransitionResult result)
    {
        if (State != DnsResolverState.QueryWritten)
        {
            result = DnsResolverTransitionResult.Fail(DnsResolverStatus.InvalidState, State, DnsMessageStatus.Success);
            return result.Status;
        }

        State = DnsResolverState.Failed;
        result = DnsResolverTransitionResult.Fail(DnsResolverStatus.Timeout, State, DnsMessageStatus.Success);
        return result.Status;
    }

    private static DnsResolverStatus MapWriteStatus(DnsMessageStatus status)
    {
        return status == DnsMessageStatus.BufferTooSmall ? DnsResolverStatus.BufferTooSmall : DnsResolverStatus.MessageWriteFailed;
    }

    private static DnsResolverStatus MapParseStatus(DnsMessageStatus status)
    {
        return status == DnsMessageStatus.TransactionIdMismatch ? DnsResolverStatus.TransactionIdMismatch : DnsResolverStatus.MessageParseFailed;
    }

    private DnsResolverStatus ValidateResponseConsistency(ReadOnlySpan<byte> message)
    {
        if (!TryReadUncompressedQuestion(message, out int questionLength, out int questionNameLength, out uint questionFingerprint, out uint questionNameFingerprint))
        {
            return DnsResolverStatus.MessageParseFailed;
        }

        if (questionLength != _questionLength || questionFingerprint != _questionFingerprint)
        {
            return DnsResolverStatus.QuestionMismatch;
        }

        if (questionNameLength != _questionNameLength || questionNameFingerprint != _questionNameFingerprint)
        {
            return DnsResolverStatus.QuestionMismatch;
        }

        int answerNameOffset = DnsMessage.HeaderLength + questionLength;
        return AnswerNameMatchesQuery(message, answerNameOffset) ? DnsResolverStatus.Success : DnsResolverStatus.AnswerNameMismatch;
    }

    private bool AnswerNameMatchesQuery(ReadOnlySpan<byte> message, int offset)
    {
        if ((uint)offset >= (uint)message.Length)
        {
            return false;
        }

        byte length = message[offset];
        byte labelKind = (byte)(length & 0xC0);
        if (labelKind == 0xC0)
        {
            if (message.Length - offset < 2)
            {
                return false;
            }

            int pointerOffset = ((length & 0x3F) << 8) | message[offset + 1];
            return pointerOffset == DnsMessage.HeaderLength;
        }

        if (labelKind != 0)
        {
            return false;
        }

        if (!TryReadUncompressedNameLength(message, offset, out int nameLength))
        {
            return false;
        }

        return nameLength == _questionNameLength && ComputeFingerprint(message.Slice(offset, nameLength)) == _questionNameFingerprint;
    }

    private static bool TryReadUncompressedQuestion(ReadOnlySpan<byte> message, out int questionLength, out int questionNameLength, out uint questionFingerprint, out uint questionNameFingerprint)
    {
        questionLength = 0;
        questionNameLength = 0;
        questionFingerprint = 0;
        questionNameFingerprint = 0;
        if (!TryReadUncompressedNameLength(message, DnsMessage.HeaderLength, out questionNameLength))
        {
            return false;
        }

        if (message.Length - DnsMessage.HeaderLength - questionNameLength < 4)
        {
            return false;
        }

        questionLength = questionNameLength + 4;
        ReadOnlySpan<byte> question = message.Slice(DnsMessage.HeaderLength, questionLength);
        questionFingerprint = ComputeFingerprint(question);
        questionNameFingerprint = ComputeFingerprint(question.Slice(0, questionNameLength));
        return true;
    }

    private static bool TryReadUncompressedNameLength(ReadOnlySpan<byte> message, int offset, out int nameLength)
    {
        nameLength = 0;
        if ((uint)offset >= (uint)message.Length)
        {
            return false;
        }

        int cursor = offset;
        while (true)
        {
            if ((uint)cursor >= (uint)message.Length)
            {
                return false;
            }

            byte length = message[cursor];
            if ((length & 0xC0) != 0)
            {
                return false;
            }

            cursor++;
            if (length == 0)
            {
                nameLength = cursor - offset;
                return true;
            }

            if (length > DnsMessage.MaxLabelLength || message.Length - cursor < length)
            {
                return false;
            }

            cursor += length;
        }
    }

    private static uint ComputeFingerprint(ReadOnlySpan<byte> bytes)
    {
        uint fingerprint = FingerprintOffset;
        for (int index = 0; index < bytes.Length; index++)
        {
            fingerprint ^= bytes[index];
            fingerprint *= FingerprintPrime;
        }

        return fingerprint;
    }
}
