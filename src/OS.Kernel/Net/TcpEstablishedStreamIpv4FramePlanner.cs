namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public static class TcpEstablishedStreamIpv4FramePlanner
{
    public const byte ProtocolTcp = 6;
    public const int HeaderBytes = EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + TcpHeader.MinHeaderLength;
    public static TcpEstablishedStreamIpv4FramePlanStatus TryWritePayloadFrame(ref TcpEstablishedStream stream, in TcpEstablishedStreamIpv4FramePlannerInfo info, ReadOnlySpan<byte> payload, Span<byte> frame, out TcpEstablishedStreamIpv4FramePlanResult result)
    {
        TcpEstablishedStreamIpv4FramePlanStatus identityStatus = ValidateIdentity(stream, info);
        if (identityStatus != TcpEstablishedStreamIpv4FramePlanStatus.Success)
        {
            result = Fail(stream, info, identityStatus);
            return result.Status;
        }

        if (info.MaximumPayloadLength <= 0)
        {
            result = Fail(stream, info, TcpEstablishedStreamIpv4FramePlanStatus.InvalidMaximumPayloadLength, TcpEstablishedStreamStatus.InvalidMaximumPayloadLength, PacketParseStatus.Success);
            return result.Status;
        }

        if (frame.Length < HeaderBytes)
        {
            result = Fail(stream, info, TcpEstablishedStreamIpv4FramePlanStatus.FrameBufferTooSmall, TcpEstablishedStreamStatus.DestinationTooSmall, PacketParseStatus.BufferTooSmall);
            return result.Status;
        }

        TcpEstablishedStream pending = stream;
        int tcpSegmentOffset = EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength;
        Span<byte> tcpSegmentDestination = frame.Slice(tcpSegmentOffset);
        TcpEstablishedStreamStatus streamStatus = pending.TryWritePayloadSegment(payload, info.MaximumPayloadLength, tcpSegmentDestination, info.WindowSize, checksum: 0, out TcpEstablishedStreamSendResult sendResult);
        if (streamStatus != TcpEstablishedStreamStatus.Success)
        {
            result = Fail(stream, info, TcpEstablishedStreamIpv4FramePlanStatus.TcpStreamFailed, streamStatus, sendResult.PacketStatus);
            return result.Status;
        }

        Span<byte> tcpSegment = tcpSegmentDestination.Slice(0, sendResult.SegmentBytesWritten);
        PacketParseStatus checksumStatus = TryWriteTcpChecksum(tcpSegment, info.LocalAddress, info.RemoteAddress);
        if (checksumStatus != PacketParseStatus.Success)
        {
            result = Fail(stream, info, TcpEstablishedStreamIpv4FramePlanStatus.TcpChecksumFailed, streamStatus, checksumStatus, tcpSegmentOffset, sendResult.SegmentBytesWritten);
            return result.Status;
        }

        PacketParseStatus frameStatus = TryWriteFrame(info, frame, tcpSegment, out EthernetHeader ethernetHeader, out Ipv4Header ipv4Header, out int bytesWritten);
        if (frameStatus != PacketParseStatus.Success)
        {
            result = Fail(stream, info, MapFrameWriteFailure(frameStatus), streamStatus, frameStatus, tcpSegmentOffset, sendResult.SegmentBytesWritten);
            return result.Status;
        }

        PacketParseStatus tcpParseStatus = TcpHeader.TryParse(tcpSegment, out TcpHeader tcpHeader, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> _);
        stream = pending;
        result = TcpEstablishedStreamIpv4FramePlanResult.Success(streamStatus, tcpParseStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten, tcpSegmentOffset, sendResult.SegmentBytesWritten, sendResult.PayloadBytesWritten, info, stream.LocalPort, stream.RemotePort, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
        return result.Status;
    }

    public static TcpEstablishedStreamIpv4FramePlanStatus ReceivePayloadFrame(ref TcpEstablishedStream stream, in TcpEstablishedStreamIpv4FramePlannerInfo info, ReadOnlySpan<byte> frame, out TcpEstablishedStreamIpv4FramePlanResult result) => ReceiveFrameCore(ref stream, info, frame, false, false, out result);
    /// <summary>Validate original Ethernet/IP/TCP identity and checksum, then
    /// apply window trimming rather than the legacy strict in-order helper.
    /// Buffer/ACK/TX lifetime remains with the exclusive live receive owner.</summary>

    public static TcpEstablishedStreamIpv4FramePlanStatus ReceiveWindowFrame(ref TcpEstablishedStream stream, in TcpEstablishedStreamIpv4FramePlannerInfo info, ReadOnlySpan<byte> frame, out TcpEstablishedStreamIpv4FramePlanResult result) => ReceiveFrameCore(ref stream, info, frame, true, false, out result);
    internal static TcpEstablishedStreamIpv4FramePlanStatus ReceiveFinWindowFrame(ref TcpEstablishedStream stream, in TcpEstablishedStreamIpv4FramePlannerInfo info, ReadOnlySpan<byte> frame, out TcpEstablishedStreamIpv4FramePlanResult result) => ReceiveFrameCore(ref stream, info, frame, true, false, out result, true);
    /// <summary>Read-only original frame/endpoint/checksum admission. Success
    /// describes validated packet metadata, never delivered bytes or ACK progress.
    /// Protocol flags, sequence acceptability and ACK/window decisions belong to
    /// the owner. This does not mutate stream metadata or its borrowed buffer.</summary>

    public static TcpEstablishedStreamIpv4FramePlanStatus ValidateFrame(in TcpEstablishedStream stream, in TcpEstablishedStreamIpv4FramePlannerInfo info, ReadOnlySpan<byte> frame, out TcpEstablishedStreamIpv4FramePlanResult result)
    {
        var view = stream;
        return ReceiveFrameCore(ref view, info, frame, false, true, out result);
    }

    private static TcpEstablishedStreamIpv4FramePlanStatus ReceiveFrameCore(ref TcpEstablishedStream stream, in TcpEstablishedStreamIpv4FramePlannerInfo info, ReadOnlySpan<byte> frame, bool windowed, bool validateOnly, out TcpEstablishedStreamIpv4FramePlanResult result, bool allowFin = false)
    {
        TcpEstablishedStreamIpv4FramePlanStatus identityStatus = ValidateIdentity(stream, info);
        if (identityStatus != TcpEstablishedStreamIpv4FramePlanStatus.Success)
        {
            result = Fail(stream, info, identityStatus);
            return result.Status;
        }

        PacketParseStatus frameParseStatus = TryParseFrame(info, frame, out EthernetHeader ethernetHeader, out Ipv4Header ipv4Header, out ReadOnlySpan<byte> tcpSegment, out int tcpSegmentOffset, out int tcpSegmentLength, out TcpEstablishedStreamIpv4FramePlanStatus frameStatus);
        if (frameParseStatus != PacketParseStatus.Success || frameStatus != TcpEstablishedStreamIpv4FramePlanStatus.Success)
        {
            result = TcpEstablishedStreamIpv4FramePlanResult.Fail(frameStatus, TcpEstablishedStreamStatus.Success, frameParseStatus, ethernetHeader, ipv4Header, default, tcpSegmentOffset, tcpSegmentLength, info, stream.LocalPort, stream.RemotePort, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        PacketParseStatus tcpParseStatus = TcpHeader.TryParse(tcpSegment, out TcpHeader tcpHeader, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> _);
        if (tcpParseStatus != PacketParseStatus.Success)
        {
            result = TcpEstablishedStreamIpv4FramePlanResult.Fail(TcpEstablishedStreamIpv4FramePlanStatus.TcpSegmentParseFailed, TcpEstablishedStreamStatus.SegmentParseFailed, tcpParseStatus, ethernetHeader, ipv4Header, tcpHeader, tcpSegmentOffset, tcpSegmentLength, info, stream.LocalPort, stream.RemotePort, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(ipv4Header.Source, ipv4Header.Destination, ProtocolTcp, tcpSegment, out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success || !checksumResult.IsValid)
        {
            result = TcpEstablishedStreamIpv4FramePlanResult.Fail(TcpEstablishedStreamIpv4FramePlanStatus.BadTcpChecksum, TcpEstablishedStreamStatus.Success, PacketParseStatus.BadChecksum, ethernetHeader, ipv4Header, tcpHeader, tcpSegmentOffset, tcpSegmentLength, info, stream.LocalPort, stream.RemotePort, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        if (validateOnly)
        {
            if (tcpHeader.SourcePort != stream.RemotePort || tcpHeader.DestinationPort != stream.LocalPort)
            {
                result = Fail(stream, info, TcpEstablishedStreamIpv4FramePlanStatus.TcpStreamFailed, TcpEstablishedStreamStatus.UnexpectedPorts, PacketParseStatus.Success);
                return result.Status;
            }

            result = TcpEstablishedStreamIpv4FramePlanResult.Success(TcpEstablishedStreamStatus.Success, tcpParseStatus, ethernetHeader, ipv4Header, tcpHeader, 0, tcpSegmentOffset, tcpSegmentLength, tcpSegmentLength - tcpHeader.HeaderLength, info, stream.LocalPort, stream.RemotePort, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        TcpEstablishedStream pending = stream;
        TcpEstablishedStreamReceiveResult receiveResult;
        TcpEstablishedStreamStatus streamStatus = windowed ? allowFin ? pending.ReceiveWindowFinSegment(tcpSegment, out receiveResult) : pending.ReceiveWindowSegment(tcpSegment, out receiveResult) : pending.ReceiveSegment(tcpSegment, out receiveResult);
        if (streamStatus != TcpEstablishedStreamStatus.Success)
        {
            result = TcpEstablishedStreamIpv4FramePlanResult.Fail(TcpEstablishedStreamIpv4FramePlanStatus.TcpStreamFailed, streamStatus, receiveResult.PacketStatus, ethernetHeader, ipv4Header, tcpHeader, tcpSegmentOffset, tcpSegmentLength, info, stream.LocalPort, stream.RemotePort, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        stream = pending;
        result = TcpEstablishedStreamIpv4FramePlanResult.Success(streamStatus, tcpParseStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten: 0, tcpSegmentOffset, tcpSegmentLength, receiveResult.PayloadBytesAccepted, info, stream.LocalPort, stream.RemotePort, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
        return result.Status;
    }

    /// <summary>Write one cumulative pure ACK with the actual free receive
    /// window. This does not advance either sequence or imply TX admission.
    /// Short output writes nothing. Payload/checksum/frame helpers are shared
    /// with the existing planner; caller retains the output until TX returns.</summary>

    public static TcpEstablishedStreamIpv4FramePlanStatus TryWriteAckFrame(ref TcpEstablishedStream stream, in TcpEstablishedStreamIpv4FramePlannerInfo info, ushort window, Span<byte> frame, out TcpEstablishedStreamIpv4FramePlanResult result) => TryWriteControlFrame(ref stream, info, window, 0x10, frame, out result);
    internal static TcpEstablishedStreamIpv4FramePlanStatus TryWriteControlFrame(ref TcpEstablishedStream stream, in TcpEstablishedStreamIpv4FramePlannerInfo info, ushort window, byte flags, Span<byte> frame, out TcpEstablishedStreamIpv4FramePlanResult result)
    {
        if (flags != 0x10 && flags != 0x11 && flags != 0x14)
        {
            result = Fail(stream, info, TcpEstablishedStreamIpv4FramePlanStatus.TcpStreamFailed, TcpEstablishedStreamStatus.InvalidFlags, PacketParseStatus.Success);
            return result.Status;
        }

        var identity = ValidateIdentity(stream, info);
        if (identity != TcpEstablishedStreamIpv4FramePlanStatus.Success)
        {
            result = Fail(stream, info, identity);
            return result.Status;
        }

        if (frame.Length < HeaderBytes)
        {
            result = Fail(stream, info, TcpEstablishedStreamIpv4FramePlanStatus.FrameBufferTooSmall);
            return result.Status;
        }

        Span<byte> segment = stackalloc byte[TcpHeader.MinHeaderLength];
        var status = TcpHeader.TryWrite(segment, stream.LocalPort, stream.RemotePort, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber, flags, window, checksum: 0, urgentPointer: 0, ReadOnlySpan<byte>.Empty, ReadOnlySpan<byte>.Empty, out int count);
        if (status == PacketParseStatus.Success)
            status = TryWriteTcpChecksum(segment.Slice(0, count), info.LocalAddress, info.RemoteAddress);
        if (status != PacketParseStatus.Success)
        {
            result = Fail(stream, info, TcpEstablishedStreamIpv4FramePlanStatus.TcpChecksumFailed);
            return result.Status;
        }

        status = TryWriteFrame(info, frame, segment.Slice(0, count), out var ethernet, out var ipv4, out int bytes);
        if (status != PacketParseStatus.Success)
        {
            result = Fail(stream, info, MapFrameWriteFailure(status));
            return result.Status;
        }

        status = TcpHeader.TryParse(segment.Slice(0, count), out var tcp, out _, out _);
        if (status != PacketParseStatus.Success)
        {
            result = Fail(stream, info, TcpEstablishedStreamIpv4FramePlanStatus.TcpSegmentParseFailed);
            return result.Status;
        }

        result = TcpEstablishedStreamIpv4FramePlanResult.Success(TcpEstablishedStreamStatus.Success, status, ethernet, ipv4, tcp, bytes, EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength, count, 0, info, stream.LocalPort, stream.RemotePort, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
        return result.Status;
    }

    private static PacketParseStatus TryWriteFrame(in TcpEstablishedStreamIpv4FramePlannerInfo info, Span<byte> frame, ReadOnlySpan<byte> tcpSegment, out EthernetHeader ethernetHeader, out Ipv4Header ipv4Header, out int bytesWritten)
    {
        bytesWritten = 0;
        ethernetHeader = new EthernetHeader(info.RemoteMac, info.LocalMac, EthernetHeader.EtherTypeIpv4);
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

        ipv4Header = Ipv4Header.Create(info.LocalAddress, info.RemoteAddress, ProtocolTcp, (ushort)tcpSegment.Length, info.TimeToLive == 0 ? (byte)64 : info.TimeToLive, info.Identification, flagsAndFragmentOffset: 0);
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

    private static PacketParseStatus TryParseFrame(in TcpEstablishedStreamIpv4FramePlannerInfo info, ReadOnlySpan<byte> frame, out EthernetHeader ethernetHeader, out Ipv4Header ipv4Header, out ReadOnlySpan<byte> tcpSegment, out int tcpSegmentOffset, out int tcpSegmentLength, out TcpEstablishedStreamIpv4FramePlanStatus frameStatus)
    {
        tcpSegment = default;
        tcpSegmentOffset = 0;
        tcpSegmentLength = 0;
        ipv4Header = default;
        PacketParseStatus ethernetStatus = EthernetHeader.TryParse(frame, out ethernetHeader, out ReadOnlySpan<byte> ethernetPayload);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.FrameParseFailed;
            return ethernetStatus;
        }

        if (ethernetHeader.EtherType != EthernetHeader.EtherTypeIpv4)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.UnsupportedEtherType;
            return PacketParseStatus.UnsupportedEtherType;
        }

        if (ethernetHeader.Destination != info.LocalMac)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.WrongLocalMac;
            return PacketParseStatus.Success;
        }

        if (ethernetHeader.Source != info.RemoteMac)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.WrongRemoteMac;
            return PacketParseStatus.Success;
        }

        PacketParseStatus ipv4Status = Ipv4Header.TryParse(ethernetPayload, out ipv4Header, out ReadOnlySpan<byte> ipv4Payload);
        if (ipv4Status != PacketParseStatus.Success)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.FrameParseFailed;
            return ipv4Status;
        }

        if (ipv4Header.Protocol != ProtocolTcp)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.UnsupportedIpProtocol;
            return PacketParseStatus.UnsupportedIpProtocol;
        }

        if (ipv4Header.Destination != info.LocalAddress)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.WrongLocalAddress;
            return PacketParseStatus.Success;
        }

        if (ipv4Header.Source != info.RemoteAddress)
        {
            frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.WrongRemoteAddress;
            return PacketParseStatus.Success;
        }

        tcpSegment = ipv4Payload;
        tcpSegmentOffset = EthernetHeader.HeaderLength + ipv4Header.HeaderLength;
        tcpSegmentLength = ipv4Payload.Length;
        frameStatus = TcpEstablishedStreamIpv4FramePlanStatus.Success;
        return PacketParseStatus.Success;
    }

    private static TcpEstablishedStreamIpv4FramePlanStatus ValidateIdentity(in TcpEstablishedStream stream, in TcpEstablishedStreamIpv4FramePlannerInfo info)
    {
        if (info.LocalMac.IsZero || info.LocalMac.IsBroadcast || info.LocalAddress == default || stream.LocalPort == 0)
        {
            return TcpEstablishedStreamIpv4FramePlanStatus.InvalidLocalIdentity;
        }

        if (info.RemoteMac.IsZero || info.RemoteMac.IsBroadcast || info.RemoteAddress == default || stream.RemotePort == 0)
        {
            return TcpEstablishedStreamIpv4FramePlanStatus.InvalidRemoteIdentity;
        }

        return TcpEstablishedStreamIpv4FramePlanStatus.Success;
    }

    private static TcpEstablishedStreamIpv4FramePlanResult Fail(in TcpEstablishedStream stream, in TcpEstablishedStreamIpv4FramePlannerInfo info, TcpEstablishedStreamIpv4FramePlanStatus status)
    {
        return Fail(stream, info, status, TcpEstablishedStreamStatus.Success, PacketParseStatus.Success);
    }

    private static TcpEstablishedStreamIpv4FramePlanResult Fail(in TcpEstablishedStream stream, in TcpEstablishedStreamIpv4FramePlannerInfo info, TcpEstablishedStreamIpv4FramePlanStatus status, TcpEstablishedStreamStatus streamStatus, PacketParseStatus packetStatus, int tcpSegmentOffset = 0, int tcpSegmentLength = 0)
    {
        return TcpEstablishedStreamIpv4FramePlanResult.Fail(status, streamStatus, packetStatus, default, default, default, tcpSegmentOffset, tcpSegmentLength, info, stream.LocalPort, stream.RemotePort, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
    }

    private static TcpEstablishedStreamIpv4FramePlanStatus MapFrameWriteFailure(PacketParseStatus status)
    {
        return status switch
        {
            PacketParseStatus.BufferTooSmall => TcpEstablishedStreamIpv4FramePlanStatus.FrameBufferTooSmall,
            _ => TcpEstablishedStreamIpv4FramePlanStatus.FrameWriteFailed
        };
    }
}
