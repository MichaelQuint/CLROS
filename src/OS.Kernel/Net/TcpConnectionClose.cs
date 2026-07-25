namespace OS.Net;

public enum TcpConnectionCloseState
{
    Established = 0,
    FinWait1 = 1,
    FinWait2 = 2,
    CloseWait = 3,
    LastAck = 4,
    TimeWait = 5,
    Closed = 6,
    Reset = 7
}

public enum TcpConnectionCloseStatus
{
    Success = 0,
    InvalidState = 1,
    BufferTooSmall = 2,
    SegmentWriteFailed = 3,
    SegmentParseFailed = 4,
    UnexpectedPorts = 5,
    InvalidFlags = 6,
    WrongAcknowledgementNumber = 7,
    WrongSequenceNumber = 8,
    UnexpectedPayload = 9
}

public readonly struct TcpConnectionCloseTransitionResult
{
    private TcpConnectionCloseTransitionResult(
        TcpConnectionCloseStatus status,
        TcpConnectionCloseState state,
        PacketParseStatus packetStatus,
        int bytesWritten,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        PacketStatus = packetStatus;
        BytesWritten = bytesWritten;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public TcpConnectionCloseStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == TcpConnectionCloseStatus.Success;

    public TcpConnectionCloseState State { get; }

    public PacketParseStatus PacketStatus { get; }

    public int BytesWritten { get; }

    public uint LocalNextSequenceNumber { get; }

    public uint RemoteNextSequenceNumber { get; }

    public static TcpConnectionCloseTransitionResult Success(
        TcpConnectionCloseState state,
        PacketParseStatus packetStatus,
        int bytesWritten,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpConnectionCloseTransitionResult(
            TcpConnectionCloseStatus.Success,
            state,
            packetStatus,
            bytesWritten,
            localNextSequenceNumber,
            remoteNextSequenceNumber);
    }

    public static TcpConnectionCloseTransitionResult Fail(
        TcpConnectionCloseStatus status,
        TcpConnectionCloseState state,
        PacketParseStatus packetStatus,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpConnectionCloseTransitionResult(
            status,
            state,
            packetStatus,
            bytesWritten: 0,
            localNextSequenceNumber,
            remoteNextSequenceNumber);
    }
}

public struct TcpConnectionClose
{
    public const byte FlagFin = 0x01;
    public const byte FlagSyn = 0x02;
    public const byte FlagRst = 0x04;
    public const byte FlagPsh = 0x08;
    public const byte FlagAck = 0x10;
    public const byte FlagUrg = 0x20;
    public const byte FlagEce = 0x40;
    public const byte FlagCwr = 0x80;

    private const byte ResetAllowedFlags = FlagRst | FlagAck;

    private TcpConnectionClose(
        ushort localPort,
        ushort remotePort,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        LocalPort = localPort;
        RemotePort = remotePort;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
        State = TcpConnectionCloseState.Established;
    }

    public TcpConnectionCloseState State { get; private set; }

    public ushort LocalPort { get; }

    public ushort RemotePort { get; }

    public uint LocalNextSequenceNumber { get; private set; }

    public uint RemoteNextSequenceNumber { get; private set; }

    public uint AcknowledgementNumber => RemoteNextSequenceNumber;

    public static TcpConnectionClose CreateEstablished(
        ushort localPort,
        ushort remotePort,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpConnectionClose(
            localPort,
            remotePort,
            localNextSequenceNumber,
            remoteNextSequenceNumber);
    }

    public TcpConnectionCloseStatus TryWriteActiveFinAck(
        Span<byte> segment,
        ushort windowSize,
        ushort checksum,
        out TcpConnectionCloseTransitionResult result)
    {
        if (State != TcpConnectionCloseState.Established)
        {
            result = CreateFailure(TcpConnectionCloseStatus.InvalidState, PacketParseStatus.Success);
            return result.Status;
        }

        PacketParseStatus writeStatus = TryWriteLocalSegment(
            segment,
            FlagFin | FlagAck,
            windowSize,
            checksum,
            out int bytesWritten);
        if (writeStatus != PacketParseStatus.Success)
        {
            TcpConnectionCloseStatus status = MapWriteStatus(writeStatus);
            result = CreateFailure(status, writeStatus);
            return status;
        }

        LocalNextSequenceNumber = AddSequence(LocalNextSequenceNumber, 1);
        State = TcpConnectionCloseState.FinWait1;
        result = TcpConnectionCloseTransitionResult.Success(
            State,
            writeStatus,
            bytesWritten,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpConnectionCloseStatus ReceiveActiveFinAcknowledgement(
        ReadOnlySpan<byte> segment,
        out TcpConnectionCloseTransitionResult result)
    {
        if (State != TcpConnectionCloseState.FinWait1)
        {
            result = CreateFailure(TcpConnectionCloseStatus.InvalidState, PacketParseStatus.Success);
            return result.Status;
        }

        TcpConnectionCloseStatus status = ValidateRemoteControlSegment(
            segment,
            FlagAck,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber,
            out PacketParseStatus packetStatus);
        if (status != TcpConnectionCloseStatus.Success)
        {
            result = CreateFailure(status, packetStatus);
            return status;
        }

        State = TcpConnectionCloseState.FinWait2;
        result = TcpConnectionCloseTransitionResult.Success(
            State,
            packetStatus,
            bytesWritten: 0,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpConnectionCloseStatus ReceiveActiveFinAckAndWriteAck(
        ReadOnlySpan<byte> inboundSegment,
        Span<byte> outboundSegment,
        ushort windowSize,
        ushort checksum,
        out TcpConnectionCloseTransitionResult result)
    {
        if (State != TcpConnectionCloseState.FinWait2)
        {
            result = CreateFailure(TcpConnectionCloseStatus.InvalidState, PacketParseStatus.Success);
            return result.Status;
        }

        return ReceiveFinAckAndWriteAck(
            inboundSegment,
            outboundSegment,
            TcpConnectionCloseState.TimeWait,
            windowSize,
            checksum,
            out result);
    }

    public TcpConnectionCloseStatus ReceivePassiveFinAckAndWriteAck(
        ReadOnlySpan<byte> inboundSegment,
        Span<byte> outboundSegment,
        ushort windowSize,
        ushort checksum,
        out TcpConnectionCloseTransitionResult result)
    {
        if (State != TcpConnectionCloseState.Established)
        {
            result = CreateFailure(TcpConnectionCloseStatus.InvalidState, PacketParseStatus.Success);
            return result.Status;
        }

        return ReceiveFinAckAndWriteAck(
            inboundSegment,
            outboundSegment,
            TcpConnectionCloseState.CloseWait,
            windowSize,
            checksum,
            out result);
    }

    public TcpConnectionCloseStatus TryWritePassiveFinAck(
        Span<byte> segment,
        ushort windowSize,
        ushort checksum,
        out TcpConnectionCloseTransitionResult result)
    {
        if (State != TcpConnectionCloseState.CloseWait)
        {
            result = CreateFailure(TcpConnectionCloseStatus.InvalidState, PacketParseStatus.Success);
            return result.Status;
        }

        PacketParseStatus writeStatus = TryWriteLocalSegment(
            segment,
            FlagFin | FlagAck,
            windowSize,
            checksum,
            out int bytesWritten);
        if (writeStatus != PacketParseStatus.Success)
        {
            TcpConnectionCloseStatus status = MapWriteStatus(writeStatus);
            result = CreateFailure(status, writeStatus);
            return status;
        }

        LocalNextSequenceNumber = AddSequence(LocalNextSequenceNumber, 1);
        State = TcpConnectionCloseState.LastAck;
        result = TcpConnectionCloseTransitionResult.Success(
            State,
            writeStatus,
            bytesWritten,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpConnectionCloseStatus ReceivePassiveFinAcknowledgement(
        ReadOnlySpan<byte> segment,
        out TcpConnectionCloseTransitionResult result)
    {
        if (State != TcpConnectionCloseState.LastAck)
        {
            result = CreateFailure(TcpConnectionCloseStatus.InvalidState, PacketParseStatus.Success);
            return result.Status;
        }

        TcpConnectionCloseStatus status = ValidateRemoteControlSegment(
            segment,
            FlagAck,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber,
            out PacketParseStatus packetStatus);
        if (status != TcpConnectionCloseStatus.Success)
        {
            result = CreateFailure(status, packetStatus);
            return status;
        }

        State = TcpConnectionCloseState.Closed;
        result = TcpConnectionCloseTransitionResult.Success(
            State,
            packetStatus,
            bytesWritten: 0,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpConnectionCloseStatus ReceiveReset(
        ReadOnlySpan<byte> segment,
        out TcpConnectionCloseTransitionResult result)
    {
        if (State == TcpConnectionCloseState.Closed || State == TcpConnectionCloseState.Reset)
        {
            result = CreateFailure(TcpConnectionCloseStatus.InvalidState, PacketParseStatus.Success);
            return result.Status;
        }

        PacketParseStatus parseStatus = TcpHeader.TryParse(
            segment,
            out TcpHeader header,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> payload);
        if (parseStatus != PacketParseStatus.Success)
        {
            result = CreateFailure(TcpConnectionCloseStatus.SegmentParseFailed, parseStatus);
            return result.Status;
        }

        TcpConnectionCloseStatus validationStatus = ValidateRemoteHeader(
            header,
            ResetAllowedFlags,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber,
            requireExactFlags: false);
        if (validationStatus != TcpConnectionCloseStatus.Success)
        {
            result = CreateFailure(validationStatus, parseStatus);
            return validationStatus;
        }

        if (payload.Length != 0)
        {
            result = CreateFailure(TcpConnectionCloseStatus.UnexpectedPayload, parseStatus);
            return result.Status;
        }

        State = TcpConnectionCloseState.Reset;
        result = TcpConnectionCloseTransitionResult.Success(
            State,
            parseStatus,
            bytesWritten: 0,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return result.Status;
    }

    private TcpConnectionCloseStatus ReceiveFinAckAndWriteAck(
        ReadOnlySpan<byte> inboundSegment,
        Span<byte> outboundSegment,
        TcpConnectionCloseState targetState,
        ushort windowSize,
        ushort checksum,
        out TcpConnectionCloseTransitionResult result)
    {
        TcpConnectionCloseStatus inboundStatus = ValidateRemoteControlSegment(
            inboundSegment,
            FlagFin | FlagAck,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber,
            out PacketParseStatus packetStatus);
        if (inboundStatus != TcpConnectionCloseStatus.Success)
        {
            result = CreateFailure(inboundStatus, packetStatus);
            return inboundStatus;
        }

        uint nextRemoteSequenceNumber = AddSequence(RemoteNextSequenceNumber, 1);
        PacketParseStatus writeStatus = TcpHeader.TryWrite(
            outboundSegment,
            LocalPort,
            RemotePort,
            LocalNextSequenceNumber,
            nextRemoteSequenceNumber,
            FlagAck,
            windowSize,
            checksum,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            ReadOnlySpan<byte>.Empty,
            out int bytesWritten);
        if (writeStatus != PacketParseStatus.Success)
        {
            TcpConnectionCloseStatus status = MapWriteStatus(writeStatus);
            result = CreateFailure(status, writeStatus);
            return status;
        }

        RemoteNextSequenceNumber = nextRemoteSequenceNumber;
        State = targetState;
        result = TcpConnectionCloseTransitionResult.Success(
            State,
            writeStatus,
            bytesWritten,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return result.Status;
    }

    private TcpConnectionCloseStatus ValidateRemoteControlSegment(
        ReadOnlySpan<byte> segment,
        byte expectedFlags,
        uint expectedAcknowledgementNumber,
        uint expectedSequenceNumber,
        out PacketParseStatus packetStatus)
    {
        packetStatus = TcpHeader.TryParse(
            segment,
            out TcpHeader header,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> payload);
        if (packetStatus != PacketParseStatus.Success)
        {
            return TcpConnectionCloseStatus.SegmentParseFailed;
        }

        TcpConnectionCloseStatus headerStatus = ValidateRemoteHeader(
            header,
            expectedFlags,
            expectedAcknowledgementNumber,
            expectedSequenceNumber,
            requireExactFlags: true);
        if (headerStatus != TcpConnectionCloseStatus.Success)
        {
            return headerStatus;
        }

        return payload.Length == 0
            ? TcpConnectionCloseStatus.Success
            : TcpConnectionCloseStatus.UnexpectedPayload;
    }

    private TcpConnectionCloseStatus ValidateRemoteHeader(
        TcpHeader header,
        byte expectedFlags,
        uint expectedAcknowledgementNumber,
        uint expectedSequenceNumber,
        bool requireExactFlags)
    {
        if (header.SourcePort != RemotePort || header.DestinationPort != LocalPort)
        {
            return TcpConnectionCloseStatus.UnexpectedPorts;
        }

        if (requireExactFlags)
        {
            if (header.Flags != expectedFlags)
            {
                return TcpConnectionCloseStatus.InvalidFlags;
            }
        }
        else if ((header.Flags & FlagRst) == 0 || (header.Flags & ~expectedFlags) != 0)
        {
            return TcpConnectionCloseStatus.InvalidFlags;
        }

        if (header.SequenceNumber != expectedSequenceNumber)
        {
            return TcpConnectionCloseStatus.WrongSequenceNumber;
        }

        if ((header.Flags & FlagAck) != 0
            && header.AcknowledgementNumber != expectedAcknowledgementNumber)
        {
            return TcpConnectionCloseStatus.WrongAcknowledgementNumber;
        }

        return TcpConnectionCloseStatus.Success;
    }

    private PacketParseStatus TryWriteLocalSegment(
        Span<byte> segment,
        byte flags,
        ushort windowSize,
        ushort checksum,
        out int bytesWritten)
    {
        return TcpHeader.TryWrite(
            segment,
            LocalPort,
            RemotePort,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber,
            flags,
            windowSize,
            checksum,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            ReadOnlySpan<byte>.Empty,
            out bytesWritten);
    }

    private TcpConnectionCloseTransitionResult CreateFailure(
        TcpConnectionCloseStatus status,
        PacketParseStatus packetStatus)
    {
        return TcpConnectionCloseTransitionResult.Fail(
            status,
            State,
            packetStatus,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
    }

    private static TcpConnectionCloseStatus MapWriteStatus(PacketParseStatus status)
    {
        return status == PacketParseStatus.BufferTooSmall
            ? TcpConnectionCloseStatus.BufferTooSmall
            : TcpConnectionCloseStatus.SegmentWriteFailed;
    }

    private static uint AddSequence(uint sequenceNumber, int bytes)
    {
        return unchecked(sequenceNumber + (uint)bytes);
    }
}
