namespace OS.Net;

public enum FetchAppHttpCompletionPlanStatus
{
    Success = 0,
    HttpPlanFailed = 1,
    UnexpectedHttpStatus = 2,
    InvalidBodyRange = 3,
    MissingExpectedMarker = 4,
    InvalidExpectedMarker = 5,
    ResponseBufferTooSmall = 6
}

public readonly struct FetchAppHttpCompletionPlanResult
{
    public const int NoMatchOffset = -1;

    private const uint InitializedFlag = 1;

    private FetchAppHttpCompletionPlanResult(
        FetchAppHttpCompletionPlanStatus status,
        HttpTcpClientGetPlanStatus httpPlanStatus,
        ushort httpStatusCode,
        ushort expectedHttpStatusCode,
        int responseBytesRead,
        int bodyOffset,
        int bodyLength,
        int expectedMarkerLength,
        int matchOffset,
        uint flags)
    {
        Status = status;
        HttpPlanStatus = httpPlanStatus;
        HttpStatusCode = httpStatusCode;
        ExpectedHttpStatusCode = expectedHttpStatusCode;
        ResponseBytesRead = responseBytesRead;
        BodyOffset = bodyOffset;
        BodyLength = bodyLength;
        ExpectedMarkerLength = expectedMarkerLength;
        MatchOffset = matchOffset;
        Flags = flags;
    }

    public FetchAppHttpCompletionPlanStatus Status { get; }

    public HttpTcpClientGetPlanStatus HttpPlanStatus { get; }

    public ushort HttpStatusCode { get; }

    public ushort ExpectedHttpStatusCode { get; }

    public int ResponseBytesRead { get; }

    public int BodyOffset { get; }

    public int BodyLength { get; }

    public int ExpectedMarkerLength { get; }

    public int MatchOffset { get; }

    public uint Flags { get; }

    public bool IsInitialized => (Flags & InitializedFlag) != 0;

    public bool Succeeded =>
        IsInitialized &&
        Status == FetchAppHttpCompletionPlanStatus.Success &&
        HttpPlanStatus == HttpTcpClientGetPlanStatus.Success &&
        ExpectedMarkerLength > 0 &&
        MatchOffset >= 0;

    public static FetchAppHttpCompletionPlanResult Success(
        HttpTcpClientGetPlanStatus httpPlanStatus,
        ushort httpStatusCode,
        ushort expectedHttpStatusCode,
        int responseBytesRead,
        int bodyOffset,
        int bodyLength,
        int expectedMarkerLength,
        int matchOffset)
    {
        return new FetchAppHttpCompletionPlanResult(
            FetchAppHttpCompletionPlanStatus.Success,
            httpPlanStatus,
            httpStatusCode,
            expectedHttpStatusCode,
            responseBytesRead,
            bodyOffset,
            bodyLength,
            expectedMarkerLength,
            matchOffset,
            InitializedFlag);
    }

    public static FetchAppHttpCompletionPlanResult Fail(
        FetchAppHttpCompletionPlanStatus status,
        in HttpTcpClientGetPlanResult httpPlan,
        ushort expectedHttpStatusCode,
        int expectedMarkerLength)
    {
        return new FetchAppHttpCompletionPlanResult(
            status,
            httpPlan.Status,
            httpPlan.HttpStatusCode,
            expectedHttpStatusCode,
            httpPlan.ResponseBytesRead,
            httpPlan.HttpBodyOffset,
            httpPlan.HttpBodyLength,
            expectedMarkerLength,
            NoMatchOffset,
            InitializedFlag);
    }
}

public static class FetchAppHttpCompletionPlan
{
    public static FetchAppHttpCompletionPlanResult Evaluate(
        in HttpTcpClientGetPlanResult httpPlan,
        ReadOnlySpan<byte> responseBuffer,
        ushort expectedHttpStatusCode,
        ReadOnlySpan<byte> expectedBodyMarker)
    {
        if (expectedBodyMarker.Length == 0)
        {
            return FetchAppHttpCompletionPlanResult.Fail(
                FetchAppHttpCompletionPlanStatus.InvalidExpectedMarker,
                in httpPlan,
                expectedHttpStatusCode,
                expectedBodyMarker.Length);
        }

        if (!httpPlan.IsInitialized || !httpPlan.Succeeded)
        {
            return FetchAppHttpCompletionPlanResult.Fail(
                FetchAppHttpCompletionPlanStatus.HttpPlanFailed,
                in httpPlan,
                expectedHttpStatusCode,
                expectedBodyMarker.Length);
        }

        if (httpPlan.HttpStatusCode != expectedHttpStatusCode)
        {
            return FetchAppHttpCompletionPlanResult.Fail(
                FetchAppHttpCompletionPlanStatus.UnexpectedHttpStatus,
                in httpPlan,
                expectedHttpStatusCode,
                expectedBodyMarker.Length);
        }

        FetchAppHttpCompletionPlanStatus rangeStatus =
            ValidateBodyRange(in httpPlan, responseBuffer.Length);
        if (rangeStatus != FetchAppHttpCompletionPlanStatus.Success)
        {
            return FetchAppHttpCompletionPlanResult.Fail(
                rangeStatus,
                in httpPlan,
                expectedHttpStatusCode,
                expectedBodyMarker.Length);
        }

        ReadOnlySpan<byte> body = responseBuffer.Slice(
            httpPlan.HttpBodyOffset,
            httpPlan.HttpBodyLength);
        int bodyMatchOffset = OS.Std.MemoryExtensions.IndexOf(body, expectedBodyMarker);
        if (bodyMatchOffset < 0)
        {
            return FetchAppHttpCompletionPlanResult.Fail(
                FetchAppHttpCompletionPlanStatus.MissingExpectedMarker,
                in httpPlan,
                expectedHttpStatusCode,
                expectedBodyMarker.Length);
        }

        return FetchAppHttpCompletionPlanResult.Success(
            httpPlan.Status,
            httpPlan.HttpStatusCode,
            expectedHttpStatusCode,
            httpPlan.ResponseBytesRead,
            httpPlan.HttpBodyOffset,
            httpPlan.HttpBodyLength,
            expectedBodyMarker.Length,
            httpPlan.HttpBodyOffset + bodyMatchOffset);
    }

    private static FetchAppHttpCompletionPlanStatus ValidateBodyRange(
        in HttpTcpClientGetPlanResult httpPlan,
        int responseBufferLength)
    {
        if (httpPlan.ResponseBytesRead < 0 ||
            httpPlan.HttpBodyOffset < 0 ||
            httpPlan.HttpBodyLength < 0 ||
            httpPlan.HttpBodyOffset > int.MaxValue - httpPlan.HttpBodyLength)
        {
            return FetchAppHttpCompletionPlanStatus.InvalidBodyRange;
        }

        int bodyEnd = httpPlan.HttpBodyOffset + httpPlan.HttpBodyLength;
        if (bodyEnd > httpPlan.ResponseBytesRead)
        {
            return FetchAppHttpCompletionPlanStatus.InvalidBodyRange;
        }

        if (responseBufferLength < httpPlan.ResponseBytesRead ||
            bodyEnd > responseBufferLength)
        {
            return FetchAppHttpCompletionPlanStatus.ResponseBufferTooSmall;
        }

        return FetchAppHttpCompletionPlanStatus.Success;
    }

}
