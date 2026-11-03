namespace OS.Drivers;

public readonly struct VirtioPciBindingCatalogResult
{
    private VirtioPciBindingCatalogResult(VirtioPciBindingCatalogStatus status, int inspectedCandidateCount, int bindingCount, int requiredBindingCapacity, int failingCandidateIndex, int failingSourceIndex, VirtioPciBindingPlanResult failingPlan, bool isInitialized)
    {
        Status = status;
        InspectedCandidateCount = inspectedCandidateCount;
        BindingCount = bindingCount;
        RequiredBindingCapacity = requiredBindingCapacity;
        FailingCandidateIndex = failingCandidateIndex;
        FailingSourceIndex = failingSourceIndex;
        FailingPlan = failingPlan;
        IsInitialized = isInitialized;
    }

    public const int NoFailingCandidateIndex = -1;
    public const int NoFailingSourceIndex = -1;
    public VirtioPciBindingCatalogStatus Status
    {
        get;
    }
    public int InspectedCandidateCount
    {
        get;
    }
    public int BindingCount
    {
        get;
    }
    public int RequiredBindingCapacity
    {
        get;
    }
    public int FailingCandidateIndex
    {
        get;
    }
    public int FailingSourceIndex
    {
        get;
    }
    public VirtioPciBindingPlanResult FailingPlan
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioPciBindingCatalogStatus.Success;

    public static VirtioPciBindingCatalogResult Success(int inspectedCandidateCount, int bindingCount)
    {
        return new VirtioPciBindingCatalogResult(VirtioPciBindingCatalogStatus.Success, inspectedCandidateCount, bindingCount, bindingCount, NoFailingCandidateIndex, NoFailingSourceIndex, default, isInitialized: true);
    }

    public static VirtioPciBindingCatalogResult Fail(VirtioPciBindingCatalogStatus status, int inspectedCandidateCount, int bindingCount, int requiredBindingCapacity, int failingCandidateIndex, int failingSourceIndex, VirtioPciBindingPlanResult failingPlan = default)
    {
        return new VirtioPciBindingCatalogResult(status, inspectedCandidateCount, bindingCount, requiredBindingCapacity, failingCandidateIndex, failingSourceIndex, failingPlan, isInitialized: true);
    }
}
