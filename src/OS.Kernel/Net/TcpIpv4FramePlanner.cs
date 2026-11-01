namespace OS.Net;
using OS.Std.Binary;
using OS.Std.Memory;

public struct TcpIpv4FramePlanner
{
    public const byte ProtocolTcp = 6;
    public const int HeaderBytes = EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + TcpHeader.MinHeaderLength;
    private readonly TcpIpv4FramePlannerInfo _info;
    private TcpActiveConnectTransaction _transaction;
    private TcpIpv4FramePlanner(TcpIpv4FramePlannerInfo info)
    {
        _info = info;
        _transaction = info.RequiresRemoteInitialSequenceNumber ? TcpActiveConnectTransaction.Create(info.LocalPort, info.RemotePort, info.InitialSequenceNumber, info.ExpectedRemoteInitialSequenceNumber) : TcpActiveConnectTransaction.Create(info.LocalPort, info.RemotePort, info.InitialSequenceNumber);
    }

    public TcpActiveConnectState State => _transaction.State;
    public uint LocalNextSequenceNumber => _transaction.LocalNextSequenceNumber;
    public uint RemoteInitialSequenceNumber => _transaction.RemoteInitialSequenceNumber;
    public uint RemoteNextSequenceNumber => _transaction.RemoteNextSequenceNumber;

    public static TcpIpv4FramePlanner Create(TcpIpv4FramePlannerInfo info)
    {
        return new TcpIpv4FramePlanner(info);
    }

    public TcpIpv4FramePlanStatus TryWriteSynFrame(Span<byte> frame, out TcpIpv4FramePlanResult result)
    {
        TcpIpv4FramePlanStatus identityStatus = ValidateIdentity();
        if (identityStatus != TcpIpv4FramePlanStatus.Success)
        {
            result = Fail(identityStatus);
            return result.Status;
        }

        if (frame.Length < HeaderBytes)
        {
            result = Fail(TcpIpv4FramePlanStatus.FrameBufferTooSmall, TcpActiveConnectStatus.BufferTooSmall, PacketParseStatus.BufferTooSmall);
            return result.Status;
        }

        TcpActiveConnectTransaction pending = _transaction;
        Span<byte> segment = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpActiveConnectStatus tcpStatus = pending.TryWriteSyn(segment, _info.WindowSize, checksum: 0, out TcpActiveConnectTransitionResult transitionResult);
        if (tcpStatus != TcpActiveConnectStatus.Success)
        {
            result = Fail(MapTcpWriteFailure(tcpStatus), tcpStatus, transitionResult.PacketStatus);
            return result.Status;
        }

        PacketParseStatus checksumStatus = TryWriteTcpChecksum(segment.Slice(0, transitionResult.BytesWritten), _info.LocalAddress, _info.RemoteAddress);
        if (checksumStatus != PacketParseStatus.Success)
        {
            result = Fail(MapFrameWriteFailure(checksumStatus), tcpStatus, checksumStatus);
            return result.Status;
        }

        PacketParseStatus packetStatus = TryWriteFrame(frame, segment.Slice(0, transitionResult.BytesWritten), out EthernetHeader ethernetHeader, out Ipv4Header ipv4Header, out int bytesWritten);
        if (packetStatus != PacketParseStatus.Success)
        {
            result = Fail(MapFrameWriteFailure(packetStatus), tcpStatus, packetStatus);
            return result.Status;
        }

        PacketParseStatus parseStatus = TcpHeader.TryParse(segment.Slice(0, transitionResult.BytesWritten), out TcpHeader tcpHeader, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> _);
        _transaction = pending;
        result = TcpIpv4FramePlanResult.Success(pending.State, tcpStatus, parseStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten, EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength, transitionResult.BytesWritten, _info, pending.LocalNextSequenceNumber, pending.RemoteInitialSequenceNumber, pending.RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpIpv4FramePlanStatus ReceiveSynAckFrame(ReadOnlySpan<byte> frame, out TcpIpv4FramePlanResult result)
    {
        TcpIpv4FramePlanStatus identityStatus = ValidateIdentity();
        if (identityStatus != TcpIpv4FramePlanStatus.Success)
        {
            result = Fail(identityStatus);
            return result.Status;
        }

        PacketParseStatus parseStatus = TryParseFrame(frame, out EthernetHeader ethernetHeader, out Ipv4Header ipv4Header, out ReadOnlySpan<byte> tcpSegment, out int tcpSegmentOffset, out int tcpSegmentLength, out TcpIpv4FramePlanStatus frameStatus);
        if (parseStatus != PacketParseStatus.Success)
        {
            result = TcpIpv4FramePlanResult.Fail(frameStatus, _transaction.State, TcpActiveConnectStatus.Success, parseStatus, ethernetHeader, ipv4Header, default, tcpSegmentOffset, tcpSegmentLength, _info, _transaction.LocalNextSequenceNumber, _transaction.RemoteInitialSequenceNumber, _transaction.RemoteNextSequenceNumber);
            return result.Status;
        }

        if (frameStatus != TcpIpv4FramePlanStatus.Success)
        {
            result = TcpIpv4FramePlanResult.Fail(frameStatus, _transaction.State, TcpActiveConnectStatus.Success, parseStatus, ethernetHeader, ipv4Header, default, tcpSegmentOffset, tcpSegmentLength, _info, _transaction.LocalNextSequenceNumber, _transaction.RemoteInitialSequenceNumber, _transaction.RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus tcpParseStatus = TcpHeader.TryParse(tcpSegment, out TcpHeader tcpHeader, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> _);
        if (tcpParseStatus == PacketParseStatus.Success)
        {
            Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(ipv4Header.Source, ipv4Header.Destination, ProtocolTcp, tcpSegment, out Ipv4TransportChecksumResult checksumResult);
            if (checksumStatus != Ipv4TransportChecksumStatus.Success || !checksumResult.IsValid)
            {
                result = TcpIpv4FramePlanResult.Fail(TcpIpv4FramePlanStatus.BadTcpChecksum, _transaction.State, TcpActiveConnectStatus.Success, PacketParseStatus.BadChecksum, ethernetHeader, ipv4Header, tcpHeader, tcpSegmentOffset, tcpSegmentLength, _info, _transaction.LocalNextSequenceNumber, _transaction.RemoteInitialSequenceNumber, _transaction.RemoteNextSequenceNumber);
                return result.Status;
            }
        }

        TcpActiveConnectTransaction pending = _transaction;
        TcpActiveConnectStatus tcpStatus = pending.ReceiveSynAck(tcpSegment, out TcpActiveConnectTransitionResult transitionResult);
        _transaction = pending;
        if (tcpStatus != TcpActiveConnectStatus.Success)
        {
            result = TcpIpv4FramePlanResult.Fail(MapTcpReceiveFailure(tcpStatus), pending.State, tcpStatus, transitionResult.PacketStatus, ethernetHeader, ipv4Header, tcpHeader, tcpSegmentOffset, tcpSegmentLength, _info, pending.LocalNextSequenceNumber, pending.RemoteInitialSequenceNumber, pending.RemoteNextSequenceNumber);
            return result.Status;
        }

        result = TcpIpv4FramePlanResult.Success(pending.State, tcpStatus, tcpParseStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten: 0, tcpSegmentOffset, tcpSegmentLength, _info, pending.LocalNextSequenceNumber, pending.RemoteInitialSequenceNumber, pending.RemoteNextSequenceNumber);
        return result.Status;
    }

    public TcpIpv4FramePlanStatus TryWriteFinalAckFrame(Span<byte> frame, out TcpIpv4FramePlanResult result)
    {
        TcpIpv4FramePlanStatus identityStatus = ValidateIdentity();
        if (identityStatus != TcpIpv4FramePlanStatus.Success)
        {
            result = Fail(identityStatus);
            return result.Status;
        }

        if (frame.Length < HeaderBytes)
        {
            result = Fail(TcpIpv4FramePlanStatus.FrameBufferTooSmall, TcpActiveConnectStatus.BufferTooSmall, PacketParseStatus.BufferTooSmall);
            return result.Status;
        }

        TcpActiveConnectTransaction pending = _transaction;
        Span<byte> segment = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpActiveConnectStatus tcpStatus = pending.TryWriteFinalAck(segment, _info.WindowSize, checksum: 0, out TcpActiveConnectTransitionResult transitionResult);
        if (tcpStatus != TcpActiveConnectStatus.Success)
        {
            result = Fail(MapTcpWriteFailure(tcpStatus), tcpStatus, transitionResult.PacketStatus);
            return result.Status;
        }

        PacketParseStatus checksumStatus = TryWriteTcpChecksum(segment.Slice(0, transitionResult.BytesWritten), _info.LocalAddress, _info.RemoteAddress);
        if (checksumStatus != PacketParseStatus.Success)
        {
            result = Fail(MapFrameWriteFailure(checksumStatus), tcpStatus, checksumStatus);
            return result.Status;
        }

        PacketParseStatus packetStatus = TryWriteFrame(frame, segment.Slice(0, transitionResult.BytesWritten), out EthernetHeader ethernetHeader, out Ipv4Header ipv4Header, out int bytesWritten);
        if (packetStatus != PacketParseStatus.Success)
        {
            result = Fail(MapFrameWriteFailure(packetStatus), tcpStatus, packetStatus);
            return result.Status;
        }

        PacketParseStatus parseStatus = TcpHeader.TryParse(segment.Slice(0, transitionResult.BytesWritten), out TcpHeader tcpHeader, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> _);
        _transaction = pending;
        result = TcpIpv4FramePlanResult.Success(pending.State, tcpStatus, parseStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten, EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength, transitionResult.BytesWritten, _info, pending.LocalNextSequenceNumber, pending.RemoteInitialSequenceNumber, pending.RemoteNextSequenceNumber);
        return result.Status;
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

    private PacketParseStatus TryParseFrame(ReadOnlySpan<byte> frame, out EthernetHeader ethernetHeader, out Ipv4Header ipv4Header, out ReadOnlySpan<byte> tcpSegment, out int tcpSegmentOffset, out int tcpSegmentLength, out TcpIpv4FramePlanStatus frameStatus)
    {
        tcpSegment = default;
        tcpSegmentOffset = 0;
        tcpSegmentLength = 0;
        ipv4Header = default;
        PacketParseStatus ethernetStatus = EthernetHeader.TryParse(frame, out ethernetHeader, out ReadOnlySpan<byte> ethernetPayload);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            frameStatus = TcpIpv4FramePlanStatus.FrameParseFailed;
            return ethernetStatus;
        }

        if (ethernetHeader.EtherType != EthernetHeader.EtherTypeIpv4)
        {
            frameStatus = TcpIpv4FramePlanStatus.UnsupportedEtherType;
            return PacketParseStatus.UnsupportedEtherType;
        }

        if (ethernetHeader.Destination != _info.LocalMac)
        {
            frameStatus = TcpIpv4FramePlanStatus.WrongLocalMac;
            return PacketParseStatus.Success;
        }

        if (ethernetHeader.Source != _info.RemoteMac)
        {
            frameStatus = TcpIpv4FramePlanStatus.WrongRemoteMac;
            return PacketParseStatus.Success;
        }

        PacketParseStatus ipv4Status = Ipv4Header.TryParse(ethernetPayload, out ipv4Header, out ReadOnlySpan<byte> ipv4Payload);
        if (ipv4Status != PacketParseStatus.Success)
        {
            frameStatus = TcpIpv4FramePlanStatus.FrameParseFailed;
            return ipv4Status;
        }

        if (ipv4Header.Protocol != ProtocolTcp)
        {
            frameStatus = TcpIpv4FramePlanStatus.UnsupportedIpProtocol;
            return PacketParseStatus.UnsupportedIpProtocol;
        }

        if (ipv4Header.Destination != _info.LocalAddress)
        {
            frameStatus = TcpIpv4FramePlanStatus.WrongLocalAddress;
            return PacketParseStatus.Success;
        }

        if (ipv4Header.Source != _info.RemoteAddress)
        {
            frameStatus = TcpIpv4FramePlanStatus.WrongRemoteAddress;
            return PacketParseStatus.Success;
        }

        tcpSegment = ipv4Payload;
        tcpSegmentOffset = EthernetHeader.HeaderLength + ipv4Header.HeaderLength;
        tcpSegmentLength = ipv4Payload.Length;
        frameStatus = TcpIpv4FramePlanStatus.Success;
        return PacketParseStatus.Success;
    }

    private TcpIpv4FramePlanStatus ValidateIdentity()
    {
        if (_info.LocalMac.IsZero || _info.LocalMac.IsBroadcast || _info.LocalAddress == default || _info.LocalPort == 0)
        {
            return TcpIpv4FramePlanStatus.InvalidLocalIdentity;
        }

        if (_info.RemoteMac.IsZero || _info.RemoteMac.IsBroadcast || _info.RemoteAddress == default || _info.RemotePort == 0)
        {
            return TcpIpv4FramePlanStatus.InvalidRemoteIdentity;
        }

        return TcpIpv4FramePlanStatus.Success;
    }

    private TcpIpv4FramePlanResult Fail(TcpIpv4FramePlanStatus status)
    {
        return Fail(status, TcpActiveConnectStatus.Success, PacketParseStatus.Success);
    }

    private TcpIpv4FramePlanResult Fail(TcpIpv4FramePlanStatus status, TcpActiveConnectStatus tcpStatus, PacketParseStatus packetStatus)
    {
        return TcpIpv4FramePlanResult.Fail(status, _transaction.State, tcpStatus, packetStatus, default, default, default, tcpSegmentOffset: 0, tcpSegmentLength: 0, _info, _transaction.LocalNextSequenceNumber, _transaction.RemoteInitialSequenceNumber, _transaction.RemoteNextSequenceNumber);
    }

    private static TcpIpv4FramePlanStatus MapFrameWriteFailure(PacketParseStatus status)
    {
        return status switch
        {
            PacketParseStatus.BufferTooSmall => TcpIpv4FramePlanStatus.FrameBufferTooSmall,
            _ => TcpIpv4FramePlanStatus.FrameWriteFailed
        };
    }

    private static TcpIpv4FramePlanStatus MapTcpWriteFailure(TcpActiveConnectStatus status)
    {
        return status switch
        {
            TcpActiveConnectStatus.InvalidState => TcpIpv4FramePlanStatus.InvalidState,
            TcpActiveConnectStatus.BufferTooSmall => TcpIpv4FramePlanStatus.FrameBufferTooSmall,
            _ => TcpIpv4FramePlanStatus.TcpSegmentWriteFailed
        };
    }

    private static TcpIpv4FramePlanStatus MapTcpReceiveFailure(TcpActiveConnectStatus status)
    {
        return status switch
        {
            TcpActiveConnectStatus.InvalidState => TcpIpv4FramePlanStatus.InvalidState,
            TcpActiveConnectStatus.SegmentParseFailed => TcpIpv4FramePlanStatus.TcpSegmentParseFailed,
            TcpActiveConnectStatus.UnexpectedPorts => TcpIpv4FramePlanStatus.WrongTcpPorts,
            TcpActiveConnectStatus.InvalidFlags => TcpIpv4FramePlanStatus.InvalidTcpFlags,
            TcpActiveConnectStatus.WrongAcknowledgementNumber => TcpIpv4FramePlanStatus.WrongAcknowledgementNumber,
            TcpActiveConnectStatus.WrongSequenceNumber => TcpIpv4FramePlanStatus.WrongSequenceNumber,
            _ => TcpIpv4FramePlanStatus.TcpSegmentParseFailed
        };
    }
}
