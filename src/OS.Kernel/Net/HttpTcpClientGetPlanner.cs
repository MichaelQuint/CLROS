namespace OS.Net;

using OS.Std.Binary;

public enum HttpTcpClientGetPlannerState
{
    Initial = 0,
    SynSent = 1,
    Established = 2,
    RequestWritten = 3,
    ResponseReceived = 4,
    Failed = 5
}

public enum HttpTcpClientGetPlanStatus
{
    Success = 0,
    InvalidState = 1,
    RequestBufferTooSmall = 2,
    RequestTooLarge = 3,
    ResponseBufferTooSmall = 4,
    TcpFrameFailed = 5,
    FrameBufferTooSmall = 6,
    FrameWriteFailed = 7,
    FrameParseFailed = 8,
    UnsupportedEtherType = 9,
    UnsupportedIpProtocol = 10,
    WrongLocalMac = 11,
    WrongRemoteMac = 12,
    WrongLocalAddress = 13,
    WrongRemoteAddress = 14,
    TcpSegmentParseFailed = 15,
    WrongTcpPorts = 16,
    InvalidTcpFlags = 17,
    WrongAcknowledgementNumber = 18,
    TcpStreamSendFailed = 19,
    TcpStreamReceiveFailed = 20,
    TcpStreamReadFailed = 21,
    HttpRequestFailed = 22,
    HttpResponseFailed = 23
}

public readonly struct HttpTcpClientGetPlannerInfo
{
    public const int DefaultMaximumRequestPayloadBytes = 1460;

    public HttpTcpClientGetPlannerInfo(
        TcpIpv4FramePlannerInfo tcpInfo,
        int maximumRequestPayloadBytes = DefaultMaximumRequestPayloadBytes)
    {
        TcpInfo = tcpInfo;
        MaximumRequestPayloadBytes = maximumRequestPayloadBytes <= 0
            ? DefaultMaximumRequestPayloadBytes
            : maximumRequestPayloadBytes;
    }

    public TcpIpv4FramePlannerInfo TcpInfo { get; }

    public int MaximumRequestPayloadBytes { get; }
}

public readonly struct HttpTcpClientGetPlanResult
{
    private HttpTcpClientGetPlanResult(
        HttpTcpClientGetPlanStatus status,
        HttpTcpClientGetPlannerState state,
        TcpIpv4FramePlanStatus tcpFrameStatus,
        TcpActiveConnectStatus tcpStatus,
        TcpEstablishedStreamStatus streamStatus,
        HttpClientGetTransactionStatus httpStatus,
        HttpRequestBuilderStatus requestStatus,
        HttpResponseParseStatus responseStatus,
        PacketParseStatus packetStatus,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int bytesWritten,
        int tcpSegmentOffset,
        int tcpSegmentLength,
        int requestBytesWritten,
        int responseBytesRead,
        ushort httpStatusCode,
        int httpBodyOffset,
        int httpBodyLength,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
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

    public static HttpTcpClientGetPlanResult FromTcpFrame(
        HttpTcpClientGetPlanStatus status,
        HttpTcpClientGetPlannerState state,
        in TcpIpv4FramePlanResult tcpResult)
    {
        return new HttpTcpClientGetPlanResult(
            status,
            state,
            tcpResult.Status,
            tcpResult.TcpStatus,
            TcpEstablishedStreamStatus.Success,
            HttpClientGetTransactionStatus.Success,
            HttpRequestBuilderStatus.Success,
            HttpResponseParseStatus.Success,
            tcpResult.PacketStatus,
            tcpResult.EthernetHeader,
            tcpResult.Ipv4Header,
            tcpResult.TcpHeader,
            tcpResult.BytesWritten,
            tcpResult.TcpSegmentOffset,
            tcpResult.TcpSegmentLength,
            requestBytesWritten: 0,
            responseBytesRead: 0,
            httpStatusCode: 0,
            httpBodyOffset: 0,
            httpBodyLength: 0,
            tcpResult.LocalNextSequenceNumber,
            tcpResult.RemoteNextSequenceNumber);
    }

    public static HttpTcpClientGetPlanResult RequestSuccess(
        HttpTcpClientGetPlannerState state,
        in TcpEstablishedStreamSendResult streamResult,
        in HttpClientGetTransactionResult httpResult,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int frameBytesWritten,
        int tcpSegmentOffset)
    {
        return new HttpTcpClientGetPlanResult(
            HttpTcpClientGetPlanStatus.Success,
            state,
            TcpIpv4FramePlanStatus.Success,
            TcpActiveConnectStatus.Success,
            streamResult.Status,
            httpResult.Status,
            httpResult.RequestStatus,
            httpResult.ResponseStatus,
            streamResult.PacketStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            frameBytesWritten,
            tcpSegmentOffset,
            streamResult.SegmentBytesWritten,
            httpResult.BytesWritten,
            responseBytesRead: 0,
            httpStatusCode: 0,
            httpBodyOffset: 0,
            httpBodyLength: 0,
            streamResult.LocalNextSequenceNumber,
            streamResult.RemoteNextSequenceNumber);
    }

    public static HttpTcpClientGetPlanResult ResponseSuccess(
        HttpTcpClientGetPlannerState state,
        in TcpEstablishedStreamReceiveResult receiveResult,
        in TcpEstablishedStreamReadResult readResult,
        in HttpClientGetTransactionResult httpResult,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int tcpSegmentOffset,
        int tcpSegmentLength)
    {
        return new HttpTcpClientGetPlanResult(
            HttpTcpClientGetPlanStatus.Success,
            state,
            TcpIpv4FramePlanStatus.Success,
            TcpActiveConnectStatus.Success,
            receiveResult.Status,
            httpResult.Status,
            httpResult.RequestStatus,
            httpResult.ResponseStatus,
            receiveResult.PacketStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            bytesWritten: 0,
            tcpSegmentOffset,
            tcpSegmentLength,
            requestBytesWritten: 0,
            readResult.BytesRead,
            httpResult.StatusCode,
            httpResult.BodyOffset,
            httpResult.BodyLength,
            receiveResult.LocalNextSequenceNumber,
            receiveResult.RemoteNextSequenceNumber);
    }

    public static HttpTcpClientGetPlanResult Fail(
        HttpTcpClientGetPlanStatus status,
        HttpTcpClientGetPlannerState state,
        TcpIpv4FramePlanStatus tcpFrameStatus,
        TcpActiveConnectStatus tcpStatus,
        TcpEstablishedStreamStatus streamStatus,
        HttpClientGetTransactionStatus httpStatus,
        HttpRequestBuilderStatus requestStatus,
        HttpResponseParseStatus responseStatus,
        PacketParseStatus packetStatus,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int tcpSegmentOffset,
        int tcpSegmentLength,
        int requestBytesWritten,
        int responseBytesRead,
        uint localNextSequenceNumber,
        uint remoteNextSequenceNumber)
    {
        return new HttpTcpClientGetPlanResult(
            status,
            state,
            tcpFrameStatus,
            tcpStatus,
            streamStatus,
            httpStatus,
            requestStatus,
            responseStatus,
            packetStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            bytesWritten: 0,
            tcpSegmentOffset,
            tcpSegmentLength,
            requestBytesWritten,
            responseBytesRead,
            httpStatusCode: 0,
            httpBodyOffset: 0,
            httpBodyLength: 0,
            localNextSequenceNumber,
            remoteNextSequenceNumber);
    }
}

public ref struct HttpTcpClientGetPlanner
{
    public const int TcpSegmentOffset =
        EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength;

    public const int HeaderBytes =
        EthernetHeader.HeaderLength + Ipv4Header.MinHeaderLength + TcpHeader.MinHeaderLength;

    private readonly HttpTcpClientGetPlannerInfo _info;
    private Span<byte> _receiveBuffer;
    private TcpIpv4FramePlanner _tcpPlanner;
    private TcpEstablishedStream _stream;
    private HttpClientGetTransaction _http;
    private HttpTcpClientGetPlannerState _state;
    private bool _streamReady;

    private HttpTcpClientGetPlanner(HttpTcpClientGetPlannerInfo info, Span<byte> receiveBuffer)
    {
        _info = info;
        _receiveBuffer = receiveBuffer;
        _tcpPlanner = TcpIpv4FramePlanner.Create(info.TcpInfo);
        _stream = TcpEstablishedStream.Create(
            info.TcpInfo.LocalPort,
            info.TcpInfo.RemotePort,
            localNextSequenceNumber: 0,
            remoteNextSequenceNumber: 0,
            receiveBuffer);
        _http = HttpClientGetTransaction.Create();
        _state = HttpTcpClientGetPlannerState.Initial;
        _streamReady = false;
    }

    public HttpTcpClientGetPlannerState State => _state;

    public TcpActiveConnectState TcpState => _tcpPlanner.State;

    public HttpClientGetTransactionState HttpState => _http.State;

    public uint LocalNextSequenceNumber => _streamReady
        ? _stream.LocalNextSequenceNumber
        : _tcpPlanner.LocalNextSequenceNumber;

    public uint RemoteNextSequenceNumber => _streamReady
        ? _stream.RemoteNextSequenceNumber
        : _tcpPlanner.RemoteNextSequenceNumber;

    public int RequestBytesWritten => _http.RequestBytesWritten;

    public ushort HttpStatusCode => _http.StatusCode;

    public int HttpBodyOffset => _http.BodyOffset;

    public int HttpBodyLength => _http.BodyLength;

    public static HttpTcpClientGetPlanner Create(
        HttpTcpClientGetPlannerInfo info,
        Span<byte> receiveBuffer)
    {
        return new HttpTcpClientGetPlanner(info, receiveBuffer);
    }

    public HttpTcpClientGetPlanStatus TryWriteSynFrame(
        Span<byte> frame,
        out HttpTcpClientGetPlanResult result)
    {
        TcpIpv4FramePlanStatus tcpStatus = _tcpPlanner.TryWriteSynFrame(
            frame,
            out TcpIpv4FramePlanResult tcpResult);
        HttpTcpClientGetPlanStatus status = MapTcpFrameStatus(tcpStatus);
        if (tcpStatus == TcpIpv4FramePlanStatus.Success)
        {
            _state = HttpTcpClientGetPlannerState.SynSent;
        }

        result = HttpTcpClientGetPlanResult.FromTcpFrame(status, _state, tcpResult);
        return result.Status;
    }

    public HttpTcpClientGetPlanStatus ReceiveSynAckFrame(
        ReadOnlySpan<byte> frame,
        out HttpTcpClientGetPlanResult result)
    {
        TcpIpv4FramePlanStatus tcpStatus = _tcpPlanner.ReceiveSynAckFrame(
            frame,
            out TcpIpv4FramePlanResult tcpResult);
        HttpTcpClientGetPlanStatus status = MapTcpFrameStatus(tcpStatus);
        if (tcpStatus != TcpIpv4FramePlanStatus.Success
            && tcpResult.TcpStatus != TcpActiveConnectStatus.Success)
        {
            _state = HttpTcpClientGetPlannerState.Failed;
        }

        result = HttpTcpClientGetPlanResult.FromTcpFrame(status, _state, tcpResult);
        return result.Status;
    }

    public HttpTcpClientGetPlanStatus TryWriteFinalAckFrame(
        Span<byte> frame,
        out HttpTcpClientGetPlanResult result)
    {
        TcpIpv4FramePlanStatus tcpStatus = _tcpPlanner.TryWriteFinalAckFrame(
            frame,
            out TcpIpv4FramePlanResult tcpResult);
        HttpTcpClientGetPlanStatus status = MapTcpFrameStatus(tcpStatus);
        if (tcpStatus == TcpIpv4FramePlanStatus.Success)
        {
            _stream = TcpEstablishedStream.Create(
                _info.TcpInfo.LocalPort,
                _info.TcpInfo.RemotePort,
                tcpResult.LocalNextSequenceNumber,
                tcpResult.RemoteNextSequenceNumber,
                _receiveBuffer);
            _streamReady = true;
            _state = HttpTcpClientGetPlannerState.Established;
        }

        result = HttpTcpClientGetPlanResult.FromTcpFrame(status, _state, tcpResult);
        return result.Status;
    }

    public HttpTcpClientGetPlanStatus TryWriteRequestFrame(
        ReadOnlySpan<byte> host,
        ReadOnlySpan<byte> path,
        Span<byte> requestScratch,
        Span<byte> frame,
        out HttpTcpClientGetPlanResult result)
    {
        if (!_streamReady || _state != HttpTcpClientGetPlannerState.Established)
        {
            result = CreateFailure(HttpTcpClientGetPlanStatus.InvalidState);
            return result.Status;
        }

        HttpClientGetTransaction pendingHttp = _http;
        HttpClientGetTransactionStatus httpStatus = pendingHttp.TryWriteRequest(
            host,
            path,
            requestScratch,
            out HttpClientGetTransactionResult httpResult);
        if (httpStatus != HttpClientGetTransactionStatus.Success)
        {
            result = CreateFailure(
                MapHttpRequestStatus(httpStatus),
                httpStatus,
                httpResult.RequestStatus,
                httpResult.ResponseStatus,
                requestBytesWritten: httpResult.BytesWritten);
            return result.Status;
        }

        if (httpResult.BytesWritten > _info.MaximumRequestPayloadBytes)
        {
            result = CreateFailure(
                HttpTcpClientGetPlanStatus.RequestTooLarge,
                httpStatus,
                httpResult.RequestStatus,
                httpResult.ResponseStatus,
                requestBytesWritten: httpResult.BytesWritten);
            return result.Status;
        }

        if (frame.Length < HeaderBytes + httpResult.BytesWritten)
        {
            result = CreateFailure(
                HttpTcpClientGetPlanStatus.FrameBufferTooSmall,
                httpStatus,
                httpResult.RequestStatus,
                httpResult.ResponseStatus,
                PacketParseStatus.BufferTooSmall,
                requestBytesWritten: httpResult.BytesWritten);
            return result.Status;
        }

        TcpEstablishedStream pendingStream = _stream;
        Span<byte> tcpSegmentDestination = frame.Slice(TcpSegmentOffset);
        TcpEstablishedStreamStatus streamStatus = pendingStream.TryWritePayloadSegment(
            requestScratch.Slice(0, httpResult.BytesWritten),
            httpResult.BytesWritten,
            tcpSegmentDestination,
            _info.TcpInfo.WindowSize,
            checksum: 0,
            out TcpEstablishedStreamSendResult streamResult);
        if (streamStatus != TcpEstablishedStreamStatus.Success)
        {
            result = CreateFailure(
                HttpTcpClientGetPlanStatus.TcpStreamSendFailed,
                httpStatus,
                httpResult.RequestStatus,
                httpResult.ResponseStatus,
                streamResult.PacketStatus,
                streamStatus,
                requestBytesWritten: httpResult.BytesWritten);
            return result.Status;
        }

        PacketParseStatus checksumStatus = TryWriteTcpChecksum(
            tcpSegmentDestination.Slice(0, streamResult.SegmentBytesWritten),
            _info.TcpInfo.LocalAddress,
            _info.TcpInfo.RemoteAddress);
        if (checksumStatus != PacketParseStatus.Success)
        {
            result = CreateFailure(
                MapFrameWriteStatus(checksumStatus),
                httpStatus,
                httpResult.RequestStatus,
                httpResult.ResponseStatus,
                checksumStatus,
                streamStatus,
                requestBytesWritten: httpResult.BytesWritten);
            return result.Status;
        }

        PacketParseStatus packetStatus = TryWriteFrameHeaders(
            frame,
            streamResult.SegmentBytesWritten,
            out EthernetHeader ethernetHeader,
            out Ipv4Header ipv4Header,
            out int frameBytesWritten);
        if (packetStatus != PacketParseStatus.Success)
        {
            result = CreateFailure(
                MapFrameWriteStatus(packetStatus),
                httpStatus,
                httpResult.RequestStatus,
                httpResult.ResponseStatus,
                packetStatus,
                streamStatus,
                requestBytesWritten: httpResult.BytesWritten);
            return result.Status;
        }

        PacketParseStatus tcpParseStatus = TcpHeader.TryParse(
            tcpSegmentDestination.Slice(0, streamResult.SegmentBytesWritten),
            out TcpHeader tcpHeader,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> _);
        if (tcpParseStatus != PacketParseStatus.Success)
        {
            result = CreateFailure(
                HttpTcpClientGetPlanStatus.TcpSegmentParseFailed,
                httpStatus,
                httpResult.RequestStatus,
                httpResult.ResponseStatus,
                tcpParseStatus,
                streamStatus,
                requestBytesWritten: httpResult.BytesWritten);
            return result.Status;
        }

        _stream = pendingStream;
        _http = pendingHttp;
        _state = HttpTcpClientGetPlannerState.RequestWritten;
        result = HttpTcpClientGetPlanResult.RequestSuccess(
            _state,
            streamResult,
            httpResult,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            frameBytesWritten,
            TcpSegmentOffset);
        return result.Status;
    }

    public HttpTcpClientGetPlanStatus ReceiveResponseFrame(
        ReadOnlySpan<byte> frame,
        Span<byte> responseDestination,
        out HttpTcpClientGetPlanResult result)
    {
        if (!_streamReady || _state != HttpTcpClientGetPlannerState.RequestWritten)
        {
            result = CreateFailure(HttpTcpClientGetPlanStatus.InvalidState);
            return result.Status;
        }

        HttpTcpClientGetPlanStatus frameStatus = TryParseInboundTcpFrame(
            frame,
            out EthernetHeader ethernetHeader,
            out Ipv4Header ipv4Header,
            out TcpHeader tcpHeader,
            out ReadOnlySpan<byte> tcpSegment,
            out int tcpSegmentOffset,
            out int tcpSegmentLength,
            out PacketParseStatus packetStatus);
        if (frameStatus != HttpTcpClientGetPlanStatus.Success)
        {
            TcpIpv4FramePlanStatus tcpFrameStatus =
                packetStatus == PacketParseStatus.BadChecksum
                    ? TcpIpv4FramePlanStatus.BadTcpChecksum
                    : TcpIpv4FramePlanStatus.Success;
            result = CreateFrameFailure(
                frameStatus,
                tcpFrameStatus,
                packetStatus,
                ethernetHeader,
                ipv4Header,
                tcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength);
            return result.Status;
        }

        if (tcpHeader.AcknowledgementNumber != _stream.LocalNextSequenceNumber)
        {
            result = CreateFrameFailure(
                HttpTcpClientGetPlanStatus.WrongAcknowledgementNumber,
                PacketParseStatus.Success,
                ethernetHeader,
                ipv4Header,
                tcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength);
            return result.Status;
        }

        TcpEstablishedStream pendingStream = _stream;
        TcpEstablishedStreamStatus receiveStatus = pendingStream.ReceiveSegment(
            tcpSegment,
            out TcpEstablishedStreamReceiveResult receiveResult);
        if (receiveStatus != TcpEstablishedStreamStatus.Success)
        {
            result = HttpTcpClientGetPlanResult.Fail(
                HttpTcpClientGetPlanStatus.TcpStreamReceiveFailed,
                _state,
                TcpIpv4FramePlanStatus.Success,
                TcpActiveConnectStatus.Success,
                receiveStatus,
                HttpClientGetTransactionStatus.Success,
                HttpRequestBuilderStatus.Success,
                HttpResponseParseStatus.Success,
                receiveResult.PacketStatus,
                ethernetHeader,
                ipv4Header,
                tcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength,
                requestBytesWritten: 0,
                responseBytesRead: 0,
                receiveResult.LocalNextSequenceNumber,
                receiveResult.RemoteNextSequenceNumber);
            return result.Status;
        }

        if (responseDestination.Length < receiveResult.PayloadBytesAccepted)
        {
            result = HttpTcpClientGetPlanResult.Fail(
                HttpTcpClientGetPlanStatus.ResponseBufferTooSmall,
                _state,
                TcpIpv4FramePlanStatus.Success,
                TcpActiveConnectStatus.Success,
                receiveStatus,
                HttpClientGetTransactionStatus.Success,
                HttpRequestBuilderStatus.Success,
                HttpResponseParseStatus.Success,
                receiveResult.PacketStatus,
                ethernetHeader,
                ipv4Header,
                tcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength,
                requestBytesWritten: 0,
                responseBytesRead: 0,
                receiveResult.LocalNextSequenceNumber,
                receiveResult.RemoteNextSequenceNumber);
            return result.Status;
        }

        TcpEstablishedStreamStatus readStatus = pendingStream.Read(
            responseDestination,
            out TcpEstablishedStreamReadResult readResult);
        if (readStatus != TcpEstablishedStreamStatus.Success)
        {
            result = HttpTcpClientGetPlanResult.Fail(
                HttpTcpClientGetPlanStatus.TcpStreamReadFailed,
                _state,
                TcpIpv4FramePlanStatus.Success,
                TcpActiveConnectStatus.Success,
                readStatus,
                HttpClientGetTransactionStatus.Success,
                HttpRequestBuilderStatus.Success,
                HttpResponseParseStatus.Success,
                receiveResult.PacketStatus,
                ethernetHeader,
                ipv4Header,
                tcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength,
                requestBytesWritten: 0,
                responseBytesRead: 0,
                pendingStream.LocalNextSequenceNumber,
                pendingStream.RemoteNextSequenceNumber);
            return result.Status;
        }

        HttpClientGetTransaction pendingHttp = _http;
        HttpClientGetTransactionStatus httpStatus = pendingHttp.ReceiveResponse(
            responseDestination.Slice(0, readResult.BytesRead),
            out HttpClientGetTransactionResult httpResult);
        if (httpStatus != HttpClientGetTransactionStatus.Success)
        {
            _stream = pendingStream;
            _http = pendingHttp;
            _state = HttpTcpClientGetPlannerState.Failed;
            result = HttpTcpClientGetPlanResult.Fail(
                HttpTcpClientGetPlanStatus.HttpResponseFailed,
                _state,
                TcpIpv4FramePlanStatus.Success,
                TcpActiveConnectStatus.Success,
                receiveStatus,
                httpStatus,
                httpResult.RequestStatus,
                httpResult.ResponseStatus,
                receiveResult.PacketStatus,
                ethernetHeader,
                ipv4Header,
                tcpHeader,
                tcpSegmentOffset,
                tcpSegmentLength,
                requestBytesWritten: 0,
                readResult.BytesRead,
                pendingStream.LocalNextSequenceNumber,
                pendingStream.RemoteNextSequenceNumber);
            return result.Status;
        }

        _stream = pendingStream;
        _http = pendingHttp;
        _state = HttpTcpClientGetPlannerState.ResponseReceived;
        result = HttpTcpClientGetPlanResult.ResponseSuccess(
            _state,
            receiveResult,
            readResult,
            httpResult,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            tcpSegmentOffset,
            tcpSegmentLength);
        return result.Status;
    }

    private PacketParseStatus TryWriteFrameHeaders(
        Span<byte> frame,
        int tcpSegmentLength,
        out EthernetHeader ethernetHeader,
        out Ipv4Header ipv4Header,
        out int bytesWritten)
    {
        bytesWritten = 0;
        ethernetHeader = new EthernetHeader(
            _info.TcpInfo.RemoteMac,
            _info.TcpInfo.LocalMac,
            EthernetHeader.EtherTypeIpv4);
        ipv4Header = default;

        if (tcpSegmentLength > ushort.MaxValue - Ipv4Header.MinHeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        int frameLength = TcpSegmentOffset + tcpSegmentLength;
        if (frame.Length < frameLength)
        {
            return PacketParseStatus.BufferTooSmall;
        }

        PacketParseStatus ethernetStatus = EthernetHeader.TryWrite(frame, ethernetHeader);
        if (ethernetStatus != PacketParseStatus.Success)
        {
            return ethernetStatus;
        }

        ipv4Header = Ipv4Header.Create(
            _info.TcpInfo.LocalAddress,
            _info.TcpInfo.RemoteAddress,
            TcpIpv4FramePlanner.ProtocolTcp,
            (ushort)tcpSegmentLength,
            _info.TcpInfo.TimeToLive == 0 ? (byte)64 : _info.TcpInfo.TimeToLive,
            _info.TcpInfo.Identification,
            flagsAndFragmentOffset: 0);
        PacketParseStatus ipv4Status = Ipv4Header.TryWrite(
            frame.Slice(EthernetHeader.HeaderLength, Ipv4Header.MinHeaderLength),
            ipv4Header,
            out int ipv4BytesWritten);
        if (ipv4Status != PacketParseStatus.Success)
        {
            return ipv4Status;
        }

        bytesWritten = EthernetHeader.HeaderLength + ipv4BytesWritten + tcpSegmentLength;
        return PacketParseStatus.Success;
    }

    private static PacketParseStatus TryWriteTcpChecksum(
        Span<byte> tcpSegment,
        Ipv4Address sourceAddress,
        Ipv4Address destinationAddress)
    {
        if (tcpSegment.Length < TcpHeader.MinHeaderLength)
        {
            return PacketParseStatus.InvalidLength;
        }

        EndianPrimitives.WriteUInt16BigEndian(
            tcpSegment.Slice(16, sizeof(ushort)),
            0);
        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Compute(
            sourceAddress,
            destinationAddress,
            TcpIpv4FramePlanner.ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success)
        {
            return PacketParseStatus.InvalidLength;
        }

        ushort checksum = checksumResult.Checksum == 0
            ? ushort.MaxValue
            : checksumResult.Checksum;
        EndianPrimitives.WriteUInt16BigEndian(
            tcpSegment.Slice(16, sizeof(ushort)),
            checksum);
        return PacketParseStatus.Success;
    }

    private HttpTcpClientGetPlanStatus TryParseInboundTcpFrame(
        ReadOnlySpan<byte> frame,
        out EthernetHeader ethernetHeader,
        out Ipv4Header ipv4Header,
        out TcpHeader tcpHeader,
        out ReadOnlySpan<byte> tcpSegment,
        out int tcpSegmentOffset,
        out int tcpSegmentLength,
        out PacketParseStatus packetStatus)
    {
        tcpHeader = default;
        ipv4Header = default;
        tcpSegment = default;
        tcpSegmentOffset = 0;
        tcpSegmentLength = 0;

        packetStatus = EthernetHeader.TryParse(
            frame,
            out ethernetHeader,
            out ReadOnlySpan<byte> ethernetPayload);
        if (packetStatus != PacketParseStatus.Success)
        {
            return HttpTcpClientGetPlanStatus.FrameParseFailed;
        }

        if (ethernetHeader.EtherType != EthernetHeader.EtherTypeIpv4)
        {
            packetStatus = PacketParseStatus.UnsupportedEtherType;
            return HttpTcpClientGetPlanStatus.UnsupportedEtherType;
        }

        if (ethernetHeader.Destination != _info.TcpInfo.LocalMac)
        {
            return HttpTcpClientGetPlanStatus.WrongLocalMac;
        }

        if (ethernetHeader.Source != _info.TcpInfo.RemoteMac)
        {
            return HttpTcpClientGetPlanStatus.WrongRemoteMac;
        }

        packetStatus = Ipv4Header.TryParse(
            ethernetPayload,
            out ipv4Header,
            out ReadOnlySpan<byte> ipv4Payload);
        if (packetStatus != PacketParseStatus.Success)
        {
            return HttpTcpClientGetPlanStatus.FrameParseFailed;
        }

        if (ipv4Header.Protocol != TcpIpv4FramePlanner.ProtocolTcp)
        {
            packetStatus = PacketParseStatus.UnsupportedIpProtocol;
            return HttpTcpClientGetPlanStatus.UnsupportedIpProtocol;
        }

        if (ipv4Header.Destination != _info.TcpInfo.LocalAddress)
        {
            return HttpTcpClientGetPlanStatus.WrongLocalAddress;
        }

        if (ipv4Header.Source != _info.TcpInfo.RemoteAddress)
        {
            return HttpTcpClientGetPlanStatus.WrongRemoteAddress;
        }

        packetStatus = TcpHeader.TryParse(
            ipv4Payload,
            out tcpHeader,
            out ReadOnlySpan<byte> _,
            out ReadOnlySpan<byte> _);
        if (packetStatus != PacketParseStatus.Success)
        {
            return HttpTcpClientGetPlanStatus.TcpSegmentParseFailed;
        }

        tcpSegment = ipv4Payload;
        tcpSegmentOffset = EthernetHeader.HeaderLength + ipv4Header.HeaderLength;
        tcpSegmentLength = ipv4Payload.Length;

        Ipv4TransportChecksumStatus checksumStatus = Ipv4TransportChecksum.Validate(
            ipv4Header.Source,
            ipv4Header.Destination,
            TcpIpv4FramePlanner.ProtocolTcp,
            tcpSegment,
            out Ipv4TransportChecksumResult checksumResult);
        if (checksumStatus != Ipv4TransportChecksumStatus.Success || !checksumResult.IsValid)
        {
            packetStatus = PacketParseStatus.BadChecksum;
            return HttpTcpClientGetPlanStatus.TcpFrameFailed;
        }

        if (tcpHeader.SourcePort != _info.TcpInfo.RemotePort
            || tcpHeader.DestinationPort != _info.TcpInfo.LocalPort)
        {
            return HttpTcpClientGetPlanStatus.WrongTcpPorts;
        }

        if ((tcpHeader.Flags & TcpEstablishedStream.FlagAck) == 0
            || (tcpHeader.Flags & ~(TcpEstablishedStream.FlagPsh | TcpEstablishedStream.FlagAck)) != 0)
        {
            return HttpTcpClientGetPlanStatus.InvalidTcpFlags;
        }

        return HttpTcpClientGetPlanStatus.Success;
    }

    private HttpTcpClientGetPlanResult CreateFrameFailure(
        HttpTcpClientGetPlanStatus status,
        PacketParseStatus packetStatus,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int tcpSegmentOffset,
        int tcpSegmentLength)
    {
        return CreateFrameFailure(
            status,
            TcpIpv4FramePlanStatus.Success,
            packetStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            tcpSegmentOffset,
            tcpSegmentLength);
    }

    private HttpTcpClientGetPlanResult CreateFrameFailure(
        HttpTcpClientGetPlanStatus status,
        TcpIpv4FramePlanStatus tcpFrameStatus,
        PacketParseStatus packetStatus,
        EthernetHeader ethernetHeader,
        Ipv4Header ipv4Header,
        TcpHeader tcpHeader,
        int tcpSegmentOffset,
        int tcpSegmentLength)
    {
        return HttpTcpClientGetPlanResult.Fail(
            status,
            _state,
            tcpFrameStatus,
            TcpActiveConnectStatus.Success,
            TcpEstablishedStreamStatus.Success,
            HttpClientGetTransactionStatus.Success,
            HttpRequestBuilderStatus.Success,
            HttpResponseParseStatus.Success,
            packetStatus,
            ethernetHeader,
            ipv4Header,
            tcpHeader,
            tcpSegmentOffset,
            tcpSegmentLength,
            requestBytesWritten: 0,
            responseBytesRead: 0,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
    }

    private HttpTcpClientGetPlanResult CreateFailure(HttpTcpClientGetPlanStatus status)
    {
        return CreateFailure(
            status,
            HttpClientGetTransactionStatus.Success,
            HttpRequestBuilderStatus.Success,
            HttpResponseParseStatus.Success,
            PacketParseStatus.Success,
            TcpEstablishedStreamStatus.Success,
            requestBytesWritten: 0);
    }

    private HttpTcpClientGetPlanResult CreateFailure(
        HttpTcpClientGetPlanStatus status,
        HttpClientGetTransactionStatus httpStatus,
        HttpRequestBuilderStatus requestStatus,
        HttpResponseParseStatus responseStatus,
        int requestBytesWritten)
    {
        return CreateFailure(
            status,
            httpStatus,
            requestStatus,
            responseStatus,
            PacketParseStatus.Success,
            TcpEstablishedStreamStatus.Success,
            requestBytesWritten);
    }

    private HttpTcpClientGetPlanResult CreateFailure(
        HttpTcpClientGetPlanStatus status,
        HttpClientGetTransactionStatus httpStatus,
        HttpRequestBuilderStatus requestStatus,
        HttpResponseParseStatus responseStatus,
        PacketParseStatus packetStatus,
        int requestBytesWritten)
    {
        return CreateFailure(
            status,
            httpStatus,
            requestStatus,
            responseStatus,
            packetStatus,
            TcpEstablishedStreamStatus.Success,
            requestBytesWritten);
    }

    private HttpTcpClientGetPlanResult CreateFailure(
        HttpTcpClientGetPlanStatus status,
        HttpClientGetTransactionStatus httpStatus,
        HttpRequestBuilderStatus requestStatus,
        HttpResponseParseStatus responseStatus,
        PacketParseStatus packetStatus,
        TcpEstablishedStreamStatus streamStatus,
        int requestBytesWritten)
    {
        return HttpTcpClientGetPlanResult.Fail(
            status,
            _state,
            TcpIpv4FramePlanStatus.Success,
            TcpActiveConnectStatus.Success,
            streamStatus,
            httpStatus,
            requestStatus,
            responseStatus,
            packetStatus,
            default,
            default,
            default,
            tcpSegmentOffset: 0,
            tcpSegmentLength: 0,
            requestBytesWritten,
            responseBytesRead: 0,
            LocalNextSequenceNumber,
            RemoteNextSequenceNumber);
    }

    private static HttpTcpClientGetPlanStatus MapTcpFrameStatus(TcpIpv4FramePlanStatus status)
    {
        return status switch
        {
            TcpIpv4FramePlanStatus.Success => HttpTcpClientGetPlanStatus.Success,
            TcpIpv4FramePlanStatus.InvalidState => HttpTcpClientGetPlanStatus.InvalidState,
            TcpIpv4FramePlanStatus.FrameBufferTooSmall => HttpTcpClientGetPlanStatus.FrameBufferTooSmall,
            TcpIpv4FramePlanStatus.FrameWriteFailed => HttpTcpClientGetPlanStatus.FrameWriteFailed,
            TcpIpv4FramePlanStatus.FrameParseFailed => HttpTcpClientGetPlanStatus.FrameParseFailed,
            TcpIpv4FramePlanStatus.UnsupportedEtherType => HttpTcpClientGetPlanStatus.UnsupportedEtherType,
            TcpIpv4FramePlanStatus.UnsupportedIpProtocol => HttpTcpClientGetPlanStatus.UnsupportedIpProtocol,
            TcpIpv4FramePlanStatus.WrongLocalMac => HttpTcpClientGetPlanStatus.WrongLocalMac,
            TcpIpv4FramePlanStatus.WrongRemoteMac => HttpTcpClientGetPlanStatus.WrongRemoteMac,
            TcpIpv4FramePlanStatus.WrongLocalAddress => HttpTcpClientGetPlanStatus.WrongLocalAddress,
            TcpIpv4FramePlanStatus.WrongRemoteAddress => HttpTcpClientGetPlanStatus.WrongRemoteAddress,
            TcpIpv4FramePlanStatus.TcpSegmentParseFailed => HttpTcpClientGetPlanStatus.TcpSegmentParseFailed,
            TcpIpv4FramePlanStatus.WrongTcpPorts => HttpTcpClientGetPlanStatus.WrongTcpPorts,
            TcpIpv4FramePlanStatus.InvalidTcpFlags => HttpTcpClientGetPlanStatus.InvalidTcpFlags,
            TcpIpv4FramePlanStatus.WrongAcknowledgementNumber =>
                HttpTcpClientGetPlanStatus.WrongAcknowledgementNumber,
            _ => HttpTcpClientGetPlanStatus.TcpFrameFailed
        };
    }

    private static HttpTcpClientGetPlanStatus MapFrameWriteStatus(PacketParseStatus status)
    {
        return status == PacketParseStatus.BufferTooSmall
            ? HttpTcpClientGetPlanStatus.FrameBufferTooSmall
            : HttpTcpClientGetPlanStatus.FrameWriteFailed;
    }

    private static HttpTcpClientGetPlanStatus MapHttpRequestStatus(
        HttpClientGetTransactionStatus status)
    {
        return status == HttpClientGetTransactionStatus.RequestBufferTooSmall
            ? HttpTcpClientGetPlanStatus.RequestBufferTooSmall
            : HttpTcpClientGetPlanStatus.HttpRequestFailed;
    }
}
