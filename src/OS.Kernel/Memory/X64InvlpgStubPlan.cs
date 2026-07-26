namespace OS.Kernel.Memory;

/// <summary>
/// Identifies whether the exact INVLPG byte stream has only been copied or has
/// also passed an independent caller-owned readback.
/// </summary>
public enum X64InvlpgStubPhase : byte
{
    Uninitialized = 0,
    Materialized = 1,
    ReadBackVerified = 2
}

/// <summary>
/// Stable status contract for the audited INVLPG leaf. Values are append-only.
/// </summary>
public enum X64InvlpgStubPlanStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidTargetRecord = 2,
    TargetOrdinalMustBeZero = 3,
    InvalidCodePage = 4,
    InvalidCodePageLength = 5,
    NonCanonicalCodePage = 6,
    InvalidEntryOffset = 7,
    MisalignedEntry = 8,
    EntryOutsideCodePage = 9,
    MissingDestination = 10,
    MisalignedDestination = 11,
    DestinationCapacityTooSmall = 12,
    DestinationCapacityExceedsPage = 13,
    DestinationSpanOverflow = 14,
    DestinationAddressMismatch = 15,
    MissingSource = 16,
    MisalignedSource = 17,
    SourceCapacityTooSmall = 18,
    SourceCapacityExceedsPage = 19,
    SourceSpanOverflow = 20,
    SourceAddressMismatch = 21,
    InvalidExpectedPlan = 22,
    ExpectedPlanNotMaterialized = 23,
    ByteReadbackMismatch = 24,
    InvalidInvocationRequest = 25,
    InvocationCountMustBeOne = 26,
    InvocationEntryMismatch = 27,
    InvocationTargetMismatch = 28,
    InvalidCodeProtection = 29,
    WritableExecutableCodeRejected = 30,
    ContractLayoutMismatch = 31
}

/// <summary>
/// Exact byte-plan and readback result. It is ordinary-data evidence and
/// contains no claim that INVLPG executed.
/// </summary>
public readonly struct X64InvlpgStubPlanResult
{
    public const ulong StructureSize = 104;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64InvlpgStubPlanStatus _status;
    private readonly X64InvlpgStubPhase _phase;
    private readonly byte _reserved0;
    private readonly ushort _reserved1;
    private readonly uint _reserved2;
    private readonly OS.Boot.VirtualAddress _codePageStart;
    private readonly OS.Boot.VirtualAddress _codePageEndExclusive;
    private readonly OS.Boot.VirtualAddress _entryAddress;
    private readonly ulong _entryOffset;
    private readonly X64TlbInvalidationRecord _target;
    private readonly ulong _availableCapacity;
    private readonly ulong _requiredByteCount;
    private readonly ulong _bytesWrittenOrVerified;
    private readonly ulong _instructionCount;

    private X64InvlpgStubPlanResult(
        X64InvlpgStubPlanStatus status,
        X64InvlpgStubPhase phase,
        OS.Boot.VirtualAddress codePageStart,
        OS.Boot.VirtualAddress codePageEndExclusive,
        OS.Boot.VirtualAddress entryAddress,
        ulong entryOffset,
        X64TlbInvalidationRecord target,
        ulong availableCapacity,
        ulong requiredByteCount,
        ulong bytesWrittenOrVerified,
        ulong instructionCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _phase = phase;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _codePageStart = codePageStart;
        _codePageEndExclusive = codePageEndExclusive;
        _entryAddress = entryAddress;
        _entryOffset = entryOffset;
        _target = target;
        _availableCapacity = availableCapacity;
        _requiredByteCount = requiredByteCount;
        _bytesWrittenOrVerified = bytesWrittenOrVerified;
        _instructionCount = instructionCount;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0;

    public X64InvlpgStubPlanStatus Status => _status;

    public X64InvlpgStubPhase Phase => _phase;

    public OS.Boot.VirtualAddress CodePageStart => _codePageStart;

    public OS.Boot.VirtualAddress CodePageEndExclusive =>
        _codePageEndExclusive;

    public OS.Boot.VirtualAddress EntryAddress => _entryAddress;

    public ulong EntryOffset => _entryOffset;

    public X64TlbInvalidationRecord Target => _target;

    public ulong AvailableCapacity => _availableCapacity;

    public ulong RequiredByteCount => _requiredByteCount;

    public ulong BytesWrittenOrVerified =>
        _bytesWrittenOrVerified;

    public ulong InstructionCount => _instructionCount;

    public bool ReadBackVerified =>
        Succeeded
        && _phase == X64InvlpgStubPhase.ReadBackVerified;

    public bool Succeeded =>
        IsInitialized
        && _status == X64InvlpgStubPlanStatus.Success
        && (_phase == X64InvlpgStubPhase.Materialized ||
            _phase == X64InvlpgStubPhase.ReadBackVerified)
        && _codePageStart.IsValid
        && _codePageStart.IsAligned(
            X64InvlpgStubPlan.PageSize)
        && _codePageStart.TryGetEndExclusive(
                X64InvlpgStubPlan.PageSize,
                out OS.Boot.VirtualAddress expectedEnd)
            == OS.Boot.AddressValueStatus.Success
        && expectedEnd.Equals(_codePageEndExclusive)
        && _entryAddress.IsValid
        && _entryAddress.IsAligned(
            X64InvlpgStubPlan.EntryAlignment)
        && _entryOffset < X64InvlpgStubPlan.PageSize
        && _entryOffset
            <= X64InvlpgStubPlan.PageSize
                - X64InvlpgStubPlan.RequiredByteLength
        && _codePageStart.TryAdd(
                _entryOffset,
                out OS.Boot.VirtualAddress expectedEntry)
            == OS.Boot.AddressValueStatus.Success
        && expectedEntry.Equals(_entryAddress)
        && _target.Validate()
            == X64TlbInvalidationStatus.Success
        && _target.Ordinal == 0
        && _availableCapacity
            == X64InvlpgStubPlan.PageSize
        && _requiredByteCount
            == X64InvlpgStubPlan.RequiredByteLength
        && _bytesWrittenOrVerified == _requiredByteCount
        && _instructionCount
            == X64InvlpgStubPlan.AuditedInstructionCount;

    internal static X64InvlpgStubPlanResult Success(
        X64InvlpgStubPhase phase,
        OS.Boot.VirtualAddress codePageStart,
        OS.Boot.VirtualAddress codePageEndExclusive,
        OS.Boot.VirtualAddress entryAddress,
        ulong entryOffset,
        X64TlbInvalidationRecord target,
        ulong availableCapacity)
    {
        return new X64InvlpgStubPlanResult(
            X64InvlpgStubPlanStatus.Success,
            phase,
            codePageStart,
            codePageEndExclusive,
            entryAddress,
            entryOffset,
            target,
            availableCapacity,
            X64InvlpgStubPlan.RequiredByteLength,
            X64InvlpgStubPlan.RequiredByteLength,
            X64InvlpgStubPlan.AuditedInstructionCount);
    }

    internal static X64InvlpgStubPlanResult Fail(
        X64InvlpgStubPlanStatus status,
        X64TlbInvalidationRecord target = default,
        ulong availableCapacity = 0)
    {
        return new X64InvlpgStubPlanResult(
            status,
            X64InvlpgStubPhase.Uninitialized,
            default,
            default,
            default,
            0,
            target,
            availableCapacity,
            X64InvlpgStubPlan.RequiredByteLength,
            0,
            X64InvlpgStubPlan.AuditedInstructionCount);
    }
}

/// <summary>
/// Caller request to bind one verified stub entry to one exact invalidation
/// target. The request is a call plan, not execution evidence.
/// </summary>
public readonly struct X64InvlpgInvocationRequest
{
    public const ulong StructureSize = 56;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _reserved0;
    private readonly OS.Boot.VirtualAddress _entryAddress;
    private readonly X64TlbInvalidationRecord _target;
    private readonly PageProtection _codeProtection;
    private readonly byte _reserved1;
    private readonly ushort _reserved2;
    private readonly uint _reserved3;
    private readonly ulong _invocationCount;

    private X64InvlpgInvocationRequest(
        OS.Boot.VirtualAddress entryAddress,
        X64TlbInvalidationRecord target,
        PageProtection codeProtection,
        ulong invocationCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved0 = 0;
        _entryAddress = entryAddress;
        _target = target;
        _codeProtection = codeProtection;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _invocationCount = invocationCount;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0
        && _reserved3 == 0;

    public OS.Boot.VirtualAddress EntryAddress => _entryAddress;

    public X64TlbInvalidationRecord Target => _target;

    public PageProtection CodeProtection => _codeProtection;

    public ulong InvocationCount => _invocationCount;

    public static X64InvlpgInvocationRequest Create(
        OS.Boot.VirtualAddress entryAddress,
        X64TlbInvalidationRecord target,
        PageProtection codeProtection,
        ulong invocationCount)
    {
        return new X64InvlpgInvocationRequest(
            entryAddress,
            target,
            codeProtection,
            invocationCount);
    }

    public X64InvlpgStubPlanStatus Validate()
    {
        if (!IsInitialized ||
            !_entryAddress.IsValid ||
            !_entryAddress.IsAligned(
                X64InvlpgStubPlan.EntryAlignment) ||
            _target.Validate()
                != X64TlbInvalidationStatus.Success)
        {
            return X64InvlpgStubPlanStatus
                .InvalidInvocationRequest;
        }

        if (_invocationCount != 1)
        {
            return X64InvlpgStubPlanStatus
                .InvocationCountMustBeOne;
        }

        byte protection = (byte)_codeProtection;
        bool writable =
            (protection & (byte)PageProtection.Write) != 0;
        bool executable =
            (protection & (byte)PageProtection.Execute) != 0;
        if (writable && executable)
        {
            return X64InvlpgStubPlanStatus
                .WritableExecutableCodeRejected;
        }

        PageProtection expected = (PageProtection)(
            (byte)PageProtection.Read
            | (byte)PageProtection.Execute);
        return _codeProtection == expected
            ? X64InvlpgStubPlanStatus.Success
            : X64InvlpgStubPlanStatus.InvalidCodeProtection;
    }
}

/// <summary>
/// One-shot checked-call plan. FunctionPointerAddress is an integer value for
/// later explicit unmanaged binding; no managed callable object or reference crosses
/// the boundary.
/// </summary>
public readonly struct X64InvlpgInvocationPlanResult
{
    public const ulong StructureSize = 88;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64InvlpgStubPlanStatus _status;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly ushort _reserved2;
    private readonly uint _reserved3;
    private readonly X64InvlpgInvocationRequest _request;
    private readonly ulong _functionPointerAddress;
    private readonly ulong _plannedInvocationCount;

    private X64InvlpgInvocationPlanResult(
        X64InvlpgStubPlanStatus status,
        X64InvlpgInvocationRequest request,
        ulong functionPointerAddress,
        ulong plannedInvocationCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _request = request;
        _functionPointerAddress = functionPointerAddress;
        _plannedInvocationCount = plannedInvocationCount;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0
        && _reserved3 == 0;

    public X64InvlpgStubPlanStatus Status => _status;

    public X64InvlpgInvocationRequest Request => _request;

    public ulong FunctionPointerAddress =>
        _functionPointerAddress;

    public ulong PlannedInvocationCount =>
        _plannedInvocationCount;

    public bool Succeeded =>
        IsInitialized
        && _status == X64InvlpgStubPlanStatus.Success
        && _request.Validate()
            == X64InvlpgStubPlanStatus.Success
        && _request.EntryAddress
            .TryGetRawValueForHardwareRegisterBoundary(
                out ulong rawEntry)
            == OS.Boot.AddressValueStatus.Success
        && _functionPointerAddress == rawEntry
        && _plannedInvocationCount == 1;

    internal static X64InvlpgInvocationPlanResult Success(
        X64InvlpgInvocationRequest request,
        ulong functionPointerAddress)
    {
        return new X64InvlpgInvocationPlanResult(
            X64InvlpgStubPlanStatus.Success,
            request,
            functionPointerAddress,
            1);
    }

    internal static X64InvlpgInvocationPlanResult Fail(
        X64InvlpgStubPlanStatus status,
        X64InvlpgInvocationRequest request = default)
    {
        return new X64InvlpgInvocationPlanResult(
            status,
            request,
            0,
            0);
    }
}

/// <summary>
/// Emits the exact Windows-x64 two-instruction sequence
/// INVLPG byte ptr [RCX]; RET as ordinary data and binds RCX to one planned
/// target. Host code never invokes the emitted bytes.
/// </summary>
public static unsafe class X64InvlpgStubPlan
{
    public const ulong PageSize = 4096;
    public const ulong EntryAlignment = 16;
    public const ulong RequiredPointerAlignment = 16;
    public const ulong RequiredByteLength = 4;
    public const ulong AuditedInstructionCount = 2;
    public const ulong MaximumInvocationCount = 1;
    public const bool UsesWindowsX64Abi = true;
    public const bool TargetPassedInRcx = true;
    public const bool TouchesNonvolatileRegisters = false;
    public const bool TouchesXmm6ThroughXmm15 = false;
    public const bool HasExternalRelocations = false;
    public const bool PerformsLiveInvalidation = false;
    public const bool ReloadsCr3 = false;
    public const bool InvokesStub = false;
    public const bool HostTestsInvokeBytes = false;

    public static X64InvlpgStubPlanResult Emit(
        byte* destination,
        ulong modeledDestinationAddress,
        ulong destinationCapacity,
        OS.Boot.VirtualAddress codePageStart,
        ulong entryOffset,
        X64TlbInvalidationRecord target)
    {
        X64InvlpgStubPlanStatus geometryStatus =
            ValidateGeometry(
                codePageStart,
                entryOffset,
                target,
                out OS.Boot.VirtualAddress codePageEnd,
                out OS.Boot.VirtualAddress entryAddress,
                out ulong rawCodePageStart);
        if (geometryStatus != X64InvlpgStubPlanStatus.Success)
        {
            return X64InvlpgStubPlanResult.Fail(
                geometryStatus,
                target,
                destinationCapacity);
        }

        X64InvlpgStubPlanStatus storageStatus =
            ValidateStorage(
                destination,
                modeledDestinationAddress,
                destinationCapacity,
                rawCodePageStart,
                missingStatus:
                    X64InvlpgStubPlanStatus.MissingDestination,
                misalignedStatus:
                    X64InvlpgStubPlanStatus
                        .MisalignedDestination,
                capacitySmallStatus:
                    X64InvlpgStubPlanStatus
                        .DestinationCapacityTooSmall,
                capacityLargeStatus:
                    X64InvlpgStubPlanStatus
                        .DestinationCapacityExceedsPage,
                spanOverflowStatus:
                    X64InvlpgStubPlanStatus
                        .DestinationSpanOverflow,
                addressMismatchStatus:
                    X64InvlpgStubPlanStatus
                        .DestinationAddressMismatch);
        if (storageStatus != X64InvlpgStubPlanStatus.Success)
        {
            return X64InvlpgStubPlanResult.Fail(
                storageStatus,
                target,
                destinationCapacity);
        }

        byte* entry = destination + entryOffset;
        entry[0] = 0x0f;
        entry[1] = 0x01;
        entry[2] = 0x39;
        entry[3] = 0xc3;

        return X64InvlpgStubPlanResult.Success(
            X64InvlpgStubPhase.Materialized,
            codePageStart,
            codePageEnd,
            entryAddress,
            entryOffset,
            target,
            destinationCapacity);
    }

    public static X64InvlpgStubPlanResult ValidateReadback(
        byte* source,
        ulong modeledSourceAddress,
        ulong sourceCapacity,
        X64InvlpgStubPlanResult expected)
    {
        if (!expected.Succeeded ||
            expected.Phase != X64InvlpgStubPhase.Materialized)
        {
            return X64InvlpgStubPlanResult.Fail(
                X64InvlpgStubPlanStatus.ExpectedPlanNotMaterialized,
                expected.Target,
                sourceCapacity);
        }

        if (expected.CodePageStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawCodePageStart)
            != OS.Boot.AddressValueStatus.Success)
        {
            return X64InvlpgStubPlanResult.Fail(
                X64InvlpgStubPlanStatus.InvalidExpectedPlan,
                expected.Target,
                sourceCapacity);
        }

        X64InvlpgStubPlanStatus storageStatus =
            ValidateStorage(
                source,
                modeledSourceAddress,
                sourceCapacity,
                rawCodePageStart,
                X64InvlpgStubPlanStatus.MissingSource,
                X64InvlpgStubPlanStatus.MisalignedSource,
                X64InvlpgStubPlanStatus.SourceCapacityTooSmall,
                X64InvlpgStubPlanStatus.SourceCapacityExceedsPage,
                X64InvlpgStubPlanStatus.SourceSpanOverflow,
                X64InvlpgStubPlanStatus.SourceAddressMismatch);
        if (storageStatus != X64InvlpgStubPlanStatus.Success)
        {
            return X64InvlpgStubPlanResult.Fail(
                storageStatus,
                expected.Target,
                sourceCapacity);
        }

        byte* entry = source + expected.EntryOffset;
        if (entry[0] != 0x0f ||
            entry[1] != 0x01 ||
            entry[2] != 0x39 ||
            entry[3] != 0xc3)
        {
            return X64InvlpgStubPlanResult.Fail(
                X64InvlpgStubPlanStatus.ByteReadbackMismatch,
                expected.Target,
                sourceCapacity);
        }

        return X64InvlpgStubPlanResult.Success(
            X64InvlpgStubPhase.ReadBackVerified,
            expected.CodePageStart,
            expected.CodePageEndExclusive,
            expected.EntryAddress,
            expected.EntryOffset,
            expected.Target,
            sourceCapacity);
    }

    public static X64InvlpgInvocationPlanResult
        CreateInvocationPlan(
            X64InvlpgStubPlanResult verifiedStub,
            X64InvlpgInvocationRequest request)
    {
        if (!verifiedStub.ReadBackVerified)
        {
            return X64InvlpgInvocationPlanResult.Fail(
                X64InvlpgStubPlanStatus.InvalidExpectedPlan,
                request);
        }

        X64InvlpgStubPlanStatus requestStatus =
            request.Validate();
        if (requestStatus != X64InvlpgStubPlanStatus.Success)
        {
            return X64InvlpgInvocationPlanResult.Fail(
                requestStatus,
                request);
        }

        if (!request.EntryAddress.Equals(
                verifiedStub.EntryAddress))
        {
            return X64InvlpgInvocationPlanResult.Fail(
                X64InvlpgStubPlanStatus.InvocationEntryMismatch,
                request);
        }

        if (!request.Target.Target.Equals(
                verifiedStub.Target.Target) ||
            request.Target.Ordinal
                != verifiedStub.Target.Ordinal)
        {
            return X64InvlpgInvocationPlanResult.Fail(
                X64InvlpgStubPlanStatus.InvocationTargetMismatch,
                request);
        }

        if (request.EntryAddress
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawEntry)
            != OS.Boot.AddressValueStatus.Success)
        {
            return X64InvlpgInvocationPlanResult.Fail(
                X64InvlpgStubPlanStatus.InvalidInvocationRequest,
                request);
        }

        return X64InvlpgInvocationPlanResult.Success(
            request,
            rawEntry);
    }

    private static X64InvlpgStubPlanStatus ValidateGeometry(
        OS.Boot.VirtualAddress codePageStart,
        ulong entryOffset,
        X64TlbInvalidationRecord target,
        out OS.Boot.VirtualAddress codePageEnd,
        out OS.Boot.VirtualAddress entryAddress,
        out ulong rawCodePageStart)
    {
        codePageEnd = default;
        entryAddress = default;
        rawCodePageStart = 0;

        if (target.Validate()
            != X64TlbInvalidationStatus.Success)
        {
            return X64InvlpgStubPlanStatus.InvalidTargetRecord;
        }

        if (target.Ordinal != 0)
        {
            return X64InvlpgStubPlanStatus
                .TargetOrdinalMustBeZero;
        }

        if (!codePageStart.IsValid ||
            !codePageStart.IsAligned(PageSize) ||
            codePageStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out rawCodePageStart)
                != OS.Boot.AddressValueStatus.Success)
        {
            return X64InvlpgStubPlanStatus.InvalidCodePage;
        }

        if (rawCodePageStart == 0 ||
            rawCodePageStart
                >= X64TlbInvalidationPlan
                    .LowCanonicalEndExclusive ||
            codePageStart.TryGetEndExclusive(
                    PageSize,
                    out codePageEnd)
                != OS.Boot.AddressValueStatus.Success ||
            codePageEnd
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawEnd)
                != OS.Boot.AddressValueStatus.Success ||
            rawEnd
                > X64TlbInvalidationPlan
                    .LowCanonicalEndExclusive)
        {
            return X64InvlpgStubPlanStatus
                .NonCanonicalCodePage;
        }

        if (entryOffset >= PageSize ||
            entryOffset > PageSize - RequiredByteLength)
        {
            return X64InvlpgStubPlanStatus.EntryOutsideCodePage;
        }

        if ((entryOffset & (EntryAlignment - 1)) != 0)
        {
            return X64InvlpgStubPlanStatus.MisalignedEntry;
        }

        if (codePageStart.TryAdd(
                entryOffset,
                out entryAddress)
            != OS.Boot.AddressValueStatus.Success ||
            !entryAddress.IsAligned(EntryAlignment))
        {
            return X64InvlpgStubPlanStatus.InvalidEntryOffset;
        }

        return X64InvlpgStubPlanStatus.Success;
    }

    private static X64InvlpgStubPlanStatus ValidateStorage(
        byte* storage,
        ulong modeledAddress,
        ulong capacity,
        ulong expectedModeledAddress,
        X64InvlpgStubPlanStatus missingStatus,
        X64InvlpgStubPlanStatus misalignedStatus,
        X64InvlpgStubPlanStatus capacitySmallStatus,
        X64InvlpgStubPlanStatus capacityLargeStatus,
        X64InvlpgStubPlanStatus spanOverflowStatus,
        X64InvlpgStubPlanStatus addressMismatchStatus)
    {
        if (capacity < RequiredByteLength)
        {
            return capacitySmallStatus;
        }

        if (capacity != PageSize)
        {
            return capacityLargeStatus;
        }

        if (storage == null)
        {
            return missingStatus;
        }

        ulong storageAddress = (ulong)storage;
        if ((storageAddress
                & (RequiredPointerAlignment - 1)) != 0)
        {
            return misalignedStatus;
        }

        if (storageAddress > ~0UL - capacity)
        {
            return spanOverflowStatus;
        }

        if (modeledAddress != expectedModeledAddress)
        {
            return addressMismatchStatus;
        }

        return X64InvlpgStubPlanStatus.Success;
    }
}
