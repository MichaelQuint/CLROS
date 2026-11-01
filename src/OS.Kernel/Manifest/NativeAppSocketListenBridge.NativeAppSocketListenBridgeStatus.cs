using OS.Pal;

namespace OS.Manifest;
public enum NativeAppSocketListenBridgeStatus
{
    Success = 0,
    PolicyRejected = 1,
    MissingSocketTable = 2,
    SocketCreateFailed = 3,
    SocketBindFailed = 4,
    SocketListenFailed = 5
}
