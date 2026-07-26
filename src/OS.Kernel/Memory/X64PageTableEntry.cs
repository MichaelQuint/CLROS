namespace OS.Kernel.Memory;

/// <summary>
/// Identifies the single canonical four-level x64 page-table level token.
/// Numeric values intentionally match the walk depth from a leaf PTE.
/// </summary>
public enum X64PageTableLevel : byte
{
    Uninitialized = 0,
    Pte = 1,
    Pde = 2,
    Pdpte = 3,
    Pml4e = 4
}

/// <summary>
/// Distinguishes an absent entry, an upper-level table pointer, and a leaf
/// mapping. A table pointer is not itself a leaf memory mapping.
/// </summary>
public enum X64PageTableEntryKind : byte
{
    Uninitialized = 0,
    Unmapped = 1,
    TablePointer = 2,
    Leaf = 3
}

/// <summary>
/// Controls whether a protection-only leaf rewrite retains the processor's
/// caller-supplied Accessed and Dirty observations.
/// </summary>
public enum X64HardwareStatePolicy : byte
{
    Uninitialized = 0,
    Clear = 1,
    Preserve = 2
}

/// <summary>
/// Stable status contract for x64 page-table entry validation and encoding.
/// Values are append-only.
/// </summary>
public enum X64PageTableEntryStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    RequestNotInitialized = 2,
    InvalidLevel = 3,
    InvalidEntryKind = 4,
    InvalidUnmappedEntry = 5,
    InvalidTablePointer = 6,
    InvalidProtection = 7,
    WritableExecutableRejected = 8,
    InvalidCachePolicy = 9,
    CachePolicyNotSupported = 10,
    PatMappingNotInitialized = 11,
    InvalidPatEntry = 12,
    PatIndexOutOfRange = 13,
    PatMemoryTypeMismatch = 14,
    PreconditionsNotInitialized = 15,
    NxeNotEnabled = 16,
    WriteProtectNotEnabled = 17,
    FiveLevelPagingNotSupported = 18,
    UnsupportedPhysicalAddressWidth = 19,
    InvalidPhysicalAddress = 20,
    MisalignedPhysicalAddress = 21,
    PhysicalAddressOutsideWidth = 22,
    NonCanonicalUnmappedEntry = 23,
    UnsupportedEntryBits = 24,
    InvalidLargePage = 25,
    DirtyBitOnTablePointer = 26,
    NonCanonicalTablePointer = 27,
    InvalidBatchCount = 28,
    TooManyEntries = 29,
    SourceCapacityTooSmall = 30,
    DestinationCapacityTooSmall = 31,
    CapacityExceedsLimit = 32,
    MissingSource = 33,
    MissingDestination = 34,
    MisalignedSource = 35,
    MisalignedDestination = 36,
    SourceSpanOverflow = 37,
    DestinationSpanOverflow = 38,
    SourceDestinationOverlap = 39,
    ContractLayoutMismatch = 40,
    InvalidPatMappingState = 41,
    InvalidPreconditionState = 42,
    OneGiBPagesNotSupported = 43,
    InvalidRequestState = 44,
    InvalidHardwareStatePolicy = 45,
    ExistingEntryNotLeaf = 46
}

/// <summary>
/// Binds the three CLROS cache policies to explicitly selected entries in one
/// observed IA32_PAT value. The token is caller-supplied value evidence only;
/// it does not read the MSR and does not prove that the value is live.
/// </summary>
public readonly struct X64PatMappingToken
{
    public const ulong StructureSize = 16;

    public const byte UnsupportedIndex = 0xff;

    private const byte UncachedMemoryType = 0;
    private const byte WriteCombiningMemoryType = 1;
    private const byte WriteBackMemoryType = 6;
    private const byte UncacheableMinusMemoryType = 7;
    private const byte WriteThroughMemoryType = 4;
    private const byte WriteProtectedMemoryType = 5;
    private const byte PatEntryReservedMask = 0xf8;

    private readonly ulong _rawIa32Pat;
    private readonly byte _isInitialized;
    private readonly byte _writeBackIndex;
    private readonly byte _uncachedIndex;
    private readonly byte _writeCombiningIndex;
    private readonly uint _reserved;

    private X64PatMappingToken(
        ulong rawIa32Pat,
        byte writeBackIndex,
        byte uncachedIndex,
        byte writeCombiningIndex)
    {
        _rawIa32Pat = rawIa32Pat;
        _isInitialized = 1;
        _writeBackIndex = writeBackIndex;
        _uncachedIndex = uncachedIndex;
        _writeCombiningIndex = writeCombiningIndex;
        _reserved = 0;
    }

    public bool IsInitialized => _isInitialized == 1;

    public ulong RawIa32Pat => _rawIa32Pat;

    public byte WriteBackIndex => _writeBackIndex;

    public byte UncachedIndex => _uncachedIndex;

    public byte WriteCombiningIndex => _writeCombiningIndex;

    public X64PageTableEntryStatus Validate()
    {
        if (_isInitialized == 0)
        {
            return X64PageTableEntryStatus.PatMappingNotInitialized;
        }

        if (_isInitialized != 1)
        {
            return X64PageTableEntryStatus.InvalidPatMappingState;
        }

        if (_reserved != 0)
        {
            return X64PageTableEntryStatus.InvalidPatMappingState;
        }

        for (byte index = 0; index < 8; index++)
        {
            byte entry = ReadEntry(_rawIa32Pat, index);
            if ((entry & PatEntryReservedMask) != 0 ||
                !IsArchitecturalMemoryType(entry))
            {
                return X64PageTableEntryStatus.InvalidPatEntry;
            }
        }

        X64PageTableEntryStatus status = ValidateSelectedIndex(
            _rawIa32Pat,
            _writeBackIndex,
            WriteBackMemoryType,
            allowUnsupported: false);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        status = ValidateSelectedIndex(
            _rawIa32Pat,
            _uncachedIndex,
            UncachedMemoryType,
            allowUnsupported: true);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        return ValidateSelectedIndex(
            _rawIa32Pat,
            _writeCombiningIndex,
            WriteCombiningMemoryType,
            allowUnsupported: true);
    }

    public static X64PageTableEntryStatus TryCreate(
        ulong rawIa32Pat,
        byte writeBackIndex,
        byte uncachedIndex,
        byte writeCombiningIndex,
        out X64PatMappingToken token)
    {
        token = default;

        for (byte index = 0; index < 8; index++)
        {
            byte entry = ReadEntry(rawIa32Pat, index);
            if ((entry & PatEntryReservedMask) != 0 ||
                !IsArchitecturalMemoryType(entry))
            {
                return X64PageTableEntryStatus.InvalidPatEntry;
            }
        }

        X64PageTableEntryStatus status = ValidateSelectedIndex(
            rawIa32Pat,
            writeBackIndex,
            WriteBackMemoryType,
            allowUnsupported: false);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        status = ValidateSelectedIndex(
            rawIa32Pat,
            uncachedIndex,
            UncachedMemoryType,
            allowUnsupported: true);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        status = ValidateSelectedIndex(
            rawIa32Pat,
            writeCombiningIndex,
            WriteCombiningMemoryType,
            allowUnsupported: true);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        token = new X64PatMappingToken(
            rawIa32Pat,
            writeBackIndex,
            uncachedIndex,
            writeCombiningIndex);
        return X64PageTableEntryStatus.Success;
    }

    public X64PageTableEntryStatus TryGetIndex(
        PageCachePolicy policy,
        out byte index)
    {
        index = UnsupportedIndex;

        X64PageTableEntryStatus validation = Validate();
        if (validation != X64PageTableEntryStatus.Success)
        {
            return validation;
        }

        if (policy == PageCachePolicy.WriteBack)
        {
            index = _writeBackIndex;
            return X64PageTableEntryStatus.Success;
        }

        if (policy == PageCachePolicy.Uncached)
        {
            if (_uncachedIndex == UnsupportedIndex)
            {
                return X64PageTableEntryStatus.CachePolicyNotSupported;
            }

            index = _uncachedIndex;
            return X64PageTableEntryStatus.Success;
        }

        if (policy == PageCachePolicy.WriteCombining)
        {
            if (_writeCombiningIndex == UnsupportedIndex)
            {
                return X64PageTableEntryStatus.CachePolicyNotSupported;
            }

            index = _writeCombiningIndex;
            return X64PageTableEntryStatus.Success;
        }

        return X64PageTableEntryStatus.InvalidCachePolicy;
    }

    public X64PageTableEntryStatus TryGetPolicy(
        byte index,
        out PageCachePolicy policy)
    {
        policy = PageCachePolicy.Uninitialized;

        X64PageTableEntryStatus validation = Validate();
        if (validation != X64PageTableEntryStatus.Success)
        {
            return validation;
        }

        if (index > 7)
        {
            return X64PageTableEntryStatus.PatIndexOutOfRange;
        }

        if (index == _writeBackIndex)
        {
            policy = PageCachePolicy.WriteBack;
            return X64PageTableEntryStatus.Success;
        }

        if (_uncachedIndex != UnsupportedIndex &&
            index == _uncachedIndex)
        {
            policy = PageCachePolicy.Uncached;
            return X64PageTableEntryStatus.Success;
        }

        if (_writeCombiningIndex != UnsupportedIndex &&
            index == _writeCombiningIndex)
        {
            policy = PageCachePolicy.WriteCombining;
            return X64PageTableEntryStatus.Success;
        }

        return X64PageTableEntryStatus.CachePolicyNotSupported;
    }

    public X64PageTableEntryStatus TryValidateMemoryTypeAtIndex(
        byte index,
        PageCachePolicy policy)
    {
        X64PageTableEntryStatus validation = Validate();
        if (validation != X64PageTableEntryStatus.Success)
        {
            return validation;
        }

        if (index > 7)
        {
            return X64PageTableEntryStatus.PatIndexOutOfRange;
        }

        byte expectedMemoryType;
        if (policy == PageCachePolicy.WriteBack)
        {
            expectedMemoryType = WriteBackMemoryType;
        }
        else if (policy == PageCachePolicy.Uncached)
        {
            expectedMemoryType = UncachedMemoryType;
        }
        else if (policy == PageCachePolicy.WriteCombining)
        {
            expectedMemoryType = WriteCombiningMemoryType;
        }
        else
        {
            return X64PageTableEntryStatus.InvalidCachePolicy;
        }

        return ReadEntry(_rawIa32Pat, index) == expectedMemoryType
            ? X64PageTableEntryStatus.Success
            : X64PageTableEntryStatus.PatMemoryTypeMismatch;
    }

    private static X64PageTableEntryStatus ValidateSelectedIndex(
        ulong rawIa32Pat,
        byte index,
        byte expectedMemoryType,
        bool allowUnsupported)
    {
        if (index == UnsupportedIndex)
        {
            return allowUnsupported
                ? X64PageTableEntryStatus.Success
                : X64PageTableEntryStatus.PatIndexOutOfRange;
        }

        if (index > 7)
        {
            return X64PageTableEntryStatus.PatIndexOutOfRange;
        }

        return ReadEntry(rawIa32Pat, index) == expectedMemoryType
            ? X64PageTableEntryStatus.Success
            : X64PageTableEntryStatus.PatMemoryTypeMismatch;
    }

    private static byte ReadEntry(ulong rawIa32Pat, byte index)
    {
        return (byte)(rawIa32Pat >> (index * 8));
    }

    private static bool IsArchitecturalMemoryType(byte memoryType)
    {
        return memoryType == UncachedMemoryType
            || memoryType == WriteCombiningMemoryType
            || memoryType == WriteThroughMemoryType
            || memoryType == WriteProtectedMemoryType
            || memoryType == WriteBackMemoryType
            || memoryType == UncacheableMinusMemoryType;
    }
}

/// <summary>
/// Records the caller's observed prerequisites for encoding present mappings.
/// It does not read CR0, CR4, EFER, CPUID, or IA32_PAT and is not by itself
/// evidence that those observations are live.
/// </summary>
public readonly struct X64PageTableEncodingPreconditions
{
    public const ulong StructureSize = 24;

    private readonly X64PatMappingToken _patMapping;
    private readonly byte _isInitialized;
    private readonly byte _physicalAddressWidth;
    private readonly byte _nxeEnabled;
    private readonly byte _writeProtectEnabled;
    private readonly byte _fiveLevelPagingEnabled;
    private readonly byte _oneGiBPagesSupported;
    private readonly byte _reserved0;
    private readonly byte _reserved1;

    private X64PageTableEncodingPreconditions(
        byte physicalAddressWidth,
        bool nxeEnabled,
        bool writeProtectEnabled,
        bool fiveLevelPagingEnabled,
        bool oneGiBPagesSupported,
        X64PatMappingToken patMapping)
    {
        _patMapping = patMapping;
        _isInitialized = 1;
        _physicalAddressWidth = physicalAddressWidth;
        _nxeEnabled = nxeEnabled ? (byte)1 : (byte)0;
        _writeProtectEnabled = writeProtectEnabled ? (byte)1 : (byte)0;
        _fiveLevelPagingEnabled = fiveLevelPagingEnabled
            ? (byte)1
            : (byte)0;
        _oneGiBPagesSupported = oneGiBPagesSupported
            ? (byte)1
            : (byte)0;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized => _isInitialized == 1;

    public byte PhysicalAddressWidth => _physicalAddressWidth;

    public bool NxeEnabled => _nxeEnabled == 1;

    public bool WriteProtectEnabled => _writeProtectEnabled == 1;

    public bool FiveLevelPagingEnabled => _fiveLevelPagingEnabled == 1;

    public bool OneGiBPagesSupported => _oneGiBPagesSupported == 1;

    public X64PatMappingToken PatMapping => _patMapping;

    public X64PageTableEntryStatus Validate()
    {
        if (_isInitialized == 0)
        {
            return X64PageTableEntryStatus.PreconditionsNotInitialized;
        }

        if (_isInitialized != 1 ||
            _nxeEnabled > 1 ||
            _writeProtectEnabled > 1 ||
            _fiveLevelPagingEnabled > 1 ||
            _oneGiBPagesSupported > 1 ||
            _reserved0 != 0 ||
            _reserved1 != 0)
        {
            return X64PageTableEntryStatus.InvalidPreconditionState;
        }

        if (_physicalAddressWidth < X64PageTableEntry.MinimumPhysicalAddressWidth ||
            _physicalAddressWidth > X64PageTableEntry.MaximumPhysicalAddressWidth)
        {
            return X64PageTableEntryStatus.UnsupportedPhysicalAddressWidth;
        }

        if (_fiveLevelPagingEnabled == 1)
        {
            return X64PageTableEntryStatus.FiveLevelPagingNotSupported;
        }

        if (_nxeEnabled == 0)
        {
            return X64PageTableEntryStatus.NxeNotEnabled;
        }

        if (_writeProtectEnabled == 0)
        {
            return X64PageTableEntryStatus.WriteProtectNotEnabled;
        }

        return _patMapping.Validate();
    }

    public static X64PageTableEncodingPreconditions CreateObserved(
        byte physicalAddressWidth,
        bool nxeEnabled,
        bool writeProtectEnabled,
        bool fiveLevelPagingEnabled,
        bool oneGiBPagesSupported,
        X64PatMappingToken patMapping)
    {
        return new X64PageTableEncodingPreconditions(
            physicalAddressWidth,
            nxeEnabled,
            writeProtectEnabled,
            fiveLevelPagingEnabled,
            oneGiBPagesSupported,
            patMapping);
    }
}

/// <summary>
/// Unmanaged request for one canonical x64 page-table entry.
/// </summary>
public readonly struct X64PageTableEntryRequest
{
    public const ulong StructureSize = 16;

    private readonly OS.Boot.PhysicalAddress _physicalAddress;
    private readonly byte _isInitialized;
    private readonly X64PageTableLevel _level;
    private readonly X64PageTableEntryKind _kind;
    private readonly PageProtection _protection;
    private readonly PageCachePolicy _cachePolicy;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;

    public X64PageTableEntryRequest(
        X64PageTableLevel level,
        X64PageTableEntryKind kind,
        OS.Boot.PhysicalAddress physicalAddress,
        PageProtection protection,
        PageCachePolicy cachePolicy)
    {
        _physicalAddress = physicalAddress;
        _isInitialized = 1;
        _level = level;
        _kind = kind;
        _protection = protection;
        _cachePolicy = cachePolicy;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
    }

    public bool IsInitialized => _isInitialized == 1;

    public X64PageTableLevel Level => _level;

    public X64PageTableEntryKind Kind => _kind;

    public OS.Boot.PhysicalAddress PhysicalAddress => _physicalAddress;

    public PageProtection Protection => _protection;

    public PageCachePolicy CachePolicy => _cachePolicy;

    public X64PageTableEntryStatus Validate()
    {
        if (_isInitialized == 0)
        {
            return X64PageTableEntryStatus.RequestNotInitialized;
        }

        return _isInitialized == 1
                && _reserved0 == 0
                && _reserved1 == 0
                && _reserved2 == 0
            ? X64PageTableEntryStatus.Success
            : X64PageTableEntryStatus.InvalidRequestState;
    }
}

/// <summary>
/// Status-coded result for one encoded or decoded entry.
/// </summary>
public readonly struct X64PageTableEntryResult
{
    public const ulong StructureSize = 40;

    private readonly X64PageTableEntry _entry;
    private readonly OS.Boot.PhysicalAddress _physicalAddress;
    private readonly ulong _pageSizeBytes;
    private readonly byte _isInitialized;
    private readonly X64PageTableEntryStatus _status;
    private readonly X64PageTableLevel _level;
    private readonly X64PageTableEntryKind _kind;
    private readonly PageProtection _protection;
    private readonly PageCachePolicy _cachePolicy;
    private readonly byte _patIndex;
    private readonly byte _accessed;
    private readonly byte _dirty;

    private X64PageTableEntryResult(
        X64PageTableEntryStatus status,
        X64PageTableEntry entry,
        X64PageTableLevel level,
        X64PageTableEntryKind kind,
        OS.Boot.PhysicalAddress physicalAddress,
        PageProtection protection,
        PageCachePolicy cachePolicy,
        ulong pageSizeBytes,
        byte patIndex,
        bool accessed,
        bool dirty)
    {
        _entry = entry;
        _physicalAddress = physicalAddress;
        _pageSizeBytes = pageSizeBytes;
        _isInitialized = 1;
        _status = status;
        _level = level;
        _kind = kind;
        _protection = protection;
        _cachePolicy = cachePolicy;
        _patIndex = patIndex;
        _accessed = accessed ? (byte)1 : (byte)0;
        _dirty = dirty ? (byte)1 : (byte)0;
    }

    public bool IsInitialized => _isInitialized == 1;

    public X64PageTableEntryStatus Status => _status;

    public bool Succeeded =>
        IsInitialized && _status == X64PageTableEntryStatus.Success;

    public X64PageTableEntry Entry => _entry;

    public X64PageTableLevel Level => _level;

    public X64PageTableEntryKind Kind => _kind;

    public OS.Boot.PhysicalAddress PhysicalAddress => _physicalAddress;

    public PageProtection Protection => _protection;

    public PageCachePolicy CachePolicy => _cachePolicy;

    public ulong PageSizeBytes => _pageSizeBytes;

    public byte PatIndex => _patIndex;

    public bool IsPresent =>
        Succeeded && _kind != X64PageTableEntryKind.Unmapped;

    public bool IsLargePage =>
        Succeeded
        && _kind == X64PageTableEntryKind.Leaf
        && (_level == X64PageTableLevel.Pde
            || _level == X64PageTableLevel.Pdpte);

    public bool Accessed => _accessed == 1;

    public bool Dirty => _dirty == 1;

    internal static X64PageTableEntryResult Success(
        X64PageTableEntry entry,
        X64PageTableLevel level,
        X64PageTableEntryKind kind,
        OS.Boot.PhysicalAddress physicalAddress,
        PageProtection protection,
        PageCachePolicy cachePolicy,
        ulong pageSizeBytes,
        byte patIndex,
        bool accessed,
        bool dirty)
    {
        return new X64PageTableEntryResult(
            X64PageTableEntryStatus.Success,
            entry,
            level,
            kind,
            physicalAddress,
            protection,
            cachePolicy,
            pageSizeBytes,
            patIndex,
            accessed,
            dirty);
    }

    internal static X64PageTableEntryResult Fail(
        X64PageTableEntryStatus status)
    {
        return new X64PageTableEntryResult(
            status,
            default,
            X64PageTableLevel.Uninitialized,
            X64PageTableEntryKind.Uninitialized,
            default,
            PageProtection.None,
            PageCachePolicy.Uninitialized,
            0,
            X64PatMappingToken.UnsupportedIndex,
            accessed: false,
            dirty: false);
    }
}

/// <summary>
/// Result of the bounded caller-owned pointer/count/capacity encoder.
/// </summary>
public readonly struct X64PageTableEntryBatchResult
{
    public const ulong StructureSize = 48;

    private readonly ulong _requestCount;
    private readonly ulong _sourceCapacity;
    private readonly ulong _destinationCapacity;
    private readonly ulong _encodedCount;
    private readonly ulong _invalidRequestIndex;
    private readonly byte _isInitialized;
    private readonly X64PageTableEntryStatus _status;

    private X64PageTableEntryBatchResult(
        X64PageTableEntryStatus status,
        ulong requestCount,
        ulong sourceCapacity,
        ulong destinationCapacity,
        ulong encodedCount,
        ulong invalidRequestIndex)
    {
        _requestCount = requestCount;
        _sourceCapacity = sourceCapacity;
        _destinationCapacity = destinationCapacity;
        _encodedCount = encodedCount;
        _invalidRequestIndex = invalidRequestIndex;
        _isInitialized = 1;
        _status = status;
    }

    public bool IsInitialized => _isInitialized == 1;

    public X64PageTableEntryStatus Status => _status;

    public bool Succeeded =>
        IsInitialized
        && _status == X64PageTableEntryStatus.Success
        && _encodedCount == _requestCount
        && _invalidRequestIndex == X64PageTableEntry.NoIndex;

    public ulong RequestCount => _requestCount;

    public ulong SourceCapacity => _sourceCapacity;

    public ulong DestinationCapacity => _destinationCapacity;

    public ulong EncodedCount => _encodedCount;

    public ulong InvalidRequestIndex => _invalidRequestIndex;

    internal static X64PageTableEntryBatchResult Success(
        ulong requestCount,
        ulong sourceCapacity,
        ulong destinationCapacity)
    {
        return new X64PageTableEntryBatchResult(
            X64PageTableEntryStatus.Success,
            requestCount,
            sourceCapacity,
            destinationCapacity,
            requestCount,
            X64PageTableEntry.NoIndex);
    }

    internal static X64PageTableEntryBatchResult Fail(
        X64PageTableEntryStatus status,
        ulong requestCount,
        ulong sourceCapacity,
        ulong destinationCapacity,
        ulong invalidRequestIndex)
    {
        return new X64PageTableEntryBatchResult(
            status,
            requestCount,
            sourceCapacity,
            destinationCapacity,
            0,
            invalidRequestIndex);
    }
}

/// <summary>
/// Canonical CLROS x64 page-table entry value plus allocation-free
/// encoder/decoder operations. These operations never read or write live page
/// tables and never execute privileged instructions.
/// </summary>
public readonly struct X64PageTableEntry
{
    public const ulong StructureSize = 8;
    public const ulong MaximumBatchEntryCount = 512;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong NoIndex = ~0UL;

    public const ulong PageSize4KiB = 4096;
    public const ulong PageSize2MiB = 2UL * 1024 * 1024;
    public const ulong PageSize1GiB = 1024UL * 1024 * 1024;

    internal const byte MinimumPhysicalAddressWidth = 36;
    internal const byte MaximumPhysicalAddressWidth = 52;

    private const ulong PresentBit = 1UL << 0;
    private const ulong WritableBit = 1UL << 1;
    private const ulong UserBit = 1UL << 2;
    private const ulong PageWriteThroughBit = 1UL << 3;
    private const ulong PageCacheDisableBit = 1UL << 4;
    private const ulong AccessedBit = 1UL << 5;
    private const ulong DirtyBit = 1UL << 6;
    private const ulong PageSizeOrPtePatBit = 1UL << 7;
    private const ulong GlobalBit = 1UL << 8;
    private const ulong SoftwareLowMask = 7UL << 9;
    private const ulong LargePagePatBit = 1UL << 12;
    private const ulong SoftwareHighMask = 0x7fUL << 52;
    private const ulong ProtectionKeyMask = 0x0fUL << 59;
    private const ulong NoExecuteBit = 1UL << 63;

    private readonly ulong _rawValue;

    private X64PageTableEntry(ulong rawValue)
    {
        _rawValue = rawValue;
    }

    public ulong RawValue => _rawValue;

    public static X64PageTableEntryResult TryEncode(
        X64PageTableEntryRequest request,
        X64PageTableEncodingPreconditions preconditions)
    {
        X64PageTableEntryStatus requestStatus = request.Validate();
        if (requestStatus != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(requestStatus);
        }

        if (!IsValidLevel(request.Level))
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.InvalidLevel);
        }

        if (request.Kind != X64PageTableEntryKind.Unmapped &&
            request.Kind != X64PageTableEntryKind.TablePointer &&
            request.Kind != X64PageTableEntryKind.Leaf)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.InvalidEntryKind);
        }

        if (request.Kind == X64PageTableEntryKind.Unmapped)
        {
            return TryEncodeUnmapped(request);
        }

        X64PageTableEntryStatus status = ValidatePreconditions(
            preconditions);
        if (status != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(status);
        }

        if (request.Kind == X64PageTableEntryKind.TablePointer)
        {
            return TryEncodeTablePointer(request, preconditions);
        }

        return TryEncodeLeaf(request, preconditions);
    }

    public static X64PageTableEntryResult TryDecode(
        X64PageTableLevel level,
        ulong rawValue,
        X64PageTableEncodingPreconditions preconditions)
    {
        if (!IsValidLevel(level))
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.InvalidLevel);
        }

        if (rawValue == 0)
        {
            return X64PageTableEntryResult.Success(
                new X64PageTableEntry(0),
                level,
                X64PageTableEntryKind.Unmapped,
                default,
                PageProtection.None,
                PageCachePolicy.Uninitialized,
                0,
                X64PatMappingToken.UnsupportedIndex,
                accessed: false,
                dirty: false);
        }

        if ((rawValue & PresentBit) == 0)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.NonCanonicalUnmappedEntry);
        }

        X64PageTableEntryStatus status = ValidatePreconditions(
            preconditions);
        if (status != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(status);
        }

        bool large = (level == X64PageTableLevel.Pde
                || level == X64PageTableLevel.Pdpte)
            && (rawValue & PageSizeOrPtePatBit) != 0;

        if (level == X64PageTableLevel.Pml4e &&
            (rawValue & PageSizeOrPtePatBit) != 0)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.InvalidLargePage);
        }

        if (level == X64PageTableLevel.Pte || large)
        {
            return TryDecodeLeaf(
                level,
                rawValue,
                large,
                preconditions);
        }

        return TryDecodeTablePointer(
            level,
            rawValue,
            preconditions);
    }

    /// <summary>
    /// Re-encodes only the protection of an existing strict CLROS leaf.
    /// Caller-supplied Accessed and Dirty observations are either retained or
    /// cleared explicitly; no live page-table memory is read or written.
    /// </summary>
    public static X64PageTableEntryResult TryReencodeLeafProtection(
        X64PageTableLevel level,
        ulong currentRawValue,
        PageProtection replacementProtection,
        X64HardwareStatePolicy hardwareStatePolicy,
        X64PageTableEncodingPreconditions preconditions)
    {
        if (hardwareStatePolicy != X64HardwareStatePolicy.Clear &&
            hardwareStatePolicy != X64HardwareStatePolicy.Preserve)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.InvalidHardwareStatePolicy);
        }

        X64PageTableEntryResult current = TryDecode(
            level,
            currentRawValue,
            preconditions);
        if (!current.Succeeded)
        {
            return current;
        }

        if (current.Kind != X64PageTableEntryKind.Leaf)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.ExistingEntryNotLeaf);
        }

        X64PageTableEntryResult replacement = TryEncode(
            new X64PageTableEntryRequest(
                current.Level,
                X64PageTableEntryKind.Leaf,
                current.PhysicalAddress,
                replacementProtection,
                current.CachePolicy),
            preconditions);
        if (!replacement.Succeeded ||
            hardwareStatePolicy == X64HardwareStatePolicy.Clear)
        {
            return replacement;
        }

        ulong rawValue = replacement.Entry.RawValue;
        if (current.Accessed)
        {
            rawValue |= AccessedBit;
        }

        if (current.Dirty)
        {
            rawValue |= DirtyBit;
        }

        return X64PageTableEntryResult.Success(
            new X64PageTableEntry(rawValue),
            replacement.Level,
            replacement.Kind,
            replacement.PhysicalAddress,
            replacement.Protection,
            replacement.CachePolicy,
            replacement.PageSizeBytes,
            replacement.PatIndex,
            current.Accessed,
            current.Dirty);
    }

    /// <summary>
    /// Encodes a bounded logical prefix from caller-owned request storage into
    /// caller-owned destination storage. All pointer, span, alias, capacity,
    /// and request validation completes before the first destination write.
    /// </summary>
    public static unsafe X64PageTableEntryBatchResult TryEncodeMany(
        X64PageTableEntryRequest* source,
        ulong requestCount,
        ulong sourceCapacity,
        X64PageTableEntry* destination,
        ulong destinationCapacity,
        X64PageTableEncodingPreconditions preconditions)
    {
        if (requestCount == 0)
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.InvalidBatchCount,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        if (requestCount > MaximumBatchEntryCount)
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.TooManyEntries,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        if (sourceCapacity > MaximumBatchEntryCount ||
            destinationCapacity > MaximumBatchEntryCount)
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.CapacityExceedsLimit,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        if (requestCount > sourceCapacity)
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.SourceCapacityTooSmall,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        if (requestCount > destinationCapacity)
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.DestinationCapacityTooSmall,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        if ((ulong)sizeof(X64PageTableEntryRequest)
                != X64PageTableEntryRequest.StructureSize ||
            (ulong)sizeof(X64PageTableEntry) != StructureSize)
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.ContractLayoutMismatch,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        if (source == null)
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.MissingSource,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        if (destination == null)
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.MissingDestination,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        ulong sourceAddress = (ulong)source;
        ulong destinationAddress = (ulong)destination;
        if ((sourceAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.MisalignedSource,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        if ((destinationAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.MisalignedDestination,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        if (!TryGetSpanEnd(
                sourceAddress,
                sourceCapacity,
                X64PageTableEntryRequest.StructureSize,
                out ulong sourceEnd))
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.SourceSpanOverflow,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        if (!TryGetSpanEnd(
                destinationAddress,
                destinationCapacity,
                StructureSize,
                out ulong destinationEnd))
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.DestinationSpanOverflow,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        if (sourceAddress < destinationEnd &&
            destinationAddress < sourceEnd)
        {
            return X64PageTableEntryBatchResult.Fail(
                X64PageTableEntryStatus.SourceDestinationOverlap,
                requestCount,
                sourceCapacity,
                destinationCapacity,
                NoIndex);
        }

        for (ulong index = 0; index < requestCount; index++)
        {
            X64PageTableEntryResult result = TryEncode(
                source[index],
                preconditions);
            if (!result.Succeeded)
            {
                return X64PageTableEntryBatchResult.Fail(
                    result.Status,
                    requestCount,
                    sourceCapacity,
                    destinationCapacity,
                    index);
            }
        }

        // The caller contract requires the complete source span to remain
        // stable for the call. Re-encoding after the validation-only pass
        // avoids a page-sized hidden stack frame while preserving the rule
        // that no destination byte changes before every request is valid.
        for (ulong index = 0; index < requestCount; index++)
        {
            destination[index] = TryEncode(
                source[index],
                preconditions).Entry;
        }

        return X64PageTableEntryBatchResult.Success(
            requestCount,
            sourceCapacity,
            destinationCapacity);
    }

    private static X64PageTableEntryResult TryEncodeUnmapped(
        X64PageTableEntryRequest request)
    {
        if (request.PhysicalAddress.IsValid ||
            request.Protection != PageProtection.None ||
            request.CachePolicy != PageCachePolicy.Uninitialized)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.InvalidUnmappedEntry);
        }

        return X64PageTableEntryResult.Success(
            new X64PageTableEntry(0),
            request.Level,
            X64PageTableEntryKind.Unmapped,
            default,
            PageProtection.None,
            PageCachePolicy.Uninitialized,
            0,
            X64PatMappingToken.UnsupportedIndex,
            accessed: false,
            dirty: false);
    }

    private static X64PageTableEntryResult TryEncodeTablePointer(
        X64PageTableEntryRequest request,
        X64PageTableEncodingPreconditions preconditions)
    {
        if (request.Level == X64PageTableLevel.Pte)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.InvalidTablePointer);
        }

        if (request.Protection != PageProtection.None ||
            request.CachePolicy != PageCachePolicy.WriteBack)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.InvalidTablePointer);
        }

        X64PageTableEntryStatus cacheStatus =
            preconditions.PatMapping.TryValidateMemoryTypeAtIndex(
                0,
                PageCachePolicy.WriteBack);
        if (cacheStatus != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(cacheStatus);
        }

        // PWT/PCD on a nonleaf entry describe page-walk structure caching,
        // not a leaf PAT policy. Stage three supports WB paging structures
        // only, represented by the explicit PAT index-zero observation.
        X64PageTableEntryStatus addressStatus = TryGetPhysicalAddress(
            request.PhysicalAddress,
            PageSize4KiB,
            preconditions.PhysicalAddressWidth,
            out ulong physicalAddress);
        if (addressStatus != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(addressStatus);
        }

        // Upper-level permissions are intentionally permissive so leaf
        // permissions decide mixed executable/data subtrees. This P|RW,NX=0
        // table pointer is not a writable-executable leaf mapping.
        ulong rawValue = physicalAddress | PresentBit | WritableBit;
        return X64PageTableEntryResult.Success(
            new X64PageTableEntry(rawValue),
            request.Level,
            X64PageTableEntryKind.TablePointer,
            request.PhysicalAddress,
            PageProtection.None,
            PageCachePolicy.WriteBack,
            0,
            0,
            accessed: false,
            dirty: false);
    }

    private static X64PageTableEntryResult TryEncodeLeaf(
        X64PageTableEntryRequest request,
        X64PageTableEncodingPreconditions preconditions)
    {
        if (request.Level == X64PageTableLevel.Pml4e)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.InvalidLargePage);
        }

        if (request.Level == X64PageTableLevel.Pdpte &&
            !preconditions.OneGiBPagesSupported)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.OneGiBPagesNotSupported);
        }

        X64PageTableEntryStatus protectionStatus =
            ValidateLeafProtection(request.Protection);
        if (protectionStatus != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(protectionStatus);
        }

        X64PageTableEntryStatus cacheStatus =
            preconditions.PatMapping.TryGetIndex(
                request.CachePolicy,
                out byte patIndex);
        if (cacheStatus != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(cacheStatus);
        }

        ulong pageSize = GetLeafPageSize(request.Level);
        X64PageTableEntryStatus addressStatus = TryGetPhysicalAddress(
            request.PhysicalAddress,
            pageSize,
            preconditions.PhysicalAddressWidth,
            out ulong physicalAddress);
        if (addressStatus != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(addressStatus);
        }

        ulong rawValue = physicalAddress | PresentBit;
        if (HasProtection(
                request.Protection,
                PageProtection.Write))
        {
            rawValue |= WritableBit;
        }

        if (!HasProtection(
                request.Protection,
                PageProtection.Execute))
        {
            rawValue |= NoExecuteBit;
        }

        rawValue |= EncodePatIndex(
            patIndex,
            request.Level);

        if (request.Level == X64PageTableLevel.Pde ||
            request.Level == X64PageTableLevel.Pdpte)
        {
            rawValue |= PageSizeOrPtePatBit;
        }

        return X64PageTableEntryResult.Success(
            new X64PageTableEntry(rawValue),
            request.Level,
            X64PageTableEntryKind.Leaf,
            request.PhysicalAddress,
            request.Protection,
            request.CachePolicy,
            pageSize,
            patIndex,
            accessed: false,
            dirty: false);
    }

    private static X64PageTableEntryResult TryDecodeTablePointer(
        X64PageTableLevel level,
        ulong rawValue,
        X64PageTableEncodingPreconditions preconditions)
    {
        if (level == X64PageTableLevel.Pte)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.InvalidTablePointer);
        }

        ulong addressMask = GetPhysicalAddressMask(
            preconditions.PhysicalAddressWidth,
            PageSize4KiB);
        ulong allowedMask =
            addressMask | PresentBit | WritableBit | AccessedBit;
        if ((rawValue & DirtyBit) != 0)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.DirtyBitOnTablePointer);
        }

        if ((rawValue & WritableBit) == 0 ||
            (rawValue & NoExecuteBit) != 0 ||
            (rawValue & (PageWriteThroughBit
                | PageCacheDisableBit)) != 0)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.NonCanonicalTablePointer);
        }

        if ((rawValue & ~allowedMask) != 0)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.UnsupportedEntryBits);
        }

        X64PageTableEntryStatus cacheStatus =
            preconditions.PatMapping.TryValidateMemoryTypeAtIndex(
                0,
                PageCachePolicy.WriteBack);
        if (cacheStatus != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(cacheStatus);
        }

        ulong physicalAddress = rawValue & addressMask;
        X64PageTableEntryStatus addressStatus =
            TryCreatePhysicalAddress(
                physicalAddress,
                PageSize4KiB,
                preconditions.PhysicalAddressWidth,
                out OS.Boot.PhysicalAddress typedAddress);
        if (addressStatus != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(addressStatus);
        }

        return X64PageTableEntryResult.Success(
            new X64PageTableEntry(rawValue),
            level,
            X64PageTableEntryKind.TablePointer,
            typedAddress,
            PageProtection.None,
            PageCachePolicy.WriteBack,
            0,
            0,
            accessed: (rawValue & AccessedBit) != 0,
            dirty: false);
    }

    private static X64PageTableEntryResult TryDecodeLeaf(
        X64PageTableLevel level,
        ulong rawValue,
        bool large,
        X64PageTableEncodingPreconditions preconditions)
    {
        if (level == X64PageTableLevel.Pml4e ||
            (level == X64PageTableLevel.Pte && large))
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.InvalidLargePage);
        }

        if (level == X64PageTableLevel.Pdpte &&
            !preconditions.OneGiBPagesSupported)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.OneGiBPagesNotSupported);
        }

        ulong pageSize = GetLeafPageSize(level);
        ulong addressMask = GetPhysicalAddressMask(
            preconditions.PhysicalAddressWidth,
            pageSize);
        ulong patBit = level == X64PageTableLevel.Pte
            ? PageSizeOrPtePatBit
            : LargePagePatBit;
        ulong allowedMask =
            addressMask
            | PresentBit
            | WritableBit
            | PageWriteThroughBit
            | PageCacheDisableBit
            | AccessedBit
            | DirtyBit
            | patBit
            | NoExecuteBit;
        if (large)
        {
            allowedMask |= PageSizeOrPtePatBit;
        }

        if ((rawValue & ~allowedMask) != 0)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.UnsupportedEntryBits);
        }

        if ((rawValue & (UserBit
                | GlobalBit
                | SoftwareLowMask
                | SoftwareHighMask
                | ProtectionKeyMask)) != 0)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.UnsupportedEntryBits);
        }

        bool writable = (rawValue & WritableBit) != 0;
        bool executable = (rawValue & NoExecuteBit) == 0;
        if (writable && executable)
        {
            return X64PageTableEntryResult.Fail(
                X64PageTableEntryStatus.WritableExecutableRejected);
        }

        PageProtection protection = PageProtection.Read;
        if (writable)
        {
            protection = (PageProtection)(
                (byte)protection | (byte)PageProtection.Write);
        }

        if (executable)
        {
            protection = (PageProtection)(
                (byte)protection | (byte)PageProtection.Execute);
        }

        byte patIndex = DecodePatIndex(rawValue, patBit);
        X64PageTableEntryStatus cacheStatus =
            preconditions.PatMapping.TryGetPolicy(
                patIndex,
                out PageCachePolicy cachePolicy);
        if (cacheStatus != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(cacheStatus);
        }

        ulong physicalAddress = rawValue & addressMask;
        X64PageTableEntryStatus addressStatus =
            TryCreatePhysicalAddress(
                physicalAddress,
                pageSize,
                preconditions.PhysicalAddressWidth,
                out OS.Boot.PhysicalAddress typedAddress);
        if (addressStatus != X64PageTableEntryStatus.Success)
        {
            return X64PageTableEntryResult.Fail(addressStatus);
        }

        return X64PageTableEntryResult.Success(
            new X64PageTableEntry(rawValue),
            level,
            X64PageTableEntryKind.Leaf,
            typedAddress,
            protection,
            cachePolicy,
            pageSize,
            patIndex,
            accessed: (rawValue & AccessedBit) != 0,
            dirty: (rawValue & DirtyBit) != 0);
    }

    private static X64PageTableEntryStatus ValidatePreconditions(
        X64PageTableEncodingPreconditions preconditions)
    {
        return preconditions.Validate();
    }

    private static X64PageTableEntryStatus ValidateLeafProtection(
        PageProtection protection)
    {
        byte raw = (byte)protection;
        byte known =
            (byte)((byte)PageProtection.Read
                | (byte)PageProtection.Write
                | (byte)PageProtection.Execute);
        if ((raw & (byte)~known) != 0)
        {
            return X64PageTableEntryStatus.InvalidProtection;
        }

        bool writable =
            (raw & (byte)PageProtection.Write) != 0;
        bool executable =
            (raw & (byte)PageProtection.Execute) != 0;
        if (writable && executable)
        {
            return X64PageTableEntryStatus
                .WritableExecutableRejected;
        }

        if (protection == PageProtection.Read ||
            protection == (PageProtection)(
                (byte)PageProtection.Read
                | (byte)PageProtection.Write) ||
            protection == (PageProtection)(
                (byte)PageProtection.Read
                | (byte)PageProtection.Execute))
        {
            return X64PageTableEntryStatus.Success;
        }

        return X64PageTableEntryStatus.InvalidProtection;
    }

    private static X64PageTableEntryStatus TryGetPhysicalAddress(
        OS.Boot.PhysicalAddress address,
        ulong alignment,
        byte physicalAddressWidth,
        out ulong rawAddress)
    {
        rawAddress = 0;
        OS.Boot.AddressValueStatus addressStatus =
            address.TryGetRawValueForHardwareRegisterBoundary(
                out ulong value);
        if (addressStatus != OS.Boot.AddressValueStatus.Success)
        {
            return X64PageTableEntryStatus.InvalidPhysicalAddress;
        }

        X64PageTableEntryStatus status = ValidatePhysicalAddress(
            value,
            alignment,
            physicalAddressWidth);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        rawAddress = value;
        return X64PageTableEntryStatus.Success;
    }

    private static X64PageTableEntryStatus TryCreatePhysicalAddress(
        ulong rawAddress,
        ulong alignment,
        byte physicalAddressWidth,
        out OS.Boot.PhysicalAddress address)
    {
        address = default;
        X64PageTableEntryStatus status = ValidatePhysicalAddress(
            rawAddress,
            alignment,
            physicalAddressWidth);
        if (status != X64PageTableEntryStatus.Success)
        {
            return status;
        }

        OS.Boot.AddressValueStatus addressStatus =
            OS.Boot.PhysicalAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    rawAddress,
                    alignment,
                    out address);
        return addressStatus == OS.Boot.AddressValueStatus.Success
            ? X64PageTableEntryStatus.Success
            : X64PageTableEntryStatus.InvalidPhysicalAddress;
    }

    private static X64PageTableEntryStatus ValidatePhysicalAddress(
        ulong address,
        ulong alignment,
        byte physicalAddressWidth)
    {
        if (address == 0)
        {
            return X64PageTableEntryStatus.InvalidPhysicalAddress;
        }

        if ((address & (alignment - 1)) != 0)
        {
            return X64PageTableEntryStatus.MisalignedPhysicalAddress;
        }

        ulong physicalLimit = 1UL << physicalAddressWidth;
        return address < physicalLimit
            ? X64PageTableEntryStatus.Success
            : X64PageTableEntryStatus.PhysicalAddressOutsideWidth;
    }

    private static ulong GetPhysicalAddressMask(
        byte physicalAddressWidth,
        ulong alignment)
    {
        ulong physicalMask =
            (1UL << physicalAddressWidth) - 1;
        return physicalMask & ~(alignment - 1);
    }

    private static ulong EncodePatIndex(
        byte patIndex,
        X64PageTableLevel level)
    {
        ulong bits = 0;
        if ((patIndex & 1) != 0)
        {
            bits |= PageWriteThroughBit;
        }

        if ((patIndex & 2) != 0)
        {
            bits |= PageCacheDisableBit;
        }

        if ((patIndex & 4) != 0)
        {
            bits |= level == X64PageTableLevel.Pte
                ? PageSizeOrPtePatBit
                : LargePagePatBit;
        }

        return bits;
    }

    private static byte DecodePatIndex(
        ulong rawValue,
        ulong patBit)
    {
        byte index = 0;
        if ((rawValue & PageWriteThroughBit) != 0)
        {
            index |= 1;
        }

        if ((rawValue & PageCacheDisableBit) != 0)
        {
            index |= 2;
        }

        if ((rawValue & patBit) != 0)
        {
            index |= 4;
        }

        return index;
    }

    private static ulong GetLeafPageSize(
        X64PageTableLevel level)
    {
        if (level == X64PageTableLevel.Pte)
        {
            return PageSize4KiB;
        }

        if (level == X64PageTableLevel.Pde)
        {
            return PageSize2MiB;
        }

        return PageSize1GiB;
    }

    private static bool HasProtection(
        PageProtection value,
        PageProtection flag)
    {
        return ((byte)value & (byte)flag) != 0;
    }

    private static bool IsValidLevel(X64PageTableLevel level)
    {
        return level == X64PageTableLevel.Pte
            || level == X64PageTableLevel.Pde
            || level == X64PageTableLevel.Pdpte
            || level == X64PageTableLevel.Pml4e;
    }

    private static bool TryGetSpanEnd(
        ulong start,
        ulong capacity,
        ulong elementSize,
        out ulong end)
    {
        end = 0;
        if (capacity != 0 &&
            elementSize > ~0UL / capacity)
        {
            return false;
        }

        ulong byteLength = capacity * elementSize;
        if (start > ~0UL - byteLength)
        {
            return false;
        }

        end = start + byteLength;
        return true;
    }
}
