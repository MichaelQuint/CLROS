namespace OS.Drivers;
public readonly struct VirtioNetReceiveReadinessResult
{
    private VirtioNetReceiveReadinessResult(VirtioNetReceiveReadinessStatus status, VirtioNetQueueMmioSetupPlanStatus queueSetupPlanStatus, VirtioNetQueueMmioSetupRegisterWindowExecutionStatus queueSetupWindowExecutionStatus, VirtioPciBarRegisterWindowPlanStatus queueSetupRegisterWindowStatus, VirtioPciBarRegisterWindowAccessExecutionStatus queueSetupAccessExecutionStatus, VirtioNetReceiveLifecyclePublishStatus receivePublishStatus, VirtioNetQueueNotificationPlanStatus notificationPlanStatus, VirtioPciQueueNotificationPlanStatus pciNotificationStatus, VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus notificationWindowExecutionStatus, VirtioPciBarRegisterWindowPlanStatus notificationRegisterWindowStatus, VirtioPciBarRegisterWindowAccessExecutionStatus notificationAccessExecutionStatus, bool receivePublishAttempted, bool notificationPlanAttempted, bool notificationWindowExecutionAttempted, bool barrierSucceeded, uint barrierSequence, ushort firstDescriptorIndex, ushort publishedDescriptorCount, ushort previousUsedIndex, ushort expectedNextUsedIndex, ushort firstAvailableRingSlot, ushort lastAvailableRingSlot, uint frameBufferBytes, uint receiveBufferStrideBytes, ushort queueIndex, ushort queueNotifyOffset, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, uint notifyRelativeOffset, ushort writeValue, uint writeWidthBytes, VirtioPciCapabilityType capabilityType, byte barIndex, VirtioPciRegisterAccessWidth accessWidth, uint accessWidthBytes, ulong barRelativeOffset, ulong writtenValue, VirtioNetReceiveLifecyclePublishResult receivePublish, VirtioNetQueueNotificationPlanResult notificationPlan, VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notificationExecution)
    {
        IsInitialized = true;
        Status = status;
        QueueSetupPlanStatus = queueSetupPlanStatus;
        QueueSetupWindowExecutionStatus = queueSetupWindowExecutionStatus;
        QueueSetupRegisterWindowStatus = queueSetupRegisterWindowStatus;
        QueueSetupAccessExecutionStatus = queueSetupAccessExecutionStatus;
        ReceivePublishStatus = receivePublishStatus;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        NotificationWindowExecutionStatus = notificationWindowExecutionStatus;
        NotificationRegisterWindowStatus = notificationRegisterWindowStatus;
        NotificationAccessExecutionStatus = notificationAccessExecutionStatus;
        ReceivePublishAttempted = receivePublishAttempted;
        NotificationPlanAttempted = notificationPlanAttempted;
        NotificationWindowExecutionAttempted = notificationWindowExecutionAttempted;
        BarrierSucceeded = barrierSucceeded;
        BarrierSequence = barrierSequence;
        FirstDescriptorIndex = firstDescriptorIndex;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousUsedIndex = previousUsedIndex;
        ExpectedNextUsedIndex = expectedNextUsedIndex;
        FirstAvailableRingSlot = firstAvailableRingSlot;
        LastAvailableRingSlot = lastAvailableRingSlot;
        FrameBufferBytes = frameBufferBytes;
        ReceiveBufferStrideBytes = receiveBufferStrideBytes;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        NotifyRelativeOffset = notifyRelativeOffset;
        WriteValue = writeValue;
        WriteWidthBytes = writeWidthBytes;
        CapabilityType = capabilityType;
        BarIndex = barIndex;
        AccessWidth = accessWidth;
        AccessWidthBytes = accessWidthBytes;
        BarRelativeOffset = barRelativeOffset;
        WrittenValue = writtenValue;
        ReceivePublish = receivePublish;
        NotificationPlan = notificationPlan;
        NotificationExecution = notificationExecution;
    }

    public VirtioNetReceiveReadinessStatus Status { get; }
    public bool IsInitialized { get; }
    public VirtioNetQueueMmioSetupPlanStatus QueueSetupPlanStatus { get; }
    public VirtioNetQueueMmioSetupRegisterWindowExecutionStatus QueueSetupWindowExecutionStatus { get; }
    public VirtioPciBarRegisterWindowPlanStatus QueueSetupRegisterWindowStatus { get; }
    public VirtioPciBarRegisterWindowAccessExecutionStatus QueueSetupAccessExecutionStatus { get; }
    public VirtioNetReceiveLifecyclePublishStatus ReceivePublishStatus { get; }
    public VirtioNetQueueNotificationPlanStatus NotificationPlanStatus { get; }
    public VirtioPciQueueNotificationPlanStatus PciNotificationStatus { get; }
    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionStatus NotificationWindowExecutionStatus { get; }
    public VirtioPciBarRegisterWindowPlanStatus NotificationRegisterWindowStatus { get; }
    public VirtioPciBarRegisterWindowAccessExecutionStatus NotificationAccessExecutionStatus { get; }
    public bool ReceivePublishAttempted { get; }
    public bool NotificationPlanAttempted { get; }
    public bool NotificationWindowExecutionAttempted { get; }
    public bool BarrierSucceeded { get; }
    public uint BarrierSequence { get; }
    public ushort FirstDescriptorIndex { get; }
    public ushort PublishedDescriptorCount { get; }
    public ushort PreviousUsedIndex { get; }
    public ushort ExpectedNextUsedIndex { get; }
    public ushort FirstAvailableRingSlot { get; }
    public ushort LastAvailableRingSlot { get; }
    public uint FrameBufferBytes { get; }
    public uint ReceiveBufferStrideBytes { get; }
    public ushort QueueIndex { get; }
    public ushort QueueNotifyOffset { get; }
    public ushort PreviousAvailableRingIndex { get; }
    public ushort NextAvailableRingIndex { get; }
    public uint NotifyRelativeOffset { get; }
    public ushort WriteValue { get; }
    public uint WriteWidthBytes { get; }
    public VirtioPciCapabilityType CapabilityType { get; }
    public byte BarIndex { get; }
    public VirtioPciRegisterAccessWidth AccessWidth { get; }
    public uint AccessWidthBytes { get; }
    public ulong BarRelativeOffset { get; }
    public ulong WrittenValue { get; }
    public VirtioNetReceiveLifecyclePublishResult ReceivePublish { get; }
    public VirtioNetQueueNotificationPlanResult NotificationPlan { get; }
    public VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult NotificationExecution { get; }
    public bool Succeeded => IsInitialized && Status == VirtioNetReceiveReadinessStatus.Success;

    internal static VirtioNetReceiveReadinessResult Create(VirtioNetReceiveReadinessStatus status, VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetQueueMmioSetupRegisterWindowExecutionResult queueSetupExecution, VirtioNetReceiveLifecyclePublishResult receivePublish, VirtioNetQueueNotificationPlanResult notificationPlan, VirtioNetQueueNotificationRegisterWindowBarrierExecutionResult notificationExecution, VirtioNetQueueNotificationBarrierReadiness barrier, bool receivePublishAttempted, bool notificationPlanAttempted, bool notificationWindowExecutionAttempted)
    {
        return new VirtioNetReceiveReadinessResult(status, queueSetup.Status, queueSetupExecution.Status, queueSetupExecution.RegisterWindowStatus, queueSetupExecution.AccessExecutionStatus, receivePublish.Status, notificationPlan.Status, notificationPlan.NotificationStatus, notificationExecution.Status, notificationExecution.RegisterWindowPlanStatus, notificationExecution.AccessExecutionStatus, receivePublishAttempted, notificationPlanAttempted, notificationWindowExecutionAttempted, barrier.Succeeded, barrier.BarrierSequence, receivePublish.FirstDescriptorIndex, receivePublish.PublishedDescriptorCount, receivePublish.PreviousUsedIndex, receivePublish.ExpectedNextUsedIndex, receivePublish.FirstAvailableRingSlot, receivePublish.LastAvailableRingSlot, receivePublish.FrameBufferBytes, receivePublish.ReceiveBufferStrideBytes, notificationExecution.QueueIndex, notificationExecution.QueueNotifyOffset, notificationExecution.PreviousAvailableRingIndex, notificationExecution.NextAvailableRingIndex, notificationExecution.NotifyRelativeOffset, notificationExecution.WriteValue, notificationExecution.WriteWidthBytes, notificationExecution.CapabilityType, notificationExecution.BarIndex, notificationExecution.AccessWidth, notificationExecution.AccessWidthBytes, notificationExecution.BarRelativeOffset, notificationExecution.WrittenValue, receivePublish, notificationPlan, notificationExecution);
    }
}
