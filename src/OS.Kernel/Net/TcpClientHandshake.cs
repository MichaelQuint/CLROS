namespace OS.Net;

public enum TcpClientHandshakeState
{
    Initial = 0,
    SynSent = 1,
    Established = 2,
    Failed = 3
}

public enum TcpClientHandshakeStatus
{
    Success = 0,
    InvalidState = 1,
    BufferTooSmall = 2,
    SegmentWriteFailed = 3,
    SegmentParseFailed = 4,
    UnexpectedPorts = 5,
    InvalidFlags = 6,
    WrongAcknowledgementNumber = 7
}

public readonly struct TcpClientHandshakeTransitionResult
{
    private TcpClientHandshakeTransitionResult(
        TcpClientHandshakeStatus status,
        TcpClientHandshakeState state,
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

    public TcpClientHandshakeStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == TcpClientHandshakeStatus.Success;

    public TcpClientHandshakeState State { get; }

    public PacketParseStatus PacketStatus { get; }

    public int BytesWritten { get; }

    public uint LocalNextSequenceNumber { get; }

    public uint RemoteNextSequenceNumber { get; }

    public static TcpClientHandshakeTransitionResult Success(
        TcpClientHandshakeState state,
        PacketParseStatus packetStatus,
        int bytesWritten,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpClientHandshakeTransitionResult(
            TcpClientHandshakeStatus.Success,
            state,
            packetStatus,
            bytesWritten,
            localNextSequenceNumber,
            remoteNextSequenceNumber);
    }

    public static TcpClientHandshakeTransitionResult Fail(
        TcpClientHandshakeStatus status,
        TcpClientHandshakeState state,
        PacketParseStatus packetStatus,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpClientHandshakeTransitionResult(
            status,
            state,
            packetStatus,
            0,
            localNextSequenceNumber,
            remoteNextSequenceNumber);
    }
}

public struct TcpClientHandshake
{
    public const byte FlagFin = 0x01;
    public const byte FlagSyn = 0x02;
    public const byte FlagRst = 0x04;
    public const byte FlagPsh = 0x08;
    public const byte FlagAck = 0x10;
    public const byte FlagUrg = 0x20;
    public const byte FlagEce = 0x40;
    public const byte FlagCwr = 0x80;

    private bool _synAckAccepted;

    private TcpClientHandshake(
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
        State = TcpClientHandshakeState.Initial;
        _synAckAccepted = false;
    }

    public TcpClientHandshakeState State { get; private set; }

    public ushort LocalPort { get; }

    public ushort RemotePort { get; }

    public uint InitialSequenceNumber { get; }

    public uint LocalNextSequenceNumber { get; private set; }

    public uint RemoteInitialSequenceNumber { get; private set; }

    public uint RemoteNextSequenceNumber { get; private set; }

    public static TcpClientHandshake Create(
        ushort localPort,
        ushort remotePort,
        uint initialSequenceNumber)
    {
        return new TcpClientHandshake(localPort, remotePort, initialSequenceNumber);
    }

    public TcpClientHandshakeStatus TryWriteSyn(
        Span<byte> segment,
        ushort windowSize,
        ushort checksum,
        out TcpClientHandshakeTransitionResult result)
    {
        if (State != TcpClientHandshakeState.Initial)
        {
            result = TcpClientHandshakeTransitionResult.Fail(
                TcpClientHandshakeStatus.InvalidState,
                State,
                PacketParseStatus.Success,
                LocalNextSequenceNumber,
                RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus writeStatus = TcpHeader.TryWrite(
            segment,
            LocalPort,
            RemotePort,
            InitialSequenceNumber,
            acknowledgementNumber: 0,
            FlagSyn,
            windowSize,
            checksum,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            ReadOnlySpan<byte>.Empty,
            out int bytesWritten);
        if (writeStatus != PacketParseStatus.Success)
        {
            TcpClientHandshakeStatus status = MapWriteStatus(writeStatus);
            result = TcpClientHandshakeTransitionResult.Fail(
                status,
                State,
                writeStatus,
                LocalNextSequenceNumber,
                RemoteNextSequenceNumber);
            return status;
        }

        LocalNextSequenceNumber = NextSequence(InitialSequenceNumber);
        State = TcpClientHandshakeState.SynSent;
        result = TcpClientHandshakeTransitionResult.Success(
            State,
            writeStatus,
            bytesWritten,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpClientHandshakeStatus ReceiveSynAck(
        ReadOnlySpan<byte> segment,
        out TcpClientHandshakeTransitionResult result)
    {
        if (State != TcpClientHandshakeState.SynSent)
        {
            result = TcpClientHandshakeTransitionResult.Fail(
                TcpClientHandshakeStatus.InvalidState,
                State,
                PacketParseStatus.Success,
                LocalNextSequenceNumber,
                RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus parseStatus = TcpHeader.TryParse(
            segment,
            out TcpHeader header,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> _);
        if (parseStatus != PacketParseStatus.Success)
        {
            return Fail(
                TcpClientHandshakeStatus.SegmentParseFailed,
                parseStatus,
                out result);
        }

        if (header.SourcePort != RemotePort || header.DestinationPort != LocalPort)
        {
            return Fail(
                TcpClientHandshakeStatus.UnexpectedPorts,
                parseStatus,
                out result);
        }

        if (header.Flags != (FlagSyn | FlagAck))
        {
            return Fail(
                TcpClientHandshakeStatus.InvalidFlags,
                parseStatus,
                out result);
        }

        if (header.AcknowledgementNumber != LocalNextSequenceNumber)
        {
            return Fail(
                TcpClientHandshakeStatus.WrongAcknowledgementNumber,
                parseStatus,
                out result);
        }

        RemoteInitialSequenceNumber = header.SequenceNumber;
        RemoteNextSequenceNumber = NextSequence(header.SequenceNumber);
        _synAckAccepted = true;
        result = TcpClientHandshakeTransitionResult.Success(
            State,
            parseStatus,
            bytesWritten: 0,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpClientHandshakeStatus TryWriteFinalAck(
        Span<byte> segment,
        ushort windowSize,
        ushort checksum,
        out TcpClientHandshakeTransitionResult result)
    {
        if (State != TcpClientHandshakeState.SynSent || !_synAckAccepted)
        {
            result = TcpClientHandshakeTransitionResult.Fail(
                TcpClientHandshakeStatus.InvalidState,
                State,
                PacketParseStatus.Success,
                LocalNextSequenceNumber,
                RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus writeStatus = TcpHeader.TryWrite(
            segment,
            LocalPort,
            RemotePort,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber,
            FlagAck,
            windowSize,
            checksum,
            urgentPointer: 0,
            ReadOnlySpan<byte>.Empty,
            ReadOnlySpan<byte>.Empty,
            out int bytesWritten);
        if (writeStatus != PacketParseStatus.Success)
        {
            TcpClientHandshakeStatus status = MapWriteStatus(writeStatus);
            result = TcpClientHandshakeTransitionResult.Fail(
                status,
                State,
                writeStatus,
                LocalNextSequenceNumber,
                RemoteNextSequenceNumber);
            return status;
        }

        State = TcpClientHandshakeState.Established;
        result = TcpClientHandshakeTransitionResult.Success(
            State,
            writeStatus,
            bytesWritten,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return result.Status;
    }

    private TcpClientHandshakeStatus Fail(
        TcpClientHandshakeStatus status,
        PacketParseStatus packetStatus,
        out TcpClientHandshakeTransitionResult result)
    {
        State = TcpClientHandshakeState.Failed;
        _synAckAccepted = false;
        result = TcpClientHandshakeTransitionResult.Fail(
            status,
            State,
            packetStatus,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
        return status;
    }

    private static TcpClientHandshakeStatus MapWriteStatus(PacketParseStatus status)
    {
        return status == PacketParseStatus.BufferTooSmall
            ? TcpClientHandshakeStatus.BufferTooSmall
            : TcpClientHandshakeStatus.SegmentWriteFailed;
    }

    private static uint NextSequence(uint sequenceNumber)
    {
        return unchecked(sequenceNumber + 1);
    }
}
