namespace OS.Drivers;

public readonly struct VirtioPciQueueNotificationPlanResult
{
    private VirtioPciQueueNotificationPlanResult(VirtioPciQueueNotificationPlanStatus status, VirtioPciBarAperture aperture, uint queueIndex, ushort availableQueueCount, ushort queueNotifyOffset, ulong negotiatedFeatures, VirtioPciQueueNotificationPlan plan, bool isInitialized)
    {
        Status = status;
        Aperture = aperture;
        QueueIndex = queueIndex;
        AvailableQueueCount = availableQueueCount;
        QueueNotifyOffset = queueNotifyOffset;
        NegotiatedFeatures = negotiatedFeatures;
        Plan = plan;
        IsInitialized = isInitialized;
    }

    public VirtioPciQueueNotificationPlanStatus Status
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public VirtioPciBarAperture Aperture
    {
        get;
    }
    public uint QueueIndex
    {
        get;
    }
    public ushort AvailableQueueCount
    {
        get;
    }
    public ushort QueueNotifyOffset
    {
        get;
    }
    public ulong NegotiatedFeatures
    {
        get;
    }
    public VirtioPciQueueNotificationPlan Plan
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioPciQueueNotificationPlanStatus.Success;

    internal static VirtioPciQueueNotificationPlanResult Success(VirtioPciQueueNotificationPlan plan, ushort availableQueueCount, ulong negotiatedFeatures)
    {
        return new VirtioPciQueueNotificationPlanResult(VirtioPciQueueNotificationPlanStatus.Success, plan.Aperture, plan.QueueIndex, availableQueueCount, plan.QueueNotifyOffset, negotiatedFeatures, plan, isInitialized: true);
    }

    internal static VirtioPciQueueNotificationPlanResult Fail(VirtioPciQueueNotificationPlanStatus status, VirtioPciBarAperture aperture, uint queueIndex, ushort availableQueueCount, ushort queueNotifyOffset, ulong negotiatedFeatures)
    {
        return new VirtioPciQueueNotificationPlanResult(status, aperture, queueIndex, availableQueueCount, queueNotifyOffset, negotiatedFeatures, default, isInitialized: true);
    }
}
