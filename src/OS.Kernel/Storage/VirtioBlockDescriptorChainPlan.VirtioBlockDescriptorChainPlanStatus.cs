using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockDescriptorChainPlanStatus
{
    Success = 0,
    TransactionPlanFailed = 1,
    QueuePlanFailed = 2,
    QueueSizeMismatch = 3,
    QueueTooSmall = 4,
    InsufficientDescriptors = 5,
    DescriptorIndexOutOfRange = 6,
    DescriptorOffsetOverflow = 7,
    DescriptorAlignmentInvalid = 8,
    AvailableRingOffsetOverflow = 9,
    PhysicalRangeOverflow = 10,
    WriteRequestRejected = 11
}
