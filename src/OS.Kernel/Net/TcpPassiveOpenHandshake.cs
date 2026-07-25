namespace OS.Net;

public enum TcpPassiveOpenHandshakeState
{
    Listen = 0,
    SynReceived = 1,
    Established = 2,
    Failed = 3
}

public enum TcpPassiveOpenHandshakeStatus
{
    Success = 0,
    InvalidState = 1,
    BufferTooSmall = 2,
    SegmentWriteFailed = 3,
    SegmentParseFailed = 4,
    UnexpectedPorts = 5,
    InvalidFlags = 6,
    WrongAcknowledgementNumber = 7,
    WrongSequenceNumber = 8
}

public readonly struct TcpPassiveOpenHandshakeTransitionResult
{
    private TcpPassiveOpenHandshakeTransitionResult(
        TcpPassiveOpenHandshakeStatus status,
        TcpPassiveOpenHandshakeState state,
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

    public TcpPassiveOpenHandshakeStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == TcpPassiveOpenHandshakeStatus.Success;

    public TcpPassiveOpenHandshakeState State { get; }

    public PacketParseStatus PacketStatus { get; }

    public int BytesWritten { get; }

    public uint LocalNextSequenceNumber { get; }

    public uint RemoteNextSequenceNumber { get; }

    public static TcpPassiveOpenHandshakeTransitionResult Success(
        TcpPassiveOpenHandshakeState state,
        PacketParseStatus packetStatus,
        int bytesWritten,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpPassiveOpenHandshakeTransitionResult(
            TcpPassiveOpenHandshakeStatus.Success,
            state,
            packetStatus,
            bytesWritten,
            localNextSequenceNumber,
            remoteNextSequenceNumber);
    }

    public static TcpPassiveOpenHandshakeTransitionResult Fail(
        TcpPassiveOpenHandshakeStatus status,
        TcpPassiveOpenHandshakeState state,
        PacketParseStatus packetStatus,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpPassiveOpenHandshakeTransitionResult(
            status,
            state,
            packetStatus,
            0,
            localNextSequenceNumber,
            remoteNextSequenceNumber);
    }
}

public struct TcpPassiveOpenHandshake
{
    public const byte FlagFin = 0x01;
    public const byte FlagSyn = 0x02;
    public const byte FlagRst = 0x04;
    public const byte FlagPsh = 0x08;
    public const byte FlagAck = 0x10;
    public const byte FlagUrg = 0x20;
    public const byte FlagEce = 0x40;
    public const byte FlagCwr = 0x80;

    private TcpPassiveOpenHandshake(
        ushort localPort,
        ushort remotePort,
        uint initialSequenceNumber)
    {
        LocalPort = localPort;
        RemotePort = remotePort;
        InitialSequenceNumber = initialSequenceNumber;
        LocalNextSequenceNumber = initialSequenceNumber;
        RemoteInitialSequenceNumber = 0;
        RemoteNextSequenceNumber = 0;
        State = TcpPassiveOpenHandshakeState.Listen;
    }

    public TcpPassiveOpenHandshakeState State { get; private set; }

    public ushort LocalPort { get; }

    public ushort RemotePort { get; }

    public uint InitialSequenceNumber { get; }

    public uint LocalNextSequenceNumber { get; private set; }

    public uint RemoteInitialSequenceNumber { get; private set; }

    public uint RemoteNextSequenceNumber { get; private set; }

    public static TcpPassiveOpenHandshake Create(
        ushort localPort,
        ushort remotePort,
        uint initialSequenceNumber)
    {
        return new TcpPassiveOpenHandshake(localPort, remotePort, initialSequenceNumber);
    }

    public TcpPassiveOpenHandshakeStatus ReceiveSynAndWriteSynAck(
        ReadOnlySpan<byte> inboundSegment,
        Span<byte> outboundSegment,
        ushort windowSize,
        ushort checksum,
        out TcpPassiveOpenHandshakeTransitionResult result)
    {
        if (State != TcpPassiveOpenHandshakeState.Listen)
        {
            result = TcpPassiveOpenHandshakeTransitionResult.Fail(
                TcpPassiveOpenHandshakeStatus.InvalidState,
                State,
                PacketParseStatus.Success,
                LocalNextSequenceNumber,
                RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus parseStatus = TcpHeader.TryParse(
            inboundSegment,
            out TcpHeader header,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> _);
        if (parseStatus != PacketParseStatus.Success)
        {
            return Fail(
                TcpPassiveOpenHandshakeStatus.SegmentParseFailed,
                parseStatus,
                out result);
        }

        if (header.SourcePort != RemotePort || header.DestinationPort != LocalPort)
        {
            return Fail(
                TcpPassiveOpenHandshakeStatus.UnexpectedPorts,
                parseStatus,
                out result);
        }

        if (header.Flags != FlagSyn || header.AcknowledgementNumber != 0)
        {
            return Fail(
                TcpPassiveOpenHandshakeStatus.InvalidFlags,
                parseStatus,
                out result);
        }

        uint remoteNextSequenceNumber = NextSequence(header.SequenceNumber);
        PacketParseStatus writeStatus = TcpHeader.TryWrite(
            outboundSegment,
            LocalPort,
            RemotePort,
            InitialSequenceNumber,
            remoteNextSequenceNumber,
            FlagSyn | FlagAck,
            windowSize,
            checksum,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            ReadOnlySpan<byte>.Empty,
            out int bytesWritten);
        if (writeStatus != PacketParseStatus.Success)
        {
            TcpPassiveOpenHandshakeStatus status = MapWriteStatus(writeStatus);
            result = TcpPassiveOpenHandshakeTransitionResult.Fail(
                status,
                State,
                writeStatus,
                LocalNextSequenceNumber,
                RemoteNextSequenceNumber);
            return status;
        }

        RemoteInitialSequenceNumber = header.SequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
        LocalNextSequenceNumber = NextSequence(InitialSequenceNumber);
        State = TcpPassiveOpenHandshakeState.SynReceived;
        result = TcpPassiveOpenHandshakeTransitionResult.Success(
            State,
            writeStatus,
            bytesWritten,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpPassiveOpenHandshakeStatus ReceiveFinalAck(
        ReadOnlySpan<byte> inboundSegment,
        out TcpPassiveOpenHandshakeTransitionResult result)
    {
        if (State != TcpPassiveOpenHandshakeState.SynReceived)
        {
            result = TcpPassiveOpenHandshakeTransitionResult.Fail(
                TcpPassiveOpenHandshakeStatus.InvalidState,
                State,
                PacketParseStatus.Success,
                LocalNextSequenceNumber,
                RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus parseStatus = TcpHeader.TryParse(
            inboundSegment,
            out TcpHeader header,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> _);
        if (parseStatus != PacketParseStatus.Success)
        {
            return Fail(
                TcpPassiveOpenHandshakeStatus.SegmentParseFailed,
                parseStatus,
                out result);
        }

        if (header.SourcePort != RemotePort || header.DestinationPort != LocalPort)
        {
            return Fail(
                TcpPassiveOpenHandshakeStatus.UnexpectedPorts,
                parseStatus,
                out result);
        }

        if (header.Flags != FlagAck)
        {
            return Fail(
                TcpPassiveOpenHandshakeStatus.InvalidFlags,
                parseStatus,
                out result);
        }

        if (header.AcknowledgementNumber != LocalNextSequenceNumber)
        {
            return Fail(
                TcpPassiveOpenHandshakeStatus.WrongAcknowledgementNumber,
                parseStatus,
                out result);
        }

        if (header.SequenceNumber != RemoteNextSequenceNumber)
        {
            return Fail(
                TcpPassiveOpenHandshakeStatus.WrongSequenceNumber,
                parseStatus,
                out result);
        }

        State = TcpPassiveOpenHandshakeState.Established;
        result = TcpPassiveOpenHandshakeTransitionResult.Success(
            State,
            parseStatus,
            bytesWritten: 0,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return result.Status;
    }

    private TcpPassiveOpenHandshakeStatus Fail(
        TcpPassiveOpenHandshakeStatus status,
        PacketParseStatus packetStatus,
        out TcpPassiveOpenHandshakeTransitionResult result)
    {
        State = TcpPassiveOpenHandshakeState.Failed;
        result = TcpPassiveOpenHandshakeTransitionResult.Fail(
            status,
            State,
            packetStatus,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return status;
    }

    private static TcpPassiveOpenHandshakeStatus MapWriteStatus(PacketParseStatus status)
    {
        return status == PacketParseStatus.BufferTooSmall
            ? TcpPassiveOpenHandshakeStatus.BufferTooSmall
            : TcpPassiveOpenHandshakeStatus.SegmentWriteFailed;
    }

    private static uint NextSequence(uint sequenceNumber)
    {
        return unchecked(sequenceNumber + 1);
    }
}
