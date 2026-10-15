namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public enum TcpPassiveIpv4FramePlanStatus
{
    Success = 0,
    InvalidLocalIdentity = 1,
    InvalidRemoteIdentity = 2,
    InvalidState = 3,
    FrameBufferTooSmall = 4,
    TcpSegmentWriteFailed = 5,
    FrameWriteFailed = 6,
    FrameParseFailed = 7,
    UnsupportedEtherType = 8,
    UnsupportedIpProtocol = 9,
    WrongLocalMac = 10,
    WrongRemoteMac = 11,
    WrongLocalAddress = 12,
    WrongRemoteAddress = 13,
    TcpSegmentParseFailed = 14,
    WrongTcpPorts = 15,
    InvalidTcpFlags = 16,
    WrongAcknowledgementNumber = 17,
    WrongSequenceNumber = 18,
    BadTcpChecksum = 19
}

public readonly struct TcpPassiveIpv4FramePlannerInfo
{
    public TcpPassiveIpv4FramePlannerInfo(
        MacAddress localMac,
        MacAddress remoteMac,
        Ipv4Address localAddress,
        Ipv4Address remoteAddress,
        ushort localPort,
        ushort remotePort,
        uint initialSequenceNumber,
        ushort windowSize = 0x4000,
        byte timeToLive = 64,
        ushort identification = 0,
        ushort tcpChecksum = 0)
    {
        LocalMac = localMac;
        RemoteMac = remoteMac;
        LocalAddress = localAddress;
        RemoteAddress = remoteAddress;
        LocalPort = localPort;
        RemotePort = remotePort;
        InitialSequenceNumber = initialSequenceNumber;
        WindowSize = windowSize;
        TimeToLive = timeToLive;
        Identification = identification;
        TcpChecksum = tcpChecksum;
    }

    public MacAddress LocalMac { get; }

    public MacAddress RemoteMac { get; }

    public Ipv4Address LocalAddress { get; }

    public Ipv4Address RemoteAddress { get; }

    public ushort LocalPort { get; }

    public ushort RemotePort { get; }

    public uint InitialSequenceNumber { get; }

    public ushort WindowSize { get; }

    public byte TimeToLive { get; }

    public ushort Identification { get; }

    public ushort TcpChecksum { get; }
}

public readonly struct TcpPassiveIpv4FramePlanResult
{
    private TcpPassiveIpv4FramePlanResult(
        TcpPassiveIpv4FramePlanStatus status,
        TcpPassiveOpenHandshakeState state,
        TcpPassiveOpenHandshakeStatus tcpStatus,
        PacketParseStatus packetStatus,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int bytesWritten,
        int tcpSegmentOffset,
        int tcpSegmentLength,
        MacAddress localMac,
        MacAddress remoteMac,
        Ipv4Address localAddress,
        Ipv4Address remoteAddress,
        ushort localPort,
        ushort remotePort,
        byte timeToLive,
        ushort identification,
        uint localNextSequenceNumber,
        uint remoteInitialSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        Status = status;
        State = state;
        TcpStatus = tcpStatus;
        PacketStatus = packetStatus;
        EthernetHeader = ethernetHeader;
        Ipv4Header = ipv4Header;
        TcpHeader = tcpHeader;
        BytesWritten = bytesWritten;
        TcpSegmentOffset = tcpSegmentOffset;
        TcpSegmentLength = tcpSegmentLength;
        LocalMac = localMac;
        RemoteMac = remoteMac;
        LocalAddress = localAddress;
        RemoteAddress = remoteAddress;
        LocalPort = localPort;
        RemotePort = remotePort;
        TimeToLive = timeToLive;
        Identification = identification;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteInitialSequenceNumber = remoteInitialSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
        IsInitialized = true;
    }

    public TcpPassiveIpv4FramePlanStatus Status { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == TcpPassiveIpv4FramePlanStatus.Success;

    public TcpPassiveOpenHandshakeState State { get; }

    public TcpPassiveOpenHandshakeStatus TcpStatus { get; }

    public PacketParseStatus PacketStatus { get; }

    public EthernetHeader EthernetHeader { get; }

    public Ipv4Header Ipv4Header { get; }

    public TcpHeader TcpHeader { get; }

    public int BytesWritten { get; }

    public int TcpSegmentOffset { get; }

    public int TcpSegmentLength { get; }

    public MacAddress LocalMac { get; }

    public MacAddress RemoteMac { get; }

    public Ipv4Address LocalAddress { get; }

    public Ipv4Address RemoteAddress { get; }

    public ushort LocalPort { get; }

    public ushort RemotePort { get; }

    public byte TimeToLive { get; }

    public ushort Identification { get; }

    public uint LocalNextSequenceNumber { get; }

    public uint RemoteInitialSequenceNumber { get; }

    public uint RemoteNextSequenceNumber { get; }

    public static TcpPassiveIpv4FramePlanResult Success(
        TcpPassiveOpenHandshakeState state,
        TcpPassiveOpenHandshakeStatus tcpStatus,
        PacketParseStatus packetStatus,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int bytesWritten,
        int tcpSegmentOffset,
        int tcpSegmentLength,
        in TcpPassiveIpv4FramePlannerInfo info,
        uint localNextSequenceNumber,
        uint remoteInitialSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpPassiveIpv4FramePlanResult(
            TcpPassiveIpv4FramePlanStatus.Success,
            state,
            tcpStatus,
            packetStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            bytesWritten,
            tcpSegmentOffset,
            tcpSegmentLength,
            info.LocalMac,
            info.RemoteMac,
            info.LocalAddress,
            info.RemoteAddress,
            info.LocalPort,
            info.RemotePort,
            EffectiveTimeToLive(info),
            info.Identification,
            localNextSequenceNumber,
            remoteInitialSequenceNumber,
            remoteNextSequenceNumber);
    }

    public static TcpPassiveIpv4FramePlanResult Fail(
        TcpPassiveIpv4FramePlanStatus status,
        TcpPassiveOpenHandshakeState state,
        TcpPassiveOpenHandshakeStatus tcpStatus,
        PacketParseStatus packetStatus,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int tcpSegmentOffset,
        int tcpSegmentLength,
        in TcpPassiveIpv4FramePlannerInfo info,
        uint localNextSequenceNumber,
        uint remoteInitialSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new TcpPassiveIpv4FramePlanResult(
            status,
            state,
            tcpStatus,
            packetStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            bytesWritten: 0,
            tcpSegmentOffset,
            tcpSegmentLength,
            info.LocalMac,
            info.RemoteMac,
            info.LocalAddress,
            info.RemoteAddress,
            info.LocalPort,
            info.RemotePort,
            EffectiveTimeToLive(info),
            info.Identification,
            localNextSequenceNumber,
            remoteInitialSequenceNumber,
            remoteNextSequenceNumber);
    }

    private static byte EffectiveTimeToLive(in TcpPassiveIpv4FramePlannerInfo info)
    {
        return info.TimeToLive == 0 ? (byte)64 : info.TimeToLive;
    }
}

public struct TcpPassiveIpv4FramePlanner
{
    public const byte ProtocolTcp = 6;

    public const int HeaderBytes =
        EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + TcpHeader.MinHeaderLength;

    private readonly TcpPassiveIpv4FramePlannerInfo _info;
    private TcpPassiveOpenHandshake _handshake;

    private TcpPassiveIpv4FramePlanner(TcpPassiveIpv4FramePlannerInfo info)
    {
        _info = info;
        _handshake = TcpPassiveOpenHandshake.Create(
            info.LocalPort,
            info.RemotePort,
            info.InitialSequenceNumber);
    }

    public TcpPassiveOpenHandshakeState State => _handshake.State;

    public uint LocalNextSequenceNumber => _handshake.LocalNextSequenceNumber;

    public uint RemoteInitialSequenceNumber => _handshake.RemoteInitialSequenceNumber;

    public uint RemoteNextSequenceNumber => _handshake.RemoteNextSequenceNumber;

    public static TcpPassiveIpv4FramePlanner Create(TcpPassiveIpv4FramePlannerInfo info)
    {
        return new TcpPassiveIpv4FramePlanner(info);
    }

    public TcpPassiveIpv4FramePlanStatus ReceiveSynFrameAndWriteSynAckFrame(
        ReadOnlySpan<byte> inboundFrame,
        Span<byte> outboundFrame,
        out TcpPassiveIpv4FramePlanResult result)
    {
        TcpPassiveIpv4FramePlanStatus identityStatus = ValidateIdentity();
        if (identityStatus != TcpPassiveIpv4FramePlanStatus.Success)
        {
            result = Fail(identityStatus);
            return result.Status;
        }

        if (outboundFrame.Length < HeaderBytes)
        {
            result = Fail(
                TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall,
                TcpPassiveOpenHandshakeStatus.BufferTooSmall,
                PacketParseStatus.BufferTooSmall);
            return result.Status;
        }

        PacketParseStatus parseStatus = TryParseFrame(
            inboundFrame,
            out EthernetHeader inboundEthernetHeader,
            out Ipv4Header inboundIpv4Header,
            out ReadOnlySpan<byte> tcpSegment,
            out int tcpSegmentOffset,
            out int tcpSegmentLength,
            out TcpPassiveIpv4FramePlanStatus frameStatus);
        if (parseStatus != PacketParseStatus.Success
            || frameStatus != TcpPassiveIpv4FramePlanStatus.Success)
        {
            result = TcpPassiveIpv4FramePlanResult.Fail(
                frameStatus,
                _handshake.State,
                TcpPassiveOpenHandshakeStatus.Success,
                parseStatus,
                inboundEthernetHeader,
                inboundIpv4Header,
                default,
                tcpSegmentOffset,
                tcpSegmentLength,
                _info,
                _handshake.LocalNextSequenceNumber,
                _handshake.RemoteInitialSequenceNumber,
                _handshake.RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus tcpParseStatus = TcpHeader.TryParse(
            tcpSegment,
            out TcpHeader inboundTcpHeader,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> _);

        if (tcpParseStatus == PacketParseStatus.Success
            && !ValidateTcpChecksum(
                inboundEthernetHeader,
                inboundIpv4Header,
                tcpSegment,
                inboundTcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength,
                out result))
        {
            return result.Status;
        }

        TcpPassiveOpenHandshake pending = _handshake;
        Span<byte> outboundSegment = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpPassiveOpenHandshakeStatus tcpStatus = pending.ReceiveSynAndWriteSynAck(
            tcpSegment,
            outboundSegment,
            _info.WindowSize,
            checksum: 0,
            out TcpPassiveOpenHandshakeTransitionResult transitionResult);

        if (tcpStatus != TcpPassiveOpenHandshakeStatus.Success)
        {
            _handshake = pending;
            result = TcpPassiveIpv4FramePlanResult.Fail(
                MapTcpFailure(tcpStatus),
                pending.State,
                tcpStatus,
                transitionResult.PacketStatus,
                inboundEthernetHeader,
                inboundIpv4Header,
                inboundTcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength,
                _info,
                pending.LocalNextSequenceNumber,
                pending.RemoteInitialSequenceNumber,
                pending.RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus checksumStatus = TryWriteTcpChecksum(
            outboundSegment.Slice(0, transitionResult.BytesWritten),
            _info.LocalAddress,
            _info.RemoteAddress);
        if (checksumStatus != PacketParseStatus.Success)
        {
            result = Fail(
                MapFrameWriteFailure(checksumStatus),
                tcpStatus,
                checksumStatus);
            return result.Status;
        }

        PacketParseStatus packetStatus = TryWriteFrame(
            outboundFrame,
            outboundSegment.Slice(0, transitionResult.BytesWritten),
            out EthernetHeader outboundEthernetHeader,
            out Ipv4Header outboundIpv4Header,
            out int bytesWritten);
        if (packetStatus != PacketParseStatus.Success)
        {
            result = Fail(
                MapFrameWriteFailure(packetStatus),
                tcpStatus,
                packetStatus);
            return result.Status;
        }

        PacketParseStatus outboundTcpParseStatus = TcpHeader.TryParse(
            outboundSegment.Slice(0, transitionResult.BytesWritten),
            out TcpHeader outboundTcpHeader,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> _);

        _handshake = pending;
        result = TcpPassiveIpv4FramePlanResult.Success(
            pending.State,
            tcpStatus,
            outboundTcpParseStatus == PacketParseStatus.Success ? tcpParseStatus : outboundTcpParseStatus,
            outboundEthernetHeader,
            outboundIpv4Header,
            outboundTcpHeader,
            bytesWritten,
            EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength,
            transitionResult.BytesWritten,
            _info,
            pending.LocalNextSequenceNumber,
            pending.RemoteInitialSequenceNumber,
            pending.RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpPassiveIpv4FramePlanStatus ReceiveFinalAckFrame(
        ReadOnlySpan<byte> inboundFrame,
        out TcpPassiveIpv4FramePlanResult result)
    {
        TcpPassiveIpv4FramePlanStatus identityStatus = ValidateIdentity();
        if (identityStatus != TcpPassiveIpv4FramePlanStatus.Success)
        {
            result = Fail(identityStatus);
            return result.Status;
        }

        PacketParseStatus parseStatus = TryParseFrame(
            inboundFrame,
            out EthernetHeader ethernetHeader,
            out Ipv4Header ipv4Header,
            out ReadOnlySpan<byte> tcpSegment,
            out int tcpSegmentOffset,
            out int tcpSegmentLength,
            out TcpPassiveIpv4FramePlanStatus frameStatus);
        if (parseStatus != PacketParseStatus.Success
            || frameStatus != TcpPassiveIpv4FramePlanStatus.Success)
        {
            result = TcpPassiveIpv4FramePlanResult.Fail(
                frameStatus,
                _handshake.State,
                TcpPassiveOpenHandshakeStatus.Success,
                parseStatus,
                ethernetHeader,
                ipv4Header,
                default,
                tcpSegmentOffset,
                tcpSegmentLength,
                _info,
                _handshake.LocalNextSequenceNumber,
                _handshake.RemoteInitialSequenceNumber,
                _handshake.RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus tcpParseStatus = TcpHeader.TryParse(
            tcpSegment,
            out TcpHeader tcpHeader,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> _);

        if (tcpParseStatus == PacketParseStatus.Success
            && !ValidateTcpChecksum(
                ethernetHeader,
                ipv4Header,
                tcpSegment,
                tcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength,
                out result))
        {
            return result.Status;
        }

        TcpPassiveOpenHandshake pending = _handshake;
        TcpPassiveOpenHandshakeStatus tcpStatus = pending.ReceiveFinalAck(
            tcpSegment,
            out TcpPassiveOpenHandshakeTransitionResult transitionResult);
        _handshake = pending;

        if (tcpStatus != TcpPassiveOpenHandshakeStatus.Success)
        {
            result = TcpPassiveIpv4FramePlanResult.Fail(
                MapTcpFailure(tcpStatus),
                pending.State,
                tcpStatus,
                transitionResult.PacketStatus,
                ethernetHeader,
                ipv4Header,
                tcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength,
                _info,
                pending.LocalNextSequenceNumber,
                pending.RemoteInitialSequenceNumber,
                pending.RemoteNextSequenceNumber);
            return result.Status;
        }

        result = TcpPassiveIpv4FramePlanResult.Success(
            pending.State,
            tcpStatus,
            tcpParseStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            bytesWritten: 0,
            tcpSegmentOffset,
            tcpSegmentLength,
            _info,
            pending.LocalNextSequenceNumber,
            pending.RemoteInitialSequenceNumber,
            pending.RemoteNextSequenceNumber);
        return result.Status;
    }

    /// <summary>Serialize the original pending SYN-ACK without changing the
    /// handshake or its sequence numbers. Output is caller-owned; short output
    /// writes nothing. Timers, TX admission and retry budgets belong to the
    /// connection owner. Established/failed/listen states are unsupported.</summary>
    public TcpPassiveIpv4FramePlanStatus WritePendingSynAckFrame(
        Span<byte> outboundFrame, out TcpPassiveIpv4FramePlanResult result)
    {
        var identity = ValidateIdentity();
        if (identity != TcpPassiveIpv4FramePlanStatus.Success) { result = Fail(identity); return result.Status; }
        if (_handshake.State != TcpPassiveOpenHandshakeState.SynReceived)
        { result = Fail(TcpPassiveIpv4FramePlanStatus.InvalidState, TcpPassiveOpenHandshakeStatus.InvalidState, PacketParseStatus.Success); return result.Status; }
        if (outboundFrame.Length < HeaderBytes)
        { result = Fail(TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall, TcpPassiveOpenHandshakeStatus.BufferTooSmall, PacketParseStatus.BufferTooSmall); return result.Status; }

        Span<byte> segment = stackalloc byte[TcpHeader.MinHeaderLength];
        var status = TcpHeader.TryWrite(segment, _info.LocalPort, _info.RemotePort,
            _info.InitialSequenceNumber, _handshake.RemoteNextSequenceNumber,
            TcpPassiveOpenHandshake.FlagSyn | TcpPassiveOpenHandshake.FlagAck,
            _info.WindowSize, checksum: 0, urgentPointer: 0,
            ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, out int count);
        if (status == PacketParseStatus.Success)
            status = TryWriteTcpChecksum(segment.Slice(0, count), _info.LocalAddress, _info.RemoteAddress);
        if (status != PacketParseStatus.Success)
        { result = Fail(TcpPassiveIpv4FramePlanStatus.TcpSegmentWriteFailed, TcpPassiveOpenHandshakeStatus.SegmentWriteFailed, status); return result.Status; }
        status = TryWriteFrame(outboundFrame, segment.Slice(0, count),
            out EthernetHeader ethernet, out Ipv4Header ipv4, out int bytes);
        if (status != PacketParseStatus.Success)
        { result = Fail(MapFrameWriteFailure(status), TcpPassiveOpenHandshakeStatus.Success, status); return result.Status; }
        status = TcpHeader.TryParse(segment.Slice(0, count), out TcpHeader tcp, out _, out _);
        if (status != PacketParseStatus.Success)
        { result = Fail(TcpPassiveIpv4FramePlanStatus.TcpSegmentWriteFailed, TcpPassiveOpenHandshakeStatus.SegmentWriteFailed, status); return result.Status; }
        result = TcpPassiveIpv4FramePlanResult.Success(_handshake.State, TcpPassiveOpenHandshakeStatus.Success,
            status, ethernet, ipv4, tcp, bytes, EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength,
            count, _info, _handshake.LocalNextSequenceNumber,
            _handshake.RemoteInitialSequenceNumber, _handshake.RemoteNextSequenceNumber);
        return result.Status;
    }

    private PacketParseStatus TryWriteFrame(
        Span<byte> frame,
        ReadOnlySpan<byte> tcpSegment,
        out EthernetHeader ethernetHeader,
        out Ipv4Header ipv4Header,
        out int bytesWritten)
    {
        bytesWritten = 0;
        ethernetHeader = new EthernetHeader(
            _info.RemoteMac,
            _info.LocalMac,
            EthernetHeader.EtherTypeIpv4);
        ipv4Header = default;

        if (tcpSegment.Length > ushort.MaxValue - Ipv4Header.MinHeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        int ipv4Length = Ipv4Header.MinHeaderLength + tcpSegment.Length;
        int frameLength = EthernetHeader.HeaderLength + ipv4Length;
        if (frame.Length < frameLength)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(frame, ethernetHeader);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            return ethernetStatus;
        }

        ipv4Header = Ipv4Header.Create(
            _info.LocalAddress,
            _info.RemoteAddress,
            ProtocolTcp,
            (ushort)tcpSegment.Length,
            _info.TimeToLive == 0 ? (byte)64 : _info.TimeToLive,
            _info.Identification,
            flagsAndFragmentOffset: 0);
        PacketParseStatus ipv4Status = Ipv4Header.TryWrite(
            frame.Slice(EthernetHeader.HeaderLength, Ipv4Header.MinHeaderLength),
            ipv4Header,
            out int ipv4BytesWritten);
        if (ipv4Status != PacketParseStatus.Success)
        {
            return ipv4Status;
        }

        BufferOperationResult copy = BufferOperations.MemoryCopy(
            tcpSegment,
            frame.Slice(EthernetHeader.HeaderLength + ipv4BytesWritten, tcpSegment.Length));
        if (!copy.Succeeded)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        bytesWritten = EthernetHeader.HeaderLength + ipv4BytesWritten + tcpSegment.Length;
        return PacketParseStatus.Success;
    }

    private static PacketParseStatus TryWriteTcpChecksum(
        Span<byte> tcpSegment,
        Ipv4Address sourceAddress,
        Ipv4Address destinationAddress)
    {
        if (tcpSegment.Length < TcpHeader.MinHeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        EndianPrimitives.WriteUInt16BigEndian(
            tcpSegment.Slice(16, sizeof(ushort)),
            0);
        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(
            sourceAddress,
            destinationAddress,
            ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success)
        {
            return PacketParseStatus.InvalidLength;
        }

        ushort checksum = checksumResult.Checksum == 0
            ? ushort.MaxValue
            : checksumResult.Checksum;
        EndianPrimitives.WriteUInt16BigEndian(
            tcpSegment.Slice(16, sizeof(ushort)),
            checksum);
        return PacketParseStatus.Success;
    }

    private bool ValidateTcpChecksum(
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        ReadOnlySpan<byte> tcpSegment,
        TcpHeader tcpHeader,
        int tcpSegmentOffset,
        int tcpSegmentLength,
        out TcpPassiveIpv4FramePlanResult result)
    {
        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4Header.Source,
            ipv4Header.Destination,
            ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus == Ipv4TransportChecksumStatus.Success && checksumResult.IsValid)
        {
            result = default;
            return true;
        }

        result = TcpPassiveIpv4FramePlanResult.Fail(
            TcpPassiveIpv4FramePlanStatus.BadTcpChecksum,
            _handshake.State,
            TcpPassiveOpenHandshakeStatus.Success,
            PacketParseStatus.BadChecksum,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            tcpSegmentOffset,
            tcpSegmentLength,
            _info,
            _handshake.LocalNextSequenceNumber,
            _handshake.RemoteInitialSequenceNumber,
            _handshake.RemoteNextSequenceNumber);
        return false;
    }

    private PacketParseStatus TryParseFrame(
        ReadOnlySpan<byte> frame,
        out EthernetHeader ethernetHeader,
        out Ipv4Header ipv4Header,
        out ReadOnlySpan<byte> tcpSegment,
        out int tcpSegmentOffset,
        out int tcpSegmentLength,
        out TcpPassiveIpv4FramePlanStatus frameStatus)
    {
        tcpSegment = default;
        tcpSegmentOffset = 0;
        tcpSegmentLength = 0;
        ipv4Header = default;

        PacketParseStatus ethernetStatus = EthernetHeader.TryParse(
            frame,
            out ethernetHeader,
            out ReadOnlySpan<byte> ethernetPayload);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            frameStatus = TcpPassiveIpv4FramePlanStatus.FrameParseFailed;
            return ethernetStatus;
        }

        if (ethernetHeader.EtherType != EthernetHeader.EtherTypeIpv4)
        {
            frameStatus = TcpPassiveIpv4FramePlanStatus.UnsupportedEtherType;
            return PacketParseStatus.UnsupportedEtherType;
        }

        if (ethernetHeader.Destination != _info.LocalMac)
        {
            frameStatus = TcpPassiveIpv4FramePlanStatus.WrongLocalMac;
            return PacketParseStatus.Success;
        }

        if (ethernetHeader.Source != _info.RemoteMac)
        {
            frameStatus = TcpPassiveIpv4FramePlanStatus.WrongRemoteMac;
            return PacketParseStatus.Success;
        }

        PacketParseStatus ipv4Status = Ipv4Header.TryParse(
            ethernetPayload,
            out ipv4Header,
            out ReadOnlySpan<byte> ipv4Payload);
        if (ipv4Status != PacketParseStatus.Success)
        {
            frameStatus = TcpPassiveIpv4FramePlanStatus.FrameParseFailed;
            return ipv4Status;
        }

        if (ipv4Header.Protocol != ProtocolTcp)
        {
            frameStatus = TcpPassiveIpv4FramePlanStatus.UnsupportedIpProtocol;
            return PacketParseStatus.UnsupportedIpProtocol;
        }

        if (ipv4Header.Destination != _info.LocalAddress)
        {
            frameStatus = TcpPassiveIpv4FramePlanStatus.WrongLocalAddress;
            return PacketParseStatus.Success;
        }

        if (ipv4Header.Source != _info.RemoteAddress)
        {
            frameStatus = TcpPassiveIpv4FramePlanStatus.WrongRemoteAddress;
            return PacketParseStatus.Success;
        }

        tcpSegment = ipv4Payload;
        tcpSegmentOffset = EthernetHeader.HeaderLength + ipv4Header.HeaderLength;
        tcpSegmentLength = ipv4Payload.Length;
        frameStatus = TcpPassiveIpv4FramePlanStatus.Success;
        return PacketParseStatus.Success;
    }

    private TcpPassiveIpv4FramePlanStatus ValidateIdentity()
    {
        if (_info.LocalMac.IsZero
            || _info.LocalMac.IsBroadcast
            || _info.LocalAddress == default
            || _info.LocalPort == 0)
        {
            return TcpPassiveIpv4FramePlanStatus.InvalidLocalIdentity;
        }

        if (_info.RemoteMac.IsZero
            || _info.RemoteMac.IsBroadcast
            || _info.RemoteAddress == default
            || _info.RemotePort == 0)
        {
            return TcpPassiveIpv4FramePlanStatus.InvalidRemoteIdentity;
        }

        return TcpPassiveIpv4FramePlanStatus.Success;
    }

    private TcpPassiveIpv4FramePlanResult Fail(TcpPassiveIpv4FramePlanStatus status)
    {
        return Fail(status, TcpPassiveOpenHandshakeStatus.Success, PacketParseStatus.Success);
    }

    private TcpPassiveIpv4FramePlanResult Fail(
        TcpPassiveIpv4FramePlanStatus status,
        TcpPassiveOpenHandshakeStatus tcpStatus,
        PacketParseStatus packetStatus)
    {
        return TcpPassiveIpv4FramePlanResult.Fail(
            status,
            _handshake.State,
            tcpStatus,
            packetStatus,
            default,
            default,
            default,
            tcpSegmentOffset: 0,
            tcpSegmentLength: 0,
            _info,
            _handshake.LocalNextSequenceNumber,
            _handshake.RemoteInitialSequenceNumber,
            _handshake.RemoteNextSequenceNumber);
    }

    private static TcpPassiveIpv4FramePlanStatus MapFrameWriteFailure(PacketParseStatus status)
    {
        return status switch
        {
            PacketParseStatus.BufferTooSmall => TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall,
            _ => TcpPassiveIpv4FramePlanStatus.FrameWriteFailed
        };
    }

    private static TcpPassiveIpv4FramePlanStatus MapTcpFailure(TcpPassiveOpenHandshakeStatus status)
    {
        return status switch
        {
            TcpPassiveOpenHandshakeStatus.InvalidState => TcpPassiveIpv4FramePlanStatus.InvalidState,
            TcpPassiveOpenHandshakeStatus.BufferTooSmall => TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall,
            TcpPassiveOpenHandshakeStatus.SegmentWriteFailed =>
                TcpPassiveIpv4FramePlanStatus.TcpSegmentWriteFailed,
            TcpPassiveOpenHandshakeStatus.SegmentParseFailed =>
                TcpPassiveIpv4FramePlanStatus.TcpSegmentParseFailed,
            TcpPassiveOpenHandshakeStatus.UnexpectedPorts => TcpPassiveIpv4FramePlanStatus.WrongTcpPorts,
            TcpPassiveOpenHandshakeStatus.InvalidFlags => TcpPassiveIpv4FramePlanStatus.InvalidTcpFlags,
            TcpPassiveOpenHandshakeStatus.WrongAcknowledgementNumber =>
                TcpPassiveIpv4FramePlanStatus.WrongAcknowledgementNumber,
            TcpPassiveOpenHandshakeStatus.WrongSequenceNumber =>
                TcpPassiveIpv4FramePlanStatus.WrongSequenceNumber,
            _ => TcpPassiveIpv4FramePlanStatus.TcpSegmentParseFailed
        };
    }

}
