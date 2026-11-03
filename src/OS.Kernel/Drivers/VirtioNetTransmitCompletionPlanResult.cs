namespace OS.Drivers;

using OS.Std.Binary;

public readonly partial struct VirtioNetTransmitCompletionPlanResult
{
    private VirtioNetTransmitCompletionPlanResult(VirtioNetTransmitCompletionPlanStatus status, VirtioNetTransmitLifecycleStatus transmitPublishStatus, VirtioNetQueueNotificationExecutionStatus notificationExecutionStatus, VirtioNetQueueNotificationPlanStatus notificationPlanStatus, VirtioPciQueueNotificationPlanStatus notificationStatus, VirtioPciNotifyWriteStatus notificationSinkStatus, VirtioNetQueueNotificationKind notificationKind, ushort notificationQueueIndex, ushort notificationQueueNotifyOffset, uint notificationNotifyRelativeOffset, ushort notificationWriteValue, uint notificationWriteWidthBytes, ushort notificationPublishedDescriptorCount, ushort notificationPreviousAvailableRingIndex, ushort notificationNextAvailableRingIndex, bool usesRegisterWindowNotification, VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus notificationWindowExecutionStatus, VirtioPciBarRegisterWindowPlanStatus notificationRegisterWindowStatus, VirtioPciBarRegisterWindowAccessExecutionStatus notificationWindowAccessExecutionStatus, bool notificationWindowExecutionAttempted, bool notificationBarrierSucceeded, uint notificationBarrierSequence, VirtioPciCapabilityType notificationCapabilityType, byte notificationBarIndex, VirtioPciRegisterAccessWidth notificationAccessWidth, uint notificationAccessWidthBytes, ulong notificationBarRelativeOffset, ulong notificationWrittenValue, ushort previousUsedIndex, ushort currentUsedIndex, ushort usedRingSlot, uint usedRingElementOffset, uint descriptorHead, uint usedByteCount, ushort reclaimedDescriptorCount, ushort headerDescriptorIndex, ushort frameDescriptorIndex, bool isInitialized)
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

    public VirtioNetTransmitCompletionPlanStatus Status
    {
        get;
    }
    public VirtioNetTransmitLifecycleStatus TransmitPublishStatus
    {
        get;
    }
    public VirtioNetQueueNotificationExecutionStatus NotificationExecutionStatus
    {
        get;
    }
    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus
    {
        get;
    }
    public VirtioPciQueueNotificationPlanStatus NotificationStatus
    {
        get;
    }
    public VirtioPciNotifyWriteStatus NotificationSinkStatus
    {
        get;
    }
    public VirtioNetQueueNotificationKind NotificationKind
    {
        get;
    }
    public ushort NotificationQueueIndex
    {
        get;
    }
    public ushort NotificationQueueNotifyOffset
    {
        get;
    }
    public uint NotificationNotifyRelativeOffset
    {
        get;
    }
    public ushort NotificationWriteValue
    {
        get;
    }
    public uint NotificationWriteWidthBytes
    {
        get;
    }
    public ushort NotificationPublishedDescriptorCount
    {
        get;
    }
    public ushort NotificationPreviousAvailableRingIndex
    {
        get;
    }
    public ushort NotificationNextAvailableRingIndex
    {
        get;
    }
    public bool UsesRegisterWindowNotification
    {
        get;
    }
    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus NotificationWindowExecutionStatus
    {
        get;
    }
    public VirtioPciBarRegisterWindowPlanStatus NotificationRegisterWindowStatus
    {
        get;
    }
    public VirtioPciBarRegisterWindowAccessExecutionStatus NotificationWindowAccessExecutionStatus
    {
        get;
    }
    public bool NotificationWindowExecutionAttempted
    {
        get;
    }
    public bool NotificationBarrierSucceeded
    {
        get;
    }
    public uint NotificationBarrierSequence
    {
        get;
    }
    public VirtioPciCapabilityType NotificationCapabilityType
    {
        get;
    }
    public byte NotificationBarIndex
    {
        get;
    }
    public VirtioPciRegisterAccessWidth NotificationAccessWidth
    {
        get;
    }
    public uint NotificationAccessWidthBytes
    {
        get;
    }
    public ulong NotificationBarRelativeOffset
    {
        get;
    }
    public ulong NotificationWrittenValue
    {
        get;
    }
    public ushort PreviousUsedIndex
    {
        get;
    }
    public ushort CurrentUsedIndex
    {
        get;
    }
    public ushort NextUsedIndex
    {
        get;
    }
    public ushort UsedRingSlot
    {
        get;
    }
    public uint UsedRingElementOffset
    {
        get;
    }
    public uint DescriptorHead
    {
        get;
    }
    public uint UsedByteCount
    {
        get;
    }
    public ushort ReclaimedDescriptorCount
    {
        get;
    }
    public ushort HeaderDescriptorIndex
    {
        get;
    }
    public ushort FrameDescriptorIndex
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetTransmitCompletionPlanStatus.Success;

    internal static VirtioNetTransmitCompletionPlanResult Success(VirtioNetTransmitLifecycleResult transmitPublish, NotificationProvenance notification, ushort previousUsedIndex, ushort currentUsedIndex, ushort usedRingSlot, uint usedRingElementOffset, uint descriptorHead, uint usedByteCount)
    {
        return new VirtioNetTransmitCompletionPlanResult(VirtioNetTransmitCompletionPlanStatus.Success, transmitPublish.Status, notification.ExecutionStatus, notification.NotificationPlanStatus, notification.NotificationStatus, notification.SinkStatus, notification.Kind, notification.QueueIndex, notification.QueueNotifyOffset, notification.NotifyRelativeOffset, notification.WriteValue, notification.WriteWidthBytes, notification.PublishedDescriptorCount, notification.PreviousAvailableRingIndex, notification.NextAvailableRingIndex, notification.UsesRegisterWindowNotification, notification.WindowExecutionStatus, notification.RegisterWindowStatus, notification.WindowAccessExecutionStatus, notification.WindowExecutionAttempted, notification.BarrierSucceeded, notification.BarrierSequence, notification.CapabilityType, notification.BarIndex, notification.AccessWidth, notification.AccessWidthBytes, notification.BarRelativeOffset, notification.WrittenValue, previousUsedIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset, descriptorHead, usedByteCount, VirtioNetTransmitLifecycle.RequiredDescriptorCount, transmitPublish.Plan.HeaderDescriptor.DescriptorIndex, transmitPublish.Plan.FrameDescriptor.DescriptorIndex, isInitialized: true);
    }

    internal static VirtioNetTransmitCompletionPlanResult Fail(VirtioNetTransmitCompletionPlanStatus status, VirtioNetTransmitLifecycleResult transmitPublish, NotificationProvenance notification, ushort previousUsedIndex, ushort currentUsedIndex = 0, ushort usedRingSlot = 0, uint usedRingElementOffset = 0, uint descriptorHead = 0, uint usedByteCount = 0)
    {
        return new VirtioNetTransmitCompletionPlanResult(status, transmitPublish.Status, notification.ExecutionStatus, notification.NotificationPlanStatus, notification.NotificationStatus, notification.SinkStatus, notification.Kind, notification.QueueIndex, notification.QueueNotifyOffset, notification.NotifyRelativeOffset, notification.WriteValue, notification.WriteWidthBytes, notification.PublishedDescriptorCount, notification.PreviousAvailableRingIndex, notification.NextAvailableRingIndex, notification.UsesRegisterWindowNotification, notification.WindowExecutionStatus, notification.RegisterWindowStatus, notification.WindowAccessExecutionStatus, notification.WindowExecutionAttempted, notification.BarrierSucceeded, notification.BarrierSequence, notification.CapabilityType, notification.BarIndex, notification.AccessWidth, notification.AccessWidthBytes, notification.BarRelativeOffset, notification.WrittenValue, previousUsedIndex, currentUsedIndex, usedRingSlot, usedRingElementOffset, descriptorHead, usedByteCount, reclaimedDescriptorCount: 0, headerDescriptorIndex: 0, frameDescriptorIndex: 0, isInitialized: true);
    }
}
