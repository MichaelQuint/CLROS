namespace OS.Drivers;
public readonly struct PciBarSizingProbeAddressPlanResult
{
    private PciBarSizingProbeAddressPlanResult(PciBarSizingProbeAddressPlanStatus status, PciBarSizingProbePlanResult probePlan, PciEcamConfigurationSpaceStatus addressStatus, PciEcamConfigurationAddressResult addressResult, ushort commandCount, ushort requiredCommandCount, ushort failingCommandIndex, byte failingBarIndex, ushort failingRegisterOffset, PciBarKind failingBarKind, bool isInitialized)
    {
        Status = status;
        ProbePlan = probePlan;
        AddressStatus = addressStatus;
        AddressResult = addressResult;
        CommandCount = commandCount;
        RequiredCommandCount = requiredCommandCount;
        FailingCommandIndex = failingCommandIndex;
        FailingBarIndex = failingBarIndex;
        FailingRegisterOffset = failingRegisterOffset;
        FailingBarKind = failingBarKind;
        IsInitialized = isInitialized;
    }

    public PciBarSizingProbeAddressPlanStatus Status { get; }
    public PciBarSizingProbePlanResult ProbePlan { get; }
    public PciBarSizingProbePlanStatus ProbePlanStatus => ProbePlan.Status;
    public PciEcamConfigurationSpaceStatus AddressStatus { get; }
    public PciEcamConfigurationAddressResult AddressResult { get; }
    public ushort CommandCount { get; }
    public ushort RequiredCommandCount { get; }
    public ushort FailingCommandIndex { get; }
    public byte FailingBarIndex { get; }
    public ushort FailingRegisterOffset { get; }
    public PciBarKind FailingBarKind { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == PciBarSizingProbeAddressPlanStatus.Success;

    public static PciBarSizingProbeAddressPlanResult Create(PciBarSizingProbeAddressPlanStatus status, PciBarSizingProbePlanResult probePlan, PciEcamConfigurationSpaceStatus addressStatus, PciEcamConfigurationAddressResult addressResult, ushort commandCount, ushort requiredCommandCount, ushort failingCommandIndex, byte failingBarIndex, ushort failingRegisterOffset, PciBarKind failingBarKind)
    {
        return new PciBarSizingProbeAddressPlanResult(status, probePlan, addressStatus, addressResult, commandCount, requiredCommandCount, failingCommandIndex, failingBarIndex, failingRegisterOffset, failingBarKind, isInitialized: true);
    }
}
