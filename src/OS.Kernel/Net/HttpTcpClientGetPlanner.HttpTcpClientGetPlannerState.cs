namespace OS.Net;

using OS.Std.Binary;

public enum HttpTcpClientGetPlannerState
{
    Initial = 0,
    SynSent = 1,
    Established = 2,
    RequestWritten = 3,
    ResponseReceived = 4,
    Failed = 5
}
