namespace OS.Net;
public static class FetchAppHttpTcpExchangePlan
{
    public const bool UsesCallerOwnedFrameBuffers = true;
    public const bool PerformsLiveNetworkIo = false;
    public const bool OpensSockets = false;
    public const bool PerformsDnsOrArp = false;
    public const bool PerformsTls = false;
    public static FetchAppHttpTcpExchangePlanStatus Execute(in FetchAppHttpTcpContextPlanResult context, ReadOnlySpan<byte> hostBytes, ReadOnlySpan<byte> pathBytes, ReadOnlySpan<byte> synAckFrame, ReadOnlySpan<byte> responseFrame, Span<byte> streamReceiveBuffer, Span<byte> synFrame, Span<byte> finalAckFrame, Span<byte> requestScratch, Span<byte> requestFrame, Span<byte> responseDestination, Span<byte> closeFrame, ushort expectedHttpStatusCode, ReadOnlySpan<byte> expectedBodyMarker, out FetchAppHttpTcpExchangePlanResult result)
    {
        HttpTcpClientGetPlanResult syn = default;
        HttpTcpClientGetPlanResult synAck = default;
        HttpTcpClientGetPlanResult finalAck = default;
        HttpTcpClientGetPlanResult request = default;
        HttpTcpClientGetPlanResult response = default;
        FetchAppHttpCompletionPlanResult completion = default;
        TcpConnectionCloseIpv4FramePlanResult close = default;
        int requiredHostBytes = context.HostBytes;
        int requiredPathBytes = context.PathBytes;
        int expectedMarkerLength = expectedBodyMarker.Length;
        if (!context.Succeeded)
        {
            result = FetchAppHttpTcpExchangePlanResult.Create(FetchAppHttpTcpExchangePlanStatus.ContextFailed, in context, in syn, in synAck, in finalAck, in request, in response, in completion, in close, requiredHostBytes, requiredPathBytes, expectedHttpStatusCode, expectedMarkerLength);
            return result.Status;
        }

        if (hostBytes.Length < requiredHostBytes || pathBytes.Length < requiredPathBytes)
        {
            result = FetchAppHttpTcpExchangePlanResult.Create(FetchAppHttpTcpExchangePlanStatus.EndpointBufferTooSmall, in context, in syn, in synAck, in finalAck, in request, in response, in completion, in close, requiredHostBytes, requiredPathBytes, expectedHttpStatusCode, expectedMarkerLength);
            return result.Status;
        }

        HttpTcpClientGetPlanner planner = HttpTcpClientGetPlanner.Create(context.PlannerInfo, streamReceiveBuffer);
        if (planner.TryWriteSynFrame(synFrame, out syn) != HttpTcpClientGetPlanStatus.Success || !syn.Succeeded)
        {
            result = CreateFailure(FetchAppHttpTcpExchangePlanStatus.SynFrameFailed, in context, in syn, in synAck, in finalAck, in request, in response, in completion, in close, requiredHostBytes, requiredPathBytes, expectedHttpStatusCode, expectedMarkerLength);
            return result.Status;
        }

        if (planner.ReceiveSynAckFrame(synAckFrame, out synAck) != HttpTcpClientGetPlanStatus.Success || !synAck.Succeeded)
        {
            result = CreateFailure(FetchAppHttpTcpExchangePlanStatus.SynAckFrameFailed, in context, in syn, in synAck, in finalAck, in request, in response, in completion, in close, requiredHostBytes, requiredPathBytes, expectedHttpStatusCode, expectedMarkerLength);
            return result.Status;
        }

        if (planner.TryWriteFinalAckFrame(finalAckFrame, out finalAck) != HttpTcpClientGetPlanStatus.Success || !finalAck.Succeeded)
        {
            result = CreateFailure(FetchAppHttpTcpExchangePlanStatus.FinalAckFrameFailed, in context, in syn, in synAck, in finalAck, in request, in response, in completion, in close, requiredHostBytes, requiredPathBytes, expectedHttpStatusCode, expectedMarkerLength);
            return result.Status;
        }

        if (planner.TryWriteRequestFrame(hostBytes.Slice(0, requiredHostBytes), pathBytes.Slice(0, requiredPathBytes), requestScratch, requestFrame, out request) != HttpTcpClientGetPlanStatus.Success || !request.Succeeded)
        {
            result = CreateFailure(FetchAppHttpTcpExchangePlanStatus.RequestFrameFailed, in context, in syn, in synAck, in finalAck, in request, in response, in completion, in close, requiredHostBytes, requiredPathBytes, expectedHttpStatusCode, expectedMarkerLength);
            return result.Status;
        }

        if (planner.ReceiveResponseFrame(responseFrame, responseDestination, out response) != HttpTcpClientGetPlanStatus.Success || !response.Succeeded)
        {
            result = CreateFailure(FetchAppHttpTcpExchangePlanStatus.ResponseFrameFailed, in context, in syn, in synAck, in finalAck, in request, in response, in completion, in close, requiredHostBytes, requiredPathBytes, expectedHttpStatusCode, expectedMarkerLength);
            return result.Status;
        }

        completion = FetchAppHttpCompletionPlan.Evaluate(in response, responseDestination, expectedHttpStatusCode, expectedBodyMarker);
        if (!completion.Succeeded)
        {
            result = CreateFailure(FetchAppHttpTcpExchangePlanStatus.CompletionFailed, in context, in syn, in synAck, in finalAck, in request, in response, in completion, in close, requiredHostBytes, requiredPathBytes, expectedHttpStatusCode, expectedMarkerLength);
            return result.Status;
        }

        TcpIpv4FramePlannerInfo tcpInfo = context.PlannerInfo.TcpInfo;
        TcpConnectionCloseIpv4FramePlannerInfo closeInfo = new TcpConnectionCloseIpv4FramePlannerInfo(tcpInfo.LocalMac, tcpInfo.RemoteMac, tcpInfo.LocalAddress, tcpInfo.RemoteAddress, tcpInfo.LocalPort, tcpInfo.RemotePort, response.LocalNextSequenceNumber, response.RemoteNextSequenceNumber, tcpInfo.WindowSize, tcpInfo.TimeToLive, tcpInfo.Identification);
        TcpConnectionCloseIpv4FramePlanner closePlanner = TcpConnectionCloseIpv4FramePlanner.Create(closeInfo);
        if (closePlanner.TryWriteActiveFinAckFrame(closeFrame, out close) != TcpConnectionCloseIpv4FramePlanStatus.Success || !close.Succeeded)
        {
            result = CreateFailure(FetchAppHttpTcpExchangePlanStatus.CloseFrameFailed, in context, in syn, in synAck, in finalAck, in request, in response, in completion, in close, requiredHostBytes, requiredPathBytes, expectedHttpStatusCode, expectedMarkerLength);
            return result.Status;
        }

        result = FetchAppHttpTcpExchangePlanResult.Create(FetchAppHttpTcpExchangePlanStatus.Success, in context, in syn, in synAck, in finalAck, in request, in response, in completion, in close, requiredHostBytes, requiredPathBytes, expectedHttpStatusCode, expectedMarkerLength);
        return result.Status;
    }

    private static FetchAppHttpTcpExchangePlanResult CreateFailure(FetchAppHttpTcpExchangePlanStatus status, in FetchAppHttpTcpContextPlanResult context, in HttpTcpClientGetPlanResult syn, in HttpTcpClientGetPlanResult synAck, in HttpTcpClientGetPlanResult finalAck, in HttpTcpClientGetPlanResult request, in HttpTcpClientGetPlanResult response, in FetchAppHttpCompletionPlanResult completion, in TcpConnectionCloseIpv4FramePlanResult close, int hostBytes, int pathBytes, ushort expectedHttpStatusCode, int expectedMarkerLength)
    {
        return FetchAppHttpTcpExchangePlanResult.Create(status, in context, in syn, in synAck, in finalAck, in request, in response, in completion, in close, hostBytes, pathBytes, expectedHttpStatusCode, expectedMarkerLength);
    }
}
