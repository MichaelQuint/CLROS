using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadQueuePipelineStatus
{
    Success = 0,
    PublicationFailed = 1,
    NotifyReadinessFailed = 2,
    NotifyCompletionFailed = 3,
    BlockReadProjectionFailed = 4
}
