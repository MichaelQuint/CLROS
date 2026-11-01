namespace OS.Net;
public readonly struct HttpStaticResponseTransactionResult
{
    private HttpStaticResponseTransactionResult(HttpStaticResponseTransactionStatus status, HttpRequestParseStatus requestStatus, HttpResponseWriterStatus responseStatus, HttpRequestMethod method, ushort statusCode, bool targetMatched, bool methodAllowed, bool connectionClose, int requestTargetOffset, int requestTargetLength, int bytesWritten, int bytesRequired, int bodyOffset, int bodyLength)
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

    public static HttpStaticResponseTransactionResult Success(HttpRequestParseResult request, HttpResponseWriterResult response, ushort statusCode, bool targetMatched, bool methodAllowed, bool connectionClose)
    {
        return new HttpStaticResponseTransactionResult(HttpStaticResponseTransactionStatus.Success, request.Status, response.Status, request.Method, statusCode, targetMatched, methodAllowed, connectionClose, request.TargetOffset, request.TargetLength, response.BytesWritten, response.BytesRequired, response.BodyOffset, response.BodyLength);
    }

    public static HttpStaticResponseTransactionResult Fail(HttpStaticResponseTransactionStatus status, HttpRequestParseStatus requestStatus, HttpResponseWriterStatus responseStatus, HttpRequestMethod method, ushort statusCode, bool targetMatched, bool methodAllowed, bool connectionClose, int requestTargetOffset, int requestTargetLength, int bytesRequired)
    {
        return new HttpStaticResponseTransactionResult(status, requestStatus, responseStatus, method, statusCode, targetMatched, methodAllowed, connectionClose, requestTargetOffset, requestTargetLength, 0, bytesRequired, 0, 0);
    }
}
