namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public struct TcpConnectionCloseIpv4FramePlanner
{
    public const byte ProtocolTcp = 6;
    public const int HeaderBytes = EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + TcpHeader.MinHeaderLength;
    private readonly TcpConnectionCloseIpv4FramePlannerInfo _info;
    private TcpConnectionClose _close;
    private TcpConnectionCloseIpv4FramePlanner(TcpConnectionCloseIpv4FramePlannerInfo info)
    {
        _info = info;
        _close = TcpConnectionClose.CreateEstablished(info.LocalPort, info.RemotePort, info.LocalNextSequenceNumber, info.RemoteNextSequenceNumber);
    }

    public TcpConnectionCloseState State => _close.State;
    public uint LocalNextSequenceNumber => _close.LocalNextSequenceNumber;
    public uint RemoteNextSequenceNumber => _close.RemoteNextSequenceNumber;

    public static TcpConnectionCloseIpv4FramePlanner Create(TcpConnectionCloseIpv4FramePlannerInfo info)
    {
        return new TcpConnectionCloseIpv4FramePlanner(info);
    }

    public TcpConnectionCloseIpv4FramePlanStatus TryWriteActiveFinAckFrame(Span<byte> frame, out TcpConnectionCloseIpv4FramePlanResult result)
    {
        TcpConnectionCloseIpv4FramePlanStatus identityStatus = ValidateIdentity();
        if (identityStatus != TcpConnectionCloseIpv4FramePlanStatus.Success)
        {
            result = Fail(identityStatus);
            return result.Status;
        }

        if (frame.Length < HeaderBytes)
        {
            result = Fail(TcpConnectionCloseIpv4FramePlanStatus.FrameBufferTooSmall, TcpConnectionCloseStatus.BufferTooSmall, PacketParseStatus.BufferTooSmall);
            return result.Status;
        }

        TcpConnectionClose pending = _close;
        Span<byte> segment = stackalloc byte[TcpHeader.MinHeaderLength];
        TcpConnectionCloseStatus tcpStatus = pending.TryWriteActiveFinAck(segment, _info.WindowSize, checksum: 0, out TcpConnectionCloseTransitionResult transitionResult);
        if (tcpStatus != TcpConnectionCloseStatus.Success)
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

        PacketParseStatus tcpParseStatus = TcpHeader.TryParse(segment.Slice(0, transitionResult.BytesWritten), out TcpHeader tcpHeader, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> _);
        _close = pending;
        result = TcpConnectionCloseIpv4FramePlanResult.Success(pending.State, tcpStatus, tcpParseStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten, EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength, transitionResult.BytesWritten, _info, pending.LocalNextSequenceNumber, pending.RemoteNextSequenceNumber);
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

    private TcpConnectionCloseIpv4FramePlanStatus ValidateIdentity()
    {
        if (_info.LocalMac.IsZero || _info.LocalMac.IsBroadcast || _info.LocalAddress == default || _info.LocalPort == 0)
        {
            return TcpConnectionCloseIpv4FramePlanStatus.InvalidLocalIdentity;
        }

        if (_info.RemoteMac.IsZero || _info.RemoteMac.IsBroadcast || _info.RemoteAddress == default || _info.RemotePort == 0)
        {
            return TcpConnectionCloseIpv4FramePlanStatus.InvalidRemoteIdentity;
        }

        return TcpConnectionCloseIpv4FramePlanStatus.Success;
    }

    private TcpConnectionCloseIpv4FramePlanResult Fail(TcpConnectionCloseIpv4FramePlanStatus status)
    {
        return Fail(status, TcpConnectionCloseStatus.Success, PacketParseStatus.Success);
    }

    private TcpConnectionCloseIpv4FramePlanResult Fail(TcpConnectionCloseIpv4FramePlanStatus status, TcpConnectionCloseStatus tcpStatus, PacketParseStatus packetStatus)
    {
        return TcpConnectionCloseIpv4FramePlanResult.Fail(status, _close.State, tcpStatus, packetStatus, _info, _close.LocalNextSequenceNumber, _close.RemoteNextSequenceNumber);
    }

    private static TcpConnectionCloseIpv4FramePlanStatus MapFrameWriteFailure(PacketParseStatus status)
    {
        return status switch
        {
            PacketParseStatus.BufferTooSmall => TcpConnectionCloseIpv4FramePlanStatus.FrameBufferTooSmall,
            _ => TcpConnectionCloseIpv4FramePlanStatus.FrameWriteFailed
        };
    }

    private static TcpConnectionCloseIpv4FramePlanStatus MapTcpWriteFailure(TcpConnectionCloseStatus status)
    {
        return status switch
        {
            TcpConnectionCloseStatus.InvalidState => TcpConnectionCloseIpv4FramePlanStatus.InvalidState,
            TcpConnectionCloseStatus.BufferTooSmall => TcpConnectionCloseIpv4FramePlanStatus.FrameBufferTooSmall,
            _ => TcpConnectionCloseIpv4FramePlanStatus.TcpSegmentWriteFailed
        };
    }
}
