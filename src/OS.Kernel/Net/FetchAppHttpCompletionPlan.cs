namespace OS.Net;

public static class FetchAppHttpCompletionPlan
{
    public static FetchAppHttpCompletionPlanResult Evaluate(in HttpTcpClientGetPlanResult httpPlan, ReadOnlySpan<byte> responseBuffer, ushort expectedHttpStatusCode, ReadOnlySpan<byte> expectedBodyMarker)
    {
        if (expectedBodyMarker.Length == 0)
        {
            return FetchAppHttpCompletionPlanResult.Fail(FetchAppHttpCompletionPlanStatus.InvalidExpectedMarker, in httpPlan, expectedHttpStatusCode, expectedBodyMarker.Length);
        }

        if (!httpPlan.IsInitialized || !httpPlan.Succeeded)
        {
            return FetchAppHttpCompletionPlanResult.Fail(FetchAppHttpCompletionPlanStatus.HttpPlanFailed, in httpPlan, expectedHttpStatusCode, expectedBodyMarker.Length);
        }

        if (httpPlan.HttpStatusCode != expectedHttpStatusCode)
        {
            return FetchAppHttpCompletionPlanResult.Fail(FetchAppHttpCompletionPlanStatus.UnexpectedHttpStatus, in httpPlan, expectedHttpStatusCode, expectedBodyMarker.Length);
        }

        FetchAppHttpCompletionPlanStatus rangeStatus = ValidateBodyRange(in httpPlan, responseBuffer.Length);
        if (rangeStatus != FetchAppHttpCompletionPlanStatus.Success)
        {
            return FetchAppHttpCompletionPlanResult.Fail(rangeStatus, in httpPlan, expectedHttpStatusCode, expectedBodyMarker.Length);
        }

        ReadOnlySpan<byte> body = responseBuffer.Slice(httpPlan.HttpBodyOffset, httpPlan.HttpBodyLength);
        int bodyMatchOffset = OS.Std.MemoryExtensions.IndexOf(body, expectedBodyMarker);
        if (bodyMatchOffset < 0)
        {
            return FetchAppHttpCompletionPlanResult.Fail(FetchAppHttpCompletionPlanStatus.MissingExpectedMarker, in httpPlan, expectedHttpStatusCode, expectedBodyMarker.Length);
        }

        return FetchAppHttpCompletionPlanResult.Success(httpPlan.Status, httpPlan.HttpStatusCode, expectedHttpStatusCode, httpPlan.ResponseBytesRead, httpPlan.HttpBodyOffset, httpPlan.HttpBodyLength, expectedBodyMarker.Length, httpPlan.HttpBodyOffset + bodyMatchOffset);
    }

    private static FetchAppHttpCompletionPlanStatus ValidateBodyRange(in HttpTcpClientGetPlanResult httpPlan, int responseBufferLength)
    {
        if (httpPlan.ResponseBytesRead < 0 || httpPlan.HttpBodyOffset < 0 || httpPlan.HttpBodyLength < 0 || httpPlan.HttpBodyOffset > int.MaxValue - httpPlan.HttpBodyLength)
        {
            return FetchAppHttpCompletionPlanStatus.InvalidBodyRange;
        }

        int bodyEnd = httpPlan.HttpBodyOffset + httpPlan.HttpBodyLength;
        if (bodyEnd > httpPlan.ResponseBytesRead)
        {
            return FetchAppHttpCompletionPlanStatus.InvalidBodyRange;
        }

        if (responseBufferLength < httpPlan.ResponseBytesRead || bodyEnd > responseBufferLength)
        {
            return FetchAppHttpCompletionPlanStatus.ResponseBufferTooSmall;
        }

        return FetchAppHttpCompletionPlanStatus.Success;
    }
}
