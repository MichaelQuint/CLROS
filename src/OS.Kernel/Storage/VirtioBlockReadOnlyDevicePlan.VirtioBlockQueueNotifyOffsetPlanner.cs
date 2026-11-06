using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockQueueNotifyOffsetPlanner
{
    public const bool ConsumesNotificationMetadata = true;
    public const bool ComputesNotifyRelativeOffset = true;
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

    public static VirtioBlockQueueNotifyOffsetPlanResult Create(
        VirtioBlockQueueNotificationMetadataResult notification,
        uint notifyOffsetMultiplier)
    {
        if (!notification.Succeeded)
        {
            return VirtioBlockQueueNotifyOffsetPlanResult.Create(
                VirtioBlockQueueNotifyOffsetPlanStatus.NotificationMetadataFailed,
                notification,
                notifyOffsetMultiplier,
                notifyRelativeOffset: 0);
        }

        ulong relativeOffset = (ulong)notification.QueueNotifyOffset * notifyOffsetMultiplier;
        if (relativeOffset > uint.MaxValue)
        {
            return VirtioBlockQueueNotifyOffsetPlanResult.Create(
                VirtioBlockQueueNotifyOffsetPlanStatus.RelativeOffsetOverflow,
                notification,
                notifyOffsetMultiplier,
                notifyRelativeOffset: 0);
        }

        return VirtioBlockQueueNotifyOffsetPlanResult.Create(
            VirtioBlockQueueNotifyOffsetPlanStatus.Success,
            notification,
            notifyOffsetMultiplier,
            (uint)relativeOffset);
    }
}
