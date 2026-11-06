namespace OS.Drivers;

public readonly struct PciBarSizingProbeDerivedSizeResult
{
    private PciBarSizingProbeDerivedSizeResult(PciBarSizingProbeDerivedSizeStatus status, PciBarSizingProbeAddressPlanResult addressPlan, PciBarSizingProbeExecutionResult execution, PciBarSizingPlanResult sizing, byte failingBarIndex, PciBarSizeDescriptorStatus failingDescriptorStatus, bool isInitialized)
    {
        Status = status;
        AddressPlan = addressPlan;
        Execution = execution;
        Sizing = sizing;
        FailingBarIndex = failingBarIndex;
        FailingDescriptorStatus = failingDescriptorStatus;
        IsInitialized = isInitialized;
    }

    public PciBarSizingProbeDerivedSizeStatus Status
    {
        get;
    }
    public PciBarSizingProbeAddressPlanResult AddressPlan
    {
        get;
    }
    public PciBarSizingProbeAddressPlanStatus AddressPlanStatus => AddressPlan.Status;
    public PciBarSizingProbeExecutionResult Execution
    {
        get;
    }
    public PciBarSizingProbeExecutionStatus ExecutionStatus => Execution.Status;
    public PciBarSizingPlanResult Sizing
    {
        get;
    }
    public PciBarSizingPlanStatus SizingStatus => Sizing.Status;
    public PciBarSizeDescriptorSet Bars => Sizing.Bars;
    public byte FailingBarIndex
    {
        get;
    }
    public PciBarSizeDescriptorStatus FailingDescriptorStatus
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == PciBarSizingProbeDerivedSizeStatus.Success;

    public bool TryCopySizesTo(Span<ulong> destination)
    {
        return Succeeded && Sizing.TryCopySizesTo(destination);
    }

    public static PciBarSizingProbeDerivedSizeResult Create(PciBarSizingProbeDerivedSizeStatus status, PciBarSizingProbeAddressPlanResult addressPlan, PciBarSizingProbeExecutionResult execution, PciBarSizingPlanResult sizing, byte failingBarIndex, PciBarSizeDescriptorStatus failingDescriptorStatus)
    {
        return new PciBarSizingProbeDerivedSizeResult(status, addressPlan, execution, sizing, failingBarIndex, failingDescriptorStatus, isInitialized: true);
    }
}
