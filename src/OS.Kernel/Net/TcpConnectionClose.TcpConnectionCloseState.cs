namespace OS.Net;

public enum TcpConnectionCloseState
{
    Established = 0,
    FinWait1 = 1,
    FinWait2 = 2,
    CloseWait = 3,
    LastAck = 4,
    TimeWait = 5,
    Closed = 6,
    Reset = 7,
    Closing = 8
}
