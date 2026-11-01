using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockQueueNotificationRegisterWindowExecutionResult
{
    private VirtioBlockQueueNotificationRegisterWindowExecutionResult(
        VirtioBlockQueueNotificationRegisterWindowExecutionStatus status,
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
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        AccessWidth = accessWidth;
        AccessWidthBytes = accessWidthBytes;
        BarRelativeOffset = barRelativeOffset;
        WrittenValue = writtenValue;
        IsInitialized = isInitialized;
    }

    public VirtioBlockQueueNotificationRegisterWindowExecutionStatus Status { get; }

    public VirtioBlockQueueNotificationRegisterWindowPlanStatus PlanStatus { get; }

    public VirtioBlockQueueNotifyOffsetPlanStatus NotifyOffsetPlanStatus { get; }

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus { get; }

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus { get; }

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyOffsetMultiplier { get; }

    public uint NotifyRelativeOffset { get; }

    public ushort PublishedDescriptorCount { get; }

    public ushort PreviousAvailableRingIndex { get; }

    public ushort NextAvailableRingIndex { get; }

    public ushort DescriptorHead { get; }

    public ushort WriteValue { get; }

    public uint WriteWidthBytes { get; }

    public VirtioPciCapabilityType CapabilityType { get; }

    public byte BarIndex { get; }

    public VirtioPciRegisterAccessWidth AccessWidth { get; }

    public uint AccessWidthBytes { get; }

    public ulong BarRelativeOffset { get; }

    public ulong WrittenValue { get; }

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockQueueNotificationRegisterWindowExecutionStatus.Success;

    internal static VirtioBlockQueueNotificationRegisterWindowExecutionResult FromPlan(
        VirtioBlockQueueNotificationRegisterWindowExecutionStatus status,
        VirtioBlockQueueNotificationRegisterWindowPlanResult plan,
        VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus)
    {
        return new VirtioBlockQueueNotificationRegisterWindowExecutionResult(
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
            plan.AccessPlan.CapabilityType,
            plan.AccessPlan.BarIndex,
            plan.AccessPlan.Width,
            plan.AccessPlan.WidthBytes,
            plan.AccessPlan.BarRelativeOffset,
            plan.WriteValue,
            isInitialized: true);
    }
}
