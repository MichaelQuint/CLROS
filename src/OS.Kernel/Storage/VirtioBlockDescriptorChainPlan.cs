using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockDescriptorChainPlan
{
    public const ushort RequiredDescriptorCount = VirtioBlockRequestPlan.RequiredDescriptorCount;
    public const bool PerformsDmaMapping = false;
    public const bool WritesQueueMemory = false;
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool PerformsDiskRead = false;

    private VirtioBlockDescriptorChainPlan(
        VirtioBlockReadTransactionPlan transactionPlan,
        VirtqueueSplitRingPlan queuePlan,
        ushort availableDescriptorCount,
        ushort availableRingIndex,
        ushort availableRingSlot,
        uint availableRingEntryOffset,
        VirtioBlockPublishedDescriptorPlan requestHeaderDescriptor,
        VirtioBlockPublishedDescriptorPlan dataBufferDescriptor,
        VirtioBlockPublishedDescriptorPlan statusByteDescriptor)
    {
        TransactionPlan = transactionPlan;
        QueuePlan = queuePlan;
        AvailableDescriptorCount = availableDescriptorCount;
        AvailableRingIndex = availableRingIndex;
        AvailableRingSlot = availableRingSlot;
        AvailableRingEntryOffset = availableRingEntryOffset;
        RequestHeaderDescriptor = requestHeaderDescriptor;
        DataBufferDescriptor = dataBufferDescriptor;
        StatusByteDescriptor = statusByteDescriptor;
    }

    public VirtioBlockReadTransactionPlan TransactionPlan { get; }

    public VirtioBlockRequestPlan RequestPlan => TransactionPlan.RequestPlan;

    public VirtqueueSplitRingPlan QueuePlan { get; }

    public ushort AvailableDescriptorCount { get; }

    public ushort AvailableRingIndex { get; }

    public ushort AvailableRingSlot { get; }

    public uint AvailableRingEntryOffset { get; }

    public ushort HeadDescriptorIndex => RequestHeaderDescriptor.DescriptorIndex;

    public VirtioBlockPublishedDescriptorPlan RequestHeaderDescriptor { get; }

    public VirtioBlockPublishedDescriptorPlan DataBufferDescriptor { get; }

    public VirtioBlockPublishedDescriptorPlan StatusByteDescriptor { get; }

    public static VirtioBlockDescriptorChainPlanResult Create(
        VirtioBlockReadTransactionPlanResult transactionPlan,
        VirtqueueSplitRingPlanResult queuePlan,
        ushort firstDescriptorIndex,
        ushort availableDescriptorCount,
        ushort availableRingIndex,
        ulong requestHeaderPhysicalAddress,
        ulong dataBufferPhysicalAddress,
        ulong statusBytePhysicalAddress)
    {
        if (!transactionPlan.Succeeded)
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.TransactionPlanFailed,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status);
        }

        if (!queuePlan.Succeeded)
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.QueuePlanFailed,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status);
        }

        VirtioBlockReadTransactionPlan transaction = transactionPlan.Plan;
        VirtqueueSplitRingPlan queue = queuePlan.Plan;
        VirtioBlockRequestPlan request = transaction.RequestPlan;

        if (request.RequestType != VirtioBlockRequestType.Read)
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.WriteRequestRejected,
                transactionPlan.Status,
                VirtioBlockRequestPlanStatus.WriteRejectedReadOnlyMode,
                queuePlan.Status);
        }

        if (transaction.QueueSize != queue.QueueSize)
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.QueueSizeMismatch,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status);
        }

        if (queue.QueueSize < RequiredDescriptorCount)
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.QueueTooSmall,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status);
        }

        if (availableDescriptorCount < RequiredDescriptorCount)
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.InsufficientDescriptors,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status);
        }

        if (!TryAddDescriptorIndex(firstDescriptorIndex, 1, out ushort dataDescriptorIndex) ||
            !TryAddDescriptorIndex(firstDescriptorIndex, 2, out ushort statusDescriptorIndex) ||
            statusDescriptorIndex >= queue.QueueSize)
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.DescriptorIndexOutOfRange,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status);
        }

        if (!queue.DescriptorTable.IsAligned ||
            queue.DescriptorTable.Alignment != VirtqueueSplitRingPlan.DescriptorTableAlignment)
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.DescriptorAlignmentInvalid,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status);
        }

        if (!TryDescriptorOffset(queue, firstDescriptorIndex, out uint headerDescriptorOffset) ||
            !TryDescriptorOffset(queue, dataDescriptorIndex, out uint dataDescriptorOffset) ||
            !TryDescriptorOffset(queue, statusDescriptorIndex, out uint statusDescriptorOffset))
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.DescriptorOffsetOverflow,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status);
        }

        if (!IsDescriptorOffsetAligned(headerDescriptorOffset) ||
            !IsDescriptorOffsetAligned(dataDescriptorOffset) ||
            !IsDescriptorOffsetAligned(statusDescriptorOffset))
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.DescriptorAlignmentInvalid,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status);
        }

        ushort availableRingSlot = (ushort)(availableRingIndex & (queue.QueueSize - 1));
        if (!TryAvailableRingEntryOffset(queue, availableRingSlot, out uint availableRingEntryOffset))
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.AvailableRingOffsetOverflow,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status);
        }

        if (!TryMemoryRange(
                VirtioBlockDescriptorChainRole.RequestHeader,
                requestHeaderPhysicalAddress,
                VirtioBlockRequestPlan.RequestHeaderLength,
                out VirtioBlockDescriptorMemoryRange requestHeaderRange) ||
            !TryMemoryRange(
                VirtioBlockDescriptorChainRole.DataBuffer,
                dataBufferPhysicalAddress,
                transaction.DataByteCount,
                out VirtioBlockDescriptorMemoryRange dataBufferRange) ||
            !TryMemoryRange(
                VirtioBlockDescriptorChainRole.StatusByte,
                statusBytePhysicalAddress,
                VirtioBlockRequestPlan.StatusLength,
                out VirtioBlockDescriptorMemoryRange statusByteRange))
        {
            return VirtioBlockDescriptorChainPlanResult.Fail(
                VirtioBlockDescriptorChainPlanStatus.PhysicalRangeOverflow,
                transactionPlan.Status,
                transactionPlan.RequestPlanStatus,
                queuePlan.Status);
        }

        VirtioBlockPublishedDescriptorPlan requestHeaderDescriptor =
            CreateDescriptor(
                VirtioBlockDescriptorChainRole.RequestHeader,
                request.HeaderDescriptor,
                firstDescriptorIndex,
                dataDescriptorIndex,
                requestHeaderPhysicalAddress,
                headerDescriptorOffset,
                requestHeaderRange);
        VirtioBlockPublishedDescriptorPlan dataBufferDescriptor =
            CreateDescriptor(
                VirtioBlockDescriptorChainRole.DataBuffer,
                request.DataDescriptor,
                dataDescriptorIndex,
                statusDescriptorIndex,
                dataBufferPhysicalAddress,
                dataDescriptorOffset,
                dataBufferRange);
        VirtioBlockPublishedDescriptorPlan statusByteDescriptor =
            CreateDescriptor(
                VirtioBlockDescriptorChainRole.StatusByte,
                request.StatusDescriptor,
                statusDescriptorIndex,
                nextDescriptorIndex: 0,
                statusBytePhysicalAddress,
                statusDescriptorOffset,
                statusByteRange);

        return VirtioBlockDescriptorChainPlanResult.Success(
            new VirtioBlockDescriptorChainPlan(
                transaction,
                queue,
                availableDescriptorCount,
                availableRingIndex,
                availableRingSlot,
                availableRingEntryOffset,
                requestHeaderDescriptor,
                dataBufferDescriptor,
                statusByteDescriptor),
            transactionPlan.Status,
            transactionPlan.RequestPlanStatus,
            queuePlan.Status);
    }

    private static VirtioBlockPublishedDescriptorPlan CreateDescriptor(
        VirtioBlockDescriptorChainRole role,
        VirtioBlockDescriptorMetadata metadata,
        ushort descriptorIndex,
        ushort nextDescriptorIndex,
        ulong physicalAddress,
        uint descriptorTableOffset,
        VirtioBlockDescriptorMemoryRange memoryRange)
    {
        return new VirtioBlockPublishedDescriptorPlan(
            role,
            descriptorIndex,
            physicalAddress,
            metadata.Length,
            metadata.HasNext,
            nextDescriptorIndex,
            metadata.DeviceWrites,
            metadata.Flags,
            descriptorTableOffset,
            memoryRange);
    }

    private static bool TryDescriptorOffset(
        VirtqueueSplitRingPlan queue,
        ushort descriptorIndex,
        out uint offset)
    {
        ulong value = (ulong)queue.DescriptorTable.Offset +
            ((ulong)descriptorIndex * VirtqueueSplitRingPlan.DescriptorLength);
        if (value > uint.MaxValue)
        {
            offset = 0;
            return false;
        }

        offset = (uint)value;
        return true;
    }

    private static bool IsDescriptorOffsetAligned(uint offset)
    {
        return (offset % VirtqueueSplitRingPlan.DescriptorTableAlignment) == 0;
    }

    private static bool TryAvailableRingEntryOffset(
        VirtqueueSplitRingPlan queue,
        ushort slot,
        out uint offset)
    {
        ulong value = (ulong)queue.AvailableRing.Offset +
            VirtqueueSplitRingPlan.AvailableRingHeaderLength +
            ((ulong)slot * VirtqueueSplitRingPlan.AvailableRingEntryLength);
        if (value > uint.MaxValue)
        {
            offset = 0;
            return false;
        }

        offset = (uint)value;
        return true;
    }

    private static bool TryAddDescriptorIndex(
        ushort value,
        ushort increment,
        out ushort result)
    {
        int computed = value + increment;
        if (computed > ushort.MaxValue)
        {
            result = 0;
            return false;
        }

        result = (ushort)computed;
        return true;
    }

    private static bool TryMemoryRange(
        VirtioBlockDescriptorChainRole role,
        ulong physicalAddress,
        uint length,
        out VirtioBlockDescriptorMemoryRange range)
    {
        if ((ulong)length > ulong.MaxValue - physicalAddress)
        {
            range = default;
            return false;
        }

        range = new VirtioBlockDescriptorMemoryRange(
            role,
            physicalAddress,
            length,
            physicalAddress + length);
        return true;
    }
}
