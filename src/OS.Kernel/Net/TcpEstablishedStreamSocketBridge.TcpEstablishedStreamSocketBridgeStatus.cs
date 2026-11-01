using OS.Pal;

namespace OS.Net;
public enum TcpEstablishedStreamSocketBridgeStatus
{
    Success = 0,
    MissingSocketTable = 1,
    ReceiveNotSuccessful = 2,
    PayloadLengthMismatch = 3,
    SocketQueueFailed = 4
}
