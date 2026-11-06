namespace OS.Net;

public readonly struct FetchAppHttpTcpExchangePlanResult
{
    private const uint InitializedFlag = 1;
    private FetchAppHttpTcpExchangePlanResult(FetchAppHttpTcpExchangePlanStatus status, FetchAppHttpTcpContextPlanResult context, HttpTcpClientGetPlanResult syn, HttpTcpClientGetPlanResult synAck, HttpTcpClientGetPlanResult finalAck, HttpTcpClientGetPlanResult request, HttpTcpClientGetPlanResult response, FetchAppHttpCompletionPlanResult completion, TcpConnectionCloseIpv4FramePlanResult close, int hostBytes, int pathBytes, ushort expectedHttpStatusCode, int expectedMarkerLength, uint flags)
    {
        Status = status;
        Context = context;
        Syn = syn;
        SynAck = synAck;
        FinalAck = finalAck;
        Request = request;
        Response = response;
        Completion = completion;
        Close = close;
        HostBytes = hostBytes;
        PathBytes = pathBytes;
        ExpectedHttpStatusCode = expectedHttpStatusCode;
        ExpectedMarkerLength = expectedMarkerLength;
        Flags = flags;
    }

    public FetchAppHttpTcpExchangePlanStatus Status
    {
        get;
    }
    public FetchAppHttpTcpContextPlanResult Context
    {
        get;
    }
    public HttpTcpClientGetPlanResult Syn
    {
        get;
    }
    public HttpTcpClientGetPlanResult SynAck
    {
        get;
    }
    public HttpTcpClientGetPlanResult FinalAck
    {
        get;
    }
    public HttpTcpClientGetPlanResult Request
    {
        get;
    }
    public HttpTcpClientGetPlanResult Response
    {
        get;
    }
    public FetchAppHttpCompletionPlanResult Completion
    {
        get;
    }
    public TcpConnectionCloseIpv4FramePlanResult Close
    {
        get;
    }
    public int HostBytes
    {
        get;
    }
    public int PathBytes
    {
        get;
    }
    public ushort ExpectedHttpStatusCode
    {
        get;
    }
    public int ExpectedMarkerLength
    {
        get;
    }
    public uint Flags
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == FetchAppHttpTcpExchangePlanStatus.Success && Context.Succeeded && Syn.Succeeded && SynAck.Succeeded && FinalAck.Succeeded && Request.Succeeded && Response.Succeeded && Completion.Succeeded && Close.Succeeded && HostBytes > 0 && PathBytes > 0 && ExpectedHttpStatusCode != 0 && ExpectedMarkerLength > 0;
    public int SynFrameBytes => Syn.BytesWritten;
    public int FinalAckFrameBytes => FinalAck.BytesWritten;
    public int RequestFrameBytes => Request.BytesWritten;
    public int ResponseBytesRead => Response.ResponseBytesRead;
    public int CloseFrameBytes => Close.BytesWritten;

    public static FetchAppHttpTcpExchangePlanResult Create(FetchAppHttpTcpExchangePlanStatus status, in FetchAppHttpTcpContextPlanResult context, in HttpTcpClientGetPlanResult syn, in HttpTcpClientGetPlanResult synAck, in HttpTcpClientGetPlanResult finalAck, in HttpTcpClientGetPlanResult request, in HttpTcpClientGetPlanResult response, in FetchAppHttpCompletionPlanResult completion, in TcpConnectionCloseIpv4FramePlanResult close, int hostBytes, int pathBytes, ushort expectedHttpStatusCode, int expectedMarkerLength)
    {
        return new FetchAppHttpTcpExchangePlanResult(status, context, syn, synAck, finalAck, request, response, completion, close, hostBytes, pathBytes, expectedHttpStatusCode, expectedMarkerLength, InitializedFlag);
    }
}
