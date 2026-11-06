using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadLifecycleCompletionStatus
{
    Success = 0,
    PublishFailed = 1,
    CompletionFailed = 2
}
