using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockReadQueueBlockReadProjector
{
    public const bool ConsumesReadQueueCompletionResult = true;
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

    public static VirtioBlockReadQueueBlockReadProjectionResult Project(
        VirtioBlockReadQueueCompletionResult completion)
    {
        if (completion.Succeeded)
        {
            return VirtioBlockReadQueueBlockReadProjectionResult.Create(
                VirtioBlockReadQueueBlockReadProjectionStatus.Success,
                BlockReadResult.Success(
                    completion.ReadQueuePublish.SectorCount,
                    completion.ReadQueuePublish.RequestedByteCount),
                completion);
        }

        if (completion.Status == VirtioBlockReadQueueCompletionStatus.PublicationFailed)
        {
            BlockDeviceStatus status = completion.ReadQueuePublish.BlockDeviceStatus;
            if (status == BlockDeviceStatus.Success)
            {
                status = BlockDeviceStatus.IoError;
            }

            return VirtioBlockReadQueueBlockReadProjectionResult.Create(
                VirtioBlockReadQueueBlockReadProjectionStatus.PublicationFailed,
                BlockReadResult.Fail(status),
                completion);
        }

        return VirtioBlockReadQueueBlockReadProjectionResult.Create(
            VirtioBlockReadQueueBlockReadProjectionStatus.CompletionFailed,
            BlockReadResult.Fail(BlockDeviceStatus.IoError),
            completion);
    }
}
