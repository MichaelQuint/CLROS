namespace OS.Drivers;

public readonly struct PciBarSizingProbeExecutionResult
{
    private PciBarSizingProbeExecutionResult(PciBarSizingProbeExecutionStatus status, PciBarSizingProbeAddressPlanResult addressPlan, PciConfigDwordAccessStatus accessStatus, int requiredCommandCount, int availableCommandCount, int executedCommandCount, int requiredReadbackCount, int availableReadbackCount, int readbackCount, int failingCommandIndex, byte failingBarIndex, ushort failingRegisterOffset, PciBarSizingProbeCommandKind failingCommandKind, ulong failingPhysicalAddress, uint value, bool isInitialized)
    {
        Status = status;
        AddressPlan = addressPlan;
        AccessStatus = accessStatus;
        RequiredCommandCount = requiredCommandCount;
        AvailableCommandCount = availableCommandCount;
        ExecutedCommandCount = executedCommandCount;
        RequiredReadbackCount = requiredReadbackCount;
        AvailableReadbackCount = availableReadbackCount;
        ReadbackCount = readbackCount;
        FailingCommandIndex = failingCommandIndex;
        FailingBarIndex = failingBarIndex;
        FailingRegisterOffset = failingRegisterOffset;
        FailingCommandKind = failingCommandKind;
        FailingPhysicalAddress = failingPhysicalAddress;
        Value = value;
        IsInitialized = isInitialized;
    }

    public PciBarSizingProbeExecutionStatus Status
    {
        get;
    }
    public PciBarSizingProbeAddressPlanResult AddressPlan
    {
        get;
    }
    public PciBarSizingProbeAddressPlanStatus AddressPlanStatus => AddressPlan.Status;
    public PciConfigDwordAccessStatus AccessStatus
    {
        get;
    }
    public int RequiredCommandCount
    {
        get;
    }
    public int AvailableCommandCount
    {
        get;
    }
    public int ExecutedCommandCount
    {
        get;
    }
    public int RequiredReadbackCount
    {
        get;
    }
    public int AvailableReadbackCount
    {
        get;
    }
    public int ReadbackCount
    {
        get;
    }
    public int FailingCommandIndex
    {
        get;
    }
    public byte FailingBarIndex
    {
        get;
    }
    public ushort FailingRegisterOffset
    {
        get;
    }
    public PciBarSizingProbeCommandKind FailingCommandKind
    {
        get;
    }
    public ulong FailingPhysicalAddress
    {
        get;
    }
    public uint Value
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == PciBarSizingProbeExecutionStatus.Success;

    public static PciBarSizingProbeExecutionResult Create(PciBarSizingProbeExecutionStatus status, PciBarSizingProbeAddressPlanResult addressPlan, PciConfigDwordAccessStatus accessStatus, int requiredCommandCount, int availableCommandCount, int executedCommandCount, int requiredReadbackCount, int availableReadbackCount, int readbackCount, int failingCommandIndex, byte failingBarIndex, ushort failingRegisterOffset, PciBarSizingProbeCommandKind failingCommandKind, ulong failingPhysicalAddress, uint value)
    {
        return new PciBarSizingProbeExecutionResult(status, addressPlan, accessStatus, requiredCommandCount, availableCommandCount, executedCommandCount, requiredReadbackCount, availableReadbackCount, readbackCount, failingCommandIndex, failingBarIndex, failingRegisterOffset, failingCommandKind, failingPhysicalAddress, value, isInitialized: true);
    }
}
