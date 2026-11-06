using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadQueueNotifyBlockReadProjectionStatus
{
    Success = 0,
    NotifyReadinessFailed = 1,
    CompletionFailed = 2
}
