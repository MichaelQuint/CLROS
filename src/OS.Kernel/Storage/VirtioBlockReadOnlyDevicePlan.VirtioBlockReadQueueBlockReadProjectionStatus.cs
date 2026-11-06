using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadQueueBlockReadProjectionStatus
{
    Success = 0,
    PublicationFailed = 1,
    CompletionFailed = 2
}
