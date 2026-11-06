using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockDescriptorOwnershipPlan
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
    public const bool WritesQueueMemory = false;
    public const bool MutatesCallerOwnedStateTable = true;

    public static VirtioBlockDescriptorOwnershipPlanResult MarkPublished(
        VirtioBlockReadLifecyclePublishResult publish,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates)
    {
        if (!IsValidPublish(publish))
        {
            return CreateFailure(
                VirtioBlockDescriptorOwnershipStatus.PublishFailed,
                publish.Status,
                VirtioBlockReadLifecycleCompletionStatus.Success,
                VirtioBlockUsedRingCompletionReaderStatus.Success,
                requestHeaderDescriptorIndex: 0,
                dataBufferDescriptorIndex: 0,
                statusByteDescriptorIndex: 0,
                descriptorStates.Length,
                requiredStateTableLength: 0);
        }

        GetDescriptorIndexes(
            publish.DescriptorHead,
            out ushort requestHeaderDescriptorIndex,
            out ushort dataBufferDescriptorIndex,
            out ushort statusByteDescriptorIndex);
        int requiredStateTableLength = GetRequiredStateTableLength(statusByteDescriptorIndex);
        if (descriptorStates.Length < requiredStateTableLength)
        {
            return CreateFailure(
                VirtioBlockDescriptorOwnershipStatus.StateTableTooSmall,
                publish.Status,
                VirtioBlockReadLifecycleCompletionStatus.Success,
                VirtioBlockUsedRingCompletionReaderStatus.Success,
                requestHeaderDescriptorIndex,
                dataBufferDescriptorIndex,
                statusByteDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        if (descriptorStates[requestHeaderDescriptorIndex] != VirtioBlockDescriptorOwnershipState.Free ||
            descriptorStates[dataBufferDescriptorIndex] != VirtioBlockDescriptorOwnershipState.Free ||
            descriptorStates[statusByteDescriptorIndex] != VirtioBlockDescriptorOwnershipState.Free)
        {
            return CreateFailure(
                VirtioBlockDescriptorOwnershipStatus.DescriptorAlreadyInFlight,
                publish.Status,
                VirtioBlockReadLifecycleCompletionStatus.Success,
                VirtioBlockUsedRingCompletionReaderStatus.Success,
                requestHeaderDescriptorIndex,
                dataBufferDescriptorIndex,
                statusByteDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        descriptorStates[requestHeaderDescriptorIndex] = VirtioBlockDescriptorOwnershipState.InFlight;
        descriptorStates[dataBufferDescriptorIndex] = VirtioBlockDescriptorOwnershipState.InFlight;
        descriptorStates[statusByteDescriptorIndex] = VirtioBlockDescriptorOwnershipState.InFlight;

        return VirtioBlockDescriptorOwnershipPlanResult.Create(
            VirtioBlockDescriptorOwnershipStatus.Success,
            publish.Status,
            VirtioBlockReadLifecycleCompletionStatus.Success,
            VirtioBlockUsedRingCompletionReaderStatus.Success,
            requestHeaderDescriptorIndex,
            dataBufferDescriptorIndex,
            statusByteDescriptorIndex,
            descriptorStates.Length,
            requiredStateTableLength,
            VirtioBlockDescriptorChainPlan.RequiredDescriptorCount,
            descriptorsFreed: 0);
    }

    public static VirtioBlockDescriptorOwnershipPlanResult ApplyCompletion(
        VirtioBlockReadLifecycleCompletionResult completion,
        Span<VirtioBlockDescriptorOwnershipState> descriptorStates)
    {
        if (!IsValidCompletion(completion))
        {
            return CreateFailure(
                VirtioBlockDescriptorOwnershipStatus.CompletionFailed,
                completion.PublishStatus,
                completion.Status,
                completion.CompletionReaderStatus,
                requestHeaderDescriptorIndex: 0,
                dataBufferDescriptorIndex: 0,
                statusByteDescriptorIndex: 0,
                descriptorStates.Length,
                requiredStateTableLength: 0);
        }

        GetDescriptorIndexes(
            completion.DescriptorHead,
            out ushort requestHeaderDescriptorIndex,
            out ushort dataBufferDescriptorIndex,
            out ushort statusByteDescriptorIndex);
        int requiredStateTableLength = GetRequiredStateTableLength(statusByteDescriptorIndex);
        if (descriptorStates.Length < requiredStateTableLength)
        {
            return CreateFailure(
                VirtioBlockDescriptorOwnershipStatus.StateTableTooSmall,
                completion.PublishStatus,
                completion.Status,
                completion.CompletionReaderStatus,
                requestHeaderDescriptorIndex,
                dataBufferDescriptorIndex,
                statusByteDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        bool requestHeaderInFlight =
            descriptorStates[requestHeaderDescriptorIndex] == VirtioBlockDescriptorOwnershipState.InFlight;
        bool dataBufferInFlight =
            descriptorStates[dataBufferDescriptorIndex] == VirtioBlockDescriptorOwnershipState.InFlight;
        bool statusByteInFlight =
            descriptorStates[statusByteDescriptorIndex] == VirtioBlockDescriptorOwnershipState.InFlight;
        if (!requestHeaderInFlight || !dataBufferInFlight || !statusByteInFlight)
        {
            VirtioBlockDescriptorOwnershipStatus status =
                IsFree(descriptorStates[requestHeaderDescriptorIndex]) &&
                IsFree(descriptorStates[dataBufferDescriptorIndex]) &&
                IsFree(descriptorStates[statusByteDescriptorIndex])
                    ? VirtioBlockDescriptorOwnershipStatus.DuplicateCompletion
                    : VirtioBlockDescriptorOwnershipStatus.DescriptorNotInFlight;

            return CreateFailure(
                status,
                completion.PublishStatus,
                completion.Status,
                completion.CompletionReaderStatus,
                requestHeaderDescriptorIndex,
                dataBufferDescriptorIndex,
                statusByteDescriptorIndex,
                descriptorStates.Length,
                requiredStateTableLength);
        }

        descriptorStates[requestHeaderDescriptorIndex] = VirtioBlockDescriptorOwnershipState.Free;
        descriptorStates[dataBufferDescriptorIndex] = VirtioBlockDescriptorOwnershipState.Free;
        descriptorStates[statusByteDescriptorIndex] = VirtioBlockDescriptorOwnershipState.Free;

        return VirtioBlockDescriptorOwnershipPlanResult.Create(
            VirtioBlockDescriptorOwnershipStatus.Success,
            completion.PublishStatus,
            completion.Status,
            completion.CompletionReaderStatus,
            requestHeaderDescriptorIndex,
            dataBufferDescriptorIndex,
            statusByteDescriptorIndex,
            descriptorStates.Length,
            requiredStateTableLength,
            descriptorsMarkedInFlight: 0,
            VirtioBlockDescriptorChainPlan.RequiredDescriptorCount);
    }

    private static bool IsValidPublish(VirtioBlockReadLifecyclePublishResult publish)
    {
        return publish.Succeeded &&
            publish.DescriptorsWritten == VirtioBlockDescriptorChainPlan.RequiredDescriptorCount &&
            VirtqueueSplitRingPlan.ValidateQueueSize(publish.QueueSize) == VirtqueueSplitRingPlanStatus.Success &&
            publish.QueueSize >= VirtioBlockDescriptorChainPlan.RequiredDescriptorCount &&
            (int)publish.DescriptorHead + VirtioBlockDescriptorChainPlan.RequiredDescriptorCount <= publish.QueueSize;
    }

    private static bool IsValidCompletion(VirtioBlockReadLifecycleCompletionResult completion)
    {
        return completion.Succeeded &&
            completion.CompletedDescriptorHead == completion.DescriptorHead &&
            (int)completion.DescriptorHead + VirtioBlockDescriptorChainPlan.RequiredDescriptorCount <=
                ushort.MaxValue + 1;
    }

    private static void GetDescriptorIndexes(
        ushort firstDescriptorIndex,
        out ushort requestHeaderDescriptorIndex,
        out ushort dataBufferDescriptorIndex,
        out ushort statusByteDescriptorIndex)
    {
        requestHeaderDescriptorIndex = firstDescriptorIndex;
        dataBufferDescriptorIndex = (ushort)(firstDescriptorIndex + 1);
        statusByteDescriptorIndex = (ushort)(firstDescriptorIndex + 2);
    }

    private static int GetRequiredStateTableLength(ushort statusByteDescriptorIndex)
    {
        return statusByteDescriptorIndex + 1;
    }

    private static bool IsFree(VirtioBlockDescriptorOwnershipState state)
    {
        return state == VirtioBlockDescriptorOwnershipState.Free;
    }

    private static VirtioBlockDescriptorOwnershipPlanResult CreateFailure(
        VirtioBlockDescriptorOwnershipStatus status,
        VirtioBlockReadLifecyclePublishStatus publishStatus,
        VirtioBlockReadLifecycleCompletionStatus completionStatus,
        VirtioBlockUsedRingCompletionReaderStatus completionReaderStatus,
        ushort requestHeaderDescriptorIndex,
        ushort dataBufferDescriptorIndex,
        ushort statusByteDescriptorIndex,
        int stateTableLength,
        int requiredStateTableLength)
    {
        return VirtioBlockDescriptorOwnershipPlanResult.Create(
            status,
            publishStatus,
            completionStatus,
            completionReaderStatus,
            requestHeaderDescriptorIndex,
            dataBufferDescriptorIndex,
            statusByteDescriptorIndex,
            stateTableLength,
            requiredStateTableLength,
            descriptorsMarkedInFlight: 0,
            descriptorsFreed: 0);
    }
}
