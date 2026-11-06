using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockDescriptorReclaimPlanner
{
    public const bool RequiresSuccessfulPublishPlan = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedStatusBuffer = true;
    public const bool RequiresCallerOwnedDescriptorStateTable = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsStatusBuffer = true;
    public const bool MutatesCallerOwnedStateTable = true;
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
    public const bool WritesQueueMemory = false;
    public const bool WritesRequestHeader = false;

    public static VirtioBlockDescriptorReclaimPlanResult CompletePublishedRead(
        VirtioBlockDescriptorPublishPlanResult publish,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> statusBuffer,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates)
    {
        if (!publish.Succeeded)
        {
            return VirtioBlockDescriptorReclaimPlanResult.Create(
                VirtioBlockDescriptorReclaimPlanStatus.PublishPlanFailed,
                publish,
                default,
                default,
                completionAttempted: false,
                ownershipReclaimAttempted: false,
                descriptorStates.Length);
        }

        VirtioBlockReadLifecycleCompletionResult completion =
            VirtioBlockReadLifecycle.CompleteReadRequest(
                publish.LifecyclePublish,
                queueMemory,
                statusBuffer);
        if (!completion.Succeeded)
        {
            return VirtioBlockDescriptorReclaimPlanResult.Create(
                VirtioBlockDescriptorReclaimPlanStatus.CompletionFailed,
                publish,
                completion,
                default,
                completionAttempted: true,
                ownershipReclaimAttempted: false,
                descriptorStates.Length);
        }

        VirtioBlockDescriptorOwnershipPlanResult ownershipReclaim =
            VirtioBlockDescriptorOwnershipPlan.ApplyCompletion(
                completion,
                descriptorStates);
        if (!ownershipReclaim.Succeeded)
        {
            return VirtioBlockDescriptorReclaimPlanResult.Create(
                VirtioBlockDescriptorReclaimPlanStatus.OwnershipReclaimFailed,
                publish,
                completion,
                ownershipReclaim,
                completionAttempted: true,
                ownershipReclaimAttempted: true,
                descriptorStates.Length);
        }

        return VirtioBlockDescriptorReclaimPlanResult.Create(
            VirtioBlockDescriptorReclaimPlanStatus.Success,
            publish,
            completion,
            ownershipReclaim,
            completionAttempted: true,
            ownershipReclaimAttempted: true,
            descriptorStates.Length);
    }
}
