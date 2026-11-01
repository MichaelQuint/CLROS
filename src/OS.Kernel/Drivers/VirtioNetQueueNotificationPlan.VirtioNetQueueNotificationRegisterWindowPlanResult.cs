namespace OS.Drivers;
public readonly struct VirtioNetQueueNotificationRegisterWindowPlanResult
{
    private VirtioNetQueueNotificationRegisterWindowPlanResult(VirtioNetQueueNotificationRegisterWindowPlanStatus status, VirtioNetQueueNotificationPlanStatus notificationPlanStatus, VirtioPciQueueNotificationPlanStatus pciNotificationStatus, VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus, VirtioNetQueueNotificationKind kind, ushort queueIndex, ushort queueNotifyOffset, uint notifyRelativeOffset, ushort writeValue, uint writeWidthBytes, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, VirtioPciBarRegisterWindowAccessPlan accessPlan, bool isInitialized)
    {
        Status = status;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        RegisterWindowPlanStatus = registerWindowPlanStatus;
        Kind = kind;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotifyRelativeOffset = notifyRelativeOffset;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        AccessPlan = accessPlan;
        IsInitialized = isInitialized;
    }

    public VirtioNetQueueNotificationRegisterWindowPlanStatus Status { get; }
    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }
    public VirtioPciQueueNotificationPlanStatus PciNotificationStatus { get; }
    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus { get; }
    public VirtioNetQueueNotificationKind Kind { get; }
    public ushort QueueIndex { get; }
    public ushort QueueNotifyOffset { get; }
    public uint NotifyRelativeOffset { get; }
    public ushort WriteValue { get; }
    public uint WriteWidthBytes { get; }
    public ushort PublishedDescriptorCount { get; }
    public ushort PreviousAvailableRingIndex { get; }
    public ushort NextAvailableRingIndex { get; }
    public VirtioPciBarRegisterWindowAccessPlan AccessPlan { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetQueueNotificationRegisterWindowPlanStatus.Success;

    internal static VirtioNetQueueNotificationRegisterWindowPlanResult FailedNotification(VirtioNetQueueNotificationPlanResult notificationPlan)
    {
        return new VirtioNetQueueNotificationRegisterWindowPlanResult(VirtioNetQueueNotificationRegisterWindowPlanStatus.NotificationPlanFailed, notificationPlan.Status, notificationPlan.NotificationStatus, VirtioPciBarRegisterWindowPlanStatus.Success, notificationPlan.Kind, queueIndex: 0, queueNotifyOffset: 0, notifyRelativeOffset: 0, writeValue: 0, writeWidthBytes: 0, publishedDescriptorCount: 0, previousAvailableRingIndex: 0, nextAvailableRingIndex: 0, default, isInitialized: true);
    }

    internal static VirtioNetQueueNotificationRegisterWindowPlanResult FromPlan(VirtioNetQueueNotificationRegisterWindowPlanStatus status, VirtioNetQueueNotificationPlanResult notificationPlan, VirtioPciBarRegisterWindowPlanStatus registerWindowPlanStatus, VirtioPciBarRegisterWindowAccessPlan accessPlan)
    {
        VirtioNetQueueNotificationPlan plan = notificationPlan.Plan;
        return new VirtioNetQueueNotificationRegisterWindowPlanResult(status, notificationPlan.Status, notificationPlan.NotificationStatus, registerWindowPlanStatus, notificationPlan.Kind, plan.QueueIndex, plan.QueueNotifyOffset, plan.NotifyRelativeOffset, plan.WriteValue, plan.WriteWidthBytes, plan.PublishedDescriptorCount, plan.PreviousAvailableRingIndex, plan.NextAvailableRingIndex, accessPlan, isInitialized: true);
    }
}
