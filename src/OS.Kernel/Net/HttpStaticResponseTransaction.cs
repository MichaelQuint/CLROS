namespace OS.Net;

public enum HttpStaticResponseTransactionStatus
{
    Success = 0,
    InvalidRoute = 1,
    RequestParseFailed = 2,
    ResponseWriteFailed = 3
}

public readonly struct HttpStaticResponseTransactionResult
{
    private HttpStaticResponseTransactionResult(
        HttpStaticResponseTransactionStatus status,
        HttpRequestParseStatus requestStatus,
        HttpResponseWriterStatus responseStatus,
        HttpRequestMethod method,
        ushort statusCode,
        bool targetMatched,
        bool methodAllowed,
        bool connectionClose,
        int requestTargetOffset,
        int requestTargetLength,
        int bytesWritten,
        int bytesRequired,
        int bodyOffset,
        int bodyLength)
    {
        IsInitialized = true;
        Status = status;
        RequestStatus = requestStatus;
        ResponseStatus = responseStatus;
        Method = method;
        StatusCode = statusCode;
        TargetMatched = targetMatched;
        MethodAllowed = methodAllowed;
        ConnectionClose = connectionClose;
        RequestTargetOffset = requestTargetOffset;
        RequestTargetLength = requestTargetLength;
        BytesWritten = bytesWritten;
        BytesRequired = bytesRequired;
        BodyOffset = bodyOffset;
        BodyLength = bodyLength;
    }

    public bool IsInitialized { get; }

    public HttpStaticResponseTransactionStatus Status { get; }

    public bool Succeeded => IsInitialized && Status == HttpStaticResponseTransactionStatus.Success;

    public HttpRequestParseStatus RequestStatus { get; }

    public HttpResponseWriterStatus ResponseStatus { get; }

    public HttpRequestMethod Method { get; }

    public ushort StatusCode { get; }

    public bool TargetMatched { get; }

    public bool MethodAllowed { get; }

    public bool ConnectionClose { get; }

    public int RequestTargetOffset { get; }

    public int RequestTargetLength { get; }

    public int BytesWritten { get; }

    public int BytesRequired { get; }

    public int BodyOffset { get; }

    public int BodyLength { get; }

    public static HttpStaticResponseTransactionResult Success(
        HttpRequestParseResult request,
        HttpResponseWriterResult response,
        ushort statusCode,
        bool targetMatched,
        bool methodAllowed,
        bool connectionClose)
    {
        return new HttpStaticResponseTransactionResult(
            HttpStaticResponseTransactionStatus.Success,
            request.Status,
            response.Status,
            request.Method,
            statusCode,
            targetMatched,
            methodAllowed,
            connectionClose,
            request.TargetOffset,
            request.TargetLength,
            response.BytesWritten,
            response.BytesRequired,
            response.BodyOffset,
            response.BodyLength);
    }

    public static HttpStaticResponseTransactionResult Fail(
        HttpStaticResponseTransactionStatus status,
        HttpRequestParseStatus requestStatus,
        HttpResponseWriterStatus responseStatus,
        HttpRequestMethod method,
        ushort statusCode,
        bool targetMatched,
        bool methodAllowed,
        bool connectionClose,
        int requestTargetOffset,
        int requestTargetLength,
        int bytesRequired)
    {
        return new HttpStaticResponseTransactionResult(
            status,
            requestStatus,
            responseStatus,
            method,
            statusCode,
            targetMatched,
            methodAllowed,
            connectionClose,
            requestTargetOffset,
            requestTargetLength,
            0,
            bytesRequired,
            0,
            0);
    }
}

public static class HttpStaticResponseTransaction
{
    public static HttpStaticResponseTransactionStatus TryHandle(
        ReadOnlySpan<byte> request,
        ReadOnlySpan<byte> routeTarget,
        ReadOnlySpan<byte> successBody,
        ReadOnlySpan<byte> notFoundBody,
        ReadOnlySpan<byte> methodNotAllowedBody,
        ReadOnlySpan<byte> contentType,
        Span<byte> responseDestination,
        out HttpStaticResponseTransactionResult result)
    {
        result = default;

        if (!IsValidRouteTarget(routeTarget))
        {
            result = HttpStaticResponseTransactionResult.Fail(
                HttpStaticResponseTransactionStatus.InvalidRoute,
                HttpRequestParseStatus.Success,
                HttpResponseWriterStatus.Success,
                HttpRequestMethod.Unknown,
                0,
                targetMatched: false,
                methodAllowed: false,
                connectionClose: true,
                requestTargetOffset: 0,
                requestTargetLength: 0,
                bytesRequired: 0);
            return result.Status;
        }

        HttpRequestParseStatus requestStatus = HttpRequestParser.TryParse(
            request,
            out HttpRequestParseResult requestResult);
        if (requestStatus != HttpRequestParseStatus.Success || !requestResult.Succeeded)
        {
            result = HttpStaticResponseTransactionResult.Fail(
                HttpStaticResponseTransactionStatus.RequestParseFailed,
                requestStatus,
                HttpResponseWriterStatus.Success,
                requestResult.Method,
                0,
                targetMatched: false,
                methodAllowed: false,
                connectionClose: true,
                requestResult.TargetOffset,
                requestResult.TargetLength,
                bytesRequired: 0);
            return result.Status;
        }

        bool methodAllowed = requestResult.Method == HttpRequestMethod.Get;
        bool targetMatched = methodAllowed
            && OS.Std.MemoryExtensions.SequenceEqual(
                request.Slice(requestResult.TargetOffset, requestResult.TargetLength),
                routeTarget);

        ushort statusCode;
        ReadOnlySpan<byte> reasonPhrase;
        ReadOnlySpan<byte> responseBody;

        if (!methodAllowed)
        {
            statusCode = 405;
            reasonPhrase = "Method Not Allowed"u8;
            responseBody = methodNotAllowedBody;
        }
        else if (targetMatched)
        {
            statusCode = 200;
            reasonPhrase = "OK"u8;
            responseBody = successBody;
        }
        else
        {
            statusCode = 404;
            reasonPhrase = "Not Found"u8;
            responseBody = notFoundBody;
        }

        bool connectionClose = requestResult.ConnectionClose || !methodAllowed || !targetMatched;
        HttpResponseWriterStatus responseStatus = HttpResponseWriter.TryWrite(
            statusCode,
            reasonPhrase,
            contentType,
            responseBody,
            connectionClose,
            responseDestination,
            out HttpResponseWriterResult responseResult);
        if (responseStatus != HttpResponseWriterStatus.Success || !responseResult.Succeeded)
        {
            result = HttpStaticResponseTransactionResult.Fail(
                HttpStaticResponseTransactionStatus.ResponseWriteFailed,
                requestResult.Status,
                responseStatus,
                requestResult.Method,
                statusCode,
                targetMatched,
                methodAllowed,
                connectionClose,
                requestResult.TargetOffset,
                requestResult.TargetLength,
                responseResult.BytesRequired);
            return result.Status;
        }

        result = HttpStaticResponseTransactionResult.Success(
            requestResult,
            responseResult,
            statusCode,
            targetMatched,
            methodAllowed,
            connectionClose);
        return result.Status;
    }

    private static bool IsValidRouteTarget(ReadOnlySpan<byte> target)
    {
        if (target.Length == 0 || target[0] != (byte)'/')
        {
            return false;
        }

        for (int i = 0; i < target.Length; i++)
        {
            byte b = target[i];
            if (b < 0x21 || b > 0x7e || b == (byte)'\r' || b == (byte)'\n')
            {
                return false;
            }
        }

        return true;
    }

}
