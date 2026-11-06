using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockDescriptorChainWriterStatus
{
    Success = 0,
    DescriptorPlanFailed = 1,
    QueuePlanFailed = 2,
    DescriptorIndexOutOfRange = 3,
    InvalidAvailableRingSlot = 4,
    QueuePlanMismatch = 5,
    QueueMemoryTooSmall = 6,
    ArithmeticOverflow = 7
}
