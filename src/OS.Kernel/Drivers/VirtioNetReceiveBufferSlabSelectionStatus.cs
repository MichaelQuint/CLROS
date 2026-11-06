namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public enum VirtioNetReceiveBufferSlabSelectionStatus
{
    Success = 0,
    ReadinessFailed = 1,
    QueueMemoryTooSmall = 2,
    NoCompletion = 3,
    UnexpectedUsedIndex = 4,
    UnexpectedDescriptorHead = 5,
    ReceiveBufferSlabTooSmall = 6
}
