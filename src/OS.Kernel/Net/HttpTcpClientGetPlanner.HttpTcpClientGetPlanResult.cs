namespace OS.Net;
using OS.Std.Binary;

public readonly struct HttpTcpClientGetPlanResult
{
    private HttpTcpClientGetPlanResult(HttpTcpClientGetPlanStatus status, HttpTcpClientGetPlannerState state, TcpIpv4FramePlanStatus tcpFrameStatus, TcpActiveConnectStatus tcpStatus, TcpEstablishedStreamStatus streamStatus, HttpClientGetTransactionStatus httpStatus, HttpRequestBuilderStatus requestStatus, HttpResponseParseStatus responseStatus, PacketParseStatus packetStatus, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int bytesWritten, int tcpSegmentOffset, int tcpSegmentLength, int requestBytesWritten, int responseBytesRead, ushort httpStatusCode, int httpBodyOffset, int httpBodyLength, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        IsInitialized = true;
        Status = status;
        State = state;
        TcpFrameStatus = tcpFrameStatus;
        TcpStatus = tcpStatus;
        StreamStatus = streamStatus;
        HttpStatus = httpStatus;
        RequestStatus = requestStatus;
        ResponseStatus = responseStatus;
        PacketStatus = packetStatus;
        EthernetHeader = ethernetHeader;
        Ipv4Header = ipv4Header;
        TcpHeader = tcpHeader;
        BytesWritten = bytesWritten;
        TcpSegmentOffset = tcpSegmentOffset;
        TcpSegmentLength = tcpSegmentLength;
        RequestBytesWritten = requestBytesWritten;
        ResponseBytesRead = responseBytesRead;
        HttpStatusCode = httpStatusCode;
        HttpBodyOffset = httpBodyOffset;
        HttpBodyLength = httpBodyLength;
        LocalNextSequenceNumber = localNextSequenceNumber;
        RemoteNextSequenceNumber = remoteNextSequenceNumber;
    }

    public HttpTcpClientGetPlanStatus Status { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == HttpTcpClientGetPlanStatus.Success;
    public HttpTcpClientGetPlannerState State { get; }
    public TcpIpv4FramePlanStatus TcpFrameStatus { get; }
    public TcpActiveConnectStatus TcpStatus { get; }
    public TcpEstablishedStreamStatus StreamStatus { get; }
    public HttpClientGetTransactionStatus HttpStatus { get; }
    public HttpRequestBuilderStatus RequestStatus { get; }
    public HttpResponseParseStatus ResponseStatus { get; }
    public PacketParseStatus PacketStatus { get; }
    public EthernetHeader EthernetHeader { get; }
    public Ipv4Header Ipv4Header { get; }
    public TcpHeader TcpHeader { get; }
    public int BytesWritten { get; }
    public int TcpSegmentOffset { get; }
    public int TcpSegmentLength { get; }
    public int RequestBytesWritten { get; }
    public int ResponseBytesRead { get; }
    public ushort HttpStatusCode { get; }
    public int HttpBodyOffset { get; }
    public int HttpBodyLength { get; }
    public uint LocalNextSequenceNumber { get; }
    public uint RemoteNextSequenceNumber { get; }

    public static HttpTcpClientGetPlanResult FromTcpFrame(HttpTcpClientGetPlanStatus status, HttpTcpClientGetPlannerState state, in TcpIpv4FramePlanResult tcpResult)
    {
        return new HttpTcpClientGetPlanResult(status, state, tcpResult.Status, tcpResult.TcpStatus, TcpEstablishedStreamStatus.Success, HttpClientGetTransactionStatus.Success, HttpRequestBuilderStatus.Success, HttpResponseParseStatus.Success, tcpResult.PacketStatus, tcpResult.EthernetHeader, tcpResult.Ipv4Header, tcpResult.TcpHeader, tcpResult.BytesWritten, tcpResult.TcpSegmentOffset, tcpResult.TcpSegmentLength, requestBytesWritten: 0, responseBytesRead: 0, httpStatusCode: 0, httpBodyOffset: 0, httpBodyLength: 0, tcpResult.LocalNextSequenceNumber, tcpResult.RemoteNextSequenceNumber);
    }

    public static HttpTcpClientGetPlanResult RequestSuccess(HttpTcpClientGetPlannerState state, in TcpEstablishedStreamSendResult streamResult, in HttpClientGetTransactionResult httpResult, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int frameBytesWritten, int tcpSegmentOffset)
    {
        return new HttpTcpClientGetPlanResult(HttpTcpClientGetPlanStatus.Success, state, TcpIpv4FramePlanStatus.Success, TcpActiveConnectStatus.Success, streamResult.Status, httpResult.Status, httpResult.RequestStatus, httpResult.ResponseStatus, streamResult.PacketStatus, ethernetHeader, ipv4Header, tcpHeader, frameBytesWritten, tcpSegmentOffset, streamResult.SegmentBytesWritten, httpResult.BytesWritten, responseBytesRead: 0, httpStatusCode: 0, httpBodyOffset: 0, httpBodyLength: 0, streamResult.LocalNextSequenceNumber, streamResult.RemoteNextSequenceNumber);
    }

    public static HttpTcpClientGetPlanResult ResponseSuccess(HttpTcpClientGetPlannerState state, in TcpEstablishedStreamReceiveResult receiveResult, in TcpEstablishedStreamReadResult readResult, in HttpClientGetTransactionResult httpResult, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int tcpSegmentOffset, int tcpSegmentLength)
    {
        return new HttpTcpClientGetPlanResult(HttpTcpClientGetPlanStatus.Success, state, TcpIpv4FramePlanStatus.Success, TcpActiveConnectStatus.Success, receiveResult.Status, httpResult.Status, httpResult.RequestStatus, httpResult.ResponseStatus, receiveResult.PacketStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten: 0, tcpSegmentOffset, tcpSegmentLength, requestBytesWritten: 0, readResult.BytesRead, httpResult.StatusCode, httpResult.BodyOffset, httpResult.BodyLength, receiveResult.LocalNextSequenceNumber, receiveResult.RemoteNextSequenceNumber);
    }

    public static HttpTcpClientGetPlanResult Fail(HttpTcpClientGetPlanStatus status, HttpTcpClientGetPlannerState state, TcpIpv4FramePlanStatus tcpFrameStatus, TcpActiveConnectStatus tcpStatus, TcpEstablishedStreamStatus streamStatus, HttpClientGetTransactionStatus httpStatus, HttpRequestBuilderStatus requestStatus, HttpResponseParseStatus responseStatus, PacketParseStatus packetStatus, EthernetHeader ethernetHeader, Ipv4Header ipv4Header, TcpHeader tcpHeader, int tcpSegmentOffset, int tcpSegmentLength, int requestBytesWritten, int responseBytesRead, uint localNextSequenceNumber, uint remoteNextSequenceNumber)
    {
        return new HttpTcpClientGetPlanResult(status, state, tcpFrameStatus, tcpStatus, streamStatus, httpStatus, requestStatus, responseStatus, packetStatus, ethernetHeader, ipv4Header, tcpHeader, bytesWritten: 0, tcpSegmentOffset, tcpSegmentLength, requestBytesWritten, responseBytesRead, httpStatusCode: 0, httpBodyOffset: 0, httpBodyLength: 0, localNextSequenceNumber, remoteNextSequenceNumber);
    }
}
