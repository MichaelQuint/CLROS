using OS.Std.Text;

namespace OS.Net;
public static class HttpClientGetTransactionDiagnosticFormatter
{
    public static HttpClientGetTransactionDiagnosticFormatResult FormatResult(in HttpClientGetTransactionResult result, int requestBytesWritten, Span<byte> destination)
    {
        HttpClientGetTransactionDiagnosticFormatStatus validationStatus = Validate(in result, requestBytesWritten);
        if (validationStatus != HttpClientGetTransactionDiagnosticFormatStatus.Success)
        {
            return HttpClientGetTransactionDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result, requestBytesWritten);
        if (destination.Length < requiredBytes)
        {
            return HttpClientGetTransactionDiagnosticFormatResult.Fail(HttpClientGetTransactionDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("HTTP_CLIENT_GET_TRANSACTION status="u8, destination, ref offset) || !TryAppendBytes(GetTransactionStatusName(result.Status), destination, ref offset) || !TryAppendBytes(" state="u8, destination, ref offset) || !TryAppendBytes(GetStateName(result.State), destination, ref offset) || !TryAppendBytes(" request="u8, destination, ref offset) || !TryAppendBytes(GetRequestStatusName(result.RequestStatus), destination, ref offset) || !TryAppendBytes(" response="u8, destination, ref offset) || !TryAppendBytes(GetResponseStatusName(result.ResponseStatus), destination, ref offset) || !TryAppendBytes(" requestBytes="u8, destination, ref offset) || !TryAppendInt64Decimal(requestBytesWritten, destination, ref offset) || !TryAppendBytes(" responseStatus="u8, destination, ref offset) || !TryAppendUInt64Decimal(result.StatusCode, destination, ref offset) || !TryAppendBytes(" bodyBytes="u8, destination, ref offset) || !TryAppendInt64Decimal(result.BodyLength, destination, ref offset))
        {
            return HttpClientGetTransactionDiagnosticFormatResult.Fail(HttpClientGetTransactionDiagnosticFormatStatus.DestinationTooSmall, result.Status, requiredBytes, destination.Length);
        }

        return new HttpClientGetTransactionDiagnosticFormatResult(HttpClientGetTransactionDiagnosticFormatStatus.Success, result.Status, bytesWritten: offset, requiredBytes, availableBytes: destination.Length);
    }

    public static HttpClientGetTransactionDiagnosticFormatResult QueryRequiredBytes(in HttpClientGetTransactionResult result, int requestBytesWritten)
    {
        HttpClientGetTransactionDiagnosticFormatStatus validationStatus = Validate(in result, requestBytesWritten);
        if (validationStatus != HttpClientGetTransactionDiagnosticFormatStatus.Success)
        {
            return HttpClientGetTransactionDiagnosticFormatResult.Fail(validationStatus, result.Status, requiredBytes: 0, availableBytes: 0);
        }

        return new HttpClientGetTransactionDiagnosticFormatResult(HttpClientGetTransactionDiagnosticFormatStatus.Success, result.Status, bytesWritten: 0, requiredBytes: GetRequiredBytes(in result, requestBytesWritten), availableBytes: 0);
    }

    private static HttpClientGetTransactionDiagnosticFormatStatus Validate(in HttpClientGetTransactionResult result, int requestBytesWritten)
    {
        if (!result.IsInitialized || !IsKnownTransactionStatus(result.Status) || !IsKnownState(result.State) || !IsKnownRequestStatus(result.RequestStatus) || !IsKnownResponseStatus(result.ResponseStatus) || result.BytesWritten < 0 || result.BytesRequired < 0 || result.BodyOffset < 0 || result.BodyLength < 0 || result.ContentLength < 0 || result.HeaderLength < 0)
        {
            return HttpClientGetTransactionDiagnosticFormatStatus.InvalidResult;
        }

        if (requestBytesWritten < 0)
        {
            return HttpClientGetTransactionDiagnosticFormatStatus.InvalidRequestBytes;
        }

        return HttpClientGetTransactionDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in HttpClientGetTransactionResult result, int requestBytesWritten)
    {
        return "HTTP_CLIENT_GET_TRANSACTION status="u8.Length + GetTransactionStatusName(result.Status).Length + " state="u8.Length + GetStateName(result.State).Length + " request="u8.Length + GetRequestStatusName(result.RequestStatus).Length + " response="u8.Length + GetResponseStatusName(result.ResponseStatus).Length + " requestBytes="u8.Length + AsciiDecimalFormatter.CountInt64Digits(requestBytesWritten) + " responseStatus="u8.Length + AsciiDecimalFormatter.CountUInt64Digits(result.StatusCode) + " bodyBytes="u8.Length + AsciiDecimalFormatter.CountInt64Digits(result.BodyLength);
    }

    private static bool IsKnownTransactionStatus(HttpClientGetTransactionStatus status)
    {
        return status >= HttpClientGetTransactionStatus.Success && status <= HttpClientGetTransactionStatus.TruncatedBody;
    }

    private static bool IsKnownState(HttpClientGetTransactionState state)
    {
        return state >= HttpClientGetTransactionState.Initial && state <= HttpClientGetTransactionState.Failed;
    }

    private static bool IsKnownRequestStatus(HttpRequestBuilderStatus status)
    {
        return status >= HttpRequestBuilderStatus.Success && status <= HttpRequestBuilderStatus.InvalidPath;
    }

    private static bool IsKnownResponseStatus(HttpResponseParseStatus status)
    {
        return status >= HttpResponseParseStatus.Success && status <= HttpResponseParseStatus.DuplicateContentLength;
    }

    private static ReadOnlySpan<byte> GetTransactionStatusName(HttpClientGetTransactionStatus status)
    {
        return status switch
        {
            HttpClientGetTransactionStatus.Success => "Success"u8,
            HttpClientGetTransactionStatus.InvalidState => "InvalidState"u8,
            HttpClientGetTransactionStatus.RequestBufferTooSmall => "RequestBufferTooSmall"u8,
            HttpClientGetTransactionStatus.EmptyHost => "EmptyHost"u8,
            HttpClientGetTransactionStatus.InvalidHost => "InvalidHost"u8,
            HttpClientGetTransactionStatus.EmptyPath => "EmptyPath"u8,
            HttpClientGetTransactionStatus.MissingLeadingSlash => "MissingLeadingSlash"u8,
            HttpClientGetTransactionStatus.InvalidPath => "InvalidPath"u8,
            HttpClientGetTransactionStatus.RequestWriteFailed => "RequestWriteFailed"u8,
            HttpClientGetTransactionStatus.MalformedResponse => "MalformedResponse"u8,
            HttpClientGetTransactionStatus.UnsupportedTransferEncoding => "UnsupportedTransferEncoding"u8,
            HttpClientGetTransactionStatus.TruncatedBody => "TruncatedBody"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetStateName(HttpClientGetTransactionState state)
    {
        return state switch
        {
            HttpClientGetTransactionState.Initial => "Initial"u8,
            HttpClientGetTransactionState.RequestWritten => "RequestWritten"u8,
            HttpClientGetTransactionState.ResponseReceived => "ResponseReceived"u8,
            HttpClientGetTransactionState.Failed => "Failed"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetRequestStatusName(HttpRequestBuilderStatus status)
    {
        return status switch
        {
            HttpRequestBuilderStatus.Success => "Success"u8,
            HttpRequestBuilderStatus.DestinationTooSmall => "DestinationTooSmall"u8,
            HttpRequestBuilderStatus.EmptyHost => "EmptyHost"u8,
            HttpRequestBuilderStatus.InvalidHost => "InvalidHost"u8,
            HttpRequestBuilderStatus.EmptyPath => "EmptyPath"u8,
            HttpRequestBuilderStatus.MissingLeadingSlash => "MissingLeadingSlash"u8,
            HttpRequestBuilderStatus.InvalidPath => "InvalidPath"u8,
            _ => "Unknown"u8
        };
    }

    private static ReadOnlySpan<byte> GetResponseStatusName(HttpResponseParseStatus status)
    {
        return status switch
        {
            HttpResponseParseStatus.Success => "Success"u8,
            HttpResponseParseStatus.BufferTooSmall => "BufferTooSmall"u8,
            HttpResponseParseStatus.MissingStatusLineTerminator => "MissingStatusLineTerminator"u8,
            HttpResponseParseStatus.InvalidProtocol => "InvalidProtocol"u8,
            HttpResponseParseStatus.InvalidStatusCode => "InvalidStatusCode"u8,
            HttpResponseParseStatus.MissingHeaderTerminator => "MissingHeaderTerminator"u8,
            HttpResponseParseStatus.InvalidHeader => "InvalidHeader"u8,
            HttpResponseParseStatus.InvalidContentLength => "InvalidContentLength"u8,
            HttpResponseParseStatus.BodyTooShort => "BodyTooShort"u8,
            HttpResponseParseStatus.UnsupportedTransferEncoding => "UnsupportedTransferEncoding"u8,
            HttpResponseParseStatus.MissingContentLength => "MissingContentLength"u8,
            HttpResponseParseStatus.DuplicateContentLength => "DuplicateContentLength"u8,
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
