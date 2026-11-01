namespace OS.Drivers;
public readonly struct PciBarDecodeResult
{
    private PciBarDecodeResult(bool isInitialized, PciBarDecodeStatus status, PciBarDescriptorSet bars, byte failingBarIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        Bars = bars;
        FailingBarIndex = failingBarIndex;
    }

    public bool IsInitialized { get; }
    public PciBarDecodeStatus Status { get; }
    public PciBarDescriptorSet Bars { get; }
    public byte FailingBarIndex { get; }
    public bool Succeeded => IsInitialized && Status == PciBarDecodeStatus.Success;

    public static PciBarDecodeResult Create(PciBarDecodeStatus status, PciBarDescriptorSet bars, byte failingBarIndex)
    {
        return new PciBarDecodeResult(true, status, bars, failingBarIndex);
    }
}
