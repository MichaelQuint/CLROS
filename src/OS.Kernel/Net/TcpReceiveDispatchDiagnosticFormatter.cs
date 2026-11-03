namespace OS.Net;

using OS.Std.Text;

public static class TcpReceiveDispatchDiagnosticFormatter
{
    public static TcpReceiveDispatchDiagnosticFormatResult FormatResult(in TcpReceiveDispatchResult result, Span<byte> destination)
    {
        TcpReceiveDispatchDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != TcpReceiveDispatchDiagnosticFormatStatus.Success)
        {
            return TcpReceiveDispatchDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return TcpReceiveDispatchDiagnosticFormatResult.Fail(TcpReceiveDispatchDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        int offset = 0;
        AppendBytes("TCP_RX_DISPATCH status="u8, destination, ref offset);
        AppendBytes(GetDispatchStatusName(result.Status), destination, ref offset);
        AppendBytes(" action="u8, destination, ref offset);
        AppendBytes(GetActionName(result.Action), destination, ref offset);
        AppendBytes(" network="u8, destination, ref offset);
        AppendBytes(GetNetworkStatusName(result.NetworkStatus), destination, ref offset);
        AppendBytes(" packet="u8, destination, ref offset);
        AppendBytes(GetPacketStatusName(result.TcpPacketStatus), destination, ref offset);
        AppendBytes(" stream="u8, destination, ref offset);
        AppendBytes(GetStreamStatusName(result.StreamStatus), destination, ref offset);
        AppendBytes(" close="u8, destination, ref offset);
        AppendBytes(GetCloseStatusName(result.CloseStatus), destination, ref offset);
        AppendBytes(" closeState="u8, destination, ref offset);
        AppendBytes(GetCloseStateName(result.CloseState), destination, ref offset);
        AppendBytes(" checksum="u8, destination, ref offset);
        AppendBytes(GetChecksumStatusName(result.ChecksumStatus), destination, ref offset);
        AppendBytes(" payloadBytes="u8, destination, ref offset);
        AppendInt64Decimal(result.PayloadBytes, destination, ref offset);
        AppendBytes(" ackBytes="u8, destination, ref offset);
        AppendInt64Decimal(result.AckBytesWritten, destination, ref offset);
        AppendBytes(" tcpSegmentOffset="u8, destination, ref offset);
        AppendInt64Decimal(result.TcpSegmentOffset, destination, ref offset);
        AppendBytes(" tcpSegmentBytes="u8, destination, ref offset);
        AppendInt64Decimal(result.TcpSegmentLength, destination, ref offset);
        AppendBytes(" localSeq="u8, destination, ref offset);
        AppendUInt64Decimal(result.LocalNextSequenceNumber, destination, ref offset);
        AppendBytes(" remoteSeq="u8, destination, ref offset);
        AppendUInt64Decimal(result.RemoteNextSequenceNumber, destination, ref offset);
        return new TcpReceiveDispatchDiagnosticFormatResult(TcpReceiveDispatchDiagnosticFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static TcpReceiveDispatchDiagnosticFormatResult QueryRequiredBytes(in TcpReceiveDispatchResult result)
    {
        TcpReceiveDispatchDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != TcpReceiveDispatchDiagnosticFormatStatus.Success)
        {
            return TcpReceiveDispatchDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new TcpReceiveDispatchDiagnosticFormatResult(TcpReceiveDispatchDiagnosticFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static TcpReceiveDispatchDiagnosticFormatStatus Validate(in TcpReceiveDispatchResult result)
    {
        if (!result.IsInitialized || !IsKnownDispatchStatus(result.Status) || !IsKnownAction(result.Action) || !IsKnownNetworkStatus(result.NetworkStatus) || !IsKnownPacketStatus(result.TcpPacketStatus) || !IsKnownStreamStatus(result.StreamStatus) || !IsKnownCloseStatus(result.CloseStatus) || !IsKnownCloseState(result.CloseState) || !IsKnownChecksumStatus(result.ChecksumStatus) || result.PayloadBytes < 0 || result.AckBytesWritten < 0 || result.TcpSegmentOffset < 0 || result.TcpSegmentLength < 0)
        {
            return TcpReceiveDispatchDiagnosticFormatStatus.InvalidResult;
        }

        return TcpReceiveDispatchDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in TcpReceiveDispatchResult result)
    {
        return "TCP_RX_DISPATCH status="u8.Length + GetDispatchStatusName(result.Status).Length + " action="u8.Length + GetActionName(result.Action).Length + " network="u8.Length + GetNetworkStatusName(result.NetworkStatus).Length + " packet="u8.Length + GetPacketStatusName(result.TcpPacketStatus).Length + " stream="u8.Length + GetStreamStatusName(result.StreamStatus).Length + " close="u8.Length + GetCloseStatusName(result.CloseStatus).Length + " closeState="u8.Length + GetCloseStateName(result.CloseState).Length + " checksum="u8.Length + GetChecksumStatusName(result.ChecksumStatus).Length + " payloadBytes="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.PayloadBytes) + " ackBytes="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.AckBytesWritten) + " tcpSegmentOffset="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.TcpSegmentOffset) + " tcpSegmentBytes="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.TcpSegmentLength) + " localSeq="u8.Length + AsciiDecimalFormatter.CountUInt32Digits(result.LocalNextSequenceNumber) + " remoteSeq="u8.Length + AsciiDecimalFormatter.CountUInt32Digits(result.RemoteNextSequenceNumber);
    }

    private static bool IsKnownDispatchStatus(TcpReceiveDispatchStatus status)
    {
        return status >= TcpReceiveDispatchStatus.Success && status <= TcpReceiveDispatchStatus.ControlSegmentRejected;
    }

    private static bool IsKnownAction(TcpReceiveDispatchAction action)
    {
        return action >= TcpReceiveDispatchAction.None && action <= TcpReceiveDispatchAction.ResetReceived;
    }

    private static bool IsKnownNetworkStatus(NetworkReceiveDispatchStatus status)
    {
        return status >= NetworkReceiveDispatchStatus.Success && status <= NetworkReceiveDispatchStatus.UnsupportedPayload;
    }

    private static bool IsKnownPacketStatus(PacketParseStatus status)
    {
        return status >= PacketParseStatus.Success && status <= PacketParseStatus.UnsupportedIcmpCode;
    }

    private static bool IsKnownStreamStatus(TcpEstablishedStreamStatus status)
    {
        return status >= TcpEstablishedStreamStatus.Success && status <= TcpEstablishedStreamStatus.InvalidAcknowledgementNumber;
    }

    private static bool IsKnownCloseStatus(TcpConnectionCloseStatus status)
    {
        return status >= TcpConnectionCloseStatus.Success && status <= TcpConnectionCloseStatus.UnexpectedPayload;
    }

    private static bool IsKnownCloseState(TcpConnectionCloseState state)
    {
        return state >= TcpConnectionCloseState.Established && state <= TcpConnectionCloseState.Reset;
    }

    private static bool IsKnownChecksumStatus(Ipv4TransportChecksumStatus status)
    {
        return status >= Ipv4TransportChecksumStatus.Success && status <= Ipv4TransportChecksumStatus.InvalidLength;
    }

    private static ReadOnlySpan<byte> GetDispatchStatusName(TcpReceiveDispatchStatus status)
    {
        return status switch
        {
            TcpReceiveDispatchStatus.Success => "Success"u8,
            TcpReceiveDispatchStatus.NetworkDispatchFailed => "NetworkDispatchFailed"u8,
            TcpReceiveDispatchStatus.NonIpv4Payload => "NonIpv4Payload"u8,
            TcpReceiveDispatchStatus.UnsupportedIpProtocol => "UnsupportedIpProtocol"u8,
            TcpReceiveDispatchStatus.InvalidPayloadRange => "InvalidPayloadRange"u8,
            TcpReceiveDispatchStatus.TcpParseFailed => "TcpParseFailed"u8,
            TcpReceiveDispatchStatus.BadTcpChecksum => "BadTcpChecksum"u8,
            TcpReceiveDispatchStatus.ConnectionMismatch => "ConnectionMismatch"u8,
            TcpReceiveDispatchStatus.TcpStreamFailed => "TcpStreamFailed"u8,
            TcpReceiveDispatchStatus.CloseFailed => "CloseFailed"u8,
            TcpReceiveDispatchStatus.AckChecksumFailed => "AckChecksumFailed"u8,
            TcpReceiveDispatchStatus.UnsupportedTcpFlags => "UnsupportedTcpFlags"u8,
            TcpReceiveDispatchStatus.ControlSegmentRejected => "ControlSegmentRejected"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetActionName(TcpReceiveDispatchAction action)
    {
        return action switch
        {
            TcpReceiveDispatchAction.None => "None"u8,
            TcpReceiveDispatchAction.PayloadAccepted => "PayloadAccepted"u8,
            TcpReceiveDispatchAction.FinAcknowledged => "FinAcknowledged"u8,
            TcpReceiveDispatchAction.ResetReceived => "ResetReceived"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetNetworkStatusName(NetworkReceiveDispatchStatus status)
    {
        return status switch
        {
            NetworkReceiveDispatchStatus.Success => "Success"u8,
            NetworkReceiveDispatchStatus.ClassifierFailed => "ClassifierFailed"u8,
            NetworkReceiveDispatchStatus.ArpParseFailed => "ArpParseFailed"u8,
            NetworkReceiveDispatchStatus.Ipv4ParseFailed => "Ipv4ParseFailed"u8,
            NetworkReceiveDispatchStatus.UnsupportedPayload => "UnsupportedPayload"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetPacketStatusName(PacketParseStatus status)
    {
        return status switch
        {
            PacketParseStatus.Success => "Success"u8,
            PacketParseStatus.BufferTooSmall => "BufferTooSmall"u8,
            PacketParseStatus.InvalidLength => "InvalidLength"u8,
            PacketParseStatus.UnsupportedEtherType => "UnsupportedEtherType"u8,
            PacketParseStatus.UnsupportedIpVersion => "UnsupportedIpVersion"u8,
            PacketParseStatus.UnsupportedIpHeaderLength => "UnsupportedIpHeaderLength"u8,
            PacketParseStatus.UnsupportedIpProtocol => "UnsupportedIpProtocol"u8,
            PacketParseStatus.BadChecksum => "BadChecksum"u8,
            PacketParseStatus.TruncatedPayload => "TruncatedPayload"u8,
            PacketParseStatus.UnsupportedHardwareType => "UnsupportedHardwareType"u8,
            PacketParseStatus.UnsupportedProtocolType => "UnsupportedProtocolType"u8,
            PacketParseStatus.UnsupportedHardwareAddressLength => "UnsupportedHardwareAddressLength"u8,
            PacketParseStatus.UnsupportedProtocolAddressLength => "UnsupportedProtocolAddressLength"u8,
            PacketParseStatus.UnsupportedOperation => "UnsupportedOperation"u8,
            PacketParseStatus.UnsupportedIcmpType => "UnsupportedIcmpType"u8,
            PacketParseStatus.UnsupportedIcmpCode => "UnsupportedIcmpCode"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetStreamStatusName(TcpEstablishedStreamStatus status)
    {
        return status switch
        {
            TcpEstablishedStreamStatus.Success => "Success"u8,
            TcpEstablishedStreamStatus.InvalidState => "InvalidState"u8,
            TcpEstablishedStreamStatus.SegmentParseFailed => "SegmentParseFailed"u8,
            TcpEstablishedStreamStatus.SegmentWriteFailed => "SegmentWriteFailed"u8,
            TcpEstablishedStreamStatus.UnexpectedPorts => "UnexpectedPorts"u8,
            TcpEstablishedStreamStatus.InvalidFlags => "InvalidFlags"u8,
            TcpEstablishedStreamStatus.NoPayload => "NoPayload"u8,
            TcpEstablishedStreamStatus.DuplicateSegment => "DuplicateSegment"u8,
            TcpEstablishedStreamStatus.OutOfOrderSegment => "OutOfOrderSegment"u8,
            TcpEstablishedStreamStatus.ReceiveBufferTooSmall => "ReceiveBufferTooSmall"u8,
            TcpEstablishedStreamStatus.NoData => "NoData"u8,
            TcpEstablishedStreamStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            TcpEstablishedStreamStatus.InvalidMaximumPayloadLength => "InvalidMaximumPayloadLength"u8,
            TcpEstablishedStreamStatus.InvalidAcknowledgementNumber => "InvalidAcknowledgementNumber"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetCloseStatusName(TcpConnectionCloseStatus status)
    {
        return status switch
        {
            TcpConnectionCloseStatus.Success => "Success"u8,
            TcpConnectionCloseStatus.InvalidState => "InvalidState"u8,
            TcpConnectionCloseStatus.BufferTooSmall => "BufferTooSmall"u8,
            TcpConnectionCloseStatus.SegmentWriteFailed => "SegmentWriteFailed"u8,
            TcpConnectionCloseStatus.SegmentParseFailed => "SegmentParseFailed"u8,
            TcpConnectionCloseStatus.UnexpectedPorts => "UnexpectedPorts"u8,
            TcpConnectionCloseStatus.InvalidFlags => "InvalidFlags"u8,
            TcpConnectionCloseStatus.WrongAcknowledgementNumber => "WrongAcknowledgementNumber"u8,
            TcpConnectionCloseStatus.WrongSequenceNumber => "WrongSequenceNumber"u8,
            TcpConnectionCloseStatus.UnexpectedPayload => "UnexpectedPayload"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetCloseStateName(TcpConnectionCloseState state)
    {
        return state switch
        {
            TcpConnectionCloseState.Established => "Established"u8,
            TcpConnectionCloseState.FinWait1 => "FinWait1"u8,
            TcpConnectionCloseState.FinWait2 => "FinWait2"u8,
            TcpConnectionCloseState.CloseWait => "CloseWait"u8,
            TcpConnectionCloseState.LastAck => "LastAck"u8,
            TcpConnectionCloseState.TimeWait => "TimeWait"u8,
            TcpConnectionCloseState.Closed => "Closed"u8,
            TcpConnectionCloseState.Reset => "Reset"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetChecksumStatusName(Ipv4TransportChecksumStatus status)
    {
        return status switch
        {
            Ipv4TransportChecksumStatus.Success => "Success"u8,
            Ipv4TransportChecksumStatus.InvalidProtocol => "InvalidProtocol"u8,
            Ipv4TransportChecksumStatus.InvalidLength => "InvalidLength"u8,
            _ => "Unknown"u8
        };
    }

    private static void AppendBytes(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        offset = result.NextOffset;
    }

    private static void AppendInt64Decimal(long value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        offset = result.NextOffset;
    }

    private static void AppendUInt64Decimal(ulong value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        offset = result.NextOffset;
    }
}
