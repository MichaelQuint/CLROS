using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockReadQueueNotifyCompletionPlanner
{
    public const bool ConsumesReadNotifyReadiness = true;
    public const bool RequiresSuccessfulReadNotifyReadiness = true;
    public const bool DelegatesReadQueueCompletionPlanner = true;
    public const bool RequiresCallerOwnedCursorState = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedStatusBuffer = true;
    public const bool RequiresCallerOwnedDescriptorStateTable = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsStatusBuffer = true;
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;
    public const bool MutatesCallerOwnedCursorState = true;
    public const bool MutatesCallerOwnedDescriptorStateTable = true;
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

    public static VirtioBlockReadQueueNotifyCompletionResult CompleteAfterNotify(
        VirtioBlockReadQueuePublishResult publish,
        VirtioBlockReadQueueNotifyReadinessResult notifyReadiness,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> statusBuffer,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        ref VirtioBlockQueueCursorState cursor)
    {
        ushort previousAvailableRingIndex = cursor.AvailableRingIndex;
        ushort previousUsedRingIndex = cursor.UsedRingIndex;
        if (!notifyReadiness.Succeeded)
        {
            return VirtioBlockReadQueueNotifyCompletionResult.Create(
                VirtioBlockReadQueueNotifyCompletionStatus.NotifyReadinessFailed,
                notifyReadiness,
                default,
                publish.Status,
                completionAttempted: false,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        VirtioBlockReadQueueCompletionResult completion =
            VirtioBlockReadQueueCompletionPlanner.CompleteRead(
                publish,
                queueMemory,
                statusBuffer,
                descriptorStates,
                ref cursor);
        if (!completion.Succeeded)
        {
            return VirtioBlockReadQueueNotifyCompletionResult.Create(
                VirtioBlockReadQueueNotifyCompletionStatus.CompletionFailed,
                notifyReadiness,
                completion,
                publish.Status,
                completionAttempted: true,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        return VirtioBlockReadQueueNotifyCompletionResult.Create(
            VirtioBlockReadQueueNotifyCompletionStatus.Success,
            notifyReadiness,
            completion,
            publish.Status,
            completionAttempted: true,
            previousAvailableRingIndex,
            cursor.AvailableRingIndex,
            previousUsedRingIndex,
            cursor.UsedRingIndex);
    }
}
