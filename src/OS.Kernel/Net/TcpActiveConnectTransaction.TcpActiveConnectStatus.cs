namespace OS.Net;

public enum TcpActiveConnectStatus
{
    Success = 0,
    InvalidState = 1,
    BufferTooSmall = 2,
    SegmentWriteFailed = 3,
    SegmentParseFailed = 4,
    UnexpectedPorts = 5,
    InvalidFlags = 6,
    WrongAcknowledgementNumber = 7,
    WrongSequenceNumber = 8,
    Timeout = 9
}
