namespace OS.Net;
public static class HttpStaticTcpResponsePlan
{
    public const bool ConsumesTcpReceiveDispatchMetadata = true;
    public const bool UsesCallerOwnedBuffers = true;
    public const bool AllocatesBuffers = false;
    public const bool OpensSockets = false;
    public const bool WritesTcpFrames = false;
    public const bool PerformsNicIo = false;
    public const bool StartsHostedDotNet = false;
    public const bool StartsKestrel = false;
    public static HttpStaticTcpResponsePlanStatus TryHandle(ReadOnlySpan<byte> receiveBuffer, TcpReceiveDispatchResult tcpDispatch, ReadOnlySpan<byte> routeTarget, ReadOnlySpan<byte> successBody, ReadOnlySpan<byte> notFoundBody, ReadOnlySpan<byte> methodNotAllowedBody, ReadOnlySpan<byte> contentType, Span<byte> responseDestination, out HttpStaticTcpResponsePlanResult result)
    {
        result = default;
        if (!tcpDispatch.IsInitialized)
        {
            result = HttpStaticTcpResponsePlanResult.Fail(HttpStaticTcpResponsePlanStatus.TcpDispatchNotInitialized, tcpDispatch, default, requestPayloadOffset: 0, requestPayloadLength: 0);
            return result.Status;
        }

        if (!tcpDispatch.Succeeded)
        {
            result = HttpStaticTcpResponsePlanResult.Fail(HttpStaticTcpResponsePlanStatus.TcpDispatchFailed, tcpDispatch, default, requestPayloadOffset: 0, requestPayloadLength: 0);
            return result.Status;
        }

        if (tcpDispatch.Action != TcpReceiveDispatchAction.PayloadAccepted || tcpDispatch.PayloadBytes <= 0)
        {
            result = HttpStaticTcpResponsePlanResult.Fail(HttpStaticTcpResponsePlanStatus.NonPayloadDispatch, tcpDispatch, default, requestPayloadOffset: 0, requestPayloadLength: tcpDispatch.PayloadBytes);
            return result.Status;
        }

        int headerLength = tcpDispatch.TcpHeader.HeaderLength;
        int requestPayloadOffset = tcpDispatch.TcpSegmentOffset + headerLength;
        if (!IsValidPayloadRange(receiveBuffer.Length, tcpDispatch.TcpSegmentOffset, tcpDispatch.TcpSegmentLength, headerLength, requestPayloadOffset, tcpDispatch.PayloadBytes))
        {
            result = HttpStaticTcpResponsePlanResult.Fail(HttpStaticTcpResponsePlanStatus.InvalidPayloadRange, tcpDispatch, default, requestPayloadOffset, tcpDispatch.PayloadBytes);
            return result.Status;
        }

        ReadOnlySpan<byte> requestPayload = receiveBuffer.Slice(requestPayloadOffset, tcpDispatch.PayloadBytes);
        HttpStaticResponseTransactionStatus httpStatus = HttpStaticResponseTransaction.TryHandle(requestPayload, routeTarget, successBody, notFoundBody, methodNotAllowedBody, contentType, responseDestination, out HttpStaticResponseTransactionResult http);
        if (httpStatus != HttpStaticResponseTransactionStatus.Success || !http.Succeeded)
        {
            result = HttpStaticTcpResponsePlanResult.Fail(HttpStaticTcpResponsePlanStatus.HttpTransactionFailed, tcpDispatch, http, requestPayloadOffset, tcpDispatch.PayloadBytes);
            return result.Status;
        }

        result = HttpStaticTcpResponsePlanResult.Success(tcpDispatch, requestPayloadOffset, http);
        return result.Status;
    }

    private static bool IsValidPayloadRange(int receiveBufferLength, int tcpSegmentOffset, int tcpSegmentLength, int headerLength, int requestPayloadOffset, int payloadLength)
    {
        return tcpSegmentOffset >= 0 && tcpSegmentLength >= TcpHeader.MinHeaderLength && headerLength >= TcpHeader.MinHeaderLength && headerLength <= TcpHeader.MaxHeaderLength && payloadLength > 0 && headerLength <= tcpSegmentLength && payloadLength <= tcpSegmentLength - headerLength && requestPayloadOffset >= 0 && requestPayloadOffset <= receiveBufferLength && payloadLength <= receiveBufferLength - requestPayloadOffset;
    }
}
