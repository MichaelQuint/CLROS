namespace OS.Net;
public readonly struct HttpClientGetTransactionResult
{
    private HttpClientGetTransactionResult(HttpClientGetTransactionStatus status, HttpClientGetTransactionState state, HttpRequestBuilderStatus requestStatus, HttpResponseParseStatus responseStatus, int bytesWritten, int bytesRequired, byte protocolMajor, byte protocolMinor, ushort statusCode, int headerLength, bool hasContentLength, int contentLength, bool connectionClose, int bodyOffset, int bodyLength)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        RequestStatus = requestStatus;
        ResponseStatus = responseStatus;
        BytesWritten = bytesWritten;
        BytesRequired = bytesRequired;
        ProtocolMajor = protocolMajor;
        ProtocolMinor = protocolMinor;
        StatusCode = statusCode;
        HeaderLength = headerLength;
        HasContentLength = hasContentLength;
        ContentLength = contentLength;
        ConnectionClose = connectionClose;
        BodyOffset = bodyOffset;
        BodyLength = bodyLength;
    }

    public HttpClientGetTransactionStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == HttpClientGetTransactionStatus.Success;
    public HttpClientGetTransactionState State { get; }
    public HttpRequestBuilderStatus RequestStatus { get; }
    public HttpResponseParseStatus ResponseStatus { get; }
    public int BytesWritten { get; }
    public int BytesRequired { get; }
    public byte ProtocolMajor { get; }
    public byte ProtocolMinor { get; }
    public ushort StatusCode { get; }
    public int HeaderLength { get; }
    public bool HasContentLength { get; }
    public int ContentLength { get; }
    public bool ConnectionClose { get; }
    public int BodyOffset { get; }
    public int BodyLength { get; }

    public static HttpClientGetTransactionResult RequestSuccess(HttpClientGetTransactionState state, HttpRequestBuilderStatus requestStatus, int bytesWritten, int bytesRequired)
    {
        return new HttpClientGetTransactionResult(HttpClientGetTransactionStatus.Success, state, requestStatus, HttpResponseParseStatus.Success, bytesWritten, bytesRequired, 0, 0, 0, 0, false, 0, false, 0, 0);
    }

    public static HttpClientGetTransactionResult ResponseSuccess(HttpClientGetTransactionState state, HttpResponseParseResult response)
    {
        return new HttpClientGetTransactionResult(HttpClientGetTransactionStatus.Success, state, HttpRequestBuilderStatus.Success, response.Status, 0, 0, response.ProtocolMajor, response.ProtocolMinor, response.StatusCode, response.HeaderLength, response.HasContentLength, response.ContentLength, response.ConnectionClose, response.BodyOffset, response.BodyLength);
    }

    public static HttpClientGetTransactionResult RequestFail(HttpClientGetTransactionStatus status, HttpClientGetTransactionState state, HttpRequestBuilderResult request)
    {
        return new HttpClientGetTransactionResult(status, state, request.Status, HttpResponseParseStatus.Success, request.BytesWritten, request.BytesRequired, 0, 0, 0, 0, false, 0, false, 0, 0);
    }

    public static HttpClientGetTransactionResult ResponseFail(HttpClientGetTransactionStatus status, HttpClientGetTransactionState state, HttpResponseParseStatus responseStatus)
    {
        return new HttpClientGetTransactionResult(status, state, HttpRequestBuilderStatus.Success, responseStatus, 0, 0, 0, 0, 0, 0, false, 0, false, 0, 0);
    }

    public static HttpClientGetTransactionResult InvalidState(HttpClientGetTransactionState state)
    {
        return new HttpClientGetTransactionResult(HttpClientGetTransactionStatus.InvalidState, state, HttpRequestBuilderStatus.Success, HttpResponseParseStatus.Success, 0, 0, 0, 0, 0, 0, false, 0, false, 0, 0);
    }
}
