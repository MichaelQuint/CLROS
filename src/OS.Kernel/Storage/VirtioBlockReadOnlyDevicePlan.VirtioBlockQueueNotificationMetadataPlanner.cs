using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockQueueNotificationMetadataPlanner
{
    public const bool ConsumesReadQueuePublication = true;
    public const bool ProducesFutureNotifyWriteMetadata = true;
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

    public static VirtioBlockQueueNotificationMetadataResult Create(
        VirtioBlockReadQueuePublishResult publish,
        ushort queueIndex,
        ushort queueNotifyOffset)
    {
        if (!publish.Succeeded)
        {
            return VirtioBlockQueueNotificationMetadataResult.Create(
                VirtioBlockQueueNotificationMetadataStatus.PublicationFailed,
                publish,
                queueIndex,
                queueNotifyOffset);
        }

        return VirtioBlockQueueNotificationMetadataResult.Create(
            VirtioBlockQueueNotificationMetadataStatus.Success,
            publish,
            queueIndex,
            queueNotifyOffset);
    }
}
