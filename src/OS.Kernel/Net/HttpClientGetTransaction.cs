namespace OS.Net;

public enum HttpClientGetTransactionState
{
    Initial = 0,
    RequestWritten = 1,
    ResponseReceived = 2,
    Failed = 3
}

public enum HttpClientGetTransactionStatus
{
    Success = 0,
    InvalidState = 1,
    RequestBufferTooSmall = 2,
    EmptyHost = 3,
    InvalidHost = 4,
    EmptyPath = 5,
    MissingLeadingSlash = 6,
    InvalidPath = 7,
    RequestWriteFailed = 8,
    MalformedResponse = 9,
    UnsupportedTransferEncoding = 10,
    TruncatedBody = 11
}

public readonly struct HttpClientGetTransactionResult
{
    private HttpClientGetTransactionResult(
        HttpClientGetTransactionStatus status,
        HttpClientGetTransactionState state,
        HttpRequestBuilderStatus requestStatus,
        HttpResponseParseStatus responseStatus,
        int bytesWritten,
        int bytesRequired,
        byte protocolMajor,
        byte protocolMinor,
        ushort statusCode,
        int headerLength,
        bool hasContentLength,
        int contentLength,
        bool connectionClose,
        int bodyOffset,
        int bodyLength)
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

    public static HttpClientGetTransactionResult RequestSuccess(
        HttpClientGetTransactionState state,
        HttpRequestBuilderStatus requestStatus,
        int bytesWritten,
        int bytesRequired)
    {
        return new HttpClientGetTransactionResult(
            HttpClientGetTransactionStatus.Success,
            state,
            requestStatus,
            HttpResponseParseStatus.Success,
            bytesWritten,
            bytesRequired,
            0,
            0,
            0,
            0,
            false,
            0,
            false,
            0,
            0);
    }

    public static HttpClientGetTransactionResult ResponseSuccess(
        HttpClientGetTransactionState state,
        HttpResponseParseResult response)
    {
        return new HttpClientGetTransactionResult(
            HttpClientGetTransactionStatus.Success,
            state,
            HttpRequestBuilderStatus.Success,
            response.Status,
            0,
            0,
            response.ProtocolMajor,
            response.ProtocolMinor,
            response.StatusCode,
            response.HeaderLength,
            response.HasContentLength,
            response.ContentLength,
            response.ConnectionClose,
            response.BodyOffset,
            response.BodyLength);
    }

    public static HttpClientGetTransactionResult RequestFail(
        HttpClientGetTransactionStatus status,
        HttpClientGetTransactionState state,
        HttpRequestBuilderResult request)
    {
        return new HttpClientGetTransactionResult(
            status,
            state,
            request.Status,
            HttpResponseParseStatus.Success,
            request.BytesWritten,
            request.BytesRequired,
            0,
            0,
            0,
            0,
            false,
            0,
            false,
            0,
            0);
    }

    public static HttpClientGetTransactionResult ResponseFail(
        HttpClientGetTransactionStatus status,
        HttpClientGetTransactionState state,
        HttpResponseParseStatus responseStatus)
    {
        return new HttpClientGetTransactionResult(
            status,
            state,
            HttpRequestBuilderStatus.Success,
            responseStatus,
            0,
            0,
            0,
            0,
            0,
            0,
            false,
            0,
            false,
            0,
            0);
    }

    public static HttpClientGetTransactionResult InvalidState(HttpClientGetTransactionState state)
    {
        return new HttpClientGetTransactionResult(
            HttpClientGetTransactionStatus.InvalidState,
            state,
            HttpRequestBuilderStatus.Success,
            HttpResponseParseStatus.Success,
            0,
            0,
            0,
            0,
            0,
            0,
            false,
            0,
            false,
            0,
            0);
    }
}

public struct HttpClientGetTransaction
{
    private HttpClientGetTransaction(HttpClientGetTransactionState state)
    {
        State = state;
        RequestBytesWritten = 0;
        ProtocolMajor = 0;
        ProtocolMinor = 0;
        StatusCode = 0;
        HeaderLength = 0;
        HasContentLength = false;
        ContentLength = 0;
        ConnectionClose = false;
        BodyOffset = 0;
        BodyLength = 0;
    }

    public HttpClientGetTransactionState State { get; private set; }

    public int RequestBytesWritten { get; private set; }

    public byte ProtocolMajor { get; private set; }

    public byte ProtocolMinor { get; private set; }

    public ushort StatusCode { get; private set; }

    public int HeaderLength { get; private set; }

    public bool HasContentLength { get; private set; }

    public int ContentLength { get; private set; }

    public bool ConnectionClose { get; private set; }

    public int BodyOffset { get; private set; }

    public int BodyLength { get; private set; }

    public static HttpClientGetTransaction Create()
    {
        return new HttpClientGetTransaction(HttpClientGetTransactionState.Initial);
    }

    public HttpClientGetTransactionStatus TryWriteRequest(
        ReadOnlySpan<byte> host,
        ReadOnlySpan<byte> path,
        Span<byte> destination,
        out HttpClientGetTransactionResult result)
    {
        if (State != HttpClientGetTransactionState.Initial)
        {
            result = HttpClientGetTransactionResult.InvalidState(State);
            return result.Status;
        }

        HttpRequestBuilderStatus requestStatus = HttpRequestBuilder.TryWriteGet(
            host,
            path,
            destination,
            out HttpRequestBuilderResult requestResult);
        if (requestStatus != HttpRequestBuilderStatus.Success)
        {
            HttpClientGetTransactionStatus status = MapRequestStatus(requestStatus);
            result = HttpClientGetTransactionResult.RequestFail(status, State, requestResult);
            return status;
        }

        RequestBytesWritten = requestResult.BytesWritten;
        State = HttpClientGetTransactionState.RequestWritten;
        result = HttpClientGetTransactionResult.RequestSuccess(
            State,
            requestStatus,
            requestResult.BytesWritten,
            requestResult.BytesRequired);
        return result.Status;
    }

    public HttpClientGetTransactionStatus ReceiveResponse(
        ReadOnlySpan<byte> response,
        out HttpClientGetTransactionResult result)
    {
        if (State != HttpClientGetTransactionState.RequestWritten)
        {
            result = HttpClientGetTransactionResult.InvalidState(State);
            return result.Status;
        }

        HttpResponseParseStatus responseStatus = HttpResponseParser.TryParse(
            response,
            out HttpResponseParseResult responseResult);
        if (responseStatus != HttpResponseParseStatus.Success)
        {
            State = HttpClientGetTransactionState.Failed;
            HttpClientGetTransactionStatus status = MapResponseStatus(responseStatus);
            result = HttpClientGetTransactionResult.ResponseFail(status, State, responseStatus);
            return status;
        }

        ProtocolMajor = responseResult.ProtocolMajor;
        ProtocolMinor = responseResult.ProtocolMinor;
        StatusCode = responseResult.StatusCode;
        HeaderLength = responseResult.HeaderLength;
        HasContentLength = responseResult.HasContentLength;
        ContentLength = responseResult.ContentLength;
        ConnectionClose = responseResult.ConnectionClose;
        BodyOffset = responseResult.BodyOffset;
        BodyLength = responseResult.BodyLength;
        State = HttpClientGetTransactionState.ResponseReceived;

        result = HttpClientGetTransactionResult.ResponseSuccess(State, responseResult);
        return result.Status;
    }

    private static HttpClientGetTransactionStatus MapRequestStatus(HttpRequestBuilderStatus status)
    {
        return status switch
        {
            HttpRequestBuilderStatus.DestinationTooSmall => HttpClientGetTransactionStatus.RequestBufferTooSmall,
            HttpRequestBuilderStatus.EmptyHost => HttpClientGetTransactionStatus.EmptyHost,
            HttpRequestBuilderStatus.InvalidHost => HttpClientGetTransactionStatus.InvalidHost,
            HttpRequestBuilderStatus.EmptyPath => HttpClientGetTransactionStatus.EmptyPath,
            HttpRequestBuilderStatus.MissingLeadingSlash => HttpClientGetTransactionStatus.MissingLeadingSlash,
            HttpRequestBuilderStatus.InvalidPath => HttpClientGetTransactionStatus.InvalidPath,
            _ => HttpClientGetTransactionStatus.RequestWriteFailed
        };
    }

    private static HttpClientGetTransactionStatus MapResponseStatus(HttpResponseParseStatus status)
    {
        return status switch
        {
            HttpResponseParseStatus.UnsupportedTransferEncoding => HttpClientGetTransactionStatus.UnsupportedTransferEncoding,
            HttpResponseParseStatus.BodyTooShort => HttpClientGetTransactionStatus.TruncatedBody,
            _ => HttpClientGetTransactionStatus.MalformedResponse
        };
    }
}
