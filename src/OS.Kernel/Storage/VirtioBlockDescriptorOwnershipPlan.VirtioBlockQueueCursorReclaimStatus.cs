using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockQueueCursorReclaimStatus
{
    Success = 0,
    ReclaimPlanFailed = 1
}
