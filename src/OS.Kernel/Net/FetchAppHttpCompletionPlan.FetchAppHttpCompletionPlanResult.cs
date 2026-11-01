namespace OS.Net;
public readonly struct FetchAppHttpCompletionPlanResult
{
    public const int NoMatchOffset = -1;
    private const uint InitializedFlag = 1;
    private FetchAppHttpCompletionPlanResult(FetchAppHttpCompletionPlanStatus status, HttpTcpClientGetPlanStatus httpPlanStatus, ushort httpStatusCode, ushort expectedHttpStatusCode, int responseBytesRead, int bodyOffset, int bodyLength, int expectedMarkerLength, int matchOffset, uint flags)
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
    public bool Succeeded => IsInitialized && Status == FetchAppHttpCompletionPlanStatus.Success && HttpPlanStatus == HttpTcpClientGetPlanStatus.Success && ExpectedMarkerLength > 0 && MatchOffset >= 0;

    public static FetchAppHttpCompletionPlanResult Success(HttpTcpClientGetPlanStatus httpPlanStatus, ushort httpStatusCode, ushort expectedHttpStatusCode, int responseBytesRead, int bodyOffset, int bodyLength, int expectedMarkerLength, int matchOffset)
    {
        return new FetchAppHttpCompletionPlanResult(FetchAppHttpCompletionPlanStatus.Success, httpPlanStatus, httpStatusCode, expectedHttpStatusCode, responseBytesRead, bodyOffset, bodyLength, expectedMarkerLength, matchOffset, InitializedFlag);
    }

    public static FetchAppHttpCompletionPlanResult Fail(FetchAppHttpCompletionPlanStatus status, in HttpTcpClientGetPlanResult httpPlan, ushort expectedHttpStatusCode, int expectedMarkerLength)
    {
        return new FetchAppHttpCompletionPlanResult(status, httpPlan.Status, httpPlan.HttpStatusCode, expectedHttpStatusCode, httpPlan.ResponseBytesRead, httpPlan.HttpBodyOffset, httpPlan.HttpBodyLength, expectedMarkerLength, NoMatchOffset, InitializedFlag);
    }
}
