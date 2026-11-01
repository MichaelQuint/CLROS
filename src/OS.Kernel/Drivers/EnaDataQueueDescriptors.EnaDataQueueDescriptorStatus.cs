namespace OS.Drivers;
using OS.Std.Binary;

public enum EnaDataQueueDescriptorStatus
{
    Success = 0,
    BufferTooSmall = 1,
    InvalidLength = 2,
    PhysicalAddressOutOfRange = 3,
    ReservedBitsSet = 4,
    MissingRequiredControlBit = 5,
    UnsupportedOffloadMetadata = 6,
    CompletionRequestWithoutFirstDescriptor = 7,
    RequestIdMismatch = 8,
    PhaseMismatch = 9,
    QueueIdMismatch = 10,
    CompletionStatusNotSuccess = 11,
    CompletionNotBuffer = 12,
    CompletionNotSingleBuffer = 13,
    CompletionNotInitialized = 14
}
