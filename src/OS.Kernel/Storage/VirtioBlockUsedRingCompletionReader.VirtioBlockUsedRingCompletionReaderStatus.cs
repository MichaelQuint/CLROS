using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockUsedRingCompletionReaderStatus
{
    Success = 0,
    InvalidQueueSize = 1,
    UsedRingTooSmall = 2,
    StatusBufferTooSmall = 3,
    NoCompletion = 4,
    UnexpectedUsedIndex = 5,
    UnexpectedDescriptorHead = 6,
    UnexpectedByteCount = 7,
    DeviceIoError = 8,
    DeviceUnsupported = 9,
    UnknownStatusByte = 10,
    ArithmeticOverflow = 11
}
