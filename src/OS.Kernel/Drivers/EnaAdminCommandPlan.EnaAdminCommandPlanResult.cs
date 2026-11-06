namespace OS.Drivers;

public readonly struct EnaAdminCommandPlanResult
{
    private EnaAdminCommandPlanResult(bool isInitialized, EnaAdminCommandPlanStatus status, ushort commandId, bool submissionPhase, bool expectedCompletionPhase, EnaAdminFeatureId featureId, uint timeoutTicks, EnaAdminCommandPlan plan)
    {
        IsInitialized = isInitialized;
        Status = status;
        CommandId = commandId;
        SubmissionPhase = submissionPhase;
        ExpectedCompletionPhase = expectedCompletionPhase;
        FeatureId = featureId;
        TimeoutTicks = timeoutTicks;
        Plan = plan;
    }

    public EnaAdminCommandPlanStatus Status
    {
        get;
    }
    public ushort CommandId
    {
        get;
    }
    public bool SubmissionPhase
    {
        get;
    }
    public bool ExpectedCompletionPhase
    {
        get;
    }
    public EnaAdminFeatureId FeatureId
    {
        get;
    }
    public uint TimeoutTicks
    {
        get;
    }
    public EnaAdminCommandPlan Plan
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == EnaAdminCommandPlanStatus.Success;

    internal static EnaAdminCommandPlanResult Success(EnaAdminCommandPlan plan)
    {
        return new EnaAdminCommandPlanResult(isInitialized: true, EnaAdminCommandPlanStatus.Success, plan.CommandId, plan.SubmissionPhase, plan.ExpectedCompletionPhase, plan.FeatureId, plan.TimeoutTicks, plan);
    }

    internal static EnaAdminCommandPlanResult Fail(EnaAdminCommandPlanStatus status, ushort commandId, bool submissionPhase, bool expectedCompletionPhase, EnaAdminFeatureId featureId, uint timeoutTicks)
    {
        return new EnaAdminCommandPlanResult(isInitialized: true, status, commandId, submissionPhase, expectedCompletionPhase, featureId, timeoutTicks, default);
    }
}
