using OS.Std.Text;

namespace OS.Net;

public static class TcpConnectionCloseIpv4FramePlanDiagnosticFormatter
{
    public static TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult FormatResult(in TcpConnectionCloseIpv4FramePlanResult result, Span<byte> destination)
    {
        TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.Success)
        {
            return TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult.Fail(TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("TCP_CLOSE_IPV4_FRAME status="u8, destination, ref offset) || !TryAppendBytes(GetFrameStatusName(result.Status), destination, ref offset) || !TryAppendBytes(" state="u8, destination, ref offset) || !TryAppendBytes(GetCloseStateName(result.State), destination, ref offset) || !TryAppendBytes(" tcp="u8, destination, ref offset) || !TryAppendBytes(GetCloseStatusName(result.TcpStatus), destination, ref offset) || !TryAppendBytes(" packet="u8, destination, ref offset) || !TryAppendBytes(GetPacketStatusName(result.PacketStatus), destination, ref offset) || !TryAppendBytes(" bytes="u8, destination, ref offset) || !TryAppendInt64Decimal(result.BytesWritten, destination, ref offset) || !TryAppendBytes(" tcpOffset="u8, destination, ref offset) || !TryAppendInt64Decimal(result.TcpSegmentOffset, destination, ref offset) || !TryAppendBytes(" tcpBytes="u8, destination, ref offset) || !TryAppendInt64Decimal(result.TcpSegmentLength, destination, ref offset) || !TryAppendBytes(" localPort="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.LocalPort, destination, ref offset) || !TryAppendBytes(" remotePort="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.RemotePort, destination, ref offset) || !TryAppendBytes(" ttl="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.TimeToLive, destination, ref offset) || !TryAppendBytes(" identification="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.Identification, destination, ref offset) || !TryAppendBytes(" localSeq="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.LocalNextSequenceNumber, destination, ref offset) || !TryAppendBytes(" remoteSeq="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.RemoteNextSequenceNumber, destination, ref offset))
        {
            return TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult.Fail(TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        return new TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult(TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult QueryRequiredBytes(in TcpConnectionCloseIpv4FramePlanResult result)
    {
        TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.Success)
        {
            return TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new TcpConnectionCloseIpv4FramePlanDiagnosticFormatResult(TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus Validate(in TcpConnectionCloseIpv4FramePlanResult result)
    {
        if (!result.IsInitialized || !IsKnownFrameStatus(result.Status) || !IsKnownCloseState(result.State) || !IsKnownCloseStatus(result.TcpStatus) || !IsKnownPacketStatus(result.PacketStatus) || result.BytesWritten < 0 || result.TcpSegmentOffset < 0 || result.TcpSegmentLength < 0)
        {
            return TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.InvalidResult;
        }

        return TcpConnectionCloseIpv4FramePlanDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in TcpConnectionCloseIpv4FramePlanResult result)
    {
        return "TCP_CLOSE_IPV4_FRAME status="u8.Length + GetFrameStatusName(result.Status).Length + " state="u8.Length + GetCloseStateName(result.State).Length + " tcp="u8.Length + GetCloseStatusName(result.TcpStatus).Length + " packet="u8.Length + GetPacketStatusName(result.PacketStatus).Length + " bytes="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.BytesWritten) + " tcpOffset="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.TcpSegmentOffset) + " tcpBytes="u8.Length + AsciiDecimalFormatter.CountInt32Digits(result.TcpSegmentLength) + " localPort="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.LocalPort) + " remotePort="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.RemotePort) + " ttl="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.TimeToLive) + " identification="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.Identification) + " localSeq="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.LocalNextSequenceNumber) + " remoteSeq="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.RemoteNextSequenceNumber);
    }

    private static bool IsKnownFrameStatus(TcpConnectionCloseIpv4FramePlanStatus status)
    {
        return status >= TcpConnectionCloseIpv4FramePlanStatus.Success && status <= TcpConnectionCloseIpv4FramePlanStatus.FrameWriteFailed;
    }

    private static bool IsKnownCloseState(TcpConnectionCloseState state)
    {
        return state >= TcpConnectionCloseState.Established && state <= TcpConnectionCloseState.Reset;
    }

    private static bool IsKnownCloseStatus(TcpConnectionCloseStatus status)
    {
        return status >= TcpConnectionCloseStatus.Success && status <= TcpConnectionCloseStatus.UnexpectedPayload;
    }

    private static bool IsKnownPacketStatus(PacketParseStatus status)
    {
        return status >= PacketParseStatus.Success && status <= PacketParseStatus.UnsupportedIcmpCode;
    }

    private static ReadOnlySpan<byte> GetFrameStatusName(TcpConnectionCloseIpv4FramePlanStatus status)
    {
        return status switch
        {
            TcpConnectionCloseIpv4FramePlanStatus.Success => "Success"u8,
            TcpConnectionCloseIpv4FramePlanStatus.InvalidLocalIdentity => "InvalidLocalIdentity"u8,
            TcpConnectionCloseIpv4FramePlanStatus.InvalidRemoteIdentity => "InvalidRemoteIdentity"u8,
            TcpConnectionCloseIpv4FramePlanStatus.InvalidState => "InvalidState"u8,
            TcpConnectionCloseIpv4FramePlanStatus.FrameBufferTooSmall => "FrameBufferTooSmall"u8,
            TcpConnectionCloseIpv4FramePlanStatus.TcpSegmentWriteFailed => "TcpSegmentWriteFailed"u8,
            TcpConnectionCloseIpv4FramePlanStatus.FrameWriteFailed => "FrameWriteFailed"u8,
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

    private static bool TryAppendBytes(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendInt64Decimal(long value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendUInt64Decimal(ulong value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }
}
