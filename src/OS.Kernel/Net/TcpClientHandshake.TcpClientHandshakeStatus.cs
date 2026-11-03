namespace OS.Net;

public enum TcpClientHandshakeStatus
{
    Success = 0,
    InvalidState = 1,
    BufferTooSmall = 2,
    SegmentWriteFailed = 3,
    SegmentParseFailed = 4,
    UnexpectedPorts = 5,
    InvalidFlags = 6,
    WrongAcknowledgementNumber = 7
}
