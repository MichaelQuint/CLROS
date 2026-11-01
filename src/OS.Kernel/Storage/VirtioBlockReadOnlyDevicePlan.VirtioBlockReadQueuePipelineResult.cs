using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadQueuePipelineResult
{
    private VirtioBlockReadQueuePipelineResult(
        VirtioBlockReadQueuePipelineStatus status,
        VirtioBlockReadQueuePublishResult publish,
        VirtioBlockReadQueueNotifyReadinessResult notifyReadiness,
        VirtioBlockReadQueueNotifyCompletionResult notifyCompletion,
        VirtioBlockReadQueueNotifyBlockReadProjectionResult blockProjection,
        bool notifyReadinessAttempted,
        bool notifyCompletionAttempted,
        bool blockProjectionAttempted,
        bool isInitialized)
    {
        Status = status;
        Publish = publish;
        NotifyReadiness = notifyReadiness;
        NotifyCompletion = notifyCompletion;
        BlockProjection = blockProjection;
        NotifyReadinessAttempted = notifyReadinessAttempted;
        NotifyCompletionAttempted = notifyCompletionAttempted;
        BlockProjectionAttempted = blockProjectionAttempted;
        IsInitialized = isInitialized;
    }

    public VirtioBlockReadQueuePipelineStatus Status { get; }

    public VirtioBlockReadQueuePublishResult Publish { get; }

    public VirtioBlockReadQueueNotifyReadinessResult NotifyReadiness { get; }

    public VirtioBlockReadQueueNotifyCompletionResult NotifyCompletion { get; }

    public VirtioBlockReadQueueNotifyBlockReadProjectionResult BlockProjection { get; }

    public bool NotifyReadinessAttempted { get; }

    public bool NotifyCompletionAttempted { get; }

    public bool BlockProjectionAttempted { get; }

    public VirtioBlockReadQueuePublishStatus PublishStatus => Publish.Status;

    public VirtioBlockReadQueueNotifyReadinessStatus NotifyReadinessStatus =>
        NotifyReadinessAttempted ?
            NotifyReadiness.Status :
            VirtioBlockReadQueueNotifyReadinessStatus.Success;

    public VirtioBlockReadQueueNotifyCompletionStatus NotifyCompletionStatus =>
        NotifyCompletionAttempted ?
            NotifyCompletion.Status :
            VirtioBlockReadQueueNotifyCompletionStatus.Success;

    public VirtioBlockReadQueueNotifyBlockReadProjectionStatus BlockProjectionStatus =>
        BlockProjectionAttempted ?
            BlockProjection.Status :
            VirtioBlockReadQueueNotifyBlockReadProjectionStatus.Success;

    public BlockReadResult BlockRead =>
        BlockProjectionAttempted ?
            BlockProjection.BlockRead :
            BlockReadResult.Fail(BlockDeviceStatus.IoError);

    public bool IsInitialized { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == VirtioBlockReadQueuePipelineStatus.Success;

    internal static VirtioBlockReadQueuePipelineResult Create(
        VirtioBlockReadQueuePipelineStatus status,
        VirtioBlockReadQueuePublishResult publish,
        VirtioBlockReadQueueNotifyReadinessResult notifyReadiness,
        VirtioBlockReadQueueNotifyCompletionResult notifyCompletion,
        VirtioBlockReadQueueNotifyBlockReadProjectionResult blockProjection,
        bool notifyReadinessAttempted,
        bool notifyCompletionAttempted,
        bool blockProjectionAttempted)
    {
        return new VirtioBlockReadQueuePipelineResult(
            status,
            publish,
            notifyReadiness,
            notifyCompletion,
            blockProjection,
            notifyReadinessAttempted,
            notifyCompletionAttempted,
            blockProjectionAttempted,
            isInitialized: true);
    }
}
