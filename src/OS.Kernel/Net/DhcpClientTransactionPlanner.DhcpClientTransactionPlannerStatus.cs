namespace OS.Net;
public enum DhcpClientTransactionPlannerStatus
{
    Success = 0,
    InvalidClientIdentity = 1,
    InvalidState = 2,
    FrameBufferTooSmall = 3,
    FrameWriteFailed = 4,
    FrameReadFailed = 5,
    UdpReceiveFailed = 6,
    WrongUdpPorts = 7,
    MalformedPacket = 8,
    UnexpectedMessageType = 9,
    WrongTransactionId = 10,
    ClientHardwareAddressMismatch = 11,
    MissingServerIdentifier = 12,
    MissingOfferedAddress = 13,
    ServerIdentifierMismatch = 14,
    OfferedAddressMismatch = 15,
    MissingLeaseTime = 16,
    InvalidLeaseTime = 17,
    LeaseConfigurationFailed = 18,
    Timeout = 19
}
