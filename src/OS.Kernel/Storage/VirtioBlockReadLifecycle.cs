using OS.Drivers;

namespace OS.Storage;

public static class VirtioBlockReadLifecycle
{
    public const bool PerformsDmaMapping = false;
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool CopiesPayload = false;
    public const bool PerformsDiskRead = false;

    public static VirtioBlockReadLifecyclePublishResult PublishReadRequest(
        uint sectorSizeBytes,
        ulong deviceSectorCount,
        ulong firstSector,
        uint sectorCount,
        ushort queueSize,
        ushort firstDescriptorIndex,
        ushort availableDescriptorCount,
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
        VirtioBlockReadTransactionPlanResult transactionPlan =
            VirtioBlockReadTransactionPlan.Create(
                sectorSizeBytes,
                deviceSectorCount,
                firstSector,
                sectorCount,
                queueSize,
                requestHeader,
                destination,
                statusBuffer);
        if (!transactionPlan.Succeeded)
        {
            return VirtioBlockReadLifecyclePublishResult.Fail(
                VirtioBlockReadLifecyclePublishStatus.ReadPlanFailed,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                VirtqueueSplitRingPlanStatus.Success,
                VirtioBlockDescriptorChainPlanStatus.Success,
                VirtioBlockDescriptorChainWriterStatus.Success,
                default,
                firstSector,
                sectorCount,
                queueSize,
                previousAvailableRingIndex,
                previousUsedIndex);
        }

        VirtqueueSplitRingPlanResult queuePlan = VirtqueueSplitRingPlan.Create(queueSize);
        if (!queuePlan.Succeeded)
        {
            return VirtioBlockReadLifecyclePublishResult.Fail(
                VirtioBlockReadLifecyclePublishStatus.QueuePlanFailed,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status,
                VirtioBlockDescriptorChainPlanStatus.Success,
                VirtioBlockDescriptorChainWriterStatus.Success,
                default,
                firstSector,
                sectorCount,
                queueSize,
                previousAvailableRingIndex,
                previousUsedIndex);
        }

        VirtioBlockDescriptorChainPlanResult descriptorPlan =
            VirtioBlockDescriptorChainPlan.Create(
                transactionPlan,
                queuePlan,
                firstDescriptorIndex,
                availableDescriptorCount,
                previousAvailableRingIndex,
                requestHeaderPhysicalAddress,
                dataBufferPhysicalAddress,
                statusBytePhysicalAddress);
        if (!descriptorPlan.Succeeded)
        {
            return VirtioBlockReadLifecyclePublishResult.Fail(
                VirtioBlockReadLifecyclePublishStatus.DescriptorPlanFailed,
                descriptorPlan.TransactionPlanStatus,
                descriptorPlan.RequestPlanStatus,
                descriptorPlan.QueuePlanStatus,
                descriptorPlan.Status,
                VirtioBlockDescriptorChainWriterStatus.Success,
                queuePlan.Plan,
                firstSector,
                sectorCount,
                queueSize,
                previousAvailableRingIndex,
                previousUsedIndex);
        }

        VirtioBlockDescriptorChainWriterResult writer =
            VirtioBlockDescriptorChainWriter.Write(
                descriptorPlan,
                queuePlan,
                queueMemory);
        if (!writer.Succeeded)
        {
            return VirtioBlockReadLifecyclePublishResult.Fail(
                VirtioBlockReadLifecyclePublishStatus.DescriptorWriteFailed,
                descriptorPlan.TransactionPlanStatus,
                descriptorPlan.RequestPlanStatus,
                writer.QueuePlanStatus,
                writer.DescriptorPlanStatus,
                writer.Status,
                queuePlan.Plan,
                firstSector,
                sectorCount,
                queueSize,
                previousAvailableRingIndex,
                previousUsedIndex);
        }

        return VirtioBlockReadLifecyclePublishResult.Success(
            transactionPlan.Plan,
            descriptorPlan.Plan,
            writer,
            previousUsedIndex);
    }

    public static VirtioBlockReadLifecycleCompletionResult CompleteReadRequest(
        VirtioBlockReadLifecyclePublishResult publish,
        ReadOnlySpan<byte> queueMemory,
        ReadOnlySpan<byte> statusBuffer)
    {
        if (!publish.Succeeded)
        {
            VirtioBlockUsedRingCompletionReaderResult notRead =
                VirtioBlockUsedRingCompletionReaderResult.Fail(
                    VirtioBlockUsedRingCompletionReaderStatus.NoCompletion,
                    publish.QueuePlanStatus,
                    publish.PreviousUsedIndex);
            return VirtioBlockReadLifecycleCompletionResult.Fail(
                VirtioBlockReadLifecycleCompletionStatus.PublishFailed,
                publish,
                notRead);
        }

        ReadOnlySpan<byte> usedRing = GetUsedRingSlice(
            publish.QueuePlan,
            queueMemory);
        VirtioBlockUsedRingCompletionReaderResult completion =
            VirtioBlockUsedRingCompletionReader.Read(
                publish.QueueSize,
                publish.PreviousUsedIndex,
                publish.DescriptorHead,
                publish.DataByteCount,
                statusBuffer,
                usedRing);

        if (!completion.Succeeded)
        {
            return VirtioBlockReadLifecycleCompletionResult.Fail(
                VirtioBlockReadLifecycleCompletionStatus.CompletionFailed,
                publish,
                completion);
        }

        return VirtioBlockReadLifecycleCompletionResult.Success(
            publish,
            completion);
    }

    private static ReadOnlySpan<byte> GetUsedRingSlice(
        VirtqueueSplitRingPlan queuePlan,
        ReadOnlySpan<byte> queueMemory)
    {
        uint usedRingOffset = queuePlan.UsedRing.Offset;
        if (usedRingOffset > int.MaxValue ||
            usedRingOffset > (uint)queueMemory.Length)
        {
            return ReadOnlySpan<byte>.Empty;
        }

        return queueMemory.Slice((int)usedRingOffset);
    }
}
