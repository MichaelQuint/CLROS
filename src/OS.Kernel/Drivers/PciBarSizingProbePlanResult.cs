namespace OS.Drivers;
public readonly struct PciBarSizingProbePlanResult
{
    private PciBarSizingProbePlanResult(PciBarSizingProbePlanStatus status, ushort commandCount, ushort requiredCommandCount, byte memoryBarCount, byte skippedBarCount, byte failingBarIndex, PciBarKind failingBarKind, bool isInitialized)
    {
        Status = status;
        CommandCount = commandCount;
        RequiredCommandCount = requiredCommandCount;
        MemoryBarCount = memoryBarCount;
        SkippedBarCount = skippedBarCount;
        FailingBarIndex = failingBarIndex;
        FailingBarKind = failingBarKind;
        IsInitialized = isInitialized;
    }

    public PciBarSizingProbePlanStatus Status { get; }
    public ushort CommandCount { get; }
    public ushort RequiredCommandCount { get; }
    public byte MemoryBarCount { get; }
    public byte SkippedBarCount { get; }
    public byte FailingBarIndex { get; }
    public PciBarKind FailingBarKind { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == PciBarSizingProbePlanStatus.Success;

    public static PciBarSizingProbePlanResult Create(PciBarSizingProbePlanStatus status, ushort commandCount, ushort requiredCommandCount, byte memoryBarCount, byte skippedBarCount, byte failingBarIndex, PciBarKind failingBarKind)
    {
        return new PciBarSizingProbePlanResult(status, commandCount, requiredCommandCount, memoryBarCount, skippedBarCount, failingBarIndex, failingBarKind, isInitialized: true);
    }
}
