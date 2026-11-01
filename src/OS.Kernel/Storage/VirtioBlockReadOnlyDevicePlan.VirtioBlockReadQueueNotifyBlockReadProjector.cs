using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockReadQueueNotifyBlockReadProjector
{
    public const bool ConsumesReadNotifyCompletionResult = true;
    public const bool DelegatesReadQueueBlockReadProjector = true;
    public const bool ReadsQueueMemory = false;
    public const bool ReadsStatusBuffer = false;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = false;
    public const bool MutatesCallerOwnedDescriptorStateTable = false;
    public const bool PerformsMmio = false;
    public const bool MapsBars = false;
    public const bool ContainsVolatileAccess = false;
    public const bool AccessesPhysicalAddresses = false;
    public const bool ExecutesMemoryBarriers = false;
    public const bool ConfiguresLiveQueues = false;
    public const bool NotifiesLiveDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool WaitsForUsedRingCompletion = false;

    public static VirtioBlockReadQueueNotifyBlockReadProjectionResult Project(
        VirtioBlockReadQueueNotifyCompletionResult notifyCompletion)
    {
        if (!notifyCompletion.CompletionAttempted)
        {
            return VirtioBlockReadQueueNotifyBlockReadProjectionResult.Create(
                VirtioBlockReadQueueNotifyBlockReadProjectionStatus.NotifyReadinessFailed,
                BlockReadResult.Fail(BlockDeviceStatus.IoError),
                notifyCompletion,
                default);
        }

        VirtioBlockReadQueueBlockReadProjectionResult readQueueProjection =
            VirtioBlockReadQueueBlockReadProjector.Project(notifyCompletion.Completion);
        if (notifyCompletion.Succeeded && readQueueProjection.Succeeded)
        {
            return VirtioBlockReadQueueNotifyBlockReadProjectionResult.Create(
                VirtioBlockReadQueueNotifyBlockReadProjectionStatus.Success,
                readQueueProjection.BlockRead,
                notifyCompletion,
                readQueueProjection);
        }

        return VirtioBlockReadQueueNotifyBlockReadProjectionResult.Create(
            VirtioBlockReadQueueNotifyBlockReadProjectionStatus.CompletionFailed,
            readQueueProjection.BlockRead,
            notifyCompletion,
            readQueueProjection);
    }
}
