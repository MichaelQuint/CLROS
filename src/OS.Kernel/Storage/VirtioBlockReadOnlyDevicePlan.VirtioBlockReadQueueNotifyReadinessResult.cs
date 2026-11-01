using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadQueueNotifyReadinessResult
{
    private VirtioBlockReadQueueNotifyReadinessResult(
        VirtioBlockReadQueueNotifyReadinessStatus status,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        VirtioBlockQueueNotificationMetadataResult notificationMetadata,
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset,
        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult notifyExecution,
        VirtioBlockQueueNotificationBarrierReadiness barrier,
        uint notifyOffsetMultiplier,
        bool notificationMetadataAttempted,
        bool notifyOffsetPlanAttempted,
        bool notifyExecutionAttempted,
        bool isInitialized)
    {
        Status = status;
        ReadQueuePublishStatus = readQueuePublishStatus;
        QueueIndex = queueIndex;
        QueueNotifyOffset = queueNotifyOffset;
        NotificationMetadata = notificationMetadata;
        NotifyOffset = notifyOffset;
        NotifyExecution = notifyExecution;
        Barrier = barrier;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
        NotificationMetadataAttempted = notificationMetadataAttempted;
        NotifyOffsetPlanAttempted = notifyOffsetPlanAttempted;
        NotifyExecutionAttempted = notifyExecutionAttempted;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueueNotifyReadinessStatus Status { get; }

    public VirtioBlockReadQueuePublishStatus ReadQueuePublishStatus { get; }

    public VirtioBlockQueueNotificationMetadataResult NotificationMetadata { get; }

    public VirtioBlockQueueNotifyOffsetPlanResult NotifyOffset { get; }

    public VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult NotifyExecution { get; }

    public VirtioBlockQueueNotificationBarrierReadiness Barrier { get; }

    public uint NotifyOffsetMultiplier { get; }

    public bool NotificationMetadataAttempted { get; }

    public bool NotifyOffsetPlanAttempted { get; }

    public bool NotifyExecutionAttempted { get; }

    public VirtioBlockQueueNotificationMetadataStatus NotificationMetadataStatus =>
        NotificationMetadataAttempted ?
            NotificationMetadata.Status :
            VirtioBlockQueueNotificationMetadataStatus.Success;

    public VirtioBlockQueueNotifyOffsetPlanStatus NotifyOffsetPlanStatus =>
        NotifyOffsetPlanAttempted ?
            NotifyOffset.Status :
            VirtioBlockQueueNotifyOffsetPlanStatus.Success;

    public VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus NotifyExecutionStatus =>
        NotifyExecutionAttempted ?
            NotifyExecution.Status :
            VirtioBlockQueueNotificationRegisterWindowBarrierExecutionStatus.Success;

    public VirtioBlockQueueNotificationRegisterWindowPlanStatus NotifyRegisterWindowPlanStatus =>
        NotifyExecutionAttempted ?
            NotifyExecution.PlanStatus :
            VirtioBlockQueueNotificationRegisterWindowPlanStatus.Success;

    public VirtioPciBarRegisterWindowPlanStatus RegisterWindowPlanStatus =>
        NotifyExecutionAttempted ?
            NotifyExecution.RegisterWindowPlanStatus :
            VirtioPciBarRegisterWindowPlanStatus.Success;

    public VirtioPciBarRegisterWindowAccessExecutionStatus AccessExecutionStatus =>
        NotifyExecutionAttempted ?
            NotifyExecution.AccessExecutionStatus :
            VirtioPciBarRegisterWindowAccessExecutionStatus.Success;

    public ushort QueueIndex { get; }

    public ushort QueueNotifyOffset { get; }

    public uint NotifyRelativeOffset =>
        NotifyOffsetPlanAttempted ?
            NotifyOffset.NotifyRelativeOffset :
            0;

    public ushort PublishedDescriptorCount => NotificationMetadata.PublishedDescriptorCount;

    public ushort PreviousAvailableRingIndex => NotificationMetadata.PreviousAvailableRingIndex;

    public ushort NextAvailableRingIndex => NotificationMetadata.NextAvailableRingIndex;

    public ushort DescriptorHead => NotificationMetadata.DescriptorHead;

    public ushort WriteValue => NotificationMetadata.WriteValue;

    public uint WriteWidthBytes => NotificationMetadata.WriteWidthBytes;

    public bool DescriptorWritesVisible => Barrier.DescriptorWritesVisible;

    public bool AvailableRingWritesVisible => Barrier.AvailableRingWritesVisible;

    public bool OrderedBeforeNotify => Barrier.OrderedBeforeNotify;

    public uint BarrierSequence => Barrier.BarrierSequence;

    public bool BarrierSucceeded => Barrier.Succeeded;

    public bool RegisterWindowExecutionAttempted =>
        NotifyExecutionAttempted &&
        NotifyExecution.RegisterWindowExecutionAttempted;

    public VirtioPciCapabilityType CapabilityType =>
        NotifyExecutionAttempted ?
            NotifyExecution.CapabilityType :
            default;

    public byte BarIndex =>
        NotifyExecutionAttempted ?
            NotifyExecution.BarIndex :
            (byte)0;

    public VirtioPciRegisterAccessWidth AccessWidth =>
        NotifyExecutionAttempted ?
            NotifyExecution.AccessWidth :
            default;

    public uint AccessWidthBytes =>
        NotifyExecutionAttempted ?
            NotifyExecution.AccessWidthBytes :
            0;

    public ulong BarRelativeOffset =>
        NotifyExecutionAttempted ?
            NotifyExecution.BarRelativeOffset :
            0;

    public ulong WrittenValue =>
        NotifyExecutionAttempted ?
            NotifyExecution.WrittenValue :
            0;

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockReadQueueNotifyReadinessStatus.Success;

    internal static VirtioBlockReadQueueNotifyReadinessResult Create(
        VirtioBlockReadQueueNotifyReadinessStatus status,
        VirtioBlockReadQueuePublishStatus readQueuePublishStatus,
        ushort queueIndex,
        ushort queueNotifyOffset,
        VirtioBlockQueueNotificationMetadataResult notificationMetadata,
        VirtioBlockQueueNotifyOffsetPlanResult notifyOffset,
        VirtioBlockQueueNotificationRegisterWindowBarrierExecutionResult notifyExecution,
        VirtioBlockQueueNotificationBarrierReadiness barrier,
        uint notifyOffsetMultiplier,
        bool notificationMetadataAttempted,
        bool notifyOffsetPlanAttempted,
        bool notifyExecutionAttempted)
    {
        return new VirtioBlockReadQueueNotifyReadinessResult(
            status,
            readQueuePublishStatus,
            queueIndex,
            queueNotifyOffset,
            notificationMetadata,
            notifyOffset,
            notifyExecution,
            barrier,
            notifyOffsetMultiplier,
            notificationMetadataAttempted,
            notifyOffsetPlanAttempted,
            notifyExecutionAttempted,
            isInitialized: true);
    }
}
