namespace OS.Net;
using OS.Std.Binary;

public enum HttpTcpClientGetPlanStatus
{
    Success = 0,
    InvalidState = 1,
    RequestBufferTooSmall = 2,
    RequestTooLarge = 3,
    ResponseBufferTooSmall = 4,
    TcpFrameFailed = 5,
    FrameBufferTooSmall = 6,
    FrameWriteFailed = 7,
    FrameParseFailed = 8,
    UnsupportedEtherType = 9,
    UnsupportedIpProtocol = 10,
    WrongLocalMac = 11,
    WrongRemoteMac = 12,
    WrongLocalAddress = 13,
    WrongRemoteAddress = 14,
    TcpSegmentParseFailed = 15,
    WrongTcpPorts = 16,
    InvalidTcpFlags = 17,
    WrongAcknowledgementNumber = 18,
    TcpStreamSendFailed = 19,
    TcpStreamReceiveFailed = 20,
    TcpStreamReadFailed = 21,
    HttpRequestFailed = 22,
    HttpResponseFailed = 23
}
