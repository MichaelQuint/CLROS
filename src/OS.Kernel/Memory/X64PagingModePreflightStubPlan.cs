namespace OS.Kernel.Memory;

/// <summary>
/// Stable semantic instruction identifiers for the paging-mode observation
/// stub. Values are append-only. They describe the independently audited
/// instruction plan; they are not decoded from the emitted byte stream.
/// </summary>
public enum X64PagingModePreflightInstructionKind : byte
{
    Uninitialized = 0,
    PreserveNonvolatileRbx = 1,
    CopyObservationPointer = 2,
    CaptureRflags = 3,
    MoveRflagsToVolatile = 4,
    StoreRflags = 5,
    ClearCompletion = 6,
    ClearReserved = 7,
    ReadCr0 = 8,
    StoreCr0 = 9,
    ReadCr3 = 10,
    StoreCr3 = 11,
    ReadCr4 = 12,
    StoreCr4 = 13,
    SelectEferMsr = 14,
    ReadEferMsr = 15,
    ShiftEferHigh = 16,
    CombineEfer = 17,
    StoreEfer = 18,
    SelectMaximumExtendedLeaf = 19,
    ExecuteCpuidMaximumLeaf = 20,
    StoreMaximumExtendedLeaf = 21,
    CompareAddressSizesLeaf = 22,
    BranchBelowAddressSizesLeaf = 23,
    SelectAddressSizesLeaf = 24,
    ExecuteCpuidAddressSizes = 25,
    StoreAddressSizes = 26,
    BranchPastUnsupportedPath = 27,
    ClearUnsupportedAddressSizes = 28,
    StoreUnsupportedAddressSizes = 29,
    StoreCompletion = 30,
    RestoreNonvolatileRbx = 31,
    Return = 32
}

/// <summary>
/// Stable register/domain identifiers used by the semantic instruction plan.
/// The plan deliberately has no XMM register identifiers.
/// </summary>
public enum X64PagingModePreflightRegister : byte
{
    Uninitialized = 0,
    None = 1,
    Rax = 2,
    Rbx = 3,
    Rcx = 4,
    Rdx = 5,
    R8 = 6,
    Rflags = 7,
    Cr0 = 8,
    Cr3 = 9,
    Cr4 = 10,
    Ia32Efer = 11,
    CpuidEaxEbxEcxEdx = 12,
    ObservationMemory = 13,
    Stack = 14
}

/// <summary>
/// Identifies whether a byte-plan result came from materialization or from
/// caller-requested readback validation.
/// </summary>
public enum X64PagingModePreflightOperation : byte
{
    Uninitialized = 0,
    Materialize = 1,
    Validate = 2
}

/// <summary>
/// Stable fail-closed status contract for byte materialization, caller-owned
/// storage validation, and architectural observation evaluation. Values are
/// append-only and the zero-valued default is not success.
/// </summary>
public enum X64PagingModePreflightStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ContractLayoutMismatch = 2,
    InvalidInstructionIndex = 3,
    InvalidByteIndex = 4,
    InvalidStubByteCount = 5,
    StubCapacityTooSmall = 6,
    StubCapacityTooLarge = 7,
    MissingStubBuffer = 8,
    MisalignedStubBuffer = 9,
    StubBufferSpanOverflow = 10,
    InvalidEntryOffset = 11,
    MisalignedEntry = 12,
    EntrySpanOverflow = 13,
    MissingObservationStorage = 14,
    ObservationStorageSizeTooSmall = 15,
    ObservationStorageSizeTooLarge = 16,
    MisalignedObservationStorage = 17,
    ObservationStorageSpanOverflow = 18,
    MissingStateStorage = 19,
    StateStorageSizeTooSmall = 20,
    StateStorageSizeTooLarge = 21,
    MisalignedStateStorage = 22,
    StateStorageSpanOverflow = 23,
    StubObservationOverlap = 24,
    StubStateOverlap = 25,
    ObservationStateOverlap = 26,
    StubByteMismatch = 27,
    StubOpcodeMismatch = 28,
    StubSemanticPlanMismatch = 29,
    InvalidRequestCount = 30,
    InvalidObservationCount = 31,
    InvalidStateCount = 32,
    RequestCapacityTooSmall = 33,
    ObservationCapacityTooSmall = 34,
    StateCapacityTooSmall = 35,
    CapacityExceedsLimit = 36,
    MissingRequest = 37,
    MissingObservation = 38,
    MissingState = 39,
    MisalignedRequest = 40,
    MisalignedObservation = 41,
    MisalignedState = 42,
    RequestSpanOverflow = 43,
    ObservationSpanOverflow = 44,
    StateSpanOverflow = 45,
    RequestObservationOverlap = 46,
    RequestStateOverlap = 47,
    EvaluationObservationStateOverlap = 48,
    RequestNotInitialized = 49,
    InvalidRequestState = 50,
    ProbeDisabled = 51,
    ObservationNotInitialized = 52,
    InvalidObservationState = 53,
    CpuidAddressLeafUnavailable = 54,
    UnsupportedPhysicalAddressWidth = 55,
    PagingNotEnabled = 56,
    WriteProtectNotEnabled = 57,
    PaeNotEnabled = 58,
    LongModeNotEnabled = 59,
    LongModeNotActive = 60,
    NxeNotEnabled = 61,
    FiveLevelPagingNotSupported = 62,
    PcideNotSupported = 63,
    InvalidRflags = 64,
    InvalidFirmwareCr3Bits = 65,
    InvalidFirmwareCr3Base = 66,
    InvalidOwnedRoot = 67,
    MisalignedOwnedRoot = 68,
    InvalidTableArena = 69,
    MisalignedTableArena = 70,
    TableArenaLengthZero = 71,
    TableArenaLengthUnaligned = 72,
    TableArenaOverflow = 73,
    TableArenaOutsidePhysicalWidth = 74,
    OwnedRootOutsideArena = 75,
    OwnedRootPageOutsideArena = 76,
    OwnedRootOutsidePhysicalWidth = 77
}

/// <summary>
/// One independently authored semantic instruction record. The record binds
/// an instruction kind to an exact byte interval without deriving the
/// semantics from those bytes.
/// </summary>
public readonly struct X64PagingModePreflightInstruction
{
    public const ulong StructureSize = 24;

    private readonly ulong _operand;
    private readonly uint _byteOffset;
    private readonly ushort _byteLength;
    private readonly byte _isInitialized;
    private readonly X64PagingModePreflightInstructionKind _kind;
    private readonly X64PagingModePreflightRegister _destination;
    private readonly X64PagingModePreflightRegister _source;
    private readonly byte _stackDepthSlotsAfter;
    private readonly byte _reserved0;
    private readonly uint _reserved1;

    internal X64PagingModePreflightInstruction(
        X64PagingModePreflightInstructionKind kind,
        X64PagingModePreflightRegister destination,
        X64PagingModePreflightRegister source,
        uint byteOffset,
        ushort byteLength,
        ulong operand,
        byte stackDepthSlotsAfter)
    {
        _operand = operand;
        _byteOffset = byteOffset;
        _byteLength = byteLength;
        _isInitialized = 1;
        _kind = kind;
        _destination = destination;
        _source = source;
        _stackDepthSlotsAfter = stackDepthSlotsAfter;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized =>
        _isInitialized == 1
        && _kind >=
            X64PagingModePreflightInstructionKind.PreserveNonvolatileRbx
        && _kind <= X64PagingModePreflightInstructionKind.Return
        && _destination >= X64PagingModePreflightRegister.None
        && _destination <=
            X64PagingModePreflightRegister.Stack
        && _source >= X64PagingModePreflightRegister.None
        && _source <= X64PagingModePreflightRegister.Stack
        && _byteLength != 0
        && _stackDepthSlotsAfter <= 2
        && _reserved0 == 0
        && _reserved1 == 0;

    public X64PagingModePreflightInstructionKind Kind => _kind;

    public X64PagingModePreflightRegister Destination => _destination;

    public X64PagingModePreflightRegister Source => _source;

    public uint ByteOffset => _byteOffset;

    public ushort ByteLength => _byteLength;

    public ulong Operand => _operand;

    public byte StackDepthSlotsAfter => _stackDepthSlotsAfter;
}

/// <summary>
/// Caller-owned raw observations written by the audited stub. The completion
/// signature is written last. Reserved bytes must remain zero.
/// </summary>
public readonly struct X64PagingModePreflightObservation
{
    public const ulong StructureSize = 56;

    public const uint CompletionSignature = 0x31464c50;

    private readonly ulong _cr0;
    private readonly ulong _rawCr3;
    private readonly ulong _rawCr4;
    private readonly ulong _ia32Efer;
    private readonly uint _cpuidMaximumExtendedLeaf;
    private readonly uint _cpuidAddressSizesEax;
    private readonly ulong _rflags;
    private readonly uint _completionSignature;
    private readonly uint _reserved;

    private X64PagingModePreflightObservation(
        ulong cr0,
        ulong rawCr3,
        ulong rawCr4,
        ulong ia32Efer,
        uint cpuidMaximumExtendedLeaf,
        uint cpuidAddressSizesEax,
        ulong rflags)
    {
        _cr0 = cr0;
        _rawCr3 = rawCr3;
        _rawCr4 = rawCr4;
        _ia32Efer = ia32Efer;
        _cpuidMaximumExtendedLeaf = cpuidMaximumExtendedLeaf;
        _cpuidAddressSizesEax = cpuidAddressSizesEax;
        _rflags = rflags;
        _completionSignature = CompletionSignature;
        _reserved = 0;
    }

    public bool IsInitialized =>
        _completionSignature == CompletionSignature
        && _reserved == 0;

    public ulong Cr0 => _cr0;

    public ulong RawCr3 => _rawCr3;

    public ulong RawCr4 => _rawCr4;

    public ulong Ia32Efer => _ia32Efer;

    public uint CpuidMaximumExtendedLeaf =>
        _cpuidMaximumExtendedLeaf;

    public uint CpuidAddressSizesEax => _cpuidAddressSizesEax;

    public byte PhysicalAddressWidth =>
        (byte)_cpuidAddressSizesEax;

    public ulong Rflags => _rflags;

    public static X64PagingModePreflightObservation CreateObserved(
        ulong cr0,
        ulong rawCr3,
        ulong rawCr4,
        ulong ia32Efer,
        uint cpuidMaximumExtendedLeaf,
        uint cpuidAddressSizesEax,
        ulong rflags)
    {
        return new X64PagingModePreflightObservation(
            cr0,
            rawCr3,
            rawCr4,
            ia32Efer,
            cpuidMaximumExtendedLeaf,
            cpuidAddressSizesEax,
            rflags);
    }
}

/// <summary>
/// Caller-owned request for evaluation of one observation. The root and arena
/// are physical-address facts supplied by the future integration layer; this
/// host-audited leaf does not discover or allocate either range.
/// </summary>
public readonly struct X64PagingModePreflightRequest
{
    public const ulong StructureSize = 32;

    private readonly OS.Boot.PhysicalAddress _ownedRoot;
    private readonly OS.Boot.PhysicalAddress _tableArenaStart;
    private readonly ulong _tableArenaByteLength;
    private readonly byte _isInitialized;
    private readonly byte _probeEnabled;
    private readonly ushort _reserved0;
    private readonly uint _reserved1;

    private X64PagingModePreflightRequest(
        bool enabled,
        OS.Boot.PhysicalAddress ownedRoot,
        OS.Boot.PhysicalAddress tableArenaStart,
        ulong tableArenaByteLength)
    {
        _ownedRoot = ownedRoot;
        _tableArenaStart = tableArenaStart;
        _tableArenaByteLength = tableArenaByteLength;
        _isInitialized = 1;
        _probeEnabled = enabled ? (byte)1 : (byte)0;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized =>
        _isInitialized == 1
        && _probeEnabled <= 1
        && _reserved0 == 0
        && _reserved1 == 0;

    public bool ProbeEnabled => _probeEnabled == 1;

    public OS.Boot.PhysicalAddress OwnedRoot => _ownedRoot;

    public OS.Boot.PhysicalAddress TableArenaStart =>
        _tableArenaStart;

    public ulong TableArenaByteLength => _tableArenaByteLength;

    public static X64PagingModePreflightRequest CreateEnabled(
        OS.Boot.PhysicalAddress ownedRoot,
        OS.Boot.PhysicalAddress tableArenaStart,
        ulong tableArenaByteLength)
    {
        return new X64PagingModePreflightRequest(
            enabled: true,
            ownedRoot,
            tableArenaStart,
            tableArenaByteLength);
    }

    public static X64PagingModePreflightRequest CreateDisabled(
        OS.Boot.PhysicalAddress ownedRoot,
        OS.Boot.PhysicalAddress tableArenaStart,
        ulong tableArenaByteLength)
    {
        return new X64PagingModePreflightRequest(
            enabled: false,
            ownedRoot,
            tableArenaStart,
            tableArenaByteLength);
    }
}

/// <summary>
/// Caller-owned evaluated state. This record preserves raw control-register
/// observations and the validated root/arena facts without executing a
/// privileged instruction.
/// </summary>
public readonly struct X64PagingModePreflightResult
{
    public const ulong StructureSize = 96;

    private readonly OS.Boot.PhysicalAddress _ownedRoot;
    private readonly OS.Boot.PhysicalAddress _tableArenaStart;
    private readonly OS.Boot.PhysicalAddress _tableArenaEndExclusive;
    private readonly ulong _cr0;
    private readonly ulong _rawFirmwareCr3;
    private readonly ulong _rawCr4;
    private readonly ulong _ia32Efer;
    private readonly ulong _rflags;
    private readonly ulong _physicalAddressMask;
    private readonly byte _isInitialized;
    private readonly X64PagingModePreflightStatus _status;
    private readonly byte _physicalAddressWidth;
    private readonly byte _probeEnabled;
    private readonly byte _pagingEnabled;
    private readonly byte _writeProtectEnabled;
    private readonly byte _paeEnabled;
    private readonly byte _longModeEnabled;
    private readonly byte _longModeActive;
    private readonly byte _nxeEnabled;
    private readonly byte _la57Enabled;
    private readonly byte _pcideEnabled;
    private readonly byte _cpuidAddressLeafAvailable;
    private readonly byte _ownedRootAligned;
    private readonly byte _ownedRootInsideArena;
    private readonly byte _reserved0;
    private readonly ulong _reserved1;

    internal X64PagingModePreflightResult(
        X64PagingModePreflightStatus status,
        OS.Boot.PhysicalAddress ownedRoot,
        OS.Boot.PhysicalAddress tableArenaStart,
        OS.Boot.PhysicalAddress tableArenaEndExclusive,
        ulong cr0,
        ulong rawFirmwareCr3,
        ulong rawCr4,
        ulong ia32Efer,
        ulong rflags,
        ulong physicalAddressMask,
        byte physicalAddressWidth,
        bool probeEnabled,
        bool pagingEnabled,
        bool writeProtectEnabled,
        bool paeEnabled,
        bool longModeEnabled,
        bool longModeActive,
        bool nxeEnabled,
        bool la57Enabled,
        bool pcideEnabled,
        bool cpuidAddressLeafAvailable,
        bool ownedRootAligned,
        bool ownedRootInsideArena)
    {
        _ownedRoot = ownedRoot;
        _tableArenaStart = tableArenaStart;
        _tableArenaEndExclusive = tableArenaEndExclusive;
        _cr0 = cr0;
        _rawFirmwareCr3 = rawFirmwareCr3;
        _rawCr4 = rawCr4;
        _ia32Efer = ia32Efer;
        _rflags = rflags;
        _physicalAddressMask = physicalAddressMask;
        _isInitialized = 1;
        _status = status;
        _physicalAddressWidth = physicalAddressWidth;
        _probeEnabled = probeEnabled ? (byte)1 : (byte)0;
        _pagingEnabled = pagingEnabled ? (byte)1 : (byte)0;
        _writeProtectEnabled =
            writeProtectEnabled ? (byte)1 : (byte)0;
        _paeEnabled = paeEnabled ? (byte)1 : (byte)0;
        _longModeEnabled = longModeEnabled ? (byte)1 : (byte)0;
        _longModeActive = longModeActive ? (byte)1 : (byte)0;
        _nxeEnabled = nxeEnabled ? (byte)1 : (byte)0;
        _la57Enabled = la57Enabled ? (byte)1 : (byte)0;
        _pcideEnabled = pcideEnabled ? (byte)1 : (byte)0;
        _cpuidAddressLeafAvailable =
            cpuidAddressLeafAvailable ? (byte)1 : (byte)0;
        _ownedRootAligned = ownedRootAligned ? (byte)1 : (byte)0;
        _ownedRootInsideArena =
            ownedRootInsideArena ? (byte)1 : (byte)0;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized
    {
        get
        {
            bool canonical =
                _isInitialized == 1
                && _status >= X64PagingModePreflightStatus.Success
                && _status <=
                    X64PagingModePreflightStatus
                        .OwnedRootOutsidePhysicalWidth
                && _probeEnabled <= 1
                && _pagingEnabled <= 1
                && _writeProtectEnabled <= 1
                && _paeEnabled <= 1
                && _longModeEnabled <= 1
                && _longModeActive <= 1
                && _nxeEnabled <= 1
                && _la57Enabled <= 1
                && _pcideEnabled <= 1
                && _cpuidAddressLeafAvailable <= 1
                && _ownedRootAligned <= 1
                && _ownedRootInsideArena <= 1
                && _reserved0 == 0
                && _reserved1 == 0;
            if (!canonical)
            {
                return false;
            }

            return _status != X64PagingModePreflightStatus.Success
                || HasSuccessfulState();
        }
    }

    public bool Succeeded =>
        IsInitialized
        && _status == X64PagingModePreflightStatus.Success;

    public X64PagingModePreflightStatus Status => _status;

    public OS.Boot.PhysicalAddress OwnedRoot => _ownedRoot;

    public OS.Boot.PhysicalAddress TableArenaStart =>
        _tableArenaStart;

    public OS.Boot.PhysicalAddress TableArenaEndExclusive =>
        _tableArenaEndExclusive;

    public ulong Cr0 => _cr0;

    public ulong RawFirmwareCr3 => _rawFirmwareCr3;

    public ulong RawCr4 => _rawCr4;

    public ulong Ia32Efer => _ia32Efer;

    public ulong Rflags => _rflags;

    public ulong PhysicalAddressMask => _physicalAddressMask;

    public byte PhysicalAddressWidth => _physicalAddressWidth;

    public bool ProbeEnabled => _probeEnabled == 1;

    public bool PagingEnabled => _pagingEnabled == 1;

    public bool WriteProtectEnabled => _writeProtectEnabled == 1;

    public bool PaeEnabled => _paeEnabled == 1;

    public bool LongModeEnabled => _longModeEnabled == 1;

    public bool LongModeActive => _longModeActive == 1;

    public bool NxeEnabled => _nxeEnabled == 1;

    public bool La57Enabled => _la57Enabled == 1;

    public bool PcideEnabled => _pcideEnabled == 1;

    public bool CpuidAddressLeafAvailable =>
        _cpuidAddressLeafAvailable == 1;

    public bool OwnedRootAligned => _ownedRootAligned == 1;

    public bool OwnedRootInsideArena =>
        _ownedRootInsideArena == 1;

    private bool HasSuccessfulState()
    {
        if (_physicalAddressWidth <
                X64PagingModePreflightStubPlan
                    .MinimumPhysicalAddressWidth
            || _physicalAddressWidth >
                X64PagingModePreflightStubPlan
                    .MaximumPhysicalAddressWidth)
        {
            return false;
        }

        ulong expectedPhysicalMask =
            (1UL << _physicalAddressWidth) - 1;
        if (_physicalAddressMask != expectedPhysicalMask
            || _probeEnabled != 1
            || _cpuidAddressLeafAvailable != 1
            || _pagingEnabled != 1
            || _writeProtectEnabled != 1
            || _paeEnabled != 1
            || _longModeEnabled != 1
            || _longModeActive != 1
            || _nxeEnabled != 1
            || _la57Enabled != 0
            || _pcideEnabled != 0
            || _ownedRootAligned != 1
            || _ownedRootInsideArena != 1)
        {
            return false;
        }

        if ((_cr0 & X64PagingModePreflightStubPlan.Cr0PagingBit)
                == 0
            || (_cr0
                & X64PagingModePreflightStubPlan.Cr0WriteProtectBit)
                == 0
            || (_rawCr4 & X64PagingModePreflightStubPlan.Cr4PaeBit)
                == 0
            || (_rawCr4 & X64PagingModePreflightStubPlan.Cr4La57Bit)
                != 0
            || (_rawCr4 & X64PagingModePreflightStubPlan.Cr4PcideBit)
                != 0
            || (_ia32Efer
                & X64PagingModePreflightStubPlan
                    .EferLongModeEnableBit)
                == 0
            || (_ia32Efer
                & X64PagingModePreflightStubPlan
                    .EferLongModeActiveBit)
                == 0
            || (_ia32Efer
                & X64PagingModePreflightStubPlan
                    .EferNoExecuteEnableBit)
                == 0
            || (_rflags
                & X64PagingModePreflightStubPlan
                    .RflagsArchitecturalOneBit)
                == 0)
        {
            return false;
        }

        ulong allowedCr3 =
            (expectedPhysicalMask & ~0xfffUL)
            | X64PagingModePreflightStubPlan.Cr3AllowedLowBits;
        ulong firmwareRoot =
            _rawFirmwareCr3 & (expectedPhysicalMask & ~0xfffUL);
        if ((_rawFirmwareCr3 & ~allowedCr3) != 0
            || firmwareRoot == 0)
        {
            return false;
        }

        if (_ownedRoot
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong root)
                != OS.Boot.AddressValueStatus.Success
            || _tableArenaStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong arenaStart)
                != OS.Boot.AddressValueStatus.Success
            || _tableArenaEndExclusive
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong arenaEnd)
                != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        if ((root & (X64PagingModePreflightStubPlan.PageSize - 1))
                != 0
            || (arenaStart
                & (X64PagingModePreflightStubPlan.PageSize - 1))
                != 0
            || (arenaEnd
                & (X64PagingModePreflightStubPlan.PageSize - 1))
                != 0
            || arenaEnd <= arenaStart
            || (arenaStart & ~expectedPhysicalMask) != 0
            || ((arenaEnd - 1) & ~expectedPhysicalMask) != 0
            || (root & ~expectedPhysicalMask) != 0
            || root < arenaStart
            || root >= arenaEnd
            || root > ~0UL - X64PagingModePreflightStubPlan.PageSize
            || root + X64PagingModePreflightStubPlan.PageSize
                > arenaEnd)
        {
            return false;
        }

        return true;
    }
}

/// <summary>
/// Result of one exact byte materialization or exact byte readback.
/// </summary>
public readonly struct X64PagingModePreflightStubResult
{
    public const ulong StructureSize = 56;

    public const ulong NoMismatchOffset = ~0UL;

    private readonly ulong _declaredByteCount;
    private readonly ulong _bufferCapacity;
    private readonly ulong _entryOffset;
    private readonly ulong _entryAddress;
    private readonly ulong _bytesProcessed;
    private readonly ulong _mismatchOffset;
    private readonly byte _isInitialized;
    private readonly X64PagingModePreflightStatus _status;
    private readonly X64PagingModePreflightOperation _operation;
    private readonly byte _exactBytes;
    private readonly uint _reserved;

    internal X64PagingModePreflightStubResult(
        X64PagingModePreflightStatus status,
        X64PagingModePreflightOperation operation,
        ulong declaredByteCount,
        ulong bufferCapacity,
        ulong entryOffset,
        ulong entryAddress,
        ulong bytesProcessed,
        ulong mismatchOffset,
        bool exactBytes)
    {
        _declaredByteCount = declaredByteCount;
        _bufferCapacity = bufferCapacity;
        _entryOffset = entryOffset;
        _entryAddress = entryAddress;
        _bytesProcessed = bytesProcessed;
        _mismatchOffset = mismatchOffset;
        _isInitialized = 1;
        _status = status;
        _operation = operation;
        _exactBytes = exactBytes ? (byte)1 : (byte)0;
        _reserved = 0;
    }

    public bool IsInitialized =>
        _isInitialized == 1
        && _status >= X64PagingModePreflightStatus.Success
        && _status <=
            X64PagingModePreflightStatus.OwnedRootOutsidePhysicalWidth
        && _operation >=
            X64PagingModePreflightOperation.Materialize
        && _operation <= X64PagingModePreflightOperation.Validate
        && _exactBytes <= 1
        && _reserved == 0;

    public bool Succeeded
    {
        get
        {
            if (!IsInitialized
                || _status != X64PagingModePreflightStatus.Success
                || _exactBytes != 1
                || _declaredByteCount
                    != X64PagingModePreflightStubPlan.RequiredByteLength
                || _bufferCapacity
                    != X64PagingModePreflightStubPlan.StubPageByteLength
                || _entryOffset >= _bufferCapacity
                || (_entryOffset
                    & (X64PagingModePreflightStubPlan
                        .RequiredEntryAlignment - 1)) != 0
                || _entryAddress == 0
                || (_entryAddress
                    & (X64PagingModePreflightStubPlan
                        .RequiredEntryAlignment - 1)) != 0
                || _entryAddress < _entryOffset
                || _bytesProcessed
                    != X64PagingModePreflightStubPlan.RequiredByteLength
                || _mismatchOffset != NoMismatchOffset)
            {
                return false;
            }

            ulong bufferAddress = _entryAddress - _entryOffset;
            if (bufferAddress == 0
                || (bufferAddress
                    & (X64PagingModePreflightStubPlan
                        .RequiredEntryAlignment - 1)) != 0
                || bufferAddress > ~0UL - _bufferCapacity)
            {
                return false;
            }

            ulong bufferEnd = bufferAddress + _bufferCapacity;
            if (_entryAddress
                > ~0UL - X64PagingModePreflightStubPlan.RequiredByteLength)
            {
                return false;
            }

            ulong entryEnd = _entryAddress
                + X64PagingModePreflightStubPlan.RequiredByteLength;
            return entryEnd <= bufferEnd;
        }
    }

    public X64PagingModePreflightStatus Status => _status;

    public X64PagingModePreflightOperation Operation => _operation;

    public ulong DeclaredByteCount => _declaredByteCount;

    public ulong BufferCapacity => _bufferCapacity;

    public ulong EntryOffset => _entryOffset;

    public ulong EntryAddress => _entryAddress;

    public ulong BytesProcessed => _bytesProcessed;

    public ulong MismatchOffset => _mismatchOffset;

    public bool ExactBytes => _exactBytes == 1;
}

/// <summary>
/// Exact, allocation-free x64 paging-mode observation stub and fail-closed
/// evaluator. No method in this type invokes the emitted bytes or executes a
/// privileged instruction.
///
/// The byte stream is independently authored. SharpOS Cr3Accessor.cs
/// (local SHA-256
/// 088D73C1BF807B652CD2F7ED795C1507C1F28884DD4184A2A28A574CF01E9E47)
/// was used only to cross-check the minimal MOV-from-CR3 encoding. SharpOS
/// BootAsmGenerator.cs (local SHA-256
/// B24C79DCD38757DCBE243DC6C4C63C1AF628525F06C4F5D296B6AFB0C9387500)
/// influenced the deterministic bytes-versus-semantics audit pattern; no
/// generator, Iced dependency, allocation, or reference runtime path is
/// imported.
/// </summary>
public static unsafe class X64PagingModePreflightStubPlan
{
    public const ulong PageSize = 4096;
    public const ulong StubPageByteLength = PageSize;
    public const ulong RequiredEntryAlignment = 16;
    public const ulong RequiredDataAlignment = 8;
    public const ulong RequiredByteLength = 111;
    public const ulong InstructionCount = 32;
    public const ulong MaximumRecordCount = 1;

    public const uint Ia32EferMsr = 0xc0000080;
    public const uint CpuidMaximumExtendedLeaf = 0x80000000;
    public const uint CpuidAddressSizesLeaf = 0x80000008;

    public const byte MinimumPhysicalAddressWidth = 36;
    public const byte MaximumPhysicalAddressWidth = 52;

    public const ulong Cr0WriteProtectBit = 1UL << 16;
    public const ulong Cr0PagingBit = 1UL << 31;
    public const ulong Cr4PaeBit = 1UL << 5;
    public const ulong Cr4PgeBit = 1UL << 7;
    public const ulong Cr4La57Bit = 1UL << 12;
    public const ulong Cr4PcideBit = 1UL << 17;
    public const ulong EferLongModeEnableBit = 1UL << 8;
    public const ulong EferLongModeActiveBit = 1UL << 10;
    public const ulong EferNoExecuteEnableBit = 1UL << 11;
    public const ulong RflagsArchitecturalOneBit = 1UL << 1;

    public const ulong Cr3PageWriteThroughBit = 1UL << 3;
    public const ulong Cr3PageCacheDisableBit = 1UL << 4;
    public const ulong Cr3AllowedLowBits =
        Cr3PageWriteThroughBit | Cr3PageCacheDisableBit;

    public const ulong ObservationCr0Offset = 0;
    public const ulong ObservationRawCr3Offset = 8;
    public const ulong ObservationRawCr4Offset = 16;
    public const ulong ObservationIa32EferOffset = 24;
    public const ulong ObservationMaximumExtendedLeafOffset = 32;
    public const ulong ObservationAddressSizesEaxOffset = 36;
    public const ulong ObservationRflagsOffset = 40;
    public const ulong ObservationCompletionOffset = 48;
    public const ulong ObservationReservedOffset = 52;

    public const bool HostMethodsExecutePrivilegedInstructions = false;
    public const bool HostMethodsInvokeEmittedBytes = false;
    public const bool EmittedStubReadsControlRegisters = true;
    public const bool EmittedStubWritesControlRegisters = false;
    public const bool EmittedStubReadsIa32Efer = true;
    public const bool EmittedStubExecutesCpuid = true;
    public const bool UsesXmmRegisters = false;
    public const bool PerformsLiveCr3Load = false;

    private const ulong ByteChunk00 = 0x3040c741c8894953UL;
    private const ulong ByteChunk01 = 0x3440c74100000000UL;
    private const ulong ByteChunk02 = 0x8949589c00000000UL;
    private const ulong ByteChunk03 = 0x008949c0200f2840UL;
    private const ulong ByteChunk04 = 0x0f08408949d8200fUL;
    private const ulong ByteChunk05 = 0x80b910408949e020UL;
    private const ulong ByteChunk06 = 0xe2c148320fc00000UL;
    private const ulong ByteChunk07 = 0x18408949d0094820UL;
    private const ulong ByteChunk08 = 0x41a20f80000000b8UL;
    private const ulong ByteChunk09 = 0x800000083d204089UL;
    private const ulong ByteChunk10 = 0x0f80000008b80d72UL;
    private const ulong ByteChunk11 = 0x3106eb24408941a2UL;
    private const ulong ByteChunk12 = 0x40c74124408941c0UL;
    private const ulong ByteChunk13 = 0x00c35b31464c5030UL;

    public static X64PagingModePreflightStatus TryGetExpectedByte(
        ulong index,
        out byte value)
    {
        value = 0;
        if (index >= RequiredByteLength)
        {
            return X64PagingModePreflightStatus.InvalidByteIndex;
        }

        ulong chunk;
        switch (index >> 3)
        {
            case 0:
                chunk = ByteChunk00;
                break;
            case 1:
                chunk = ByteChunk01;
                break;
            case 2:
                chunk = ByteChunk02;
                break;
            case 3:
                chunk = ByteChunk03;
                break;
            case 4:
                chunk = ByteChunk04;
                break;
            case 5:
                chunk = ByteChunk05;
                break;
            case 6:
                chunk = ByteChunk06;
                break;
            case 7:
                chunk = ByteChunk07;
                break;
            case 8:
                chunk = ByteChunk08;
                break;
            case 9:
                chunk = ByteChunk09;
                break;
            case 10:
                chunk = ByteChunk10;
                break;
            case 11:
                chunk = ByteChunk11;
                break;
            case 12:
                chunk = ByteChunk12;
                break;
            default:
                chunk = ByteChunk13;
                break;
        }

        value = (byte)(chunk >> (int)((index & 7) * 8));
        return X64PagingModePreflightStatus.Success;
    }

    public static X64PagingModePreflightStatus TryGetInstruction(
        ulong index,
        out X64PagingModePreflightInstruction instruction)
    {
        instruction = default;
        switch (index)
        {
            case 0:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .PreserveNonvolatileRbx,
                    X64PagingModePreflightRegister.Stack,
                    X64PagingModePreflightRegister.Rbx,
                    0,
                    1,
                    0,
                    1);
                break;
            case 1:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .CopyObservationPointer,
                    X64PagingModePreflightRegister.R8,
                    X64PagingModePreflightRegister.Rcx,
                    1,
                    3,
                    0,
                    1);
                break;
            case 2:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.ClearCompletion,
                    X64PagingModePreflightRegister.ObservationMemory,
                    X64PagingModePreflightRegister.None,
                    4,
                    8,
                    ObservationCompletionOffset,
                    1);
                break;
            case 3:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .ClearReserved,
                    X64PagingModePreflightRegister.ObservationMemory,
                    X64PagingModePreflightRegister.None,
                    12,
                    8,
                    ObservationReservedOffset,
                    1);
                break;
            case 4:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.CaptureRflags,
                    X64PagingModePreflightRegister.Stack,
                    X64PagingModePreflightRegister.Rflags,
                    20,
                    1,
                    0,
                    2);
                break;
            case 5:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .MoveRflagsToVolatile,
                    X64PagingModePreflightRegister.Rax,
                    X64PagingModePreflightRegister.Stack,
                    21,
                    1,
                    0,
                    1);
                break;
            case 6:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.StoreRflags,
                    X64PagingModePreflightRegister.ObservationMemory,
                    X64PagingModePreflightRegister.Rax,
                    22,
                    4,
                    ObservationRflagsOffset,
                    1);
                break;
            case 7:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.ReadCr0,
                    X64PagingModePreflightRegister.Rax,
                    X64PagingModePreflightRegister.Cr0,
                    26,
                    3,
                    0,
                    1);
                break;
            case 8:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.StoreCr0,
                    X64PagingModePreflightRegister.ObservationMemory,
                    X64PagingModePreflightRegister.Rax,
                    29,
                    3,
                    ObservationCr0Offset,
                    1);
                break;
            case 9:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.ReadCr3,
                    X64PagingModePreflightRegister.Rax,
                    X64PagingModePreflightRegister.Cr3,
                    32,
                    3,
                    0,
                    1);
                break;
            case 10:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.StoreCr3,
                    X64PagingModePreflightRegister.ObservationMemory,
                    X64PagingModePreflightRegister.Rax,
                    35,
                    4,
                    ObservationRawCr3Offset,
                    1);
                break;
            case 11:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.ReadCr4,
                    X64PagingModePreflightRegister.Rax,
                    X64PagingModePreflightRegister.Cr4,
                    39,
                    3,
                    0,
                    1);
                break;
            case 12:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.StoreCr4,
                    X64PagingModePreflightRegister.ObservationMemory,
                    X64PagingModePreflightRegister.Rax,
                    42,
                    4,
                    ObservationRawCr4Offset,
                    1);
                break;
            case 13:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.SelectEferMsr,
                    X64PagingModePreflightRegister.Rcx,
                    X64PagingModePreflightRegister.None,
                    46,
                    5,
                    Ia32EferMsr,
                    1);
                break;
            case 14:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.ReadEferMsr,
                    X64PagingModePreflightRegister.Rax,
                    X64PagingModePreflightRegister.Ia32Efer,
                    51,
                    2,
                    Ia32EferMsr,
                    1);
                break;
            case 15:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.ShiftEferHigh,
                    X64PagingModePreflightRegister.Rdx,
                    X64PagingModePreflightRegister.Rdx,
                    53,
                    4,
                    32,
                    1);
                break;
            case 16:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.CombineEfer,
                    X64PagingModePreflightRegister.Rax,
                    X64PagingModePreflightRegister.Rdx,
                    57,
                    3,
                    0,
                    1);
                break;
            case 17:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.StoreEfer,
                    X64PagingModePreflightRegister.ObservationMemory,
                    X64PagingModePreflightRegister.Rax,
                    60,
                    4,
                    ObservationIa32EferOffset,
                    1);
                break;
            case 18:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .SelectMaximumExtendedLeaf,
                    X64PagingModePreflightRegister.Rax,
                    X64PagingModePreflightRegister.None,
                    64,
                    5,
                    CpuidMaximumExtendedLeaf,
                    1);
                break;
            case 19:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .ExecuteCpuidMaximumLeaf,
                    X64PagingModePreflightRegister.CpuidEaxEbxEcxEdx,
                    X64PagingModePreflightRegister.Rax,
                    69,
                    2,
                    CpuidMaximumExtendedLeaf,
                    1);
                break;
            case 20:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .StoreMaximumExtendedLeaf,
                    X64PagingModePreflightRegister.ObservationMemory,
                    X64PagingModePreflightRegister.Rax,
                    71,
                    4,
                    ObservationMaximumExtendedLeafOffset,
                    1);
                break;
            case 21:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .CompareAddressSizesLeaf,
                    X64PagingModePreflightRegister.Rflags,
                    X64PagingModePreflightRegister.Rax,
                    75,
                    5,
                    CpuidAddressSizesLeaf,
                    1);
                break;
            case 22:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .BranchBelowAddressSizesLeaf,
                    X64PagingModePreflightRegister.None,
                    X64PagingModePreflightRegister.Rflags,
                    80,
                    2,
                    95,
                    1);
                break;
            case 23:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .SelectAddressSizesLeaf,
                    X64PagingModePreflightRegister.Rax,
                    X64PagingModePreflightRegister.None,
                    82,
                    5,
                    CpuidAddressSizesLeaf,
                    1);
                break;
            case 24:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .ExecuteCpuidAddressSizes,
                    X64PagingModePreflightRegister.CpuidEaxEbxEcxEdx,
                    X64PagingModePreflightRegister.Rax,
                    87,
                    2,
                    CpuidAddressSizesLeaf,
                    1);
                break;
            case 25:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.StoreAddressSizes,
                    X64PagingModePreflightRegister.ObservationMemory,
                    X64PagingModePreflightRegister.Rax,
                    89,
                    4,
                    ObservationAddressSizesEaxOffset,
                    1);
                break;
            case 26:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .BranchPastUnsupportedPath,
                    X64PagingModePreflightRegister.None,
                    X64PagingModePreflightRegister.None,
                    93,
                    2,
                    101,
                    1);
                break;
            case 27:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .ClearUnsupportedAddressSizes,
                    X64PagingModePreflightRegister.Rax,
                    X64PagingModePreflightRegister.None,
                    95,
                    2,
                    0,
                    1);
                break;
            case 28:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .StoreUnsupportedAddressSizes,
                    X64PagingModePreflightRegister.ObservationMemory,
                    X64PagingModePreflightRegister.Rax,
                    97,
                    4,
                    ObservationAddressSizesEaxOffset,
                    1);
                break;
            case 29:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.StoreCompletion,
                    X64PagingModePreflightRegister.ObservationMemory,
                    X64PagingModePreflightRegister.None,
                    101,
                    8,
                    X64PagingModePreflightObservation.CompletionSignature,
                    1);
                break;
            case 30:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind
                        .RestoreNonvolatileRbx,
                    X64PagingModePreflightRegister.Rbx,
                    X64PagingModePreflightRegister.Stack,
                    109,
                    1,
                    0,
                    0);
                break;
            case 31:
                instruction = Instruction(
                    X64PagingModePreflightInstructionKind.Return,
                    X64PagingModePreflightRegister.None,
                    X64PagingModePreflightRegister.None,
                    110,
                    1,
                    0,
                    0);
                break;
            default:
                return X64PagingModePreflightStatus
                    .InvalidInstructionIndex;
        }

        return X64PagingModePreflightStatus.Success;
    }

    public static X64PagingModePreflightStatus ValidateSemanticPlan()
    {
        if ((ulong)sizeof(X64PagingModePreflightInstruction)
                != X64PagingModePreflightInstruction.StructureSize)
        {
            return X64PagingModePreflightStatus.ContractLayoutMismatch;
        }

        ulong expectedOffset = 0;
        for (ulong index = 0; index < InstructionCount; index++)
        {
            X64PagingModePreflightStatus status =
                TryGetInstruction(index, out var instruction);
            if (status != X64PagingModePreflightStatus.Success
                || !instruction.IsInitialized
                || instruction.ByteOffset != expectedOffset)
            {
                return X64PagingModePreflightStatus
                    .StubSemanticPlanMismatch;
            }

            expectedOffset += instruction.ByteLength;
        }

        if (expectedOffset != RequiredByteLength)
        {
            return X64PagingModePreflightStatus
                .StubSemanticPlanMismatch;
        }

        TryGetInstruction(0, out var first);
        TryGetInstruction(2, out var clearCompletion);
        TryGetInstruction(3, out var clearReserved);
        TryGetInstruction(4, out var flagsPush);
        TryGetInstruction(5, out var flagsPop);
        TryGetInstruction(6, out var flagsStore);
        TryGetInstruction(29, out var completion);
        TryGetInstruction(30, out var restore);
        TryGetInstruction(31, out var last);
        if (first.Kind !=
                X64PagingModePreflightInstructionKind
                    .PreserveNonvolatileRbx
            || first.StackDepthSlotsAfter != 1
            || clearCompletion.Kind !=
                X64PagingModePreflightInstructionKind.ClearCompletion
            || clearCompletion.ByteOffset != 4
            || clearReserved.Kind !=
                X64PagingModePreflightInstructionKind.ClearReserved
            || clearReserved.ByteOffset != 12
            || flagsPush.Kind !=
                X64PagingModePreflightInstructionKind.CaptureRflags
            || flagsPush.ByteOffset != 20
            || flagsPush.StackDepthSlotsAfter != 2
            || flagsPop.Kind !=
                X64PagingModePreflightInstructionKind
                    .MoveRflagsToVolatile
            || flagsPop.StackDepthSlotsAfter != 1
            || flagsStore.Kind !=
                X64PagingModePreflightInstructionKind.StoreRflags
            || flagsStore.ByteOffset != 22
            || completion.Kind !=
                X64PagingModePreflightInstructionKind.StoreCompletion
            || restore.Kind !=
                X64PagingModePreflightInstructionKind
                    .RestoreNonvolatileRbx
            || restore.StackDepthSlotsAfter != 0
            || last.Kind !=
                X64PagingModePreflightInstructionKind.Return
            || last.StackDepthSlotsAfter != 0)
        {
            return X64PagingModePreflightStatus
                .StubSemanticPlanMismatch;
        }

        return X64PagingModePreflightStatus.Success;
    }

    public static X64PagingModePreflightStubResult TryMaterialize(
        byte* destination,
        ulong destinationCapacity,
        ulong entryOffset,
        ulong declaredByteCount,
        X64PagingModePreflightObservation* observation,
        ulong observationByteLength,
        X64PagingModePreflightResult* state,
        ulong stateByteLength)
    {
        X64PagingModePreflightStatus status = ValidateStubStorage(
            destination,
            destinationCapacity,
            entryOffset,
            declaredByteCount,
            observation,
            observationByteLength,
            state,
            stateByteLength,
            out ulong entryAddress);
        if (status != X64PagingModePreflightStatus.Success)
        {
            return StubResult(
                status,
                X64PagingModePreflightOperation.Materialize,
                declaredByteCount,
                destinationCapacity,
                entryOffset,
                entryAddress,
                0,
                X64PagingModePreflightStubResult.NoMismatchOffset,
                exactBytes: false);
        }

        byte* entry = (byte*)entryAddress;
        for (ulong index = 0; index < RequiredByteLength; index++)
        {
            TryGetExpectedByte(index, out byte value);
            entry[index] = value;
        }

        return StubResult(
            X64PagingModePreflightStatus.Success,
            X64PagingModePreflightOperation.Materialize,
            declaredByteCount,
            destinationCapacity,
            entryOffset,
            entryAddress,
            RequiredByteLength,
            X64PagingModePreflightStubResult.NoMismatchOffset,
            exactBytes: true);
    }

    public static X64PagingModePreflightStubResult
        TryValidateMaterialized(
            byte* source,
            ulong sourceCapacity,
            ulong entryOffset,
            ulong declaredByteCount,
            X64PagingModePreflightObservation* observation,
            ulong observationByteLength,
            X64PagingModePreflightResult* state,
            ulong stateByteLength)
    {
        X64PagingModePreflightStatus status = ValidateStubStorage(
            source,
            sourceCapacity,
            entryOffset,
            declaredByteCount,
            observation,
            observationByteLength,
            state,
            stateByteLength,
            out ulong entryAddress);
        if (status != X64PagingModePreflightStatus.Success)
        {
            return StubResult(
                status,
                X64PagingModePreflightOperation.Validate,
                declaredByteCount,
                sourceCapacity,
                entryOffset,
                entryAddress,
                0,
                X64PagingModePreflightStubResult.NoMismatchOffset,
                exactBytes: false);
        }

        byte* entry = (byte*)entryAddress;
        for (ulong index = 0; index < RequiredByteLength; index++)
        {
            TryGetExpectedByte(index, out byte expected);
            if (entry[index] != expected)
            {
                return StubResult(
                    IsOpcodeByte(index)
                        ? X64PagingModePreflightStatus
                            .StubOpcodeMismatch
                        : X64PagingModePreflightStatus
                            .StubByteMismatch,
                    X64PagingModePreflightOperation.Validate,
                    declaredByteCount,
                    sourceCapacity,
                    entryOffset,
                    entryAddress,
                    index,
                    index,
                    exactBytes: false);
            }
        }

        return StubResult(
            X64PagingModePreflightStatus.Success,
            X64PagingModePreflightOperation.Validate,
            declaredByteCount,
            sourceCapacity,
            entryOffset,
            entryAddress,
            RequiredByteLength,
            X64PagingModePreflightStubResult.NoMismatchOffset,
            exactBytes: true);
    }

    public static X64PagingModePreflightResult Evaluate(
        X64PagingModePreflightRequest* request,
        ulong requestCount,
        ulong requestCapacity,
        X64PagingModePreflightObservation* observation,
        ulong observationCount,
        ulong observationCapacity,
        X64PagingModePreflightResult* state,
        ulong stateCount,
        ulong stateCapacity)
    {
        X64PagingModePreflightStatus storageStatus =
            ValidateEvaluationStorage(
                request,
                requestCount,
                requestCapacity,
                observation,
                observationCount,
                observationCapacity,
                state,
                stateCount,
                stateCapacity);
        if (storageStatus != X64PagingModePreflightStatus.Success)
        {
            return EvaluationResult(
                storageStatus,
                default,
                default,
                default,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                probeEnabled: false,
                pagingEnabled: false,
                writeProtectEnabled: false,
                paeEnabled: false,
                longModeEnabled: false,
                longModeActive: false,
                nxeEnabled: false,
                la57Enabled: false,
                pcideEnabled: false,
                cpuidAddressLeafAvailable: false,
                ownedRootAligned: false,
                ownedRootInsideArena: false);
        }

        X64PagingModePreflightRequest requestValue = *request;
        X64PagingModePreflightObservation observationValue =
            *observation;

        bool enabled = requestValue.ProbeEnabled;
        ulong cr0 = observationValue.Cr0;
        ulong rawCr3 = observationValue.RawCr3;
        ulong rawCr4 = observationValue.RawCr4;
        ulong efer = observationValue.Ia32Efer;
        ulong rflags = observationValue.Rflags;
        bool paging = (cr0 & Cr0PagingBit) != 0;
        bool writeProtect = (cr0 & Cr0WriteProtectBit) != 0;
        bool pae = (rawCr4 & Cr4PaeBit) != 0;
        bool la57 = (rawCr4 & Cr4La57Bit) != 0;
        bool pcide = (rawCr4 & Cr4PcideBit) != 0;
        bool lme = (efer & EferLongModeEnableBit) != 0;
        bool lma = (efer & EferLongModeActiveBit) != 0;
        bool nxe = (efer & EferNoExecuteEnableBit) != 0;
        bool cpuidLeaf =
            observationValue.CpuidMaximumExtendedLeaf >=
                CpuidAddressSizesLeaf;
        byte width = observationValue.PhysicalAddressWidth;
        ulong physicalMask = 0;
        bool rootAligned = false;
        bool rootInsideArena = false;
        OS.Boot.PhysicalAddress root = requestValue.OwnedRoot;
        OS.Boot.PhysicalAddress arenaStart =
            requestValue.TableArenaStart;
        OS.Boot.PhysicalAddress arenaEnd = default;

        X64PagingModePreflightStatus status;
        if (!requestValue.IsInitialized)
        {
            status = IsAllZero(request)
                ? X64PagingModePreflightStatus.RequestNotInitialized
                : X64PagingModePreflightStatus.InvalidRequestState;
        }
        else if (!enabled)
        {
            status = X64PagingModePreflightStatus.ProbeDisabled;
        }
        else if (!observationValue.IsInitialized)
        {
            status = IsObservationAllZero(observation)
                ? X64PagingModePreflightStatus
                    .ObservationNotInitialized
                : X64PagingModePreflightStatus.InvalidObservationState;
        }
        else if (!cpuidLeaf)
        {
            status = X64PagingModePreflightStatus
                .CpuidAddressLeafUnavailable;
        }
        else if (width < MinimumPhysicalAddressWidth
            || width > MaximumPhysicalAddressWidth)
        {
            status = X64PagingModePreflightStatus
                .UnsupportedPhysicalAddressWidth;
        }
        else
        {
            physicalMask = (1UL << width) - 1;
            if (!paging)
            {
                status = X64PagingModePreflightStatus
                    .PagingNotEnabled;
            }
            else if (!writeProtect)
            {
                status = X64PagingModePreflightStatus
                    .WriteProtectNotEnabled;
            }
            else if (!pae)
            {
                status = X64PagingModePreflightStatus.PaeNotEnabled;
            }
            else if (!lme)
            {
                status = X64PagingModePreflightStatus
                    .LongModeNotEnabled;
            }
            else if (!lma)
            {
                status = X64PagingModePreflightStatus
                    .LongModeNotActive;
            }
            else if (!nxe)
            {
                status = X64PagingModePreflightStatus.NxeNotEnabled;
            }
            else if (la57)
            {
                status = X64PagingModePreflightStatus
                    .FiveLevelPagingNotSupported;
            }
            else if (pcide)
            {
                status = X64PagingModePreflightStatus
                    .PcideNotSupported;
            }
            else if ((rflags & RflagsArchitecturalOneBit) == 0)
            {
                status = X64PagingModePreflightStatus.InvalidRflags;
            }
            else
            {
                ulong allowedCr3 =
                    (physicalMask & ~0xfffUL) | Cr3AllowedLowBits;
                ulong firmwareRoot =
                    rawCr3 & (physicalMask & ~0xfffUL);
                if ((rawCr3 & ~allowedCr3) != 0)
                {
                    status = X64PagingModePreflightStatus
                        .InvalidFirmwareCr3Bits;
                }
                else if (firmwareRoot == 0)
                {
                    status = X64PagingModePreflightStatus
                        .InvalidFirmwareCr3Base;
                }
                else
                {
                    status = ValidateRootAndArena(
                        requestValue,
                        physicalMask,
                        out rootAligned,
                        out rootInsideArena,
                        out arenaEnd);
                }
            }
        }

        X64PagingModePreflightResult result = EvaluationResult(
            status,
            root,
            arenaStart,
            arenaEnd,
            cr0,
            rawCr3,
            rawCr4,
            efer,
            rflags,
            physicalMask,
            width,
            enabled,
            paging,
            writeProtect,
            pae,
            lme,
            lma,
            nxe,
            la57,
            pcide,
            cpuidLeaf,
            rootAligned,
            rootInsideArena);
        *state = result;
        return result;
    }

    private static X64PagingModePreflightInstruction Instruction(
        X64PagingModePreflightInstructionKind kind,
        X64PagingModePreflightRegister destination,
        X64PagingModePreflightRegister source,
        uint byteOffset,
        ushort byteLength,
        ulong operand,
        byte stackDepthSlotsAfter)
    {
        return new X64PagingModePreflightInstruction(
            kind,
            destination,
            source,
            byteOffset,
            byteLength,
            operand,
            stackDepthSlotsAfter);
    }

    private static X64PagingModePreflightStubResult StubResult(
        X64PagingModePreflightStatus status,
        X64PagingModePreflightOperation operation,
        ulong declaredByteCount,
        ulong bufferCapacity,
        ulong entryOffset,
        ulong entryAddress,
        ulong bytesProcessed,
        ulong mismatchOffset,
        bool exactBytes)
    {
        return new X64PagingModePreflightStubResult(
            status,
            operation,
            declaredByteCount,
            bufferCapacity,
            entryOffset,
            entryAddress,
            bytesProcessed,
            mismatchOffset,
            exactBytes);
    }

    private static X64PagingModePreflightResult EvaluationResult(
        X64PagingModePreflightStatus status,
        OS.Boot.PhysicalAddress ownedRoot,
        OS.Boot.PhysicalAddress tableArenaStart,
        OS.Boot.PhysicalAddress tableArenaEndExclusive,
        ulong cr0,
        ulong rawFirmwareCr3,
        ulong rawCr4,
        ulong ia32Efer,
        ulong rflags,
        ulong physicalAddressMask,
        byte physicalAddressWidth,
        bool probeEnabled,
        bool pagingEnabled,
        bool writeProtectEnabled,
        bool paeEnabled,
        bool longModeEnabled,
        bool longModeActive,
        bool nxeEnabled,
        bool la57Enabled,
        bool pcideEnabled,
        bool cpuidAddressLeafAvailable,
        bool ownedRootAligned,
        bool ownedRootInsideArena)
    {
        return new X64PagingModePreflightResult(
            status,
            ownedRoot,
            tableArenaStart,
            tableArenaEndExclusive,
            cr0,
            rawFirmwareCr3,
            rawCr4,
            ia32Efer,
            rflags,
            physicalAddressMask,
            physicalAddressWidth,
            probeEnabled,
            pagingEnabled,
            writeProtectEnabled,
            paeEnabled,
            longModeEnabled,
            longModeActive,
            nxeEnabled,
            la57Enabled,
            pcideEnabled,
            cpuidAddressLeafAvailable,
            ownedRootAligned,
            ownedRootInsideArena);
    }

    private static X64PagingModePreflightStatus ValidateStubStorage(
        byte* buffer,
        ulong bufferCapacity,
        ulong entryOffset,
        ulong declaredByteCount,
        X64PagingModePreflightObservation* observation,
        ulong observationByteLength,
        X64PagingModePreflightResult* state,
        ulong stateByteLength,
        out ulong entryAddress)
    {
        entryAddress = 0;
        if ((ulong)sizeof(X64PagingModePreflightInstruction)
                != X64PagingModePreflightInstruction.StructureSize
            || (ulong)sizeof(X64PagingModePreflightObservation)
                != X64PagingModePreflightObservation.StructureSize
            || (ulong)sizeof(X64PagingModePreflightRequest)
                != X64PagingModePreflightRequest.StructureSize
            || (ulong)sizeof(X64PagingModePreflightResult)
                != X64PagingModePreflightResult.StructureSize
            || (ulong)sizeof(X64PagingModePreflightStubResult)
                != X64PagingModePreflightStubResult.StructureSize)
        {
            return X64PagingModePreflightStatus.ContractLayoutMismatch;
        }

        if (ValidateSemanticPlan()
            != X64PagingModePreflightStatus.Success)
        {
            return X64PagingModePreflightStatus
                .StubSemanticPlanMismatch;
        }

        if (declaredByteCount != RequiredByteLength)
        {
            return X64PagingModePreflightStatus.InvalidStubByteCount;
        }

        if (bufferCapacity < StubPageByteLength)
        {
            return X64PagingModePreflightStatus.StubCapacityTooSmall;
        }

        if (bufferCapacity > StubPageByteLength)
        {
            return X64PagingModePreflightStatus.StubCapacityTooLarge;
        }

        if (buffer == null)
        {
            return X64PagingModePreflightStatus.MissingStubBuffer;
        }

        ulong bufferAddress = (ulong)buffer;
        if ((bufferAddress & (RequiredEntryAlignment - 1)) != 0)
        {
            return X64PagingModePreflightStatus.MisalignedStubBuffer;
        }

        if (!TryGetEnd(
                bufferAddress,
                bufferCapacity,
                out ulong bufferEnd))
        {
            return X64PagingModePreflightStatus
                .StubBufferSpanOverflow;
        }

        if (entryOffset >= bufferCapacity)
        {
            return X64PagingModePreflightStatus.InvalidEntryOffset;
        }

        if ((entryOffset & (RequiredEntryAlignment - 1)) != 0)
        {
            return X64PagingModePreflightStatus.MisalignedEntry;
        }

        if (bufferAddress > ~0UL - entryOffset)
        {
            return X64PagingModePreflightStatus.EntrySpanOverflow;
        }

        entryAddress = bufferAddress + entryOffset;
        if (!TryGetEnd(
                entryAddress,
                RequiredByteLength,
                out ulong entryEnd)
            || entryEnd > bufferEnd)
        {
            return X64PagingModePreflightStatus.EntrySpanOverflow;
        }

        if (observation == null)
        {
            return X64PagingModePreflightStatus
                .MissingObservationStorage;
        }

        if (observationByteLength <
            X64PagingModePreflightObservation.StructureSize)
        {
            return X64PagingModePreflightStatus
                .ObservationStorageSizeTooSmall;
        }

        if (observationByteLength >
            X64PagingModePreflightObservation.StructureSize)
        {
            return X64PagingModePreflightStatus
                .ObservationStorageSizeTooLarge;
        }

        ulong observationAddress = (ulong)observation;
        if ((observationAddress & (RequiredDataAlignment - 1)) != 0)
        {
            return X64PagingModePreflightStatus
                .MisalignedObservationStorage;
        }

        if (!TryGetEnd(
                observationAddress,
                observationByteLength,
                out ulong observationEnd))
        {
            return X64PagingModePreflightStatus
                .ObservationStorageSpanOverflow;
        }

        if (state == null)
        {
            return X64PagingModePreflightStatus.MissingStateStorage;
        }

        if (stateByteLength <
            X64PagingModePreflightResult.StructureSize)
        {
            return X64PagingModePreflightStatus
                .StateStorageSizeTooSmall;
        }

        if (stateByteLength >
            X64PagingModePreflightResult.StructureSize)
        {
            return X64PagingModePreflightStatus
                .StateStorageSizeTooLarge;
        }

        ulong stateAddress = (ulong)state;
        if ((stateAddress & (RequiredDataAlignment - 1)) != 0)
        {
            return X64PagingModePreflightStatus
                .MisalignedStateStorage;
        }

        if (!TryGetEnd(
                stateAddress,
                stateByteLength,
                out ulong stateEnd))
        {
            return X64PagingModePreflightStatus
                .StateStorageSpanOverflow;
        }

        if (RangesOverlap(
                bufferAddress,
                bufferEnd,
                observationAddress,
                observationEnd))
        {
            return X64PagingModePreflightStatus
                .StubObservationOverlap;
        }

        if (RangesOverlap(
                bufferAddress,
                bufferEnd,
                stateAddress,
                stateEnd))
        {
            return X64PagingModePreflightStatus.StubStateOverlap;
        }

        if (RangesOverlap(
                observationAddress,
                observationEnd,
                stateAddress,
                stateEnd))
        {
            return X64PagingModePreflightStatus
                .ObservationStateOverlap;
        }

        return X64PagingModePreflightStatus.Success;
    }

    private static X64PagingModePreflightStatus
        ValidateEvaluationStorage(
            X64PagingModePreflightRequest* request,
            ulong requestCount,
            ulong requestCapacity,
            X64PagingModePreflightObservation* observation,
            ulong observationCount,
            ulong observationCapacity,
            X64PagingModePreflightResult* state,
            ulong stateCount,
            ulong stateCapacity)
    {
        if ((ulong)sizeof(X64PagingModePreflightObservation)
                != X64PagingModePreflightObservation.StructureSize
            || (ulong)sizeof(X64PagingModePreflightRequest)
                != X64PagingModePreflightRequest.StructureSize
            || (ulong)sizeof(X64PagingModePreflightResult)
                != X64PagingModePreflightResult.StructureSize)
        {
            return X64PagingModePreflightStatus.ContractLayoutMismatch;
        }

        if (requestCount != 1)
        {
            return X64PagingModePreflightStatus.InvalidRequestCount;
        }

        if (observationCount != 1)
        {
            return X64PagingModePreflightStatus.InvalidObservationCount;
        }

        if (stateCount != 1)
        {
            return X64PagingModePreflightStatus.InvalidStateCount;
        }

        if (requestCapacity > MaximumRecordCount
            || observationCapacity > MaximumRecordCount
            || stateCapacity > MaximumRecordCount)
        {
            return X64PagingModePreflightStatus.CapacityExceedsLimit;
        }

        if (requestCapacity < requestCount)
        {
            return X64PagingModePreflightStatus
                .RequestCapacityTooSmall;
        }

        if (observationCapacity < observationCount)
        {
            return X64PagingModePreflightStatus
                .ObservationCapacityTooSmall;
        }

        if (stateCapacity < stateCount)
        {
            return X64PagingModePreflightStatus.StateCapacityTooSmall;
        }

        if (request == null)
        {
            return X64PagingModePreflightStatus.MissingRequest;
        }

        if (observation == null)
        {
            return X64PagingModePreflightStatus.MissingObservation;
        }

        if (state == null)
        {
            return X64PagingModePreflightStatus.MissingState;
        }

        ulong requestAddress = (ulong)request;
        ulong observationAddress = (ulong)observation;
        ulong stateAddress = (ulong)state;
        if ((requestAddress & (RequiredDataAlignment - 1)) != 0)
        {
            return X64PagingModePreflightStatus.MisalignedRequest;
        }

        if ((observationAddress & (RequiredDataAlignment - 1)) != 0)
        {
            return X64PagingModePreflightStatus.MisalignedObservation;
        }

        if ((stateAddress & (RequiredDataAlignment - 1)) != 0)
        {
            return X64PagingModePreflightStatus.MisalignedState;
        }

        if (!TryGetEnd(
                requestAddress,
                X64PagingModePreflightRequest.StructureSize,
                out ulong requestEnd))
        {
            return X64PagingModePreflightStatus.RequestSpanOverflow;
        }

        if (!TryGetEnd(
                observationAddress,
                X64PagingModePreflightObservation.StructureSize,
                out ulong observationEnd))
        {
            return X64PagingModePreflightStatus
                .ObservationSpanOverflow;
        }

        if (!TryGetEnd(
                stateAddress,
                X64PagingModePreflightResult.StructureSize,
                out ulong stateEnd))
        {
            return X64PagingModePreflightStatus.StateSpanOverflow;
        }

        if (RangesOverlap(
                requestAddress,
                requestEnd,
                observationAddress,
                observationEnd))
        {
            return X64PagingModePreflightStatus
                .RequestObservationOverlap;
        }

        if (RangesOverlap(
                requestAddress,
                requestEnd,
                stateAddress,
                stateEnd))
        {
            return X64PagingModePreflightStatus.RequestStateOverlap;
        }

        if (RangesOverlap(
                observationAddress,
                observationEnd,
                stateAddress,
                stateEnd))
        {
            return X64PagingModePreflightStatus
                .EvaluationObservationStateOverlap;
        }

        return X64PagingModePreflightStatus.Success;
    }

    private static X64PagingModePreflightStatus ValidateRootAndArena(
        X64PagingModePreflightRequest request,
        ulong physicalMask,
        out bool rootAligned,
        out bool rootInsideArena,
        out OS.Boot.PhysicalAddress arenaEnd)
    {
        rootAligned = false;
        rootInsideArena = false;
        arenaEnd = default;

        if (request.OwnedRoot
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong root)
            != OS.Boot.AddressValueStatus.Success)
        {
            return X64PagingModePreflightStatus.InvalidOwnedRoot;
        }

        rootAligned = (root & (PageSize - 1)) == 0;
        if (!rootAligned)
        {
            return X64PagingModePreflightStatus.MisalignedOwnedRoot;
        }

        if (request.TableArenaStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong start)
            != OS.Boot.AddressValueStatus.Success)
        {
            return X64PagingModePreflightStatus.InvalidTableArena;
        }

        if ((start & (PageSize - 1)) != 0)
        {
            return X64PagingModePreflightStatus.MisalignedTableArena;
        }

        ulong length = request.TableArenaByteLength;
        if (length == 0)
        {
            return X64PagingModePreflightStatus
                .TableArenaLengthZero;
        }

        if ((length & (PageSize - 1)) != 0)
        {
            return X64PagingModePreflightStatus
                .TableArenaLengthUnaligned;
        }

        if (!TryGetEnd(start, length, out ulong end))
        {
            return X64PagingModePreflightStatus.TableArenaOverflow;
        }

        if ((start & ~physicalMask) != 0
            || ((end - 1) & ~physicalMask) != 0)
        {
            return X64PagingModePreflightStatus
                .TableArenaOutsidePhysicalWidth;
        }

        if ((root & ~physicalMask) != 0)
        {
            return X64PagingModePreflightStatus
                .OwnedRootOutsidePhysicalWidth;
        }

        if (root < start || root >= end)
        {
            return X64PagingModePreflightStatus
                .OwnedRootOutsideArena;
        }

        if (!TryGetEnd(root, PageSize, out ulong rootEnd)
            || rootEnd > end)
        {
            return X64PagingModePreflightStatus
                .OwnedRootPageOutsideArena;
        }

        if (OS.Boot.PhysicalAddress
                .TryCreateFromHardwareRegisterBoundary(
                    end,
                    out arenaEnd)
            != OS.Boot.AddressValueStatus.Success)
        {
            return X64PagingModePreflightStatus.TableArenaOverflow;
        }

        rootInsideArena = true;
        return X64PagingModePreflightStatus.Success;
    }

    private static bool IsAllZero(
        X64PagingModePreflightRequest* request)
    {
        byte* bytes = (byte*)request;
        for (ulong index = 0;
            index < X64PagingModePreflightRequest.StructureSize;
            index++)
        {
            if (bytes[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsObservationAllZero(
        X64PagingModePreflightObservation* observation)
    {
        byte* bytes = (byte*)observation;
        for (ulong index = 0;
            index < X64PagingModePreflightObservation.StructureSize;
            index++)
        {
            if (bytes[index] != 0)
            {
                return false;
            }
        }

        return true;
    }

    // StubOpcodeMismatch is intentionally narrow and deterministic: it means
    // the mismatching byte is a REX prefix, an opcode-escape byte, or a
    // primary/secondary opcode byte. ModRM, displacement, branch-relative,
    // and immediate bytes are classified as StubByteMismatch even when
    // corrupting one would also change the decoded instruction's behavior.
    private static bool IsOpcodeByte(ulong index)
    {
        switch (index)
        {
            case 0:
            case 1:
            case 2:
            case 4:
            case 5:
            case 12:
            case 13:
            case 20:
            case 21:
            case 22:
            case 23:
            case 26:
            case 27:
            case 29:
            case 30:
            case 32:
            case 33:
            case 35:
            case 36:
            case 39:
            case 40:
            case 42:
            case 43:
            case 46:
            case 51:
            case 52:
            case 53:
            case 54:
            case 57:
            case 58:
            case 60:
            case 61:
            case 64:
            case 69:
            case 70:
            case 71:
            case 72:
            case 75:
            case 80:
            case 82:
            case 87:
            case 88:
            case 89:
            case 90:
            case 93:
            case 95:
            case 97:
            case 98:
            case 101:
            case 102:
            case 109:
            case 110:
                return true;
            default:
                return false;
        }
    }

    private static bool TryGetEnd(
        ulong start,
        ulong byteLength,
        out ulong endExclusive)
    {
        endExclusive = 0;
        if (byteLength == 0 || start > ~0UL - byteLength)
        {
            return false;
        }

        endExclusive = start + byteLength;
        return endExclusive > start;
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
}
