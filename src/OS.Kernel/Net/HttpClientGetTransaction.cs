namespace OS.Net;

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

    public HttpClientGetTransactionState State
    {
        get; private set;
    }
    public int RequestBytesWritten
    {
        get; private set;
    }
    public byte ProtocolMajor
    {
        get; private set;
    }
    public byte ProtocolMinor
    {
        get; private set;
    }
    public ushort StatusCode
    {
        get; private set;
    }
    public int HeaderLength
    {
        get; private set;
    }
    public bool HasContentLength
    {
        get; private set;
    }
    public int ContentLength
    {
        get; private set;
    }
    public bool ConnectionClose
    {
        get; private set;
    }
    public int BodyOffset
    {
        get; private set;
    }
    public int BodyLength
    {
        get; private set;
    }

    public static HttpClientGetTransaction Create()
    {
        return new HttpClientGetTransaction(HttpClientGetTransactionState.Initial);
    }

    public HttpClientGetTransactionStatus TryWriteRequest(ReadOnlySpan<byte> host, ReadOnlySpan<byte> path, Span<byte> destination, out HttpClientGetTransactionResult result)
    {
        if (State != HttpClientGetTransactionState.Initial)
        {
            result = HttpClientGetTransactionResult.InvalidState(State);
            return result.Status;
        }

        HttpRequestBuilderStatus requestStatus = HttpRequestBuilder.TryWriteGet(host, path, destination, out HttpRequestBuilderResult requestResult);
        if (requestStatus != HttpRequestBuilderStatus.Success)
        {
            HttpClientGetTransactionStatus status = MapRequestStatus(requestStatus);
            result = HttpClientGetTransactionResult.RequestFail(status, State, requestResult);
            return status;
        }

        RequestBytesWritten = requestResult.BytesWritten;
        State = HttpClientGetTransactionState.RequestWritten;
        result = HttpClientGetTransactionResult.RequestSuccess(State, requestStatus, requestResult.BytesWritten, requestResult.BytesRequired);
        return result.Status;
    }

    public HttpClientGetTransactionStatus ReceiveResponse(ReadOnlySpan<byte> response, out HttpClientGetTransactionResult result)
    {
        if (State != HttpClientGetTransactionState.RequestWritten)
        {
            result = HttpClientGetTransactionResult.InvalidState(State);
            return result.Status;
        }

        HttpResponseParseStatus responseStatus = HttpResponseParser.TryParse(response, out HttpResponseParseResult responseResult);
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
