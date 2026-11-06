namespace OS.Net;

using OS.Std.Binary;
using OS.Std.Memory;

public enum TcpPassiveIpv4FramePlanStatus
{
    Success = 0,
    InvalidLocalIdentity = 1,
    InvalidRemoteIdentity = 2,
    InvalidState = 3,
    FrameBufferTooSmall = 4,
    TcpSegmentWriteFailed = 5,
    FrameWriteFailed = 6,
    FrameParseFailed = 7,
    UnsupportedEtherType = 8,
    UnsupportedIpProtocol = 9,
    WrongLocalMac = 10,
    WrongRemoteMac = 11,
    WrongLocalAddress = 12,
    WrongRemoteAddress = 13,
    TcpSegmentParseFailed = 14,
    WrongTcpPorts = 15,
    InvalidTcpFlags = 16,
    WrongAcknowledgementNumber = 17,
    WrongSequenceNumber = 18,
    BadTcpChecksum = 19
}
