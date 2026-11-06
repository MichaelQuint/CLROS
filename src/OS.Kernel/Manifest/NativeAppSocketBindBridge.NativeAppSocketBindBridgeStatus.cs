using OS.Pal;

namespace OS.Manifest;

public enum NativeAppSocketBindBridgeStatus
{
    Success = 0,
    PolicyRejected = 1,
    MissingSocketTable = 2,
    SocketBindFailed = 3
}
