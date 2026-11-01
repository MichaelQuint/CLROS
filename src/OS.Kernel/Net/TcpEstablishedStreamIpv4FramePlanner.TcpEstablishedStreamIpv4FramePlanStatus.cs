namespace OS.Net;
using OS.Std.Binary;
using OS.Std.Memory;

public enum TcpEstablishedStreamIpv4FramePlanStatus
{
    Success = 0,
    InvalidLocalIdentity = 1,
    InvalidRemoteIdentity = 2,
    InvalidMaximumPayloadLength = 3,
    FrameBufferTooSmall = 4,
    TcpStreamFailed = 5,
    TcpChecksumFailed = 6,
    FrameWriteFailed = 7,
    FrameParseFailed = 8,
    UnsupportedEtherType = 9,
    UnsupportedIpProtocol = 10,
    WrongLocalMac = 11,
    WrongRemoteMac = 12,
    WrongLocalAddress = 13,
    WrongRemoteAddress = 14,
    TcpSegmentParseFailed = 15,
    BadTcpChecksum = 16
}
