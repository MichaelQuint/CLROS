using OS.Std.Text;

namespace OS.Net;
public static class FetchAppHttpTcpExchangePlanDiagnosticFormatter
{
    public static FetchAppHttpTcpExchangePlanDiagnosticFormatResult FormatResult(in FetchAppHttpTcpExchangePlanResult result, Span<byte> destination)
    {
        FetchAppHttpTcpExchangePlanDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.Success)
        {
            return FetchAppHttpTcpExchangePlanDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return FetchAppHttpTcpExchangePlanDiagnosticFormatResult.Fail(FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        int offset = 0;
        Append("FETCH_HTTP_TCP_EXCHANGE status="u8, destination, ref offset);
        Append(GetExchangeStatusName(result.Status), destination, ref offset);
        Append(" context="u8, destination, ref offset);
        Append(GetContextStatusName(result.Context.Status), destination, ref offset);
        Append(" syn="u8, destination, ref offset);
        Append(GetHttpPlanStatusName(result.Syn.Status), destination, ref offset);
        Append(" synAck="u8, destination, ref offset);
        Append(GetHttpPlanStatusName(result.SynAck.Status), destination, ref offset);
        Append(" finalAck="u8, destination, ref offset);
        Append(GetHttpPlanStatusName(result.FinalAck.Status), destination, ref offset);
        Append(" request="u8, destination, ref offset);
        Append(GetHttpPlanStatusName(result.Request.Status), destination, ref offset);
        Append(" response="u8, destination, ref offset);
        Append(GetHttpPlanStatusName(result.Response.Status), destination, ref offset);
        Append(" completion="u8, destination, ref offset);
        Append(GetCompletionStatusName(result.Completion.Status), destination, ref offset);
        Append(" close="u8, destination, ref offset);
        Append(GetCloseStatusName(result.Close.Status), destination, ref offset);
        Append(" hostBytes="u8, destination, ref offset);
        AppendInt64(result.HostBytes, destination, ref offset);
        Append(" pathBytes="u8, destination, ref offset);
        AppendInt64(result.PathBytes, destination, ref offset);
        Append(" expectedHttpStatus="u8, destination, ref offset);
        AppendUInt64(result.ExpectedHttpStatusCode, destination, ref offset);
        Append(" expectedMarkerBytes="u8, destination, ref offset);
        AppendInt64(result.ExpectedMarkerLength, destination, ref offset);
        Append(" synBytes="u8, destination, ref offset);
        AppendInt64(result.SynFrameBytes, destination, ref offset);
        Append(" finalAckBytes="u8, destination, ref offset);
        AppendInt64(result.FinalAckFrameBytes, destination, ref offset);
        Append(" requestBytes="u8, destination, ref offset);
        AppendInt64(result.RequestFrameBytes, destination, ref offset);
        Append(" responseBytes="u8, destination, ref offset);
        AppendInt64(result.ResponseBytesRead, destination, ref offset);
        Append(" closeBytes="u8, destination, ref offset);
        AppendInt64(result.CloseFrameBytes, destination, ref offset);
        Append(" localSeq="u8, destination, ref offset);
        AppendUInt64(GetLocalSequence(in result), destination, ref offset);
        Append(" remoteSeq="u8, destination, ref offset);
        AppendUInt64(GetRemoteSequence(in result), destination, ref offset);
        return new FetchAppHttpTcpExchangePlanDiagnosticFormatResult(FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static FetchAppHttpTcpExchangePlanDiagnosticFormatResult QueryRequiredBytes(in FetchAppHttpTcpExchangePlanResult result)
    {
        FetchAppHttpTcpExchangePlanDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.Success)
        {
            return FetchAppHttpTcpExchangePlanDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new FetchAppHttpTcpExchangePlanDiagnosticFormatResult(FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static FetchAppHttpTcpExchangePlanDiagnosticFormatStatus Validate(in FetchAppHttpTcpExchangePlanResult result)
    {
        if (!result.IsInitialized || !IsKnownExchangeStatus(result.Status) || !IsKnownContextStatus(result.Context.Status) || !IsKnownHttpPlanStatus(result.Syn.Status) || !IsKnownHttpPlanStatus(result.SynAck.Status) || !IsKnownHttpPlanStatus(result.FinalAck.Status) || !IsKnownHttpPlanStatus(result.Request.Status) || !IsKnownHttpPlanStatus(result.Response.Status) || !IsKnownCompletionStatus(result.Completion.Status) || !IsKnownCloseStatus(result.Close.Status) || result.HostBytes < 0 || result.PathBytes < 0 || result.ExpectedMarkerLength < 0 || result.SynFrameBytes < 0 || result.FinalAckFrameBytes < 0 || result.RequestFrameBytes < 0 || result.ResponseBytesRead < 0 || result.CloseFrameBytes < 0)
        {
            return FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.InvalidResult;
        }

        if (result.Status == FetchAppHttpTcpExchangePlanStatus.Success && !result.Succeeded)
        {
            return FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.InvalidResult;
        }

        return FetchAppHttpTcpExchangePlanDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in FetchAppHttpTcpExchangePlanResult result)
    {
        return "FETCH_HTTP_TCP_EXCHANGE status="u8.Length + GetExchangeStatusName(result.Status).Length + " context="u8.Length + GetContextStatusName(result.Context.Status).Length + " syn="u8.Length + GetHttpPlanStatusName(result.Syn.Status).Length + " synAck="u8.Length + GetHttpPlanStatusName(result.SynAck.Status).Length + " finalAck="u8.Length + GetHttpPlanStatusName(result.FinalAck.Status).Length + " request="u8.Length + GetHttpPlanStatusName(result.Request.Status).Length + " response="u8.Length + GetHttpPlanStatusName(result.Response.Status).Length + " completion="u8.Length + GetCompletionStatusName(result.Completion.Status).Length + " close="u8.Length + GetCloseStatusName(result.Close.Status).Length + " hostBytes="u8.Length + GetInt64DecimalByteCount(result.HostBytes) + " pathBytes="u8.Length + GetInt64DecimalByteCount(result.PathBytes) + " expectedHttpStatus="u8.Length + GetUInt64DecimalByteCount(result.ExpectedHttpStatusCode) + " expectedMarkerBytes="u8.Length + GetInt64DecimalByteCount(result.ExpectedMarkerLength) + " synBytes="u8.Length + GetInt64DecimalByteCount(result.SynFrameBytes) + " finalAckBytes="u8.Length + GetInt64DecimalByteCount(result.FinalAckFrameBytes) + " requestBytes="u8.Length + GetInt64DecimalByteCount(result.RequestFrameBytes) + " responseBytes="u8.Length + GetInt64DecimalByteCount(result.ResponseBytesRead) + " closeBytes="u8.Length + GetInt64DecimalByteCount(result.CloseFrameBytes) + " localSeq="u8.Length + GetUInt64DecimalByteCount(GetLocalSequence(in result)) + " remoteSeq="u8.Length + GetUInt64DecimalByteCount(GetRemoteSequence(in result));
    }

    private static ulong GetLocalSequence(in FetchAppHttpTcpExchangePlanResult result)
    {
        if (result.Close.IsInitialized)
        {
            return result.Close.LocalNextSequenceNumber;
        }

        if (result.Response.IsInitialized)
        {
            return result.Response.LocalNextSequenceNumber;
        }

        if (result.Request.IsInitialized)
        {
            return result.Request.LocalNextSequenceNumber;
        }

        if (result.FinalAck.IsInitialized)
        {
            return result.FinalAck.LocalNextSequenceNumber;
        }

        if (result.SynAck.IsInitialized)
        {
            return result.SynAck.LocalNextSequenceNumber;
        }

        return result.Syn.LocalNextSequenceNumber;
    }

    private static ulong GetRemoteSequence(in FetchAppHttpTcpExchangePlanResult result)
    {
        if (result.Close.IsInitialized)
        {
            return result.Close.RemoteNextSequenceNumber;
        }

        if (result.Response.IsInitialized)
        {
            return result.Response.RemoteNextSequenceNumber;
        }

        if (result.Request.IsInitialized)
        {
            return result.Request.RemoteNextSequenceNumber;
        }

        if (result.FinalAck.IsInitialized)
        {
            return result.FinalAck.RemoteNextSequenceNumber;
        }

        if (result.SynAck.IsInitialized)
        {
            return result.SynAck.RemoteNextSequenceNumber;
        }

        return result.Syn.RemoteNextSequenceNumber;
    }

    private static bool IsKnownExchangeStatus(FetchAppHttpTcpExchangePlanStatus status)
    {
        return status >= FetchAppHttpTcpExchangePlanStatus.Success && status <= FetchAppHttpTcpExchangePlanStatus.CloseFrameFailed;
    }

    private static bool IsKnownContextStatus(FetchAppHttpTcpContextPlanStatus status)
    {
        return status >= FetchAppHttpTcpContextPlanStatus.Success && status <= FetchAppHttpTcpContextPlanStatus.InvalidRemoteIdentity;
    }

    private static bool IsKnownHttpPlanStatus(HttpTcpClientGetPlanStatus status)
    {
        return status >= HttpTcpClientGetPlanStatus.Success && status <= HttpTcpClientGetPlanStatus.HttpResponseFailed;
    }

    private static bool IsKnownCompletionStatus(FetchAppHttpCompletionPlanStatus status)
    {
        return status >= FetchAppHttpCompletionPlanStatus.Success && status <= FetchAppHttpCompletionPlanStatus.ResponseBufferTooSmall;
    }

    private static bool IsKnownCloseStatus(TcpConnectionCloseIpv4FramePlanStatus status)
    {
        return status >= TcpConnectionCloseIpv4FramePlanStatus.Success && status <= TcpConnectionCloseIpv4FramePlanStatus.FrameWriteFailed;
    }

    private static ReadOnlySpan<byte> GetExchangeStatusName(FetchAppHttpTcpExchangePlanStatus status)
    {
        return status switch
        {
            FetchAppHttpTcpExchangePlanStatus.Success => "Success"u8,
            FetchAppHttpTcpExchangePlanStatus.ContextFailed => "ContextFailed"u8,
            FetchAppHttpTcpExchangePlanStatus.EndpointBufferTooSmall => "EndpointBufferTooSmall"u8,
            FetchAppHttpTcpExchangePlanStatus.SynFrameFailed => "SynFrameFailed"u8,
            FetchAppHttpTcpExchangePlanStatus.SynAckFrameFailed => "SynAckFrameFailed"u8,
            FetchAppHttpTcpExchangePlanStatus.FinalAckFrameFailed => "FinalAckFrameFailed"u8,
            FetchAppHttpTcpExchangePlanStatus.RequestFrameFailed => "RequestFrameFailed"u8,
            FetchAppHttpTcpExchangePlanStatus.ResponseFrameFailed => "ResponseFrameFailed"u8,
            FetchAppHttpTcpExchangePlanStatus.CompletionFailed => "CompletionFailed"u8,
            FetchAppHttpTcpExchangePlanStatus.CloseFrameFailed => "CloseFrameFailed"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetContextStatusName(FetchAppHttpTcpContextPlanStatus status)
    {
        return status switch
        {
            FetchAppHttpTcpContextPlanStatus.Success => "Success"u8,
            FetchAppHttpTcpContextPlanStatus.EndpointFailed => "EndpointFailed"u8,
            FetchAppHttpTcpContextPlanStatus.DnsFailed => "DnsFailed"u8,
            FetchAppHttpTcpContextPlanStatus.DnsHostMismatch => "DnsHostMismatch"u8,
            FetchAppHttpTcpContextPlanStatus.RouteNotCached => "RouteNotCached"u8,
            FetchAppHttpTcpContextPlanStatus.InvalidLocalIdentity => "InvalidLocalIdentity"u8,
            FetchAppHttpTcpContextPlanStatus.InvalidRemoteIdentity => "InvalidRemoteIdentity"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetHttpPlanStatusName(HttpTcpClientGetPlanStatus status)
    {
        return status switch
        {
            HttpTcpClientGetPlanStatus.Success => "Success"u8,
            HttpTcpClientGetPlanStatus.InvalidState => "InvalidState"u8,
            HttpTcpClientGetPlanStatus.RequestBufferTooSmall => "RequestBufferTooSmall"u8,
            HttpTcpClientGetPlanStatus.RequestTooLarge => "RequestTooLarge"u8,
            HttpTcpClientGetPlanStatus.ResponseBufferTooSmall => "ResponseBufferTooSmall"u8,
            HttpTcpClientGetPlanStatus.TcpFrameFailed => "TcpFrameFailed"u8,
            HttpTcpClientGetPlanStatus.FrameBufferTooSmall => "FrameBufferTooSmall"u8,
            HttpTcpClientGetPlanStatus.FrameWriteFailed => "FrameWriteFailed"u8,
            HttpTcpClientGetPlanStatus.FrameParseFailed => "FrameParseFailed"u8,
            HttpTcpClientGetPlanStatus.UnsupportedEtherType => "UnsupportedEtherType"u8,
            HttpTcpClientGetPlanStatus.UnsupportedIpProtocol => "UnsupportedIpProtocol"u8,
            HttpTcpClientGetPlanStatus.WrongLocalMac => "WrongLocalMac"u8,
            HttpTcpClientGetPlanStatus.WrongRemoteMac => "WrongRemoteMac"u8,
            HttpTcpClientGetPlanStatus.WrongLocalAddress => "WrongLocalAddress"u8,
            HttpTcpClientGetPlanStatus.WrongRemoteAddress => "WrongRemoteAddress"u8,
            HttpTcpClientGetPlanStatus.TcpSegmentParseFailed => "TcpSegmentParseFailed"u8,
            HttpTcpClientGetPlanStatus.WrongTcpPorts => "WrongTcpPorts"u8,
            HttpTcpClientGetPlanStatus.InvalidTcpFlags => "InvalidTcpFlags"u8,
            HttpTcpClientGetPlanStatus.WrongAcknowledgementNumber => "WrongAcknowledgementNumber"u8,
            HttpTcpClientGetPlanStatus.TcpStreamSendFailed => "TcpStreamSendFailed"u8,
            HttpTcpClientGetPlanStatus.TcpStreamReceiveFailed => "TcpStreamReceiveFailed"u8,
            HttpTcpClientGetPlanStatus.TcpStreamReadFailed => "TcpStreamReadFailed"u8,
            HttpTcpClientGetPlanStatus.HttpRequestFailed => "HttpRequestFailed"u8,
            HttpTcpClientGetPlanStatus.HttpResponseFailed => "HttpResponseFailed"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetCompletionStatusName(FetchAppHttpCompletionPlanStatus status)
    {
        return status switch
        {
            FetchAppHttpCompletionPlanStatus.Success => "Success"u8,
            FetchAppHttpCompletionPlanStatus.HttpPlanFailed => "HttpPlanFailed"u8,
            FetchAppHttpCompletionPlanStatus.UnexpectedHttpStatus => "UnexpectedHttpStatus"u8,
            FetchAppHttpCompletionPlanStatus.InvalidBodyRange => "InvalidBodyRange"u8,
            FetchAppHttpCompletionPlanStatus.MissingExpectedMarker => "MissingExpectedMarker"u8,
            FetchAppHttpCompletionPlanStatus.InvalidExpectedMarker => "InvalidExpectedMarker"u8,
            FetchAppHttpCompletionPlanStatus.ResponseBufferTooSmall => "ResponseBufferTooSmall"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetCloseStatusName(TcpConnectionCloseIpv4FramePlanStatus status)
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

    private static int GetInt64DecimalByteCount(long value)
    {
        bool negative = value < 0;
        ulong magnitude;
        if (value == long.MinValue)
        {
            magnitude = ((ulong)long.MaxValue) + 1UL;
        }
        else if (negative)
        {
            magnitude = (ulong)(-value);
        }
        else
        {
            magnitude = (ulong)value;
        }

        return GetUInt64DecimalByteCount(magnitude) + (negative ? 1 : 0);
    }

    private static int GetUInt64DecimalByteCount(ulong value)
    {
        int count = 1;
        while (value >= 10)
        {
            value /= 10;
            count++;
        }

        return count;
    }

    private static void Append(ReadOnlySpan<byte> value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }

    private static void AppendInt64(long value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }

    private static void AppendUInt64(ulong value, Span<byte> destination, ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (result.Succeeded)
        {
            offset = result.NextOffset;
        }
    }
}
