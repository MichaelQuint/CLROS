namespace OS.Drivers;

using OS.Std.Binary;

public enum VirtioNetTransmitCompletionPlanStatus
{
    Success = 0,
    TransmitPublishFailed = 1,
    NotificationFailed = 2,
    NotificationKindMismatch = 3,
    NotificationPublishMismatch = 4,
    QueueMemoryTooSmall = 5,
    NoCompletion = 6,
    UnexpectedUsedIndex = 7,
    UnexpectedDescriptorHead = 8,
    UnexpectedByteCount = 9
}

public readonly struct VirtioNetTransmitCompletionPlanResult
{
    private VirtioNetTransmitCompletionPlanResult(
        VirtioNetTransmitCompletionPlanStatus status,
        VirtioNetTransmitLifecycleStatus transmitPublishStatus,
        VirtioNetQueueNotificationExecutionStatus notificationExecutionStatus,
        VirtioNetQueueNotificationPlanStatus notificationPlanStatus,
        VirtioPciQueueNotificationPlanStatus notificationStatus,
        VirtioPciNotifyWriteStatus notificationSinkStatus,
        VirtioNetQueueNotificationKind notificationKind,
        ushort notificationQueueIndex,
        ushort notificationQueueNotifyOffset,
        uint notificationNotifyRelativeOffset,
        ushort notificationWriteValue,
        uint notificationWriteWidthBytes,
        ushort notificationPublishedDescriptorCount,
        ushort notificationPreviousAvailableRingIndex,
        ushort notificationNextAvailableRingIndex,
        bool usesRegisterWindowNotification,
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus notificationWindowExecutionStatus,
        VirtioPciBarRegisterWindowPlanStatus notificationRegisterWindowStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus notificationWindowAccessExecutionStatus,
        bool notificationWindowExecutionAttempted,
        bool notificationBarrierSucceeded,
        uint notificationBarrierSequence,
        VirtioPciCapabilityType notificationCapabilityType,
        byte notificationBarIndex,
        VirtioPciRegisterAccessWidth notificationAccessWidth,
        uint notificationAccessWidthBytes,
        ulong notificationBarRelativeOffset,
        ulong notificationWrittenValue,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        ushort usedRingSlot,
        uint usedRingElementOffset,
        uint descriptorHead,
        uint usedByteCount,
        ushort reclaimedDescriptorCount,
        ushort headerDescriptorIndex,
        ushort frameDescriptorIndex,
        bool isInitialized)
    {
        Status = status;
        TransmitPublishStatus = transmitPublishStatus;
        NotificationExecutionStatus = notificationExecutionStatus;
        NotificationPlanStatus = notificationPlanStatus;
        NotificationStatus = notificationStatus;
        NotificationSinkStatus = notificationSinkStatus;
        NotificationKind = notificationKind;
        NotificationQueueIndex = notificationQueueIndex;
        NotificationQueueNotifyOffset = notificationQueueNotifyOffset;
        NotificationNotifyRelativeOffset = notificationNotifyRelativeOffset;
        NotificationWriteValue = notificationWriteValue;
        NotificationWriteWidthBytes = notificationWriteWidthBytes;
        NotificationPublishedDescriptorCount = notificationPublishedDescriptorCount;
        NotificationPreviousAvailableRingIndex = notificationPreviousAvailableRingIndex;
        NotificationNextAvailableRingIndex = notificationNextAvailableRingIndex;
        UsesRegisterWindowNotification = usesRegisterWindowNotification;
        NotificationWindowExecutionStatus = notificationWindowExecutionStatus;
        NotificationRegisterWindowStatus = notificationRegisterWindowStatus;
        NotificationWindowAccessExecutionStatus = notificationWindowAccessExecutionStatus;
        NotificationWindowExecutionAttempted = notificationWindowExecutionAttempted;
        NotificationBarrierSucceeded = notificationBarrierSucceeded;
        NotificationBarrierSequence = notificationBarrierSequence;
        NotificationCapabilityType = notificationCapabilityType;
        NotificationBarIndex = notificationBarIndex;
        NotificationAccessWidth = notificationAccessWidth;
        NotificationAccessWidthBytes = notificationAccessWidthBytes;
        NotificationBarRelativeOffset = notificationBarRelativeOffset;
        NotificationWrittenValue = notificationWrittenValue;
        PreviousUsedIndex = previousUsedIndex;
        CurrentUsedIndex = currentUsedIndex;
        NextUsedIndex = currentUsedIndex;
        UsedRingSlot = usedRingSlot;
        UsedRingElementOffset = usedRingElementOffset;
        DescriptorHead = descriptorHead;
        UsedByteCount = usedByteCount;
        ReclaimedDescriptorCount = reclaimedDescriptorCount;
        HeaderDescriptorIndex = headerDescriptorIndex;
        FrameDescriptorIndex = frameDescriptorIndex;
        IsInitialized = isInitialized;
    }

    public VirtioNetTransmitCompletionPlanStatus Status { get; }

    public VirtioNetTransmitLifecycleStatus TransmitPublishStatus { get; }

    public VirtioNetQueueNotificationExecutionStatus NotificationExecutionStatus { get; }

    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }

    public VirtioPciQueueNotificationPlanStatus NotificationStatus { get; }

    public VirtioPciNotifyWriteStatus NotificationSinkStatus { get; }

    public VirtioNetQueueNotificationKind NotificationKind { get; }

    public ushort NotificationQueueIndex { get; }

    public ushort NotificationQueueNotifyOffset { get; }

    public uint NotificationNotifyRelativeOffset { get; }

    public ushort NotificationWriteValue { get; }

    public uint NotificationWriteWidthBytes { get; }

    public ushort NotificationPublishedDescriptorCount { get; }

    public ushort NotificationPreviousAvailableRingIndex { get; }

    public ushort NotificationNextAvailableRingIndex { get; }

    public bool UsesRegisterWindowNotification { get; }

    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus NotificationWindowExecutionStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus NotificationRegisterWindowStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus NotificationWindowAccessExecutionStatus { get; }

    public bool NotificationWindowExecutionAttempted { get; }

    public bool NotificationBarrierSucceeded { get; }

    public uint NotificationBarrierSequence { get; }

    public VirtioPciCapabilityType NotificationCapabilityType { get; }

    public byte NotificationBarIndex { get; }

    public VirtioPciRegisterAccessWidth NotificationAccessWidth { get; }

    public uint NotificationAccessWidthBytes { get; }

    public ulong NotificationBarRelativeOffset { get; }

    public ulong NotificationWrittenValue { get; }

    public ushort PreviousUsedIndex { get; }

    public ushort CurrentUsedIndex { get; }

    public ushort NextUsedIndex { get; }

    public ushort UsedRingSlot { get; }

    public uint UsedRingElementOffset { get; }

    public uint DescriptorHead { get; }

    public uint UsedByteCount { get; }

    public ushort ReclaimedDescriptorCount { get; }

    public ushort HeaderDescriptorIndex { get; }

    public ushort FrameDescriptorIndex { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioNetTransmitCompletionPlanStatus.Success;

    internal static VirtioNetTransmitCompletionPlanResult Success(
        VirtioNetTransmitLifecycleResult transmitPublish,
        NotificationProvenance notification,
        ushort previousUsedIndex,
        ushort currentUsedIndex,
        ushort usedRingSlot,
        uint usedRingElementOffset,
        uint descriptorHead,
        uint usedByteCount)
    {
        return new VirtioNetTransmitCompletionPlanResult(
            VirtioNetTransmitCompletionPlanStatus.Success,
            transmitPublish.Status,
            notification.ExecutionStatus,
            notification.NotificationPlanStatus,
            notification.NotificationStatus,
            notification.SinkStatus,
            notification.Kind,
            notification.QueueIndex,
            notification.QueueNotifyOffset,
            notification.NotifyRelativeOffset,
            notification.WriteValue,
            notification.WriteWidthBytes,
            notification.PublishedDescriptorCount,
            notification.PreviousAvailableRingIndex,
            notification.NextAvailableRingIndex,
            notification.UsesRegisterWindowNotification,
            notification.WindowExecutionStatus,
            notification.RegisterWindowStatus,
            notification.WindowAccessExecutionStatus,
            notification.WindowExecutionAttempted,
            notification.BarrierSucceeded,
            notification.BarrierSequence,
            notification.CapabilityType,
            notification.BarIndex,
            notification.AccessWidth,
            notification.AccessWidthBytes,
            notification.BarRelativeOffset,
            notification.WrittenValue,
            previousUsedIndex,
            currentUsedIndex,
            usedRingSlot,
            usedRingElementOffset,
            descriptorHead,
            usedByteCount,
            VirtioNetTransmitLifecycle.RequiredDescriptorCount,
            transmitPublish.Plan.HeaderDescriptor.DescriptorIndex,
            transmitPublish.Plan.FrameDescriptor.DescriptorIndex,
            isInitialized: true);
    }

    internal static VirtioNetTransmitCompletionPlanResult Fail(
        VirtioNetTransmitCompletionPlanStatus status,
        VirtioNetTransmitLifecycleResult transmitPublish,
        NotificationProvenance notification,
        ushort previousUsedIndex,
        ushort currentUsedIndex = 0,
        ushort usedRingSlot = 0,
        uint usedRingElementOffset = 0,
        uint descriptorHead = 0,
        uint usedByteCount = 0)
    {
        return new VirtioNetTransmitCompletionPlanResult(
            status,
            transmitPublish.Status,
            notification.ExecutionStatus,
            notification.NotificationPlanStatus,
            notification.NotificationStatus,
            notification.SinkStatus,
            notification.Kind,
            notification.QueueIndex,
            notification.QueueNotifyOffset,
            notification.NotifyRelativeOffset,
            notification.WriteValue,
            notification.WriteWidthBytes,
            notification.PublishedDescriptorCount,
            notification.PreviousAvailableRingIndex,
            notification.NextAvailableRingIndex,
            notification.UsesRegisterWindowNotification,
            notification.WindowExecutionStatus,
            notification.RegisterWindowStatus,
            notification.WindowAccessExecutionStatus,
            notification.WindowExecutionAttempted,
            notification.BarrierSucceeded,
            notification.BarrierSequence,
            notification.CapabilityType,
            notification.BarIndex,
            notification.AccessWidth,
            notification.AccessWidthBytes,
            notification.BarRelativeOffset,
            notification.WrittenValue,
            previousUsedIndex,
            currentUsedIndex,
            usedRingSlot,
            usedRingElementOffset,
            descriptorHead,
            usedByteCount,
            reclaimedDescriptorCount: 0,
            headerDescriptorIndex: 0,
            frameDescriptorIndex: 0,
            isInitialized: true);
    }

    internal readonly struct NotificationProvenance
    {
        private NotificationProvenance(
            bool usesRegisterWindowNotification,
            bool succeeded,
            VirtioNetQueueNotificationExecutionStatus executionStatus,
            VirtioNetQueueNotificationPlanStatus notificationPlanStatus,
            VirtioPciQueueNotificationPlanStatus notificationStatus,
            VirtioPciNotifyWriteStatus sinkStatus,
            VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus windowExecutionStatus,
            VirtioPciBarRegisterWindowPlanStatus registerWindowStatus,
            VirtioPciBarRegisterWindowAccessExecutionStatus windowAccessExecutionStatus,
            bool windowExecutionAttempted,
            bool barrierSucceeded,
            uint barrierSequence,
            VirtioNetQueueNotificationKind kind,
            ushort queueIndex,
            ushort queueNotifyOffset,
            uint notifyRelativeOffset,
            ushort writeValue,
            uint writeWidthBytes,
            ushort publishedDescriptorCount,
            ushort previousAvailableRingIndex,
            ushort nextAvailableRingIndex,
            VirtioPciCapabilityType capabilityType,
            byte barIndex,
            VirtioPciRegisterAccessWidth accessWidth,
            uint accessWidthBytes,
            ulong barRelativeOffset,
            ulong writtenValue)
        {
            UsesRegisterWindowNotification = usesRegisterWindowNotification;
            Succeeded = succeeded;
            ExecutionStatus = executionStatus;
            NotificationPlanStatus = notificationPlanStatus;
            NotificationStatus = notificationStatus;
            SinkStatus = sinkStatus;
            WindowExecutionStatus = windowExecutionStatus;
            RegisterWindowStatus = registerWindowStatus;
            WindowAccessExecutionStatus = windowAccessExecutionStatus;
            WindowExecutionAttempted = windowExecutionAttempted;
            BarrierSucceeded = barrierSucceeded;
            BarrierSequence = barrierSequence;
            Kind = kind;
            QueueIndex = queueIndex;
            QueueNotifyOffset = queueNotifyOffset;
            NotifyRelativeOffset = notifyRelativeOffset;
            WriteValue = writeValue;
            WriteWidthBytes = writeWidthBytes;
            PublishedDescriptorCount = publishedDescriptorCount;
            PreviousAvailableRingIndex = previousAvailableRingIndex;
            NextAvailableRingIndex = nextAvailableRingIndex;
            CapabilityType = capabilityType;
            BarIndex = barIndex;
            AccessWidth = accessWidth;
            AccessWidthBytes = accessWidthBytes;
            BarRelativeOffset = barRelativeOffset;
            WrittenValue = writtenValue;
        }

        public bool UsesRegisterWindowNotification { get; }

        public bool Succeeded { get; }

        public VirtioNetQueueNotificationExecutionStatus ExecutionStatus { get; }

        public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }

        public VirtioPciQueueNotificationPlanStatus NotificationStatus { get; }

        public VirtioPciNotifyWriteStatus SinkStatus { get; }

        public VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus WindowExecutionStatus { get; }

        public VirtioPciBarRegisterWindowPlanStatus RegisterWindowStatus { get; }

        public VirtioPciBarRegisterWindowAccessExecutionStatus WindowAccessExecutionStatus { get; }

        public bool WindowExecutionAttempted { get; }

        public bool BarrierSucceeded { get; }

        public uint BarrierSequence { get; }

        public VirtioNetQueueNotificationKind Kind { get; }

        public ushort QueueIndex { get; }

        public ushort QueueNotifyOffset { get; }

        public uint NotifyRelativeOffset { get; }

        public ushort WriteValue { get; }

        public uint WriteWidthBytes { get; }

        public ushort PublishedDescriptorCount { get; }

        public ushort PreviousAvailableRingIndex { get; }

        public ushort NextAvailableRingIndex { get; }

        public VirtioPciCapabilityType CapabilityType { get; }

        public byte BarIndex { get; }

        public VirtioPciRegisterAccessWidth AccessWidth { get; }

        public uint AccessWidthBytes { get; }

        public ulong BarRelativeOffset { get; }

        public ulong WrittenValue { get; }

        public static NotificationProvenance FromFakeSink(
            VirtioNetQueueNotificationExecutionResult notification)
        {
            return new NotificationProvenance(
                false,
                notification.Succeeded,
                notification.Status,
                notification.NotificationPlanStatus,
                notification.PciNotificationStatus,
                notification.SinkStatus,
                VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success,
                VirtioPciBarRegisterWindowPlanStatus.Success,
                VirtioPciBarRegisterWindowAccessExecutionStatus.Success,
                false,
                notification.BarrierSucceeded,
                notification.BarrierSequence,
                notification.Kind,
                notification.QueueIndex,
                notification.QueueNotifyOffset,
                notification.NotifyRelativeOffset,
                notification.WriteValue,
                notification.WriteWidthBytes,
                notification.PublishedDescriptorCount,
                notification.PreviousAvailableRingIndex,
                notification.NextAvailableRingIndex,
                default,
                0,
                default,
                0,
                0,
                0);
        }

        public static NotificationProvenance FromRegisterWindow(
            VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notification)
        {
            return new NotificationProvenance(
                true,
                notification.Succeeded,
                MapRegisterWindowStatus(notification.Status),
                notification.NotificationPlanStatus,
                notification.PciNotificationStatus,
                VirtioPciNotifyWriteStatus.Success,
                notification.Status,
                notification.RegisterWindowPlanStatus,
                notification.AccessExecutionStatus,
                notification.RegisterWindowExecutionAttempted,
                notification.BarrierSucceeded,
                notification.BarrierSequence,
                notification.Kind,
                notification.QueueIndex,
                notification.QueueNotifyOffset,
                notification.NotifyRelativeOffset,
                notification.WriteValue,
                notification.WriteWidthBytes,
                notification.PublishedDescriptorCount,
                notification.PreviousAvailableRingIndex,
                notification.NextAvailableRingIndex,
                notification.CapabilityType,
                notification.BarIndex,
                notification.AccessWidth,
                notification.AccessWidthBytes,
                notification.BarRelativeOffset,
                notification.WrittenValue);
        }

        private static VirtioNetQueueNotificationExecutionStatus MapRegisterWindowStatus(
            VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus status)
        {
            return status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success
                ? VirtioNetQueueNotificationExecutionStatus.Success
                : status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.NotificationPlanFailed
                    ? VirtioNetQueueNotificationExecutionStatus.NotificationPlanFailed
                    : status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.MemoryBarrierNotReady
                        ? VirtioNetQueueNotificationExecutionStatus.MemoryBarrierNotReady
                        : status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.UnsupportedWriteWidth
                            ? VirtioNetQueueNotificationExecutionStatus.UnsupportedWriteWidth
                            : VirtioNetQueueNotificationExecutionStatus.SinkWriteFailed;
        }
    }
}

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

    public static VirtioNetTransmitCompletionPlanResult Create(
        VirtioNetTransmitLifecycleResult transmitPublish,
        VirtioNetQueueNotificationExecutionResult notification,
        ushort previousUsedIndex,
        ReadOnlySpan<byte> queueMemory)
    {
        return CreateCore(
            transmitPublish,
            VirtioNetTransmitCompletionPlanResult.NotificationProvenance.FromFakeSink(notification),
            previousUsedIndex,
            queueMemory);
    }

    public static VirtioNetTransmitCompletionPlanResult Create(
        VirtioNetTransmitLifecycleResult transmitPublish,
        VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notification,
        ushort previousUsedIndex,
        ReadOnlySpan<byte> queueMemory)
    {
        return CreateCore(
            transmitPublish,
            VirtioNetTransmitCompletionPlanResult.NotificationProvenance.FromRegisterWindow(notification),
            previousUsedIndex,
            queueMemory);
    }

    private static VirtioNetTransmitCompletionPlanResult CreateCore(
        VirtioNetTransmitLifecycleResult transmitPublish,
        VirtioNetTransmitCompletionPlanResult.NotificationProvenance notification,
        ushort previousUsedIndex,
        ReadOnlySpan<byte> queueMemory)
    {
        if (!IsValidTransmitPublish(transmitPublish))
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(
                VirtioNetTransmitCompletionPlanStatus.TransmitPublishFailed,
                transmitPublish,
                notification,
                previousUsedIndex);
        }

        if (!notification.Succeeded)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(
                VirtioNetTransmitCompletionPlanStatus.NotificationFailed,
                transmitPublish,
                notification,
                previousUsedIndex);
        }

        if (notification.Kind != VirtioNetQueueNotificationKind.Transmit)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(
                VirtioNetTransmitCompletionPlanStatus.NotificationKindMismatch,
                transmitPublish,
                notification,
                previousUsedIndex);
        }

        if (notification.QueueIndex != VirtioNetQueuePlan.TransmitQueueIndex ||
            notification.PublishedDescriptorCount != transmitPublish.DescriptorsWritten ||
            notification.PreviousAvailableRingIndex != transmitPublish.Plan.AvailableRingIndex ||
            notification.NextAvailableRingIndex != transmitPublish.NextAvailableRingIndex)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(
                VirtioNetTransmitCompletionPlanStatus.NotificationPublishMismatch,
                transmitPublish,
                notification,
                previousUsedIndex);
        }

        VirtqueueSplitRingPlan txQueue = transmitPublish.Plan.TxQueue;
        ushort usedRingSlot = (ushort)(previousUsedIndex & (txQueue.QueueSize - 1));
        uint usedRingIndexOffset = txQueue.UsedRing.Offset + UsedRingIndexOffset;
        if (!TryValidateRange(usedRingIndexOffset, UsedRingIndexLength, queueMemory.Length) ||
            !TryGetUsedRingElementOffset(txQueue, usedRingSlot, out uint usedRingElementOffset) ||
            !TryValidateRange(usedRingElementOffset, UsedRingElementLength, queueMemory.Length))
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(
                VirtioNetTransmitCompletionPlanStatus.QueueMemoryTooSmall,
                transmitPublish,
                notification,
                previousUsedIndex,
                previousUsedIndex,
                usedRingSlot);
        }

        ushort currentUsedIndex = ReadUInt16(queueMemory, usedRingIndexOffset);
        if (currentUsedIndex == previousUsedIndex)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(
                VirtioNetTransmitCompletionPlanStatus.NoCompletion,
                transmitPublish,
                notification,
                previousUsedIndex,
                currentUsedIndex,
                usedRingSlot,
                usedRingElementOffset);
        }

        ushort expectedUsedIndex = unchecked((ushort)(previousUsedIndex + 1));
        if (currentUsedIndex != expectedUsedIndex)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(
                VirtioNetTransmitCompletionPlanStatus.UnexpectedUsedIndex,
                transmitPublish,
                notification,
                previousUsedIndex,
                currentUsedIndex,
                usedRingSlot,
                usedRingElementOffset);
        }

        uint descriptorHead = ReadUInt32(
            queueMemory,
            usedRingElementOffset + UsedRingElementIdOffset);
        uint usedByteCount = ReadUInt32(
            queueMemory,
            usedRingElementOffset + UsedRingElementLengthOffset);

        if (descriptorHead != transmitPublish.HeadDescriptorIndex)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(
                VirtioNetTransmitCompletionPlanStatus.UnexpectedDescriptorHead,
                transmitPublish,
                notification,
                previousUsedIndex,
                currentUsedIndex,
                usedRingSlot,
                usedRingElementOffset,
                descriptorHead,
                usedByteCount);
        }

        if (usedByteCount != ExpectedTransmitUsedByteCount)
        {
            return VirtioNetTransmitCompletionPlanResult.Fail(
                VirtioNetTransmitCompletionPlanStatus.UnexpectedByteCount,
                transmitPublish,
                notification,
                previousUsedIndex,
                currentUsedIndex,
                usedRingSlot,
                usedRingElementOffset,
                descriptorHead,
                usedByteCount);
        }

        return VirtioNetTransmitCompletionPlanResult.Success(
            transmitPublish,
            notification,
            previousUsedIndex,
            currentUsedIndex,
            usedRingSlot,
            usedRingElementOffset,
            descriptorHead,
            usedByteCount);
    }

    private static bool IsValidTransmitPublish(
        VirtioNetTransmitLifecycleResult transmitPublish)
    {
        return transmitPublish.Succeeded &&
            transmitPublish.DescriptorsWritten == VirtioNetTransmitLifecycle.RequiredDescriptorCount &&
            VirtqueueSplitRingPlan.ValidateQueueSize(transmitPublish.Plan.TxQueue.QueueSize) ==
                VirtqueueSplitRingPlanStatus.Success;
    }

    private static bool TryGetUsedRingElementOffset(
        VirtqueueSplitRingPlan queue,
        ushort slot,
        out uint offset)
    {
        ulong value = (ulong)queue.UsedRing.Offset +
            VirtqueueSplitRingPlan.UsedRingHeaderLength +
            ((ulong)slot * VirtqueueSplitRingPlan.UsedRingElementLength);
        if (value > uint.MaxValue)
        {
            offset = 0;
            return false;
        }

        offset = (uint)value;
        return true;
    }

    private static bool TryValidateRange(
        uint offset,
        uint length,
        int sourceLength)
    {
        ulong end = (ulong)offset + length;
        return end <= int.MaxValue && end <= (ulong)sourceLength;
    }

    private static ushort ReadUInt16(
        ReadOnlySpan<byte> source,
        uint offset)
    {
        return EndianPrimitives.ReadUInt16LittleEndian(
            source.Slice((int)offset, sizeof(ushort)));
    }

    private static uint ReadUInt32(
        ReadOnlySpan<byte> source,
        uint offset)
    {
        return EndianPrimitives.ReadUInt32LittleEndian(
            source.Slice((int)offset, sizeof(uint)));
    }
}
