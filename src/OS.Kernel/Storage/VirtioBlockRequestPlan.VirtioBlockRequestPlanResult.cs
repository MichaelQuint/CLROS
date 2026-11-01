using OS.Std.Binary;

namespace OS.Storage;

public readonly struct VirtioBlockRequestPlanResult
{
    private VirtioBlockRequestPlanResult(
        VirtioBlockRequestPlanStatus status,
        VirtioBlockRequestPlan plan)
    {
        IsInitialized = true;
        Status = status;
        Plan = plan;
    }

    public bool IsInitialized { get; }

    public VirtioBlockRequestPlanStatus Status { get; }

    public VirtioBlockRequestPlan Plan { get; }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockRequestPlanStatus.Success;

    public static VirtioBlockRequestPlanResult Success(VirtioBlockRequestPlan plan)
    {
        return new VirtioBlockRequestPlanResult(
            VirtioBlockRequestPlanStatus.Success,
            plan);
    }

    public static VirtioBlockRequestPlanResult Fail(VirtioBlockRequestPlanStatus status)
    {
        return new VirtioBlockRequestPlanResult(status, default);
    }
}
