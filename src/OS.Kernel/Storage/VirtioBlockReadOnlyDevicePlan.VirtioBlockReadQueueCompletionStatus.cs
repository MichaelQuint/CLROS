using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadQueueCompletionStatus
{
    Success = 0,
    PublicationFailed = 1,
    CursorReclaimFailed = 2
}
