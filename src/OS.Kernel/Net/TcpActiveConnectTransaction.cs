namespace OS.Net;
public struct TcpActiveConnectTransaction
{
    public const byte FlagFin = 0x01;
    public const byte FlagSyn = 0x02;
    public const byte FlagRst = 0x04;
    public const byte FlagPsh = 0x08;
    public const byte FlagAck = 0x10;
    public const byte FlagUrg = 0x20;
    public const byte FlagEce = 0x40;
    public const byte FlagCwr = 0x80;
    private readonly bool _requiresRemoteInitialSequenceNumber;
    private readonly uint _expectedRemoteInitialSequenceNumber;
    private bool _synAckAccepted;
    private TcpActiveConnectTransaction(ushort localPort, ushort remotePort, uint initialSequenceNumber, bool requiresRemoteInitialSequenceNumber, uint expectedRemoteInitialSequenceNumber)
    {
        LocalPort = localPort;
        RemotePort = remotePort;
        InitialSequenceNumber = initialSequenceNumber;
        LocalNextSequenceNumber = initialSequenceNumber;
        RemoteInitialSequenceNumber = 0;
        RemoteNextSequenceNumber = 0;
        State = TcpActiveConnectState.Closed;
        _requiresRemoteInitialSequenceNumber = requiresRemoteInitialSequenceNumber;
        _expectedRemoteInitialSequenceNumber = expectedRemoteInitialSequenceNumber;
        _synAckAccepted = false;
    }

    public TcpActiveConnectState State { get; private set; }
    public ushort LocalPort { get; }
    public ushort RemotePort { get; }
    public uint InitialSequenceNumber { get; }
    public uint LocalNextSequenceNumber { get; private set; }
    public uint RemoteInitialSequenceNumber { get; private set; }
    public uint RemoteNextSequenceNumber { get; private set; }
    public bool RequiresRemoteInitialSequenceNumber => _requiresRemoteInitialSequenceNumber;
    public uint ExpectedRemoteInitialSequenceNumber => _expectedRemoteInitialSequenceNumber;

    public static TcpActiveConnectTransaction Create(ushort localPort, ushort remotePort, uint initialSequenceNumber)
    {
        return new TcpActiveConnectTransaction(localPort, remotePort, initialSequenceNumber, requiresRemoteInitialSequenceNumber: false, expectedRemoteInitialSequenceNumber: 0);
    }

    public static TcpActiveConnectTransaction Create(ushort localPort, ushort remotePort, uint initialSequenceNumber, uint expectedRemoteInitialSequenceNumber)
    {
        return new TcpActiveConnectTransaction(localPort, remotePort, initialSequenceNumber, requiresRemoteInitialSequenceNumber: true, expectedRemoteInitialSequenceNumber);
    }

    public TcpActiveConnectStatus TryWriteSyn(Span<byte> segment, ushort windowSize, ushort checksum, out TcpActiveConnectTransitionResult result)
    {
        if (State != TcpActiveConnectState.Closed)
        {
            result = TcpActiveConnectTransitionResult.Fail(TcpActiveConnectStatus.InvalidState, State, PacketParseStatus.Success, LocalNextSequenceNumber, RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus writeStatus = TcpHeader.TryWrite(segment, LocalPort, RemotePort, InitialSequenceNumber, acknowledgementNumber: 0, FlagSyn, windowSize, checksum, urgentPointer: 0, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, out int bytesWritten);
        if (writeStatus != PacketParseStatus.Success)
        {
            TcpActiveConnectStatus status = MapWriteStatus(writeStatus);
            result = TcpActiveConnectTransitionResult.Fail(status, State, writeStatus, LocalNextSequenceNumber, RemoteNextSequenceNumber);
            return status;
        }

        LocalNextSequenceNumber = NextSequence(InitialSequenceNumber);
        State = TcpActiveConnectState.SynSent;
        result = TcpActiveConnectTransitionResult.Success(State, writeStatus, bytesWritten, LocalNextSequenceNumber, RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpActiveConnectStatus ReceiveSynAck(ReadOnlySpan<byte> segment, out TcpActiveConnectTransitionResult result)
    {
        if (State != TcpActiveConnectState.SynSent)
        {
            result = TcpActiveConnectTransitionResult.Fail(TcpActiveConnectStatus.InvalidState, State, PacketParseStatus.Success, LocalNextSequenceNumber, RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus parseStatus = TcpHeader.TryParse(segment, out TcpHeader header, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> _);
        if (parseStatus != PacketParseStatus.Success)
        {
            return Fail(TcpActiveConnectStatus.SegmentParseFailed, parseStatus, out result);
        }

        if (header.SourcePort != RemotePort || header.DestinationPort != LocalPort)
        {
            return Fail(TcpActiveConnectStatus.UnexpectedPorts, parseStatus, out result);
        }

        if (header.Flags != (FlagSyn | FlagAck))
        {
            return Fail(TcpActiveConnectStatus.InvalidFlags, parseStatus, out result);
        }

        if (header.AcknowledgementNumber != LocalNextSequenceNumber)
        {
            return Fail(TcpActiveConnectStatus.WrongAcknowledgementNumber, parseStatus, out result);
        }

        if (_requiresRemoteInitialSequenceNumber && header.SequenceNumber != _expectedRemoteInitialSequenceNumber)
        {
            return Fail(TcpActiveConnectStatus.WrongSequenceNumber, parseStatus, out result);
        }

        RemoteInitialSequenceNumber = header.SequenceNumber;
        RemoteNextSequenceNumber = NextSequence(header.SequenceNumber);
        _synAckAccepted = true;
        result = TcpActiveConnectTransitionResult.Success(State, parseStatus, bytesWritten: 0, LocalNextSequenceNumber, RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpActiveConnectStatus TryWriteFinalAck(Span<byte> segment, ushort windowSize, ushort checksum, out TcpActiveConnectTransitionResult result)
    {
        if (State != TcpActiveConnectState.SynSent || !_synAckAccepted)
        {
            result = TcpActiveConnectTransitionResult.Fail(TcpActiveConnectStatus.InvalidState, State, PacketParseStatus.Success, LocalNextSequenceNumber, RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus writeStatus = TcpHeader.TryWrite(segment, LocalPort, RemotePort, LocalNextSequenceNumber, RemoteNextSequenceNumber, FlagAck, windowSize, checksum, urgentPointer: 0, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, out int bytesWritten);
        if (writeStatus != PacketParseStatus.Success)
        {
            TcpActiveConnectStatus status = MapWriteStatus(writeStatus);
            result = TcpActiveConnectTransitionResult.Fail(status, State, writeStatus, LocalNextSequenceNumber, RemoteNextSequenceNumber);
            return status;
        }

        State = TcpActiveConnectState.Established;
        result = TcpActiveConnectTransitionResult.Success(State, writeStatus, bytesWritten, LocalNextSequenceNumber, RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpActiveConnectStatus Timeout(out TcpActiveConnectTransitionResult result)
    {
        if (State != TcpActiveConnectState.SynSent)
        {
            result = TcpActiveConnectTransitionResult.Fail(TcpActiveConnectStatus.InvalidState, State, PacketParseStatus.Success, LocalNextSequenceNumber, RemoteNextSequenceNumber);
            return result.Status;
        }

        State = TcpActiveConnectState.Failed;
        _synAckAccepted = false;
        result = TcpActiveConnectTransitionResult.Fail(TcpActiveConnectStatus.Timeout, State, PacketParseStatus.Success, LocalNextSequenceNumber, RemoteNextSequenceNumber);
        return result.Status;
    }

    private TcpActiveConnectStatus Fail(TcpActiveConnectStatus status, PacketParseStatus packetStatus, out TcpActiveConnectTransitionResult result)
    {
        State = TcpActiveConnectState.Failed;
        _synAckAccepted = false;
        result = TcpActiveConnectTransitionResult.Fail(status, State, packetStatus, LocalNextSequenceNumber, RemoteNextSequenceNumber);
        return status;
    }

    private static TcpActiveConnectStatus MapWriteStatus(PacketParseStatus status)
    {
        return status == PacketParseStatus.BufferTooSmall ? TcpActiveConnectStatus.BufferTooSmall : TcpActiveConnectStatus.SegmentWriteFailed;
    }

    private static uint NextSequence(uint sequenceNumber)
    {
        return unchecked(sequenceNumber + 1);
    }
}
