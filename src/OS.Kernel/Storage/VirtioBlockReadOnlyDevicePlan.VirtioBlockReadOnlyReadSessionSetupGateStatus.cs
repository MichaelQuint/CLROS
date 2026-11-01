using OS.Drivers;

namespace OS.Storage;

public enum VirtioBlockReadOnlyReadSessionSetupGateStatus
{
    Success = 0,
    QueueSetupExecutionFailed = 1,
    QueueSetupBarrierNotReady = 2,
    QueueEnableNotAttempted = 3,
    ReadSessionFailed = 4
}
