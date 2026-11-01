namespace OS.Boot;
/// <summary>
/// Preserves creation evidence without retaining the caller's allocation
/// allocation-table pointer.
/// </summary>

public readonly struct OwnedBootStackSwitchPlanResult
{
    private readonly OwnedBootStackSwitchStatus _status;
    private readonly AddressValueStatus _addressStatus;
    private readonly OwnedBootStackLifetime _stackLifetime;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly ulong _allocationRecordCount;
    private readonly ulong _availableAllocationRecordCapacity;
    private readonly ulong _invalidAllocationRecordIndex;
    private readonly OwnedBootStackSwitchPlan _plan;
    private OwnedBootStackSwitchPlanResult(OwnedBootStackSwitchStatus status, AddressValueStatus addressStatus, OwnedBootStackLifetime stackLifetime, ulong allocationRecordCount, ulong availableAllocationRecordCapacity, ulong invalidAllocationRecordIndex, OwnedBootStackSwitchPlan plan)
    {
        _status = status;
        _addressStatus = addressStatus;
        _stackLifetime = stackLifetime;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _allocationRecordCount = allocationRecordCount;
        _availableAllocationRecordCapacity = availableAllocationRecordCapacity;
        _invalidAllocationRecordIndex = invalidAllocationRecordIndex;
        _plan = plan;
    }

    public OwnedBootStackSwitchStatus Status => _status;
    public AddressValueStatus AddressStatus => _addressStatus;
    public OwnedBootStackLifetime StackLifetime => _stackLifetime;
    public ulong AllocationRecordCount => _allocationRecordCount;
    public ulong AvailableAllocationRecordCapacity => _availableAllocationRecordCapacity;
    public ulong RequiredAllocationRecordCapacity => OwnedBootStackSwitchPlan.RequiredAllocationRecordCount;
    public ulong InvalidAllocationRecordIndex => _invalidAllocationRecordIndex;
    public OwnedBootStackSwitchPlan Plan => _plan;
    public bool Succeeded => _status == OwnedBootStackSwitchStatus.Success && _addressStatus == AddressValueStatus.Success && _stackLifetime == OwnedBootStackLifetime.Ready && _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0 && _allocationRecordCount == OwnedBootStackSwitchPlan.RequiredAllocationRecordCount && _availableAllocationRecordCapacity >= _allocationRecordCount && _invalidAllocationRecordIndex == OwnedBootStackSwitchPlan.NoIndex && _plan.IsValid;

    internal static OwnedBootStackSwitchPlanResult Success(OwnedBootStackLifetime stackLifetime, ulong allocationRecordCount, ulong availableAllocationRecordCapacity, OwnedBootStackSwitchPlan plan)
    {
        return new OwnedBootStackSwitchPlanResult(OwnedBootStackSwitchStatus.Success, AddressValueStatus.Success, stackLifetime, allocationRecordCount, availableAllocationRecordCapacity, OwnedBootStackSwitchPlan.NoIndex, plan);
    }

    internal static OwnedBootStackSwitchPlanResult Fail(OwnedBootStackSwitchStatus status, AddressValueStatus addressStatus, OwnedBootStackLifetime stackLifetime, ulong allocationRecordCount, ulong availableAllocationRecordCapacity, ulong invalidAllocationRecordIndex)
    {
        return new OwnedBootStackSwitchPlanResult(status, addressStatus, stackLifetime, allocationRecordCount, availableAllocationRecordCapacity, invalidAllocationRecordIndex, default);
    }
}
