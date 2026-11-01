namespace OS.Net;
using OS.Drivers;
using OS.Std.Binary;

public enum TcpReceiveDispatchStatus
{
    Success = 0,
    NetworkDispatchFailed = 1,
    NonIpv4Payload = 2,
    UnsupportedIpProtocol = 3,
    InvalidPayloadRange = 4,
    TcpParseFailed = 5,
    BadTcpChecksum = 6,
    ConnectionMismatch = 7,
    TcpStreamFailed = 8,
    CloseFailed = 9,
    AckChecksumFailed = 10,
    UnsupportedTcpFlags = 11,
    ControlSegmentRejected = 12
}
