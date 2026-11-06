namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public enum TcpConnectionCloseIpv4FramePlanStatus
{
    Success = 0,
    InvalidLocalIdentity = 1,
    InvalidRemoteIdentity = 2,
    InvalidState = 3,
    FrameBufferTooSmall = 4,
    TcpSegmentWriteFailed = 5,
    FrameWriteFailed = 6
}
