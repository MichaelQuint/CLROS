namespace OS.Drivers;
using OS.Std.Binary;

public static class VirtioNetTransmitCompletionPlanner
{
    public const uint ExpectedTransmitUsedByteCount = 0;
    public const bool PerformsMmio = false;
    public const bool PollsDevice = false;
    public const bool HandlesInterrupts = false;
    public const bool NotifiesDevice = false;
    public const bool PerformsDmaMapping = false;
    public const bool PerformsPacketIo = false;
    public const bool MutatesDescriptorAllocator = false;
    private const uint UsedRingIndexOffset = 2;
    private const uint UsedRingIndexLength = sizeof(ushort);
    private const uint UsedRingElementIdOffset = 0;
    private const uint UsedRingElementLengthOffset = 4;
    private const uint UsedRingElementLength = VirtqueueSplitRingPlan.UsedRingElementLength;
    public static VirtioNetTransmitCompletionPlanResult Create(VirtioNetTransmitLifecycleResult transmitPublish, VirtioNetQueueNotificationExecutionResult notification, ushort previousUsedIndex, ReadOnlySpan<byte> queueMemory)
    {
        return CreateCore(transmitPublish, VirtioNetTransmitCompletionPlanResult.NotificationProvenance.FromFakeSink(notification), previousUsedIndex, queueMemory);
    }

    public static VirtioNetTransmitCompletionPlanResult Create(VirtioNetTransmitLifecycleResult transmitPublish, VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notification, ushort previousUsedIndex, ReadOnlySpan<byte> queueMemory)
    {
        return CreateCore(transmitPublish, VirtioNetTransmitCompletionPlanResult.NotificationProvenance.FromRegisterWindow(notification), previousUsedIndex, queueMemory);
    }

    private static VirtioNetTransmitCompletionPlanResult CreateCore(VirtioNetTransmitLifecycleResult transmitPublish, VirtioNetTransmitCompletionPlanResult.NotificationProvenance notification, ushort previousUsedIndex, ReadOnlySpan<byte> queueMemory)
    {
        if (!IsValidTransmitPublish(transmitPublish))
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(VirtioNetTransmitCompletionPlanStatus.TransmitPublishFailed, transmitPublish, notification, previousUsedIndex);
        }

        if (!notification.Succeeded)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(VirtioNetTransmitCompletionPlanStatus.NotificationFailed, transmitPublish, notification, previousUsedIndex);
        }

        if (notification.Kind != VirtioNetQueueNotificationKind.Transmit)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(VirtioNetTransmitCompletionPlanStatus.NotificationKindMismatch, transmitPublish, notification, previousUsedIndex);
        }

        if (notification.QueueIndex != VirtioNetQueuePlan.TransmitQueueIndex || notification.PublishedDescriptorCount != transmitPublish.DescriptorsWritten || notification.PreviousAvailableRingIndex != transmitPublish.Plan.AvailableRingIndex || notification.NextAvailableRingIndex != transmitPublish.NextAvailableRingIndex)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(VirtioNetTransmitCompletionPlanStatus.NotificationPublishMismatch, transmitPublish, notification, previousUsedIndex);
        }

        VirtqueueSplitRingPlan txQueue = transmitPublish.Plan.TxQueue;
        ushort usedRingSlot = (ushort)(previousUsedIndex & (txQueue.QueueSize - 1));
        uint usedRingIndexOffset = txQueue.UsedRing.Offset + UsedRingIndexOffset;
        if (!TryValidateRange(usedRingIndexOffset, UsedRingIndexLength, queueMemory.Length) || !TryGetUsedRingElementOffset(txQueue, usedRingSlot, out uint usedRingElementOffset) || !TryValidateRange(usedRingElementOffset, UsedRingElementLength, queueMemory.Length))
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(VirtioNetTransmitCompletionPlanStatus.QueueMemoryTooSmall, transmitPublish, notification, previousUsedIndex, previousUsedIndex, usedRingSlot);
        }

        ushort currentUsedIndex = ReadUInt16(queueMemory, usedRingIndexOffset);
        if (currentUsedIndex == previousUsedIndex)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(VirtioNetTransmitCompletionPlanStatus.NoCompletion, transmitPublish, notification, previousUsedIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset);
        }

        ushort expectedUsedIndex = unchecked((ushort)(previousUsedIndex + 1));
        if (currentUsedIndex != expectedUsedIndex)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(VirtioNetTransmitCompletionPlanStatus.UnexpectedUsedIndex, transmitPublish, notification, previousUsedIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset);
        }

        uint descriptorHead = ReadUInt32(queueMemory, usedRingElementOffset + UsedRingElementIdOffset);
        uint usedByteCount = ReadUInt32(queueMemory, usedRingElementOffset + UsedRingElementLengthOffset);
        if (descriptorHead != transmitPublish.HeadDescriptorIndex)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(VirtioNetTransmitCompletionPlanStatus.UnexpectedDescriptorHead, transmitPublish, notification, previousUsedIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset, descriptorHead, usedByteCount);
        }

        if (usedByteCount != ExpectedTransmitUsedByteCount)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(VirtioNetTransmitCompletionPlanStatus.UnexpectedByteCount, transmitPublish, notification, previousUsedIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset, descriptorHead, usedByteCount);
        }

        return VirtioNetTransmitCompletionPlanResult.Success(transmitPublish, notification, previousUsedIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset, descriptorHead, usedByteCount);
    }

    private static bool IsValidTransmitPublish(VirtioNetTransmitLifecycleResult transmitPublish)
    {
        return transmitPublish.Succeeded && transmitPublish.DescriptorsWritten == VirtioNetTransmitLifecycle.RequiredDescriptorCount && VirtqueueSplitRingPlan.ValidateQueueSize(transmitPublish.Plan.TxQueue.QueueSize) == VirtqueueSplitRingPlanStatus.Success;
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
