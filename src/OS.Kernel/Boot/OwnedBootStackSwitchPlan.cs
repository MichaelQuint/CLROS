namespace OS.Boot;

/// <summary>
/// Identifies the caller-attested state of one owned boot-stack switch plan.
/// Existing values are append-only and the zero-valued default is invalid.
/// These values do not represent processor observations.
/// </summary>
public enum OwnedBootStackSwitchState : byte
{
    Uninitialized = 0,
    Planned = 1,
    Armed = 2,
    TransferRecorded = 3,
    ContinuationObserved = 4,
    Failed = 5
}

/// <summary>
/// Identifies the only continuation contract accepted by the first
/// owned-stack switch plan.
/// </summary>
public enum OwnedBootStackSwitchContinuationContract : byte
{
    Uninitialized = 0,
    StaticNonReturningRspAndHandoff = 1
}

/// <summary>
/// Records the caller's provenance attestation for the handoff storage.
/// The planner validates range separation, but a later live coordinator must
/// prove that the storage is actually static and remains owned.
/// </summary>
public enum OwnedBootStackSwitchHandoffProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedStaticOwnedData = 1
}

/// <summary>
/// Records the caller's provenance attestation for the canonical mutable
/// state storage bound to one immutable plan.
/// </summary>
public enum OwnedBootStackSwitchStateStorageProvenance : byte
{
    Uninitialized = 0,
    CallerAttestedStaticOwnedData = 1
}

/// <summary>
/// Distinguishes the two terminal failure paths represented by the host-only
/// state machine.
/// </summary>
public enum OwnedBootStackSwitchTerminalCause : byte
{
    Uninitialized = 0,
    ContinuationRspAuditFailed = 1,
    UnexpectedReturnReported = 2
}

/// <summary>
/// Identifies whether byte-plan evidence came from emission or readback.
/// Only successful readback evidence can arm a switch plan.
/// </summary>
public enum OwnedBootStackSwitchByteOperation : byte
{
    Uninitialized = 0,
    Emit = 1,
    Verify = 2
}

/// <summary>
/// Reports planning, byte materialization, one-shot state, or continuation
/// stack-pointer audit failures. Existing values are append-only and the
/// zero-valued default is intentionally not success.
/// </summary>
public enum OwnedBootStackSwitchStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidAllocationRecordCount = 2,
    TooManyAllocationRecords = 3,
    AllocationRecordCapacityTooSmall = 4,
    MissingAllocationRecords = 5,
    MisalignedAllocationRecords = 6,
    AllocationRecordAddressRangeOverflow = 7,
    InvalidStackLayout = 8,
    StackLayoutNotReady = 9,
    InvalidCodeMemoryType = 10,
    InvalidCodePhysicalStart = 11,
    MisalignedCodePhysicalStart = 12,
    InvalidCodeVirtualStart = 13,
    MisalignedCodeVirtualStart = 14,
    InvalidCodePageCount = 15,
    CodeByteCountOverflow = 16,
    CodeByteLengthMismatch = 17,
    CodePhysicalEndOverflow = 18,
    CodePhysicalEndMismatch = 19,
    CodeVirtualEndOverflow = 20,
    InvalidDataMemoryType = 21,
    InvalidDataPhysicalStart = 22,
    MisalignedDataPhysicalStart = 23,
    InvalidDataVirtualStart = 24,
    MisalignedDataVirtualStart = 25,
    InvalidDataPageCount = 26,
    DataByteCountOverflow = 27,
    DataByteLengthMismatch = 28,
    DataPhysicalEndOverflow = 29,
    DataPhysicalEndMismatch = 30,
    DataVirtualEndOverflow = 31,
    PhysicalAllocationOverlap = 32,
    VirtualAllocationOverlap = 33,
    DataArenaMismatch = 34,
    GuardVirtualRangeOverflow = 35,
    InvalidStubEntryOffset = 36,
    MisalignedStubEntry = 37,
    StubEntryOverflow = 38,
    InvalidDeclaredStubByteLength = 39,
    StubBytePlanOutOfRange = 40,
    InvalidContinuationAddress = 41,
    InvalidContinuationContract = 42,
    ContinuationInsideStackArena = 43,
    InvalidHandoffProvenance = 44,
    InvalidHandoffAddress = 45,
    MisalignedHandoffAddress = 46,
    InvalidHandoffLength = 47,
    HandoffRangeOverflow = 48,
    HandoffOverlapsStubPage = 49,
    HandoffInsideGuard = 50,
    HandoffInsideUsableStack = 51,
    AbiStackTooSmall = 52,
    AbiStackArithmeticOverflow = 53,
    InvalidAbiAlignment = 54,
    InvalidPlan = 55,
    MissingByteBuffer = 56,
    ByteBufferTooSmall = 57,
    ByteBufferAddressRangeOverflow = 58,
    ByteLengthMismatch = 59,
    BytePlanMismatch = 60,
    InvalidBytePlanEvidence = 61,
    RepeatedArm = 62,
    IllegalArmState = 63,
    TransferBeforeArm = 64,
    RepeatedTransfer = 65,
    IllegalTransferState = 66,
    ObservationBeforeTransfer = 67,
    RepeatedObservation = 68,
    InvalidObservedRsp = 69,
    ObservedRspInGuard = 70,
    ObservedRspOutsideUsableStack = 71,
    MisalignedObservedRsp = 72,
    ObservedRspMismatch = 73,
    ShadowSpaceOverflow = 74,
    ShadowSpaceOutOfRange = 75,
    UnexpectedReturn = 76,
    RepeatedUnexpectedReturn = 77,
    InvalidState = 78,
    ContinuationInsideStubPage = 79,
    StubReservationRejected = 80,
    InvalidMaterializedPageAddress = 81,
    MaterializedPageMismatch = 82,
    MaterializedPageLengthMismatch = 83,
    PlanEvidenceMismatch = 84,
    MissingStateRecord = 85,
    StateRecordCapacityTooSmall = 86,
    MisalignedStateRecord = 87,
    StateRecordAddressRangeOverflow = 88,
    StateRecordStorageMismatch = 89,
    StateRecordPlanMismatch = 90,
    StateRecordNotEmpty = 91,
    InvalidStateRecord = 92,
    ContinuationOverlapsHandoff = 93,
    InvalidStateStorageProvenance = 94,
    InvalidStateStorageAddress = 95,
    MisalignedStateStorageAddress = 96,
    InvalidStateStorageLength = 97,
    StateStorageRangeOverflow = 98,
    StateStorageOverlapsStubPage = 99,
    StateStorageOverlapsGuard = 100,
    StateStorageOverlapsUsableStack = 101,
    StateStorageOverlapsHandoff = 102,
    ContinuationOverlapsStateStorage = 103
}

/// <summary>
/// Describes the POD payload that a future static handoff buffer will hold.
/// The x64 stub leaves the handoff address in RDX, publishes the observed
/// continuation-entry RSP in RCX, and jumps to the nonreturning continuation.
/// </summary>
public readonly struct OwnedBootStackSwitchHandoff
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 88;
    public const ulong RequiredStorageAlignment = 8;

    private readonly uint _size;
    private readonly uint _version;
    private readonly VirtualAddress _storageStart;
    private readonly VirtualAddress _storageEndExclusive;
    private readonly ulong _storageByteLength;
    private readonly VirtualAddress _stubEntry;
    private readonly VirtualAddress _usableStackStart;
    private readonly VirtualAddress _usableStackEndExclusive;
    private readonly VirtualAddress _preTransferStackPointer;
    private readonly VirtualAddress _expectedContinuationStackPointer;
    private readonly VirtualAddress _continuation;
    private readonly OwnedBootStackSwitchHandoffProvenance _provenance;
    private readonly OwnedBootStackSwitchContinuationContract
        _continuationContract;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;

    private OwnedBootStackSwitchHandoff(
        VirtualAddress storageStart,
        VirtualAddress storageEndExclusive,
        ulong storageByteLength,
        VirtualAddress stubEntry,
        VirtualAddress usableStackStart,
        VirtualAddress usableStackEndExclusive,
        VirtualAddress preTransferStackPointer,
        VirtualAddress expectedContinuationStackPointer,
        VirtualAddress continuation,
        OwnedBootStackSwitchHandoffProvenance provenance,
        OwnedBootStackSwitchContinuationContract continuationContract)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _storageStart = storageStart;
        _storageEndExclusive = storageEndExclusive;
        _storageByteLength = storageByteLength;
        _stubEntry = stubEntry;
        _usableStackStart = usableStackStart;
        _usableStackEndExclusive = usableStackEndExclusive;
        _preTransferStackPointer = preTransferStackPointer;
        _expectedContinuationStackPointer =
            expectedContinuationStackPointer;
        _continuation = continuation;
        _provenance = provenance;
        _continuationContract = continuationContract;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
    }

    public uint Size => _size;

    public uint Version => _version;

    public VirtualAddress StorageStart => _storageStart;

    public VirtualAddress StorageEndExclusive => _storageEndExclusive;

    public ulong StorageByteLength => _storageByteLength;

    public VirtualAddress StubEntry => _stubEntry;

    public VirtualAddress UsableStackStart => _usableStackStart;

    public VirtualAddress UsableStackEndExclusive =>
        _usableStackEndExclusive;

    public VirtualAddress PreTransferStackPointer =>
        _preTransferStackPointer;

    public VirtualAddress ExpectedContinuationStackPointer =>
        _expectedContinuationStackPointer;

    public VirtualAddress Continuation => _continuation;

    public OwnedBootStackSwitchHandoffProvenance Provenance =>
        _provenance;

    public OwnedBootStackSwitchContinuationContract
        ContinuationContract => _continuationContract;

    public bool IsValid => Validate();

    internal static OwnedBootStackSwitchHandoff Create(
        VirtualAddress storageStart,
        VirtualAddress storageEndExclusive,
        ulong storageByteLength,
        VirtualAddress stubEntry,
        VirtualAddress usableStackStart,
        VirtualAddress usableStackEndExclusive,
        VirtualAddress preTransferStackPointer,
        VirtualAddress expectedContinuationStackPointer,
        VirtualAddress continuation,
        OwnedBootStackSwitchHandoffProvenance provenance,
        OwnedBootStackSwitchContinuationContract continuationContract)
    {
        return new OwnedBootStackSwitchHandoff(
            storageStart,
            storageEndExclusive,
            storageByteLength,
            stubEntry,
            usableStackStart,
            usableStackEndExclusive,
            preTransferStackPointer,
            expectedContinuationStackPointer,
            continuation,
            provenance,
            continuationContract);
    }

    internal bool Validate()
    {
        if (_size != StructureSize
            || _version != CurrentVersion
            || !_storageStart.IsValid
            || !_storageStart.IsAligned(RequiredStorageAlignment)
            || !_storageEndExclusive.IsValid
            || _storageByteLength != StructureSize
            || !_stubEntry.IsValid
            || !_stubEntry.IsAligned(
                OwnedBootStackSwitchPlan.StubEntryAlignmentBytes)
            || !_usableStackStart.IsValid
            || !_usableStackEndExclusive.IsValid
            || !_preTransferStackPointer.IsValid
            || !_expectedContinuationStackPointer.IsValid
            || !_continuation.IsValid
            || _provenance
                != OwnedBootStackSwitchHandoffProvenance
                    .CallerAttestedStaticOwnedData
            || _continuationContract
                != OwnedBootStackSwitchContinuationContract
                    .StaticNonReturningRspAndHandoff
            || _reserved0 != 0
            || _reserved1 != 0
            || _reserved2 != 0
            || _reserved3 != 0
            || _reserved4 != 0
            || _reserved5 != 0)
        {
            return false;
        }

        if (_storageStart.TryGetEndExclusive(
                _storageByteLength,
                out VirtualAddress expectedStorageEnd)
                != AddressValueStatus.Success
            || !expectedStorageEnd.Equals(_storageEndExclusive)
            || !TryGetRaw(
                _usableStackStart,
                out ulong usableStart)
            || !TryGetRaw(
                _usableStackEndExclusive,
                out ulong usableEnd)
            || !TryGetRaw(
                _preTransferStackPointer,
                out ulong preTransfer)
            || !TryGetRaw(
                _expectedContinuationStackPointer,
                out ulong continuationRsp)
            || usableStart >= usableEnd
            || continuationRsp < usableStart
            || preTransfer < continuationRsp
            || preTransfer - continuationRsp
                != OwnedBootStackSwitchPlan.FakeReturnAddressBytes
            || usableEnd < preTransfer
            || usableEnd - preTransfer
                != OwnedBootStackSwitchPlan.X64ShadowSpaceBytes
            || (preTransfer
                & (OwnedBootStackSwitchPlan.X64StackAlignmentBytes - 1))
                != 0
            || (continuationRsp
                & (OwnedBootStackSwitchPlan.X64StackAlignmentBytes - 1))
                != OwnedBootStackSwitchPlan
                    .ContinuationEntryAlignmentRemainder)
        {
            return false;
        }

        return true;
    }

    private static bool TryGetRaw(
        VirtualAddress address,
        out ulong value)
    {
        return address.TryGetRawValueForUefiBoundary(out value)
            == AddressValueStatus.Success;
    }
}

/// <summary>
/// Host-audited description of one future x64 owned-stack transition.
/// This value never writes RSP, invokes the stub, calls the continuation,
/// changes page protections, or establishes that any address is live.
/// </summary>
public readonly struct OwnedBootStackSwitchPlan
{
    public const ulong RequiredAllocationRecordCount = 2;
    public const ulong CodeAllocationRecordIndex = 0;
    public const ulong DataAllocationRecordIndex = 1;
    public const ulong RequiredCodePageCount = 1;
    public const ulong StubEntryAlignmentBytes = 16;
    public const ulong X64StackAlignmentBytes = 16;
    public const ulong X64ShadowSpaceBytes = 32;
    public const ulong FakeReturnAddressBytes = 8;
    public const ulong RequiredContinuationFrameBytes =
        X64ShadowSpaceBytes + FakeReturnAddressBytes;
    public const ulong ContinuationEntryAlignmentRemainder = 8;
    public const ulong RequiredStubByteLength = 14;
    public const ulong NoIndex = ~0UL;

    public const byte StubByte00 = 0x48;
    public const byte StubByte01 = 0x89;
    public const byte StubByte02 = 0xCC;
    public const byte StubByte03 = 0x48;
    public const byte StubByte04 = 0x31;
    public const byte StubByte05 = 0xED;
    public const byte StubByte06 = 0x6A;
    public const byte StubByte07 = 0x00;
    public const byte StubByte08 = 0x48;
    public const byte StubByte09 = 0x89;
    public const byte StubByte10 = 0xE1;
    public const byte StubByte11 = 0x41;
    public const byte StubByte12 = 0xFF;
    public const byte StubByte13 = 0xE0;

    private const ulong MaximumUnsignedLong = ~0UL;
    private const ulong RequiredTableAlignment = 8;

    private readonly BootMemoryRegionRecord _codeAllocation;
    private readonly BootMemoryRegionRecord _dataAllocation;
    private readonly OS.Kernel.Memory.BootReservedRange
        _stubReservation;
    private readonly OwnedBootStackLayout _stackLayout;
    private readonly OwnedBootStackSwitchHandoff _handoff;
    private readonly VirtualAddress _stateStorageStart;
    private readonly VirtualAddress _stateStorageEndExclusive;
    private readonly ulong _stateStorageByteLength;
    private readonly OwnedBootStackSwitchStateStorageProvenance
        _stateStorageProvenance;
    private readonly byte _stateStorageReserved0;
    private readonly byte _stateStorageReserved1;
    private readonly byte _stateStorageReserved2;
    private readonly byte _stateStorageReserved3;
    private readonly byte _stateStorageReserved4;
    private readonly byte _stateStorageReserved5;
    private readonly byte _stateStorageReserved6;
    private readonly PhysicalAddress _stubPhysicalEntry;
    private readonly VirtualAddress _stubVirtualEntry;
    private readonly VirtualAddress _codeVirtualEndExclusive;
    private readonly VirtualAddress _dataVirtualEndExclusive;
    private readonly VirtualAddress _guardVirtualStart;
    private readonly VirtualAddress _guardVirtualEndExclusive;
    private readonly VirtualAddress _usableStackVirtualStart;
    private readonly VirtualAddress _usableStackVirtualEndExclusive;
    private readonly VirtualAddress _preTransferStackPointer;
    private readonly VirtualAddress _expectedContinuationStackPointer;
    private readonly ulong _stubEntryOffset;
    private readonly ulong _declaredStubByteLength;

    private OwnedBootStackSwitchPlan(
        BootMemoryRegionRecord codeAllocation,
        BootMemoryRegionRecord dataAllocation,
        OS.Kernel.Memory.BootReservedRange stubReservation,
        OwnedBootStackLayout stackLayout,
        OwnedBootStackSwitchHandoff handoff,
        VirtualAddress stateStorageStart,
        VirtualAddress stateStorageEndExclusive,
        ulong stateStorageByteLength,
        OwnedBootStackSwitchStateStorageProvenance
            stateStorageProvenance,
        PhysicalAddress stubPhysicalEntry,
        VirtualAddress stubVirtualEntry,
        VirtualAddress codeVirtualEndExclusive,
        VirtualAddress dataVirtualEndExclusive,
        VirtualAddress guardVirtualStart,
        VirtualAddress guardVirtualEndExclusive,
        VirtualAddress usableStackVirtualStart,
        VirtualAddress usableStackVirtualEndExclusive,
        VirtualAddress preTransferStackPointer,
        VirtualAddress expectedContinuationStackPointer,
        ulong stubEntryOffset,
        ulong declaredStubByteLength)
    {
        _codeAllocation = codeAllocation;
        _dataAllocation = dataAllocation;
        _stubReservation = stubReservation;
        _stackLayout = stackLayout;
        _handoff = handoff;
        _stateStorageStart = stateStorageStart;
        _stateStorageEndExclusive = stateStorageEndExclusive;
        _stateStorageByteLength = stateStorageByteLength;
        _stateStorageProvenance = stateStorageProvenance;
        _stateStorageReserved0 = 0;
        _stateStorageReserved1 = 0;
        _stateStorageReserved2 = 0;
        _stateStorageReserved3 = 0;
        _stateStorageReserved4 = 0;
        _stateStorageReserved5 = 0;
        _stateStorageReserved6 = 0;
        _stubPhysicalEntry = stubPhysicalEntry;
        _stubVirtualEntry = stubVirtualEntry;
        _codeVirtualEndExclusive = codeVirtualEndExclusive;
        _dataVirtualEndExclusive = dataVirtualEndExclusive;
        _guardVirtualStart = guardVirtualStart;
        _guardVirtualEndExclusive = guardVirtualEndExclusive;
        _usableStackVirtualStart = usableStackVirtualStart;
        _usableStackVirtualEndExclusive =
            usableStackVirtualEndExclusive;
        _preTransferStackPointer = preTransferStackPointer;
        _expectedContinuationStackPointer =
            expectedContinuationStackPointer;
        _stubEntryOffset = stubEntryOffset;
        _declaredStubByteLength = declaredStubByteLength;
    }

    public BootMemoryRegionRecord CodeAllocation => _codeAllocation;

    public BootMemoryRegionRecord DataAllocation => _dataAllocation;

    public OS.Kernel.Memory.BootReservedRange StubReservation =>
        _stubReservation;

    public OwnedBootStackLayout StackLayout => _stackLayout;

    public OwnedBootStackSwitchHandoff Handoff => _handoff;

    public VirtualAddress StateStorageStart => _stateStorageStart;

    public VirtualAddress StateStorageEndExclusive =>
        _stateStorageEndExclusive;

    public ulong StateStorageByteLength => _stateStorageByteLength;

    public OwnedBootStackSwitchStateStorageProvenance
        StateStorageProvenance => _stateStorageProvenance;

    public PhysicalAddress StubPhysicalEntry => _stubPhysicalEntry;

    public VirtualAddress StubVirtualEntry => _stubVirtualEntry;

    public VirtualAddress CodeVirtualEndExclusive =>
        _codeVirtualEndExclusive;

    public VirtualAddress DataVirtualEndExclusive =>
        _dataVirtualEndExclusive;

    public VirtualAddress GuardVirtualStart => _guardVirtualStart;

    public VirtualAddress GuardVirtualEndExclusive =>
        _guardVirtualEndExclusive;

    public VirtualAddress UsableStackVirtualStart =>
        _usableStackVirtualStart;

    public VirtualAddress UsableStackVirtualEndExclusive =>
        _usableStackVirtualEndExclusive;

    public VirtualAddress PreTransferStackPointer =>
        _preTransferStackPointer;

    public VirtualAddress ExpectedContinuationStackPointer =>
        _expectedContinuationStackPointer;

    public ulong StubEntryOffset => _stubEntryOffset;

    public ulong DeclaredStubByteLength => _declaredStubByteLength;

    public bool IsValid => Validate();

    public static unsafe OwnedBootStackSwitchPlanResult Create(
        BootMemoryRegionRecord* allocationRecords,
        ulong allocationRecordCount,
        ulong allocationRecordCapacity,
        OwnedBootStackLayout stackLayout,
        ulong stubEntryOffset,
        ulong declaredStubByteLength,
        VirtualAddress handoffStorageAddress,
        ulong handoffStorageByteLength,
        OwnedBootStackSwitchHandoffProvenance handoffProvenance,
        VirtualAddress stateStorageAddress,
        ulong stateStorageByteLength,
        OwnedBootStackSwitchStateStorageProvenance
            stateStorageProvenance,
        VirtualAddress continuationAddress,
        OwnedBootStackSwitchContinuationContract continuationContract)
    {
        if (allocationRecordCount < RequiredAllocationRecordCount)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidAllocationRecordCount,
                AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (allocationRecordCount > RequiredAllocationRecordCount)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.TooManyAllocationRecords,
                AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (allocationRecordCapacity < allocationRecordCount)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus
                    .AllocationRecordCapacityTooSmall,
                AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (allocationRecords == null)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.MissingAllocationRecords,
                AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        ulong tableAddress = (ulong)allocationRecords;
        if ((tableAddress & (RequiredTableAlignment - 1)) != 0)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.MisalignedAllocationRecords,
                AddressValueStatus.MisalignedAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        ulong recordSize = (ulong)sizeof(BootMemoryRegionRecord);
        if (allocationRecordCount > MaximumUnsignedLong / recordSize)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus
                    .AllocationRecordAddressRangeOverflow,
                AddressValueStatus.AddressOverflow,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        ulong tableByteLength = allocationRecordCount * recordSize;
        if (tableAddress > MaximumUnsignedLong - tableByteLength)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus
                    .AllocationRecordAddressRangeOverflow,
                AddressValueStatus.AddressOverflow,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (!stackLayout.IsValid)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidStackLayout,
                AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (stackLayout.Lifetime != OwnedBootStackLifetime.Ready)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.StackLayoutNotReady,
                AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        BootMemoryRegionRecord codeAllocation =
            allocationRecords[CodeAllocationRecordIndex];
        OwnedBootStackSwitchStatus codeStatus = ValidateAllocationRecord(
            codeAllocation,
            isCode: true,
            out AddressValueStatus addressStatus,
            out PhysicalAddress codePhysicalStart,
            out PhysicalAddress codePhysicalEnd,
            out VirtualAddress codeVirtualStart,
            out VirtualAddress codeVirtualEnd);
        if (codeStatus != OwnedBootStackSwitchStatus.Success)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                codeStatus,
                addressStatus,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                CodeAllocationRecordIndex);
        }

        BootMemoryRegionRecord dataAllocation =
            allocationRecords[DataAllocationRecordIndex];
        OwnedBootStackSwitchStatus dataStatus = ValidateAllocationRecord(
            dataAllocation,
            isCode: false,
            out addressStatus,
            out PhysicalAddress dataPhysicalStart,
            out PhysicalAddress dataPhysicalEnd,
            out VirtualAddress dataVirtualStart,
            out VirtualAddress dataVirtualEnd);
        if (dataStatus != OwnedBootStackSwitchStatus.Success)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                dataStatus,
                addressStatus,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        if (RangesOverlap(
                codeAllocation.PhysicalStart,
                codeAllocation.PhysicalEndExclusive,
                dataAllocation.PhysicalStart,
                dataAllocation.PhysicalEndExclusive))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.PhysicalAllocationOverlap,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        OS.Kernel.Memory.BootReservedRangeStatus stubReservationStatus =
            OS.Kernel.Memory.BootReservedRange.TryCreate(
                OS.Kernel.Memory.BootReservationKind
                    .BootStackSwitchStub,
                codePhysicalStart,
                codeAllocation.PageCount,
                out OS.Kernel.Memory.BootReservedRange
                    stubReservation);
        if (stubReservationStatus
            != OS.Kernel.Memory.BootReservedRangeStatus.Success)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.StubReservationRejected,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                CodeAllocationRecordIndex);
        }

        ulong codeVirtualEndRaw =
            codeAllocation.VirtualStart + codeAllocation.ByteLength;
        ulong dataVirtualEndRaw =
            dataAllocation.VirtualStart + dataAllocation.ByteLength;
        if (RangesOverlap(
                codeAllocation.VirtualStart,
                codeVirtualEndRaw,
                dataAllocation.VirtualStart,
                dataVirtualEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.VirtualAllocationOverlap,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        if (!dataPhysicalStart.Equals(stackLayout.BackingStart)
            || !dataPhysicalEnd.Equals(
                stackLayout.BackingEndExclusive)
            || dataAllocation.PageCount
                != stackLayout.BackingPageCount
            || dataAllocation.ByteLength
                != stackLayout.BackingByteLength)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.DataArenaMismatch,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        VirtualAddress guardVirtualStart = dataVirtualStart;
        AddressValueStatus guardEndStatus =
            guardVirtualStart.TryGetEndExclusive(
                stackLayout.GuardReservation.ByteLength,
                out VirtualAddress guardVirtualEnd);
        if (guardEndStatus != AddressValueStatus.Success)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.GuardVirtualRangeOverflow,
                guardEndStatus,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        VirtualAddress usableStackVirtualStart = guardVirtualEnd;
        VirtualAddress usableStackVirtualEnd = dataVirtualEnd;

        if (dataAllocation.ByteLength
                < RequiredContinuationFrameBytes
            || stackLayout.StackReservation.ByteLength
                < RequiredContinuationFrameBytes)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.AbiStackTooSmall,
                AddressValueStatus.ZeroLength,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        if (!TryGetRaw(
                usableStackVirtualStart,
                out ulong usableStackStartRaw)
            || !TryGetRaw(
                usableStackVirtualEnd,
                out ulong usableStackEndRaw)
            || usableStackEndRaw < RequiredContinuationFrameBytes
            || usableStackEndRaw - RequiredContinuationFrameBytes
                < usableStackStartRaw)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.AbiStackArithmeticOverflow,
                AddressValueStatus.AddressOverflow,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        ulong preTransferRaw =
            usableStackEndRaw - X64ShadowSpaceBytes;
        ulong continuationRspRaw =
            preTransferRaw - FakeReturnAddressBytes;
        if ((preTransferRaw & (X64StackAlignmentBytes - 1)) != 0
            || (continuationRspRaw
                & (X64StackAlignmentBytes - 1))
                != ContinuationEntryAlignmentRemainder)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidAbiAlignment,
                AddressValueStatus.MisalignedAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        if (VirtualAddress.TryCreateFromUefiBoundary(
                preTransferRaw,
                out VirtualAddress preTransferStackPointer)
                != AddressValueStatus.Success
            || VirtualAddress.TryCreateFromUefiBoundary(
                continuationRspRaw,
                out VirtualAddress expectedContinuationStackPointer)
                != AddressValueStatus.Success)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.AbiStackArithmeticOverflow,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        if (declaredStubByteLength != RequiredStubByteLength)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus
                    .InvalidDeclaredStubByteLength,
                AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                CodeAllocationRecordIndex);
        }

        if (stubEntryOffset
            > MaximumUnsignedLong - declaredStubByteLength)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.StubEntryOverflow,
                AddressValueStatus.AddressOverflow,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                CodeAllocationRecordIndex);
        }

        if (stubEntryOffset >= codeAllocation.ByteLength)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidStubEntryOffset,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                CodeAllocationRecordIndex);
        }

        if (stubEntryOffset + declaredStubByteLength
            > codeAllocation.ByteLength)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.StubBytePlanOutOfRange,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                CodeAllocationRecordIndex);
        }

        AddressValueStatus stubPhysicalStatus =
            codePhysicalStart.TryAdd(
                stubEntryOffset,
                out PhysicalAddress stubPhysicalEntry);
        AddressValueStatus stubVirtualStatus =
            codeVirtualStart.TryAdd(
                stubEntryOffset,
                out VirtualAddress stubVirtualEntry);
        if (stubPhysicalStatus != AddressValueStatus.Success
            || stubVirtualStatus != AddressValueStatus.Success)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.StubEntryOverflow,
                stubPhysicalStatus != AddressValueStatus.Success
                    ? stubPhysicalStatus
                    : stubVirtualStatus,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                CodeAllocationRecordIndex);
        }

        if (!stubPhysicalEntry.IsAligned(StubEntryAlignmentBytes)
            || !stubVirtualEntry.IsAligned(StubEntryAlignmentBytes))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.MisalignedStubEntry,
                AddressValueStatus.MisalignedAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                CodeAllocationRecordIndex);
        }

        if (!continuationAddress.IsValid)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidContinuationAddress,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (continuationContract
            != OwnedBootStackSwitchContinuationContract
                .StaticNonReturningRspAndHandoff)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidContinuationContract,
                AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (!TryGetRaw(
                continuationAddress,
                out ulong continuationRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidContinuationAddress,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (IsAddressInside(
                continuationRaw,
                codeAllocation.VirtualStart,
                codeVirtualEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus
                    .ContinuationInsideStubPage,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                CodeAllocationRecordIndex);
        }

        if (IsAddressInside(
                continuationRaw,
                dataAllocation.VirtualStart,
                dataVirtualEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.ContinuationInsideStackArena,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        if (handoffProvenance
            != OwnedBootStackSwitchHandoffProvenance
                .CallerAttestedStaticOwnedData)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidHandoffProvenance,
                AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (!handoffStorageAddress.IsValid)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidHandoffAddress,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (!handoffStorageAddress.IsAligned(
                OwnedBootStackSwitchHandoff
                    .RequiredStorageAlignment))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.MisalignedHandoffAddress,
                AddressValueStatus.MisalignedAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (handoffStorageByteLength
            != OwnedBootStackSwitchHandoff.StructureSize)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidHandoffLength,
                handoffStorageByteLength == 0
                    ? AddressValueStatus.ZeroLength
                    : AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        AddressValueStatus handoffEndStatus =
            handoffStorageAddress.TryGetEndExclusive(
                handoffStorageByteLength,
                out VirtualAddress handoffStorageEnd);
        if (handoffEndStatus != AddressValueStatus.Success
            || !TryGetRaw(
                handoffStorageAddress,
                out ulong handoffStartRaw)
            || !TryGetRaw(
                handoffStorageEnd,
                out ulong handoffEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.HandoffRangeOverflow,
                handoffEndStatus,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (IsAddressInside(
                continuationRaw,
                handoffStartRaw,
                handoffEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.ContinuationOverlapsHandoff,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (RangesOverlap(
                handoffStartRaw,
                handoffEndRaw,
                codeAllocation.VirtualStart,
                codeVirtualEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.HandoffOverlapsStubPage,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                CodeAllocationRecordIndex);
        }

        if (!TryGetRaw(
                guardVirtualStart,
                out ulong guardStartRaw)
            || !TryGetRaw(
                guardVirtualEnd,
                out ulong guardEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.GuardVirtualRangeOverflow,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        if (RangesOverlap(
                handoffStartRaw,
                handoffEndRaw,
                guardStartRaw,
                guardEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.HandoffInsideGuard,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        if (RangesOverlap(
                handoffStartRaw,
                handoffEndRaw,
                usableStackStartRaw,
                usableStackEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.HandoffInsideUsableStack,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        if (stateStorageProvenance
            != OwnedBootStackSwitchStateStorageProvenance
                .CallerAttestedStaticOwnedData)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus
                    .InvalidStateStorageProvenance,
                AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (!stateStorageAddress.IsValid
            || !TryGetRaw(
                stateStorageAddress,
                out ulong stateStorageStartRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidStateStorageAddress,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (!stateStorageAddress.IsAligned(
                OwnedBootStackSwitchStateRecord
                    .RequiredStorageAlignment))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus
                    .MisalignedStateStorageAddress,
                AddressValueStatus.MisalignedAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (stateStorageByteLength
            != OwnedBootStackSwitchStateRecord.StructureSize)
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidStateStorageLength,
                stateStorageByteLength == 0
                    ? AddressValueStatus.ZeroLength
                    : AddressValueStatus.Uninitialized,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        AddressValueStatus stateStorageEndStatus =
            stateStorageAddress.TryGetEndExclusive(
                stateStorageByteLength,
                out VirtualAddress stateStorageEnd);
        if (stateStorageEndStatus != AddressValueStatus.Success
            || !TryGetRaw(
                stateStorageEnd,
                out ulong stateStorageEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.StateStorageRangeOverflow,
                stateStorageEndStatus,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (RangesOverlap(
                stateStorageStartRaw,
                stateStorageEndRaw,
                codeAllocation.VirtualStart,
                codeVirtualEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus
                    .StateStorageOverlapsStubPage,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                CodeAllocationRecordIndex);
        }

        if (RangesOverlap(
                stateStorageStartRaw,
                stateStorageEndRaw,
                guardStartRaw,
                guardEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.StateStorageOverlapsGuard,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        if (RangesOverlap(
                stateStorageStartRaw,
                stateStorageEndRaw,
                usableStackStartRaw,
                usableStackEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus
                    .StateStorageOverlapsUsableStack,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                DataAllocationRecordIndex);
        }

        if (RangesOverlap(
                stateStorageStartRaw,
                stateStorageEndRaw,
                handoffStartRaw,
                handoffEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus
                    .StateStorageOverlapsHandoff,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        if (IsAddressInside(
                continuationRaw,
                stateStorageStartRaw,
                stateStorageEndRaw))
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus
                    .ContinuationOverlapsStateStorage,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        OwnedBootStackSwitchHandoff handoff =
            OwnedBootStackSwitchHandoff.Create(
                handoffStorageAddress,
                handoffStorageEnd,
                handoffStorageByteLength,
                stubVirtualEntry,
                usableStackVirtualStart,
                usableStackVirtualEnd,
                preTransferStackPointer,
                expectedContinuationStackPointer,
                continuationAddress,
                handoffProvenance,
                continuationContract);
        OwnedBootStackSwitchPlan plan =
            new OwnedBootStackSwitchPlan(
                codeAllocation,
                dataAllocation,
                stubReservation,
                stackLayout,
                handoff,
                stateStorageAddress,
                stateStorageEnd,
                stateStorageByteLength,
                stateStorageProvenance,
                stubPhysicalEntry,
                stubVirtualEntry,
                codeVirtualEnd,
                dataVirtualEnd,
                guardVirtualStart,
                guardVirtualEnd,
                usableStackVirtualStart,
                usableStackVirtualEnd,
                preTransferStackPointer,
                expectedContinuationStackPointer,
                stubEntryOffset,
                declaredStubByteLength);
        if (!plan.Validate())
        {
            return OwnedBootStackSwitchPlanResult.Fail(
                OwnedBootStackSwitchStatus.InvalidPlan,
                AddressValueStatus.InvalidAddress,
                stackLayout.Lifetime,
                allocationRecordCount,
                allocationRecordCapacity,
                NoIndex);
        }

        return OwnedBootStackSwitchPlanResult.Success(
            stackLayout.Lifetime,
            allocationRecordCount,
            allocationRecordCapacity,
            plan);
    }

    public unsafe OwnedBootStackSwitchBytePlanResult
        EmitAuditedStubPage(
            byte* codePageBase,
            ulong availablePageCapacity,
            ulong materializedPageByteLength)
    {
        OwnedBootStackSwitchStatus preflightStatus =
            ValidateMaterializedPage(
                codePageBase,
                availablePageCapacity,
                materializedPageByteLength);
        if (preflightStatus != OwnedBootStackSwitchStatus.Success)
        {
            return OwnedBootStackSwitchBytePlanResult.Fail(
                preflightStatus,
                OwnedBootStackSwitchByteOperation.Emit,
                (ulong)codePageBase,
                availablePageCapacity,
                materializedPageByteLength,
                this);
        }

        byte* stubEntry = codePageBase + _stubEntryOffset;
        stubEntry[0] = StubByte00;
        stubEntry[1] = StubByte01;
        stubEntry[2] = StubByte02;
        stubEntry[3] = StubByte03;
        stubEntry[4] = StubByte04;
        stubEntry[5] = StubByte05;
        stubEntry[6] = StubByte06;
        stubEntry[7] = StubByte07;
        stubEntry[8] = StubByte08;
        stubEntry[9] = StubByte09;
        stubEntry[10] = StubByte10;
        stubEntry[11] = StubByte11;
        stubEntry[12] = StubByte12;
        stubEntry[13] = StubByte13;

        return OwnedBootStackSwitchBytePlanResult.Success(
            OwnedBootStackSwitchByteOperation.Emit,
            (ulong)codePageBase,
            availablePageCapacity,
            materializedPageByteLength,
            this);
    }

    public unsafe OwnedBootStackSwitchBytePlanResult
        VerifyAuditedStubPage(
            byte* codePageBase,
            ulong availablePageCapacity,
            ulong materializedPageByteLength,
            ulong materializedStubByteLength)
    {
        OwnedBootStackSwitchStatus preflightStatus =
            ValidateMaterializedPage(
                codePageBase,
                availablePageCapacity,
                materializedPageByteLength);
        if (preflightStatus != OwnedBootStackSwitchStatus.Success)
        {
            return OwnedBootStackSwitchBytePlanResult.Fail(
                preflightStatus,
                OwnedBootStackSwitchByteOperation.Verify,
                (ulong)codePageBase,
                availablePageCapacity,
                materializedPageByteLength,
                this);
        }

        if (materializedStubByteLength != RequiredStubByteLength)
        {
            return OwnedBootStackSwitchBytePlanResult.Fail(
                OwnedBootStackSwitchStatus.ByteLengthMismatch,
                OwnedBootStackSwitchByteOperation.Verify,
                (ulong)codePageBase,
                availablePageCapacity,
                materializedPageByteLength,
                this);
        }

        byte* stubEntry = codePageBase + _stubEntryOffset;
        for (ulong index = 0; index < RequiredStubByteLength; index++)
        {
            if (stubEntry[index] != GetExpectedByte(index))
            {
                return OwnedBootStackSwitchBytePlanResult.Fail(
                    OwnedBootStackSwitchStatus.BytePlanMismatch,
                    OwnedBootStackSwitchByteOperation.Verify,
                    (ulong)codePageBase,
                    availablePageCapacity,
                    materializedPageByteLength,
                    this,
                    index);
            }
        }

        return OwnedBootStackSwitchBytePlanResult.Success(
            OwnedBootStackSwitchByteOperation.Verify,
            (ulong)codePageBase,
            availablePageCapacity,
            materializedPageByteLength,
            this);
    }

    public unsafe OwnedBootStackSwitchStateResult
        InitializeOneShotState(
            OwnedBootStackSwitchStateRecord* stateStorage,
            ulong availableStateStorageBytes)
    {
        if (!Validate())
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.InvalidPlan,
                OwnedBootStackSwitchState.Planned);
        }

        OwnedBootStackSwitchStatus pointerStatus =
            ValidateStateStoragePointer(
                stateStorage,
                availableStateStorageBytes,
                out AddressValueStatus addressStatus,
                out ulong stateStorageAddress);
        if (pointerStatus != OwnedBootStackSwitchStatus.Success)
        {
            return StateFailure(
                pointerStatus,
                OwnedBootStackSwitchState.Planned,
                default,
                addressStatus);
        }

        TryGetRaw(_stateStorageStart, out ulong expectedStorageAddress);
        if (stateStorageAddress != expectedStorageAddress)
        {
            return StateFailure(
                OwnedBootStackSwitchStatus
                    .StateRecordStorageMismatch,
                OwnedBootStackSwitchState.Planned,
                default,
                AddressValueStatus.InvalidAddress);
        }

        if (!IsZeroedStateStorage(stateStorage))
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.StateRecordNotEmpty,
                OwnedBootStackSwitchState.Planned);
        }

        *stateStorage = OwnedBootStackSwitchStateRecord.Create(
            stateStorageAddress,
            this);
        return OwnedBootStackSwitchStateResult.Success(
            OwnedBootStackSwitchState.Uninitialized,
            OwnedBootStackSwitchState.Planned,
            default,
            stateStorageAddress,
            stateStorage->TransitionCount,
            this);
    }

    public unsafe OwnedBootStackSwitchStateResult Arm(
        OwnedBootStackSwitchStateRecord* stateStorage,
        ulong availableStateStorageBytes,
        OwnedBootStackSwitchBytePlanResult verification)
    {
        OwnedBootStackSwitchStatus stateStatus = ValidateBoundState(
            stateStorage,
            availableStateStorageBytes,
            out AddressValueStatus addressStatus,
            out ulong stateStorageAddress);
        if (stateStatus != OwnedBootStackSwitchStatus.Success)
        {
            return StateFailure(
                stateStatus,
                OwnedBootStackSwitchState.Armed,
                default,
                addressStatus);
        }

        OwnedBootStackSwitchState previousState =
            stateStorage->State;
        if (previousState == OwnedBootStackSwitchState.Armed)
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.RepeatedArm,
                previousState,
                OwnedBootStackSwitchState.Armed,
                stateStorageAddress,
                stateStorage->TransitionCount);
        }

        if (previousState != OwnedBootStackSwitchState.Planned)
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.IllegalArmState,
                previousState,
                OwnedBootStackSwitchState.Armed,
                stateStorageAddress,
                stateStorage->TransitionCount);
        }

        if (!verification.Succeeded
            || verification.Operation
                != OwnedBootStackSwitchByteOperation.Verify)
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.InvalidBytePlanEvidence,
                previousState,
                OwnedBootStackSwitchState.Armed,
                stateStorageAddress,
                stateStorage->TransitionCount);
        }

        if (!verification.Plan.Equals(this))
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.PlanEvidenceMismatch,
                previousState,
                OwnedBootStackSwitchState.Armed,
                stateStorageAddress,
                stateStorage->TransitionCount);
        }

        stateStorage->Advance(
            OwnedBootStackSwitchState.Armed,
            stateStorage->TransitionCount + 1,
            default,
            OwnedBootStackSwitchTerminalCause.Uninitialized,
            OwnedBootStackSwitchStatus.Uninitialized);
        return OwnedBootStackSwitchStateResult.Success(
            previousState,
            OwnedBootStackSwitchState.Armed,
            default,
            stateStorageAddress,
            stateStorage->TransitionCount,
            this);
    }

    public unsafe OwnedBootStackSwitchStateResult RecordTransfer(
        OwnedBootStackSwitchStateRecord* stateStorage,
        ulong availableStateStorageBytes)
    {
        OwnedBootStackSwitchStatus stateStatus = ValidateBoundState(
            stateStorage,
            availableStateStorageBytes,
            out AddressValueStatus addressStatus,
            out ulong stateStorageAddress);
        if (stateStatus != OwnedBootStackSwitchStatus.Success)
        {
            return StateFailure(
                stateStatus,
                OwnedBootStackSwitchState.TransferRecorded,
                default,
                addressStatus);
        }

        OwnedBootStackSwitchState previousState =
            stateStorage->State;
        if (previousState == OwnedBootStackSwitchState.Planned)
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.TransferBeforeArm,
                previousState,
                OwnedBootStackSwitchState.TransferRecorded,
                stateStorageAddress,
                stateStorage->TransitionCount);
        }

        if (previousState
                == OwnedBootStackSwitchState.TransferRecorded
            || previousState
                == OwnedBootStackSwitchState.ContinuationObserved)
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.RepeatedTransfer,
                previousState,
                OwnedBootStackSwitchState.TransferRecorded,
                stateStorageAddress,
                stateStorage->TransitionCount);
        }

        if (previousState != OwnedBootStackSwitchState.Armed)
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.IllegalTransferState,
                previousState,
                OwnedBootStackSwitchState.TransferRecorded,
                stateStorageAddress,
                stateStorage->TransitionCount);
        }

        stateStorage->Advance(
            OwnedBootStackSwitchState.TransferRecorded,
            stateStorage->TransitionCount + 1,
            default,
            OwnedBootStackSwitchTerminalCause.Uninitialized,
            OwnedBootStackSwitchStatus.Uninitialized);
        return OwnedBootStackSwitchStateResult.Success(
            previousState,
            OwnedBootStackSwitchState.TransferRecorded,
            default,
            stateStorageAddress,
            stateStorage->TransitionCount,
            this);
    }

    public unsafe OwnedBootStackSwitchStateResult
        AuditContinuationRsp(
            OwnedBootStackSwitchStateRecord* stateStorage,
            ulong availableStateStorageBytes,
            VirtualAddress observedRsp)
    {
        OwnedBootStackSwitchStatus stateStatus = ValidateBoundState(
            stateStorage,
            availableStateStorageBytes,
            out AddressValueStatus addressStatus,
            out ulong stateStorageAddress);
        if (stateStatus != OwnedBootStackSwitchStatus.Success)
        {
            return StateFailure(
                stateStatus,
                OwnedBootStackSwitchState.ContinuationObserved,
                observedRsp,
                addressStatus);
        }

        OwnedBootStackSwitchState previousState =
            stateStorage->State;
        if (previousState == OwnedBootStackSwitchState.Planned
            || previousState == OwnedBootStackSwitchState.Armed)
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.ObservationBeforeTransfer,
                previousState,
                OwnedBootStackSwitchState.ContinuationObserved,
                stateStorageAddress,
                stateStorage->TransitionCount,
                observedRsp);
        }

        if (previousState
            == OwnedBootStackSwitchState.ContinuationObserved)
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.RepeatedObservation,
                previousState,
                OwnedBootStackSwitchState.ContinuationObserved,
                stateStorageAddress,
                stateStorage->TransitionCount,
                observedRsp);
        }

        if (previousState
            != OwnedBootStackSwitchState.TransferRecorded)
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.InvalidState,
                previousState,
                OwnedBootStackSwitchState.ContinuationObserved,
                stateStorageAddress,
                stateStorage->TransitionCount,
                observedRsp);
        }

        OwnedBootStackSwitchStatus auditStatus =
            ValidateObservedRsp(
                observedRsp,
                out addressStatus);
        if (auditStatus != OwnedBootStackSwitchStatus.Success)
        {
            stateStorage->Advance(
                OwnedBootStackSwitchState.Failed,
                stateStorage->TransitionCount + 1,
                observedRsp,
                OwnedBootStackSwitchTerminalCause
                    .ContinuationRspAuditFailed,
                auditStatus);
            return OwnedBootStackSwitchStateResult.Fail(
                auditStatus,
                addressStatus,
                previousState,
                OwnedBootStackSwitchState.ContinuationObserved,
                OwnedBootStackSwitchState.Failed,
                observedRsp,
                stateStorageAddress,
                stateStorage->TransitionCount,
                this);
        }

        stateStorage->Advance(
            OwnedBootStackSwitchState.ContinuationObserved,
            stateStorage->TransitionCount + 1,
            observedRsp,
            OwnedBootStackSwitchTerminalCause.Uninitialized,
            OwnedBootStackSwitchStatus.Uninitialized);
        return OwnedBootStackSwitchStateResult.Success(
            previousState,
            OwnedBootStackSwitchState.ContinuationObserved,
            observedRsp,
            stateStorageAddress,
            stateStorage->TransitionCount,
            this);
    }

    public unsafe OwnedBootStackSwitchStateResult
        ReportUnexpectedReturn(
            OwnedBootStackSwitchStateRecord* stateStorage,
            ulong availableStateStorageBytes)
    {
        OwnedBootStackSwitchStatus stateStatus = ValidateBoundState(
            stateStorage,
            availableStateStorageBytes,
            out AddressValueStatus addressStatus,
            out ulong stateStorageAddress);
        if (stateStatus != OwnedBootStackSwitchStatus.Success)
        {
            return StateFailure(
                stateStatus,
                OwnedBootStackSwitchState.Failed,
                default,
                addressStatus);
        }

        OwnedBootStackSwitchState previousState =
            stateStorage->State;
        if (previousState == OwnedBootStackSwitchState.Failed)
        {
            return StateFailure(
                stateStorage->TerminalCause
                        == OwnedBootStackSwitchTerminalCause
                            .UnexpectedReturnReported
                    ? OwnedBootStackSwitchStatus
                        .RepeatedUnexpectedReturn
                    : OwnedBootStackSwitchStatus.InvalidState,
                previousState,
                OwnedBootStackSwitchState.Failed,
                stateStorageAddress,
                stateStorage->TransitionCount);
        }

        if (previousState
            != OwnedBootStackSwitchState.TransferRecorded)
        {
            return StateFailure(
                OwnedBootStackSwitchStatus.InvalidState,
                previousState,
                OwnedBootStackSwitchState.Failed,
                stateStorageAddress,
                stateStorage->TransitionCount);
        }

        stateStorage->Advance(
            OwnedBootStackSwitchState.Failed,
            stateStorage->TransitionCount + 1,
            default,
            OwnedBootStackSwitchTerminalCause
                .UnexpectedReturnReported,
            OwnedBootStackSwitchStatus.UnexpectedReturn);
        return OwnedBootStackSwitchStateResult.Fail(
            OwnedBootStackSwitchStatus.UnexpectedReturn,
            AddressValueStatus.Uninitialized,
            previousState,
            OwnedBootStackSwitchState.Failed,
            OwnedBootStackSwitchState.Failed,
            default,
            stateStorageAddress,
            stateStorage->TransitionCount,
            this);
    }

    public bool Equals(OwnedBootStackSwitchPlan other)
    {
        return RegionEquals(
                _codeAllocation,
                other._codeAllocation)
            && RegionEquals(
                _dataAllocation,
                other._dataAllocation)
            && _stubReservation.Equals(other._stubReservation)
            && _stackLayout.Equals(other._stackLayout)
            && HandoffEquals(_handoff, other._handoff)
            && _stateStorageStart.Equals(
                other._stateStorageStart)
            && _stateStorageEndExclusive.Equals(
                other._stateStorageEndExclusive)
            && _stateStorageByteLength
                == other._stateStorageByteLength
            && _stateStorageProvenance
                == other._stateStorageProvenance
            && _stateStorageReserved0
                == other._stateStorageReserved0
            && _stateStorageReserved1
                == other._stateStorageReserved1
            && _stateStorageReserved2
                == other._stateStorageReserved2
            && _stateStorageReserved3
                == other._stateStorageReserved3
            && _stateStorageReserved4
                == other._stateStorageReserved4
            && _stateStorageReserved5
                == other._stateStorageReserved5
            && _stateStorageReserved6
                == other._stateStorageReserved6
            && _stubPhysicalEntry.Equals(other._stubPhysicalEntry)
            && _stubVirtualEntry.Equals(other._stubVirtualEntry)
            && _codeVirtualEndExclusive.Equals(
                other._codeVirtualEndExclusive)
            && _dataVirtualEndExclusive.Equals(
                other._dataVirtualEndExclusive)
            && _guardVirtualStart.Equals(
                other._guardVirtualStart)
            && _guardVirtualEndExclusive.Equals(
                other._guardVirtualEndExclusive)
            && _usableStackVirtualStart.Equals(
                other._usableStackVirtualStart)
            && _usableStackVirtualEndExclusive.Equals(
                other._usableStackVirtualEndExclusive)
            && _preTransferStackPointer.Equals(
                other._preTransferStackPointer)
            && _expectedContinuationStackPointer.Equals(
                other._expectedContinuationStackPointer)
            && _stubEntryOffset == other._stubEntryOffset
            && _declaredStubByteLength
                == other._declaredStubByteLength;
    }

    internal bool Validate()
    {
        if (!_stackLayout.IsValid
            || _stackLayout.Lifetime != OwnedBootStackLifetime.Ready
            || !_stubReservation.IsValid
            || _stubReservation.Kind
                != OS.Kernel.Memory.BootReservationKind
                    .BootStackSwitchStub
            || !_handoff.IsValid
            || _stateStorageProvenance
                != OwnedBootStackSwitchStateStorageProvenance
                    .CallerAttestedStaticOwnedData
            || _stateStorageByteLength
                != OwnedBootStackSwitchStateRecord.StructureSize
            || _stateStorageReserved0 != 0
            || _stateStorageReserved1 != 0
            || _stateStorageReserved2 != 0
            || _stateStorageReserved3 != 0
            || _stateStorageReserved4 != 0
            || _stateStorageReserved5 != 0
            || _stateStorageReserved6 != 0
            || _declaredStubByteLength != RequiredStubByteLength)
        {
            return false;
        }

        OwnedBootStackSwitchStatus codeStatus =
            ValidateAllocationRecord(
                _codeAllocation,
                isCode: true,
                out AddressValueStatus codeAddressStatus,
                out PhysicalAddress codePhysicalStart,
                out PhysicalAddress codePhysicalEnd,
                out VirtualAddress codeVirtualStart,
                out VirtualAddress codeVirtualEnd);
        OwnedBootStackSwitchStatus dataStatus =
            ValidateAllocationRecord(
                _dataAllocation,
                isCode: false,
                out AddressValueStatus dataAddressStatus,
                out PhysicalAddress dataPhysicalStart,
                out PhysicalAddress dataPhysicalEnd,
                out VirtualAddress dataVirtualStart,
                out VirtualAddress dataVirtualEnd);
        if (codeStatus != OwnedBootStackSwitchStatus.Success
            || dataStatus != OwnedBootStackSwitchStatus.Success
            || codeAddressStatus != AddressValueStatus.Success
            || dataAddressStatus != AddressValueStatus.Success
            || RangesOverlap(
                _codeAllocation.PhysicalStart,
                _codeAllocation.PhysicalEndExclusive,
                _dataAllocation.PhysicalStart,
                _dataAllocation.PhysicalEndExclusive)
            || RangesOverlap(
                _codeAllocation.VirtualStart,
                _codeAllocation.VirtualStart
                    + _codeAllocation.ByteLength,
                _dataAllocation.VirtualStart,
                _dataAllocation.VirtualStart
                    + _dataAllocation.ByteLength)
            || !dataPhysicalStart.Equals(_stackLayout.BackingStart)
            || !dataPhysicalEnd.Equals(
                _stackLayout.BackingEndExclusive)
            || !_stubReservation.PhysicalStart.Equals(
                codePhysicalStart)
            || !_stubReservation.PhysicalEndExclusive.Equals(
                codePhysicalEnd)
            || _stubReservation.PageCount
                != _codeAllocation.PageCount
            || _stubReservation.ByteLength
                != _codeAllocation.ByteLength
            || _dataAllocation.PageCount
                != _stackLayout.BackingPageCount
            || _dataAllocation.ByteLength
                != _stackLayout.BackingByteLength
            || _stubEntryOffset >= _codeAllocation.ByteLength
            || _stubEntryOffset
                > MaximumUnsignedLong - _declaredStubByteLength
            || _stubEntryOffset + _declaredStubByteLength
                > _codeAllocation.ByteLength)
        {
            return false;
        }

        if (codePhysicalStart.TryAdd(
                _stubEntryOffset,
                out PhysicalAddress expectedStubPhysical)
                != AddressValueStatus.Success
            || codeVirtualStart.TryAdd(
                _stubEntryOffset,
                out VirtualAddress expectedStubVirtual)
                != AddressValueStatus.Success
            || !expectedStubPhysical.Equals(_stubPhysicalEntry)
            || !expectedStubVirtual.Equals(_stubVirtualEntry)
            || !_stubPhysicalEntry.IsAligned(
                StubEntryAlignmentBytes)
            || !_stubVirtualEntry.IsAligned(
                StubEntryAlignmentBytes)
            || !codeVirtualEnd.Equals(_codeVirtualEndExclusive)
            || !dataVirtualEnd.Equals(_dataVirtualEndExclusive))
        {
            return false;
        }

        VirtualAddress expectedGuardStart = dataVirtualStart;
        if (expectedGuardStart.TryGetEndExclusive(
                _stackLayout.GuardReservation.ByteLength,
                out VirtualAddress expectedGuardEnd)
                != AddressValueStatus.Success
            || !expectedGuardStart.Equals(_guardVirtualStart)
            || !expectedGuardEnd.Equals(
                _guardVirtualEndExclusive)
            || !expectedGuardEnd.Equals(
                _usableStackVirtualStart)
            || !dataVirtualEnd.Equals(
                _usableStackVirtualEndExclusive)
            || !TryGetRaw(
                _usableStackVirtualStart,
                out ulong usableStart)
            || !TryGetRaw(
                _usableStackVirtualEndExclusive,
                out ulong usableEnd)
            || usableEnd < RequiredContinuationFrameBytes
            || usableEnd - RequiredContinuationFrameBytes
                < usableStart)
        {
            return false;
        }

        ulong expectedPreTransfer =
            usableEnd - X64ShadowSpaceBytes;
        ulong expectedContinuationRsp =
            expectedPreTransfer - FakeReturnAddressBytes;
        if (!TryGetRaw(
                _preTransferStackPointer,
                out ulong actualPreTransfer)
            || !TryGetRaw(
                _expectedContinuationStackPointer,
                out ulong actualContinuationRsp)
            || actualPreTransfer != expectedPreTransfer
            || actualContinuationRsp != expectedContinuationRsp
            || (actualPreTransfer
                & (X64StackAlignmentBytes - 1)) != 0
            || (actualContinuationRsp
                & (X64StackAlignmentBytes - 1))
                != ContinuationEntryAlignmentRemainder)
        {
            return false;
        }

        if (!_handoff.StubEntry.Equals(_stubVirtualEntry)
            || !_handoff.UsableStackStart.Equals(
                _usableStackVirtualStart)
            || !_handoff.UsableStackEndExclusive.Equals(
                _usableStackVirtualEndExclusive)
            || !_handoff.PreTransferStackPointer.Equals(
                _preTransferStackPointer)
            || !_handoff.ExpectedContinuationStackPointer.Equals(
                _expectedContinuationStackPointer)
            || !TryGetRaw(
                _handoff.Continuation,
                out ulong continuationRaw)
            || IsAddressInside(
                continuationRaw,
                _codeAllocation.VirtualStart,
                _codeAllocation.VirtualStart
                    + _codeAllocation.ByteLength)
            || IsAddressInside(
                continuationRaw,
                _dataAllocation.VirtualStart,
                _dataAllocation.VirtualStart
                    + _dataAllocation.ByteLength)
            || !TryGetRaw(
                _handoff.StorageStart,
                out ulong handoffStart)
            || !TryGetRaw(
                _handoff.StorageEndExclusive,
                out ulong handoffEnd)
            || RangesOverlap(
                handoffStart,
                handoffEnd,
                _codeAllocation.VirtualStart,
                _codeAllocation.VirtualStart
                    + _codeAllocation.ByteLength)
            || RangesOverlap(
                handoffStart,
                handoffEnd,
                _dataAllocation.VirtualStart,
                _dataAllocation.VirtualStart
                    + _dataAllocation.ByteLength)
            || IsAddressInside(
                continuationRaw,
                handoffStart,
                handoffEnd)
            || !TryGetRaw(
                _stateStorageStart,
                out ulong stateStorageStart)
            || !TryGetRaw(
                _stateStorageEndExclusive,
                out ulong stateStorageEnd)
            || !_stateStorageStart.IsAligned(
                OwnedBootStackSwitchStateRecord
                    .RequiredStorageAlignment)
            || _stateStorageStart.TryGetEndExclusive(
                _stateStorageByteLength,
                out VirtualAddress expectedStateStorageEnd)
                != AddressValueStatus.Success
            || !expectedStateStorageEnd.Equals(
                _stateStorageEndExclusive)
            || RangesOverlap(
                stateStorageStart,
                stateStorageEnd,
                _codeAllocation.VirtualStart,
                _codeAllocation.VirtualStart
                    + _codeAllocation.ByteLength)
            || RangesOverlap(
                stateStorageStart,
                stateStorageEnd,
                _dataAllocation.VirtualStart,
                _dataAllocation.VirtualStart
                    + _dataAllocation.ByteLength)
            || RangesOverlap(
                stateStorageStart,
                stateStorageEnd,
                handoffStart,
                handoffEnd)
            || IsAddressInside(
                continuationRaw,
                stateStorageStart,
                stateStorageEnd))
        {
            return false;
        }

        return true;
    }

    private OwnedBootStackSwitchStateResult StateFailure(
        OwnedBootStackSwitchStatus status,
        OwnedBootStackSwitchState requestedState,
        VirtualAddress observedRsp = default,
        AddressValueStatus addressStatus =
            AddressValueStatus.Uninitialized)
    {
        return OwnedBootStackSwitchStateResult.Fail(
            status,
            addressStatus,
            OwnedBootStackSwitchState.Uninitialized,
            requestedState,
            OwnedBootStackSwitchState.Uninitialized,
            observedRsp,
            0,
            0,
            this);
    }

    private OwnedBootStackSwitchStateResult StateFailure(
        OwnedBootStackSwitchStatus status,
        OwnedBootStackSwitchState previousState,
        OwnedBootStackSwitchState requestedState,
        ulong stateStorageAddress,
        ulong transitionCount,
        VirtualAddress observedRsp = default,
        AddressValueStatus addressStatus =
            AddressValueStatus.Uninitialized)
    {
        return OwnedBootStackSwitchStateResult.Fail(
            status,
            addressStatus,
            previousState,
            requestedState,
            previousState,
            observedRsp,
            stateStorageAddress,
            transitionCount,
            this);
    }

    private unsafe OwnedBootStackSwitchStatus
        ValidateMaterializedPage(
            byte* codePageBase,
            ulong availablePageCapacity,
            ulong materializedPageByteLength)
    {
        if (!Validate())
        {
            return OwnedBootStackSwitchStatus.InvalidPlan;
        }

        if (codePageBase == null)
        {
            return OwnedBootStackSwitchStatus.MissingByteBuffer;
        }

        if (materializedPageByteLength
            != _codeAllocation.ByteLength)
        {
            return OwnedBootStackSwitchStatus
                .MaterializedPageLengthMismatch;
        }

        if (availablePageCapacity < materializedPageByteLength)
        {
            return OwnedBootStackSwitchStatus.ByteBufferTooSmall;
        }

        ulong pageAddress = (ulong)codePageBase;
        if (pageAddress
            > MaximumUnsignedLong - materializedPageByteLength)
        {
            return OwnedBootStackSwitchStatus
                .ByteBufferAddressRangeOverflow;
        }

        if ((pageAddress
                & (BootMemoryRegionRecord.PageSize - 1)) != 0)
        {
            return OwnedBootStackSwitchStatus
                .InvalidMaterializedPageAddress;
        }

        if (pageAddress != _codeAllocation.VirtualStart)
        {
            return OwnedBootStackSwitchStatus.MaterializedPageMismatch;
        }

        return OwnedBootStackSwitchStatus.Success;
    }

    private static unsafe OwnedBootStackSwitchStatus
        ValidateStateStoragePointer(
            OwnedBootStackSwitchStateRecord* stateStorage,
            ulong availableStateStorageBytes,
            out AddressValueStatus addressStatus,
            out ulong stateStorageAddress)
    {
        addressStatus = AddressValueStatus.Uninitialized;
        stateStorageAddress = 0;

        if (stateStorage == null)
        {
            return OwnedBootStackSwitchStatus.MissingStateRecord;
        }

        if (availableStateStorageBytes
            < OwnedBootStackSwitchStateRecord.StructureSize)
        {
            return OwnedBootStackSwitchStatus
                .StateRecordCapacityTooSmall;
        }

        stateStorageAddress = (ulong)stateStorage;
        if ((stateStorageAddress
                & (OwnedBootStackSwitchStateRecord
                    .RequiredStorageAlignment - 1)) != 0)
        {
            addressStatus = AddressValueStatus.MisalignedAddress;
            return OwnedBootStackSwitchStatus.MisalignedStateRecord;
        }

        if (stateStorageAddress
            > MaximumUnsignedLong
                - OwnedBootStackSwitchStateRecord.StructureSize)
        {
            addressStatus = AddressValueStatus.AddressOverflow;
            return OwnedBootStackSwitchStatus
                .StateRecordAddressRangeOverflow;
        }

        addressStatus = AddressValueStatus.Success;
        return OwnedBootStackSwitchStatus.Success;
    }

    private static unsafe bool IsZeroedStateStorage(
        OwnedBootStackSwitchStateRecord* stateStorage)
    {
        byte* stateBytes = (byte*)stateStorage;
        for (ulong index = 0;
            index < OwnedBootStackSwitchStateRecord.StructureSize;
            index++)
        {
            if (stateBytes[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private unsafe OwnedBootStackSwitchStatus ValidateBoundState(
        OwnedBootStackSwitchStateRecord* stateStorage,
        ulong availableStateStorageBytes,
        out AddressValueStatus addressStatus,
        out ulong stateStorageAddress)
    {
        if (!Validate())
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            stateStorageAddress = 0;
            return OwnedBootStackSwitchStatus.InvalidPlan;
        }

        OwnedBootStackSwitchStatus pointerStatus =
            ValidateStateStoragePointer(
                stateStorage,
                availableStateStorageBytes,
                out addressStatus,
                out stateStorageAddress);
        if (pointerStatus != OwnedBootStackSwitchStatus.Success)
        {
            return pointerStatus;
        }

        TryGetRaw(_stateStorageStart, out ulong expectedStorageAddress);
        if (stateStorageAddress != expectedStorageAddress)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return OwnedBootStackSwitchStatus
                .StateRecordStorageMismatch;
        }

        if (stateStorage->StorageAddress != stateStorageAddress)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return OwnedBootStackSwitchStatus
                .StateRecordStorageMismatch;
        }

        if (!stateStorage->Plan.Equals(this))
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return OwnedBootStackSwitchStatus.StateRecordPlanMismatch;
        }

        if (!stateStorage->IsValid)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return OwnedBootStackSwitchStatus.InvalidStateRecord;
        }

        return OwnedBootStackSwitchStatus.Success;
    }

    internal OwnedBootStackSwitchStatus ValidateObservedRsp(
        VirtualAddress observedRsp,
        out AddressValueStatus addressStatus)
    {
        addressStatus = AddressValueStatus.Uninitialized;
        if (!observedRsp.IsValid
            || !TryGetRaw(observedRsp, out ulong observedRaw))
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return OwnedBootStackSwitchStatus.InvalidObservedRsp;
        }

        TryGetRaw(_guardVirtualStart, out ulong guardStart);
        TryGetRaw(_guardVirtualEndExclusive, out ulong guardEnd);
        TryGetRaw(_usableStackVirtualStart, out ulong stackStart);
        TryGetRaw(_usableStackVirtualEndExclusive, out ulong stackEnd);
        if (IsAddressInside(observedRaw, guardStart, guardEnd))
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return OwnedBootStackSwitchStatus.ObservedRspInGuard;
        }

        if (!IsAddressInside(observedRaw, stackStart, stackEnd))
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return OwnedBootStackSwitchStatus
                .ObservedRspOutsideUsableStack;
        }

        if ((observedRaw & (X64StackAlignmentBytes - 1))
            != ContinuationEntryAlignmentRemainder)
        {
            addressStatus = AddressValueStatus.MisalignedAddress;
            return OwnedBootStackSwitchStatus.MisalignedObservedRsp;
        }

        if (observedRaw
            > MaximumUnsignedLong - RequiredContinuationFrameBytes)
        {
            addressStatus = AddressValueStatus.AddressOverflow;
            return OwnedBootStackSwitchStatus.ShadowSpaceOverflow;
        }

        ulong frameEnd = observedRaw + RequiredContinuationFrameBytes;
        if (frameEnd > stackEnd)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return OwnedBootStackSwitchStatus.ShadowSpaceOutOfRange;
        }

        TryGetRaw(
            _expectedContinuationStackPointer,
            out ulong expectedRaw);
        if (observedRaw != expectedRaw)
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return OwnedBootStackSwitchStatus.ObservedRspMismatch;
        }

        addressStatus = AddressValueStatus.Success;
        return OwnedBootStackSwitchStatus.Success;
    }

    private static OwnedBootStackSwitchStatus
        ValidateAllocationRecord(
            BootMemoryRegionRecord region,
            bool isCode,
            out AddressValueStatus addressStatus,
            out PhysicalAddress physicalStart,
            out PhysicalAddress physicalEnd,
            out VirtualAddress virtualStart,
            out VirtualAddress virtualEnd)
    {
        addressStatus = AddressValueStatus.Uninitialized;
        physicalStart = default;
        physicalEnd = default;
        virtualStart = default;
        virtualEnd = default;

        uint requiredType = isCode
            ? (uint)UefiMemoryType.LoaderCode
            : (uint)UefiMemoryType.LoaderData;
        if (region.UefiType != requiredType)
        {
            return isCode
                ? OwnedBootStackSwitchStatus.InvalidCodeMemoryType
                : OwnedBootStackSwitchStatus.InvalidDataMemoryType;
        }

        addressStatus =
            PhysicalAddress.TryCreateFromUefiBoundary(
                region.PhysicalStart,
                out physicalStart);
        if (addressStatus != AddressValueStatus.Success)
        {
            return isCode
                ? OwnedBootStackSwitchStatus.InvalidCodePhysicalStart
                : OwnedBootStackSwitchStatus.InvalidDataPhysicalStart;
        }

        if (!physicalStart.IsAligned(BootMemoryRegionRecord.PageSize))
        {
            addressStatus = AddressValueStatus.MisalignedAddress;
            return isCode
                ? OwnedBootStackSwitchStatus.MisalignedCodePhysicalStart
                : OwnedBootStackSwitchStatus.MisalignedDataPhysicalStart;
        }

        addressStatus =
            VirtualAddress.TryCreateFromUefiBoundary(
                region.VirtualStart,
                out virtualStart);
        if (addressStatus != AddressValueStatus.Success)
        {
            return isCode
                ? OwnedBootStackSwitchStatus.InvalidCodeVirtualStart
                : OwnedBootStackSwitchStatus.InvalidDataVirtualStart;
        }

        if (!virtualStart.IsAligned(BootMemoryRegionRecord.PageSize))
        {
            addressStatus = AddressValueStatus.MisalignedAddress;
            return isCode
                ? OwnedBootStackSwitchStatus.MisalignedCodeVirtualStart
                : OwnedBootStackSwitchStatus.MisalignedDataVirtualStart;
        }

        if (region.PageCount == 0)
        {
            addressStatus = AddressValueStatus.ZeroLength;
            return isCode
                ? OwnedBootStackSwitchStatus.InvalidCodePageCount
                : OwnedBootStackSwitchStatus.InvalidDataPageCount;
        }

        if (region.PageCount
            > MaximumUnsignedLong / BootMemoryRegionRecord.PageSize)
        {
            addressStatus = AddressValueStatus.AddressOverflow;
            return isCode
                ? OwnedBootStackSwitchStatus.CodeByteCountOverflow
                : OwnedBootStackSwitchStatus.DataByteCountOverflow;
        }

        if (isCode
            && region.PageCount != RequiredCodePageCount)
        {
            addressStatus = AddressValueStatus.Uninitialized;
            return OwnedBootStackSwitchStatus.InvalidCodePageCount;
        }

        ulong expectedByteLength =
            region.PageCount * BootMemoryRegionRecord.PageSize;
        if (region.ByteLength != expectedByteLength)
        {
            addressStatus = AddressValueStatus.Uninitialized;
            return isCode
                ? OwnedBootStackSwitchStatus.CodeByteLengthMismatch
                : OwnedBootStackSwitchStatus.DataByteLengthMismatch;
        }

        addressStatus = physicalStart.TryGetEndExclusive(
            expectedByteLength,
            out physicalEnd);
        if (addressStatus != AddressValueStatus.Success)
        {
            return isCode
                ? OwnedBootStackSwitchStatus.CodePhysicalEndOverflow
                : OwnedBootStackSwitchStatus.DataPhysicalEndOverflow;
        }

        if (PhysicalAddress.TryCreateFromUefiBoundary(
                region.PhysicalEndExclusive,
                out PhysicalAddress recordedPhysicalEnd)
                != AddressValueStatus.Success
            || !recordedPhysicalEnd.Equals(physicalEnd))
        {
            addressStatus = AddressValueStatus.InvalidAddress;
            return isCode
                ? OwnedBootStackSwitchStatus.CodePhysicalEndMismatch
                : OwnedBootStackSwitchStatus.DataPhysicalEndMismatch;
        }

        addressStatus = virtualStart.TryGetEndExclusive(
            expectedByteLength,
            out virtualEnd);
        if (addressStatus != AddressValueStatus.Success)
        {
            return isCode
                ? OwnedBootStackSwitchStatus.CodeVirtualEndOverflow
                : OwnedBootStackSwitchStatus.DataVirtualEndOverflow;
        }

        addressStatus = AddressValueStatus.Success;
        return OwnedBootStackSwitchStatus.Success;
    }

    private static byte GetExpectedByte(ulong index)
    {
        if (index == 0) return StubByte00;
        if (index == 1) return StubByte01;
        if (index == 2) return StubByte02;
        if (index == 3) return StubByte03;
        if (index == 4) return StubByte04;
        if (index == 5) return StubByte05;
        if (index == 6) return StubByte06;
        if (index == 7) return StubByte07;
        if (index == 8) return StubByte08;
        if (index == 9) return StubByte09;
        if (index == 10) return StubByte10;
        if (index == 11) return StubByte11;
        if (index == 12) return StubByte12;
        return StubByte13;
    }

    private static bool RegionEquals(
        BootMemoryRegionRecord left,
        BootMemoryRegionRecord right)
    {
        return left.UefiType == right.UefiType
            && left.PhysicalStart == right.PhysicalStart
            && left.VirtualStart == right.VirtualStart
            && left.PageCount == right.PageCount
            && left.Attribute == right.Attribute
            && left.ByteLength == right.ByteLength
            && left.PhysicalEndExclusive
                == right.PhysicalEndExclusive;
    }

    private static bool HandoffEquals(
        OwnedBootStackSwitchHandoff left,
        OwnedBootStackSwitchHandoff right)
    {
        return left.Size == right.Size
            && left.Version == right.Version
            && left.StorageStart.Equals(right.StorageStart)
            && left.StorageEndExclusive.Equals(
                right.StorageEndExclusive)
            && left.StorageByteLength == right.StorageByteLength
            && left.StubEntry.Equals(right.StubEntry)
            && left.UsableStackStart.Equals(
                right.UsableStackStart)
            && left.UsableStackEndExclusive.Equals(
                right.UsableStackEndExclusive)
            && left.PreTransferStackPointer.Equals(
                right.PreTransferStackPointer)
            && left.ExpectedContinuationStackPointer.Equals(
                right.ExpectedContinuationStackPointer)
            && left.Continuation.Equals(right.Continuation)
            && left.Provenance == right.Provenance
            && left.ContinuationContract
                == right.ContinuationContract;
    }

    private static bool RangesOverlap(
        ulong leftStart,
        ulong leftEndExclusive,
        ulong rightStart,
        ulong rightEndExclusive)
    {
        return leftStart < rightEndExclusive
            && rightStart < leftEndExclusive;
    }

    private static bool IsAddressInside(
        ulong address,
        ulong start,
        ulong endExclusive)
    {
        return address >= start && address < endExclusive;
    }

    private static bool TryGetRaw(
        VirtualAddress address,
        out ulong value)
    {
        return address.TryGetRawValueForUefiBoundary(out value)
            == AddressValueStatus.Success;
    }
}

/// <summary>
/// Caller-owned mutable one-shot state for one exact stack-switch plan.
/// The stored address binds the state bytes to their initialized location so
/// copying them elsewhere cannot replay the transition.
/// </summary>
public struct OwnedBootStackSwitchStateRecord
{
    public const uint CurrentVersion = 1;
    public const uint StructureSize = 536;
    public const ulong RequiredStorageAlignment = 8;

    private uint _size;
    private uint _version;
    private ulong _storageAddress;
    private ulong _transitionCount;
    private VirtualAddress _observedRsp;
    private OwnedBootStackSwitchPlan _plan;
    private OwnedBootStackSwitchState _state;
    private OwnedBootStackSwitchTerminalCause _terminalCause;
    private OwnedBootStackSwitchStatus _terminalStatus;
    private byte _reserved0;
    private byte _reserved1;
    private byte _reserved2;
    private byte _reserved3;
    private byte _reserved4;

    private OwnedBootStackSwitchStateRecord(
        ulong storageAddress,
        OwnedBootStackSwitchPlan plan)
    {
        _size = StructureSize;
        _version = CurrentVersion;
        _storageAddress = storageAddress;
        _transitionCount = 0;
        _observedRsp = default;
        _plan = plan;
        _state = OwnedBootStackSwitchState.Planned;
        _terminalCause =
            OwnedBootStackSwitchTerminalCause.Uninitialized;
        _terminalStatus = OwnedBootStackSwitchStatus.Uninitialized;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
    }

    public uint Size => _size;

    public uint Version => _version;

    public ulong StorageAddress => _storageAddress;

    public ulong TransitionCount => _transitionCount;

    public VirtualAddress ObservedRsp => _observedRsp;

    public OwnedBootStackSwitchPlan Plan => _plan;

    public OwnedBootStackSwitchState State => _state;

    public OwnedBootStackSwitchTerminalCause TerminalCause =>
        _terminalCause;

    public OwnedBootStackSwitchStatus TerminalStatus =>
        _terminalStatus;

    public bool IsValid
    {
        get
        {
            if (_size != StructureSize
                || _version != CurrentVersion
                || _storageAddress == 0
                || (_storageAddress
                    & (RequiredStorageAlignment - 1)) != 0
                || _storageAddress > ~0UL - StructureSize
                || !_plan.IsValid
                || _reserved0 != 0
                || _reserved1 != 0
                || _reserved2 != 0
                || _reserved3 != 0
                || _reserved4 != 0)
            {
                return false;
            }

            if (_plan.StateStorageStart
                    .TryGetRawValueForUefiBoundary(
                        out ulong plannedStorageAddress)
                    != AddressValueStatus.Success
                || plannedStorageAddress != _storageAddress
                || _plan.StateStorageByteLength != StructureSize)
            {
                return false;
            }

            if (_state == OwnedBootStackSwitchState.Planned)
            {
                return _transitionCount == 0
                    && !_observedRsp.IsValid
                    && HasNoTerminalCause();
            }

            if (_state == OwnedBootStackSwitchState.Armed)
            {
                return _transitionCount == 1
                    && !_observedRsp.IsValid
                    && HasNoTerminalCause();
            }

            if (_state
                == OwnedBootStackSwitchState.TransferRecorded)
            {
                return _transitionCount == 2
                    && !_observedRsp.IsValid
                    && HasNoTerminalCause();
            }

            if (_state
                == OwnedBootStackSwitchState.ContinuationObserved)
            {
                return _transitionCount == 3
                    && _observedRsp.IsValid
                    && _observedRsp.Equals(
                        _plan.ExpectedContinuationStackPointer)
                    && HasNoTerminalCause();
            }

            if (_state != OwnedBootStackSwitchState.Failed
                || _transitionCount != 3)
            {
                return false;
            }

            if (_terminalCause
                == OwnedBootStackSwitchTerminalCause
                    .UnexpectedReturnReported)
            {
                return _terminalStatus
                        == OwnedBootStackSwitchStatus.UnexpectedReturn
                    && !_observedRsp.IsValid;
            }

            return _terminalCause
                    == OwnedBootStackSwitchTerminalCause
                        .ContinuationRspAuditFailed
                && _plan.ValidateObservedRsp(
                    _observedRsp,
                    out AddressValueStatus terminalAddressStatus)
                    == _terminalStatus
                && terminalAddressStatus
                    != AddressValueStatus.Success;
        }
    }

    internal static OwnedBootStackSwitchStateRecord Create(
        ulong storageAddress,
        OwnedBootStackSwitchPlan plan)
    {
        return new OwnedBootStackSwitchStateRecord(
            storageAddress,
            plan);
    }

    internal void Advance(
        OwnedBootStackSwitchState state,
        ulong transitionCount,
        VirtualAddress observedRsp,
        OwnedBootStackSwitchTerminalCause terminalCause,
        OwnedBootStackSwitchStatus terminalStatus)
    {
        _state = state;
        _transitionCount = transitionCount;
        _observedRsp = observedRsp;
        _terminalCause = terminalCause;
        _terminalStatus = terminalStatus;
    }

    private bool HasNoTerminalCause()
    {
        return _terminalCause
                == OwnedBootStackSwitchTerminalCause.Uninitialized
            && _terminalStatus
                == OwnedBootStackSwitchStatus.Uninitialized;
    }

}

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

    private OwnedBootStackSwitchPlanResult(
        OwnedBootStackSwitchStatus status,
        AddressValueStatus addressStatus,
        OwnedBootStackLifetime stackLifetime,
        ulong allocationRecordCount,
        ulong availableAllocationRecordCapacity,
        ulong invalidAllocationRecordIndex,
        OwnedBootStackSwitchPlan plan)
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
        _availableAllocationRecordCapacity =
            availableAllocationRecordCapacity;
        _invalidAllocationRecordIndex =
            invalidAllocationRecordIndex;
        _plan = plan;
    }

    public OwnedBootStackSwitchStatus Status => _status;

    public AddressValueStatus AddressStatus => _addressStatus;

    public OwnedBootStackLifetime StackLifetime => _stackLifetime;

    public ulong AllocationRecordCount => _allocationRecordCount;

    public ulong AvailableAllocationRecordCapacity =>
        _availableAllocationRecordCapacity;

    public ulong RequiredAllocationRecordCapacity =>
        OwnedBootStackSwitchPlan.RequiredAllocationRecordCount;

    public ulong InvalidAllocationRecordIndex =>
        _invalidAllocationRecordIndex;

    public OwnedBootStackSwitchPlan Plan => _plan;

    public bool Succeeded =>
        _status == OwnedBootStackSwitchStatus.Success
        && _addressStatus == AddressValueStatus.Success
        && _stackLifetime == OwnedBootStackLifetime.Ready
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0
        && _reserved3 == 0
        && _reserved4 == 0
        && _allocationRecordCount
            == OwnedBootStackSwitchPlan
                .RequiredAllocationRecordCount
        && _availableAllocationRecordCapacity
            >= _allocationRecordCount
        && _invalidAllocationRecordIndex
            == OwnedBootStackSwitchPlan.NoIndex
        && _plan.IsValid;

    internal static OwnedBootStackSwitchPlanResult Success(
        OwnedBootStackLifetime stackLifetime,
        ulong allocationRecordCount,
        ulong availableAllocationRecordCapacity,
        OwnedBootStackSwitchPlan plan)
    {
        return new OwnedBootStackSwitchPlanResult(
            OwnedBootStackSwitchStatus.Success,
            AddressValueStatus.Success,
            stackLifetime,
            allocationRecordCount,
            availableAllocationRecordCapacity,
            OwnedBootStackSwitchPlan.NoIndex,
            plan);
    }

    internal static OwnedBootStackSwitchPlanResult Fail(
        OwnedBootStackSwitchStatus status,
        AddressValueStatus addressStatus,
        OwnedBootStackLifetime stackLifetime,
        ulong allocationRecordCount,
        ulong availableAllocationRecordCapacity,
        ulong invalidAllocationRecordIndex)
    {
        return new OwnedBootStackSwitchPlanResult(
            status,
            addressStatus,
            stackLifetime,
            allocationRecordCount,
            availableAllocationRecordCapacity,
            invalidAllocationRecordIndex,
            default);
    }
}

/// <summary>
/// Preserves exact byte-plan emission or readback evidence.
/// </summary>
public readonly struct OwnedBootStackSwitchBytePlanResult
{
    private readonly OwnedBootStackSwitchStatus _status;
    private readonly OwnedBootStackSwitchByteOperation _operation;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly ulong _codePageBufferAddress;
    private readonly ulong _materializedPageByteLength;
    private readonly ulong _availablePageCapacity;
    private readonly ulong _stubEntryOffset;
    private readonly ulong _materializedStubByteLength;
    private readonly ulong _bytesProcessed;
    private readonly ulong _mismatchByteOffset;
    private readonly OwnedBootStackSwitchPlan _plan;

    private OwnedBootStackSwitchBytePlanResult(
        OwnedBootStackSwitchStatus status,
        OwnedBootStackSwitchByteOperation operation,
        ulong codePageBufferAddress,
        ulong materializedPageByteLength,
        ulong availablePageCapacity,
        ulong stubEntryOffset,
        ulong materializedStubByteLength,
        ulong bytesProcessed,
        ulong mismatchByteOffset,
        OwnedBootStackSwitchPlan plan)
    {
        _status = status;
        _operation = operation;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _codePageBufferAddress = codePageBufferAddress;
        _materializedPageByteLength = materializedPageByteLength;
        _availablePageCapacity = availablePageCapacity;
        _stubEntryOffset = stubEntryOffset;
        _materializedStubByteLength = materializedStubByteLength;
        _bytesProcessed = bytesProcessed;
        _mismatchByteOffset = mismatchByteOffset;
        _plan = plan;
    }

    public OwnedBootStackSwitchStatus Status => _status;

    public OwnedBootStackSwitchByteOperation Operation => _operation;

    public ulong CodePageBufferAddress => _codePageBufferAddress;

    public ulong MaterializedPageByteLength =>
        _materializedPageByteLength;

    public ulong AvailablePageCapacity => _availablePageCapacity;

    public ulong StubEntryOffset => _stubEntryOffset;

    public ulong MaterializedStubByteLength =>
        _materializedStubByteLength;

    public ulong BytesProcessed => _bytesProcessed;

    public ulong MismatchByteOffset => _mismatchByteOffset;

    public OwnedBootStackSwitchPlan Plan => _plan;

    public bool Succeeded =>
        _status == OwnedBootStackSwitchStatus.Success
        && (_operation == OwnedBootStackSwitchByteOperation.Emit
            || _operation
                == OwnedBootStackSwitchByteOperation.Verify)
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0
        && _reserved3 == 0
        && _reserved4 == 0
        && _reserved5 == 0
        && _plan.IsValid
        && _codePageBufferAddress != 0
        && _codePageBufferAddress
            == _plan.CodeAllocation.VirtualStart
        && _materializedPageByteLength
            == _plan.CodeAllocation.ByteLength
        && _availablePageCapacity
            >= _materializedPageByteLength
        && _stubEntryOffset == _plan.StubEntryOffset
        && _materializedStubByteLength
            == OwnedBootStackSwitchPlan.RequiredStubByteLength
        && _bytesProcessed == _materializedStubByteLength
        && _mismatchByteOffset == OwnedBootStackSwitchPlan.NoIndex;

    internal static OwnedBootStackSwitchBytePlanResult Success(
        OwnedBootStackSwitchByteOperation operation,
        ulong codePageBufferAddress,
        ulong availablePageCapacity,
        ulong materializedPageByteLength,
        OwnedBootStackSwitchPlan plan)
    {
        return new OwnedBootStackSwitchBytePlanResult(
            OwnedBootStackSwitchStatus.Success,
            operation,
            codePageBufferAddress,
            materializedPageByteLength,
            availablePageCapacity,
            plan.StubEntryOffset,
            OwnedBootStackSwitchPlan.RequiredStubByteLength,
            OwnedBootStackSwitchPlan.RequiredStubByteLength,
            OwnedBootStackSwitchPlan.NoIndex,
            plan);
    }

    internal static OwnedBootStackSwitchBytePlanResult Fail(
        OwnedBootStackSwitchStatus status,
        OwnedBootStackSwitchByteOperation operation,
        ulong codePageBufferAddress,
        ulong availablePageCapacity,
        ulong materializedPageByteLength,
        OwnedBootStackSwitchPlan plan,
        ulong mismatchByteOffset =
            OwnedBootStackSwitchPlan.NoIndex)
    {
        return new OwnedBootStackSwitchBytePlanResult(
            status,
            operation,
            codePageBufferAddress,
            materializedPageByteLength,
            availablePageCapacity,
            plan.StubEntryOffset,
            OwnedBootStackSwitchPlan.RequiredStubByteLength,
            0,
            mismatchByteOffset,
            plan);
    }
}

/// <summary>
/// Preserves one-shot state-record transition and RSP-audit evidence.
/// The caller owns serialization of the bound mutable state storage.
/// </summary>
public readonly struct OwnedBootStackSwitchStateResult
{
    private readonly OwnedBootStackSwitchStatus _status;
    private readonly AddressValueStatus _addressStatus;
    private readonly OwnedBootStackSwitchState _previousState;
    private readonly OwnedBootStackSwitchState _requestedState;
    private readonly OwnedBootStackSwitchState _currentState;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly VirtualAddress _observedRsp;
    private readonly ulong _stateStorageAddress;
    private readonly ulong _transitionCount;
    private readonly OwnedBootStackSwitchPlan _plan;

    private OwnedBootStackSwitchStateResult(
        OwnedBootStackSwitchStatus status,
        AddressValueStatus addressStatus,
        OwnedBootStackSwitchState previousState,
        OwnedBootStackSwitchState requestedState,
        OwnedBootStackSwitchState currentState,
        VirtualAddress observedRsp,
        ulong stateStorageAddress,
        ulong transitionCount,
        OwnedBootStackSwitchPlan plan)
    {
        _status = status;
        _addressStatus = addressStatus;
        _previousState = previousState;
        _requestedState = requestedState;
        _currentState = currentState;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _observedRsp = observedRsp;
        _stateStorageAddress = stateStorageAddress;
        _transitionCount = transitionCount;
        _plan = plan;
    }

    public OwnedBootStackSwitchStatus Status => _status;

    public AddressValueStatus AddressStatus => _addressStatus;

    public OwnedBootStackSwitchState PreviousState =>
        _previousState;

    public OwnedBootStackSwitchState RequestedState =>
        _requestedState;

    public OwnedBootStackSwitchState CurrentState => _currentState;

    public VirtualAddress ObservedRsp => _observedRsp;

    public ulong StateStorageAddress => _stateStorageAddress;

    public ulong TransitionCount => _transitionCount;

    public OwnedBootStackSwitchPlan Plan => _plan;

    public bool Succeeded =>
        _status == OwnedBootStackSwitchStatus.Success
        && _addressStatus == AddressValueStatus.Success
        && _requestedState == _currentState
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0
        && _plan.IsValid
        && _stateStorageAddress != 0
        && (_stateStorageAddress
            & (OwnedBootStackSwitchStateRecord
                .RequiredStorageAlignment - 1)) == 0
        && _stateStorageAddress
            <= ~0UL - OwnedBootStackSwitchStateRecord.StructureSize
        && IsSuccessfulTransition(
            _previousState,
            _currentState,
            _observedRsp,
            _transitionCount,
            _plan);

    internal static OwnedBootStackSwitchStateResult Success(
        OwnedBootStackSwitchState previousState,
        OwnedBootStackSwitchState currentState,
        VirtualAddress observedRsp,
        ulong stateStorageAddress,
        ulong transitionCount,
        OwnedBootStackSwitchPlan plan)
    {
        return new OwnedBootStackSwitchStateResult(
            OwnedBootStackSwitchStatus.Success,
            AddressValueStatus.Success,
            previousState,
            currentState,
            currentState,
            observedRsp,
            stateStorageAddress,
            transitionCount,
            plan);
    }

    internal static OwnedBootStackSwitchStateResult Fail(
        OwnedBootStackSwitchStatus status,
        AddressValueStatus addressStatus,
        OwnedBootStackSwitchState previousState,
        OwnedBootStackSwitchState requestedState,
        OwnedBootStackSwitchState currentState,
        VirtualAddress observedRsp,
        ulong stateStorageAddress,
        ulong transitionCount,
        OwnedBootStackSwitchPlan plan)
    {
        return new OwnedBootStackSwitchStateResult(
            status,
            addressStatus,
            previousState,
            requestedState,
            currentState,
            observedRsp,
            stateStorageAddress,
            transitionCount,
            plan);
    }

    private static bool IsSuccessfulTransition(
        OwnedBootStackSwitchState previousState,
        OwnedBootStackSwitchState currentState,
        VirtualAddress observedRsp,
        ulong transitionCount,
        OwnedBootStackSwitchPlan plan)
    {
        if (previousState == OwnedBootStackSwitchState.Uninitialized
            && currentState == OwnedBootStackSwitchState.Planned)
        {
            return transitionCount == 0 && !observedRsp.IsValid;
        }

        if (previousState == OwnedBootStackSwitchState.Planned
            && currentState == OwnedBootStackSwitchState.Armed)
        {
            return transitionCount == 1 && !observedRsp.IsValid;
        }

        if (previousState == OwnedBootStackSwitchState.Armed
            && currentState
                == OwnedBootStackSwitchState.TransferRecorded)
        {
            return transitionCount == 2 && !observedRsp.IsValid;
        }

        return previousState
                == OwnedBootStackSwitchState.TransferRecorded
            && currentState
                == OwnedBootStackSwitchState.ContinuationObserved
            && transitionCount == 3
            && observedRsp.IsValid
            && observedRsp.Equals(
                plan.ExpectedContinuationStackPointer);
    }
}
