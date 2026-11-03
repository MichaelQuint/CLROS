namespace OS.Drivers;

public readonly struct VirtioNetTransmitReadinessResult
{
    private VirtioNetTransmitReadinessResult(bool isInitialized, VirtioNetTransmitReadinessStatus status, VirtioNetQueueMmioSetupPlanStatus queueSetupPlanStatus, VirtioNetQueueMmioSetupExecutionStatus queueSetupExecutionStatus, VirtioNetTransmitDescriptorPublishPlanStatus transmitPublishPlanStatus, VirtioNetTransmitLifecycleStatus transmitPublishStatus, VirtioNetQueueNotificationPlanStatus notificationPlanStatus, VirtioPciQueueNotificationPlanStatus pciNotificationStatus, VirtioNetQueueNotificationExecutionStatus notificationExecutionStatus, VirtioPciNotifyWriteStatus notificationSinkStatus, bool transmitPublishAttempted, bool notificationPlanAttempted, bool notificationExecutionAttempted, bool barrierSucceeded, uint barrierSequence, ushort headerDescriptorIndex, ushort frameDescriptorIndex, ushort descriptorsWritten, ushort descriptorsMarkedInFlight, ushort queueIndex, ushort publishedDescriptorCount, ushort previousAvailableRingIndex, ushort nextAvailableRingIndex, uint notifyRelativeOffset, ushort writeValue, VirtioNetTransmitDescriptorPublishPlanResult transmitPublish, VirtioNetQueueNotificationPlanResult notificationPlan, VirtioNetQueueNotificationExecutionResult notificationExecution)
    {
        IsInitialized = isInitialized;
        Status = status;
        QueueSetupPlanStatus = queueSetupPlanStatus;
        QueueSetupExecutionStatus = queueSetupExecutionStatus;
        TransmitPublishPlanStatus = transmitPublishPlanStatus;
        TransmitPublishStatus = transmitPublishStatus;
        NotificationPlanStatus = notificationPlanStatus;
        PciNotificationStatus = pciNotificationStatus;
        NotificationExecutionStatus = notificationExecutionStatus;
        NotificationSinkStatus = notificationSinkStatus;
        TransmitPublishAttempted = transmitPublishAttempted;
        NotificationPlanAttempted = notificationPlanAttempted;
        NotificationExecutionAttempted = notificationExecutionAttempted;
        BarrierSucceeded = barrierSucceeded;
        BarrierSequence = barrierSequence;
        HeaderDescriptorIndex = headerDescriptorIndex;
        FrameDescriptorIndex = frameDescriptorIndex;
        DescriptorsWritten = descriptorsWritten;
        DescriptorsMarkedInFlight = descriptorsMarkedInFlight;
        QueueIndex = queueIndex;
        PublishedDescriptorCount = publishedDescriptorCount;
        PreviousAvailableRingIndex = previousAvailableRingIndex;
        NextAvailableRingIndex = nextAvailableRingIndex;
        NotifyRelativeOffset = notifyRelativeOffset;
        WriteValue = writeValue;
        TransmitPublish = transmitPublish;
        NotificationPlan = notificationPlan;
        NotificationExecution = notificationExecution;
    }

    public VirtioNetTransmitReadinessStatus Status
    {
        get;
    }
    public VirtioNetQueueMmioSetupPlanStatus QueueSetupPlanStatus
    {
        get;
    }
    public VirtioNetQueueMmioSetupExecutionStatus QueueSetupExecutionStatus
    {
        get;
    }
    public VirtioNetTransmitDescriptorPublishPlanStatus TransmitPublishPlanStatus
    {
        get;
    }
    public VirtioNetTransmitLifecycleStatus TransmitPublishStatus
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
    public VirtioNetQueueNotificationExecutionStatus NotificationExecutionStatus
    {
        get;
    }
    public VirtioPciNotifyWriteStatus NotificationSinkStatus
    {
        get;
    }
    public bool TransmitPublishAttempted
    {
        get;
    }
    public bool NotificationPlanAttempted
    {
        get;
    }
    public bool NotificationExecutionAttempted
    {
        get;
    }
    public bool BarrierSucceeded
    {
        get;
    }
    public uint BarrierSequence
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
    public ushort DescriptorsWritten
    {
        get;
    }
    public ushort DescriptorsMarkedInFlight
    {
        get;
    }
    public ushort QueueIndex
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
    public uint NotifyRelativeOffset
    {
        get;
    }
    public ushort WriteValue
    {
        get;
    }
    public VirtioNetTransmitDescriptorPublishPlanResult TransmitPublish
    {
        get;
    }
    public VirtioNetQueueNotificationPlanResult NotificationPlan
    {
        get;
    }
    public VirtioNetQueueNotificationExecutionResult NotificationExecution
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetTransmitReadinessStatus.Success;

    internal static VirtioNetTransmitReadinessResult Create(VirtioNetTransmitReadinessStatus status, VirtioNetQueueMmioSetupPlanResult queueSetup, VirtioNetQueueMmioSetupExecutionResult queueSetupExecution, VirtioNetTransmitDescriptorPublishPlanResult transmitPublish, VirtioNetQueueNotificationPlanResult notificationPlan, VirtioNetQueueNotificationExecutionResult notificationExecution, VirtioNetQueueNotificationBarrierReadiness barrier, bool transmitPublishAttempted, bool notificationPlanAttempted, bool notificationExecutionAttempted)
    {
        return new VirtioNetTransmitReadinessResult(isInitialized: true, status, queueSetup.Status, queueSetupExecution.Status, transmitPublish.Status, transmitPublish.TransmitPublishStatus, notificationPlan.Status, notificationPlan.NotificationStatus, notificationExecution.Status, notificationExecution.SinkStatus, transmitPublishAttempted, notificationPlanAttempted, notificationExecutionAttempted, barrier.Succeeded, barrier.BarrierSequence, transmitPublish.HeaderDescriptorIndex, transmitPublish.FrameDescriptorIndex, transmitPublish.DescriptorsWritten, transmitPublish.DescriptorsMarkedInFlight, notificationExecution.QueueIndex, notificationExecution.PublishedDescriptorCount, notificationExecution.PreviousAvailableRingIndex, notificationExecution.NextAvailableRingIndex, notificationExecution.NotifyRelativeOffset, notificationExecution.WriteValue, transmitPublish, notificationPlan, notificationExecution);
    }
}
