namespace OS.Net;

public readonly struct HttpStaticTcpResponseIpv4FramePlanResult
{
    private const uint InitializedFlag = 1;
    private HttpStaticTcpResponseIpv4FramePlanResult(HttpStaticTcpResponseIpv4FramePlanStatus status, HttpStaticTcpResponsePlanResult response, TcpEstablishedStreamIpv4FramePlanResult frame, uint flags)
    {
        Status = status;
        Response = response;
        Frame = frame;
        Flags = flags;
    }

    public HttpStaticTcpResponseIpv4FramePlanStatus Status
    {
        get;
    }
    public HttpStaticTcpResponsePlanResult Response
    {
        get;
    }
    public TcpEstablishedStreamIpv4FramePlanResult Frame
    {
        get;
    }
    public uint Flags
    {
        get;
    }
    public bool IsInitialized => (Flags & InitializedFlag) != 0;
    public bool Succeeded => IsInitialized && Status == HttpStaticTcpResponseIpv4FramePlanStatus.Success && Response.Succeeded && Frame.Succeeded && Response.ResponseBytesWritten > 0 && Frame.PayloadBytes == Response.ResponseBytesWritten;
    public int ResponseBytesWritten => Response.ResponseBytesWritten;
    public int FrameBytesWritten => Frame.BytesWritten;
    public int TcpSegmentOffset => Frame.TcpSegmentOffset;
    public int TcpSegmentLength => Frame.TcpSegmentLength;
    public int PayloadBytes => Frame.PayloadBytes;
    public ushort HttpStatusCode => Response.HttpStatusCode;
    public uint LocalNextSequenceNumber => Frame.LocalNextSequenceNumber;
    public uint RemoteNextSequenceNumber => Frame.RemoteNextSequenceNumber;

    public static HttpStaticTcpResponseIpv4FramePlanResult Create(HttpStaticTcpResponseIpv4FramePlanStatus status, HttpStaticTcpResponsePlanResult response, TcpEstablishedStreamIpv4FramePlanResult frame)
    {
        return new HttpStaticTcpResponseIpv4FramePlanResult(status, response, frame, InitializedFlag);
    }
}
