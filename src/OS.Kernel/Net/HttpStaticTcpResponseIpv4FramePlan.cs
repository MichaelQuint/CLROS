namespace OS.Net;

public enum HttpStaticTcpResponseIpv4FramePlanStatus
{
    Success = 0,
    ResponsePlanFailed = 1,
    ResponseExceedsMaximumPayload = 2,
    FramePlanFailed = 3
}

public readonly struct HttpStaticTcpResponseIpv4FramePlanResult
{
    private const uint InitializedFlag = 1;

    private HttpStaticTcpResponseIpv4FramePlanResult(
        HttpStaticTcpResponseIpv4FramePlanStatus status,
        HttpStaticTcpResponsePlanResult response,
        TcpEstablishedStreamIpv4FramePlanResult frame,
        uint flags)
    {
        Status = status;
        Response = response;
        Frame = frame;
        Flags = flags;
    }

    public HttpStaticTcpResponseIpv4FramePlanStatus Status { get; }

    public HttpStaticTcpResponsePlanResult Response { get; }

    public TcpEstablishedStreamIpv4FramePlanResult Frame { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == HttpStaticTcpResponseIpv4FramePlanStatus.Success &&
        Response.Succeeded &&
        Frame.Succeeded &&
        Response.ResponseBytesWritten > 0 &&
        Frame.PayloadBytes == Response.ResponseBytesWritten;

    public int ResponseBytesWritten => Response.ResponseBytesWritten;

    public int FrameBytesWritten => Frame.BytesWritten;

    public int TcpSegmentOffset => Frame.TcpSegmentOffset;

    public int TcpSegmentLength => Frame.TcpSegmentLength;

    public int PayloadBytes => Frame.PayloadBytes;

    public ushort HttpStatusCode => Response.HttpStatusCode;

    public uint LocalNextSequenceNumber => Frame.LocalNextSequenceNumber;

    public uint RemoteNextSequenceNumber => Frame.RemoteNextSequenceNumber;

    public static HttpStaticTcpResponseIpv4FramePlanResult Create(
        HttpStaticTcpResponseIpv4FramePlanStatus status,
        HttpStaticTcpResponsePlanResult response,
        TcpEstablishedStreamIpv4FramePlanResult frame)
    {
        return new HttpStaticTcpResponseIpv4FramePlanResult(
            status,
            response,
            frame,
            InitializedFlag);
    }
}

public static class HttpStaticTcpResponseIpv4FramePlan
{
    public const bool ConsumesStaticTcpResponsePlan = true;
    public const bool UsesCallerOwnedBuffers = true;
    public const bool AllocatesBuffers = false;
    public const bool OpensSockets = false;
    public const bool WritesTcpFrame = true;
    public const bool PerformsNicIo = false;
    public const bool OwnsTimers = false;
    public const bool PerformsRetransmission = false;
    public const bool PerformsTls = false;
    public const bool StartsHostedDotNet = false;
    public const bool StartsKestrel = false;

    public static HttpStaticTcpResponseIpv4FramePlanStatus TryWriteResponseFrame(
        ReadOnlySpan<byte> receiveBuffer,
        TcpReceiveDispatchResult tcpDispatch,
        in TcpEstablishedStreamIpv4FramePlannerInfo frameInfo,
        ReadOnlySpan<byte> routeTarget,
        ReadOnlySpan<byte> successBody,
        ReadOnlySpan<byte> notFoundBody,
        ReadOnlySpan<byte> methodNotAllowedBody,
        ReadOnlySpan<byte> contentType,
        Span<byte> responseScratch,
        Span<byte> streamReceiveScratch,
        Span<byte> frameDestination,
        out HttpStaticTcpResponseIpv4FramePlanResult result)
    {
        result = default;

        HttpStaticTcpResponsePlanStatus responseStatus = HttpStaticTcpResponsePlan.TryHandle(
            receiveBuffer,
            tcpDispatch,
            routeTarget,
            successBody,
            notFoundBody,
            methodNotAllowedBody,
            contentType,
            responseScratch,
            out HttpStaticTcpResponsePlanResult response);
        if (responseStatus != HttpStaticTcpResponsePlanStatus.Success || !response.Succeeded)
        {
            result = HttpStaticTcpResponseIpv4FramePlanResult.Create(
                HttpStaticTcpResponseIpv4FramePlanStatus.ResponsePlanFailed,
                response,
                default);
            return result.Status;
        }

        if (frameInfo.MaximumPayloadLength <= 0 ||
            response.ResponseBytesWritten > frameInfo.MaximumPayloadLength)
        {
            result = HttpStaticTcpResponseIpv4FramePlanResult.Create(
                HttpStaticTcpResponseIpv4FramePlanStatus.ResponseExceedsMaximumPayload,
                response,
                default);
            return result.Status;
        }

        TcpEstablishedStream stream = TcpEstablishedStream.Create(
            tcpDispatch.TcpHeader.DestinationPort,
            tcpDispatch.TcpHeader.SourcePort,
            tcpDispatch.LocalNextSequenceNumber,
            tcpDispatch.RemoteNextSequenceNumber,
            streamReceiveScratch);
        TcpEstablishedStreamIpv4FramePlanStatus frameStatus =
            TcpEstablishedStreamIpv4FramePlanner.TryWritePayloadFrame(
                ref stream,
                frameInfo,
                responseScratch.Slice(0, response.ResponseBytesWritten),
                frameDestination,
                out TcpEstablishedStreamIpv4FramePlanResult frame);
        if (frameStatus != TcpEstablishedStreamIpv4FramePlanStatus.Success ||
            !frame.Succeeded ||
            frame.PayloadBytes != response.ResponseBytesWritten)
        {
            result = HttpStaticTcpResponseIpv4FramePlanResult.Create(
                HttpStaticTcpResponseIpv4FramePlanStatus.FramePlanFailed,
                response,
                frame);
            return result.Status;
        }

        result = HttpStaticTcpResponseIpv4FramePlanResult.Create(
            HttpStaticTcpResponseIpv4FramePlanStatus.Success,
            response,
            frame);
        return result.Status;
    }
}
