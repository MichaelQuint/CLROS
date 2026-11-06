namespace OS.Net;

using OS.Std.Binary;

public readonly struct HttpTcpClientGetPlannerInfo
{
    public const int DefaultMaximumRequestPayloadBytes = 1460;
    public HttpTcpClientGetPlannerInfo(TcpIpv4FramePlannerInfo tcpInfo, int maximumRequestPayloadBytes = DefaultMaximumRequestPayloadBytes)
    {
        TcpInfo = tcpInfo;
        MaximumRequestPayloadBytes = maximumRequestPayloadBytes <= 0 ? DefaultMaximumRequestPayloadBytes : maximumRequestPayloadBytes;
    }

    public TcpIpv4FramePlannerInfo TcpInfo
    {
        get;
    }
    public int MaximumRequestPayloadBytes
    {
        get;
    }
}
