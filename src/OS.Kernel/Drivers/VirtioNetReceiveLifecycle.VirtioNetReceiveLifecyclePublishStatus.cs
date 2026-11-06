namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public enum VirtioNetReceiveLifecyclePublishStatus
{
    Success = 0,
    QueuePlanFailed = 1,
    InvalidReceiveBufferCount = 2,
    InvalidFrameBufferSize = 3,
    InvalidReceiveBufferStride = 4,
    InsufficientDescriptors = 5,
    DescriptorRangeOutOfRange = 6,
    QueueMemoryTooSmall = 7,
    ArithmeticOverflow = 8
}
