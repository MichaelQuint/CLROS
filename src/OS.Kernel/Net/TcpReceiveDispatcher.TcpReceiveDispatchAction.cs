namespace OS.Net;
using OS.Drivers;
using OS.Std.Binary;

public enum TcpReceiveDispatchAction
{
    None = 0,
    PayloadAccepted = 1,
    FinAcknowledged = 2,
    ResetReceived = 3
}
