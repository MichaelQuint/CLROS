namespace OS.Drivers;

using OS.Net;
using OS.Std.Binary;

public static class VirtioNetReceiveRegisterWindowSlabCompletionPlanner
{
    public const bool RequiresSuccessfulRegisterWindowReadiness = true;
    public const bool SelectsCallerOwnedReceiveBufferFromSlab = true;
    public const bool ConsumesRegisterWindowNotificationProvenance = true;
    public const bool RequiresCallerOwnedQueueMemory = true;
    public const bool RequiresCallerOwnedReceiveBufferSlab = true;
    public const bool ReadsQueueMemory = true;
    public const bool ReadsCallerOwnedReceiveBuffer = true;
    public const bool ClassifiesCallerOwnedFrame = true;
    public const bool PerformsLiveMmio = false;
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
    public const bool PerformsPacketIo = false;
    public const bool PerformsPacketReceive = false;
    public const bool CopiesPayload = false;
    public const bool WritesQueueMemory = false;
    private const uint UsedRingIndexOffset = 2;
    private const uint UsedRingIndexLength = sizeof(ushort);
    private const uint UsedRingElementIdOffset = 0;
    private const uint UsedRingElementLengthOffset = 4;
    private const uint UsedRingElementLength = VirtqueueSplitRingPlan.UsedRingElementLength;
    public static VirtioNetReceiveRegisterWindowSlabCompletionResult CompleteReceiveFromSlab(VirtioNetReceiveReadinessResult readiness, ReadOnlySpan<byte> queueMemory, ReadOnlySpan<byte> receiveBufferSlab)
    {
        if (!readiness.Succeeded)
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromReadinessFailure(readiness, receiveBufferSlab.Length);
        }

        VirtioNetReceiveLifecyclePublishResult publish = readiness.ReceivePublish;
        ushort usedRingSlot = (ushort)(publish.PreviousUsedIndex & (publish.QueueSize - 1));
        uint usedRingIndexOffset = publish.QueuePlan.UsedRing.Offset + UsedRingIndexOffset;
        if (!TryValidateRange(usedRingIndexOffset, UsedRingIndexLength, queueMemory.Length) || !TryGetUsedRingElementOffset(publish.QueuePlan, usedRingSlot, out uint usedRingElementOffset) || !TryValidateRange(usedRingElementOffset, UsedRingElementLength, queueMemory.Length))
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromSelectionFailure(VirtioNetReceiveRegisterWindowSlabCompletionStatus.QueueMemoryTooSmall, readiness, VirtioNetReceiveLifecycleCompletionStatus.QueueMemoryTooSmall, receiveBufferSlab.Length, descriptorHead: 0, bufferIndex: 0, selectedBufferOffset: 0, selectedBufferLength: 0, publish.PreviousUsedIndex, usedRingSlot, usedRingElementOffset: 0, usedByteCount: 0);
        }

        ushort currentUsedIndex = ReadUInt16(queueMemory, usedRingIndexOffset);
        if (currentUsedIndex == publish.PreviousUsedIndex)
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromSelectionFailure(VirtioNetReceiveRegisterWindowSlabCompletionStatus.NoCompletion, readiness, VirtioNetReceiveLifecycleCompletionStatus.NoCompletion, receiveBufferSlab.Length, descriptorHead: 0, bufferIndex: 0, selectedBufferOffset: 0, selectedBufferLength: 0, currentUsedIndex, usedRingSlot, usedRingElementOffset, usedByteCount: 0);
        }

        if (currentUsedIndex != publish.ExpectedNextUsedIndex)
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromSelectionFailure(VirtioNetReceiveRegisterWindowSlabCompletionStatus.UnexpectedUsedIndex, readiness, VirtioNetReceiveLifecycleCompletionStatus.UnexpectedUsedIndex, receiveBufferSlab.Length, descriptorHead: 0, bufferIndex: 0, selectedBufferOffset: 0, selectedBufferLength: 0, currentUsedIndex, usedRingSlot, usedRingElementOffset, usedByteCount: 0);
        }

        uint descriptorHead = ReadUInt32(queueMemory, usedRingElementOffset + UsedRingElementIdOffset);
        uint usedByteCount = ReadUInt32(queueMemory, usedRingElementOffset + UsedRingElementLengthOffset);
        if (!TryMapDescriptorHead(publish, descriptorHead, out ushort descriptorIndex, out ushort bufferIndex))
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromSelectionFailure(VirtioNetReceiveRegisterWindowSlabCompletionStatus.UnexpectedDescriptorHead, readiness, VirtioNetReceiveLifecycleCompletionStatus.UnexpectedDescriptorHead, receiveBufferSlab.Length, descriptorIndex, bufferIndex, selectedBufferOffset: 0, selectedBufferLength: 0, currentUsedIndex, usedRingSlot, usedRingElementOffset, usedByteCount);
        }

        if (!TryGetSelectedBufferRange(publish, bufferIndex, receiveBufferSlab.Length, out uint selectedBufferOffset, out uint selectedBufferLength))
        {
            return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromSelectionFailure(VirtioNetReceiveRegisterWindowSlabCompletionStatus.ReceiveSlabTooSmall, readiness, VirtioNetReceiveLifecycleCompletionStatus.ReceiveBufferTooSmall, receiveBufferSlab.Length, descriptorIndex, bufferIndex, selectedBufferOffset, selectedBufferLength, currentUsedIndex, usedRingSlot, usedRingElementOffset, usedByteCount);
        }

        VirtioNetReceiveRegisterWindowCompletionResult completion = VirtioNetReceiveRegisterWindowCompletionPlanner.CompleteReceive(readiness, queueMemory, receiveBufferSlab.Slice((int)selectedBufferOffset, (int)selectedBufferLength));
        return VirtioNetReceiveRegisterWindowSlabCompletionResult.FromCompletion(readiness, receiveBufferSlab.Length, selectedBufferOffset, selectedBufferLength, completion);
    }

    private static bool TryMapDescriptorHead(VirtioNetReceiveLifecyclePublishResult publish, uint descriptorHead, out ushort descriptorIndex, out ushort bufferIndex)
    {
        descriptorIndex = 0;
        bufferIndex = 0;
        if (descriptorHead > ushort.MaxValue || descriptorHead < publish.FirstDescriptorIndex)
        {
            return false;
        }

        uint relativeIndex = descriptorHead - publish.FirstDescriptorIndex;
        if (relativeIndex >= publish.PublishedDescriptorCount || relativeIndex > ushort.MaxValue)
        {
            return false;
        }

        descriptorIndex = (ushort)descriptorHead;
        bufferIndex = (ushort)relativeIndex;
        return true;
    }

    private static bool TryGetSelectedBufferRange(VirtioNetReceiveLifecyclePublishResult publish, ushort bufferIndex, int slabLength, out uint selectedBufferOffset, out uint selectedBufferLength)
    {
        selectedBufferOffset = 0;
        selectedBufferLength = publish.FrameBufferBytes;
        ulong offset = (ulong)bufferIndex * publish.ReceiveBufferStrideBytes;
        ulong end = offset + publish.FrameBufferBytes;
        if (offset > uint.MaxValue || end > uint.MaxValue || end > (ulong)slabLength)
        {
            selectedBufferOffset = offset <= uint.MaxValue ? (uint)offset : 0;
            return false;
        }

        selectedBufferOffset = (uint)offset;
        return true;
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

    private static bool TryValidateRange(uint offset, uint length, int sourceLength)
    {
        ulong end = (ulong)offset + length;
        return end <= int.MaxValue && end <= (ulong)sourceLength;
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> source, uint offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(source.Slice((int)offset, sizeof(ushort)));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> source, uint offset)
    {
        return EndianPrimitives.ReadUInt32LittleEndian(source.Slice((int)offset, sizeof(uint)));
    }
}
