using OS.Std.Binary;

namespace OS.Storage;

public readonly struct VirtioBlockQueueReadinessResult
{
    private VirtioBlockQueueReadinessResult(
        VirtioBlockQueueReadinessStatus status,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        VirtioBlockRequestPlan requestPlan,
        ushort queueSize)
    {
        IsInitialized = true;
        Status = status;
        RequestPlanStatus = requestPlanStatus;
        RequestPlan = requestPlan;
        QueueSize = queueSize;
    }

    public bool IsInitialized { get; }

    public VirtioBlockQueueReadinessStatus Status { get; }

    public VirtioBlockRequestPlanStatus RequestPlanStatus { get; }

    public VirtioBlockRequestPlan RequestPlan { get; }

    public ushort QueueSize { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockQueueReadinessStatus.Ready;

    public static VirtioBlockQueueReadinessResult Ready(
        VirtioBlockRequestPlan requestPlan,
        ushort queueSize)
    {
        return new VirtioBlockQueueReadinessResult(
            VirtioBlockQueueReadinessStatus.Ready,
            VirtioBlockRequestPlanStatus.Success,
            requestPlan,
            queueSize);
    }

    public static VirtioBlockQueueReadinessResult Fail(
        VirtioBlockQueueReadinessStatus status,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        ushort queueSize)
    {
        return new VirtioBlockQueueReadinessResult(
            status,
            requestPlanStatus,
            default,
            queueSize);
    }
}
