using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockDescriptorPublishPlanner
{
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDmaMapping = false;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsBlockIo = false;
    public const bool PerformsDiskRead = false;
    public const bool ReadsQueueMemory = false;
    public const bool WritesQueueMemory = true;
    public const bool WritesRequestHeader = true;
    public const bool MutatesCallerOwnedStateTable = true;

    public static VirtioBlockDescriptorPublishPlanResult PublishReadFromFreeList(
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ushort queueSize,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates,
        ushort previousAvailableRingIndex,
        ushort previousUsedIndex,
        ulong requestHeaderPhysicalAddress,
        ulong dataBufferPhysicalAddress,
        ulong statusBytePhysicalAddress,
        Span<byte> requestHeader,
        Span<byte> destination,
        Span<byte> statusBuffer,
        Span<byte> queueMemory)
    {
        VirtqueueSplitRingPlanResult queuePlan = VirtqueueSplitRingPlan.Create(queueSize);
        VirtioBlockDescriptorFreeListResult freeList =
            VirtioBlockDescriptorFreeListPlanner.SelectFreeDescriptorTriple(
                queuePlan,
                descriptorStates);
        if (!freeList.Succeeded)
        {
            return VirtioBlockDescriptorPublishPlanResult.Create(
                VirtioBlockDescriptorPublishPlanStatus.FreeListFailed,
                queuePlan.Status,
                freeList,
                default,
                default,
                lifecyclePublishAttempted: false,
                ownershipMarkAttempted: false);
        }

        VirtioBlockReadLifecyclePublishResult lifecyclePublish =
            VirtioBlockReadLifecycle.PublishReadRequest(
                sectorSizeBytes,
                deviceSectorCount,
                firstSector,
                sectorCount,
                queueSize,
                freeList.FirstDescriptorIndex,
                freeList.DescriptorsRequired,
                previousAvailableRingIndex,
                previousUsedIndex,
                requestHeaderPhysicalAddress,
                dataBufferPhysicalAddress,
                statusBytePhysicalAddress,
                requestHeader,
                destination,
                statusBuffer,
                queueMemory);
        if (!lifecyclePublish.Succeeded)
        {
            return VirtioBlockDescriptorPublishPlanResult.Create(
                VirtioBlockDescriptorPublishPlanStatus.LifecyclePublishFailed,
                queuePlan.Status,
                freeList,
                lifecyclePublish,
                default,
                lifecyclePublishAttempted: true,
                ownershipMarkAttempted: false);
        }

        VirtioBlockDescriptorOwnershipPlanResult ownershipMark =
            VirtioBlockDescriptorOwnershipPlan.MarkPublished(
                lifecyclePublish,
                descriptorStates);
        if (!ownershipMark.Succeeded)
        {
            return VirtioBlockDescriptorPublishPlanResult.Create(
                VirtioBlockDescriptorPublishPlanStatus.OwnershipMarkFailed,
                queuePlan.Status,
                freeList,
                lifecyclePublish,
                ownershipMark,
                lifecyclePublishAttempted: true,
                ownershipMarkAttempted: true);
        }

        return VirtioBlockDescriptorPublishPlanResult.Create(
            VirtioBlockDescriptorPublishPlanStatus.Success,
            queuePlan.Status,
            freeList,
            lifecyclePublish,
            ownershipMark,
            lifecyclePublishAttempted: true,
            ownershipMarkAttempted: true);
    }
}
