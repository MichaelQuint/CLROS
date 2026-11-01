namespace OS.Net;
public readonly struct HttpStaticTcpResponsePlanResult
{
    private HttpStaticTcpResponsePlanResult(HttpStaticTcpResponsePlanStatus status, TcpReceiveDispatchStatus tcpDispatchStatus, TcpReceiveDispatchAction tcpAction, HttpStaticResponseTransactionStatus httpStatus, HttpRequestParseStatus requestStatus, HttpResponseWriterStatus responseStatus, HttpRequestMethod method, ushort httpStatusCode, int requestPayloadOffset, int requestPayloadLength, int responseBytesWritten, int responseBytesRequired, int responseBodyOffset, int responseBodyLength)
    {
        IsInitialized = true;
        Status = status;
        TcpDispatchStatus = tcpDispatchStatus;
        TcpAction = tcpAction;
        HttpStatus = httpStatus;
        RequestStatus = requestStatus;
        ResponseStatus = responseStatus;
        Method = method;
        HttpStatusCode = httpStatusCode;
        RequestPayloadOffset = requestPayloadOffset;
        RequestPayloadLength = requestPayloadLength;
        ResponseBytesWritten = responseBytesWritten;
        ResponseBytesRequired = responseBytesRequired;
        ResponseBodyOffset = responseBodyOffset;
        ResponseBodyLength = responseBodyLength;
    }

    public bool IsInitialized { get; }
    public HttpStaticTcpResponsePlanStatus Status { get; }
    public bool Succeeded => IsInitialized && Status == HttpStaticTcpResponsePlanStatus.Success;
    public TcpReceiveDispatchStatus TcpDispatchStatus { get; }
    public TcpReceiveDispatchAction TcpAction { get; }
    public HttpStaticResponseTransactionStatus HttpStatus { get; }
    public HttpRequestParseStatus RequestStatus { get; }
    public HttpResponseWriterStatus ResponseStatus { get; }
    public HttpRequestMethod Method { get; }
    public ushort HttpStatusCode { get; }
    public int RequestPayloadOffset { get; }
    public int RequestPayloadLength { get; }
    public int ResponseBytesWritten { get; }
    public int ResponseBytesRequired { get; }
    public int ResponseBodyOffset { get; }
    public int ResponseBodyLength { get; }

    public static HttpStaticTcpResponsePlanResult Success(TcpReceiveDispatchResult tcpDispatch, int requestPayloadOffset, HttpStaticResponseTransactionResult http)
    {
        return new HttpStaticTcpResponsePlanResult(HttpStaticTcpResponsePlanStatus.Success, tcpDispatch.Status, tcpDispatch.Action, http.Status, http.RequestStatus, http.ResponseStatus, http.Method, http.StatusCode, requestPayloadOffset, tcpDispatch.PayloadBytes, http.BytesWritten, http.BytesRequired, http.BodyOffset, http.BodyLength);
    }

    public static HttpStaticTcpResponsePlanResult Fail(HttpStaticTcpResponsePlanStatus status, TcpReceiveDispatchResult tcpDispatch, HttpStaticResponseTransactionResult http, int requestPayloadOffset, int requestPayloadLength)
    {
        return new HttpStaticTcpResponsePlanResult(status, tcpDispatch.Status, tcpDispatch.Action, http.Status, http.RequestStatus, http.ResponseStatus, http.Method, http.StatusCode, requestPayloadOffset, requestPayloadLength, http.BytesWritten, http.BytesRequired, http.BodyOffset, http.BodyLength);
    }
}
