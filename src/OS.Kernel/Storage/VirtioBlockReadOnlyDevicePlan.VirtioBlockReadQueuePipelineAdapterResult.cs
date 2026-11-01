using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockReadQueuePipelineAdapterResult
{
    private VirtioBlockReadQueuePipelineAdapterResult(
        VirtioBlockReadQueuePipelineAdapterStatus status,
        VirtioBlockReadQueuePipelineResult pipeline,
        bool isInitialized,
        bool pipelineAttempted,
        ulong requiredByteCount,
        ulong availableByteCount,
        uint requiredElementCount,
        uint availableElementCount,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioPciBarRegisterWindowPlanStatus notifyWindowPlanStatus)
    {
        Status = status;
        Pipeline = pipeline;
        IsInitialized = isInitialized;
        PipelineAttempted = pipelineAttempted;
        RequiredByteCount = requiredByteCount;
        AvailableByteCount = availableByteCount;
        RequiredElementCount = requiredElementCount;
        AvailableElementCount = availableElementCount;
        QueuePlanStatus = queuePlanStatus;
        NotifyWindowPlanStatus = notifyWindowPlanStatus;
    }

    public VirtioBlockReadQueuePipelineAdapterStatus Status { get; }

    public VirtioBlockReadQueuePipelineResult Pipeline { get; }

    public bool IsInitialized { get; }

    public bool PipelineAttempted { get; }

    public ulong RequiredByteCount { get; }

    public ulong AvailableByteCount { get; }

    public uint RequiredElementCount { get; }

    public uint AvailableElementCount { get; }

    public VirtqueueSplitRingPlanStatus QueuePlanStatus { get; }

    public VirtioPciBarRegisterWindowPlanStatus NotifyWindowPlanStatus { get; }

    public VirtioBlockReadQueuePipelineStatus PipelineStatus =>
        PipelineAttempted ?
            Pipeline.Status :
            VirtioBlockReadQueuePipelineStatus.Success;

    public BlockReadResult BlockRead =>
        PipelineAttempted ?
            Pipeline.BlockRead :
            BlockReadResult.Fail(BlockDeviceStatus.IoError);

    public bool Succeeded =>
        IsInitialized && Status == VirtioBlockReadQueuePipelineAdapterStatus.Success;

    internal static VirtioBlockReadQueuePipelineAdapterResult PreflightFailure(
        VirtioBlockReadQueuePipelineAdapterStatus status,
        ulong requiredByteCount,
        ulong availableByteCount,
        uint requiredElementCount,
        uint availableElementCount,
        VirtqueueSplitRingPlanStatus queuePlanStatus,
        VirtioPciBarRegisterWindowPlanStatus notifyWindowPlanStatus)
    {
        return new VirtioBlockReadQueuePipelineAdapterResult(
            status,
            default,
            isInitialized: true,
            pipelineAttempted: false,
            requiredByteCount,
            availableByteCount,
            requiredElementCount,
            availableElementCount,
            queuePlanStatus,
            notifyWindowPlanStatus);
    }

    internal static VirtioBlockReadQueuePipelineAdapterResult FromPipeline(
        VirtioBlockReadQueuePipelineResult pipeline)
    {
        VirtioBlockReadQueuePipelineAdapterStatus status = pipeline.Succeeded ?
            VirtioBlockReadQueuePipelineAdapterStatus.Success :
            VirtioBlockReadQueuePipelineAdapterStatus.PipelineFailed;
        return new VirtioBlockReadQueuePipelineAdapterResult(
            status,
            pipeline,
            isInitialized: true,
            pipelineAttempted: true,
            requiredByteCount: 0,
            availableByteCount: 0,
            requiredElementCount: 0,
            availableElementCount: 0,
            VirtqueueSplitRingPlanStatus.Success,
            VirtioPciBarRegisterWindowPlanStatus.Success);
    }
}
