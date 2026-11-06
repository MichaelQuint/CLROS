namespace OS.Drivers;

public readonly struct VirtioPciBindingDerivedBarSizeCatalogResult
{
    private VirtioPciBindingDerivedBarSizeCatalogResult(VirtioPciBindingDerivedBarSizeCatalogStatus status, int inspectedCandidateCount, int bindingCount, int requiredBindingCapacity, int failingCandidateIndex, int failingSourceIndex, VirtioPciBindingDerivedBarSizePlanResult failingPlan, bool isInitialized)
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
    public VirtioPciBindingDerivedBarSizeCatalogStatus Status
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
    public VirtioPciBindingDerivedBarSizePlanResult FailingPlan
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioPciBindingDerivedBarSizeCatalogStatus.Success;

    public static VirtioPciBindingDerivedBarSizeCatalogResult Success(int inspectedCandidateCount, int bindingCount)
    {
        return new VirtioPciBindingDerivedBarSizeCatalogResult(VirtioPciBindingDerivedBarSizeCatalogStatus.Success, inspectedCandidateCount, bindingCount, bindingCount, NoFailingCandidateIndex, NoFailingSourceIndex, default, isInitialized: true);
    }

    public static VirtioPciBindingDerivedBarSizeCatalogResult Fail(VirtioPciBindingDerivedBarSizeCatalogStatus status, int inspectedCandidateCount, int bindingCount, int requiredBindingCapacity, int failingCandidateIndex, int failingSourceIndex, VirtioPciBindingDerivedBarSizePlanResult failingPlan = default)
    {
        return new VirtioPciBindingDerivedBarSizeCatalogResult(status, inspectedCandidateCount, bindingCount, requiredBindingCapacity, failingCandidateIndex, failingSourceIndex, failingPlan, isInitialized: true);
    }
}
