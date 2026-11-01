using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadQueueNotifyReadinessStatus
{
    Success = 0,
    PublicationFailed = 1,
    NotificationMetadataFailed = 2,
    NotifyOffsetPlanFailed = 3,
    RegisterWindowPlanFailed = 4,
    MemoryBarrierNotReady = 5,
    RegisterWindowAccessFailed = 6
}
