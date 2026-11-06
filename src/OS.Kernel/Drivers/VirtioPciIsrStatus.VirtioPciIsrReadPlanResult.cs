namespace OS.Drivers;

public readonly struct VirtioPciIsrReadPlanResult
{
    private VirtioPciIsrReadPlanResult(VirtioPciIsrReadPlanStatus status, VirtioPciBarAperture aperture, VirtioPciIsrReadPlan plan, bool isInitialized)
    {
        Status = status;
        Aperture = aperture;
        Plan = plan;
        IsInitialized = isInitialized;
    }

    public VirtioPciIsrReadPlanStatus Status
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
    public VirtioPciIsrReadPlan Plan
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioPciIsrReadPlanStatus.Success;

    public static VirtioPciIsrReadPlanResult Success(VirtioPciIsrReadPlan plan)
    {
        return new VirtioPciIsrReadPlanResult(VirtioPciIsrReadPlanStatus.Success, plan.Aperture, plan, isInitialized: true);
    }

    public static VirtioPciIsrReadPlanResult Fail(VirtioPciIsrReadPlanStatus status, VirtioPciBarAperture aperture)
    {
        return new VirtioPciIsrReadPlanResult(status, aperture, default, isInitialized: true);
    }
}
