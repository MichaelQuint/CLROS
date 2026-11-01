namespace OS.Drivers;
using OS.Std.Binary;

public static class VirtioNetTransmitLifecycle
{
    public const ushort RequiredDescriptorCount = VirtioNetQueuePlan.RequiredTxDescriptorCount;
    public const uint TxHeaderBytes = VirtioNetHeader.HeaderLengthWithoutMergeableBuffers;
    public const bool AllocatesDmaMemory = false;
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool WaitsForUsedRingCompletion = false;
    public const bool PerformsPacketTransmit = false;
    private const ushort DescriptorNextFlag = 1;
    private const uint DescriptorAddressOffset = 0;
    private const uint DescriptorLengthOffset = 8;
    private const uint DescriptorFlagsOffset = 12;
    private const uint DescriptorNextOffset = 14;
    private const uint DescriptorEntryLength = VirtqueueSplitRingPlan.DescriptorLength;
    private const uint AvailableRingIndexOffset = 2;
    private const uint AvailableRingFieldLength = sizeof(ushort);
    public static VirtioNetTransmitLifecycleResult Publish(VirtioNetQueuePlanResult queuePlan, ushort firstDescriptorIndex, ushort availableDescriptorCount, ushort availableRingIndex, ulong txHeaderPhysicalAddress, ulong ethernetFramePhysicalAddress, ReadOnlySpan<byte> ethernetFrame, Span<byte> txHeader, Span<byte> queueMemory)
    {
        VirtioNetTransmitLifecycleStatus validationStatus = TryCreatePlan(queuePlan, firstDescriptorIndex, availableDescriptorCount, availableRingIndex, txHeaderPhysicalAddress, ethernetFramePhysicalAddress, ethernetFrame.Length, txHeader.Length, queueMemory.Length, out VirtioNetTransmitLifecyclePlan plan);
        if (validationStatus != VirtioNetTransmitLifecycleStatus.Success)
        {
            return VirtioNetTransmitLifecycleResult.Fail(validationStatus, queuePlan.Status, validationStatus == VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall ? VirtioNetHeaderStatus.BufferTooSmall : VirtioNetHeaderStatus.Success);
        }

        VirtioNetHeaderStatus headerStatus = VirtioNetHeader.TryWriteNoOffloadTxHeader(txHeader, out int headerBytesWritten);
        if (headerStatus != VirtioNetHeaderStatus.Success)
        {
            return VirtioNetTransmitLifecycleResult.Fail(VirtioNetTransmitLifecycleStatus.HeaderWriteFailed, queuePlan.Status, headerStatus);
        }

        WriteDescriptor(queueMemory, plan.HeaderDescriptor);
        WriteDescriptor(queueMemory, plan.FrameDescriptor);
        WriteUInt16(queueMemory, plan.AvailableRingEntryOffset, plan.HeadDescriptorIndex);
        WriteUInt16(queueMemory, plan.TxQueue.AvailableRing.Offset + AvailableRingIndexOffset, VirtioNetTransmitLifecycleResult.GetNextAvailableRingIndex(plan.AvailableRingIndex));
        return VirtioNetTransmitLifecycleResult.Success(plan, headerBytesWritten, ethernetFrame.Length);
    }

    private static VirtioNetTransmitLifecycleStatus TryCreatePlan(VirtioNetQueuePlanResult queuePlan, ushort firstDescriptorIndex, ushort availableDescriptorCount, ushort availableRingIndex, ulong txHeaderPhysicalAddress, ulong ethernetFramePhysicalAddress, int ethernetFrameBytes, int txHeaderBytes, int queueMemoryBytes, out VirtioNetTransmitLifecyclePlan plan)
    {
        plan = default;
        if (!queuePlan.Succeeded)
        {
            return VirtioNetTransmitLifecycleStatus.QueuePlanFailed;
        }

        if (txHeaderBytes < TxHeaderBytes)
        {
            return VirtioNetTransmitLifecycleStatus.HeaderBufferTooSmall;
        }

        if (ethernetFrameBytes < VirtioNetQueuePlan.EthernetHeaderLength)
        {
            return VirtioNetTransmitLifecycleStatus.EthernetFrameTooSmall;
        }

        VirtioNetQueuePlan netQueue = queuePlan.Plan;
        if ((uint)ethernetFrameBytes > netQueue.TxFrame.EthernetFrameBytes)
        {
            return VirtioNetTransmitLifecycleStatus.EthernetFrameTooLarge;
        }

        VirtqueueSplitRingPlan txQueue = netQueue.TxQueue;
        if (availableDescriptorCount < RequiredDescriptorCount)
        {
            return VirtioNetTransmitLifecycleStatus.InsufficientDescriptors;
        }

        if (!TryAddDescriptorIndex(firstDescriptorIndex, 1, out ushort frameDescriptorIndex) || frameDescriptorIndex >= txQueue.QueueSize)
        {
            return VirtioNetTransmitLifecycleStatus.DescriptorIndexOutOfRange;
        }

        if (!txQueue.DescriptorTable.IsAligned || txQueue.DescriptorTable.Alignment != VirtqueueSplitRingPlan.DescriptorTableAlignment)
        {
            return VirtioNetTransmitLifecycleStatus.DescriptorAlignmentInvalid;
        }

        if (!TryDescriptorOffset(txQueue, firstDescriptorIndex, out uint headerDescriptorOffset) || !TryDescriptorOffset(txQueue, frameDescriptorIndex, out uint frameDescriptorOffset))
        {
            return VirtioNetTransmitLifecycleStatus.DescriptorOffsetOverflow;
        }

        if (!IsDescriptorOffsetAligned(headerDescriptorOffset) || !IsDescriptorOffsetAligned(frameDescriptorOffset))
        {
            return VirtioNetTransmitLifecycleStatus.DescriptorAlignmentInvalid;
        }

        ushort availableRingSlot = (ushort)(availableRingIndex & (txQueue.QueueSize - 1));
        if (!TryAvailableRingEntryOffset(txQueue, availableRingSlot, out uint availableRingEntryOffset))
        {
            return VirtioNetTransmitLifecycleStatus.AvailableRingOffsetOverflow;
        }

        if (!TryMemoryRange(VirtioNetTransmitDescriptorRole.VirtioHeader, txHeaderPhysicalAddress, TxHeaderBytes, out VirtioNetTransmitDescriptorMemoryRange headerRange) || !TryMemoryRange(VirtioNetTransmitDescriptorRole.EthernetFrame, ethernetFramePhysicalAddress, (uint)ethernetFrameBytes, out VirtioNetTransmitDescriptorMemoryRange frameRange))
        {
            return VirtioNetTransmitLifecycleStatus.PhysicalRangeOverflow;
        }

        VirtioNetTransmitLifecycleStatus memoryStatus = ValidateQueueMemory(txQueue, queueMemoryBytes);
        if (memoryStatus != VirtioNetTransmitLifecycleStatus.Success)
        {
            return memoryStatus;
        }

        VirtioNetTransmitPublishedDescriptor headerDescriptor = new VirtioNetTransmitPublishedDescriptor(VirtioNetTransmitDescriptorRole.VirtioHeader, firstDescriptorIndex, txHeaderPhysicalAddress, TxHeaderBytes, hasNext: true, frameDescriptorIndex, DescriptorNextFlag, headerDescriptorOffset, headerRange);
        VirtioNetTransmitPublishedDescriptor frameDescriptor = new VirtioNetTransmitPublishedDescriptor(VirtioNetTransmitDescriptorRole.EthernetFrame, frameDescriptorIndex, ethernetFramePhysicalAddress, (uint)ethernetFrameBytes, hasNext: false, nextDescriptorIndex: 0, flags: 0, frameDescriptorOffset, frameRange);
        plan = new VirtioNetTransmitLifecyclePlan(netQueue, availableDescriptorCount, availableRingIndex, availableRingSlot, availableRingEntryOffset, TxHeaderBytes, (uint)ethernetFrameBytes, headerDescriptor, frameDescriptor);
        return VirtioNetTransmitLifecycleStatus.Success;
    }

    private static VirtioNetTransmitLifecycleStatus ValidateQueueMemory(VirtqueueSplitRingPlan queue, int queueMemoryBytes)
    {
        VirtioNetTransmitLifecycleStatus status = ValidateRange(queue.EndOffset, 0, queueMemoryBytes);
        if (status != VirtioNetTransmitLifecycleStatus.Success)
        {
            return status;
        }

        return ValidateRange(queue.AvailableRing.Offset + AvailableRingIndexOffset, AvailableRingFieldLength, queueMemoryBytes);
    }

    private static VirtioNetTransmitLifecycleStatus ValidateRange(uint offset, uint length, int destinationLength)
    {
        ulong end = (ulong)offset + length;
        if (end > int.MaxValue)
        {
            return VirtioNetTransmitLifecycleStatus.ArithmeticOverflow;
        }

        if (end > (ulong)destinationLength)
        {
            return VirtioNetTransmitLifecycleStatus.QueueMemoryTooSmall;
        }

        return VirtioNetTransmitLifecycleStatus.Success;
    }

    private static bool TryDescriptorOffset(VirtqueueSplitRingPlan queue, ushort descriptorIndex, out uint offset)
    {
        ulong value = (ulong)queue.DescriptorTable.Offset + ((ulong)descriptorIndex * DescriptorEntryLength);
        if (value > uint.MaxValue)
        {
            offset = 0;
            return false;
        }

        offset = (uint)value;
        return true;
    }

    private static bool TryAvailableRingEntryOffset(VirtqueueSplitRingPlan queue, ushort slot, out uint offset)
    {
        ulong value = (ulong)queue.AvailableRing.Offset + VirtqueueSplitRingPlan.AvailableRingHeaderLength + ((ulong)slot * VirtqueueSplitRingPlan.AvailableRingEntryLength);
        if (value > uint.MaxValue)
        {
            offset = 0;
            return false;
        }

        offset = (uint)value;
        return true;
    }

    private static bool TryAddDescriptorIndex(ushort value, ushort increment, out ushort result)
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

    private static bool IsDescriptorOffsetAligned(uint offset)
    {
        return (offset % VirtqueueSplitRingPlan.DescriptorTableAlignment) == 0;
    }

    private static bool TryMemoryRange(VirtioNetTransmitDescriptorRole role, ulong physicalAddress, uint length, out VirtioNetTransmitDescriptorMemoryRange range)
    {
        if ((ulong)length > ulong.MaxValue - physicalAddress)
        {
            range = default;
            return false;
        }

        range = new VirtioNetTransmitDescriptorMemoryRange(role, physicalAddress, length, physicalAddress + length);
        return true;
    }

    private static void WriteDescriptor(Span<byte> queueMemory, VirtioNetTransmitPublishedDescriptor descriptor)
    {
        int offset = (int)descriptor.DescriptorTableOffset;
        Span<byte> descriptorBytes = queueMemory.Slice(offset, (int)DescriptorEntryLength);
        EndianPrimitives.WriteUInt64LittleEndian(descriptorBytes.Slice((int)DescriptorAddressOffset, sizeof(ulong)), descriptor.PhysicalAddress);
        EndianPrimitives.WriteUInt32LittleEndian(descriptorBytes.Slice((int)DescriptorLengthOffset, sizeof(uint)), descriptor.Length);
        EndianPrimitives.WriteUInt16LittleEndian(descriptorBytes.Slice((int)DescriptorFlagsOffset, sizeof(ushort)), descriptor.Flags);
        EndianPrimitives.WriteUInt16LittleEndian(descriptorBytes.Slice((int)DescriptorNextOffset, sizeof(ushort)), descriptor.NextDescriptorIndex);
    }

    private static void WriteUInt16(Span<byte> queueMemory, uint offset, ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(queueMemory.Slice((int)offset, sizeof(ushort)), value);
    }
}
