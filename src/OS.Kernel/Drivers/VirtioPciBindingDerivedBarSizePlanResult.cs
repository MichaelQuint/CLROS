namespace OS.Drivers;

public readonly struct VirtioPciBindingDerivedBarSizePlanResult
{
    private VirtioPciBindingDerivedBarSizePlanResult(VirtioPciBindingDerivedBarSizePlanStatus status, PciConfigurationHeaderStatus headerStatus, PciBarDecodeResult barDecode, PciBarSizingProbeDerivedSizeResult derivedSize, VirtioPciBindingPlanResult bindingPlan, bool isInitialized)
    {
        Status = status;
        HeaderStatus = headerStatus;
        BarDecode = barDecode;
        DerivedSize = derivedSize;
        BindingPlan = bindingPlan;
        IsInitialized = isInitialized;
    }

    public VirtioPciBindingDerivedBarSizePlanStatus Status
    {
        get;
    }
    public PciConfigurationHeaderStatus HeaderStatus
    {
        get;
    }
    public PciBarDecodeResult BarDecode
    {
        get;
    }
    public PciBarSizingProbeDerivedSizeResult DerivedSize
    {
        get;
    }
    public PciBarSizingProbeDerivedSizeStatus DerivedSizeStatus => DerivedSize.Status;
    public PciBarSizingProbeExecutionStatus ExecutionStatus => DerivedSize.ExecutionStatus;
    public PciBarSizingPlanStatus SizingStatus => DerivedSize.SizingStatus;
    public VirtioPciBindingPlanResult BindingPlan
    {
        get;
    }
    public VirtioPciBindingPlanStatus BindingStatus => BindingPlan.Status;
    public VirtioPciBindingMetadata Binding => BindingPlan.Binding;
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == VirtioPciBindingDerivedBarSizePlanStatus.Success;

    public static VirtioPciBindingDerivedBarSizePlanResult Create(VirtioPciBindingDerivedBarSizePlanStatus status, PciConfigurationHeaderStatus headerStatus, PciBarDecodeResult barDecode, PciBarSizingProbeDerivedSizeResult derivedSize, VirtioPciBindingPlanResult bindingPlan)
    {
        return new VirtioPciBindingDerivedBarSizePlanResult(status, headerStatus, barDecode, derivedSize, bindingPlan, isInitialized: true);
    }
}
