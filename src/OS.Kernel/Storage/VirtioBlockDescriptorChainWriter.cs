using OS.Drivers;
using OS.Std.Binary;

namespace OS.Storage;

public enum VirtioBlockDescriptorChainWriterStatus
{
    Success = 0,
    DescriptorPlanFailed = 1,
    QueuePlanFailed = 2,
    DescriptorIndexOutOfRange = 3,
    InvalidAvailableRingSlot = 4,
    QueuePlanMismatch = 5,
    QueueMemoryTooSmall = 6,
    ArithmeticOverflow = 7
}

public readonly struct VirtioBlockDescriptorChainWriterResult
{
    private VirtioBlockDescriptorChainWriterResult(
        VirtioBlockDescriptorChainWriterStatus status,
        VirtioBlockDescriptorChainPlanStatus descriptorPlanStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        ushort headDescriptorIndex,
        ushort nextAvailableRingIndex,
        ushort descriptorsWritten)
    {
        IsInitialized = true;
        Status = status;
        DescriptorPlanStatus = descriptorPlanStatus;
        QueuePlanStatus = queuePlanStatus;
        HeadDescriptorIndex = headDescriptorIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorsWritten = descriptorsWritten;
    }

    public bool IsInitialized { get; }

    public VirtioBlockDescriptorChainWriterStatus Status { get; }

    public VirtioBlockDescriptorChainPlanStatus DescriptorPlanStatus { get; }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus { get; }

    public ushort HeadDescriptorIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public ushort DescriptorsWritten { get; }

    public bool Succeeded => IsInitialized && Status == VirtioBlockDescriptorChainWriterStatus.Success;

    public static VirtioBlockDescriptorChainWriterResult Success(
        VirtioBlockDescriptorChainPlan plan,
        VirtqueueSplitRingPlanStatus queuePlanStatus)
    {
        return new VirtioBlockDescriptorChainWriterResult(
            VirtioBlockDescriptorChainWriterStatus.Success,
            VirtioBlockDescriptorChainPlanStatus.Success,
            queuePlanStatus,
            plan.HeadDescriptorIndex,
            GetNextAvailableRingIndex(plan.AvailableRingIndex),
            VirtioBlockDescriptorChainPlan.RequiredDescriptorCount);
    }

    public static VirtioBlockDescriptorChainWriterResult Fail(
        VirtioBlockDescriptorChainWriterStatus status,
        VirtioBlockDescriptorChainPlanStatus descriptorPlanStatus,
        VirtqueueSplitRingPlanStatus queuePlanStatus)
    {
        return new VirtioBlockDescriptorChainWriterResult(
            status,
            descriptorPlanStatus,
            queuePlanStatus,
            headDescriptorIndex: 0,
            nextAvailableRingIndex: 0,
            descriptorsWritten: 0);
    }

    internal static ushort GetNextAvailableRingIndex(ushort availableRingIndex)
    {
        return unchecked((ushort)(availableRingIndex + 1));
    }
}

public static class VirtioBlockDescriptorChainWriter
{
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool WaitsForUsedRingCompletion = false;
    public const bool PerformsDiskRead = false;

    private const uint DescriptorAddressOffset = 0;
    private const uint DescriptorLengthOffset = 8;
    private const uint DescriptorFlagsOffset = 12;
    private const uint DescriptorNextOffset = 14;
    private const uint AvailableRingIndexOffset = 2;
    private const uint DescriptorEntryLength = VirtqueueSplitRingPlan.DescriptorLength;
    private const uint AvailableRingFieldLength = sizeof(ushort);

    public static VirtioBlockDescriptorChainWriterResult Write(
        VirtioBlockDescriptorChainPlanResult descriptorPlan,
        VirtqueueSplitRingPlanResult queuePlan,
        Span<byte> queueMemory)
    {
        if (!descriptorPlan.Succeeded)
        {
            return VirtioBlockDescriptorChainWriterResult.Fail(
                VirtioBlockDescriptorChainWriterStatus.DescriptorPlanFailed,
                descriptorPlan.Status,
                queuePlan.Status);
        }

        if (!queuePlan.Succeeded)
        {
            return VirtioBlockDescriptorChainWriterResult.Fail(
                VirtioBlockDescriptorChainWriterStatus.QueuePlanFailed,
                descriptorPlan.Status,
                queuePlan.Status);
        }

        VirtioBlockDescriptorChainPlan plan = descriptorPlan.Plan;
        VirtqueueSplitRingPlan queue = queuePlan.Plan;

        VirtioBlockDescriptorChainWriterStatus validationStatus =
            ValidatePlanAndMemory(plan, queue, queueMemory);
        if (validationStatus != VirtioBlockDescriptorChainWriterStatus.Success)
        {
            return VirtioBlockDescriptorChainWriterResult.Fail(
                validationStatus,
                descriptorPlan.Status,
                queuePlan.Status);
        }

        WriteDescriptor(queueMemory, plan.RequestHeaderDescriptor);
        WriteDescriptor(queueMemory, plan.DataBufferDescriptor);
        WriteDescriptor(queueMemory, plan.StatusByteDescriptor);

        WriteUInt16(
            queueMemory,
            plan.AvailableRingEntryOffset,
            plan.HeadDescriptorIndex);
        WriteUInt16(
            queueMemory,
            queue.AvailableRing.Offset + AvailableRingIndexOffset,
            VirtioBlockDescriptorChainWriterResult.GetNextAvailableRingIndex(
                plan.AvailableRingIndex));

        return VirtioBlockDescriptorChainWriterResult.Success(
            plan,
            queuePlan.Status);
    }

    private static VirtioBlockDescriptorChainWriterStatus ValidatePlanAndMemory(
        VirtioBlockDescriptorChainPlan plan,
        VirtqueueSplitRingPlan queue,
        Span<byte> queueMemory)
    {
        if (plan.RequestHeaderDescriptor.DescriptorIndex >= queue.QueueSize ||
            plan.DataBufferDescriptor.DescriptorIndex >= queue.QueueSize ||
            plan.StatusByteDescriptor.DescriptorIndex >= queue.QueueSize)
        {
            return VirtioBlockDescriptorChainWriterStatus.DescriptorIndexOutOfRange;
        }

        if (plan.AvailableRingSlot >= queue.QueueSize)
        {
            return VirtioBlockDescriptorChainWriterStatus.InvalidAvailableRingSlot;
        }

        if (!TryGetDescriptorOffset(
                queue,
                plan.RequestHeaderDescriptor.DescriptorIndex,
                out uint expectedRequestDescriptorOffset) ||
            !TryGetDescriptorOffset(
                queue,
                plan.DataBufferDescriptor.DescriptorIndex,
                out uint expectedDataDescriptorOffset) ||
            !TryGetDescriptorOffset(
                queue,
                plan.StatusByteDescriptor.DescriptorIndex,
                out uint expectedStatusDescriptorOffset) ||
            !TryGetAvailableRingEntryOffset(
                queue,
                plan.AvailableRingSlot,
                out uint expectedAvailableEntryOffset))
        {
            return VirtioBlockDescriptorChainWriterStatus.ArithmeticOverflow;
        }

        if (plan.RequestHeaderDescriptor.DescriptorTableOffset != expectedRequestDescriptorOffset ||
            plan.DataBufferDescriptor.DescriptorTableOffset != expectedDataDescriptorOffset ||
            plan.StatusByteDescriptor.DescriptorTableOffset != expectedStatusDescriptorOffset ||
            plan.AvailableRingEntryOffset != expectedAvailableEntryOffset)
        {
            return VirtioBlockDescriptorChainWriterStatus.QueuePlanMismatch;
        }

        VirtioBlockDescriptorChainWriterStatus rangeStatus =
            ValidateRange(queue.EndOffset, 0, queueMemory.Length);
        if (rangeStatus != VirtioBlockDescriptorChainWriterStatus.Success)
        {
            return rangeStatus;
        }

        rangeStatus = ValidateRange(
            plan.RequestHeaderDescriptor.DescriptorTableOffset,
            DescriptorEntryLength,
            queueMemory.Length);
        if (rangeStatus != VirtioBlockDescriptorChainWriterStatus.Success)
        {
            return rangeStatus;
        }

        rangeStatus = ValidateRange(
            plan.DataBufferDescriptor.DescriptorTableOffset,
            DescriptorEntryLength,
            queueMemory.Length);
        if (rangeStatus != VirtioBlockDescriptorChainWriterStatus.Success)
        {
            return rangeStatus;
        }

        rangeStatus = ValidateRange(
            plan.StatusByteDescriptor.DescriptorTableOffset,
            DescriptorEntryLength,
            queueMemory.Length);
        if (rangeStatus != VirtioBlockDescriptorChainWriterStatus.Success)
        {
            return rangeStatus;
        }

        rangeStatus = ValidateRange(
            plan.AvailableRingEntryOffset,
            AvailableRingFieldLength,
            queueMemory.Length);
        if (rangeStatus != VirtioBlockDescriptorChainWriterStatus.Success)
        {
            return rangeStatus;
        }

        return ValidateRange(
            queue.AvailableRing.Offset + AvailableRingIndexOffset,
            AvailableRingFieldLength,
            queueMemory.Length);
    }

    private static bool TryGetDescriptorOffset(
        VirtqueueSplitRingPlan queue,
        ushort descriptorIndex,
        out uint offset)
    {
        ulong value = (ulong)queue.DescriptorTable.Offset +
            ((ulong)descriptorIndex * DescriptorEntryLength);
        if (value > uint.MaxValue)
        {
            offset = 0;
            return false;
        }

        offset = (uint)value;
        return true;
    }

    private static bool TryGetAvailableRingEntryOffset(
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

    private static VirtioBlockDescriptorChainWriterStatus ValidateRange(
        uint offset,
        uint length,
        int destinationLength)
    {
        ulong end = (ulong)offset + length;
        if (end > int.MaxValue)
        {
            return VirtioBlockDescriptorChainWriterStatus.ArithmeticOverflow;
        }

        if (end > (ulong)destinationLength)
        {
            return VirtioBlockDescriptorChainWriterStatus.QueueMemoryTooSmall;
        }

        return VirtioBlockDescriptorChainWriterStatus.Success;
    }

    private static void WriteDescriptor(
        Span<byte> queueMemory,
        VirtioBlockPublishedDescriptorPlan descriptor)
    {
        int offset = (int)descriptor.DescriptorTableOffset;
        Span<byte> descriptorBytes = queueMemory.Slice(
            offset,
            (int)DescriptorEntryLength);

        EndianPrimitives.WriteUInt64LittleEndian(
            descriptorBytes.Slice((int)DescriptorAddressOffset, sizeof(ulong)),
            descriptor.PhysicalAddress);
        EndianPrimitives.WriteUInt32LittleEndian(
            descriptorBytes.Slice((int)DescriptorLengthOffset, sizeof(uint)),
            descriptor.Length);
        EndianPrimitives.WriteUInt16LittleEndian(
            descriptorBytes.Slice((int)DescriptorFlagsOffset, sizeof(ushort)),
            descriptor.Flags);
        EndianPrimitives.WriteUInt16LittleEndian(
            descriptorBytes.Slice((int)DescriptorNextOffset, sizeof(ushort)),
            descriptor.NextDescriptorIndex);
    }

    private static void WriteUInt16(
        Span<byte> queueMemory,
        uint offset,
        ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(
            queueMemory.Slice((int)offset, sizeof(ushort)),
            value);
    }
}
