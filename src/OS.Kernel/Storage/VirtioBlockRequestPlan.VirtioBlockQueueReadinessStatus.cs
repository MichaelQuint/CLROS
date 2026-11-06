using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockQueueReadinessStatus
{
    Ready = 0,
    MissingReadOnlyFeature = 1,
    QueueTooSmall = 2,
    RequestPlanFailed = 3,
    WriteRejectedReadOnlyMode = 4
}
