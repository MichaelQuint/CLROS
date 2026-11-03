namespace OS.Storage;

public readonly struct VirtioBlockReadTransactionPlanResult
{
    private VirtioBlockReadTransactionPlanResult(
        VirtioBlockReadTransactionPlanStatus status,
        VirtioBlockRequestPlanStatus requestPlanStatus,
        VirtioBlockReadTransactionPlan plan)
    {
        IsInitialized = true;
        Status = status;
        RequestPlanStatus = requestPlanStatus;
        Plan = plan;
    }

    public bool IsInitialized
    {
        get;
    }

    public VirtioBlockReadTransactionPlanStatus Status
    {
        get;
    }

    public VirtioBlockRequestPlanStatus RequestPlanStatus
    {
        get;
    }

    public VirtioBlockReadTransactionPlan Plan
    {
        get;
    }

    public bool Succeeded => IsInitialized &&
        Status == VirtioBlockReadTransactionPlanStatus.Success;

    public static VirtioBlockReadTransactionPlanResult Success(
        VirtioBlockReadTransactionPlan plan)
    {
        return new VirtioBlockReadTransactionPlanResult(
            VirtioBlockReadTransactionPlanStatus.Success,
            VirtioBlockRequestPlanStatus.Success,
            plan);
    }

    public static VirtioBlockReadTransactionPlanResult Fail(
        VirtioBlockReadTransactionPlanStatus status)
    {
        return new VirtioBlockReadTransactionPlanResult(
            status,
            VirtioBlockRequestPlanStatus.Success,
            default);
    }

    public static VirtioBlockReadTransactionPlanResult Fail(
        VirtioBlockReadTransactionPlanStatus status,
        VirtioBlockRequestPlanStatus requestPlanStatus)
    {
        return new VirtioBlockReadTransactionPlanResult(
            status,
            requestPlanStatus,
            default);
    }
}
