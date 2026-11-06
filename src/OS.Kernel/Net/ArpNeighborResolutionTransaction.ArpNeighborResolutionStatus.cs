namespace OS.Net;

public enum ArpNeighborResolutionStatus
{
    Success = 0,
    InvalidState = 1,
    BufferTooSmall = 2,
    FrameWriteFailed = 3,
    FrameParseFailed = 4,
    UnsupportedEtherType = 5,
    UnexpectedOperation = 6,
    SenderAddressMismatch = 7,
    TargetAddressMismatch = 8,
    TargetMacMismatch = 9,
    EthernetDestinationMismatch = 10,
    EthernetSourceMismatch = 11,
    Timeout = 12
}
