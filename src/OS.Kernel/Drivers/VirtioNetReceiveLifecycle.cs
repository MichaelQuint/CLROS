namespace OS.Drivers;
using OS.Net;
using OS.Std.Binary;

public static class VirtioNetReceiveLifecycle
{
    public const uint MinimumReceiveBufferBytes = (uint)(VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + EthernetHeader.HeaderLength);
    public const uint MaximumReceiveBufferBytes = (uint)VirtioNetHeader.HeaderLengthWithoutMergeableBuffers + VirtioNetQueuePlan.MaximumEthernetFrameBytes;
    public const bool AllocatesReceiveBuffers = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsMmio = false;
    public const bool NotifiesDevice = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool CopiesPayload = false;
    public const bool PerformsPacketIo = false;
    private const ushort DescriptorFlagWrite = 0x0002;
    private const uint DescriptorAddressOffset = 0;
    private const uint DescriptorLengthOffset = 8;
    private const uint DescriptorFlagsOffset = 12;
    private const uint DescriptorNextOffset = 14;
    private const uint AvailableRingIndexOffset = 2;
    private const uint UsedRingIndexOffset = 2;
    private const uint DescriptorEntryLength = VirtqueueSplitRingPlan.DescriptorLength;
    private const uint AvailableRingFieldLength = sizeof(ushort);
    private const uint UsedRingIndexLength = sizeof(ushort);
    private const uint UsedRingElementLength = VirtqueueSplitRingPlan.UsedRingElementLength;
    public static VirtioNetReceiveLifecyclePublishResult PublishReceiveBuffers(ushort queueSize, ushort firstDescriptorIndex, ushort availableDescriptorCount, ushort previousAvailableRingIndex, ushort previousUsedIndex, ulong firstReceiveBufferPhysicalAddress, uint receiveBufferStrideBytes, ushort receiveBufferCount, uint frameBufferBytes, Span<byte> queueMemory)
    {
        VirtqueueSplitRingPlanResult queuePlan = VirtqueueSplitRingPlan.Create(queueSize);
        if (!queuePlan.Succeeded)
        {
            return VirtioNetReceiveLifecyclePublishResult.Fail(VirtioNetReceiveLifecyclePublishStatus.QueuePlanFailed, queuePlan.Status, default, queueSize, firstDescriptorIndex, receiveBufferCount, previousAvailableRingIndex, previousUsedIndex, frameBufferBytes, receiveBufferStrideBytes);
        }

        VirtioNetReceiveLifecyclePublishStatus validationStatus = ValidatePublishInputs(queuePlan.Plan, firstDescriptorIndex, availableDescriptorCount, receiveBufferCount, frameBufferBytes, receiveBufferStrideBytes, firstReceiveBufferPhysicalAddress, queueMemory);
        if (validationStatus != VirtioNetReceiveLifecyclePublishStatus.Success)
        {
            return VirtioNetReceiveLifecyclePublishResult.Fail(validationStatus, queuePlan.Status, queuePlan.Plan, queueSize, firstDescriptorIndex, receiveBufferCount, previousAvailableRingIndex, previousUsedIndex, frameBufferBytes, receiveBufferStrideBytes);
        }

        ushort firstAvailableRingSlot = GetRingSlot(previousAvailableRingIndex, queueSize);
        ushort lastAvailableRingSlot = GetRingSlot(unchecked((ushort)(previousAvailableRingIndex + receiveBufferCount - 1)), queueSize);
        uint firstAvailableRingEntryOffset = GetAvailableRingEntryOffset(queuePlan.Plan, firstAvailableRingSlot);
        for (ushort bufferIndex = 0; bufferIndex < receiveBufferCount; bufferIndex++)
        {
            ushort descriptorIndex = unchecked((ushort)(firstDescriptorIndex + bufferIndex));
            ulong physicalAddress = firstReceiveBufferPhysicalAddress + ((ulong)bufferIndex * receiveBufferStrideBytes);
            WriteDescriptor(queueMemory, queuePlan.Plan, descriptorIndex, physicalAddress, frameBufferBytes);
            ushort slot = GetRingSlot(unchecked((ushort)(previousAvailableRingIndex + bufferIndex)), queueSize);
            WriteUInt16(queueMemory, GetAvailableRingEntryOffset(queuePlan.Plan, slot), descriptorIndex);
        }

        WriteUInt16(queueMemory, queuePlan.Plan.AvailableRing.Offset + AvailableRingIndexOffset, unchecked((ushort)(previousAvailableRingIndex + receiveBufferCount)));
        return VirtioNetReceiveLifecyclePublishResult.Success(queuePlan.Plan, firstDescriptorIndex, receiveBufferCount, previousAvailableRingIndex, previousUsedIndex, firstAvailableRingSlot, lastAvailableRingSlot, firstAvailableRingEntryOffset, frameBufferBytes, receiveBufferStrideBytes);
    }

    public static VirtioNetReceiveLifecycleCompletionResult CompleteReceiveBuffer(VirtioNetReceiveLifecyclePublishResult publish, ReadOnlySpan<byte> queueMemory, ReadOnlySpan<byte> receiveBuffer)
    {
        if (!publish.Succeeded)
        {
            return CreateCompletionFailure(VirtioNetReceiveLifecycleCompletionStatus.PublishFailed, publish, publish.PreviousUsedIndex, descriptorHead: 0, bufferIndex: 0, usedRingSlot: 0, usedRingElementOffset: 0, usedByteCount: 0);
        }

        if (!TryValidateRange(publish.QueuePlan.UsedRing.Offset + UsedRingIndexOffset, UsedRingIndexLength, queueMemory.Length) || !TryGetUsedRingElementOffset(publish.QueuePlan, GetRingSlot(publish.PreviousUsedIndex, publish.QueueSize), out uint usedRingElementOffset) || !TryValidateRange(usedRingElementOffset, UsedRingElementLength, queueMemory.Length))
        {
            return CreateCompletionFailure(VirtioNetReceiveLifecycleCompletionStatus.QueueMemoryTooSmall, publish, publish.PreviousUsedIndex, descriptorHead: 0, bufferIndex: 0, usedRingSlot: 0, usedRingElementOffset: 0, usedByteCount: 0);
        }

        ushort currentUsedIndex = ReadUInt16(queueMemory, publish.QueuePlan.UsedRing.Offset + UsedRingIndexOffset);
        ushort usedRingSlot = GetRingSlot(publish.PreviousUsedIndex, publish.QueueSize);
        if (currentUsedIndex == publish.PreviousUsedIndex)
        {
            return CreateCompletionFailure(VirtioNetReceiveLifecycleCompletionStatus.NoCompletion, publish, currentUsedIndex, descriptorHead: 0, bufferIndex: 0, usedRingSlot, usedRingElementOffset, usedByteCount: 0);
        }

        if (currentUsedIndex != publish.ExpectedNextUsedIndex)
        {
            return CreateCompletionFailure(VirtioNetReceiveLifecycleCompletionStatus.UnexpectedUsedIndex, publish, currentUsedIndex, descriptorHead: 0, bufferIndex: 0, usedRingSlot, usedRingElementOffset, usedByteCount: 0);
        }

        uint completedDescriptorHead = ReadUInt32(queueMemory, usedRingElementOffset);
        uint usedByteCount = ReadUInt32(queueMemory, usedRingElementOffset + sizeof(uint));
        if (!TryMapDescriptorHead(publish, completedDescriptorHead, out ushort descriptorHead, out ushort bufferIndex))
        {
            return CreateCompletionFailure(VirtioNetReceiveLifecycleCompletionStatus.UnexpectedDescriptorHead, publish, currentUsedIndex, descriptorHead: completedDescriptorHead <= ushort.MaxValue ? (ushort)completedDescriptorHead : (ushort)0, bufferIndex: 0, usedRingSlot, usedRingElementOffset, usedByteCount);
        }

        if (usedByteCount > publish.FrameBufferBytes)
        {
            return CreateCompletionFailure(VirtioNetReceiveLifecycleCompletionStatus.UnexpectedByteCount, publish, currentUsedIndex, descriptorHead, bufferIndex, usedRingSlot, usedRingElementOffset, usedByteCount);
        }

        if (usedByteCount > (uint)receiveBuffer.Length)
        {
            return CreateCompletionFailure(VirtioNetReceiveLifecycleCompletionStatus.ReceiveBufferTooSmall, publish, currentUsedIndex, descriptorHead, bufferIndex, usedRingSlot, usedRingElementOffset, usedByteCount);
        }

        VirtioNetReceiveFrameClassificationResult classification = VirtioNetReceiveFrameClassifier.Classify(receiveBuffer.Slice(0, (int)usedByteCount));
        if (!classification.Succeeded)
        {
            return VirtioNetReceiveLifecycleCompletionResult.Create(VirtioNetReceiveLifecycleCompletionStatus.FrameClassificationFailed, publish, classification.Status, classification.PacketStatus, descriptorHead, bufferIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset, usedByteCount, classification);
        }

        return VirtioNetReceiveLifecycleCompletionResult.Create(VirtioNetReceiveLifecycleCompletionStatus.Success, publish, classification.Status, classification.PacketStatus, descriptorHead, bufferIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset, usedByteCount, classification);
    }

    private static VirtioNetReceiveLifecyclePublishStatus ValidatePublishInputs(VirtqueueSplitRingPlan queue, ushort firstDescriptorIndex, ushort availableDescriptorCount, ushort receiveBufferCount, uint frameBufferBytes, uint receiveBufferStrideBytes, ulong firstReceiveBufferPhysicalAddress, ReadOnlySpan<byte> queueMemory)
    {
        if (receiveBufferCount == 0)
        {
            return VirtioNetReceiveLifecyclePublishStatus.InvalidReceiveBufferCount;
        }

        if (frameBufferBytes < MinimumReceiveBufferBytes || frameBufferBytes > MaximumReceiveBufferBytes)
        {
            return VirtioNetReceiveLifecyclePublishStatus.InvalidFrameBufferSize;
        }

        if (receiveBufferStrideBytes < frameBufferBytes)
        {
            return VirtioNetReceiveLifecyclePublishStatus.InvalidReceiveBufferStride;
        }

        if (availableDescriptorCount < receiveBufferCount)
        {
            return VirtioNetReceiveLifecyclePublishStatus.InsufficientDescriptors;
        }

        if ((uint)firstDescriptorIndex + receiveBufferCount > queue.QueueSize)
        {
            return VirtioNetReceiveLifecyclePublishStatus.DescriptorRangeOutOfRange;
        }

        VirtioNetReceiveLifecyclePublishStatus memoryStatus = ValidateQueueMemory(queue, queueMemory);
        if (memoryStatus != VirtioNetReceiveLifecyclePublishStatus.Success)
        {
            return memoryStatus;
        }

        for (ushort bufferIndex = 0; bufferIndex < receiveBufferCount; bufferIndex++)
        {
            if (!TryMultiply(bufferIndex, receiveBufferStrideBytes, out ulong byteOffset) || firstReceiveBufferPhysicalAddress > ulong.MaxValue - byteOffset || !TryGetDescriptorOffset(queue, unchecked((ushort)(firstDescriptorIndex + bufferIndex)), out uint descriptorOffset) || !TryValidateRange(descriptorOffset, DescriptorEntryLength, queueMemory.Length))
            {
                return VirtioNetReceiveLifecyclePublishStatus.ArithmeticOverflow;
            }
        }

        return VirtioNetReceiveLifecyclePublishStatus.Success;
    }

    private static VirtioNetReceiveLifecyclePublishStatus ValidateQueueMemory(VirtqueueSplitRingPlan queue, ReadOnlySpan<byte> queueMemory)
    {
        if (!TryValidateRange(queue.EndOffset, 0, queueMemory.Length) || !TryValidateRange(queue.AvailableRing.Offset + AvailableRingIndexOffset, AvailableRingFieldLength, queueMemory.Length))
        {
            return VirtioNetReceiveLifecyclePublishStatus.QueueMemoryTooSmall;
        }

        return VirtioNetReceiveLifecyclePublishStatus.Success;
    }

    private static VirtioNetReceiveLifecycleCompletionResult CreateCompletionFailure(VirtioNetReceiveLifecycleCompletionStatus status, VirtioNetReceiveLifecyclePublishResult publish, ushort currentUsedIndex, uint descriptorHead, ushort bufferIndex, ushort usedRingSlot, uint usedRingElementOffset, uint usedByteCount)
    {
        return VirtioNetReceiveLifecycleCompletionResult.Create(status, publish, VirtioNetReceiveFrameClassifierStatus.BufferTooSmall, PacketParseStatus.BufferTooSmall, descriptorHead <= ushort.MaxValue ? (ushort)descriptorHead : (ushort)0, bufferIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset, usedByteCount, default);
    }

    private static bool TryMapDescriptorHead(VirtioNetReceiveLifecyclePublishResult publish, uint completedDescriptorHead, out ushort descriptorHead, out ushort bufferIndex)
    {
        descriptorHead = 0;
        bufferIndex = 0;
        uint first = publish.FirstDescriptorIndex;
        uint end = first + publish.PublishedDescriptorCount;
        if (completedDescriptorHead < first || completedDescriptorHead >= end || completedDescriptorHead > ushort.MaxValue)
        {
            return false;
        }

        descriptorHead = (ushort)completedDescriptorHead;
        bufferIndex = (ushort)(completedDescriptorHead - first);
        return true;
    }

    private static ushort GetRingSlot(ushort index, ushort queueSize)
    {
        return (ushort)(index & (queueSize - 1));
    }

    private static bool TryGetDescriptorOffset(VirtqueueSplitRingPlan queue, ushort descriptorIndex, out uint offset)
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

    private static uint GetAvailableRingEntryOffset(VirtqueueSplitRingPlan queue, ushort slot)
    {
        return queue.AvailableRing.Offset + VirtqueueSplitRingPlan.AvailableRingHeaderLength + ((uint)slot * VirtqueueSplitRingPlan.AvailableRingEntryLength);
    }

    private static bool TryGetUsedRingElementOffset(VirtqueueSplitRingPlan queue, ushort slot, out uint offset)
    {
        ulong value = (ulong)queue.UsedRing.Offset + VirtqueueSplitRingPlan.UsedRingHeaderLength + ((ulong)slot * VirtqueueSplitRingPlan.UsedRingElementLength);
        if (value > uint.MaxValue)
        {
            offset = 0;
            return false;
        }

        offset = (uint)value;
        return true;
    }

    private static bool TryValidateRange(uint offset, uint length, int bufferLength)
    {
        ulong end = (ulong)offset + length;
        return end <= int.MaxValue && end <= (ulong)bufferLength;
    }

    private static bool TryMultiply(ushort left, uint right, out ulong result)
    {
        result = (ulong)left * right;
        return result / right == left;
    }

    private static void WriteDescriptor(Span<byte> queueMemory, VirtqueueSplitRingPlan queue, ushort descriptorIndex, ulong physicalAddress, uint length)
    {
        uint descriptorOffset = queue.DescriptorTable.Offset + ((uint)descriptorIndex * DescriptorEntryLength);
        Span<byte> descriptor = queueMemory.Slice((int)descriptorOffset, (int)DescriptorEntryLength);
        EndianPrimitives.WriteUInt64LittleEndian(descriptor.Slice((int)DescriptorAddressOffset, sizeof(ulong)), physicalAddress);
        EndianPrimitives.WriteUInt32LittleEndian(descriptor.Slice((int)DescriptorLengthOffset, sizeof(uint)), length);
        EndianPrimitives.WriteUInt16LittleEndian(descriptor.Slice((int)DescriptorFlagsOffset, sizeof(ushort)), DescriptorFlagWrite);
        EndianPrimitives.WriteUInt16LittleEndian(descriptor.Slice((int)DescriptorNextOffset, sizeof(ushort)), 0);
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, uint offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(source.Slice((int)offset, sizeof(ushort)));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> source, uint offset)
    {
        return EndianPrimitives.ReadUInt32LittleEndian(source.Slice((int)offset, sizeof(uint)));
    }

    private static void WriteUInt16(Span<byte> destination, uint offset, ushort value)
    {
        EndianPrimitives.WriteUInt16LittleEndian(destination.Slice((int)offset, sizeof(ushort)), value);
    }
}
