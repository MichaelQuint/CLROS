using OS.Std.Binary;

namespace OS.Drivers;

public readonly struct EnaPciResourcePlanResult
{
    private EnaPciResourcePlanResult(bool isInitialized, EnaPciResourcePlanStatus status, EnaPciCandidateValidationStatus candidateValidationStatus, byte failingBarIndex, EnaPciResourcePlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        CandidateValidationStatus = candidateValidationStatus;
        FailingBarIndex = failingBarIndex;
        Plan = plan;
    }

    public EnaPciResourcePlanStatus Status
    {
        get;
    }
    public EnaPciCandidateValidationStatus CandidateValidationStatus
    {
        get;
    }
    public byte FailingBarIndex
    {
        get;
    }
    public EnaPciResourcePlan Plan
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == EnaPciResourcePlanStatus.Success;

    internal static EnaPciResourcePlanResult Success(EnaPciResourcePlan plan)
    {
        return new EnaPciResourcePlanResult(isInitialized: true, EnaPciResourcePlanStatus.Success, EnaPciCandidateValidationStatus.Success, PciBarDecoder.NoBarIndex, plan);
    }

    internal static EnaPciResourcePlanResult Fail(EnaPciResourcePlanStatus status, EnaPciCandidateValidationStatus candidateValidationStatus, byte failingBarIndex)
    {
        return new EnaPciResourcePlanResult(isInitialized: true, status, candidateValidationStatus, failingBarIndex, default);
    }
}
