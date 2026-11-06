namespace OS.Drivers;

public readonly struct VirtioNetQueueNotificationExecutionResult
{
    private VirtioNetQueueNotificationExecutionResult(VirtioNetQueueNotificationExecutionStatus status, VirtioNetQueueNotificationPlanStatus notificationPlanStatus, VirtioPciQueueNotificationPlanStatus pciNotificationStatus, VirtioPciNotifyWriteStatus sinkStatus, VirtioNetQueueNotificationKind kind, ushort queueIndex, ushort queueNotifyOffset, uint notifyRelativeOffset, ushort writeValue, uint writeWidthBytes, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, bool descriptorWritesVisible, bool availableRingWritesVisible, bool orderedBeforeNotify, uint barrierSequence, bool isInitialized)
    {
        Status = status;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        SinkStatus = sinkStatus;
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
        IsInitialized = isInitialized;
    }

    public VirtioNetQueueNotificationExecutionStatus Status
    {
        get;
    }
    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus
    {
        get;
    }
    public VirtioPciQueueNotificationPlanStatus PciNotificationStatus
    {
        get;
    }
    public VirtioPciNotifyWriteStatus SinkStatus
    {
        get;
    }
    public VirtioNetQueueNotificationKind Kind
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
    public uint NotifyRelativeOffset
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
    public bool BarrierSucceeded => DescriptorWritesVisible && AvailableRingWritesVisible && OrderedBeforeNotify;
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetQueueNotificationExecutionStatus.Success;

    internal static VirtioNetQueueNotificationExecutionResult FromPlan(VirtioNetQueueNotificationExecutionStatus status, VirtioNetQueueNotificationPlanResult notificationPlan, VirtioNetQueueNotificationBarrierReadiness barrier, VirtioPciNotifyWriteStatus sinkStatus)
    {
        VirtioNetQueueNotificationPlan plan = notificationPlan.Plan;
        return new VirtioNetQueueNotificationExecutionResult(status, notificationPlan.Status, notificationPlan.NotificationStatus, sinkStatus, notificationPlan.Kind, plan.QueueIndex, plan.QueueNotifyOffset, plan.NotifyRelativeOffset, plan.WriteValue, plan.WriteWidthBytes, plan.PublishedDescriptorCount, plan.PreviousAvailableRingIndex, plan.NextAvailableRingIndex, barrier.DescriptorWritesVisible, barrier.AvailableRingWritesVisible, barrier.OrderedBeforeNotify, barrier.BarrierSequence, isInitialized: true);
    }

    internal static VirtioNetQueueNotificationExecutionResult FailedPlan(VirtioNetQueueNotificationPlanResult notificationPlan, VirtioNetQueueNotificationBarrierReadiness barrier)
    {
        return new VirtioNetQueueNotificationExecutionResult(VirtioNetQueueNotificationExecutionStatus.NotificationPlanFailed, notificationPlan.Status, notificationPlan.NotificationStatus, VirtioPciNotifyWriteStatus.NotReady, notificationPlan.Kind, queueIndex: 0, queueNotifyOffset: 0, notifyRelativeOffset: 0, writeValue: 0, writeWidthBytes: 0, publishedDescriptorCount: 0, previousAvailableRingIndex: 0, nextAvailableRingIndex: 0, barrier.DescriptorWritesVisible, barrier.AvailableRingWritesVisible, barrier.OrderedBeforeNotify, barrier.BarrierSequence, isInitialized: true);
    }
}
