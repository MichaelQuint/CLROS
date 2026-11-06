namespace OS.Drivers;

public readonly struct VirtioNetQueueNotificationPlanResult
{
    private VirtioNetQueueNotificationPlanResult(VirtioNetQueueNotificationPlanStatus status, VirtioNetQueueNotificationKind kind, VirtioNetQueueMmioSetupPlanStatus queueSetupStatus, VirtioNetReceiveLifecyclePublishStatus receivePublishStatus, VirtioNetTransmitLifecycleStatus transmitPublishStatus, VirtioPciQueueNotificationPlanStatus notificationStatus, VirtioNetQueueNotificationPlan plan, bool isInitialized)
    {
        Status = status;
        Kind = kind;
        QueueSetupStatus = queueSetupStatus;
        ReceivePublishStatus = receivePublishStatus;
        TransmitPublishStatus = transmitPublishStatus;
        NotificationStatus = notificationStatus;
        Plan = plan;
        IsInitialized = isInitialized;
    }

    public VirtioNetQueueNotificationPlanStatus Status
    {
        get;
    }
    public VirtioNetQueueNotificationKind Kind
    {
        get;
    }
    public VirtioNetQueueMmioSetupPlanStatus QueueSetupStatus
    {
        get;
    }
    public VirtioNetReceiveLifecyclePublishStatus ReceivePublishStatus
    {
        get;
    }
    public VirtioNetTransmitLifecycleStatus TransmitPublishStatus
    {
        get;
    }
    public VirtioPciQueueNotificationPlanStatus NotificationStatus
    {
        get;
    }
    public VirtioNetQueueNotificationPlan Plan
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioNetQueueNotificationPlanStatus.Success;

    internal static VirtioNetQueueNotificationPlanResult Success(VirtioNetQueueNotificationKind kind, VirtioNetQueueMmioSetupPlanStatus queueSetupStatus, VirtioNetReceiveLifecyclePublishStatus receivePublishStatus, VirtioNetTransmitLifecycleStatus transmitPublishStatus, VirtioPciQueueNotificationPlanStatus notificationStatus, VirtioNetQueueNotificationPlan plan)
    {
        return new VirtioNetQueueNotificationPlanResult(VirtioNetQueueNotificationPlanStatus.Success, kind, queueSetupStatus, receivePublishStatus, transmitPublishStatus, notificationStatus, plan, isInitialized: true);
    }

    internal static VirtioNetQueueNotificationPlanResult Fail(VirtioNetQueueNotificationPlanStatus status, VirtioNetQueueNotificationKind kind, VirtioNetQueueMmioSetupPlanStatus queueSetupStatus, VirtioNetReceiveLifecyclePublishStatus receivePublishStatus, VirtioNetTransmitLifecycleStatus transmitPublishStatus, VirtioPciQueueNotificationPlanStatus notificationStatus)
    {
        return new VirtioNetQueueNotificationPlanResult(status, kind, queueSetupStatus, receivePublishStatus, transmitPublishStatus, notificationStatus, default, isInitialized: true);
    }
}
