using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockReadQueueCompletionPlanner
{
    public const bool RequiresSuccessfulReadQueuePublication = true;
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

    public static VirtioBlockReadQueueCompletionResult CompleteRead(
        VirtioBlockReadQueuePublishResult publish,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> statusBuffer,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        ref VirtioBlockQueueCursorState cursor)
    {
        ushort previousAvailableRingIndex = cursor.AvailableRingIndex;
        ushort previousUsedRingIndex = cursor.UsedRingIndex;
        if (!publish.Succeeded)
        {
            return VirtioBlockReadQueueCompletionResult.Create(
                VirtioBlockReadQueueCompletionStatus.PublicationFailed,
                publish,
                default,
                cursorReclaimAttempted: false,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        VirtioBlockQueueCursorReclaimResult cursorReclaim =
            VirtioBlockQueueCursorPlanner.CompletePublishedRead(
                publish.CursorPublish.Publish,
                queueMemory,
                statusBuffer,
                descriptorStates,
                ref cursor);
        if (!cursorReclaim.Succeeded)
        {
            return VirtioBlockReadQueueCompletionResult.Create(
                VirtioBlockReadQueueCompletionStatus.CursorReclaimFailed,
                publish,
                cursorReclaim,
                cursorReclaimAttempted: true,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        return VirtioBlockReadQueueCompletionResult.Create(
            VirtioBlockReadQueueCompletionStatus.Success,
            publish,
            cursorReclaim,
            cursorReclaimAttempted: true,
            previousAvailableRingIndex,
            cursor.AvailableRingIndex,
            previousUsedRingIndex,
            cursor.UsedRingIndex);
    }
}
