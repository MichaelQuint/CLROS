namespace OS.Drivers;

public readonly struct PciBarSizingPlanResult
{
    private PciBarSizingPlanResult(PciBarSizingPlanStatus status, byte failingBarIndex, PciBarSizeDescriptorStatus failingDescriptorStatus, PciBarSizeDescriptorSet bars, bool isInitialized)
    {
        Status = status;
        FailingBarIndex = failingBarIndex;
        FailingDescriptorStatus = failingDescriptorStatus;
        Bars = bars;
        IsInitialized = isInitialized;
    }

    public PciBarSizingPlanStatus Status
    {
        get;
    }
    public byte FailingBarIndex
    {
        get;
    }
    public PciBarSizeDescriptorStatus FailingDescriptorStatus
    {
        get;
    }
    public PciBarSizeDescriptorSet Bars
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == PciBarSizingPlanStatus.Success;

    public bool TryCopySizesTo(Span<ulong> destination)
    {
        return Succeeded && Bars.TryCopySizesTo(destination);
    }

    public static PciBarSizingPlanResult Create(PciBarSizingPlanStatus status, byte failingBarIndex, PciBarSizeDescriptorStatus failingDescriptorStatus, PciBarSizeDescriptorSet bars)
    {
        return new PciBarSizingPlanResult(status, failingBarIndex, failingDescriptorStatus, bars, isInitialized: true);
    }
}
