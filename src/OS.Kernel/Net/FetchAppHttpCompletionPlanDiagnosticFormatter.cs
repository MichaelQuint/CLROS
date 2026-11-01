using OS.Std.Text;

namespace OS.Net;
public static class FetchAppHttpCompletionPlanDiagnosticFormatter
{
    public static FetchAppHttpCompletionPlanDiagnosticFormatResult FormatResult(in FetchAppHttpCompletionPlanResult result, Span<byte> destination)
    {
        FetchAppHttpCompletionPlanDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != FetchAppHttpCompletionPlanDiagnosticFormatStatus.Success)
        {
            return FetchAppHttpCompletionPlanDiagnosticFormatResult.Fail(validationStatus, result.Status, result.HttpPlanStatus, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return FetchAppHttpCompletionPlanDiagnosticFormatResult.Fail(FetchAppHttpCompletionPlanDiagnosticFormatStatus.DestinationTooSmall, result.Status, result.HttpPlanStatus, requiredBytes, destination.Length);
        }

        int offset = 0;
        Append("FETCH_HTTP_COMPLETION status="u8, destination, ref offset);
        Append(GetCompletionStatusName(result.Status), destination, ref offset);
        Append(" httpPlan="u8, destination, ref offset);
        Append(GetHttpPlanStatusName(result.HttpPlanStatus), destination, ref offset);
        Append(" httpStatus="u8, destination, ref offset);
        AppendUInt64(result.HttpStatusCode, destination, ref offset);
        Append(" expectedHttpStatus="u8, destination, ref offset);
        AppendUInt64(result.ExpectedHttpStatusCode, destination, ref offset);
        Append(" responseBytes="u8, destination, ref offset);
        AppendInt64(result.ResponseBytesRead, destination, ref offset);
        Append(" bodyOffset="u8, destination, ref offset);
        AppendInt64(result.BodyOffset, destination, ref offset);
        Append(" bodyBytes="u8, destination, ref offset);
        AppendInt64(result.BodyLength, destination, ref offset);
        Append(" expectedMarkerBytes="u8, destination, ref offset);
        AppendInt64(result.ExpectedMarkerLength, destination, ref offset);
        Append(" matchOffset="u8, destination, ref offset);
        AppendInt64(result.MatchOffset, destination, ref offset);
        return new FetchAppHttpCompletionPlanDiagnosticFormatResult(FetchAppHttpCompletionPlanDiagnosticFormatStatus.Success, result.Status, result.HttpPlanStatus, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static FetchAppHttpCompletionPlanDiagnosticFormatResult QueryRequiredBytes(in FetchAppHttpCompletionPlanResult result)
    {
        FetchAppHttpCompletionPlanDiagnosticFormatStatus validationStatus = Validate(in result);
        if (validationStatus != FetchAppHttpCompletionPlanDiagnosticFormatStatus.Success)
        {
            return FetchAppHttpCompletionPlanDiagnosticFormatResult.Fail(validationStatus, result.Status, result.HttpPlanStatus, requiredBytes: 0, availableBytes: 0);
        }

        return new FetchAppHttpCompletionPlanDiagnosticFormatResult(FetchAppHttpCompletionPlanDiagnosticFormatStatus.Success, result.Status, result.HttpPlanStatus, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result), availableBytes: 0);
    }

    private static FetchAppHttpCompletionPlanDiagnosticFormatStatus Validate(in FetchAppHttpCompletionPlanResult result)
    {
        if (!result.IsInitialized || !IsKnownCompletionStatus(result.Status) || !IsKnownHttpPlanStatus(result.HttpPlanStatus) || result.ResponseBytesRead < 0 || result.BodyOffset < 0 || result.BodyLength < 0 || result.ExpectedMarkerLength < 0)
        {
            return FetchAppHttpCompletionPlanDiagnosticFormatStatus.InvalidResult;
        }

        if (result.Status == FetchAppHttpCompletionPlanStatus.Success)
        {
            if (!result.Succeeded || result.MatchOffset < 0)
            {
                return FetchAppHttpCompletionPlanDiagnosticFormatStatus.InvalidResult;
            }
        }
        else if (result.MatchOffset < FetchAppHttpCompletionPlanResult.NoMatchOffset)
        {
            return FetchAppHttpCompletionPlanDiagnosticFormatStatus.InvalidResult;
        }

        return FetchAppHttpCompletionPlanDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in FetchAppHttpCompletionPlanResult result)
    {
        return "FETCH_HTTP_COMPLETION status="u8.Length + GetCompletionStatusName(result.Status).Length + " httpPlan="u8.Length + GetHttpPlanStatusName(result.HttpPlanStatus).Length + " httpStatus="u8.Length + GetUInt64DecimalByteCount(result.HttpStatusCode) + " expectedHttpStatus="u8.Length + GetUInt64DecimalByteCount(result.ExpectedHttpStatusCode) + " responseBytes="u8.Length + GetInt64DecimalByteCount(result.ResponseBytesRead) + " bodyOffset="u8.Length + GetInt64DecimalByteCount(result.BodyOffset) + " bodyBytes="u8.Length + GetInt64DecimalByteCount(result.BodyLength) + " expectedMarkerBytes="u8.Length + GetInt64DecimalByteCount(result.ExpectedMarkerLength) + " matchOffset="u8.Length + GetInt64DecimalByteCount(result.MatchOffset);
    }

    private static bool IsKnownCompletionStatus(FetchAppHttpCompletionPlanStatus status)
    {
        return status >= FetchAppHttpCompletionPlanStatus.Success && status <= FetchAppHttpCompletionPlanStatus.ResponseBufferTooSmall;
    }

    private static bool IsKnownHttpPlanStatus(HttpTcpClientGetPlanStatus status)
    {
        return status >= HttpTcpClientGetPlanStatus.Success && status <= HttpTcpClientGetPlanStatus.HttpResponseFailed;
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
