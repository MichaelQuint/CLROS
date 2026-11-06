using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult
{
    private VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult(
        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus status,
        VirtioBlockQueueNotificationRegisterWindowPlanStatus planStatus,
        VirtioBlockQueueNotifyOffsetPlanStatus notifyOffsetPlanStatus,
        VirtioBlockQueueNotificationMetadataStatus notificationMetadataStatus,
        VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        uint notifyOffsetMultiplier,
        uint notifyRelativeOffset,
        ushort publishedDescriptorCount,
        ushort previousAvailableRingIndex,
        ushort nextAvailableRingIndex,
        ushort descriptorHead,
        ushort writeValue,
        uint writeWidthBytes,
        bool descriptorWritesVisible,
        bool availableRingWritesVisible,
        bool orderedBeforeNotify,
        uint barrierSequence,
        bool registerWindowExecutionAttempted,
        VirtioPciCapabilityType capabilityType,
        byte barIndex,
        VirtioPciRegisterAccessWidth accessWidth,
        uint accessWidthBytes,
        ulong barRelativeOffset,
        ulong writtenValue,
        bool isInitialized)
    {
        Status = status;
        PlanStatus = planStatus;
        NotifyOffsetPlanStatus = notifyOffsetPlanStatus;
        NotificationMetadataStatus = notificationMetadataStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
        AccessExecutionStatus = accessExecutionStatus;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
        NotifyRelativeOffset = notifyRelativeOffset;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        DescriptorHead = descriptorHead;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        DescriptorWritesVisible = descriptorWritesVisible;
        AvailableRingWritesVisible = availableRingWritesVisible;
        OrderedBeforeNotify = orderedBeforeNotify;
        BarrierSequence = barrierSequence;
        RegisterWindowExecutionAttempted = registerWindowExecutionAttempted;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        AccessWidth = accessWidth;
        AccessWidthBytes = accessWidthBytes;
        BarRelativeOffset = barRelativeOffset;
        WrittenValue = writtenValue;
        IsInitialized = isInitialized;
    }

    public VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus Status
    {
        get;
    }

    public VirtioBlockQueueNotificationRegisterWindowPlanStatus PlanStatus
    {
        get;
    }

    public VirtioBlockQueueNotifyOffsetPlanStatus NotifyOffsetPlanStatus
    {
        get;
    }

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus
    {
        get;
    }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus
    {
        get;
    }

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus
    {
        get;
    }

    public ushort QueueIndex
    {
        get;
    }

    public ushort QueueNotifyOffset
    {
        get;
    }

    public uint NotifyOffsetMultiplier
    {
        get;
    }

    public uint NotifyRelativeOffset
    {
        get;
    }

    public ushort PublishedDescriptorCount
    {
        get;
    }

    public ushort PreviousAvailableRingIndex
    {
        get;
    }

    public ushort NextAvailableRingIndex
    {
        get;
    }

    public ushort DescriptorHead
    {
        get;
    }

    public ushort WriteValue
    {
        get;
    }

    public uint WriteWidthBytes
    {
        get;
    }

    public bool DescriptorWritesVisible
    {
        get;
    }

    public bool AvailableRingWritesVisible
    {
        get;
    }

    public bool OrderedBeforeNotify
    {
        get;
    }

    public uint BarrierSequence
    {
        get;
    }

    public bool BarrierSucceeded =>
        DescriptorWritesVisible &&
        AvailableRingWritesVisible &&
        OrderedBeforeNotify;

    public bool RegisterWindowExecutionAttempted
    {
        get;
    }

    public VirtioPciCapabilityType CapabilityType
    {
        get;
    }

    public byte BarIndex
    {
        get;
    }

    public VirtioPciRegisterAccessWidth AccessWidth
    {
        get;
    }

    public uint AccessWidthBytes
    {
        get;
    }

    public ulong BarRelativeOffset
    {
        get;
    }

    public ulong WrittenValue
    {
        get;
    }

    public bool IsInitialized
    {
        get;
    }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.Success;

    internal static VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult FromPlan(
        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus status,
        VirtioBlockQueueNotificationRegisterWindowPlanResult plan,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus,
        VirtioBlockQueueNotificationBarrierReadiness barrier,
        bool registerWindowExecutionAttempted)
    {
        return new VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult(
            status,
            plan.Status,
            plan.NotifyOffsetPlanStatus,
            plan.NotificationMetadataStatus,
            plan.RegisterWindowPlanStatus,
            accessExecutionStatus,
            plan.QueueIndex,
            plan.QueueNotifyOffset,
            plan.NotifyOffsetMultiplier,
            plan.NotifyRelativeOffset,
            plan.PublishedDescriptorCount,
            plan.PreviousAvailableRingIndex,
            plan.NextAvailableRingIndex,
            plan.DescriptorHead,
            plan.WriteValue,
            plan.WriteWidthBytes,
            barrier.DescriptorWritesVisible,
            barrier.AvailableRingWritesVisible,
            barrier.OrderedBeforeNotify,
            barrier.BarrierSequence,
            registerWindowExecutionAttempted,
            plan.AccessPlan.CapabilityType,
            plan.AccessPlan.BarIndex,
            plan.AccessPlan.Width,
            plan.AccessPlan.WidthBytes,
            plan.AccessPlan.BarRelativeOffset,
            plan.WriteValue,
            isInitialized: true);
    }
}
