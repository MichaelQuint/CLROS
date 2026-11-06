using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockReadQueueNotifyReadinessExecutor
{
    public const bool ConsumesReadQueuePublication = true;
    public const bool InvokesNotificationMetadataPlanner = true;
    public const bool InvokesNotifyOffsetPlanner = true;
    public const bool RequiresBarrierReadiness = true;
    public const bool RequiresRegisterWindowBinding = true;
    public const bool UsesCallerOwnedWindowBuffer = true;
    public const bool InvokesRegisterWindowBarrierExecutor = true;
    public const bool WritesCallerOwnedWindowBuffer = true;
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

    public static VirtioBlockReadQueueNotifyReadinessResult Execute(
        VirtioBlockReadQueuePublishResult publish,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        VirtioBlockQueueNotificationBarrierReadiness barrier,
        VirtioPciBarRegisterWindowBinding notifyConfiguration,
        Span<byte> notifyWindowBytes)
    {
        if (!publish.Succeeded)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.PublicationFailed,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                default,
                default,
                default,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: false,
                notifyOffsetPlanAttempted: false,
                notifyExecutionAttempted: false);
        }

        VirtioBlockQueueNotificationMetadataResult notificationMetadata =
            VirtioBlockQueueNotificationMetadataPlanner.Create(
                publish,
                queueIndex,
                queueNotifyOffset);
        if (!notificationMetadata.Succeeded)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.NotificationMetadataFailed,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                notificationMetadata,
                default,
                default,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: true,
                notifyOffsetPlanAttempted: false,
                notifyExecutionAttempted: false);
        }

        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset =
            VirtioBlockQueueNotifyOffsetPlanner.Create(
                notificationMetadata,
                notifyOffsetMultiplier);
        if (!notifyOffset.Succeeded)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.NotifyOffsetPlanFailed,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                notificationMetadata,
                notifyOffset,
                default,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: true,
                notifyOffsetPlanAttempted: true,
                notifyExecutionAttempted: false);
        }

        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult notifyExecution =
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutor.Execute(
                notifyOffset,
                barrier,
                notifyConfiguration,
                notifyWindowBytes);
        if (notifyExecution.Status ==
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.PlanFailed)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.RegisterWindowPlanFailed,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                notificationMetadata,
                notifyOffset,
                notifyExecution,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: true,
                notifyOffsetPlanAttempted: true,
                notifyExecutionAttempted: true);
        }

        if (notifyExecution.Status ==
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.MemoryBarrierNotReady,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                notificationMetadata,
                notifyOffset,
                notifyExecution,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: true,
                notifyOffsetPlanAttempted: true,
                notifyExecutionAttempted: true);
        }

        if (notifyExecution.Status ==
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.AccessExecutionFailed)
        {
            return VirtioBlockReadQueueNotifyReadinessResult.Create(
                VirtioBlockReadQueueNotifyReadinessStatus.RegisterWindowAccessFailed,
                publish.Status,
                queueIndex,
                queueNotifyOffset,
                notificationMetadata,
                notifyOffset,
                notifyExecution,
                barrier,
                notifyOffsetMultiplier,
                notificationMetadataAttempted: true,
                notifyOffsetPlanAttempted: true,
                notifyExecutionAttempted: true);
        }

        return VirtioBlockReadQueueNotifyReadinessResult.Create(
            VirtioBlockReadQueueNotifyReadinessStatus.Success,
            publish.Status,
            queueIndex,
            queueNotifyOffset,
            notificationMetadata,
            notifyOffset,
            notifyExecution,
            barrier,
            notifyOffsetMultiplier,
            notificationMetadataAttempted: true,
            notifyOffsetPlanAttempted: true,
            notifyExecutionAttempted: true);
    }
}
