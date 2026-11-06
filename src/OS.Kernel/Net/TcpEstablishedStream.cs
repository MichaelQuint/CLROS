namespace OS.Net;

public ref struct TcpEstablishedStream
{
    public const byte FlagFin = 0x01;
    public const byte FlagSyn = 0x02;
    public const byte FlagRst = 0x04;
    public const byte FlagPsh = 0x08;
    public const byte FlagAck = 0x10;
    public const byte FlagUrg = 0x20;
    public const byte FlagEce = 0x40;
    public const byte FlagCwr = 0x80;
    private const byte AllowedInboundFlags = FlagPsh | FlagAck;
    private Span<byte> _receiveBuffer;
    private int _receiveOffset;
    private int _unreadBytes;
    private uint _minimumAcknowledgement;
    private TcpEstablishedStream(ushort localPort, ushort remotePort, uint localNextSequenceNumber, uint remoteNextSequenceNumber, Span<byte> receiveBuffer)
    {
        LocalPort = localPort;
        RemotePort = remotePort;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
        _receiveBuffer = receiveBuffer;
        _receiveOffset = 0;
        _unreadBytes = 0;
        _minimumAcknowledgement = localNextSequenceNumber;
    }

    public ushort LocalPort
    {
        get;
    }
    public ushort RemotePort
    {
        get;
    }
    public uint LocalNextSequenceNumber
    {
        get; private set;
    }
    public uint RemoteNextSequenceNumber
    {
        get; private set;
    }
    public uint AcknowledgementNumber => RemoteNextSequenceNumber;
    public int UnreadBytes => _unreadBytes;
    public int ReceiveCapacity => _receiveBuffer.Length;
    public int ReceiveAvailable => _receiveBuffer.Length - _unreadBytes;

    public static TcpEstablishedStream Create(ushort localPort, ushort remotePort, uint localNextSequenceNumber, uint remoteNextSequenceNumber, Span<byte> receiveBuffer)
    {
        return new TcpEstablishedStream(localPort, remotePort, localNextSequenceNumber, remoteNextSequenceNumber, receiveBuffer);
    }

    public TcpEstablishedStreamStatus ReceiveSegment(scoped ReadOnlySpan<byte> segment, out TcpEstablishedStreamReceiveResult result) => ReceiveSegmentCore(segment, false, out result);
    /// <summary>Receive only the new contiguous part within the free window.
    /// Trim an already-received prefix and a suffix beyond capacity. Gaps are
    /// dropped without buffering. Duplicate/gap/full statuses require a cumulative
    /// ACK from the live owner. This helper owns no checksums, TX or persistent
    /// lifetime; caller-owned buffer contents survive only through its lease.</summary>

    public TcpEstablishedStreamStatus ReceiveWindowSegment(scoped ReadOnlySpan<byte> segment, out TcpEstablishedStreamReceiveResult result) => ReceiveSegmentCore(segment, true, out result);
    // FIN sequence consumption/EOF belongs to the retained receive owner after
    // these original payload bytes fit its window; never fabricate a stripped packet.
    internal TcpEstablishedStreamStatus ReceiveWindowFinSegment(scoped ReadOnlySpan<byte> segment, out TcpEstablishedStreamReceiveResult result) => ReceiveSegmentCore(segment, true, out result, true);
    private TcpEstablishedStreamStatus ReceiveSegmentCore(scoped ReadOnlySpan<byte> segment, bool windowed, out TcpEstablishedStreamReceiveResult result, bool allowFin = false)
    {
        PacketParseStatus parseStatus = TcpHeader.TryParse(segment, out TcpHeader header, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> payload);
        if (parseStatus != PacketParseStatus.Success)
        {
            result = CreateReceiveFailure(TcpEstablishedStreamStatus.SegmentParseFailed, parseStatus);
            return result.Status;
        }

        if (header.SourcePort != RemotePort || header.DestinationPort != LocalPort)
        {
            result = CreateReceiveFailure(TcpEstablishedStreamStatus.UnexpectedPorts, parseStatus);
            return result.Status;
        }

        if (!HasAllowedPayloadFlags(header.Flags) && !(allowFin && (header.Flags & (FlagFin | FlagAck)) == (FlagFin | FlagAck) && (header.Flags & ~0x19) == 0))
        {
            result = CreateReceiveFailure(TcpEstablishedStreamStatus.InvalidFlags, parseStatus);
            return result.Status;
        }

        bool validAcknowledgement = windowed ? unchecked(header.AcknowledgementNumber - _minimumAcknowledgement) <= unchecked(LocalNextSequenceNumber - _minimumAcknowledgement) : header.AcknowledgementNumber == LocalNextSequenceNumber;
        // The retained close dispatcher already admitted this original ACK.
        // An old cumulative ACK releases no send bytes but may carry new data.
        if (allowFin && unchecked((int)(header.AcknowledgementNumber - _minimumAcknowledgement)) < 0)
            validAcknowledgement = true;
        if (!validAcknowledgement)
        {
            result = CreateReceiveFailure(TcpEstablishedStreamStatus.InvalidAcknowledgementNumber, parseStatus);
            return result.Status;
        }

        if (payload.Length == 0)
        {
            if (windowed)
            {
                int delta = unchecked((int)(header.SequenceNumber - RemoteNextSequenceNumber));
                if (delta < 0 || (delta != 0 && delta >= ReceiveAvailable))
                {
                    result = CreateReceiveFailure(delta < 0 ? TcpEstablishedStreamStatus.DuplicateSegment : TcpEstablishedStreamStatus.OutOfOrderSegment, parseStatus);
                    return result.Status;
                }
            }

            result = CreateReceiveFailure(TcpEstablishedStreamStatus.NoPayload, parseStatus);
            return result.Status;
        }

        int sequenceComparison = CompareSequence(header.SequenceNumber, RemoteNextSequenceNumber);
        if (sequenceComparison < 0)
        {
            uint oldBytes = unchecked(RemoteNextSequenceNumber - header.SequenceNumber);
            if (windowed && oldBytes < (uint)payload.Length)
                payload = payload.Slice((int)oldBytes);
            else
            {
                result = CreateReceiveFailure(TcpEstablishedStreamStatus.DuplicateSegment, parseStatus);
                return result.Status;
            }
        }

        if (sequenceComparison > 0)
        {
            result = CreateReceiveFailure(TcpEstablishedStreamStatus.OutOfOrderSegment, parseStatus);
            return result.Status;
        }

        if (payload.Length > ReceiveAvailable)
        {
            if (windowed && ReceiveAvailable > 0)
                payload = payload.Slice(0, ReceiveAvailable);
            else
            {
                result = CreateReceiveFailure(TcpEstablishedStreamStatus.ReceiveBufferTooSmall, parseStatus);
                return result.Status;
            }
        }

        EnsureContiguousTail(payload.Length);
        int appendOffset = _receiveOffset + _unreadBytes;
        Copy(payload, _receiveBuffer.Slice(appendOffset, payload.Length));
        _unreadBytes += payload.Length;
        RemoteNextSequenceNumber = AddSequence(RemoteNextSequenceNumber, payload.Length);
        result = TcpEstablishedStreamReceiveResult.Success(payload.Length, _unreadBytes, LocalNextSequenceNumber, RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpEstablishedStreamStatus Read(Span<byte> destination, out TcpEstablishedStreamReadResult result)
    {
        if (_unreadBytes == 0)
        {
            result = TcpEstablishedStreamReadResult.Fail(TcpEstablishedStreamStatus.NoData, _unreadBytes, AcknowledgementNumber);
            return result.Status;
        }

        if (destination.Length == 0)
        {
            result = TcpEstablishedStreamReadResult.Fail(TcpEstablishedStreamStatus.DestinationTooSmall, _unreadBytes, AcknowledgementNumber);
            return result.Status;
        }

        int bytesToRead = destination.Length < _unreadBytes ? destination.Length : _unreadBytes;
        Copy(_receiveBuffer.Slice(_receiveOffset, bytesToRead), destination.Slice(0, bytesToRead));
        _receiveOffset += bytesToRead;
        _unreadBytes -= bytesToRead;
        if (_unreadBytes == 0)
        {
            _receiveOffset = 0;
        }

        result = TcpEstablishedStreamReadResult.Success(bytesToRead, _unreadBytes, AcknowledgementNumber);
        return result.Status;
    }

    public TcpEstablishedStreamStatus TryWritePayloadSegment(ReadOnlySpan<byte> payload, int maximumPayloadLength, Span<byte> segment, ushort windowSize, ushort checksum, out TcpEstablishedStreamSendResult result)
    {
        uint sequenceNumber = LocalNextSequenceNumber;
        uint acknowledgementNumber = AcknowledgementNumber;
        if (payload.Length == 0)
        {
            result = CreateSendFailure(TcpEstablishedStreamStatus.NoPayload, PacketParseStatus.Success, sequenceNumber, acknowledgementNumber);
            return result.Status;
        }

        if (maximumPayloadLength <= 0)
        {
            result = CreateSendFailure(TcpEstablishedStreamStatus.InvalidMaximumPayloadLength, PacketParseStatus.Success, sequenceNumber, acknowledgementNumber);
            return result.Status;
        }

        int payloadBytesToWrite = payload.Length < maximumPayloadLength ? payload.Length : maximumPayloadLength;
        PacketParseStatus writeStatus = TcpHeader.TryWrite(segment, LocalPort, RemotePort, sequenceNumber, acknowledgementNumber, FlagPsh | FlagAck, windowSize, checksum, urgentPointer: 0, ReadOnlySpan<byte>.Empty, payload.Slice(0, payloadBytesToWrite), out int bytesWritten);
        if (writeStatus != PacketParseStatus.Success)
        {
            TcpEstablishedStreamStatus status = MapWriteStatus(writeStatus);
            result = CreateSendFailure(status, writeStatus, sequenceNumber, acknowledgementNumber);
            return status;
        }

        LocalNextSequenceNumber = AddSequence(LocalNextSequenceNumber, payloadBytesToWrite);
        result = TcpEstablishedStreamSendResult.Success(bytesWritten, payloadBytesToWrite, sequenceNumber, acknowledgementNumber, LocalNextSequenceNumber, RemoteNextSequenceNumber);
        return result.Status;
    }

    private TcpEstablishedStreamReceiveResult CreateReceiveFailure(TcpEstablishedStreamStatus status, PacketParseStatus packetStatus)
    {
        return TcpEstablishedStreamReceiveResult.Fail(status, packetStatus, _unreadBytes, LocalNextSequenceNumber, RemoteNextSequenceNumber);
    }

    private TcpEstablishedStreamSendResult CreateSendFailure(TcpEstablishedStreamStatus status, PacketParseStatus packetStatus, uint sequenceNumber, uint acknowledgementNumber)
    {
        return TcpEstablishedStreamSendResult.Fail(status, packetStatus, sequenceNumber, acknowledgementNumber, LocalNextSequenceNumber, RemoteNextSequenceNumber);
    }

    // The persistent receiver validates its stored bounds/handle before borrowing
    // this temporary view. These APIs never retain a Span in persistent metadata.
    internal int ReceiveOffset => _receiveOffset;

    internal static TcpEstablishedStream Restore(ushort localPort, ushort remotePort, uint localNext, uint remoteNext, Span<byte> buffer, int offset, int unread, uint minimumAcknowledgement)
    {
        var stream = Create(localPort, remotePort, localNext, remoteNext, buffer);
        stream._receiveOffset = offset;
        stream._unreadBytes = unread;
        stream._minimumAcknowledgement = minimumAcknowledgement;
        return stream;
    }

    private void EnsureContiguousTail(int bytesRequired)
    {
        if (_receiveOffset + _unreadBytes + bytesRequired <= _receiveBuffer.Length)
        {
            return;
        }

        for (int index = 0; index < _unreadBytes; index++)
        {
            _receiveBuffer[index] = _receiveBuffer[_receiveOffset + index];
        }

        _receiveOffset = 0;
    }

    private static bool HasAllowedPayloadFlags(byte flags)
    {
        return (flags & FlagAck) != 0 && (flags & ~AllowedInboundFlags) == 0;
    }

    private static TcpEstablishedStreamStatus MapWriteStatus(PacketParseStatus status)
    {
        return status == PacketParseStatus.BufferTooSmall ? TcpEstablishedStreamStatus.DestinationTooSmall : TcpEstablishedStreamStatus.SegmentWriteFailed;
    }

    private static int CompareSequence(uint left, uint right)
    {
        if (left == right)
        {
            return 0;
        }

        return unchecked((int)(left - right)) < 0 ? -1 : 1;
    }

    private static uint AddSequence(uint sequenceNumber, int bytes)
    {
        return unchecked(sequenceNumber + (uint)bytes);
    }

    private static void Copy(ReadOnlySpan<byte> source, Span<byte> destination)
    {
        _ = OS.Std.Memory.BufferOperations.MemoryCopy(source, destination);
    }
}
