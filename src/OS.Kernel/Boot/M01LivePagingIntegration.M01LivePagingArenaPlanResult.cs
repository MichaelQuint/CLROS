namespace OS.Boot;
/// <summary>
/// Value-only result of one bounded table/state-arena fixed-point plan.
/// The exact LoaderData allocation record can be passed unchanged to the
/// materializer after the caller has performed the allocation.
/// </summary>

public readonly struct M01LivePagingArenaPlanResult
{
    public const ushort CurrentVersion = 1;
    public const ulong StructureSize = 568;
    public const ulong NoIndex = ~0UL;
    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly M01LivePagingArenaPlanStatus _status;
    private readonly byte _isInitialized;
    private readonly AddressValueStatus _addressStatus;
    private readonly OS.Kernel.Memory.BootReservationTableStatus _reservationStatus;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingStatus _mappingStatus;
    private readonly OS.Kernel.Memory.X64PageTableEntryStatus _pageTableEntryStatus;
    private readonly uint _reserved;
    private readonly OS.Kernel.Memory.X64PageTableEncodingPreconditions _pageTablePreconditions;
    private readonly BootMemoryRegionRecord _allocationRecord;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingPlanResult _mappingPlan;
    private readonly PhysicalAddress _candidatePhysicalBase;
    private readonly PhysicalAddress _rootTable;
    private readonly PhysicalAddress _statePage;
    private readonly PhysicalAddress _arenaEndExclusive;
    private readonly ulong _iterationCount;
    private readonly ulong _iterationLimit;
    private readonly ulong _tablePageCount;
    private readonly ulong _statePageCount;
    private readonly ulong _totalPageCount;
    private readonly ulong _tableByteLength;
    private readonly ulong _totalByteLength;
    private readonly ulong _availablePageCountFromCandidate;
    private readonly ulong _requiredFixedRequirementCount;
    private readonly ulong _requiredRequirementWorkspaceCount;
    private readonly ulong _requiredRangeWorkspaceCount;
    private readonly ulong _requiredPageImageCount;
    private readonly ulong _requiredPageTableEntryCount;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingRequirement _bootStackGuardRequirement;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingRequirement _codeAndCallGateRequirement;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingRequirement _ownedStackRequirement;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingRequirement _protectionProbeRequirement;
    internal M01LivePagingArenaPlanResult(M01LivePagingArenaPlanStatus status, AddressValueStatus addressStatus, OS.Kernel.Memory.BootReservationTableStatus reservationStatus, OS.Kernel.Boot.X64BootIdentityMappingStatus mappingStatus, OS.Kernel.Memory.X64PageTableEntryStatus pageTableEntryStatus, OS.Kernel.Memory.X64PageTableEncodingPreconditions pageTablePreconditions, BootMemoryRegionRecord allocationRecord, OS.Kernel.Boot.X64BootIdentityMappingPlanResult mappingPlan, PhysicalAddress candidatePhysicalBase, PhysicalAddress rootTable, PhysicalAddress statePage, PhysicalAddress arenaEndExclusive, ulong iterationCount, ulong iterationLimit, ulong tablePageCount, ulong statePageCount, ulong totalPageCount, ulong tableByteLength, ulong totalByteLength, ulong availablePageCountFromCandidate, ulong requiredFixedRequirementCount, ulong requiredRequirementWorkspaceCount, ulong requiredRangeWorkspaceCount, ulong requiredPageImageCount, ulong requiredPageTableEntryCount, OS.Kernel.Boot.X64BootIdentityMappingRequirement bootStackGuardRequirement, OS.Kernel.Boot.X64BootIdentityMappingRequirement codeAndCallGateRequirement, OS.Kernel.Boot.X64BootIdentityMappingRequirement ownedStackRequirement, OS.Kernel.Boot.X64BootIdentityMappingRequirement protectionProbeRequirement)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = CurrentVersion;
        _status = status;
        _isInitialized = 1;
        _addressStatus = addressStatus;
        _reservationStatus = reservationStatus;
        _mappingStatus = mappingStatus;
        _pageTableEntryStatus = pageTableEntryStatus;
        _reserved = 0;
        _pageTablePreconditions = pageTablePreconditions;
        _allocationRecord = allocationRecord;
        _mappingPlan = mappingPlan;
        _candidatePhysicalBase = candidatePhysicalBase;
        _rootTable = rootTable;
        _statePage = statePage;
        _arenaEndExclusive = arenaEndExclusive;
        _iterationCount = iterationCount;
        _iterationLimit = iterationLimit;
        _tablePageCount = tablePageCount;
        _statePageCount = statePageCount;
        _totalPageCount = totalPageCount;
        _tableByteLength = tableByteLength;
        _totalByteLength = totalByteLength;
        _availablePageCountFromCandidate = availablePageCountFromCandidate;
        _requiredFixedRequirementCount = requiredFixedRequirementCount;
        _requiredRequirementWorkspaceCount = requiredRequirementWorkspaceCount;
        _requiredRangeWorkspaceCount = requiredRangeWorkspaceCount;
        _requiredPageImageCount = requiredPageImageCount;
        _requiredPageTableEntryCount = requiredPageTableEntryCount;
        _bootStackGuardRequirement = bootStackGuardRequirement;
        _codeAndCallGateRequirement = codeAndCallGateRequirement;
        _ownedStackRequirement = ownedStackRequirement;
        _protectionProbeRequirement = protectionProbeRequirement;
    }

    public uint Size => _structureSize;
    public ushort Version => _structureVersion;
    public bool IsInitialized => _structureSize == StructureSize && _structureVersion == CurrentVersion && _isInitialized == 1 && _reserved == 0;
    public M01LivePagingArenaPlanStatus Status => _status;
    public AddressValueStatus AddressStatus => _addressStatus;
    public OS.Kernel.Memory.BootReservationTableStatus ReservationStatus => _reservationStatus;
    public OS.Kernel.Boot.X64BootIdentityMappingStatus MappingStatus => _mappingStatus;
    public OS.Kernel.Memory.X64PageTableEntryStatus PageTableEntryStatus => _pageTableEntryStatus;
    public OS.Kernel.Memory.X64PageTableEncodingPreconditions PageTablePreconditions => _pageTablePreconditions;
    public BootMemoryRegionRecord AllocationRecord => _allocationRecord;
    public OS.Kernel.Boot.X64BootIdentityMappingPlanResult MappingPlan => _mappingPlan;
    public PhysicalAddress CandidatePhysicalBase => _candidatePhysicalBase;
    public PhysicalAddress RootTable => _rootTable;
    public PhysicalAddress StatePage => _statePage;
    public PhysicalAddress ArenaEndExclusive => _arenaEndExclusive;
    public ulong IterationCount => _iterationCount;
    public ulong IterationLimit => _iterationLimit;
    public ulong TablePageCount => _tablePageCount;
    public ulong StatePageCount => _statePageCount;
    public ulong TotalPageCount => _totalPageCount;
    public ulong TableByteLength => _tableByteLength;
    public ulong TotalByteLength => _totalByteLength;
    public ulong AvailablePageCountFromCandidate => _availablePageCountFromCandidate;
    public ulong RequiredFixedRequirementCount => _requiredFixedRequirementCount;
    public ulong RequiredRequirementWorkspaceCount => _requiredRequirementWorkspaceCount;
    public ulong RequiredRangeWorkspaceCount => _requiredRangeWorkspaceCount;
    public ulong RequiredPageImageCount => _requiredPageImageCount;
    public ulong RequiredPageTableEntryCount => _requiredPageTableEntryCount;
    public OS.Kernel.Boot.X64BootIdentityMappingRequirement BootStackGuardRequirement => _bootStackGuardRequirement;
    public OS.Kernel.Boot.X64BootIdentityMappingRequirement CodeAndCallGateRequirement => _codeAndCallGateRequirement;
    public OS.Kernel.Boot.X64BootIdentityMappingRequirement OwnedStackRequirement => _ownedStackRequirement;
    public OS.Kernel.Boot.X64BootIdentityMappingRequirement ProtectionProbeRequirement => _protectionProbeRequirement;
    public bool Succeeded => IsInitialized && _status == M01LivePagingArenaPlanStatus.Success && _addressStatus == AddressValueStatus.Success && _reservationStatus == OS.Kernel.Memory.BootReservationTableStatus.Success && _mappingStatus == OS.Kernel.Boot.X64BootIdentityMappingStatus.Success && _pageTableEntryStatus == OS.Kernel.Memory.X64PageTableEntryStatus.Success && _mappingPlan.Succeeded && _candidatePhysicalBase.IsValid && _rootTable.IsValid && _statePage.IsValid && _arenaEndExclusive.IsValid && _tablePageCount != 0 && _statePageCount == M01LivePagingIntegration.RequiredArenaStatePageCount && _totalPageCount == _tablePageCount + _statePageCount && _requiredPageImageCount == _tablePageCount && _requiredPageTableEntryCount == _tablePageCount * OS.Kernel.Memory.X64PageTableImagePage.EntryCountPerPage && _bootStackGuardRequirement.IsInitialized && _bootStackGuardRequirement.Purpose == OS.Kernel.Boot.X64BootIdentityMappingPurpose.BootStackGuard && _codeAndCallGateRequirement.IsInitialized && _codeAndCallGateRequirement.Purpose == OS.Kernel.Boot.X64BootIdentityMappingPurpose.CompositeCodeAndCallGate && _ownedStackRequirement.IsInitialized && _ownedStackRequirement.Purpose == OS.Kernel.Boot.X64BootIdentityMappingPurpose.CompositeOwnedStack && _protectionProbeRequirement.IsInitialized && _protectionProbeRequirement.Purpose == OS.Kernel.Boot.X64BootIdentityMappingPurpose.ProtectionProbePage;
}
