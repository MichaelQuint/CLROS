using OS.Std.Text;

namespace OS.Net;

public enum HttpStaticTcpResponsePlanDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}

public readonly struct HttpStaticTcpResponsePlanDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;

    public HttpStaticTcpResponsePlanDiagnosticFormatResult(
        HttpStaticTcpResponsePlanDiagnosticFormatStatus status,
        HttpStaticTcpResponsePlanStatus planStatus,
        int bytesWritten,
        int requiredBytes,
        int availableBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        PlanStatus = planStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public HttpStaticTcpResponsePlanDiagnosticFormatStatus Status { get; }

    public HttpStaticTcpResponsePlanStatus PlanStatus { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public int AvailableBytes { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == HttpStaticTcpResponsePlanDiagnosticFormatStatus.Success &&
        RequiredBytes > 0 &&
        BytesWritten == RequiredBytes &&
        RequiredBytes <= AvailableBytes;

    public static HttpStaticTcpResponsePlanDiagnosticFormatResult Fail(
        HttpStaticTcpResponsePlanDiagnosticFormatStatus status,
        HttpStaticTcpResponsePlanStatus planStatus,
        int requiredBytes,
        int availableBytes)
    {
        return new HttpStaticTcpResponsePlanDiagnosticFormatResult(
            status,
            planStatus,
            bytesWritten: 0,
            requiredBytes,
            availableBytes);
    }
}

public static class HttpStaticTcpResponsePlanDiagnosticFormatter
{
    public const bool UsesSharedAsciiTextBuilder = true;
    public const bool UsesCallerProvidedDestination = true;
    public const bool AllocatesManagedStorage = false;
    public const bool OpensSocket = false;
    public const bool WritesTcpFrame = false;
    public const bool PerformsNicIo = false;
    public const bool StartsKestrel = false;

    public static HttpStaticTcpResponsePlanDiagnosticFormatResult FormatResult(
        in HttpStaticTcpResponsePlanResult result,
        Span<byte> destination)
    {
        HttpStaticTcpResponsePlanDiagnosticFormatStatus validationStatus =
            Validate(in result);
        if (validationStatus != HttpStaticTcpResponsePlanDiagnosticFormatStatus.Success)
        {
            return HttpStaticTcpResponsePlanDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return HttpStaticTcpResponsePlanDiagnosticFormatResult.Fail(
                HttpStaticTcpResponsePlanDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("HTTP_STATIC_TCP_RESPONSE status="u8, destination, ref offset) ||
            !TryAppendBytes(GetPlanStatusName(result.Status), destination, ref offset) ||
            !TryAppendBytes(" tcp="u8, destination, ref offset) ||
            !TryAppendBytes(GetTcpDispatchStatusName(result.TcpDispatchStatus), destination, ref offset) ||
            !TryAppendBytes(" action="u8, destination, ref offset) ||
            !TryAppendBytes(GetTcpActionName(result.TcpAction), destination, ref offset) ||
            !TryAppendBytes(" http="u8, destination, ref offset) ||
            !TryAppendBytes(GetHttpTransactionStatusName(result.HttpStatus), destination, ref offset) ||
            !TryAppendBytes(" request="u8, destination, ref offset) ||
            !TryAppendBytes(GetRequestStatusName(result.RequestStatus), destination, ref offset) ||
            !TryAppendBytes(" response="u8, destination, ref offset) ||
            !TryAppendBytes(GetResponseStatusName(result.ResponseStatus), destination, ref offset) ||
            !TryAppendBytes(" method="u8, destination, ref offset) ||
            !TryAppendBytes(GetMethodName(result.Method), destination, ref offset) ||
            !TryAppendBytes(" httpStatus="u8, destination, ref offset) ||
            !TryAppendUInt64Decimal(result.HttpStatusCode, destination, ref offset) ||
            !TryAppendBytes(" requestPayloadOffset="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.RequestPayloadOffset, destination, ref offset) ||
            !TryAppendBytes(" requestPayloadBytes="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.RequestPayloadLength, destination, ref offset) ||
            !TryAppendBytes(" responseBytesWritten="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.ResponseBytesWritten, destination, ref offset) ||
            !TryAppendBytes(" responseBytesRequired="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.ResponseBytesRequired, destination, ref offset) ||
            !TryAppendBytes(" responseBodyOffset="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.ResponseBodyOffset, destination, ref offset) ||
            !TryAppendBytes(" responseBodyBytes="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.ResponseBodyLength, destination, ref offset) ||
            !TryAppendBytes(" opensSocket="u8, destination, ref offset) ||
            !TryAppendBool(OpensSocket, destination, ref offset) ||
            !TryAppendBytes(" writesTcpFrame="u8, destination, ref offset) ||
            !TryAppendBool(WritesTcpFrame, destination, ref offset) ||
            !TryAppendBytes(" performsNicIo="u8, destination, ref offset) ||
            !TryAppendBool(PerformsNicIo, destination, ref offset) ||
            !TryAppendBytes(" startsKestrel="u8, destination, ref offset) ||
            !TryAppendBool(StartsKestrel, destination, ref offset))
        {
            return HttpStaticTcpResponsePlanDiagnosticFormatResult.Fail(
                HttpStaticTcpResponsePlanDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        return new HttpStaticTcpResponsePlanDiagnosticFormatResult(
            HttpStaticTcpResponsePlanDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: offset,
            requiredBytes,
            availableBytes: destination.Length);
    }

    public static HttpStaticTcpResponsePlanDiagnosticFormatResult QueryRequiredBytes(
        in HttpStaticTcpResponsePlanResult result)
    {
        HttpStaticTcpResponsePlanDiagnosticFormatStatus validationStatus =
            Validate(in result);
        if (validationStatus != HttpStaticTcpResponsePlanDiagnosticFormatStatus.Success)
        {
            return HttpStaticTcpResponsePlanDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                availableBytes: 0);
        }

        return new HttpStaticTcpResponsePlanDiagnosticFormatResult(
            HttpStaticTcpResponsePlanDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: 0,
            requiredBytes: GetRequiredBytes(in result),
            availableBytes: 0);
    }

    private static HttpStaticTcpResponsePlanDiagnosticFormatStatus Validate(
        in HttpStaticTcpResponsePlanResult result)
    {
        if (!result.IsInitialized ||
            !IsKnownPlanStatus(result.Status) ||
            !IsKnownTcpDispatchStatus(result.TcpDispatchStatus) ||
            !IsKnownTcpAction(result.TcpAction) ||
            !IsKnownHttpTransactionStatus(result.HttpStatus) ||
            !IsKnownRequestStatus(result.RequestStatus) ||
            !IsKnownResponseStatus(result.ResponseStatus) ||
            !IsKnownMethod(result.Method) ||
            result.RequestPayloadOffset < 0 ||
            result.RequestPayloadLength < 0 ||
            result.ResponseBytesWritten < 0 ||
            result.ResponseBytesRequired < 0 ||
            result.ResponseBodyOffset < 0 ||
            result.ResponseBodyLength < 0)
        {
            return HttpStaticTcpResponsePlanDiagnosticFormatStatus.InvalidResult;
        }

        if (result.Status == HttpStaticTcpResponsePlanStatus.Success)
        {
            if (!result.Succeeded ||
                result.TcpDispatchStatus != TcpReceiveDispatchStatus.Success ||
                result.TcpAction != TcpReceiveDispatchAction.PayloadAccepted ||
                result.HttpStatus != HttpStaticResponseTransactionStatus.Success ||
                result.RequestStatus != HttpRequestParseStatus.Success ||
                result.ResponseStatus != HttpResponseWriterStatus.Success ||
                result.HttpStatusCode == 0 ||
                result.RequestPayloadLength <= 0 ||
                result.ResponseBytesWritten <= 0 ||
                result.ResponseBytesRequired != result.ResponseBytesWritten ||
                result.ResponseBodyOffset > result.ResponseBytesWritten ||
                result.ResponseBodyLength > result.ResponseBytesWritten - result.ResponseBodyOffset)
            {
                return HttpStaticTcpResponsePlanDiagnosticFormatStatus.InvalidResult;
            }
        }

        return HttpStaticTcpResponsePlanDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in HttpStaticTcpResponsePlanResult result)
    {
        return "HTTP_STATIC_TCP_RESPONSE status="u8.Length
            + GetPlanStatusName(result.Status).Length
            + " tcp="u8.Length
            + GetTcpDispatchStatusName(result.TcpDispatchStatus).Length
            + " action="u8.Length
            + GetTcpActionName(result.TcpAction).Length
            + " http="u8.Length
            + GetHttpTransactionStatusName(result.HttpStatus).Length
            + " request="u8.Length
            + GetRequestStatusName(result.RequestStatus).Length
            + " response="u8.Length
            + GetResponseStatusName(result.ResponseStatus).Length
            + " method="u8.Length
            + GetMethodName(result.Method).Length
            + " httpStatus="u8.Length
            + AsciiDecimalFormatter.CountUInt32Digits(result.HttpStatusCode)
            + " requestPayloadOffset="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.RequestPayloadOffset)
            + " requestPayloadBytes="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.RequestPayloadLength)
            + " responseBytesWritten="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.ResponseBytesWritten)
            + " responseBytesRequired="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.ResponseBytesRequired)
            + " responseBodyOffset="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.ResponseBodyOffset)
            + " responseBodyBytes="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.ResponseBodyLength)
            + " opensSocket="u8.Length
            + GetBoolName(OpensSocket).Length
            + " writesTcpFrame="u8.Length
            + GetBoolName(WritesTcpFrame).Length
            + " performsNicIo="u8.Length
            + GetBoolName(PerformsNicIo).Length
            + " startsKestrel="u8.Length
            + GetBoolName(StartsKestrel).Length;
    }

    private static bool IsKnownPlanStatus(HttpStaticTcpResponsePlanStatus status)
    {
        return status >= HttpStaticTcpResponsePlanStatus.Success &&
            status <= HttpStaticTcpResponsePlanStatus.HttpTransactionFailed;
    }

    private static bool IsKnownTcpDispatchStatus(TcpReceiveDispatchStatus status)
    {
        return status >= TcpReceiveDispatchStatus.Success &&
            status <= TcpReceiveDispatchStatus.ControlSegmentRejected;
    }

    private static bool IsKnownTcpAction(TcpReceiveDispatchAction action)
    {
        return action >= TcpReceiveDispatchAction.None &&
            action <= TcpReceiveDispatchAction.ResetReceived;
    }

    private static bool IsKnownHttpTransactionStatus(HttpStaticResponseTransactionStatus status)
    {
        return status >= HttpStaticResponseTransactionStatus.Success &&
            status <= HttpStaticResponseTransactionStatus.ResponseWriteFailed;
    }

    private static bool IsKnownRequestStatus(HttpRequestParseStatus status)
    {
        return status >= HttpRequestParseStatus.Success &&
            status <= HttpRequestParseStatus.DuplicateContentLength;
    }

    private static bool IsKnownResponseStatus(HttpResponseWriterStatus status)
    {
        return status >= HttpResponseWriterStatus.Success &&
            status <= HttpResponseWriterStatus.InvalidContentType;
    }

    private static bool IsKnownMethod(HttpRequestMethod method)
    {
        return method >= HttpRequestMethod.Unknown &&
            method <= HttpRequestMethod.Post;
    }

    private static ReadOnlySpan<byte> GetPlanStatusName(HttpStaticTcpResponsePlanStatus status)
    {
        return status switch
        {
            HttpStaticTcpResponsePlanStatus.Success => "Success"u8,
            HttpStaticTcpResponsePlanStatus.TcpDispatchNotInitialized => "TcpDispatchNotInitialized"u8,
            HttpStaticTcpResponsePlanStatus.TcpDispatchFailed => "TcpDispatchFailed"u8,
            HttpStaticTcpResponsePlanStatus.NonPayloadDispatch => "NonPayloadDispatch"u8,
            HttpStaticTcpResponsePlanStatus.InvalidPayloadRange => "InvalidPayloadRange"u8,
            HttpStaticTcpResponsePlanStatus.HttpTransactionFailed => "HttpTransactionFailed"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetTcpDispatchStatusName(TcpReceiveDispatchStatus status)
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

    private static ReadOnlySpan<byte> GetTcpActionName(TcpReceiveDispatchAction action)
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

    private static ReadOnlySpan<byte> GetHttpTransactionStatusName(
        HttpStaticResponseTransactionStatus status)
    {
        return status switch
        {
            HttpStaticResponseTransactionStatus.Success => "Success"u8,
            HttpStaticResponseTransactionStatus.InvalidRoute => "InvalidRoute"u8,
            HttpStaticResponseTransactionStatus.RequestParseFailed => "RequestParseFailed"u8,
            HttpStaticResponseTransactionStatus.ResponseWriteFailed => "ResponseWriteFailed"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetRequestStatusName(HttpRequestParseStatus status)
    {
        return status switch
        {
            HttpRequestParseStatus.Success => "Success"u8,
            HttpRequestParseStatus.BufferTooSmall => "BufferTooSmall"u8,
            HttpRequestParseStatus.MissingRequestLineTerminator => "MissingRequestLineTerminator"u8,
            HttpRequestParseStatus.UnsupportedMethod => "UnsupportedMethod"u8,
            HttpRequestParseStatus.InvalidRequestTarget => "InvalidRequestTarget"u8,
            HttpRequestParseStatus.InvalidProtocol => "InvalidProtocol"u8,
            HttpRequestParseStatus.MissingHeaderTerminator => "MissingHeaderTerminator"u8,
            HttpRequestParseStatus.InvalidHeader => "InvalidHeader"u8,
            HttpRequestParseStatus.InvalidContentLength => "InvalidContentLength"u8,
            HttpRequestParseStatus.BodyTooShort => "BodyTooShort"u8,
            HttpRequestParseStatus.UnsupportedTransferEncoding => "UnsupportedTransferEncoding"u8,
            HttpRequestParseStatus.DuplicateContentLength => "DuplicateContentLength"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetResponseStatusName(HttpResponseWriterStatus status)
    {
        return status switch
        {
            HttpResponseWriterStatus.Success => "Success"u8,
            HttpResponseWriterStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            HttpResponseWriterStatus.InvalidStatusCode => "InvalidStatusCode"u8,
            HttpResponseWriterStatus.EmptyReasonPhrase => "EmptyReasonPhrase"u8,
            HttpResponseWriterStatus.InvalidReasonPhrase => "InvalidReasonPhrase"u8,
            HttpResponseWriterStatus.EmptyContentType => "EmptyContentType"u8,
            HttpResponseWriterStatus.InvalidContentType => "InvalidContentType"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetMethodName(HttpRequestMethod method)
    {
        return method switch
        {
            HttpRequestMethod.Unknown => "Unknown"u8,
            HttpRequestMethod.Get => "Get"u8,
            HttpRequestMethod.Head => "Head"u8,
            HttpRequestMethod.Post => "Post"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetBoolName(bool value)
    {
        return value ? "true"u8 : "false"u8;
    }

    private static bool TryAppendBytes(
        ReadOnlySpan<byte> value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result = AsciiTextBuilder.AppendBytes(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendInt64Decimal(
        long value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendUInt64Decimal(
        ulong value,
        Span<byte> destination,
        ref int offset)
    {
        AsciiTextBuilderResult result =
            AsciiTextBuilder.AppendUInt64Decimal(destination, offset, value);
        if (!result.Succeeded)
        {
            return false;
        }

        offset = result.NextOffset;
        return true;
    }

    private static bool TryAppendBool(
        bool value,
        Span<byte> destination,
        ref int offset)
    {
        return TryAppendBytes(GetBoolName(value), destination, ref offset);
    }
}
