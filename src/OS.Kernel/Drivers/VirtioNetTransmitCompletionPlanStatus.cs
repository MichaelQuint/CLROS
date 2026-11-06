namespace OS.Drivers;

using OS.Std.Binary;

public enum VirtioNetTransmitCompletionPlanStatus
{
    Success = 0,
    TransmitPublishFailed = 1,
    NotificationFailed = 2,
    NotificationKindMismatch = 3,
    NotificationPublishMismatch = 4,
    QueueMemoryTooSmall = 5,
    NoCompletion = 6,
    UnexpectedUsedIndex = 7,
    UnexpectedDescriptorHead = 8,
    UnexpectedByteCount = 9
}
