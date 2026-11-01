namespace OS.Drivers;
public enum VirtqueueSplitRingPlanStatus
{
    Success = 0,
    InvalidQueueSize = 1,
    QueueSizeNotPowerOfTwo = 2,
    QueueSizeTooLarge = 3,
    LayoutOverflow = 4
}
