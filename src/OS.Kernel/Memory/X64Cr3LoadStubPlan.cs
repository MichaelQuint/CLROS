namespace OS.Kernel.Memory;

// Reference boundary:
// - SharpOS Cr3Accessor.cs, local snapshot SHA-256
//   088D73C1BF807B652CD2F7ED795C1507C1F28884DD4184A2A28A574CF01E9E47,
//   was used only to cross-check that CR3 restoration must preserve the
//   observed raw register value rather than reconstruct an address.
// - SharpOS BootAsmGenerator.cs, local snapshot SHA-256
//   B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500,
//   influenced deterministic plan-versus-materialized-byte auditing.
// - SharpOS PagingValidation.cs, local snapshot SHA-256
//   B1699D328E49D5FC68B8CF8979B366D72CCA2E3B4AA5B982F91F0A2C9A1C40F6,
//   supplied missing-map/alignment/readback negative-test categories only.
// No reference implementation, live CR3 operation, or firmware callback path
// is imported by this host-audited leaf.

/// <summary>
/// Records the only accepted opt-in policy for the reversible CR3 probe.
/// Default-enabled execution is always rejected.
/// </summary>
public readonly struct X64Cr3ProbePolicy
{
    public const ulong StructureSize = 16;

    private readonly ulong _timeoutTicks;
    private readonly byte _isInitialized;
    private readonly byte _enabledByDefault;
    private readonly byte _explicitOneShotOptIn;
    private readonly byte _reserved0;
    private readonly uint _reserved1;

    public X64Cr3ProbePolicy(
        bool enabledByDefault,
        bool explicitOneShotOptIn,
        ulong timeoutTicks)
    {
        _timeoutTicks = timeoutTicks;
        _isInitialized = 1;
        _enabledByDefault = enabledByDefault ? (byte)1 : (byte)0;
        _explicitOneShotOptIn =
            explicitOneShotOptIn ? (byte)1 : (byte)0;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized =>
        _isInitialized == 1
        && _enabledByDefault <= 1
        && _explicitOneShotOptIn <= 1
        && _reserved0 == 0
        && _reserved1 == 0;

    public bool EnabledByDefault => _enabledByDefault == 1;

    public bool ExplicitOneShotOptIn =>
        _explicitOneShotOptIn == 1;

    public ulong TimeoutTicks => _timeoutTicks;
}

public enum X64Cr3ProbeStorageProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedOwnedLoaderData = 1
}

/// <summary>
/// Binds the caller-owned state and canary bytes inside the final LoaderData
/// state page. The values describe ownership only; they do not map memory.
/// </summary>
public readonly struct X64Cr3ProbeStorageLayout
{
    public const ulong StructureSize = 56;
    public const ulong RequiredStateRecordByteLength =
        X64ReversibleCr3TransitionRuntimeState.StructureSize;
    public const ulong RequiredCanaryByteLength = 8;
    public const ulong RequiredAlignment = 8;

    private readonly OS.Boot.VirtualAddress _stateAddress;
    private readonly ulong _stateByteLength;
    private readonly OS.Boot.VirtualAddress _canaryAddress;
    private readonly ulong _canaryByteLength;
    private readonly ulong _initialCanaryValue;
    private readonly ulong _updatedCanaryValue;
    private readonly byte _isInitialized;
    private readonly X64Cr3ProbeStorageProvenance _provenance;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;

    public X64Cr3ProbeStorageLayout(
        OS.Boot.VirtualAddress stateAddress,
        ulong stateByteLength,
        OS.Boot.VirtualAddress canaryAddress,
        ulong canaryByteLength,
        ulong initialCanaryValue,
        ulong updatedCanaryValue,
        X64Cr3ProbeStorageProvenance provenance)
    {
        _stateAddress = stateAddress;
        _stateByteLength = stateByteLength;
        _canaryAddress = canaryAddress;
        _canaryByteLength = canaryByteLength;
        _initialCanaryValue = initialCanaryValue;
        _updatedCanaryValue = updatedCanaryValue;
        _isInitialized = 1;
        _provenance = provenance;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized =>
        _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0;

    public OS.Boot.VirtualAddress StateAddress => _stateAddress;

    public ulong StateByteLength => _stateByteLength;

    public OS.Boot.VirtualAddress CanaryAddress => _canaryAddress;

    public ulong CanaryByteLength => _canaryByteLength;

    public ulong InitialCanaryValue => _initialCanaryValue;

    public ulong UpdatedCanaryValue => _updatedCanaryValue;

    public X64Cr3ProbeStorageProvenance Provenance => _provenance;
}

public enum X64Cr3ProbeEventKind : byte
{
    Uninitialized = 0,
    PreMarkerEmitted = 1,
    TransitionEntered = 2,
    OwnedRootLoaded = 3,
    OwnedRootReadBack = 4,
    CanaryUpdated = 5,
    FirmwareRootRestored = 6,
    FirmwareRootReadBack = 7,
    CanaryValidated = 8,
    PostMarkerEmitted = 9,
    FirmwareCallbackAttempted = 10,
    InterruptReenabledInsideInterval = 11,
    Timeout = 12,
    Reset = 13,
    TripleFault = 14
}

/// <summary>
/// One caller-owned observation in the modeled probe transcript.
/// </summary>
public readonly struct X64Cr3ProbeEvent
{
    public const ulong StructureSize = 40;

    private readonly ulong _observedRawCr3;
    private readonly OS.Boot.VirtualAddress _observedRsp;
    private readonly ulong _observedCanary;
    private readonly ulong _elapsedTicks;
    private readonly byte _isInitialized;
    private readonly X64Cr3ProbeEventKind _kind;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;

    public X64Cr3ProbeEvent(
        X64Cr3ProbeEventKind kind,
        ulong observedRawCr3,
        OS.Boot.VirtualAddress observedRsp,
        ulong observedCanary,
        ulong elapsedTicks)
    {
        _observedRawCr3 = observedRawCr3;
        _observedRsp = observedRsp;
        _observedCanary = observedCanary;
        _elapsedTicks = elapsedTicks;
        _isInitialized = 1;
        _kind = kind;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized =>
        _isInitialized == 1
        && _kind >= X64Cr3ProbeEventKind.PreMarkerEmitted
        && _kind <= X64Cr3ProbeEventKind.TripleFault
        && _observedRsp.IsValid
        && _reserved0 == 0
        && _reserved1 == 0;

    public X64Cr3ProbeEventKind Kind => _kind;

    public ulong ObservedRawCr3 => _observedRawCr3;

    public OS.Boot.VirtualAddress ObservedRsp => _observedRsp;

    public ulong ObservedCanary => _observedCanary;

    public ulong ElapsedTicks => _elapsedTicks;
}

/// <summary>
/// Stable composite planning and transcript-audit statuses. Values are
/// append-only and the zero-valued default is not success.
/// </summary>
public enum X64Cr3LoadStubPlanStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidPolicy = 2,
    EnabledByDefault = 3,
    ExplicitOptInRequired = 4,
    InvalidTimeout = 5,
    InvalidAllocationRecordCount = 6,
    TooManyAllocationRecords = 7,
    AllocationRecordCapacityTooSmall = 8,
    MissingAllocationRecords = 9,
    MisalignedAllocationRecords = 10,
    AllocationRecordSpanOverflow = 11,
    InvalidCodeMemoryType = 12,
    InvalidCodeAllocation = 13,
    InvalidDataMemoryType = 14,
    InvalidDataAllocation = 15,
    AllocationOverlap = 16,
    DataArenaPageCountMismatch = 17,
    InvalidMappingPlan = 18,
    InvalidMappingRangeCount = 19,
    MappingRangeCapacityTooSmall = 20,
    MissingMappingRanges = 21,
    MisalignedMappingRanges = 22,
    MappingRangeSpanOverflow = 23,
    InvalidMappingRange = 24,
    MappingAccountingMismatch = 25,
    WritableExecutableMapping = 26,
    MissingStubMapping = 27,
    MissingDataArenaMapping = 28,
    InvalidStackSwitchPlan = 29,
    InvalidExpectedRsp = 30,
    MissingStackMapping = 31,
    InvalidOriginalRoot = 32,
    InvalidOwnedRoot = 33,
    RootsNotDistinct = 34,
    OwnedRootOutsideTableArena = 35,
    OriginalRootInsideOwnedArena = 36,
    InvalidStorageLayout = 37,
    InvalidStateStorage = 38,
    InvalidCanaryStorage = 39,
    StorageOverlap = 40,
    InvalidCanaryValues = 41,
    InvalidPreflightPlan = 42,
    InvalidPreflightMaterialization = 43,
    InvalidPreflightReadback = 44,
    InvalidTransitionPlan = 45,
    InvalidTransitionMaterialization = 46,
    InvalidTransitionReadback = 47,
    StubByteRangeOverlap = 48,
    StubByteRangeOutsideCodePage = 49,
    StubEntryMisaligned = 50,
    InvalidEventCount = 51,
    TooManyEvents = 52,
    EventCapacityTooSmall = 53,
    MissingEvents = 54,
    MisalignedEvents = 55,
    EventSpanOverflow = 56,
    InvalidEvent = 57,
    UnexpectedEvent = 58,
    MissingPreMarker = 59,
    EarlyPostMarker = 60,
    MissingPostMarker = 61,
    FirmwareCallbackInsideInterval = 62,
    InterruptReenabledInsideInterval = 63,
    RootReadbackMismatch = 64,
    RspEscapedOwnedStack = 65,
    CanaryNotUpdated = 66,
    CanaryValidationFailed = 67,
    Timeout = 68,
    Reset = 69,
    TripleFault = 70,
    ContractLayoutMismatch = 71,
    StackSwitchAllocationOverlap = 72
}

/// <summary>
/// Immutable composite evidence for a future reversible CR3 probe. The plan
/// does not invoke either sibling stub or execute a privileged instruction.
/// </summary>
public readonly struct X64Cr3LoadStubPlan
{
    public const ulong StructureSize = 1016;
    public const ulong RequiredAllocationRecordCount = 2;
    public const ulong CodeAllocationRecordIndex = 0;
    public const ulong DataAllocationRecordIndex = 1;
    public const ulong RequiredCodePageCount = 1;
    public const ulong RequiredStatePageCount = 1;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong RequiredStubEntryAlignment = 16;
    public const ulong RequiredProbeStackHeadroomBytes = 64;
    public const ulong RequiredProbeCallFrameBytes = 40;
    public const ulong RequiredEventCount = 9;
    public const ulong MaximumEventCount = 32;
    public const ulong NoIndex = ~0UL;

    public const bool DefaultEnabled = false;
    public const bool RequiresExplicitOneShotOptIn = true;
    public const bool PerformsLiveCr3Load = false;
    public const bool HostTestsExecuteMovCr3 = false;
    public const bool AllowsFirmwareCallbacksInsideInterval = false;
    public const bool AllowsInterruptReenableInsideInterval = false;

    private readonly X64Cr3ProbePolicy _policy;
    private readonly OS.Boot.BootMemoryRegionRecord _codeAllocation;
    private readonly OS.Boot.BootMemoryRegionRecord _dataAllocation;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingPlanResult
        _identityMappingPlan;
    private readonly OS.Boot.OwnedBootStackSwitchPlanResult
        _stackSwitchPlan;
    private readonly X64Cr3ProbeStorageLayout _storageLayout;
    private readonly ulong _rawFirmwareCr3;
    private readonly OS.Boot.PhysicalAddress _firmwareRootBase;
    private readonly OS.Boot.PhysicalAddress _ownedRoot;
    private readonly OS.Boot.PhysicalAddress _tableArenaStart;
    private readonly OS.Boot.PhysicalAddress _tableArenaEndExclusive;
    private readonly OS.Boot.VirtualAddress _statePageStart;
    private readonly OS.Boot.VirtualAddress _statePageEndExclusive;
    private readonly OS.Boot.VirtualAddress _usableStackStart;
    private readonly OS.Boot.VirtualAddress _usableStackEndExclusive;
    private readonly ulong _physicalAddressMask;
    private readonly ulong _preflightEntryOffset;
    private readonly ulong _preflightByteLength;
    private readonly ulong _transitionEntryOffset;
    private readonly ulong _transitionByteLength;
    private readonly ulong _transitionRuntimeStateAddress;
    private readonly byte _isInitialized;
    private readonly byte _physicalAddressWidth;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;

    private X64Cr3LoadStubPlan(
        X64Cr3ProbePolicy policy,
        OS.Boot.BootMemoryRegionRecord codeAllocation,
        OS.Boot.BootMemoryRegionRecord dataAllocation,
        OS.Kernel.Boot.X64BootIdentityMappingPlanResult
            identityMappingPlan,
        OS.Boot.OwnedBootStackSwitchPlanResult stackSwitchPlan,
        X64Cr3ProbeStorageLayout storageLayout,
        ulong rawFirmwareCr3,
        OS.Boot.PhysicalAddress firmwareRootBase,
        OS.Boot.PhysicalAddress ownedRoot,
        OS.Boot.PhysicalAddress tableArenaStart,
        OS.Boot.PhysicalAddress tableArenaEndExclusive,
        OS.Boot.VirtualAddress statePageStart,
        OS.Boot.VirtualAddress statePageEndExclusive,
        OS.Boot.VirtualAddress usableStackStart,
        OS.Boot.VirtualAddress usableStackEndExclusive,
        ulong physicalAddressMask,
        byte physicalAddressWidth,
        ulong preflightEntryOffset,
        ulong preflightByteLength,
        ulong transitionEntryOffset,
        ulong transitionByteLength,
        ulong transitionRuntimeStateAddress)
    {
        _policy = policy;
        _codeAllocation = codeAllocation;
        _dataAllocation = dataAllocation;
        _identityMappingPlan = identityMappingPlan;
        _stackSwitchPlan = stackSwitchPlan;
        _storageLayout = storageLayout;
        _rawFirmwareCr3 = rawFirmwareCr3;
        _firmwareRootBase = firmwareRootBase;
        _ownedRoot = ownedRoot;
        _tableArenaStart = tableArenaStart;
        _tableArenaEndExclusive = tableArenaEndExclusive;
        _statePageStart = statePageStart;
        _statePageEndExclusive = statePageEndExclusive;
        _usableStackStart = usableStackStart;
        _usableStackEndExclusive = usableStackEndExclusive;
        _physicalAddressMask = physicalAddressMask;
        _preflightEntryOffset = preflightEntryOffset;
        _preflightByteLength = preflightByteLength;
        _transitionEntryOffset = transitionEntryOffset;
        _transitionByteLength = transitionByteLength;
        _transitionRuntimeStateAddress = transitionRuntimeStateAddress;
        _isInitialized = 1;
        _physicalAddressWidth = physicalAddressWidth;
        _reserved1 = 0;
        _reserved2 = 0;
    }

    public bool IsInitialized =>
        _isInitialized == 1
        && _physicalAddressWidth >= 36
        && _physicalAddressWidth <= 52
        && _reserved1 == 0
        && _reserved2 == 0;

    public bool IsValid
    {
        get
        {
            if (!IsInitialized
                || !_policy.IsInitialized
                || _policy.EnabledByDefault
                || !_policy.ExplicitOneShotOptIn
                || _policy.TimeoutTicks == 0
                || !HasValidMappingSummary(_identityMappingPlan)
                || !_stackSwitchPlan.Succeeded
                || !_storageLayout.IsInitialized
                || _rawFirmwareCr3 == 0
                || _physicalAddressMask == 0
                || _physicalAddressMask
                    != ((1UL << _physicalAddressWidth) - 1)
                || !_firmwareRootBase.IsValid
                || !_ownedRoot.IsValid
                || _firmwareRootBase.Equals(_ownedRoot)
                || !_tableArenaStart.IsValid
                || !_tableArenaEndExclusive.IsValid
                || !_statePageStart.IsValid
                || !_statePageEndExclusive.IsValid
                || !_usableStackStart.IsValid
                || !_usableStackEndExclusive.IsValid
                || !_usableStackStart.Equals(
                    _stackSwitchPlan.Plan.UsableStackVirtualStart)
                || !_usableStackEndExclusive.Equals(
                    _stackSwitchPlan.Plan.UsableStackVirtualEndExclusive)
                || _preflightByteLength !=
                    X64PagingModePreflightStubPlan.RequiredByteLength
                || _transitionByteLength !=
                    X64ReversibleCr3TransitionStubPlan
                        .RequiredByteLength
                || !X64ReversibleCr3TransitionStubPlan
                    .IsValidRuntimeStateAddress(
                        _transitionRuntimeStateAddress))
            {
                return false;
            }

            if (_firmwareRootBase
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong firmwareRoot)
                != OS.Boot.AddressValueStatus.Success
                || _ownedRoot.TryGetRawValueForHardwareRegisterBoundary(
                        out ulong ownedRoot)
                    != OS.Boot.AddressValueStatus.Success
                || _usableStackStart
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong usableStackStart)
                    != OS.Boot.AddressValueStatus.Success
                || _usableStackEndExclusive
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong usableStackEnd)
                    != OS.Boot.AddressValueStatus.Success
                || usableStackStart
                    > ~0UL - RequiredProbeStackHeadroomBytes
                || usableStackStart
                        + RequiredProbeStackHeadroomBytes
                    >= usableStackEnd)
            {
                return false;
            }

            if (_identityMappingPlan.TotalTablePageCount == 0
                || _identityMappingPlan.TotalTablePageCount
                    > (~0UL - RequiredStatePageCount)
                || !HasExactAllocation(
                    _codeAllocation,
                    (uint)OS.Boot.UefiMemoryType.LoaderCode,
                    RequiredCodePageCount,
                    out ulong codeStart,
                    out ulong codeEnd)
                || !HasExactAllocation(
                    _dataAllocation,
                    (uint)OS.Boot.UefiMemoryType.LoaderData,
                    _identityMappingPlan.TotalTablePageCount
                        + RequiredStatePageCount,
                    out ulong dataStart,
                    out ulong dataEnd)
                || (codeStart < dataEnd && dataStart < codeEnd)
                || _preflightEntryOffset >=
                    OS.Boot.BootMemoryRegionRecord.PageSize
                || _transitionEntryOffset >=
                    OS.Boot.BootMemoryRegionRecord.PageSize
                || codeStart > ~0UL - _preflightEntryOffset
                || codeStart > ~0UL - _transitionEntryOffset)
            {
                return false;
            }

            OS.Boot.BootMemoryRegionRecord stackCode =
                _stackSwitchPlan.Plan.CodeAllocation;
            OS.Boot.BootMemoryRegionRecord stackData =
                _stackSwitchPlan.Plan.DataAllocation;
            if (!HasExactAllocation(
                    stackCode,
                    (uint)OS.Boot.UefiMemoryType.LoaderCode,
                    OS.Boot.OwnedBootStackSwitchPlan
                        .RequiredCodePageCount,
                    out ulong stackCodeStart,
                    out ulong stackCodeEnd)
                || !HasExactAllocation(
                    stackData,
                    (uint)OS.Boot.UefiMemoryType.LoaderData,
                    stackData.PageCount,
                    out ulong stackDataStart,
                    out ulong stackDataEnd)
                || RangesOverlap(
                    codeStart,
                    codeEnd,
                    stackCodeStart,
                    stackCodeEnd)
                || RangesOverlap(
                    codeStart,
                    codeEnd,
                    stackDataStart,
                    stackDataEnd)
                || RangesOverlap(
                    dataStart,
                    dataEnd,
                    stackCodeStart,
                    stackCodeEnd)
                || RangesOverlap(
                    dataStart,
                    dataEnd,
                    stackDataStart,
                    stackDataEnd))
            {
                return false;
            }

            ulong preflightStart = codeStart + _preflightEntryOffset;
            ulong transitionStart = codeStart + _transitionEntryOffset;
            if (preflightStart > ~0UL - _preflightByteLength
                || transitionStart > ~0UL - _transitionByteLength)
            {
                return false;
            }
            ulong preflightEnd = preflightStart + _preflightByteLength;
            ulong transitionEnd = transitionStart + _transitionByteLength;
            if (preflightEnd > codeEnd
                || transitionEnd > codeEnd
                || (preflightStart < transitionEnd
                    && transitionStart < preflightEnd)
                || (preflightStart
                    & (RequiredStubEntryAlignment - 1)) != 0
                || (transitionStart
                    & (RequiredStubEntryAlignment - 1)) != 0)
            {
                return false;
            }

            if (_tableArenaStart
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong tableStart)
                    != OS.Boot.AddressValueStatus.Success
                || _tableArenaEndExclusive
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong tableEnd)
                    != OS.Boot.AddressValueStatus.Success
                || _statePageStart
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong statePageStart)
                    != OS.Boot.AddressValueStatus.Success
                || _statePageEndExclusive
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong statePageEnd)
                    != OS.Boot.AddressValueStatus.Success
                || tableStart != dataStart
                || ownedRoot != tableStart
                || tableEnd != statePageStart
                || statePageEnd != dataEnd
                || tableEnd <= tableStart
                || tableEnd - tableStart
                    != _identityMappingPlan.TotalTableByteLength
                || statePageEnd - statePageStart
                    != OS.Boot.BootMemoryRegionRecord.PageSize)
            {
                return false;
            }

            if (_storageLayout.Provenance
                    != X64Cr3ProbeStorageProvenance
                        .CallerAttestedOwnedLoaderData
                || _storageLayout.StateAddress
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong stateStart)
                    != OS.Boot.AddressValueStatus.Success
                || _storageLayout.CanaryAddress
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong canaryStart)
                    != OS.Boot.AddressValueStatus.Success
                || stateStart != statePageStart
                || _transitionRuntimeStateAddress != stateStart
                || (stateStart
                    & (X64Cr3ProbeStorageLayout.RequiredAlignment - 1))
                    != 0
                || _storageLayout.StateByteLength
                    != X64Cr3ProbeStorageLayout
                        .RequiredStateRecordByteLength
                || _storageLayout.CanaryByteLength
                    != X64Cr3ProbeStorageLayout.RequiredCanaryByteLength
                || stateStart
                    > ~0UL - _storageLayout.StateByteLength
                || canaryStart
                    > ~0UL - _storageLayout.CanaryByteLength
                || canaryStart
                    < stateStart + _storageLayout.StateByteLength
                || (canaryStart
                    & (X64Cr3ProbeStorageLayout.RequiredAlignment - 1))
                    != 0
                || canaryStart + _storageLayout.CanaryByteLength
                    > statePageEnd
                || _storageLayout.InitialCanaryValue == 0
                || _storageLayout.UpdatedCanaryValue == 0
                || _storageLayout.InitialCanaryValue
                    == _storageLayout.UpdatedCanaryValue)
            {
                return false;
            }

            ulong permitted =
                (_physicalAddressMask & ~0xFFFUL) | 0x18UL;
            return (_rawFirmwareCr3 & ~permitted) == 0
                && (_rawFirmwareCr3
                    & _physicalAddressMask
                    & ~0xFFFUL) == firmwareRoot
                && (firmwareRoot < dataStart
                    || firmwareRoot >= dataEnd)
                && (ownedRoot & 0xFFFUL) == 0
                && (ownedRoot & ~_physicalAddressMask) == 0;
        }
    }

    private static bool HasValidMappingSummary(
        OS.Kernel.Boot.X64BootIdentityMappingPlanResult mapping)
    {
        if (!mapping.Succeeded
            || mapping.SourceCount == 0
            || mapping.ValidatedSourceCount != mapping.SourceCount
            || mapping.SourceCapacity < mapping.SourceCount
            || mapping.OutputRangeCount == 0
            || mapping.DestinationCapacity
                < mapping.OutputRangeCount
            || mapping.DestinationCapacity
                > OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .MaximumDestinationCount
            || mapping.RequiredDestinationCapacity
                != mapping.OutputRangeCount
            || mapping.RequiredPurposeMask
                != OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .RequiredPurposeMask
            || (mapping.ObservedPurposeMask
                & mapping.RequiredPurposeMask)
                != mapping.RequiredPurposeMask
            || mapping.MissingPurposeMask != 0
            || mapping.InvalidSourceIndex
                != OS.Kernel.Boot
                    .X64BootIdentityMappingPlanResult.NoIndex
            || mapping.ConflictingSourceIndex
                != OS.Kernel.Boot
                    .X64BootIdentityMappingPlanResult.NoIndex
            || mapping.GuardSourceIndex
                == OS.Kernel.Boot
                    .X64BootIdentityMappingPlanResult.NoIndex
            || mapping.GuardSourceIndex >= mapping.SourceCount
            || mapping.UnionPageCount == 0
            || mapping.UnionPageCount
                > ~0UL / OS.Boot.BootMemoryRegionRecord.PageSize
            || mapping.UnionByteLength
                != mapping.UnionPageCount
                    * OS.Boot.BootMemoryRegionRecord.PageSize
            || mapping.Pml4PageCount != 1
            || mapping.TotalTablePageCount == 0
            || mapping.TotalTablePageCount
                > ~0UL / OS.Boot.BootMemoryRegionRecord.PageSize
            || mapping.TotalTableByteLength
                != mapping.TotalTablePageCount
                    * OS.Boot.BootMemoryRegionRecord.PageSize
            || mapping.AvailableTablePageCapacity
                < mapping.TotalTablePageCount)
        {
            return false;
        }

        if (mapping.Pml4PageCount > ~0UL - mapping.PdptPageCount)
            return false;
        ulong first = mapping.Pml4PageCount + mapping.PdptPageCount;
        if (first > ~0UL - mapping.PdPageCount)
            return false;
        ulong second = first + mapping.PdPageCount;
        return second <= ~0UL - mapping.PtPageCount
            && second + mapping.PtPageCount
                == mapping.TotalTablePageCount;
    }

    private static bool HasExactAllocation(
        OS.Boot.BootMemoryRegionRecord record,
        uint uefiType,
        ulong pageCount,
        out ulong start,
        out ulong end)
    {
        start = record.PhysicalStart;
        end = record.PhysicalEndExclusive;
        ulong pageSize = OS.Boot.BootMemoryRegionRecord.PageSize;
        if (record.UefiType != uefiType
            || start == 0
            || (start & (pageSize - 1)) != 0
            || record.VirtualStart != start
            || pageCount == 0
            || record.PageCount != pageCount
            || pageCount > ~0UL / pageSize)
        {
            return false;
        }
        ulong bytes = pageCount * pageSize;
        return record.ByteLength == bytes
            && start <= ~0UL - bytes
            && end == start + bytes;
    }

    private static bool RangesOverlap(
        ulong leftStart,
        ulong leftEnd,
        ulong rightStart,
        ulong rightEnd)
    {
        return leftStart < rightEnd && rightStart < leftEnd;
    }

    public X64Cr3ProbePolicy Policy => _policy;

    public OS.Boot.BootMemoryRegionRecord CodeAllocation =>
        _codeAllocation;

    public OS.Boot.BootMemoryRegionRecord DataAllocation =>
        _dataAllocation;

    public OS.Kernel.Boot.X64BootIdentityMappingPlanResult
        IdentityMappingPlan => _identityMappingPlan;

    public OS.Boot.OwnedBootStackSwitchPlanResult StackSwitchPlan =>
        _stackSwitchPlan;

    public X64Cr3ProbeStorageLayout StorageLayout => _storageLayout;

    /// <summary>
    /// Exact firmware CR3 value, including architecturally meaningful PWT/PCD
    /// low bits. This is deliberately not represented as a physical address.
    /// </summary>
    public ulong RawFirmwareCr3 => _rawFirmwareCr3;

    /// <summary>
    /// Address portion derived from <see cref="RawFirmwareCr3"/> only after
    /// applying the preflight-validated physical-address mask.
    /// </summary>
    public OS.Boot.PhysicalAddress FirmwareRootBase => _firmwareRootBase;

    public OS.Boot.PhysicalAddress OwnedRoot => _ownedRoot;

    public OS.Boot.PhysicalAddress TableArenaStart =>
        _tableArenaStart;

    public OS.Boot.PhysicalAddress TableArenaEndExclusive =>
        _tableArenaEndExclusive;

    public OS.Boot.VirtualAddress StatePageStart => _statePageStart;

    public OS.Boot.VirtualAddress StatePageEndExclusive =>
        _statePageEndExclusive;

    public OS.Boot.VirtualAddress UsableStackStart =>
        _usableStackStart;

    public OS.Boot.VirtualAddress UsableStackEndExclusive =>
        _usableStackEndExclusive;

    public ulong PhysicalAddressMask => _physicalAddressMask;

    public byte PhysicalAddressWidth => _physicalAddressWidth;

    public ulong PreflightEntryOffset => _preflightEntryOffset;

    public ulong PreflightByteLength => _preflightByteLength;

    public ulong TransitionEntryOffset => _transitionEntryOffset;

    public ulong TransitionByteLength => _transitionByteLength;

    /// <summary>
    /// Modeled guest address at which the transition state is stored. This
    /// is retained separately from any host observation pointer so copied
    /// plan evidence cannot be rebound to another state record.
    /// </summary>
    public ulong TransitionRuntimeStateAddress =>
        _transitionRuntimeStateAddress;

    /// <summary>
    /// Composes already-audited sibling byte and runtime evidence into a
    /// default-off reversible probe plan. This method validates only
    /// caller-owned POD; it does not invoke either stub or execute CR3.
    /// </summary>
    public static unsafe X64Cr3LoadStubPlanResult Create(
        X64Cr3ProbePolicy policy,
        OS.Boot.BootMemoryRegionRecord* allocationRecords,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingPlanResult mappingPlan,
        OS.Kernel.Boot.X64BootIdentityMappingRange* mappingRanges,
        ulong mappingRangeCount,
        ulong mappingRangeCapacity,
        OS.Boot.OwnedBootStackSwitchPlanResult stackSwitchPlan,
        X64Cr3ProbeStorageLayout storageLayout,
        X64PagingModePreflightResult preflight,
        X64PagingModePreflightStubResult preflightMaterialization,
        X64PagingModePreflightStubResult preflightReadback,
        X64ReversibleCr3TransitionStubPlanResult
            transitionMaterialization,
        X64ReversibleCr3TransitionStubPlanResult transitionReadback,
        X64ReversibleCr3TransitionEvidenceResult transitionEvidence,
        ulong preflightEntryOffset,
        ulong transitionEntryOffset)
    {
        bool transitionContractValid =
            X64ReversibleCr3TransitionStubPlan
                .RequiredByteLength != 0
            && X64ReversibleCr3TransitionStubPlan
                .AuditedInstructionCount != 0
            && X64ReversibleCr3TransitionStubPlan
                .RequiredByteLength
                <= X64ReversibleCr3TransitionStubPlan
                    .MaximumByteCapacity
            && X64ReversibleCr3TransitionStubPlan
                .EntryAlignment == RequiredStubEntryAlignment
            && X64ReversibleCr3TransitionStubPlan
                .ExternalPatchCount == 0
            && !X64ReversibleCr3TransitionStubPlan
                .HasExternalRelocations
            && X64ReversibleCr3TransitionStubPlan.UsesWindowsX64Abi
            && !X64ReversibleCr3TransitionStubPlan.InvokesCallbacks
            && !X64ReversibleCr3TransitionStubPlan
                .TouchesNonvolatileGeneralRegisters
            && !X64ReversibleCr3TransitionStubPlan
                .TouchesXmm6ThroughXmm15
            && X64ReversibleCr3TransitionStubPlan
                .PreservesStackPointer
            && X64ReversibleCr3TransitionStubPlan
                .PreservesShadowSpace
            && !X64ReversibleCr3TransitionStubPlan.SupportsPcide
            && !X64ReversibleCr3TransitionStubPlan
                .SupportsFiveLevelPaging;
        bool transitionMaterializationValid =
            transitionMaterialization.Succeeded
            && transitionMaterialization.RequiredBytes
                == X64ReversibleCr3TransitionStubPlan
                    .RequiredByteLength
            && transitionMaterialization.BytesWritten
                == transitionMaterialization.RequiredBytes
            && transitionMaterialization.InvalidByteIndex
                == X64ReversibleCr3TransitionStubPlanResult.NoIndex
            && transitionMaterialization.ExternalPatchCount == 0
            && transitionMaterialization.InstructionCount
                == X64ReversibleCr3TransitionStubPlan
                    .AuditedInstructionCount
            && !transitionMaterialization.HasExternalRelocations;
        bool transitionReadbackValid =
            transitionReadback.Succeeded
            && transitionReadback.RequiredBytes
                == X64ReversibleCr3TransitionStubPlan
                    .RequiredByteLength
            && transitionReadback.BytesWritten
                == transitionReadback.RequiredBytes
            && transitionReadback.SourceCount
                == X64ReversibleCr3TransitionStubPlan
                    .RequiredByteLength
            && transitionReadback.InvalidByteIndex
                == X64ReversibleCr3TransitionStubPlanResult.NoIndex
            && transitionReadback.ExternalPatchCount == 0
            && transitionReadback.InstructionCount
                == X64ReversibleCr3TransitionStubPlan
                    .AuditedInstructionCount
            && !transitionReadback.HasExternalRelocations;
        OS.Boot.AddressValueStatus storageStateAddressStatus =
            storageLayout.StateAddress
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong storageStateAddress);
        bool transitionEvidenceValid =
            storageStateAddressStatus
                == OS.Boot.AddressValueStatus.Success
            && transitionEvidence.Succeeded
            && transitionEvidence.State.IsInitialized
            && transitionEvidence.RuntimeStatus
                == X64ReversibleCr3TransitionRuntimeStatus.Success
            && transitionEvidence.Checkpoint
                == X64ReversibleCr3TransitionCheckpoint.Returned
            && transitionEvidence.TerminalKind
                == X64ReversibleCr3TransitionTerminalKind.Returned
            && transitionEvidence.ExpectedValue == 0
            && transitionEvidence.ObservedValue == 0
            && transitionEvidence.RuntimeStateAddress
                == storageStateAddress;

        X64Cr3LoadStubPlanResult composed =
            X64Cr3LoadStubPlanComposer.CreateCore(
                policy,
                allocationRecords,
                allocationRecordCount,
                allocationRecordCapacity,
                mappingPlan,
                mappingRanges,
                mappingRangeCount,
                mappingRangeCapacity,
                stackSwitchPlan,
                storageLayout,
                preflight,
                preflightMaterialization,
                preflightReadback,
                transitionContractValid,
                transitionMaterializationValid,
                transitionReadbackValid,
                transitionEvidenceValid,
                transitionEvidence.RuntimeStateAddress,
                preflightEntryOffset,
                transitionEntryOffset,
                X64ReversibleCr3TransitionStubPlan
                    .RequiredByteLength);
        if (!composed.Succeeded)
            return composed;

        X64Cr3LoadStubPlan plan = composed.Plan;
        ulong codeStart = plan.CodeAllocation.PhysicalStart;
        ulong preflightEntry = codeStart + preflightEntryOffset;
        ulong transitionEntry = codeStart + transitionEntryOffset;
        if (preflightMaterialization.EntryAddress != preflightEntry
            || preflightMaterialization.BufferCapacity
                != OS.Boot.BootMemoryRegionRecord.PageSize)
        {
            return Failure(
                X64Cr3LoadStubPlanStatus
                    .InvalidPreflightMaterialization);
        }
        if (preflightReadback.EntryAddress != preflightEntry
            || preflightReadback.BufferCapacity
                != OS.Boot.BootMemoryRegionRecord.PageSize)
        {
            return Failure(
                X64Cr3LoadStubPlanStatus.InvalidPreflightReadback);
        }
        if (transitionMaterialization.EntryAddress != transitionEntry)
        {
            return Failure(
                X64Cr3LoadStubPlanStatus
                    .InvalidTransitionMaterialization);
        }
        ulong transitionSourceCapacity =
            OS.Boot.BootMemoryRegionRecord.PageSize
            - transitionEntryOffset;
        if (transitionMaterialization.SourceCapacity
                != transitionSourceCapacity
            || transitionMaterialization.SourceCount
                != X64ReversibleCr3TransitionStubPlan
                    .RequiredByteLength)
        {
            return Failure(
                X64Cr3LoadStubPlanStatus
                    .InvalidTransitionMaterialization);
        }
        if (transitionReadback.EntryAddress != transitionEntry
            || transitionReadback.SourceCapacity
                != transitionSourceCapacity
            || transitionReadback.SourceCount
                != X64ReversibleCr3TransitionStubPlan
                    .RequiredByteLength)
        {
            return Failure(
                X64Cr3LoadStubPlanStatus.InvalidTransitionReadback);
        }

        X64ReversibleCr3TransitionRuntimeState transitionState =
            transitionEvidence.State;
        if (plan.OwnedRoot.TryGetRawValueForHardwareRegisterBoundary(
                out ulong ownedRoot)
                != OS.Boot.AddressValueStatus.Success
            || plan.StackSwitchPlan.Plan.UsableStackVirtualStart
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong stackStart)
                != OS.Boot.AddressValueStatus.Success
            || plan.StackSwitchPlan.Plan.UsableStackVirtualEndExclusive
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong stackEnd)
                != OS.Boot.AddressValueStatus.Success
            || plan.StorageLayout.CanaryAddress
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong canaryAddress)
                != OS.Boot.AddressValueStatus.Success)
        {
            return Failure(X64Cr3LoadStubPlanStatus.InvalidTransitionPlan);
        }

        ulong rawCr4 = preflight.RawCr4;
        ulong originalRflags = preflight.Rflags;
        if (transitionState.OwnedCr3Raw != ownedRoot
            || transitionState.StackStart != stackStart
            || transitionState.StackEndExclusive != stackEnd
            || transitionState.CanaryAddress != canaryAddress
            || transitionState.ExpectedCanary
                != plan.StorageLayout.InitialCanaryValue
            || transitionState.ReplacementCanary
                != plan.StorageLayout.UpdatedCanaryValue
            || transitionState.OriginalRflags != originalRflags
            || transitionState.OriginalCr3Raw != plan.RawFirmwareCr3
            || transitionState.OriginalCr4Raw != rawCr4
            || transitionState.ClearedCr4Readback
                != (rawCr4
                    & ~X64ReversibleCr3TransitionStubPlan
                        .Cr4PageGlobalEnableBit)
            || transitionState.OwnedCr3Readback != ownedRoot
            || transitionState.CanaryBefore
                != plan.StorageLayout.InitialCanaryValue
            || transitionState.CanaryAfter
                != plan.StorageLayout.UpdatedCanaryValue
            || transitionState.FirmwareCr3Readback
                != plan.RawFirmwareCr3
            || transitionState.RestoredCr4Readback != rawCr4
            || (originalRflags
                & X64ReversibleCr3TransitionStubPlan
                    .RflagsArchitecturalReservedOneBit)
                != X64ReversibleCr3TransitionStubPlan
                    .RflagsArchitecturalReservedOneBit
            || (transitionState.CliRflagsReadback
                & (X64ReversibleCr3TransitionStubPlan
                    .RflagsInterruptEnableBit
                    | X64ReversibleCr3TransitionStubPlan
                        .RflagsArchitecturalReservedOneBit))
                != X64ReversibleCr3TransitionStubPlan
                    .RflagsArchitecturalReservedOneBit
            || transitionState.RestoredRflagsReadback
                != originalRflags
            || transitionState.EntryRsp != transitionState.ExitRsp
            || stackStart
                > ~0UL - RequiredProbeStackHeadroomBytes
            || transitionState.EntryRsp
                < stackStart + RequiredProbeStackHeadroomBytes
            || transitionState.EntryRsp > ~0UL - RequiredProbeCallFrameBytes
            || transitionState.EntryRsp + RequiredProbeCallFrameBytes
                > stackEnd
            || (transitionState.EntryRsp & 15UL) != 8UL
            || transitionState.PostRestoreCanaryReadback
                != plan.StorageLayout.UpdatedCanaryValue
            || transitionState.FirmwareCallbackCount != 0
            || transitionState.RuntimeStatus
                != X64ReversibleCr3TransitionRuntimeStatus.Success
            || transitionState.Checkpoint
                != X64ReversibleCr3TransitionCheckpoint.Returned)
        {
            return Failure(X64Cr3LoadStubPlanStatus.InvalidTransitionPlan);
        }

        return composed;

        X64Cr3LoadStubPlanResult Failure(
            X64Cr3LoadStubPlanStatus status)
        {
            return X64Cr3LoadStubPlanResult.Create(
                status,
                OS.Boot.AddressValueStatus.Success,
                mappingPlan.Status,
                stackSwitchPlan.Status,
                allocationRecordCount,
                allocationRecordCapacity,
                mappingRangeCount,
                mappingRangeCapacity,
                NoIndex,
                NoIndex,
                default);
        }
    }

    internal static X64Cr3LoadStubPlan CreateValidated(
        X64Cr3ProbePolicy policy,
        OS.Boot.BootMemoryRegionRecord codeAllocation,
        OS.Boot.BootMemoryRegionRecord dataAllocation,
        OS.Kernel.Boot.X64BootIdentityMappingPlanResult
            identityMappingPlan,
        OS.Boot.OwnedBootStackSwitchPlanResult stackSwitchPlan,
        X64Cr3ProbeStorageLayout storageLayout,
        ulong rawFirmwareCr3,
        OS.Boot.PhysicalAddress firmwareRootBase,
        OS.Boot.PhysicalAddress ownedRoot,
        OS.Boot.PhysicalAddress tableArenaStart,
        OS.Boot.PhysicalAddress tableArenaEndExclusive,
        OS.Boot.VirtualAddress statePageStart,
        OS.Boot.VirtualAddress statePageEndExclusive,
        OS.Boot.VirtualAddress usableStackStart,
        OS.Boot.VirtualAddress usableStackEndExclusive,
        ulong physicalAddressMask,
        byte physicalAddressWidth,
        ulong preflightEntryOffset,
        ulong preflightByteLength,
        ulong transitionEntryOffset,
        ulong transitionByteLength,
        ulong transitionRuntimeStateAddress)
    {
        return new X64Cr3LoadStubPlan(
            policy,
            codeAllocation,
            dataAllocation,
            identityMappingPlan,
            stackSwitchPlan,
            storageLayout,
            rawFirmwareCr3,
            firmwareRootBase,
            ownedRoot,
            tableArenaStart,
            tableArenaEndExclusive,
            statePageStart,
            statePageEndExclusive,
            usableStackStart,
            usableStackEndExclusive,
            physicalAddressMask,
            physicalAddressWidth,
            preflightEntryOffset,
            preflightByteLength,
            transitionEntryOffset,
            transitionByteLength,
            transitionRuntimeStateAddress);
    }

    public unsafe X64Cr3ProbeAuditResult Audit(
        X64Cr3ProbeEvent* events,
        ulong eventCount,
        ulong eventCapacity)
    {
        return X64Cr3ProbeTranscriptAuditor.Audit(
            this,
            events,
            eventCount,
            eventCapacity);
    }
}

internal static unsafe class X64Cr3LoadStubPlanComposer
{
    private const ulong MaximumUnsignedLong = ~0UL;
    private const ulong PageSize = OS.Boot.BootMemoryRegionRecord.PageSize;
    private const ulong Cr3PageMask = ~0xFFFUL;
    private const ulong Cr3PwtPcdMask = 0x18UL;

    internal static X64Cr3LoadStubPlanResult CreateCore(
        X64Cr3ProbePolicy policy,
        OS.Boot.BootMemoryRegionRecord* allocationRecords,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        OS.Kernel.Boot.X64BootIdentityMappingPlanResult mappingPlan,
        OS.Kernel.Boot.X64BootIdentityMappingRange* mappingRanges,
        ulong mappingRangeCount,
        ulong mappingRangeCapacity,
        OS.Boot.OwnedBootStackSwitchPlanResult stackSwitchPlan,
        X64Cr3ProbeStorageLayout storageLayout,
        X64PagingModePreflightResult preflight,
        X64PagingModePreflightStubResult preflightMaterialization,
        X64PagingModePreflightStubResult preflightReadback,
        bool transitionPlanValid,
        bool transitionMaterializationValid,
        bool transitionReadbackValid,
        bool transitionEvidenceValid,
        ulong transitionRuntimeStateAddress,
        ulong preflightEntryOffset,
        ulong transitionEntryOffset,
        ulong transitionByteLength)
    {
        OS.Kernel.Boot.X64BootIdentityMappingStatus mappingStatus =
            mappingPlan.Status;
        OS.Boot.OwnedBootStackSwitchStatus stackStatus =
            stackSwitchPlan.Status;

        if ((ulong)sizeof(X64Cr3ProbePolicy)
                != X64Cr3ProbePolicy.StructureSize
            || (ulong)sizeof(X64Cr3ProbeStorageLayout)
                != X64Cr3ProbeStorageLayout.StructureSize
            || (ulong)sizeof(X64Cr3ProbeEvent)
                != X64Cr3ProbeEvent.StructureSize
            || (ulong)sizeof(X64Cr3LoadStubPlan)
                != X64Cr3LoadStubPlan.StructureSize
            || (ulong)sizeof(X64Cr3LoadStubPlanResult)
                != X64Cr3LoadStubPlanResult.StructureSize
            || (ulong)sizeof(X64Cr3ProbeAuditResult)
                != X64Cr3ProbeAuditResult.StructureSize)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.ContractLayoutMismatch);
        }

        if (!policy.IsInitialized)
            return Fail(X64Cr3LoadStubPlanStatus.InvalidPolicy);
        if (policy.EnabledByDefault)
            return Fail(X64Cr3LoadStubPlanStatus.EnabledByDefault);
        if (!policy.ExplicitOneShotOptIn)
            return Fail(X64Cr3LoadStubPlanStatus.ExplicitOptInRequired);
        if (policy.TimeoutTicks == 0)
            return Fail(X64Cr3LoadStubPlanStatus.InvalidTimeout);

        if (allocationRecordCount
            != X64Cr3LoadStubPlan.RequiredAllocationRecordCount)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidAllocationRecordCount);
        }
        if (allocationRecordCapacity
            > X64Cr3LoadStubPlan.RequiredAllocationRecordCount)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.TooManyAllocationRecords);
        }
        if (allocationRecordCapacity < allocationRecordCount)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus
                    .AllocationRecordCapacityTooSmall);
        }
        if (allocationRecords == null)
            return Fail(X64Cr3LoadStubPlanStatus.MissingAllocationRecords);
        ulong allocationAddress = (ulong)allocationRecords;
        if ((allocationAddress
            & (X64Cr3LoadStubPlan.RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.MisalignedAllocationRecords);
        }
        if (!TryGetSpanEnd(
            allocationAddress,
            allocationRecordCapacity,
            (ulong)sizeof(OS.Boot.BootMemoryRegionRecord),
            out ulong ignoredAllocationEnd))
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.AllocationRecordSpanOverflow);
        }

        OS.Boot.BootMemoryRegionRecord code =
            allocationRecords[X64Cr3LoadStubPlan.CodeAllocationRecordIndex];
        OS.Boot.BootMemoryRegionRecord data =
            allocationRecords[X64Cr3LoadStubPlan.DataAllocationRecordIndex];

        if (code.UefiType != (uint)OS.Boot.UefiMemoryType.LoaderCode)
            return Fail(X64Cr3LoadStubPlanStatus.InvalidCodeMemoryType);
        if (!TryValidateAllocation(
            code,
            X64Cr3LoadStubPlan.RequiredCodePageCount,
            out ulong codeStart,
            out ulong codeEnd))
        {
            return Fail(X64Cr3LoadStubPlanStatus.InvalidCodeAllocation);
        }

        if (data.UefiType != (uint)OS.Boot.UefiMemoryType.LoaderData)
            return Fail(X64Cr3LoadStubPlanStatus.InvalidDataMemoryType);
        if (!mappingPlan.Succeeded)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidMappingPlan,
                mappingStatus,
                stackStatus);
        }
        if (mappingPlan.ValidatedSourceCount != mappingPlan.SourceCount
            || mappingPlan.RequiredPurposeMask
                != OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .RequiredPurposeMask
            || (mappingPlan.ObservedPurposeMask
                & mappingPlan.RequiredPurposeMask)
                != mappingPlan.RequiredPurposeMask
            || mappingPlan.MissingPurposeMask != 0
            || mappingPlan.InvalidSourceIndex
                != OS.Kernel.Boot
                    .X64BootIdentityMappingPlanResult.NoIndex
            || mappingPlan.ConflictingSourceIndex
                != OS.Kernel.Boot
                    .X64BootIdentityMappingPlanResult.NoIndex
            || mappingPlan.UnionPageCount == 0
            || mappingPlan.UnionPageCount
                > MaximumUnsignedLong / PageSize
            || mappingPlan.UnionByteLength
                != mappingPlan.UnionPageCount * PageSize
            || mappingPlan.TotalTablePageCount == 0
            || mappingPlan.TotalTablePageCount
                > MaximumUnsignedLong / PageSize
            || mappingPlan.TotalTableByteLength
                != mappingPlan.TotalTablePageCount * PageSize
            || mappingPlan.Pml4PageCount != 1
            || !TryAddTablePages(
                mappingPlan.Pml4PageCount,
                mappingPlan.PdptPageCount,
                mappingPlan.PdPageCount,
                mappingPlan.PtPageCount,
                out ulong summedTablePages)
            || summedTablePages
                != mappingPlan.TotalTablePageCount
            || mappingPlan.AvailableTablePageCapacity
                < mappingPlan.TotalTablePageCount)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidMappingPlan,
                mappingStatus,
                stackStatus);
        }
        if (mappingPlan.TotalTablePageCount == 0
            || mappingPlan.TotalTablePageCount
                > MaximumUnsignedLong
                    - X64Cr3LoadStubPlan.RequiredStatePageCount)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.DataArenaPageCountMismatch,
                mappingStatus,
                stackStatus);
        }
        ulong requiredDataPages =
            mappingPlan.TotalTablePageCount
            + X64Cr3LoadStubPlan.RequiredStatePageCount;
        if (!TryValidateAllocation(
            data,
            requiredDataPages,
            out ulong dataStart,
            out ulong dataEnd))
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidDataAllocation,
                mappingStatus,
                stackStatus);
        }
        if (RangesOverlap(codeStart, codeEnd, dataStart, dataEnd))
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.AllocationOverlap,
                mappingStatus,
                stackStatus);
        }

        if (mappingRangeCount != mappingPlan.OutputRangeCount
            || mappingRangeCount == 0)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidMappingRangeCount,
                mappingStatus,
                stackStatus);
        }
        if (mappingRangeCapacity < mappingRangeCount)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus
                    .MappingRangeCapacityTooSmall,
                mappingStatus,
                stackStatus);
        }
        if (mappingRangeCapacity != mappingPlan.DestinationCapacity
            || mappingRangeCapacity
                > OS.Kernel.Boot.X64BootIdentityMappingPlan
                    .MaximumDestinationCount)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidMappingRangeCount,
                mappingStatus,
                stackStatus);
        }
        if (mappingRanges == null)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.MissingMappingRanges,
                mappingStatus,
                stackStatus);
        }
        ulong mappingAddress = (ulong)mappingRanges;
        if ((mappingAddress
            & (X64Cr3LoadStubPlan.RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.MisalignedMappingRanges,
                mappingStatus,
                stackStatus);
        }
        if (!TryGetSpanEnd(
            mappingAddress,
            mappingRangeCapacity,
            OS.Kernel.Boot.X64BootIdentityMappingRange.StructureSize,
            out ulong ignoredMappingEnd))
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.MappingRangeSpanOverflow,
                mappingStatus,
                stackStatus);
        }

        ulong unionPages = 0;
        ulong unionBytes = 0;
        ulong previousEnd = 0;
        bool hasCodeMapping = false;
        bool hasDataMapping = false;
        bool hasStackMapping = false;
        ulong additionalRequiredMask =
            1UL << (byte)OS.Kernel.Boot
                .X64BootIdentityMappingPurpose.AdditionalRequired;
        ulong ownedStackMask =
            1UL << (byte)OS.Kernel.Boot
                .X64BootIdentityMappingPurpose.OwnedStack;

        if (!stackSwitchPlan.Succeeded)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidStackSwitchPlan,
                mappingStatus,
                stackStatus);
        }
        OS.Boot.BootMemoryRegionRecord stackCodeAllocation =
            stackSwitchPlan.Plan.CodeAllocation;
        OS.Boot.BootMemoryRegionRecord stackDataAllocation =
            stackSwitchPlan.Plan.DataAllocation;
        if (stackCodeAllocation.UefiType
                != (uint)OS.Boot.UefiMemoryType.LoaderCode
            || stackDataAllocation.UefiType
                != (uint)OS.Boot.UefiMemoryType.LoaderData
            || !TryValidateAllocation(
                stackCodeAllocation,
                OS.Boot.OwnedBootStackSwitchPlan.RequiredCodePageCount,
                out ulong stackCodeStart,
                out ulong stackCodeEnd)
            || !TryValidateAllocation(
                stackDataAllocation,
                stackDataAllocation.PageCount,
                out ulong stackDataStart,
                out ulong stackDataEnd))
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidStackSwitchPlan,
                mappingStatus,
                stackStatus);
        }
        if (RangesOverlap(
                codeStart,
                codeEnd,
                stackCodeStart,
                stackCodeEnd)
            || RangesOverlap(
                codeStart,
                codeEnd,
                stackDataStart,
                stackDataEnd)
            || RangesOverlap(
                dataStart,
                dataEnd,
                stackCodeStart,
                stackCodeEnd)
            || RangesOverlap(
                dataStart,
                dataEnd,
                stackDataStart,
                stackDataEnd))
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.StackSwitchAllocationOverlap,
                mappingStatus,
                stackStatus);
        }
        OS.Boot.VirtualAddress expectedRspAddress =
            stackSwitchPlan.Plan.ExpectedContinuationStackPointer;
        if (!TryGetVirtualRaw(expectedRspAddress, out ulong expectedRsp)
            || !TryGetVirtualRaw(
                stackSwitchPlan.Plan.UsableStackVirtualStart,
                out ulong usableStackStart)
            || !TryGetVirtualRaw(
                stackSwitchPlan.Plan.UsableStackVirtualEndExclusive,
                out ulong usableStackEnd)
            || expectedRsp < usableStackStart
            || expectedRsp >= usableStackEnd)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidExpectedRsp,
                mappingStatus,
                stackStatus);
        }

        for (ulong index = 0; index < mappingRangeCount; index++)
        {
            OS.Kernel.Boot.X64BootIdentityMappingRange range =
                mappingRanges[index];
            if (!TryValidateRange(
                range,
                previousEnd,
                out ulong rangeStart,
                out ulong rangeEnd))
            {
                return Fail(
                    X64Cr3LoadStubPlanStatus.InvalidMappingRange,
                    mappingStatus,
                    stackStatus,
                    invalidIndex: index);
            }
            previousEnd = rangeEnd;

            if (unionPages > MaximumUnsignedLong - range.PageCount
                || unionBytes > MaximumUnsignedLong - range.ByteLength)
            {
                return Fail(
                    X64Cr3LoadStubPlanStatus.MappingAccountingMismatch,
                    mappingStatus,
                    stackStatus,
                    invalidIndex: index);
            }
            unionPages += range.PageCount;
            unionBytes += range.ByteLength;

            byte protection = (byte)range.Protection;
            if ((protection
                & ((byte)OS.Kernel.Memory.PageProtection.Write
                    | (byte)OS.Kernel.Memory.PageProtection.Execute))
                == ((byte)OS.Kernel.Memory.PageProtection.Write
                    | (byte)OS.Kernel.Memory.PageProtection.Execute))
            {
                return Fail(
                    X64Cr3LoadStubPlanStatus.WritableExecutableMapping,
                    mappingStatus,
                    stackStatus,
                    invalidIndex: index);
            }

            bool hasAdditional =
                (range.ContributingPurposeMask & additionalRequiredMask)
                    != 0;
            bool hasOwnedStack =
                (range.ContributingPurposeMask & ownedStackMask) != 0;
            if (hasAdditional
                && Covers(rangeStart, rangeEnd, codeStart, codeEnd)
                && range.Protection
                    == (OS.Kernel.Memory.PageProtection.Read
                        | OS.Kernel.Memory.PageProtection.Execute)
                && range.CachePolicy
                    == OS.Kernel.Memory.PageCachePolicy.WriteBack)
            {
                hasCodeMapping = true;
            }
            if (hasAdditional
                && Covers(rangeStart, rangeEnd, dataStart, dataEnd)
                && range.Protection
                    == (OS.Kernel.Memory.PageProtection.Read
                        | OS.Kernel.Memory.PageProtection.Write)
                && range.CachePolicy
                    == OS.Kernel.Memory.PageCachePolicy.WriteBack)
            {
                hasDataMapping = true;
            }
            if (hasOwnedStack
                && Covers(
                    rangeStart,
                    rangeEnd,
                    usableStackStart,
                    usableStackEnd)
                && expectedRsp >= rangeStart
                && expectedRsp < rangeEnd
                && (protection
                    & (byte)OS.Kernel.Memory.PageProtection.Write) != 0
                && (protection
                    & (byte)OS.Kernel.Memory.PageProtection.Execute) == 0
                && range.CachePolicy
                    == OS.Kernel.Memory.PageCachePolicy.WriteBack)
            {
                hasStackMapping = true;
            }
        }

        if (unionPages != mappingPlan.UnionPageCount
            || unionBytes != mappingPlan.UnionByteLength)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.MappingAccountingMismatch,
                mappingStatus,
                stackStatus);
        }
        if (!hasCodeMapping)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.MissingStubMapping,
                mappingStatus,
                stackStatus);
        }
        if (!hasDataMapping)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.MissingDataArenaMapping,
                mappingStatus,
                stackStatus);
        }
        if (!hasStackMapping)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.MissingStackMapping,
                mappingStatus,
                stackStatus);
        }

        if (!preflight.Succeeded
            || preflight.PhysicalAddressMask == 0
            || preflight.PhysicalAddressWidth < 36
            || preflight.PhysicalAddressWidth > 52
            || preflight.PhysicalAddressMask
                != ((1UL << preflight.PhysicalAddressWidth) - 1)
            || (preflight.Cr0
                & X64PagingModePreflightStubPlan.Cr0PagingBit) == 0
            || (preflight.Cr0
                & X64PagingModePreflightStubPlan
                    .Cr0WriteProtectBit) == 0
            || (preflight.RawCr4
                & X64PagingModePreflightStubPlan.Cr4PaeBit) == 0
            || (preflight.RawCr4
                & X64PagingModePreflightStubPlan.Cr4La57Bit) != 0
            || (preflight.RawCr4
                & X64PagingModePreflightStubPlan.Cr4PcideBit) != 0
            || (preflight.Ia32Efer
                & X64PagingModePreflightStubPlan
                    .EferLongModeEnableBit) == 0
            || (preflight.Ia32Efer
                & X64PagingModePreflightStubPlan
                    .EferLongModeActiveBit) == 0
            || (preflight.Ia32Efer
                & X64PagingModePreflightStubPlan
                    .EferNoExecuteEnableBit) == 0)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidPreflightPlan,
                mappingStatus,
                stackStatus);
        }
        ulong physicalAddressMask = preflight.PhysicalAddressMask;
        ulong rawFirmwareCr3 = preflight.RawFirmwareCr3;
        ulong permittedFirmwareCr3Bits =
            (physicalAddressMask & Cr3PageMask) | Cr3PwtPcdMask;
        if (rawFirmwareCr3 == 0
            || (rawFirmwareCr3 & ~permittedFirmwareCr3Bits) != 0)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidOriginalRoot,
                mappingStatus,
                stackStatus);
        }
        ulong firmwareRootRaw =
            rawFirmwareCr3 & physicalAddressMask & Cr3PageMask;
        if (firmwareRootRaw == 0
            || OS.Boot.PhysicalAddress
                    .TryCreateAlignedFromHardwareRegisterBoundary(
                        firmwareRootRaw,
                        PageSize,
                        out OS.Boot.PhysicalAddress firmwareRootBase)
                != OS.Boot.AddressValueStatus.Success)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidOriginalRoot,
                mappingStatus,
                stackStatus);
        }

        if (!TryGetPhysicalRaw(preflight.OwnedRoot, out ulong ownedRoot)
            || (ownedRoot & (PageSize - 1)) != 0
            || (ownedRoot & ~physicalAddressMask) != 0)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidOwnedRoot,
                mappingStatus,
                stackStatus);
        }
        if (firmwareRootRaw == ownedRoot)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.RootsNotDistinct,
                mappingStatus,
                stackStatus);
        }
        if (!TryGetPhysicalRaw(
                preflight.TableArenaStart,
                out ulong tableStart)
            || !TryGetPhysicalRaw(
                preflight.TableArenaEndExclusive,
                out ulong tableEnd)
            || tableStart != dataStart
            || tableEnd <= tableStart
            || tableEnd - tableStart
                != mappingPlan.TotalTableByteLength
            || ownedRoot != tableStart)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.OwnedRootOutsideTableArena,
                mappingStatus,
                stackStatus);
        }
        if (firmwareRootRaw >= dataStart && firmwareRootRaw < dataEnd)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.OriginalRootInsideOwnedArena,
                mappingStatus,
                stackStatus);
        }

        ulong statePageStartRaw = tableEnd;
        if (statePageStartRaw > MaximumUnsignedLong - PageSize
            || statePageStartRaw + PageSize != dataEnd
            || OS.Boot.VirtualAddress.TryCreateAlignedFromUefiBoundary(
                    statePageStartRaw,
                    PageSize,
                    out OS.Boot.VirtualAddress statePageStart)
                != OS.Boot.AddressValueStatus.Success
            || OS.Boot.VirtualAddress.TryCreateAlignedFromUefiBoundary(
                    dataEnd,
                    PageSize,
                    out OS.Boot.VirtualAddress statePageEnd)
                != OS.Boot.AddressValueStatus.Success)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidStateStorage,
                mappingStatus,
                stackStatus);
        }
        if (!storageLayout.IsInitialized
            || storageLayout.Provenance
                != X64Cr3ProbeStorageProvenance
                    .CallerAttestedOwnedLoaderData)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidStorageLayout,
                mappingStatus,
                stackStatus);
        }
        if (!TryGetVirtualRaw(
                storageLayout.StateAddress,
                out ulong stateStart)
            || storageLayout.StateByteLength
                != X64Cr3ProbeStorageLayout.RequiredStateRecordByteLength
            || stateStart != statePageStartRaw
            || !TryGetRangeEnd(
                stateStart,
                storageLayout.StateByteLength,
                out ulong stateEnd)
            || stateEnd > dataEnd)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidStateStorage,
                mappingStatus,
                stackStatus);
        }
        if (!TryGetVirtualRaw(
                storageLayout.CanaryAddress,
                out ulong canaryStart)
            || storageLayout.CanaryByteLength
                != X64Cr3ProbeStorageLayout.RequiredCanaryByteLength
            || (canaryStart
                & (X64Cr3ProbeStorageLayout.RequiredAlignment - 1)) != 0
            || !TryGetRangeEnd(
                canaryStart,
                storageLayout.CanaryByteLength,
                out ulong canaryEnd)
            || canaryStart < statePageStartRaw
            || canaryEnd > dataEnd)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidCanaryStorage,
                mappingStatus,
                stackStatus);
        }
        if (RangesOverlap(stateStart, stateEnd, canaryStart, canaryEnd))
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.StorageOverlap,
                mappingStatus,
                stackStatus);
        }
        if (storageLayout.InitialCanaryValue == 0
            || storageLayout.UpdatedCanaryValue == 0
            || storageLayout.InitialCanaryValue
                == storageLayout.UpdatedCanaryValue)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidCanaryValues,
                mappingStatus,
                stackStatus);
        }

        if (!preflightMaterialization.Succeeded
            || preflightMaterialization.Operation
                != X64PagingModePreflightOperation.Materialize
            || preflightMaterialization.EntryOffset
                != preflightEntryOffset)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidPreflightMaterialization,
                mappingStatus,
                stackStatus);
        }
        if (!preflightReadback.Succeeded
            || preflightReadback.Operation
                != X64PagingModePreflightOperation.Validate
            || preflightReadback.EntryOffset != preflightEntryOffset)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidPreflightReadback,
                mappingStatus,
                stackStatus);
        }
        if (!transitionPlanValid || !transitionEvidenceValid)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidTransitionPlan,
                mappingStatus,
                stackStatus);
        }
        if (!transitionMaterializationValid)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidTransitionMaterialization,
                mappingStatus,
                stackStatus);
        }
        if (!transitionReadbackValid)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidTransitionReadback,
                mappingStatus,
                stackStatus);
        }

        ulong preflightByteLength =
            X64PagingModePreflightStubPlan.RequiredByteLength;
        if (!TryValidateStubRange(
                codeStart,
                codeEnd,
                preflightEntryOffset,
                preflightByteLength,
                out ulong preflightStart,
                out ulong preflightEnd)
            || !TryValidateStubRange(
                codeStart,
                codeEnd,
                transitionEntryOffset,
                transitionByteLength,
                out ulong transitionStart,
                out ulong transitionEnd))
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.StubByteRangeOutsideCodePage,
                mappingStatus,
                stackStatus);
        }
        if ((preflightStart
                & (X64Cr3LoadStubPlan.RequiredStubEntryAlignment - 1))
                != 0
            || (transitionStart
                & (X64Cr3LoadStubPlan.RequiredStubEntryAlignment - 1))
                != 0)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.StubEntryMisaligned,
                mappingStatus,
                stackStatus);
        }
        if (RangesOverlap(
            preflightStart,
            preflightEnd,
            transitionStart,
            transitionEnd))
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.StubByteRangeOverlap,
                mappingStatus,
                stackStatus);
        }

        X64Cr3LoadStubPlan plan = X64Cr3LoadStubPlan.CreateValidated(
            policy,
            code,
            data,
            mappingPlan,
            stackSwitchPlan,
            storageLayout,
            rawFirmwareCr3,
            firmwareRootBase,
            preflight.OwnedRoot,
            preflight.TableArenaStart,
            preflight.TableArenaEndExclusive,
            statePageStart,
            statePageEnd,
            stackSwitchPlan.Plan.UsableStackVirtualStart,
            stackSwitchPlan.Plan.UsableStackVirtualEndExclusive,
            physicalAddressMask,
            preflight.PhysicalAddressWidth,
            preflightEntryOffset,
            preflightByteLength,
            transitionEntryOffset,
            transitionByteLength,
            transitionRuntimeStateAddress);

        return X64Cr3LoadStubPlanResult.Create(
            X64Cr3LoadStubPlanStatus.Success,
            OS.Boot.AddressValueStatus.Success,
            mappingStatus,
            stackStatus,
            allocationRecordCount,
            allocationRecordCapacity,
            mappingRangeCount,
            mappingRangeCapacity,
            X64Cr3LoadStubPlan.NoIndex,
            X64Cr3LoadStubPlan.NoIndex,
            plan);

        X64Cr3LoadStubPlanResult Fail(
            X64Cr3LoadStubPlanStatus status,
            OS.Kernel.Boot.X64BootIdentityMappingStatus
                failureMappingStatus =
                    OS.Kernel.Boot.X64BootIdentityMappingStatus
                        .Uninitialized,
            OS.Boot.OwnedBootStackSwitchStatus failureStackStatus =
                OS.Boot.OwnedBootStackSwitchStatus.Uninitialized,
            OS.Boot.AddressValueStatus addressStatus =
                OS.Boot.AddressValueStatus.Success,
            ulong invalidIndex = X64Cr3LoadStubPlan.NoIndex,
            ulong conflictingIndex = X64Cr3LoadStubPlan.NoIndex)
        {
            return X64Cr3LoadStubPlanResult.Create(
                status,
                addressStatus,
                failureMappingStatus,
                failureStackStatus,
                allocationRecordCount,
                allocationRecordCapacity,
                mappingRangeCount,
                mappingRangeCapacity,
                invalidIndex,
                conflictingIndex,
                default);
        }
    }

    private static bool TryValidateAllocation(
        OS.Boot.BootMemoryRegionRecord record,
        ulong expectedPageCount,
        out ulong start,
        out ulong end)
    {
        start = record.PhysicalStart;
        end = record.PhysicalEndExclusive;
        if (start == 0
            || (start & (PageSize - 1)) != 0
            || record.VirtualStart != start
            || record.PageCount != expectedPageCount
            || expectedPageCount == 0
            || expectedPageCount > MaximumUnsignedLong / PageSize)
        {
            return false;
        }
        ulong expectedBytes = expectedPageCount * PageSize;
        return record.ByteLength == expectedBytes
            && start <= MaximumUnsignedLong - expectedBytes
            && end == start + expectedBytes;
    }

    private static bool TryValidateRange(
        OS.Kernel.Boot.X64BootIdentityMappingRange range,
        ulong previousEnd,
        out ulong start,
        out ulong end)
    {
        start = 0;
        end = 0;
        if (!range.IsInitialized
            || range.PageCount == 0
            || range.PageCount > MaximumUnsignedLong / PageSize
            || range.ByteLength != range.PageCount * PageSize
            || range.ContributingPurposeMask == 0
            || (range.ContributingPurposeMask & ~0x17eUL) != 0
            || range.Protection == OS.Kernel.Memory.PageProtection.None
            || ((byte)range.Protection & ~7) != 0
            || range.CachePolicy
                == OS.Kernel.Memory.PageCachePolicy.Uninitialized
            || (byte)range.CachePolicy
                > (byte)OS.Kernel.Memory.PageCachePolicy.WriteCombining)
        {
            return false;
        }
        if (!TryGetPhysicalRaw(range.PhysicalStart, out start)
            || !TryGetPhysicalRaw(range.PhysicalEndExclusive, out end)
            || !TryGetVirtualRaw(
                range.VirtualStart,
                out ulong virtualStart)
            || !TryGetVirtualRaw(
                range.VirtualEndExclusive,
                out ulong virtualEnd)
            || start != virtualStart
            || end != virtualEnd
            || (start & (PageSize - 1)) != 0
            || (end & (PageSize - 1)) != 0
            || end <= start
            || end - start != range.ByteLength
            || start < previousEnd)
        {
            return false;
        }
        return true;
    }

    private static bool TryValidateStubRange(
        ulong codeStart,
        ulong codeEnd,
        ulong entryOffset,
        ulong byteLength,
        out ulong start,
        out ulong end)
    {
        start = 0;
        end = 0;
        if (byteLength == 0
            || entryOffset >= PageSize
            || codeStart > MaximumUnsignedLong - entryOffset)
        {
            return false;
        }
        start = codeStart + entryOffset;
        return TryGetRangeEnd(start, byteLength, out end)
            && start >= codeStart
            && end <= codeEnd;
    }

    private static bool TryGetPhysicalRaw(
        OS.Boot.PhysicalAddress address,
        out ulong value)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(
                out value)
            == OS.Boot.AddressValueStatus.Success;
    }

    private static bool TryGetVirtualRaw(
        OS.Boot.VirtualAddress address,
        out ulong value)
    {
        return address.TryGetRawValueForHardwareRegisterBoundary(
                out value)
            == OS.Boot.AddressValueStatus.Success;
    }

    private static bool TryGetRangeEnd(
        ulong start,
        ulong byteLength,
        out ulong end)
    {
        end = 0;
        if (byteLength == 0
            || start > MaximumUnsignedLong - byteLength)
        {
            return false;
        }
        end = start + byteLength;
        return true;
    }

    private static bool TryAddTablePages(
        ulong first,
        ulong second,
        ulong third,
        ulong fourth,
        out ulong total)
    {
        total = 0;
        if (first > MaximumUnsignedLong - second)
            return false;
        ulong firstTwo = first + second;
        if (firstTwo > MaximumUnsignedLong - third)
            return false;
        ulong firstThree = firstTwo + third;
        if (firstThree > MaximumUnsignedLong - fourth)
            return false;
        total = firstThree + fourth;
        return true;
    }

    private static bool TryGetSpanEnd(
        ulong start,
        ulong capacity,
        ulong elementSize,
        out ulong end)
    {
        end = 0;
        if (capacity != 0
            && elementSize > MaximumUnsignedLong / capacity)
        {
            return false;
        }
        ulong byteLength = capacity * elementSize;
        if (start > MaximumUnsignedLong - byteLength)
            return false;
        end = start + byteLength;
        return true;
    }

    private static bool Covers(
        ulong outerStart,
        ulong outerEnd,
        ulong innerStart,
        ulong innerEnd)
    {
        return outerStart <= innerStart && outerEnd >= innerEnd;
    }

    private static bool RangesOverlap(
        ulong leftStart,
        ulong leftEnd,
        ulong rightStart,
        ulong rightEnd)
    {
        return leftStart < rightEnd && rightStart < leftEnd;
    }
}

public readonly struct X64Cr3LoadStubPlanResult
{
    public const ulong StructureSize = 1072;
    private readonly X64Cr3LoadStubPlanStatus _status;
    private readonly OS.Boot.AddressValueStatus _addressStatus;
    private readonly OS.Kernel.Boot.X64BootIdentityMappingStatus
        _identityMappingStatus;
    private readonly OS.Boot.OwnedBootStackSwitchStatus
        _stackSwitchStatus;
    private readonly uint _reserved;
    private readonly ulong _allocationRecordCount;
    private readonly ulong _allocationRecordCapacity;
    private readonly ulong _mappingRangeCount;
    private readonly ulong _mappingRangeCapacity;
    private readonly ulong _invalidIndex;
    private readonly ulong _conflictingIndex;
    private readonly X64Cr3LoadStubPlan _plan;

    private X64Cr3LoadStubPlanResult(
        X64Cr3LoadStubPlanStatus status,
        OS.Boot.AddressValueStatus addressStatus,
        OS.Kernel.Boot.X64BootIdentityMappingStatus
            identityMappingStatus,
        OS.Boot.OwnedBootStackSwitchStatus stackSwitchStatus,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        ulong mappingRangeCount,
        ulong mappingRangeCapacity,
        ulong invalidIndex,
        ulong conflictingIndex,
        X64Cr3LoadStubPlan plan)
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

    public OS.Kernel.Boot.X64BootIdentityMappingStatus
        IdentityMappingStatus => _identityMappingStatus;

    public OS.Boot.OwnedBootStackSwitchStatus StackSwitchStatus =>
        _stackSwitchStatus;

    public ulong AllocationRecordCount => _allocationRecordCount;

    public ulong AllocationRecordCapacity =>
        _allocationRecordCapacity;

    public ulong MappingRangeCount => _mappingRangeCount;

    public ulong MappingRangeCapacity => _mappingRangeCapacity;

    public ulong InvalidIndex => _invalidIndex;

    public ulong ConflictingIndex => _conflictingIndex;

    public X64Cr3LoadStubPlan Plan => _plan;

    public bool Succeeded =>
        _status == X64Cr3LoadStubPlanStatus.Success
        && _addressStatus == OS.Boot.AddressValueStatus.Success
        && _identityMappingStatus
            == OS.Kernel.Boot.X64BootIdentityMappingStatus.Success
        && _stackSwitchStatus
            == OS.Boot.OwnedBootStackSwitchStatus.Success
        && _reserved == 0
        && _allocationRecordCount
            == X64Cr3LoadStubPlan.RequiredAllocationRecordCount
        && _allocationRecordCapacity == _allocationRecordCount
        && _mappingRangeCount == _plan.IdentityMappingPlan.OutputRangeCount
        && _mappingRangeCapacity
            == _plan.IdentityMappingPlan.DestinationCapacity
        && _mappingRangeCapacity >= _mappingRangeCount
        && _identityMappingStatus
            == _plan.IdentityMappingPlan.Status
        && _stackSwitchStatus == _plan.StackSwitchPlan.Status
        && _invalidIndex == X64Cr3LoadStubPlan.NoIndex
        && _conflictingIndex == X64Cr3LoadStubPlan.NoIndex
        && _plan.IsValid;

    internal static X64Cr3LoadStubPlanResult Create(
        X64Cr3LoadStubPlanStatus status,
        OS.Boot.AddressValueStatus addressStatus,
        OS.Kernel.Boot.X64BootIdentityMappingStatus
            identityMappingStatus,
        OS.Boot.OwnedBootStackSwitchStatus stackSwitchStatus,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        ulong mappingRangeCount,
        ulong mappingRangeCapacity,
        ulong invalidIndex,
        ulong conflictingIndex,
        X64Cr3LoadStubPlan plan)
    {
        return new X64Cr3LoadStubPlanResult(
            status,
            addressStatus,
            identityMappingStatus,
            stackSwitchStatus,
            allocationRecordCount,
            allocationRecordCapacity,
            mappingRangeCount,
            mappingRangeCapacity,
            invalidIndex,
            conflictingIndex,
            plan);
    }
}

public readonly struct X64Cr3ProbeAuditResult
{
    public const ulong StructureSize = 64;
    private readonly X64Cr3LoadStubPlanStatus _status;
    private readonly byte _reserved0;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;
    private readonly ulong _eventCount;
    private readonly ulong _eventCapacity;
    private readonly ulong _validatedEventCount;
    private readonly ulong _invalidEventIndex;
    private readonly ulong _finalRawCr3;
    private readonly ulong _finalCanaryValue;
    private readonly ulong _elapsedTicks;

    private X64Cr3ProbeAuditResult(
        X64Cr3LoadStubPlanStatus status,
        ulong eventCount,
        ulong eventCapacity,
        ulong validatedEventCount,
        ulong invalidEventIndex,
        ulong finalRawCr3,
        ulong finalCanaryValue,
        ulong elapsedTicks)
    {
        _status = status;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _eventCount = eventCount;
        _eventCapacity = eventCapacity;
        _validatedEventCount = validatedEventCount;
        _invalidEventIndex = invalidEventIndex;
        _finalRawCr3 = finalRawCr3;
        _finalCanaryValue = finalCanaryValue;
        _elapsedTicks = elapsedTicks;
    }

    public X64Cr3LoadStubPlanStatus Status => _status;

    public bool Succeeded =>
        _status == X64Cr3LoadStubPlanStatus.Success
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0
        && _eventCount == X64Cr3LoadStubPlan.RequiredEventCount
        && _eventCapacity >= _eventCount
        && _eventCapacity <= X64Cr3LoadStubPlan.MaximumEventCount
        && _validatedEventCount == X64Cr3LoadStubPlan.RequiredEventCount
        && _invalidEventIndex == X64Cr3LoadStubPlan.NoIndex
        && _finalRawCr3 != 0
        && _finalCanaryValue != 0;

    public ulong EventCount => _eventCount;

    public ulong EventCapacity => _eventCapacity;

    public ulong ValidatedEventCount => _validatedEventCount;

    public ulong InvalidEventIndex => _invalidEventIndex;

    public ulong FinalRawCr3 => _finalRawCr3;

    public ulong FinalCanaryValue => _finalCanaryValue;

    public ulong ElapsedTicks => _elapsedTicks;

    internal static X64Cr3ProbeAuditResult Create(
        X64Cr3LoadStubPlanStatus status,
        ulong eventCount,
        ulong eventCapacity,
        ulong validatedEventCount,
        ulong invalidEventIndex,
        ulong finalRawCr3,
        ulong finalCanaryValue,
        ulong elapsedTicks)
    {
        return new X64Cr3ProbeAuditResult(
            status,
            eventCount,
            eventCapacity,
            validatedEventCount,
            invalidEventIndex,
            finalRawCr3,
            finalCanaryValue,
            elapsedTicks);
    }
}

internal static unsafe class X64Cr3ProbeTranscriptAuditor
{
    private const ulong MaximumUnsignedLong = ~0UL;

    public static X64Cr3ProbeAuditResult Audit(
        X64Cr3LoadStubPlan plan,
        X64Cr3ProbeEvent* events,
        ulong eventCount,
        ulong eventCapacity)
    {
        if ((ulong)sizeof(X64Cr3ProbeEvent)
                != X64Cr3ProbeEvent.StructureSize
            || (ulong)sizeof(X64Cr3ProbeAuditResult)
                != X64Cr3ProbeAuditResult.StructureSize)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.ContractLayoutMismatch,
                eventCount,
                eventCapacity);
        }

        if (!plan.IsValid)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.Uninitialized,
                eventCount,
                eventCapacity);
        }

        if (eventCount > X64Cr3LoadStubPlan.MaximumEventCount)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.TooManyEvents,
                eventCount,
                eventCapacity);
        }

        if (eventCapacity > X64Cr3LoadStubPlan.MaximumEventCount)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.TooManyEvents,
                eventCount,
                eventCapacity);
        }

        if (eventCount > eventCapacity)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.EventCapacityTooSmall,
                eventCount,
                eventCapacity);
        }

        if (eventCount > X64Cr3LoadStubPlan.RequiredEventCount)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidEventCount,
                eventCount,
                eventCapacity,
                X64Cr3LoadStubPlan.RequiredEventCount,
                X64Cr3LoadStubPlan.RequiredEventCount);
        }

        if (events == null)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.MissingEvents,
                eventCount,
                eventCapacity);
        }

        ulong eventAddress = (ulong)events;
        if ((eventAddress
            & (X64Cr3LoadStubPlan.RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.MisalignedEvents,
                eventCount,
                eventCapacity);
        }

        if (!TryGetSpanEnd(
                eventAddress,
                eventCapacity,
                X64Cr3ProbeEvent.StructureSize,
                out ulong ignoredEnd))
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.EventSpanOverflow,
                eventCount,
                eventCapacity);
        }

        ulong rawFirmwareCr3 = plan.RawFirmwareCr3;
        ulong ownedRoot = 0;
        plan.OwnedRoot.TryGetRawValueForHardwareRegisterBoundary(
            out ownedRoot);
        plan.UsableStackStart
            .TryGetRawValueForHardwareRegisterBoundary(
                out ulong usableStackStart);
        plan.UsableStackEndExclusive
            .TryGetRawValueForHardwareRegisterBoundary(
                out ulong usableStackEnd);
        ulong validated = 0;
        ulong previousElapsed = 0;
        for (ulong index = 0; index < eventCount; index++)
        {
            X64Cr3ProbeEvent current = events[index];
            if (!current.IsInitialized)
            {
                return Fail(
                    X64Cr3LoadStubPlanStatus.InvalidEvent,
                    eventCount,
                    eventCapacity,
                    validated,
                    index);
            }

            X64Cr3LoadStubPlanStatus terminal =
                GetTerminalStatus(current.Kind);
            if (terminal != X64Cr3LoadStubPlanStatus.Success)
            {
                return Fail(
                    terminal,
                    eventCount,
                    eventCapacity,
                    validated,
                    index,
                    current);
            }

            if (current.ElapsedTicks < previousElapsed ||
                current.ElapsedTicks > plan.Policy.TimeoutTicks)
            {
                return Fail(
                    X64Cr3LoadStubPlanStatus.Timeout,
                    eventCount,
                    eventCapacity,
                    validated,
                    index,
                    current);
            }

            X64Cr3ProbeEventKind expectedKind =
                GetExpectedKind(index);
            if (current.Kind != expectedKind)
            {
                X64Cr3LoadStubPlanStatus status =
                    current.Kind
                        == X64Cr3ProbeEventKind.PostMarkerEmitted
                    ? X64Cr3LoadStubPlanStatus.EarlyPostMarker
                    : index == 0
                        ? X64Cr3LoadStubPlanStatus.MissingPreMarker
                        : X64Cr3LoadStubPlanStatus.UnexpectedEvent;
                return Fail(
                    status,
                    eventCount,
                    eventCapacity,
                    validated,
                    index,
                    current);
            }

            if (current.ObservedRsp
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong observedRsp)
                    != OS.Boot.AddressValueStatus.Success
                || usableStackStart
                    > MaximumUnsignedLong
                        - X64Cr3LoadStubPlan
                            .RequiredProbeStackHeadroomBytes
                || observedRsp
                    < usableStackStart
                        + X64Cr3LoadStubPlan
                            .RequiredProbeStackHeadroomBytes
                || observedRsp >= usableStackEnd)
            {
                return Fail(
                    X64Cr3LoadStubPlanStatus.RspEscapedOwnedStack,
                    eventCount,
                    eventCapacity,
                    validated,
                    index,
                    current);
            }

            ulong expectedRoot = index >= 2 && index <= 4
                ? ownedRoot
                : rawFirmwareCr3;
            if (current.ObservedRawCr3 != expectedRoot)
            {
                return Fail(
                    X64Cr3LoadStubPlanStatus.RootReadbackMismatch,
                    eventCount,
                    eventCapacity,
                    validated,
                    index,
                    current);
            }

            ulong expectedCanary = index < 4
                ? plan.StorageLayout.InitialCanaryValue
                : plan.StorageLayout.UpdatedCanaryValue;
            if (current.ObservedCanary != expectedCanary)
            {
                X64Cr3LoadStubPlanStatus status = index == 4
                    ? X64Cr3LoadStubPlanStatus.CanaryNotUpdated
                    : index >= 7
                        ? X64Cr3LoadStubPlanStatus
                            .CanaryValidationFailed
                        : X64Cr3LoadStubPlanStatus.UnexpectedEvent;
                return Fail(
                    status,
                    eventCount,
                    eventCapacity,
                    validated,
                    index,
                    current);
            }

            previousElapsed = current.ElapsedTicks;
            validated = index + 1;
        }

        if (eventCount < X64Cr3LoadStubPlan.RequiredEventCount)
        {
            X64Cr3LoadStubPlanStatus status = eventCount == 0
                ? X64Cr3LoadStubPlanStatus.MissingPreMarker
                : X64Cr3LoadStubPlanStatus.MissingPostMarker;
            return Fail(
                status,
                eventCount,
                eventCapacity,
                validated);
        }

        if (eventCount != X64Cr3LoadStubPlan.RequiredEventCount)
        {
            return Fail(
                X64Cr3LoadStubPlanStatus.InvalidEventCount,
                eventCount,
                eventCapacity,
                validated,
                X64Cr3LoadStubPlan.RequiredEventCount);
        }

        X64Cr3ProbeEvent finalEvent =
            events[X64Cr3LoadStubPlan.RequiredEventCount - 1];
        return X64Cr3ProbeAuditResult.Create(
            X64Cr3LoadStubPlanStatus.Success,
            eventCount,
            eventCapacity,
            validated,
            X64Cr3LoadStubPlan.NoIndex,
            finalEvent.ObservedRawCr3,
            finalEvent.ObservedCanary,
            finalEvent.ElapsedTicks);
    }

    private static X64Cr3ProbeEventKind GetExpectedKind(ulong index)
    {
        if (index == 0)
            return X64Cr3ProbeEventKind.PreMarkerEmitted;
        if (index == 1)
            return X64Cr3ProbeEventKind.TransitionEntered;
        if (index == 2)
            return X64Cr3ProbeEventKind.OwnedRootLoaded;
        if (index == 3)
            return X64Cr3ProbeEventKind.OwnedRootReadBack;
        if (index == 4)
            return X64Cr3ProbeEventKind.CanaryUpdated;
        if (index == 5)
            return X64Cr3ProbeEventKind.FirmwareRootRestored;
        if (index == 6)
            return X64Cr3ProbeEventKind.FirmwareRootReadBack;
        if (index == 7)
            return X64Cr3ProbeEventKind.CanaryValidated;
        return index == 8
            ? X64Cr3ProbeEventKind.PostMarkerEmitted
            : X64Cr3ProbeEventKind.Uninitialized;
    }

    private static X64Cr3LoadStubPlanStatus GetTerminalStatus(
        X64Cr3ProbeEventKind kind)
    {
        if (kind == X64Cr3ProbeEventKind.FirmwareCallbackAttempted)
            return X64Cr3LoadStubPlanStatus
                .FirmwareCallbackInsideInterval;
        if (kind
            == X64Cr3ProbeEventKind.InterruptReenabledInsideInterval)
            return X64Cr3LoadStubPlanStatus
                .InterruptReenabledInsideInterval;
        if (kind == X64Cr3ProbeEventKind.Timeout)
            return X64Cr3LoadStubPlanStatus.Timeout;
        if (kind == X64Cr3ProbeEventKind.Reset)
            return X64Cr3LoadStubPlanStatus.Reset;
        if (kind == X64Cr3ProbeEventKind.TripleFault)
            return X64Cr3LoadStubPlanStatus.TripleFault;
        return X64Cr3LoadStubPlanStatus.Success;
    }

    private static X64Cr3ProbeAuditResult Fail(
        X64Cr3LoadStubPlanStatus status,
        ulong eventCount,
        ulong eventCapacity,
        ulong validatedEventCount = 0,
        ulong invalidEventIndex = X64Cr3LoadStubPlan.NoIndex,
        X64Cr3ProbeEvent observation = default)
    {
        return X64Cr3ProbeAuditResult.Create(
            status,
            eventCount,
            eventCapacity,
            validatedEventCount,
            invalidEventIndex,
            observation.ObservedRawCr3,
            observation.ObservedCanary,
            observation.ElapsedTicks);
    }

    private static bool TryGetSpanEnd(
        ulong start,
        ulong capacity,
        ulong elementSize,
        out ulong end)
    {
        end = 0;
        if (capacity != 0 &&
            elementSize > MaximumUnsignedLong / capacity)
        {
            return false;
        }

        ulong byteLength = capacity * elementSize;
        if (start > MaximumUnsignedLong - byteLength)
        {
            return false;
        }

        end = start + byteLength;
        return true;
    }
}
