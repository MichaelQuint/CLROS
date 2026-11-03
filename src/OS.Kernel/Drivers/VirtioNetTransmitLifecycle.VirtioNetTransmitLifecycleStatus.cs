namespace OS.Drivers;

using OS.Std.Binary;

public enum VirtioNetTransmitLifecycleStatus
{
    Success = 0,
    QueuePlanFailed = 1,
    HeaderBufferTooSmall = 2,
    EthernetFrameTooSmall = 3,
    EthernetFrameTooLarge = 4,
    InsufficientDescriptors = 5,
    DescriptorIndexOutOfRange = 6,
    DescriptorOffsetOverflow = 7,
    DescriptorAlignmentInvalid = 8,
    AvailableRingOffsetOverflow = 9,
    PhysicalRangeOverflow = 10,
    QueueMemoryTooSmall = 11,
    ArithmeticOverflow = 12,
    HeaderWriteFailed = 13
}
