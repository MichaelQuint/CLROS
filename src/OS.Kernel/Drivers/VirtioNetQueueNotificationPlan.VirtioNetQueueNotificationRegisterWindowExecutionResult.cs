namespace OS.Drivers;
public readonly struct VirtioNetQueueNotificationRegisterWindowExecutionResult
{
    private VirtioNetQueueNotificationRegisterWindowExecutionResult(VirtioNetQueueNotificationRegisterWindowExecutionStatus status, VirtioNetQueueNotificationPlanStatus notificationPlanStatus, VirtioPciQueueNotificationPlanStatus pciNotificationStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus, VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus, VirtioNetQueueNotificationKind kind, ushort queueIndex, ushort queueNotifyOffset, uint notifyRelativeOffset, ushort writeValue, uint writeWidthBytes, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, VirtioPciCapabilityType capabilityType, byte barIndex, VirtioPciRegisterAccessWidth accessWidth, uint accessWidthBytes, ulong barRelativeOffset, ulong writtenValue, bool isInitialized)
    {
        Status = status;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
        AccessExecutionStatus = accessExecutionStatus;
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
        IsInitialized = isInitialized;
    }

    public VirtioNetQueueNotificationRegisterWindowExecutionStatus Status { get; }
    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }
    public VirtioPciQueueNotificationPlanStatus PciNotificationStatus { get; }
    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus { get; }
    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus { get; }
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
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetQueueNotificationRegisterWindowExecutionStatus.Success;

    internal static VirtioNetQueueNotificationRegisterWindowExecutionResult FromPlan(VirtioNetQueueNotificationRegisterWindowExecutionStatus status, VirtioNetQueueNotificationRegisterWindowPlanResult plan, VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus)
    {
        return new VirtioNetQueueNotificationRegisterWindowExecutionResult(status, plan.NotificationPlanStatus, plan.PciNotificationStatus, plan.RegisterWindowPlanStatus, accessExecutionStatus, plan.Kind, plan.QueueIndex, plan.QueueNotifyOffset, plan.NotifyRelativeOffset, plan.WriteValue, plan.WriteWidthBytes, plan.PublishedDescriptorCount, plan.PreviousAvailableRingIndex, plan.NextAvailableRingIndex, plan.AccessPlan.CapabilityType, plan.AccessPlan.BarIndex, plan.AccessPlan.Width, plan.AccessPlan.WidthBytes, plan.AccessPlan.BarRelativeOffset, plan.WriteValue, isInitialized: true);
    }
}
