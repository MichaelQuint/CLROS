namespace OS.Kernel.Memory;

public readonly struct X64Cr3LoadStubPlanResult
{
    public const ulong StructureSize = 1072;
    private readonly X64Cr3LoadStubPlanStatus _status;
    private readonly OS.Boot.AddressValueStatus _addressStatus;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingStatus _identityMappingStatus;
    private readonly OS.Boot.OwnedBootStackSwitchStatus _stackSwitchStatus;
    private readonly uint _reserved;
    private readonly ulong _allocationRecordCount;
    private readonly ulong _allocationRecordCapacity;
    private readonly ulong _mappingRangeCount;
    private readonly ulong _mappingRangeCapacity;
    private readonly ulong _invalidIndex;
    private readonly ulong _conflictingIndex;
    private readonly X64Cr3LoadStubPlan _plan;
    private X64Cr3LoadStubPlanResult(X64Cr3LoadStubPlanStatus status, OS.Boot.AddressValueStatus addressStatus, OS.Kernel.Boot.X64BootIdentityMappingStatus identityMappingStatus, OS.Boot.OwnedBootStackSwitchStatus stackSwitchStatus, ulong allocationRecordCount, ulong allocationRecordCapacity, ulong mappingRangeCount, ulong mappingRangeCapacity, ulong invalidIndex, ulong conflictingIndex, X64Cr3LoadStubPlan plan)
    {
        _status = status;
        _addressStatus = addressStatus;
        _identityMappingStatus = identityMappingStatus;
        _stackSwitchStatus = stackSwitchStatus;
        _reserved = 0;
        _allocationRecordCount = allocationRecordCount;
        _allocationRecordCapacity = allocationRecordCapacity;
        _mappingRangeCount = mappingRangeCount;
        _mappingRangeCapacity = mappingRangeCapacity;
        _invalidIndex = invalidIndex;
        _conflictingIndex = conflictingIndex;
        _plan = plan;
    }

    public X64Cr3LoadStubPlanStatus Status => _status;
    public OS.Boot.AddressValueStatus AddressStatus => _addressStatus;
    public OS.Kernel.Boot.X64BootIdentityMappingStatus IdentityMappingStatus => _identityMappingStatus;
    public OS.Boot.OwnedBootStackSwitchStatus StackSwitchStatus => _stackSwitchStatus;
    public ulong AllocationRecordCount => _allocationRecordCount;
    public ulong AllocationRecordCapacity => _allocationRecordCapacity;
    public ulong MappingRangeCount => _mappingRangeCount;
    public ulong MappingRangeCapacity => _mappingRangeCapacity;
    public ulong InvalidIndex => _invalidIndex;
    public ulong ConflictingIndex => _conflictingIndex;
    public X64Cr3LoadStubPlan Plan => _plan;
    public bool Succeeded => _status == X64Cr3LoadStubPlanStatus.Success && _addressStatus == OS.Boot.AddressValueStatus.Success && _identityMappingStatus == OS.Kernel.Boot.X64BootIdentityMappingStatus.Success && _stackSwitchStatus == OS.Boot.OwnedBootStackSwitchStatus.Success && _reserved == 0 && _allocationRecordCount == X64Cr3LoadStubPlan.RequiredAllocationRecordCount && _allocationRecordCapacity == _allocationRecordCount && _mappingRangeCount == _plan.IdentityMappingPlan.OutputRangeCount && _mappingRangeCapacity == _plan.IdentityMappingPlan.DestinationCapacity && _mappingRangeCapacity >= _mappingRangeCount && _identityMappingStatus == _plan.IdentityMappingPlan.Status && _stackSwitchStatus == _plan.StackSwitchPlan.Status && _invalidIndex == X64Cr3LoadStubPlan.NoIndex && _conflictingIndex == X64Cr3LoadStubPlan.NoIndex && _plan.IsValid;

    internal static X64Cr3LoadStubPlanResult Create(X64Cr3LoadStubPlanStatus status, OS.Boot.AddressValueStatus addressStatus, OS.Kernel.Boot.X64BootIdentityMappingStatus identityMappingStatus, OS.Boot.OwnedBootStackSwitchStatus stackSwitchStatus, ulong allocationRecordCount, ulong allocationRecordCapacity, ulong mappingRangeCount, ulong mappingRangeCapacity, ulong invalidIndex, ulong conflictingIndex, X64Cr3LoadStubPlan plan)
    {
        return new X64Cr3LoadStubPlanResult(status, addressStatus, identityMappingStatus, stackSwitchStatus, allocationRecordCount, allocationRecordCapacity, mappingRangeCount, mappingRangeCapacity, invalidIndex, conflictingIndex, plan);
    }
}
