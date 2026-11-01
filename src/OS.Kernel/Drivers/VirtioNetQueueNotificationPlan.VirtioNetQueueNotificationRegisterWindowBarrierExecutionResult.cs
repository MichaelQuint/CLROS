namespace OS.Drivers;
public readonly struct VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult
{
    private VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult(VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus status, VirtioNetQueueNotificationPlanStatus notificationPlanStatus, VirtioPciQueueNotificationPlanStatus pciNotificationStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus, VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus, VirtioNetQueueNotificationKind kind, ushort queueIndex, ushort queueNotifyOffset, uint notifyRelativeOffset, ushort writeValue, uint writeWidthBytes, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, bool descriptorWritesVisible, bool availableRingWritesVisible, bool orderedBeforeNotify, uint barrierSequence, bool registerWindowExecutionAttempted, VirtioPciCapabilityType capabilityType, byte barIndex, VirtioPciRegisterAccessWidth accessWidth, uint accessWidthBytes, ulong barRelativeOffset, ulong writtenValue, bool isInitialized)
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

    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus Status { get; }
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
    public bool DescriptorWritesVisible { get; }
    public bool AvailableRingWritesVisible { get; }
    public bool OrderedBeforeNotify { get; }
    public uint BarrierSequence { get; }
    public bool BarrierSucceeded => DescriptorWritesVisible && AvailableRingWritesVisible && OrderedBeforeNotify;
    public bool RegisterWindowExecutionAttempted { get; }
    public VirtioPciCapabilityType CapabilityType { get; }
    public byte BarIndex { get; }
    public VirtioPciRegisterAccessWidth AccessWidth { get; }
    public uint AccessWidthBytes { get; }
    public ulong BarRelativeOffset { get; }
    public ulong WrittenValue { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus.Success;

    internal static VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult FromPlan(VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus status, VirtioNetQueueNotificationRegisterWindowPlanResult plan, VirtioPciBarRegisterWindowAccessExecutionStatus accessExecutionStatus, VirtioNetQueueNotificationBarrierReadiness barrier, bool registerWindowExecutionAttempted)
    {
        return new VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult(status, plan.NotificationPlanStatus, plan.PciNotificationStatus, plan.RegisterWindowPlanStatus, accessExecutionStatus, plan.Kind, plan.QueueIndex, plan.QueueNotifyOffset, plan.NotifyRelativeOffset, plan.WriteValue, plan.WriteWidthBytes, plan.PublishedDescriptorCount, plan.PreviousAvailableRingIndex, plan.NextAvailableRingIndex, barrier.DescriptorWritesVisible, barrier.AvailableRingWritesVisible, barrier.OrderedBeforeNotify, barrier.BarrierSequence, registerWindowExecutionAttempted, plan.AccessPlan.CapabilityType, plan.AccessPlan.BarIndex, plan.AccessPlan.Width, plan.AccessPlan.WidthBytes, plan.AccessPlan.BarRelativeOffset, plan.WriteValue, isInitialized: true);
    }

    internal static VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult FromExecution(VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus status, VirtioNetQueueNotificationRegisterWindowExecutionResult execution, VirtioNetQueueNotificationBarrierReadiness barrier)
    {
        return new VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult(status, execution.NotificationPlanStatus, execution.PciNotificationStatus, execution.RegisterWindowPlanStatus, execution.AccessExecutionStatus, execution.Kind, execution.QueueIndex, execution.QueueNotifyOffset, execution.NotifyRelativeOffset, execution.WriteValue, execution.WriteWidthBytes, execution.PublishedDescriptorCount, execution.PreviousAvailableRingIndex, execution.NextAvailableRingIndex, barrier.DescriptorWritesVisible, barrier.AvailableRingWritesVisible, barrier.OrderedBeforeNotify, barrier.BarrierSequence, registerWindowExecutionAttempted: true, execution.CapabilityType, execution.BarIndex, execution.AccessWidth, execution.AccessWidthBytes, execution.BarRelativeOffset, execution.WrittenValue, isInitialized: true);
    }
}
