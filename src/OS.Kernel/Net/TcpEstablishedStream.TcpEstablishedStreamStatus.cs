namespace OS.Net;

public enum TcpEstablishedStreamStatus
{
    Success = 0,
    InvalidState = 1,
    SegmentParseFailed = 2,
    SegmentWriteFailed = 3,
    UnexpectedPorts = 4,
    InvalidFlags = 5,
    NoPayload = 6,
    DuplicateSegment = 7,
    OutOfOrderSegment = 8,
    ReceiveBufferTooSmall = 9,
    NoData = 10,
    DestinationTooSmall = 11,
    InvalidMaximumPayloadLength = 12,
    InvalidAcknowledgementNumber = 13
}
