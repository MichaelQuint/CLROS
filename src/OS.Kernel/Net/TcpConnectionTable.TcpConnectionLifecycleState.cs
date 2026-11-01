namespace OS.Net;
public enum TcpConnectionLifecycleState
{
    Free = 0,
    Established = 1,
    Closing = 2,
    Closed = 3,
    SynReceived = 4
}
