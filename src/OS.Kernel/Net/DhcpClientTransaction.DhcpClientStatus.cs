namespace OS.Net;
public enum DhcpClientStatus
{
    Success = 0,
    InvalidState = 1,
    BufferTooSmall = 2,
    MessageWriteFailed = 3,
    MessageParseFailed = 4,
    UnexpectedMessageType = 5,
    TransactionIdMismatch = 6,
    ClientHardwareAddressMismatch = 7,
    MissingServerIdentifier = 8,
    MissingOfferedAddress = 9,
    ServerIdentifierMismatch = 10,
    OfferedAddressMismatch = 11,
    MissingLeaseTime = 12,
    InvalidLeaseTime = 13,
    Timeout = 14
}
