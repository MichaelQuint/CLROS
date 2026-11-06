namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public struct TcpPassiveIpv4FramePlanner
{
    public const byte ProtocolTcp = 6;
    public const int HeaderBytes = EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + TcpHeader.MinHeaderLength;
    private readonly TcpPassiveIpv4FramePlannerInfo _info;
    private TcpPassiveOpenHandshake _handshake;
    private TcpPassiveIpv4FramePlanner(TcpPassiveIpv4FramePlannerInfo info)
    {
        _info = info;
        _handshake = TcpPassiveOpenHandshake.Create(info.LocalPort, info.RemotePort, info.InitialSequenceNumber);
    }

    public TcpPassiveOpenHandshakeState State => _handshake.State;
    public uint LocalNextSequenceNumber => _handshake.LocalNextSequenceNumber;
    public uint RemoteInitialSequenceNumber => _handshake.RemoteInitialSequenceNumber;
    public uint RemoteNextSequenceNumber => _handshake.RemoteNextSequenceNumber;
    public int RequiredReplyBytes => HeaderBytes + (_info.MaximumSegmentSize == 0 ? 0 : 4);

    public static TcpPassiveIpv4FramePlanner Create(TcpPassiveIpv4FramePlannerInfo info)
    {
        return new TcpPassiveIpv4FramePlanner(info);
    }

    public TcpPassiveIpv4FramePlanStatus ReceiveSynFrameAndWriteSynAckFrame(ReadOnlySpan<byte> inboundFrame, Span<byte> outboundFrame, out TcpPassiveIpv4FramePlanResult result)
    {
        TcpPassiveIpv4FramePlanStatus identityStatus = ValidateIdentity();
        if (identityStatus != TcpPassiveIpv4FramePlanStatus.Success)
        {
            result = Fail(identityStatus);
            return result.Status;
        }

        if (outboundFrame.Length < RequiredReplyBytes)
        {
            result = Fail(TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall, TcpPassiveOpenHandshakeStatus.BufferTooSmall, PacketParseStatus.BufferTooSmall);
            return result.Status;
        }

        PacketParseStatus parseStatus = TryParseFrame(inboundFrame, out EthernetHeader inboundEthernetHeader, out Ipv4Header inboundIpv4Header, out ReadOnlySpan<byte> tcpSegment, out int tcpSegmentOffset, out int tcpSegmentLength, out TcpPassiveIpv4FramePlanStatus frameStatus);
        if (parseStatus != PacketParseStatus.Success || frameStatus != TcpPassiveIpv4FramePlanStatus.Success)
        {
            result = TcpPassiveIpv4FramePlanResult.Fail(frameStatus, _handshake.State, TcpPassiveOpenHandshakeStatus.Success, parseStatus, inboundEthernetHeader, inboundIpv4Header, default, tcpSegmentOffset, tcpSegmentLength, _info, _handshake.LocalNextSequenceNumber, _handshake.RemoteInitialSequenceNumber, _handshake.RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus tcpParseStatus = TcpHeader.TryParse(tcpSegment, out TcpHeader inboundTcpHeader, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> _);
        if (tcpParseStatus == PacketParseStatus.Success && !ValidateTcpChecksum(inboundEthernetHeader, inboundIpv4Header, tcpSegment, inboundTcpHeader, tcpSegmentOffset, tcpSegmentLength, out result))
        {
            return result.Status;
        }

        TcpPassiveOpenHandshake pending = _handshake;
        Span<byte> outboundSegment = stackalloc byte[TcpHeader.MinHeaderLength + 4];
        Span<byte> synOptions = stackalloc byte[4];
        int optionCount = WriteMssOption(synOptions);
        TcpPassiveOpenHandshakeStatus tcpStatus = pending.ReceiveSynAndWriteSynAck(tcpSegment, outboundSegment, _info.WindowSize, checksum: 0, synOptions.Slice(0, optionCount), out TcpPassiveOpenHandshakeTransitionResult transitionResult);
        if (tcpStatus != TcpPassiveOpenHandshakeStatus.Success)
        {
            _handshake = pending;
            result = TcpPassiveIpv4FramePlanResult.Fail(MapTcpFailure(tcpStatus), pending.State, tcpStatus, transitionResult.PacketStatus, inboundEthernetHeader, inboundIpv4Header, inboundTcpHeader, tcpSegmentOffset, tcpSegmentLength, _info, pending.LocalNextSequenceNumber, pending.RemoteInitialSequenceNumber, pending.RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus checksumStatus = TryWriteTcpChecksum(outboundSegment.Slice(0, transitionResult.BytesWritten), _info.LocalAddress, _info.RemoteAddress);
        if (checksumStatus != PacketParseStatus.Success)
        {
            result = Fail(MapFrameWriteFailure(checksumStatus), tcpStatus, checksumStatus);
            return result.Status;
        }

        PacketParseStatus packetStatus = TryWriteFrame(outboundFrame, outboundSegment.Slice(0, transitionResult.BytesWritten), out EthernetHeader outboundEthernetHeader, out Ipv4Header outboundIpv4Header, out int bytesWritten);
        if (packetStatus != PacketParseStatus.Success)
        {
            result = Fail(MapFrameWriteFailure(packetStatus), tcpStatus, packetStatus);
            return result.Status;
        }

        PacketParseStatus outboundTcpParseStatus = TcpHeader.TryParse(outboundSegment.Slice(0, transitionResult.BytesWritten), out TcpHeader outboundTcpHeader, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> _);
        _handshake = pending;
        result = TcpPassiveIpv4FramePlanResult.Success(pending.State, tcpStatus, outboundTcpParseStatus == PacketParseStatus.Success ? tcpParseStatus : outboundTcpParseStatus, outboundEthernetHeader, outboundIpv4Header, outboundTcpHeader, bytesWritten, EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength, transitionResult.BytesWritten, _info, pending.LocalNextSequenceNumber, pending.RemoteInitialSequenceNumber, pending.RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpPassiveIpv4FramePlanStatus ReceiveFinalAckFrame(ReadOnlySpan<byte> inboundFrame, out TcpPassiveIpv4FramePlanResult result)
    {
        TcpPassiveIpv4FramePlanStatus identityStatus = ValidateIdentity();
        if (identityStatus != TcpPassiveIpv4FramePlanStatus.Success)
        {
            result = Fail(identityStatus);
            return result.Status;
        }

        PacketParseStatus parseStatus = TryParseFrame(inboundFrame, out EthernetHeader ethernetHeader, out Ipv4Header ipv4Header, out ReadOnlySpan<byte> tcpSegment, out int tcpSegmentOffset, out int tcpSegmentLength, out TcpPassiveIpv4FramePlanStatus frameStatus);
        if (parseStatus != PacketParseStatus.Success || frameStatus != TcpPassiveIpv4FramePlanStatus.Success)
        {
            result = TcpPassiveIpv4FramePlanResult.Fail(frameStatus, _handshake.State, TcpPassiveOpenHandshakeStatus.Success, parseStatus, ethernetHeader, ipv4Header, default, tcpSegmentOffset, tcpSegmentLength, _info, _handshake.LocalNextSequenceNumber, _handshake.RemoteInitialSequenceNumber, _handshake.RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus tcpParseStatus = TcpHeader.TryParse(tcpSegment, out TcpHeader tcpHeader, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> _);
        if (tcpParseStatus == PacketParseStatus.Success && !ValidateTcpChecksum(ethernetHeader, ipv4Header, tcpSegment, tcpHeader, tcpSegmentOffset, tcpSegmentLength, out result))
        {
            return result.Status;
        }

        TcpPassiveOpenHandshake pending = _handshake;
        TcpPassiveOpenHandshakeStatus tcpStatus = pending.ReceiveFinalAck(tcpSegment, out TcpPassiveOpenHandshakeTransitionResult transitionResult);
        _handshake = pending;
        if (tcpStatus != TcpPassiveOpenHandshakeStatus.Success)
        {
            result = TcpPassiveIpv4FramePlanResult.Fail(MapTcpFailure(tcpStatus), pending.State, tcpStatus, transitionResult.PacketStatus, ethernetHeader, ipv4Header, tcpHeader, tcpSegmentOffset, tcpSegmentLength, _info, pending.LocalNextSequenceNumber, pending.RemoteInitialSequenceNumber, pending.RemoteNextSequenceNumber);
            return result.Status;
        }

        result = TcpPassiveIpv4FramePlanResult.Success(pending.State, tcpStatus, tcpParseStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten: 0, tcpSegmentOffset, tcpSegmentLength, _info, pending.LocalNextSequenceNumber, pending.RemoteInitialSequenceNumber, pending.RemoteNextSequenceNumber);
        return result.Status;
    }

    /// <summary>Serialize the original pending SYN-ACK without changing the
    /// handshake or its sequence numbers. Output is caller-owned; short output
    /// writes nothing. Timers, TX admission and retry budgets belong to the
    /// connection owner. Established/failed/listen states are unsupported.</summary>

    public TcpPassiveIpv4FramePlanStatus WritePendingSynAckFrame(Span<byte> outboundFrame, out TcpPassiveIpv4FramePlanResult result)
    {
        var identity = ValidateIdentity();
        if (identity != TcpPassiveIpv4FramePlanStatus.Success)
        {
            result = Fail(identity);
            return result.Status;
        }

        if (_handshake.State != TcpPassiveOpenHandshakeState.SynReceived)
        {
            result = Fail(TcpPassiveIpv4FramePlanStatus.InvalidState, TcpPassiveOpenHandshakeStatus.InvalidState, PacketParseStatus.Success);
            return result.Status;
        }

        if (outboundFrame.Length < RequiredReplyBytes)
        {
            result = Fail(TcpPassiveIpv4FramePlanStatus.FrameBufferTooSmall, TcpPassiveOpenHandshakeStatus.BufferTooSmall, PacketParseStatus.BufferTooSmall);
            return result.Status;
        }

        Span<byte> segment = stackalloc byte[TcpHeader.MinHeaderLength + 4];
        Span<byte> synOptions = stackalloc byte[4];
        int optionCount = WriteMssOption(synOptions);
        var status = TcpHeader.TryWrite(segment, _info.LocalPort, _info.RemotePort, _info.InitialSequenceNumber, _handshake.RemoteNextSequenceNumber, TcpPassiveOpenHandshake.FlagSyn | TcpPassiveOpenHandshake.FlagAck, _info.WindowSize, checksum: 0, urgentPointer: 0, synOptions.Slice(0, optionCount), ReadOnlySpan<byte>.Empty, out int count);
        if (status == PacketParseStatus.Success)
            status = TryWriteTcpChecksum(segment.Slice(0, count), _info.LocalAddress, _info.RemoteAddress);
        if (status != PacketParseStatus.Success)
        {
            result = Fail(TcpPassiveIpv4FramePlanStatus.TcpSegmentWriteFailed, TcpPassiveOpenHandshakeStatus.SegmentWriteFailed, status);
            return result.Status;
        }

        status = TryWriteFrame(outboundFrame, segment.Slice(0, count), out EthernetHeader ethernet, out Ipv4Header ipv4, out int bytes);
        if (status != PacketParseStatus.Success)
        {
            result = Fail(MapFrameWriteFailure(status), TcpPassiveOpenHandshakeStatus.Success, status);
            return result.Status;
        }

        status = TcpHeader.TryParse(segment.Slice(0, count), out TcpHeader tcp, out _, out _);
        if (status != PacketParseStatus.Success)
        {
            result = Fail(TcpPassiveIpv4FramePlanStatus.TcpSegmentWriteFailed, TcpPassiveOpenHandshakeStatus.SegmentWriteFailed, status);
            return result.Status;
        }

        result = TcpPassiveIpv4FramePlanResult.Success(_handshake.State, TcpPassiveOpenHandshakeStatus.Success, status, ethernet, ipv4, tcp, bytes, EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength, count, _info, _handshake.LocalNextSequenceNumber, _handshake.RemoteInitialSequenceNumber, _handshake.RemoteNextSequenceNumber);
        return result.Status;
    }

    private int WriteMssOption(Span<byte> options)
    {
        if (_info.MaximumSegmentSize == 0)
            return 0;
        options[0] = 2;
        options[1] = 4;
        options[2] = (byte)(_info.MaximumSegmentSize >> 8);
        options[3] = (byte)_info.MaximumSegmentSize;
        return 4;
    }

    private PacketParseStatus TryWriteFrame(Span<byte> frame, ReadOnlySpan<byte> tcpSegment, out EthernetHeader ethernetHeader, out Ipv4Header ipv4Header, out int bytesWritten)
    {
        bytesWritten = 0;
        ethernetHeader = new EthernetHeader(_info.RemoteMac, _info.LocalMac, EthernetHeader.EtherTypeIpv4);
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

        ipv4Header = Ipv4Header.Create(_info.LocalAddress, _info.RemoteAddress, ProtocolTcp, (ushort)tcpSegment.Length, _info.TimeToLive == 0 ? (byte)64 : _info.TimeToLive, _info.Identification, flagsAndFragmentOffset: 0);
        PacketParseStatus ipv4Status = Ipv4Header.TryWrite(frame.Slice(EthernetHeader.HeaderLength, Ipv4Header.MinHeaderLength), ipv4Header, out int ipv4BytesWritten);
        if (ipv4Status != PacketParseStatus.Success)
        {
            return ipv4Status;
        }

        BufferOperationResult copy = BufferOperations.MemoryCopy(tcpSegment, frame.Slice(EthernetHeader.HeaderLength + ipv4BytesWritten, tcpSegment.Length));
        if (!copy.Succeeded)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        bytesWritten = EthernetHeader.HeaderLength + ipv4BytesWritten + tcpSegment.Length;
        return PacketParseStatus.Success;
    }

    private static PacketParseStatus TryWriteTcpChecksum(Span<byte> tcpSegment, Ipv4Address sourceAddress, Ipv4Address destinationAddress)
    {
        if (tcpSegment.Length < TcpHeader.MinHeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        EndianPrimitives.WriteUInt16BigEndian(tcpSegment.Slice(16, sizeof(ushort)), 0);
        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(sourceAddress, destinationAddress, ProtocolTcp, tcpSegment, out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success)
        {
            return PacketParseStatus.InvalidLength;
        }

        ushort checksum = checksumResult.Checksum == 0 ? ushort.MaxValue : checksumResult.Checksum;
        EndianPrimitives.WriteUInt16BigEndian(tcpSegment.Slice(16, sizeof(ushort)), checksum);
        return PacketParseStatus.Success;
    }

    private bool ValidateTcpChecksum(EthernetHeader ethernetHeader, Ipv4Header ipv4Header, ReadOnlySpan<byte> tcpSegment, TcpHeader tcpHeader, int tcpSegmentOffset, int tcpSegmentLength, out TcpPassiveIpv4FramePlanResult result)
    {
        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(ipv4Header.Source, ipv4Header.Destination, ProtocolTcp, tcpSegment, out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus == Ipv4TransportChecksumStatus.Success && checksumResult.IsValid)
        {
            result = default;
            return true;
        }

        result = TcpPassiveIpv4FramePlanResult.Fail(TcpPassiveIpv4FramePlanStatus.BadTcpChecksum, _handshake.State, TcpPassiveOpenHandshakeStatus.Success, PacketParseStatus.BadChecksum, ethernetHeader, ipv4Header, tcpHeader, tcpSegmentOffset, tcpSegmentLength, _info, _handshake.LocalNextSequenceNumber, _handshake.RemoteInitialSequenceNumber, _handshake.RemoteNextSequenceNumber);
        return false;
    }

    private PacketParseStatus TryParseFrame(ReadOnlySpan<byte> frame, out EthernetHeader ethernetHeader, out Ipv4Header ipv4Header, out ReadOnlySpan<byte> tcpSegment, out int tcpSegmentOffset, out int tcpSegmentLength, out TcpPassiveIpv4FramePlanStatus frameStatus)
    {
        tcpSegment = default;
        tcpSegmentOffset = 0;
        tcpSegmentLength = 0;
        ipv4Header = default;
        PacketParseStatus ethernetStatus = EthernetHeader.TryParse(frame, out ethernetHeader, out ReadOnlySpan<byte> ethernetPayload);
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

        PacketParseStatus ipv4Status = Ipv4Header.TryParse(ethernetPayload, out ipv4Header, out ReadOnlySpan<byte> ipv4Payload);
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
        if (_info.LocalMac.IsZero || _info.LocalMac.IsBroadcast || _info.LocalAddress == default || _info.LocalPort == 0 || _info.MaximumSegmentSize > 1460)
        {
            return TcpPassiveIpv4FramePlanStatus.InvalidLocalIdentity;
        }

        if (_info.RemoteMac.IsZero || _info.RemoteMac.IsBroadcast || _info.RemoteAddress == default || _info.RemotePort == 0)
        {
            return TcpPassiveIpv4FramePlanStatus.InvalidRemoteIdentity;
        }

        return TcpPassiveIpv4FramePlanStatus.Success;
    }

    private TcpPassiveIpv4FramePlanResult Fail(TcpPassiveIpv4FramePlanStatus status)
    {
        return Fail(status, TcpPassiveOpenHandshakeStatus.Success, PacketParseStatus.Success);
    }

    private TcpPassiveIpv4FramePlanResult Fail(TcpPassiveIpv4FramePlanStatus status, TcpPassiveOpenHandshakeStatus tcpStatus, PacketParseStatus packetStatus)
    {
        return TcpPassiveIpv4FramePlanResult.Fail(status, _handshake.State, tcpStatus, packetStatus, default, default, default, tcpSegmentOffset: 0, tcpSegmentLength: 0, _info, _handshake.LocalNextSequenceNumber, _handshake.RemoteInitialSequenceNumber, _handshake.RemoteNextSequenceNumber);
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
            TcpPassiveOpenHandshakeStatus.SegmentWriteFailed => TcpPassiveIpv4FramePlanStatus.TcpSegmentWriteFailed,
            TcpPassiveOpenHandshakeStatus.SegmentParseFailed => TcpPassiveIpv4FramePlanStatus.TcpSegmentParseFailed,
            TcpPassiveOpenHandshakeStatus.UnexpectedPorts => TcpPassiveIpv4FramePlanStatus.WrongTcpPorts,
            TcpPassiveOpenHandshakeStatus.InvalidFlags => TcpPassiveIpv4FramePlanStatus.InvalidTcpFlags,
            TcpPassiveOpenHandshakeStatus.WrongAcknowledgementNumber => TcpPassiveIpv4FramePlanStatus.WrongAcknowledgementNumber,
            TcpPassiveOpenHandshakeStatus.WrongSequenceNumber => TcpPassiveIpv4FramePlanStatus.WrongSequenceNumber,
            _ => TcpPassiveIpv4FramePlanStatus.TcpSegmentParseFailed
        };
    }
}
