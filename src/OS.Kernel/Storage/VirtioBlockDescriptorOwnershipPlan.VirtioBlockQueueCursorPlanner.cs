using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockQueueCursorPlanner
{
    public const bool RequiresCallerOwnedCursorState = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedStatusBuffer = true;
    public const bool RequiresCallerOwnedDescriptorStateTable = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsStatusBuffer = true;
    public const bool WritesQueueMemory = true;
    public const bool WritesRequestHeader = true;
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

    public static VirtioBlockQueueCursorPublishResult PublishReadFromFreeList(
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ushort queueSize,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        ref VirtioBlockQueueCursorState cursor,
        ulong requestHeaderPhysicalAddress,
        ulong dataBufferPhysicalAddress,
        ulong statusBytePhysicalAddress,
        Span<byte> requestHeader,
        Span<byte> destination,
        Span<byte> statusBuffer,
        Span<byte> queueMemory)
    {
        ushort previousAvailableRingIndex = cursor.AvailableRingIndex;
        ushort previousUsedRingIndex = cursor.UsedRingIndex;
        VirtioBlockDescriptorPublishPlanResult publish =
            VirtioBlockDescriptorPublishPlanner.PublishReadFromFreeList(
                sectorSizeBytes,
                deviceSectorCount,
                firstSector,
                sectorCount,
                queueSize,
                descriptorStates,
                previousAvailableRingIndex,
                previousUsedRingIndex,
                requestHeaderPhysicalAddress,
                dataBufferPhysicalAddress,
                statusBytePhysicalAddress,
                requestHeader,
                destination,
                statusBuffer,
                queueMemory);
        if (!publish.Succeeded)
        {
            return VirtioBlockQueueCursorPublishResult.Create(
                VirtioBlockQueueCursorPublishStatus.PublishPlanFailed,
                publish,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        cursor.AvailableRingIndex = publish.NextAvailableRingIndex;
        return VirtioBlockQueueCursorPublishResult.Create(
            VirtioBlockQueueCursorPublishStatus.Success,
            publish,
            previousAvailableRingIndex,
            cursor.AvailableRingIndex,
            previousUsedRingIndex,
            cursor.UsedRingIndex);
    }

    public static VirtioBlockQueueCursorReclaimResult CompletePublishedRead(
        VirtioBlockDescriptorPublishPlanResult publish,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> statusBuffer,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        ref VirtioBlockQueueCursorState cursor)
    {
        ushort previousAvailableRingIndex = cursor.AvailableRingIndex;
        ushort previousUsedRingIndex = cursor.UsedRingIndex;
        VirtioBlockDescriptorReclaimPlanResult reclaim =
            VirtioBlockDescriptorReclaimPlanner.CompletePublishedRead(
                publish,
                queueMemory,
                statusBuffer,
                descriptorStates);
        if (!reclaim.Succeeded)
        {
            return VirtioBlockQueueCursorReclaimResult.Create(
                VirtioBlockQueueCursorReclaimStatus.ReclaimPlanFailed,
                reclaim,
                previousAvailableRingIndex,
                cursor.AvailableRingIndex,
                previousUsedRingIndex,
                cursor.UsedRingIndex);
        }

        cursor.UsedRingIndex = reclaim.NextUsedIndex;
        return VirtioBlockQueueCursorReclaimResult.Create(
            VirtioBlockQueueCursorReclaimStatus.Success,
            reclaim,
            previousAvailableRingIndex,
            cursor.AvailableRingIndex,
            previousUsedRingIndex,
            cursor.UsedRingIndex);
    }
}
