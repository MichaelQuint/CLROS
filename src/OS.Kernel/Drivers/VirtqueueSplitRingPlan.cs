namespace OS.Drivers;
public readonly struct VirtqueueSplitRingPlan
{
    public const ushort MaximumQueueSize = 32768;
    public const uint DescriptorTableAlignment = 16;
    public const uint DescriptorLength = 16;
    public const uint AvailableRingAlignment = 2;
    public const uint AvailableRingHeaderLength = 4;
    public const uint AvailableRingEntryLength = 2;
    public const uint UsedRingAlignment = 4;
    public const uint UsedRingHeaderLength = 4;
    public const uint UsedRingElementLength = 8;
    private VirtqueueSplitRingPlan(ushort queueSize, uint queueStartOffset, VirtqueueSplitRingRegion descriptorTable, VirtqueueSplitRingRegion availableRing, VirtqueueSplitRingRegion usedRing)
    {
        QueueSize = queueSize;
        QueueStartOffset = queueStartOffset;
        DescriptorTable = descriptorTable;
        AvailableRing = availableRing;
        UsedRing = usedRing;
        EndOffset = usedRing.EndOffset;
        RequiredByteLength = EndOffset - queueStartOffset;
    }

    public ushort QueueSize { get; }
    public uint QueueStartOffset { get; }
    public VirtqueueSplitRingRegion DescriptorTable { get; }
    public VirtqueueSplitRingRegion AvailableRing { get; }
    public VirtqueueSplitRingRegion UsedRing { get; }
    public uint EndOffset { get; }
    public uint RequiredByteLength { get; }

    public static VirtqueueSplitRingPlanResult Create(ushort queueSize)
    {
        return Create(queueStartOffset: 0, queueSize);
    }

    public static VirtqueueSplitRingPlanResult Create(uint queueStartOffset, ushort queueSize)
    {
        VirtqueueSplitRingPlanStatus queueStatus = ValidateQueueSize(queueSize);
        if (queueStatus != VirtqueueSplitRingPlanStatus.Success)
        {
            return VirtqueueSplitRingPlanResult.Fail(queueStatus, queueSize);
        }

        if (!TryAlignUp(queueStartOffset, DescriptorTableAlignment, out uint descriptorOffset))
        {
            return VirtqueueSplitRingPlanResult.Fail(VirtqueueSplitRingPlanStatus.LayoutOverflow, queueSize);
        }

        if (!TryMultiply(queueSize, DescriptorLength, out uint descriptorLength) || !TryAdd(descriptorOffset, descriptorLength, out uint descriptorEnd))
        {
            return VirtqueueSplitRingPlanResult.Fail(VirtqueueSplitRingPlanStatus.LayoutOverflow, queueSize);
        }

        if (!TryAlignUp(descriptorEnd, AvailableRingAlignment, out uint availableOffset))
        {
            return VirtqueueSplitRingPlanResult.Fail(VirtqueueSplitRingPlanStatus.LayoutOverflow, queueSize);
        }

        if (!TryMultiply(queueSize, AvailableRingEntryLength, out uint availableEntriesLength) || !TryAdd(AvailableRingHeaderLength, availableEntriesLength, out uint availableLength) || !TryAdd(availableOffset, availableLength, out uint availableEnd))
        {
            return VirtqueueSplitRingPlanResult.Fail(VirtqueueSplitRingPlanStatus.LayoutOverflow, queueSize);
        }

        if (!TryAlignUp(availableEnd, UsedRingAlignment, out uint usedOffset))
        {
            return VirtqueueSplitRingPlanResult.Fail(VirtqueueSplitRingPlanStatus.LayoutOverflow, queueSize);
        }

        if (!TryMultiply(queueSize, UsedRingElementLength, out uint usedEntriesLength) || !TryAdd(UsedRingHeaderLength, usedEntriesLength, out uint usedLength) || !TryAdd(usedOffset, usedLength, out uint usedEnd))
        {
            return VirtqueueSplitRingPlanResult.Fail(VirtqueueSplitRingPlanStatus.LayoutOverflow, queueSize);
        }

        VirtqueueSplitRingRegion descriptorTable = new VirtqueueSplitRingRegion(descriptorOffset, descriptorLength, DescriptorTableAlignment);
        VirtqueueSplitRingRegion availableRing = new VirtqueueSplitRingRegion(availableOffset, availableLength, AvailableRingAlignment);
        VirtqueueSplitRingRegion usedRing = new VirtqueueSplitRingRegion(usedOffset, usedLength, UsedRingAlignment);
        return VirtqueueSplitRingPlanResult.Success(new VirtqueueSplitRingPlan(queueSize, queueStartOffset, descriptorTable, availableRing, usedRing));
    }

    public static VirtqueueSplitRingPlanStatus ValidateQueueSize(ushort queueSize)
    {
        if (queueSize == 0)
        {
            return VirtqueueSplitRingPlanStatus.InvalidQueueSize;
        }

        if (queueSize > MaximumQueueSize)
        {
            return VirtqueueSplitRingPlanStatus.QueueSizeTooLarge;
        }

        if ((queueSize & (queueSize - 1)) != 0)
        {
            return VirtqueueSplitRingPlanStatus.QueueSizeNotPowerOfTwo;
        }

        return VirtqueueSplitRingPlanStatus.Success;
    }

    private static bool TryAlignUp(uint value, uint alignment, out uint aligned)
    {
        uint remainder = value % alignment;
        if (remainder == 0)
        {
            aligned = value;
            return true;
        }

        uint padding = alignment - remainder;
        return TryAdd(value, padding, out aligned);
    }

    private static bool TryAdd(uint left, uint right, out uint result)
    {
        if (left > (~0u) - right)
        {
            result = 0;
            return false;
        }

        result = left + right;
        return true;
    }

    private static bool TryMultiply(ushort left, uint right, out uint result)
    {
        ulong value = (ulong)left * right;
        if (value > (~0u))
        {
            result = 0;
            return false;
        }

        result = (uint)value;
        return true;
    }
}
