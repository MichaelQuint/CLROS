namespace OS.Net;

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
    public static HttpStaticTcpResponseIpv4FramePlanStatus TryWriteResponseFrame(ReadOnlySpan<byte> receiveBuffer, TcpReceiveDispatchResult tcpDispatch, in TcpEstablishedStreamIpv4FramePlannerInfo frameInfo, ReadOnlySpan<byte> routeTarget, ReadOnlySpan<byte> successBody, ReadOnlySpan<byte> notFoundBody, ReadOnlySpan<byte> methodNotAllowedBody, ReadOnlySpan<byte> contentType, Span<byte> responseScratch, Span<byte> streamReceiveScratch, Span<byte> frameDestination, out HttpStaticTcpResponseIpv4FramePlanResult result)
    {
        result = default;
        HttpStaticTcpResponsePlanStatus responseStatus = HttpStaticTcpResponsePlan.TryHandle(receiveBuffer, tcpDispatch, routeTarget, successBody, notFoundBody, methodNotAllowedBody, contentType, responseScratch, out HttpStaticTcpResponsePlanResult response);
        if (responseStatus != HttpStaticTcpResponsePlanStatus.Success || !response.Succeeded)
        {
            result = HttpStaticTcpResponseIpv4FramePlanResult.Create(HttpStaticTcpResponseIpv4FramePlanStatus.ResponsePlanFailed, response, default);
            return result.Status;
        }

        if (frameInfo.MaximumPayloadLength <= 0 || response.ResponseBytesWritten > frameInfo.MaximumPayloadLength)
        {
            result = HttpStaticTcpResponseIpv4FramePlanResult.Create(HttpStaticTcpResponseIpv4FramePlanStatus.ResponseExceedsMaximumPayload, response, default);
            return result.Status;
        }

        TcpEstablishedStream stream = TcpEstablishedStream.Create(tcpDispatch.TcpHeader.DestinationPort, tcpDispatch.TcpHeader.SourcePort, tcpDispatch.LocalNextSequenceNumber, tcpDispatch.RemoteNextSequenceNumber, streamReceiveScratch);
        TcpEstablishedStreamIpv4FramePlanStatus frameStatus = TcpEstablishedStreamIpv4FramePlanner.TryWritePayloadFrame(ref stream, frameInfo, responseScratch.Slice(0, response.ResponseBytesWritten), frameDestination, out TcpEstablishedStreamIpv4FramePlanResult frame);
        if (frameStatus != TcpEstablishedStreamIpv4FramePlanStatus.Success || !frame.Succeeded || frame.PayloadBytes != response.ResponseBytesWritten)
        {
            result = HttpStaticTcpResponseIpv4FramePlanResult.Create(HttpStaticTcpResponseIpv4FramePlanStatus.FramePlanFailed, response, frame);
            return result.Status;
        }

        result = HttpStaticTcpResponseIpv4FramePlanResult.Create(HttpStaticTcpResponseIpv4FramePlanStatus.Success, response, frame);
        return result.Status;
    }
}
