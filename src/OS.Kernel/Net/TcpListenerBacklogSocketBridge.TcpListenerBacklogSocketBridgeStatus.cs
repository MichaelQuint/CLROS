using OS.Pal;

namespace OS.Net;

public enum TcpListenerBacklogSocketBridgeStatus
{
    Success = 0,
    MissingSocketTable = 1,
    BacklogUnavailable = 2,
    InvalidEndpoint = 3,
    SocketEnqueueFailed = 4,
    BacklogAcceptFailed = 5
}
