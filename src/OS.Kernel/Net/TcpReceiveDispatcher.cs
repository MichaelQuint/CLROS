namespace OS.Net;
using OS.Drivers;
using OS.Std.Binary;

public static class TcpReceiveDispatcher
{
    public const byte ProtocolTcp = Ipv4TransportChecksum.ProtocolTcp;
    public const bool ConsumesParsedNetworkMetadata = true;
    public const bool UsesCallerOwnedBuffers = true;
    public const bool AllocatesBuffers = false;
    public const bool PerformsNicIo = false;
    public const bool DispatchesSockets = false;
    public const bool ImplementsTls = false;
    public const bool ImplementsFullTcp = false;
    public const bool StartsKestrel = false;
    public static TcpReceiveDispatchStatus Dispatch(ReadOnlySpan<byte> receiveBuffer, ref TcpEstablishedStream stream, ref TcpConnectionClose close, Span<byte> passiveCloseAckSegment, out TcpReceiveDispatchResult result)
    {
        NetworkReceiveDispatchResult networkResult = NetworkReceiveDispatcher.Dispatch(receiveBuffer);
        return Dispatch(receiveBuffer, networkResult, ref stream, ref close, passiveCloseAckSegment, out result);
    }

    public static TcpReceiveDispatchStatus Dispatch(ReadOnlySpan<byte> receiveBuffer, NetworkReceiveDispatchResult networkResult, ref TcpEstablishedStream stream, ref TcpConnectionClose close, Span<byte> passiveCloseAckSegment, out TcpReceiveDispatchResult result)
    {
        if (!networkResult.Succeeded)
        {
            result = Fail(stream, close, TcpReceiveDispatchStatus.NetworkDispatchFailed, networkResult, networkResult.PayloadPacketStatus, Ipv4TransportChecksumStatus.Success);
            return result.Status;
        }

        if (networkResult.PayloadKind != EthernetPayloadKind.Ipv4)
        {
            result = Fail(stream, close, TcpReceiveDispatchStatus.NonIpv4Payload, networkResult, PacketParseStatus.UnsupportedEtherType, Ipv4TransportChecksumStatus.Success);
            return result.Status;
        }

        if (networkResult.Ipv4Header.Protocol != ProtocolTcp)
        {
            result = Fail(stream, close, TcpReceiveDispatchStatus.UnsupportedIpProtocol, networkResult, PacketParseStatus.UnsupportedIpProtocol, Ipv4TransportChecksumStatus.Success);
            return result.Status;
        }

        if (!IsConnectionConsistent(stream, close))
        {
            result = Fail(stream, close, TcpReceiveDispatchStatus.ConnectionMismatch, networkResult, PacketParseStatus.Success, Ipv4TransportChecksumStatus.Success);
            return result.Status;
        }

        if (!IsRangeInBuffer(receiveBuffer.Length, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength))
        {
            result = Fail(stream, close, TcpReceiveDispatchStatus.InvalidPayloadRange, networkResult, PacketParseStatus.TruncatedPayload, Ipv4TransportChecksumStatus.Success);
            return result.Status;
        }

        ReadOnlySpan<byte> tcpSegment = receiveBuffer.Slice(networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength);
        PacketParseStatus tcpStatus = TcpHeader.TryParse(tcpSegment, out TcpHeader tcpHeader, out ReadOnlySpan<byte> _, out ReadOnlySpan<byte> payload);
        if (tcpStatus != PacketParseStatus.Success)
        {
            result = Fail(stream, close, TcpReceiveDispatchStatus.TcpParseFailed, networkResult, tcpStatus, Ipv4TransportChecksumStatus.Success, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength);
            return result.Status;
        }

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(networkResult.Ipv4Header.Source, networkResult.Ipv4Header.Destination, ProtocolTcp, tcpSegment, out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success || !checksumResult.IsValid)
        {
            result = Fail(stream, close, TcpReceiveDispatchStatus.BadTcpChecksum, networkResult, PacketParseStatus.BadChecksum, checksumStatus, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength);
            return result.Status;
        }

        if (ShouldValidateControlSegment(tcpHeader.Flags, payload.Length))
        {
            TcpConnectionCloseStatus controlStatus = ValidateControlSegment(stream, tcpHeader, payload.Length);
            if (controlStatus != TcpConnectionCloseStatus.Success)
            {
                result = TcpReceiveDispatchResult.Fail(TcpReceiveDispatchStatus.ControlSegmentRejected, networkResult, PacketParseStatus.Success, TcpEstablishedStreamStatus.Success, controlStatus, close.State, Ipv4TransportChecksumStatus.Success, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
                return result.Status;
            }
        }

        if ((tcpHeader.Flags & TcpConnectionClose.FlagRst) != 0)
        {
            return DispatchReset(tcpSegment, networkResult, tcpHeader, ref stream, ref close, out result);
        }

        if ((tcpHeader.Flags & TcpConnectionClose.FlagFin) != 0)
        {
            return DispatchFinAck(tcpSegment, networkResult, tcpHeader, ref stream, ref close, passiveCloseAckSegment, out result);
        }

        if (payload.Length != 0)
        {
            return DispatchPayload(tcpSegment, networkResult, tcpHeader, ref stream, close, payload.Length, out result);
        }

        if (tcpHeader.Flags == TcpEstablishedStream.FlagAck)
        {
            result = TcpReceiveDispatchResult.Success(TcpReceiveDispatchAction.None, networkResult, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength, payloadBytes: 0, ackBytesWritten: 0, close.State, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        result = Fail(stream, close, TcpReceiveDispatchStatus.UnsupportedTcpFlags, networkResult, PacketParseStatus.Success, Ipv4TransportChecksumStatus.Success, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength);
        return result.Status;
    }

    private static TcpReceiveDispatchStatus DispatchPayload(ReadOnlySpan<byte> tcpSegment, NetworkReceiveDispatchResult networkResult, TcpHeader tcpHeader, ref TcpEstablishedStream stream, in TcpConnectionClose close, int payloadLength, out TcpReceiveDispatchResult result)
    {
        TcpEstablishedStream pending = stream;
        TcpEstablishedStreamStatus streamStatus = pending.ReceiveSegment(tcpSegment, out TcpEstablishedStreamReceiveResult receiveResult);
        if (streamStatus != TcpEstablishedStreamStatus.Success)
        {
            result = TcpReceiveDispatchResult.Fail(TcpReceiveDispatchStatus.TcpStreamFailed, networkResult, receiveResult.PacketStatus, streamStatus, TcpConnectionCloseStatus.Success, close.State, Ipv4TransportChecksumStatus.Success, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        stream = pending;
        result = TcpReceiveDispatchResult.Success(TcpReceiveDispatchAction.PayloadAccepted, networkResult, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength, payloadLength, ackBytesWritten: 0, close.State, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
        return result.Status;
    }

    private static TcpReceiveDispatchStatus DispatchFinAck(ReadOnlySpan<byte> tcpSegment, NetworkReceiveDispatchResult networkResult, TcpHeader tcpHeader, ref TcpEstablishedStream stream, ref TcpConnectionClose close, Span<byte> passiveCloseAckSegment, out TcpReceiveDispatchResult result)
    {
        TcpConnectionClose pending = CreateCloseForEstablishedReceive(stream, close);
        TcpConnectionCloseStatus closeStatus = pending.ReceivePassiveFinAckAndWriteAck(tcpSegment, passiveCloseAckSegment, tcpHeader.WindowSize, checksum: 0, out TcpConnectionCloseTransitionResult closeResult);
        if (closeStatus != TcpConnectionCloseStatus.Success)
        {
            result = TcpReceiveDispatchResult.Fail(TcpReceiveDispatchStatus.CloseFailed, networkResult, closeResult.PacketStatus, TcpEstablishedStreamStatus.Success, closeStatus, close.State, Ipv4TransportChecksumStatus.Success, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        Span<byte> acknowledgementSegment = passiveCloseAckSegment.Slice(0, closeResult.BytesWritten);
        Ipv4TransportChecksumStatus checksumStatus = WriteTcpChecksum(acknowledgementSegment, networkResult.Ipv4Header.Destination, networkResult.Ipv4Header.Source);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success)
        {
            result = TcpReceiveDispatchResult.Fail(TcpReceiveDispatchStatus.AckChecksumFailed, networkResult, PacketParseStatus.InvalidLength, TcpEstablishedStreamStatus.Success, closeStatus, close.State, checksumStatus, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        close = pending;
        result = TcpReceiveDispatchResult.Success(TcpReceiveDispatchAction.FinAcknowledged, networkResult, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength, payloadBytes: 0, closeResult.BytesWritten, close.State, close.LocalNextSequenceNumber, close.RemoteNextSequenceNumber);
        return result.Status;
    }

    private static TcpReceiveDispatchStatus DispatchReset(ReadOnlySpan<byte> tcpSegment, NetworkReceiveDispatchResult networkResult, TcpHeader tcpHeader, ref TcpEstablishedStream stream, ref TcpConnectionClose close, out TcpReceiveDispatchResult result)
    {
        TcpConnectionClose pending = CreateCloseForEstablishedReceive(stream, close);
        TcpConnectionCloseStatus closeStatus = pending.ReceiveReset(tcpSegment, out TcpConnectionCloseTransitionResult closeResult);
        if (closeStatus != TcpConnectionCloseStatus.Success)
        {
            result = TcpReceiveDispatchResult.Fail(TcpReceiveDispatchStatus.CloseFailed, networkResult, closeResult.PacketStatus, TcpEstablishedStreamStatus.Success, closeStatus, close.State, Ipv4TransportChecksumStatus.Success, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
            return result.Status;
        }

        close = pending;
        result = TcpReceiveDispatchResult.Success(TcpReceiveDispatchAction.ResetReceived, networkResult, tcpHeader, networkResult.Ipv4PayloadOffset, networkResult.Ipv4PayloadLength, payloadBytes: 0, ackBytesWritten: 0, close.State, close.LocalNextSequenceNumber, close.RemoteNextSequenceNumber);
        return result.Status;
    }

    private static TcpReceiveDispatchResult Fail(in TcpEstablishedStream stream, in TcpConnectionClose close, TcpReceiveDispatchStatus status, NetworkReceiveDispatchResult networkResult, PacketParseStatus tcpPacketStatus, Ipv4TransportChecksumStatus checksumStatus, TcpHeader tcpHeader = default, int tcpSegmentOffset = 0, int tcpSegmentLength = 0)
    {
        return TcpReceiveDispatchResult.Fail(status, networkResult, tcpPacketStatus, TcpEstablishedStreamStatus.Success, TcpConnectionCloseStatus.Success, close.State, checksumStatus, tcpHeader, tcpSegmentOffset, tcpSegmentLength, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber);
    }

    private static bool IsConnectionConsistent(in TcpEstablishedStream stream, in TcpConnectionClose close)
    {
        return stream.LocalPort == close.LocalPort && stream.RemotePort == close.RemotePort;
    }

    private static bool ShouldValidateControlSegment(byte flags, int payloadLength)
    {
        return (flags & TcpConnectionClose.FlagRst) != 0 || (flags & TcpConnectionClose.FlagFin) != 0 || (payloadLength == 0 && flags == TcpConnectionClose.FlagAck);
    }

    private static TcpConnectionCloseStatus ValidateControlSegment(in TcpEstablishedStream stream, TcpHeader header, int payloadLength)
    {
        if (header.SourcePort != stream.RemotePort || header.DestinationPort != stream.LocalPort)
        {
            return TcpConnectionCloseStatus.UnexpectedPorts;
        }

        if ((header.Flags & TcpConnectionClose.FlagRst) != 0)
        {
            return ValidateResetControlSegment(stream, header, payloadLength);
        }

        if ((header.Flags & TcpConnectionClose.FlagFin) != 0)
        {
            return ValidateExactControlSegment(stream, header, payloadLength, TcpConnectionClose.FlagFin | TcpConnectionClose.FlagAck);
        }

        return ValidateExactControlSegment(stream, header, payloadLength, TcpConnectionClose.FlagAck);
    }

    private static TcpConnectionCloseStatus ValidateResetControlSegment(in TcpEstablishedStream stream, TcpHeader header, int payloadLength)
    {
        if ((header.Flags & TcpConnectionClose.FlagRst) == 0 || (header.Flags & ~(TcpConnectionClose.FlagRst | TcpConnectionClose.FlagAck)) != 0)
        {
            return TcpConnectionCloseStatus.InvalidFlags;
        }

        if (header.SequenceNumber != stream.RemoteNextSequenceNumber)
        {
            return TcpConnectionCloseStatus.WrongSequenceNumber;
        }

        if ((header.Flags & TcpConnectionClose.FlagAck) != 0 && header.AcknowledgementNumber != stream.LocalNextSequenceNumber)
        {
            return TcpConnectionCloseStatus.WrongAcknowledgementNumber;
        }

        return payloadLength == 0 ? TcpConnectionCloseStatus.Success : TcpConnectionCloseStatus.UnexpectedPayload;
    }

    private static TcpConnectionCloseStatus ValidateExactControlSegment(in TcpEstablishedStream stream, TcpHeader header, int payloadLength, byte expectedFlags)
    {
        if (header.Flags != expectedFlags)
        {
            return TcpConnectionCloseStatus.InvalidFlags;
        }

        if (header.SequenceNumber != stream.RemoteNextSequenceNumber)
        {
            return TcpConnectionCloseStatus.WrongSequenceNumber;
        }

        if (header.AcknowledgementNumber != stream.LocalNextSequenceNumber)
        {
            return TcpConnectionCloseStatus.WrongAcknowledgementNumber;
        }

        return payloadLength == 0 ? TcpConnectionCloseStatus.Success : TcpConnectionCloseStatus.UnexpectedPayload;
    }

    private static TcpConnectionClose CreateCloseForEstablishedReceive(in TcpEstablishedStream stream, in TcpConnectionClose close)
    {
        return close.State == TcpConnectionCloseState.Established ? TcpConnectionClose.CreateEstablished(stream.LocalPort, stream.RemotePort, stream.LocalNextSequenceNumber, stream.RemoteNextSequenceNumber) : close;
    }

    private static bool IsRangeInBuffer(int bufferLength, int offset, int length)
    {
        return offset >= 0 && length >= 0 && offset <= bufferLength && length <= bufferLength - offset;
    }

    private static Ipv4TransportChecksumStatus WriteTcpChecksum(Span<byte> tcpSegment, Ipv4Address source, Ipv4Address destination)
    {
        if (tcpSegment.Length < TcpHeader.MinHeaderLength)
        {
            return Ipv4TransportChecksumStatus.InvalidLength;
        }

        EndianPrimitives.WriteUInt16BigEndian(tcpSegment.Slice(16, sizeof(ushort)), 0);
        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(source, destination, ProtocolTcp, tcpSegment, out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success)
        {
            return checksumStatus;
        }

        ushort checksum = checksumResult.Checksum == 0 ? ushort.MaxValue : checksumResult.Checksum;
        EndianPrimitives.WriteUInt16BigEndian(tcpSegment.Slice(16, sizeof(ushort)), checksum);
        return Ipv4TransportChecksumStatus.Success;
    }
}
