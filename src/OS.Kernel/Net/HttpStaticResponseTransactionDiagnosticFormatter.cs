using OS.Std.Text;

namespace OS.Net;

public enum HttpStaticResponseTransactionDiagnosticFormatStatus
{
    Success = 0,
    InvalidResult = 1,
    DestinationTooSmall = 2
}

public readonly struct HttpStaticResponseTransactionDiagnosticFormatResult
{
    private const uint InitializedFlag = 1;

    public HttpStaticResponseTransactionDiagnosticFormatResult(
        HttpStaticResponseTransactionDiagnosticFormatStatus status,
        HttpStaticResponseTransactionStatus transactionStatus,
        int bytesWritten,
        int requiredBytes,
        int availableBytes,
        uint flags = InitializedFlag)
    {
        Status = status;
        TransactionStatus = transactionStatus;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        AvailableBytes = availableBytes;
        Flags = flags;
    }

    public HttpStaticResponseTransactionDiagnosticFormatStatus Status { get; }

    public HttpStaticResponseTransactionStatus TransactionStatus { get; }

    public int BytesWritten { get; }

    public int RequiredBytes { get; }

    public int AvailableBytes { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == HttpStaticResponseTransactionDiagnosticFormatStatus.Success &&
        RequiredBytes > 0 &&
        BytesWritten == RequiredBytes &&
        RequiredBytes <= AvailableBytes;

    public static HttpStaticResponseTransactionDiagnosticFormatResult Fail(
        HttpStaticResponseTransactionDiagnosticFormatStatus status,
        HttpStaticResponseTransactionStatus transactionStatus,
        int requiredBytes,
        int availableBytes)
    {
        return new HttpStaticResponseTransactionDiagnosticFormatResult(
            status,
            transactionStatus,
            bytesWritten: 0,
            requiredBytes,
            availableBytes);
    }
}

public static class HttpStaticResponseTransactionDiagnosticFormatter
{
    public const bool UsesSharedAsciiTextBuilder = true;
    public const bool UsesCallerProvidedDestination = true;
    public const bool AllocatesManagedStorage = false;
    public const bool OpensLiveSocket = false;
    public const bool StartsKestrel = false;

    public static HttpStaticResponseTransactionDiagnosticFormatResult FormatResult(
        in HttpStaticResponseTransactionResult result,
        Span<byte> destination)
    {
        HttpStaticResponseTransactionDiagnosticFormatStatus validationStatus =
            Validate(in result);
        if (validationStatus != HttpStaticResponseTransactionDiagnosticFormatStatus.Success)
        {
            return HttpStaticResponseTransactionDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                destination.Length);
        }

        int requiredBytes = GetRequiredBytes(in result);
        if (destination.Length < requiredBytes)
        {
            return HttpStaticResponseTransactionDiagnosticFormatResult.Fail(
                HttpStaticResponseTransactionDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        int offset = 0;
        if (!TryAppendBytes("HTTP_STATIC_RESPONSE status="u8, destination, ref offset) ||
            !TryAppendBytes(GetTransactionStatusName(result.Status), destination, ref offset) ||
            !TryAppendBytes(" request="u8, destination, ref offset) ||
            !TryAppendBytes(GetRequestStatusName(result.RequestStatus), destination, ref offset) ||
            !TryAppendBytes(" response="u8, destination, ref offset) ||
            !TryAppendBytes(GetResponseStatusName(result.ResponseStatus), destination, ref offset) ||
            !TryAppendBytes(" method="u8, destination, ref offset) ||
            !TryAppendBytes(GetMethodName(result.Method), destination, ref offset) ||
            !TryAppendBytes(" httpStatus="u8, destination, ref offset) ||
            !TryAppendUInt64Decimal(result.StatusCode, destination, ref offset) ||
            !TryAppendBytes(" targetMatched="u8, destination, ref offset) ||
            !TryAppendBool(result.TargetMatched, destination, ref offset) ||
            !TryAppendBytes(" methodAllowed="u8, destination, ref offset) ||
            !TryAppendBool(result.MethodAllowed, destination, ref offset) ||
            !TryAppendBytes(" connectionClose="u8, destination, ref offset) ||
            !TryAppendBool(result.ConnectionClose, destination, ref offset) ||
            !TryAppendBytes(" targetBytes="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.RequestTargetLength, destination, ref offset) ||
            !TryAppendBytes(" bytesWritten="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.BytesWritten, destination, ref offset) ||
            !TryAppendBytes(" bytesRequired="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.BytesRequired, destination, ref offset) ||
            !TryAppendBytes(" bodyOffset="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.BodyOffset, destination, ref offset) ||
            !TryAppendBytes(" bodyBytes="u8, destination, ref offset) ||
            !TryAppendInt64Decimal(result.BodyLength, destination, ref offset) ||
            !TryAppendBytes(" liveSocket="u8, destination, ref offset) ||
            !TryAppendBool(OpensLiveSocket, destination, ref offset) ||
            !TryAppendBytes(" startsKestrel="u8, destination, ref offset) ||
            !TryAppendBool(StartsKestrel, destination, ref offset))
        {
            return HttpStaticResponseTransactionDiagnosticFormatResult.Fail(
                HttpStaticResponseTransactionDiagnosticFormatStatus.DestinationTooSmall,
                result.Status,
                requiredBytes,
                destination.Length);
        }

        return new HttpStaticResponseTransactionDiagnosticFormatResult(
            HttpStaticResponseTransactionDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: offset,
            requiredBytes,
            availableBytes: destination.Length);
    }

    public static HttpStaticResponseTransactionDiagnosticFormatResult QueryRequiredBytes(
        in HttpStaticResponseTransactionResult result)
    {
        HttpStaticResponseTransactionDiagnosticFormatStatus validationStatus =
            Validate(in result);
        if (validationStatus != HttpStaticResponseTransactionDiagnosticFormatStatus.Success)
        {
            return HttpStaticResponseTransactionDiagnosticFormatResult.Fail(
                validationStatus,
                result.Status,
                requiredBytes: 0,
                availableBytes: 0);
        }

        return new HttpStaticResponseTransactionDiagnosticFormatResult(
            HttpStaticResponseTransactionDiagnosticFormatStatus.Success,
            result.Status,
            bytesWritten: 0,
            requiredBytes: GetRequiredBytes(in result),
            availableBytes: 0);
    }

    private static HttpStaticResponseTransactionDiagnosticFormatStatus Validate(
        in HttpStaticResponseTransactionResult result)
    {
        if (!result.IsInitialized ||
            !IsKnownTransactionStatus(result.Status) ||
            !IsKnownRequestStatus(result.RequestStatus) ||
            !IsKnownResponseStatus(result.ResponseStatus) ||
            !IsKnownMethod(result.Method) ||
            result.RequestTargetOffset < 0 ||
            result.RequestTargetLength < 0 ||
            result.BytesWritten < 0 ||
            result.BytesRequired < 0 ||
            result.BodyOffset < 0 ||
            result.BodyLength < 0)
        {
            return HttpStaticResponseTransactionDiagnosticFormatStatus.InvalidResult;
        }

        if (result.Status == HttpStaticResponseTransactionStatus.Success)
        {
            if (!result.Succeeded ||
                result.RequestStatus != HttpRequestParseStatus.Success ||
                result.ResponseStatus != HttpResponseWriterStatus.Success ||
                result.StatusCode == 0 ||
                result.BytesWritten <= 0 ||
                result.BytesRequired != result.BytesWritten)
            {
                return HttpStaticResponseTransactionDiagnosticFormatStatus.InvalidResult;
            }
        }

        return HttpStaticResponseTransactionDiagnosticFormatStatus.Success;
    }

    private static int GetRequiredBytes(in HttpStaticResponseTransactionResult result)
    {
        return "HTTP_STATIC_RESPONSE status="u8.Length
            + GetTransactionStatusName(result.Status).Length
            + " request="u8.Length
            + GetRequestStatusName(result.RequestStatus).Length
            + " response="u8.Length
            + GetResponseStatusName(result.ResponseStatus).Length
            + " method="u8.Length
            + GetMethodName(result.Method).Length
            + " httpStatus="u8.Length
            + AsciiDecimalFormatter.CountUInt32Digits(result.StatusCode)
            + " targetMatched="u8.Length
            + GetBoolName(result.TargetMatched).Length
            + " methodAllowed="u8.Length
            + GetBoolName(result.MethodAllowed).Length
            + " connectionClose="u8.Length
            + GetBoolName(result.ConnectionClose).Length
            + " targetBytes="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.RequestTargetLength)
            + " bytesWritten="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.BytesWritten)
            + " bytesRequired="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.BytesRequired)
            + " bodyOffset="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.BodyOffset)
            + " bodyBytes="u8.Length
            + AsciiDecimalFormatter.CountInt32Digits(result.BodyLength)
            + " liveSocket="u8.Length
            + GetBoolName(OpensLiveSocket).Length
            + " startsKestrel="u8.Length
            + GetBoolName(StartsKestrel).Length;
    }

    private static bool IsKnownTransactionStatus(HttpStaticResponseTransactionStatus status)
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

    private static ReadOnlySpan<byte> GetTransactionStatusName(
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
