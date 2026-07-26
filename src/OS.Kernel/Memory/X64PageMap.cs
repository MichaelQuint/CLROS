namespace OS.Kernel.Memory;

/// <summary>
/// Identifies the single leaf mutation requested from a caller-owned x64
/// page-table image. Values are append-only.
/// </summary>
public enum X64PageMapOperation : byte
{
    Uninitialized = 0,
    Map = 1,
    Unmap = 2,
    Protect = 3
}

/// <summary>
/// Stable status contract for caller-owned x64 table-image mutation.
/// The zero/default value is intentionally not success.
/// </summary>
public enum X64PageMapStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidRequestState = 2,
    InvalidOperation = 3,
    InvalidRootTable = 4,
    InvalidVirtualAddress = 5,
    PageZeroRejected = 6,
    MisalignedVirtualAddress = 7,
    NonCanonicalVirtualAddress = 8,
    InvalidPhysicalAddress = 9,
    InvalidProtection = 10,
    InvalidCachePolicy = 11,
    InvalidHardwareStatePolicy = 12,
    InvalidPageImageCount = 13,
    TooManyPageImages = 14,
    PageImageCapacityTooSmall = 15,
    PageImageCapacityExceedsLimit = 16,
    MissingPageImages = 17,
    MisalignedPageImages = 18,
    PageImageSpanOverflow = 19,
    InvalidEntryCount = 20,
    EntryCountMismatch = 21,
    EntryCapacityTooSmall = 22,
    EntryCapacityExceedsLimit = 23,
    MissingEntries = 24,
    MisalignedEntries = 25,
    EntrySpanOverflow = 26,
    StorageOverlap = 27,
    InvalidPageImageRecord = 28,
    PageImageOrderInvalid = 29,
    PageImagePhysicalOverlap = 30,
    RootPageNotFound = 31,
    MissingTable = 32,
    LargePageCollision = 33,
    TablePointerTargetMissing = 34,
    MalformedEntry = 35,
    TargetAlreadyMapped = 36,
    TargetNotMapped = 37,
    EssentialMappingMutationRejected = 38,
    InvalidEssentialRangeCount = 39,
    TooManyEssentialRanges = 40,
    EssentialRangeCapacityTooSmall = 41,
    EssentialRangeCapacityExceedsLimit = 42,
    MissingEssentialRanges = 43,
    MisalignedEssentialRanges = 44,
    EssentialRangeSpanOverflow = 45,
    EssentialRangeStorageOverlap = 46,
    InvalidEssentialRange = 47,
    EssentialRangeOrderInvalid = 48,
    EntryEncodingFailed = 49,
    ProtectionRewriteFailed = 50,
    ProtectionUnchanged = 51,
    InvalidationPlanFailed = 52,
    ContractLayoutMismatch = 53
}

/// <summary>
/// Locates one 4 KiB table page inside a separate flat caller-owned array of
/// raw entry values. No pointer is retained in this POD.
/// </summary>
public readonly struct X64PageTableImagePage
{
    public const ulong StructureSize = 24;
    public const ushort StructureVersion = 1;
    public const ulong EntryCountPerPage = 512;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _reserved;
    private readonly OS.Boot.PhysicalAddress _physicalStart;
    private readonly ulong _entryStartIndex;

    private X64PageTableImagePage(
        OS.Boot.PhysicalAddress physicalStart,
        ulong entryStartIndex)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved = 0;
        _physicalStart = physicalStart;
        _entryStartIndex = entryStartIndex;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved == 0;

    public OS.Boot.PhysicalAddress PhysicalStart => _physicalStart;

    public ulong EntryStartIndex => _entryStartIndex;

    public static X64PageMapStatus TryCreate(
        OS.Boot.PhysicalAddress physicalStart,
        ulong entryStartIndex,
        out X64PageTableImagePage page)
    {
        page = default;
        if (!physicalStart.IsValid ||
            !physicalStart.IsAligned(X64PageMap.PageSize))
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        if ((entryStartIndex
                & (EntryCountPerPage - 1)) != 0 ||
            entryStartIndex > ~0UL - EntryCountPerPage)
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        page = new X64PageTableImagePage(
            physicalStart,
            entryStartIndex);
        return X64PageMapStatus.Success;
    }

    internal X64PageMapStatus Validate(
        ulong ordinal,
        ulong entryCount,
        byte physicalAddressWidth)
    {
        if (!IsInitialized ||
            !_physicalStart.IsValid ||
            !_physicalStart.IsAligned(X64PageMap.PageSize))
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        if (ordinal > ~0UL / EntryCountPerPage ||
            _entryStartIndex
                != ordinal * EntryCountPerPage ||
            _entryStartIndex > entryCount ||
            entryCount - _entryStartIndex
                < EntryCountPerPage)
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        if (_physicalStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawPhysical)
            != OS.Boot.AddressValueStatus.Success)
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        ulong physicalLimit = 1UL << physicalAddressWidth;
        if (rawPhysical >= physicalLimit ||
            rawPhysical > physicalLimit - X64PageMap.PageSize)
        {
            return X64PageMapStatus.InvalidPageImageRecord;
        }

        return X64PageMapStatus.Success;
    }
}

/// <summary>
/// Marks a page-aligned virtual interval whose leaf entry must not be removed
/// or rewritten by this mutation boundary.
/// </summary>
public readonly struct X64EssentialMappingRange
{
    public const ulong StructureSize = 32;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _reserved;
    private readonly OS.Boot.VirtualAddress _virtualStart;
    private readonly OS.Boot.VirtualAddress _virtualEndExclusive;
    private readonly ulong _pageCount;

    private X64EssentialMappingRange(
        OS.Boot.VirtualAddress virtualStart,
        OS.Boot.VirtualAddress virtualEndExclusive,
        ulong pageCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved = 0;
        _virtualStart = virtualStart;
        _virtualEndExclusive = virtualEndExclusive;
        _pageCount = pageCount;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved == 0;

    public OS.Boot.VirtualAddress VirtualStart => _virtualStart;

    public OS.Boot.VirtualAddress VirtualEndExclusive =>
        _virtualEndExclusive;

    public ulong PageCount => _pageCount;

    public static X64PageMapStatus TryCreate(
        OS.Boot.VirtualAddress virtualStart,
        ulong pageCount,
        out X64EssentialMappingRange range)
    {
        range = default;
        if (!virtualStart.IsValid ||
            !virtualStart.IsAligned(X64PageMap.PageSize) ||
            pageCount == 0 ||
            pageCount > X64PageMap.MaximumEssentialRangePages ||
            pageCount > ~0UL / X64PageMap.PageSize)
        {
            return X64PageMapStatus.InvalidEssentialRange;
        }

        if (virtualStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawStart)
            != OS.Boot.AddressValueStatus.Success ||
            rawStart == 0 ||
            rawStart >= X64PageMap.LowCanonicalEndExclusive)
        {
            return X64PageMapStatus.InvalidEssentialRange;
        }

        ulong byteLength = pageCount * X64PageMap.PageSize;
        if (virtualStart.TryGetEndExclusive(
                byteLength,
                out OS.Boot.VirtualAddress endExclusive)
            != OS.Boot.AddressValueStatus.Success ||
            endExclusive
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawEnd)
                != OS.Boot.AddressValueStatus.Success ||
            rawEnd > X64PageMap.LowCanonicalEndExclusive ||
            rawEnd <= rawStart)
        {
            return X64PageMapStatus.InvalidEssentialRange;
        }

        range = new X64EssentialMappingRange(
            virtualStart,
            endExclusive,
            pageCount);
        return X64PageMapStatus.Success;
    }

    internal X64PageMapStatus Validate()
    {
        if (!IsInitialized ||
            TryCreate(
                _virtualStart,
                _pageCount,
                out X64EssentialMappingRange recreated)
                != X64PageMapStatus.Success ||
            !recreated.VirtualEndExclusive.Equals(
                _virtualEndExclusive))
        {
            return X64PageMapStatus.InvalidEssentialRange;
        }

        return X64PageMapStatus.Success;
    }
}

/// <summary>
/// Immutable request for one exact 4 KiB leaf mutation.
/// </summary>
public readonly struct X64PageMapRequest
{
    public const ulong StructureSize = 64;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64PageMapOperation _operation;
    private readonly PageProtection _protection;
    private readonly PageCachePolicy _cachePolicy;
    private readonly X64HardwareStatePolicy _hardwareStatePolicy;
    private readonly byte _reserved0;
    private readonly uint _reserved1;
    private readonly OS.Boot.PhysicalAddress _rootTable;
    private readonly OS.Boot.VirtualAddress _virtualAddress;
    private readonly OS.Boot.PhysicalAddress _physicalAddress;
    private readonly X64PageTableEncodingPreconditions _preconditions;

    private X64PageMapRequest(
        X64PageMapOperation operation,
        OS.Boot.PhysicalAddress rootTable,
        OS.Boot.VirtualAddress virtualAddress,
        OS.Boot.PhysicalAddress physicalAddress,
        PageProtection protection,
        PageCachePolicy cachePolicy,
        X64HardwareStatePolicy hardwareStatePolicy,
        X64PageTableEncodingPreconditions preconditions)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _operation = operation;
        _protection = protection;
        _cachePolicy = cachePolicy;
        _hardwareStatePolicy = hardwareStatePolicy;
        _reserved0 = 0;
        _reserved1 = 0;
        _rootTable = rootTable;
        _virtualAddress = virtualAddress;
        _physicalAddress = physicalAddress;
        _preconditions = preconditions;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0;

    public X64PageMapOperation Operation => _operation;

    public OS.Boot.PhysicalAddress RootTable => _rootTable;

    public OS.Boot.VirtualAddress VirtualAddress => _virtualAddress;

    public OS.Boot.PhysicalAddress PhysicalAddress =>
        _physicalAddress;

    public PageProtection Protection => _protection;

    public PageCachePolicy CachePolicy => _cachePolicy;

    public X64HardwareStatePolicy HardwareStatePolicy =>
        _hardwareStatePolicy;

    public X64PageTableEncodingPreconditions Preconditions =>
        _preconditions;

    public static X64PageMapRequest CreateMap(
        OS.Boot.PhysicalAddress rootTable,
        OS.Boot.VirtualAddress virtualAddress,
        OS.Boot.PhysicalAddress physicalAddress,
        PageProtection protection,
        PageCachePolicy cachePolicy,
        X64PageTableEncodingPreconditions preconditions)
    {
        return new X64PageMapRequest(
            X64PageMapOperation.Map,
            rootTable,
            virtualAddress,
            physicalAddress,
            protection,
            cachePolicy,
            X64HardwareStatePolicy.Uninitialized,
            preconditions);
    }

    public static X64PageMapRequest CreateUnmap(
        OS.Boot.PhysicalAddress rootTable,
        OS.Boot.VirtualAddress virtualAddress,
        X64PageTableEncodingPreconditions preconditions)
    {
        return new X64PageMapRequest(
            X64PageMapOperation.Unmap,
            rootTable,
            virtualAddress,
            default,
            PageProtection.None,
            PageCachePolicy.Uninitialized,
            X64HardwareStatePolicy.Uninitialized,
            preconditions);
    }

    public static X64PageMapRequest CreateProtect(
        OS.Boot.PhysicalAddress rootTable,
        OS.Boot.VirtualAddress virtualAddress,
        PageProtection protection,
        X64HardwareStatePolicy hardwareStatePolicy,
        X64PageTableEncodingPreconditions preconditions)
    {
        return new X64PageMapRequest(
            X64PageMapOperation.Protect,
            rootTable,
            virtualAddress,
            default,
            protection,
            PageCachePolicy.Uninitialized,
            hardwareStatePolicy,
            preconditions);
    }

    public X64PageMapStatus Validate()
    {
        if (!IsInitialized)
        {
            return X64PageMapStatus.InvalidRequestState;
        }

        if (_operation != X64PageMapOperation.Map &&
            _operation != X64PageMapOperation.Unmap &&
            _operation != X64PageMapOperation.Protect)
        {
            return X64PageMapStatus.InvalidOperation;
        }

        if (!_rootTable.IsValid ||
            !_rootTable.IsAligned(X64PageMap.PageSize))
        {
            return X64PageMapStatus.InvalidRootTable;
        }

        if (!_virtualAddress.IsValid)
        {
            return X64PageMapStatus.PageZeroRejected;
        }

        if (!_virtualAddress.IsAligned(X64PageMap.PageSize))
        {
            return X64PageMapStatus.MisalignedVirtualAddress;
        }

        if (_virtualAddress
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawVirtual)
            != OS.Boot.AddressValueStatus.Success)
        {
            return X64PageMapStatus.InvalidVirtualAddress;
        }

        if (rawVirtual == 0)
        {
            return X64PageMapStatus.PageZeroRejected;
        }

        if (rawVirtual >= X64PageMap.LowCanonicalEndExclusive ||
            rawVirtual
                > X64PageMap.LowCanonicalEndExclusive
                    - X64PageMap.PageSize)
        {
            return X64PageMapStatus.NonCanonicalVirtualAddress;
        }

        X64PageTableEntryStatus preconditionStatus =
            _preconditions.Validate();
        if (preconditionStatus
            != X64PageTableEntryStatus.Success)
        {
            return X64PageMapStatus.InvalidRequestState;
        }

        if (_operation == X64PageMapOperation.Map)
        {
            if (!_physicalAddress.IsValid ||
                !_physicalAddress.IsAligned(X64PageMap.PageSize))
            {
                return X64PageMapStatus.InvalidPhysicalAddress;
            }

            X64PageTableEntryResult encoded =
                X64PageTableEntry.TryEncode(
                    new X64PageTableEntryRequest(
                        X64PageTableLevel.Pte,
                        X64PageTableEntryKind.Leaf,
                        _physicalAddress,
                        _protection,
                        _cachePolicy),
                    _preconditions);
            return encoded.Succeeded
                ? X64PageMapStatus.Success
                : MapEntryStatus(encoded.Status);
        }

        if (_physicalAddress.IsValid)
        {
            return X64PageMapStatus.InvalidPhysicalAddress;
        }

        if (_operation == X64PageMapOperation.Unmap)
        {
            if (_protection != PageProtection.None)
            {
                return X64PageMapStatus.InvalidProtection;
            }

            if (_cachePolicy != PageCachePolicy.Uninitialized)
            {
                return X64PageMapStatus.InvalidCachePolicy;
            }

            return _hardwareStatePolicy
                    == X64HardwareStatePolicy.Uninitialized
                ? X64PageMapStatus.Success
                : X64PageMapStatus.InvalidHardwareStatePolicy;
        }

        if (_cachePolicy != PageCachePolicy.Uninitialized)
        {
            return X64PageMapStatus.InvalidCachePolicy;
        }

        if (_hardwareStatePolicy != X64HardwareStatePolicy.Clear &&
            _hardwareStatePolicy
                != X64HardwareStatePolicy.Preserve)
        {
            return X64PageMapStatus.InvalidHardwareStatePolicy;
        }

        X64PageTableEntryResult protectionCheck =
            X64PageTableEntry.TryEncode(
                new X64PageTableEntryRequest(
                    X64PageTableLevel.Pte,
                    X64PageTableEntryKind.Leaf,
                    _rootTable,
                    _protection,
                    PageCachePolicy.WriteBack),
                _preconditions);
        return protectionCheck.Succeeded
            ? X64PageMapStatus.Success
            : MapEntryStatus(protectionCheck.Status);
    }

    private static X64PageMapStatus MapEntryStatus(
        X64PageTableEntryStatus status)
    {
        if (status
            == X64PageTableEntryStatus.InvalidProtection ||
            status
            == X64PageTableEntryStatus
                .WritableExecutableRejected)
        {
            return X64PageMapStatus.InvalidProtection;
        }

        if (status
                == X64PageTableEntryStatus.InvalidCachePolicy ||
            status
                == X64PageTableEntryStatus.CachePolicyNotSupported)
        {
            return X64PageMapStatus.InvalidCachePolicy;
        }

        if (status
                == X64PageTableEntryStatus.InvalidPhysicalAddress ||
            status
                == X64PageTableEntryStatus
                    .MisalignedPhysicalAddress ||
            status
                == X64PageTableEntryStatus
                    .PhysicalAddressOutsideWidth)
        {
            return X64PageMapStatus.InvalidPhysicalAddress;
        }

        return X64PageMapStatus.InvalidRequestState;
    }
}

/// <summary>
/// Immutable result for one successful or rejected caller-owned leaf rewrite.
/// </summary>
public readonly struct X64PageMapResult
{
    public const ulong StructureSize = 264;
    public const ushort StructureVersion = 1;
    public const ulong NoIndex = ~0UL;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64PageMapStatus _status;
    private readonly X64PageTableEntryStatus _entryStatus;
    private readonly X64TlbInvalidationStatus _invalidationStatus;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly uint _reserved2;
    private readonly X64PageMapRequest _request;
    private readonly ulong _pageImageCount;
    private readonly ulong _pageImageCapacity;
    private readonly OS.Boot.PhysicalAddress
        _pageImagePhysicalStart;
    private readonly OS.Boot.PhysicalAddress
        _pageImagePhysicalEndExclusive;
    private readonly ulong _entryCount;
    private readonly ulong _entryCapacity;
    private readonly ulong _essentialRangeCount;
    private readonly ulong _essentialRangeCapacity;
    private readonly ulong _rootPageIndex;
    private readonly ulong _targetPageIndex;
    private readonly ulong _targetEntryIndex;
    private readonly ulong _invalidPageImageIndex;
    private readonly ulong _invalidEssentialRangeIndex;
    private readonly ulong _changedEntryCount;
    private readonly OS.Boot.VirtualAddress _changedVirtualStart;
    private readonly OS.Boot.VirtualAddress _changedVirtualEndExclusive;
    private readonly X64PageTableEntry _previousEntry;
    private readonly X64PageTableEntry _replacementEntry;
    private readonly X64TlbInvalidationRange _invalidationRange;

    private X64PageMapResult(
        X64PageMapStatus status,
        X64PageTableEntryStatus entryStatus,
        X64TlbInvalidationStatus invalidationStatus,
        X64PageMapRequest request,
        ulong pageImageCount,
        ulong pageImageCapacity,
        OS.Boot.PhysicalAddress pageImagePhysicalStart,
        OS.Boot.PhysicalAddress pageImagePhysicalEndExclusive,
        ulong entryCount,
        ulong entryCapacity,
        ulong essentialRangeCount,
        ulong essentialRangeCapacity,
        ulong rootPageIndex,
        ulong targetPageIndex,
        ulong targetEntryIndex,
        ulong invalidPageImageIndex,
        ulong invalidEssentialRangeIndex,
        ulong changedEntryCount,
        OS.Boot.VirtualAddress changedVirtualStart,
        OS.Boot.VirtualAddress changedVirtualEndExclusive,
        X64PageTableEntry previousEntry,
        X64PageTableEntry replacementEntry,
        X64TlbInvalidationRange invalidationRange)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _entryStatus = entryStatus;
        _invalidationStatus = invalidationStatus;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _request = request;
        _pageImageCount = pageImageCount;
        _pageImageCapacity = pageImageCapacity;
        _pageImagePhysicalStart = pageImagePhysicalStart;
        _pageImagePhysicalEndExclusive =
            pageImagePhysicalEndExclusive;
        _entryCount = entryCount;
        _entryCapacity = entryCapacity;
        _essentialRangeCount = essentialRangeCount;
        _essentialRangeCapacity = essentialRangeCapacity;
        _rootPageIndex = rootPageIndex;
        _targetPageIndex = targetPageIndex;
        _targetEntryIndex = targetEntryIndex;
        _invalidPageImageIndex = invalidPageImageIndex;
        _invalidEssentialRangeIndex = invalidEssentialRangeIndex;
        _changedEntryCount = changedEntryCount;
        _changedVirtualStart = changedVirtualStart;
        _changedVirtualEndExclusive = changedVirtualEndExclusive;
        _previousEntry = previousEntry;
        _replacementEntry = replacementEntry;
        _invalidationRange = invalidationRange;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0;

    public X64PageMapStatus Status => _status;

    public X64PageTableEntryStatus EntryStatus => _entryStatus;

    public X64TlbInvalidationStatus InvalidationStatus =>
        _invalidationStatus;

    public X64PageMapRequest Request => _request;

    public ulong PageImageCount => _pageImageCount;

    public ulong PageImageCapacity => _pageImageCapacity;

    public OS.Boot.PhysicalAddress PageImagePhysicalStart =>
        _pageImagePhysicalStart;

    public OS.Boot.PhysicalAddress
        PageImagePhysicalEndExclusive =>
            _pageImagePhysicalEndExclusive;

    public ulong EntryCount => _entryCount;

    public ulong EntryCapacity => _entryCapacity;

    public ulong EssentialRangeCount => _essentialRangeCount;

    public ulong EssentialRangeCapacity => _essentialRangeCapacity;

    public ulong RootPageIndex => _rootPageIndex;

    public ulong TargetPageIndex => _targetPageIndex;

    public ulong TargetEntryIndex => _targetEntryIndex;

    public ulong InvalidPageImageIndex => _invalidPageImageIndex;

    public ulong InvalidEssentialRangeIndex =>
        _invalidEssentialRangeIndex;

    public ulong ChangedEntryCount => _changedEntryCount;

    public OS.Boot.VirtualAddress ChangedVirtualStart =>
        _changedVirtualStart;

    public OS.Boot.VirtualAddress ChangedVirtualEndExclusive =>
        _changedVirtualEndExclusive;

    public X64PageTableEntry PreviousEntry => _previousEntry;

    public X64PageTableEntry ReplacementEntry => _replacementEntry;

    public X64TlbInvalidationRange InvalidationRange =>
        _invalidationRange;

    public bool Succeeded
    {
        get
        {
            if (!IsInitialized ||
                _status != X64PageMapStatus.Success ||
                _entryStatus
                    != X64PageTableEntryStatus.Success ||
                _invalidationStatus
                    != X64TlbInvalidationStatus.Success ||
                _request.Validate() != X64PageMapStatus.Success ||
                _pageImageCount == 0 ||
                _pageImageCount > _pageImageCapacity ||
                _pageImageCapacity > X64PageMap.MaximumPageImages ||
                _pageImagePhysicalStart
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong rawPageImageStart)
                    != OS.Boot.AddressValueStatus.Success ||
                _pageImagePhysicalEndExclusive
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong rawPageImageEnd)
                    != OS.Boot.AddressValueStatus.Success ||
                !_pageImagePhysicalStart.IsAligned(
                    X64PageMap.PageSize) ||
                !_pageImagePhysicalEndExclusive.IsAligned(
                    X64PageMap.PageSize) ||
                rawPageImageEnd <= rawPageImageStart ||
                _pageImageCount
                    > ~0UL / X64PageMap.PageSize ||
                rawPageImageEnd - rawPageImageStart
                    < _pageImageCount * X64PageMap.PageSize ||
                _request.RootTable
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong rawRootTable)
                    != OS.Boot.AddressValueStatus.Success ||
                rawRootTable < rawPageImageStart ||
                rawRootTable
                    > rawPageImageEnd - X64PageMap.PageSize ||
                rawPageImageEnd
                    > (1UL
                        << _request.Preconditions
                            .PhysicalAddressWidth) ||
                _entryCount
                    != _pageImageCount
                        * X64PageTableImagePage
                            .EntryCountPerPage ||
                _entryCount > _entryCapacity ||
                _entryCapacity
                    > X64PageMap.MaximumEntryCount ||
                _essentialRangeCount > _essentialRangeCapacity ||
                _essentialRangeCapacity
                    > X64PageMap.MaximumEssentialRangeCount ||
                _rootPageIndex >= _pageImageCount ||
                _targetPageIndex >= _pageImageCount ||
                _targetEntryIndex >= _entryCount ||
                _invalidPageImageIndex != NoIndex ||
                _invalidEssentialRangeIndex != NoIndex ||
                _changedEntryCount != 1 ||
                _previousEntry.RawValue
                    == _replacementEntry.RawValue ||
                _invalidationRange.Validate()
                    != X64TlbInvalidationStatus.Success ||
                _invalidationRange.PageCount != 1 ||
                _invalidationRange.ByteLength
                    != X64PageMap.PageSize ||
                !_invalidationRange.VirtualStart.Equals(
                    _request.VirtualAddress) ||
                !_changedVirtualStart.Equals(
                    _request.VirtualAddress) ||
                !_changedVirtualEndExclusive.Equals(
                    _invalidationRange.VirtualEndExclusive))
            {
                return false;
            }

            if (_request.Operation == X64PageMapOperation.Map)
            {
                return _previousEntry.RawValue == 0
                    && _replacementEntry.RawValue != 0;
            }

            if (_request.Operation == X64PageMapOperation.Unmap)
            {
                return _previousEntry.RawValue != 0
                    && _replacementEntry.RawValue == 0;
            }

            return _request.Operation
                    == X64PageMapOperation.Protect
                && _previousEntry.RawValue != 0
                && _replacementEntry.RawValue != 0;
        }
    }

    internal static X64PageMapResult Success(
        X64PageMapRequest request,
        ulong pageImageCount,
        ulong pageImageCapacity,
        OS.Boot.PhysicalAddress pageImagePhysicalStart,
        OS.Boot.PhysicalAddress pageImagePhysicalEndExclusive,
        ulong entryCount,
        ulong entryCapacity,
        ulong essentialRangeCount,
        ulong essentialRangeCapacity,
        ulong rootPageIndex,
        ulong targetPageIndex,
        ulong targetEntryIndex,
        X64PageTableEntry previousEntry,
        X64PageTableEntry replacementEntry,
        X64TlbInvalidationRange invalidationRange)
    {
        return new X64PageMapResult(
            X64PageMapStatus.Success,
            X64PageTableEntryStatus.Success,
            X64TlbInvalidationStatus.Success,
            request,
            pageImageCount,
            pageImageCapacity,
            pageImagePhysicalStart,
            pageImagePhysicalEndExclusive,
            entryCount,
            entryCapacity,
            essentialRangeCount,
            essentialRangeCapacity,
            rootPageIndex,
            targetPageIndex,
            targetEntryIndex,
            NoIndex,
            NoIndex,
            1,
            request.VirtualAddress,
            invalidationRange.VirtualEndExclusive,
            previousEntry,
            replacementEntry,
            invalidationRange);
    }

    internal static X64PageMapResult Fail(
        X64PageMapStatus status,
        X64PageMapRequest request,
        ulong pageImageCount,
        ulong pageImageCapacity,
        ulong entryCount,
        ulong entryCapacity,
        ulong essentialRangeCount,
        ulong essentialRangeCapacity,
        X64PageTableEntryStatus entryStatus =
            X64PageTableEntryStatus.Uninitialized,
        X64TlbInvalidationStatus invalidationStatus =
            X64TlbInvalidationStatus.Uninitialized,
        ulong rootPageIndex = NoIndex,
        ulong targetPageIndex = NoIndex,
        ulong targetEntryIndex = NoIndex,
        ulong invalidPageImageIndex = NoIndex,
        ulong invalidEssentialRangeIndex = NoIndex)
    {
        return new X64PageMapResult(
            status,
            entryStatus,
            invalidationStatus,
            request,
            pageImageCount,
            pageImageCapacity,
            default,
            default,
            entryCount,
            entryCapacity,
            essentialRangeCount,
            essentialRangeCapacity,
            rootPageIndex,
            targetPageIndex,
            targetEntryIndex,
            invalidPageImageIndex,
            invalidEssentialRangeIndex,
            0,
            default,
            default,
            default,
            default,
            default);
    }
}

/// <summary>
/// Walks and mutates only a validated caller-owned flat image. It allocates no
/// table page, touches no live CR3 hierarchy, and performs no TLB operation.
/// </summary>
public static unsafe class X64PageMap
{
    public const ulong PageSize = 4096;
    public const ulong MaximumPageImages = 64;
    public const ulong MaximumEntryCount =
        MaximumPageImages
        * X64PageTableImagePage.EntryCountPerPage;
    public const ulong MaximumEssentialRangeCount = 128;
    public const ulong MaximumEssentialRangePages = 4096;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong LowCanonicalEndExclusive =
        0x0000_8000_0000_0000UL;
    public const bool PerformsLiveMapping = false;
    public const bool PerformsLiveInvalidation = false;
    public const bool AllocatesPageTables = false;
    public const bool SplitsLargePages = false;

    public static X64PageMapResult Apply(
        X64PageMapRequest request,
        X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong pageImageCapacity,
        ulong* entries,
        ulong entryCount,
        ulong entryCapacity,
        X64EssentialMappingRange* essentialRanges,
        ulong essentialRangeCount,
        ulong essentialRangeCapacity)
    {
        X64PageMapStatus requestStatus = request.Validate();
        if (requestStatus != X64PageMapStatus.Success)
        {
            return Fail(
                requestStatus,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        X64PageMapResult storageFailure = ValidateStorage(
            request,
            pageImages,
            pageImageCount,
            pageImageCapacity,
            entries,
            entryCount,
            entryCapacity,
            essentialRanges,
            essentialRangeCount,
            essentialRangeCapacity);
        if (storageFailure.IsInitialized)
        {
            return storageFailure;
        }

        ulong rootPageIndex = FindPageImage(
            request.RootTable,
            pageImages,
            pageImageCount);
        if (rootPageIndex == X64PageMapResult.NoIndex)
        {
            return Fail(
                X64PageMapStatus.RootPageNotFound,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (request.VirtualAddress
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawVirtual)
            != OS.Boot.AddressValueStatus.Success)
        {
            return Fail(
                X64PageMapStatus.InvalidVirtualAddress,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity,
                rootPageIndex: rootPageIndex);
        }

        ulong currentPageIndex = rootPageIndex;
        X64PageTableLevel level = X64PageTableLevel.Pml4e;
        for (uint depth = 0; depth < 3; depth++)
        {
            uint tableIndex = GetIndex(rawVirtual, level);
            ulong entryIndex =
                pageImages[currentPageIndex].EntryStartIndex
                + tableIndex;
            ulong rawEntry = entries[entryIndex];
            X64PageTableEntryResult decoded =
                X64PageTableEntry.TryDecode(
                    level,
                    rawEntry,
                    request.Preconditions);
            if (!decoded.Succeeded)
            {
                return Fail(
                    X64PageMapStatus.MalformedEntry,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity,
                    decoded.Status,
                    rootPageIndex: rootPageIndex,
                    targetPageIndex: currentPageIndex,
                    targetEntryIndex: entryIndex);
            }

            if (decoded.Kind == X64PageTableEntryKind.Unmapped)
            {
                return Fail(
                    X64PageMapStatus.MissingTable,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity,
                    rootPageIndex: rootPageIndex,
                    targetPageIndex: currentPageIndex,
                    targetEntryIndex: entryIndex);
            }

            if (decoded.Kind != X64PageTableEntryKind.TablePointer)
            {
                return Fail(
                    X64PageMapStatus.LargePageCollision,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity,
                    rootPageIndex: rootPageIndex,
                    targetPageIndex: currentPageIndex,
                    targetEntryIndex: entryIndex);
            }

            ulong childPageIndex = FindPageImage(
                decoded.PhysicalAddress,
                pageImages,
                pageImageCount);
            if (childPageIndex == X64PageMapResult.NoIndex)
            {
                return Fail(
                    X64PageMapStatus.TablePointerTargetMissing,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity,
                    rootPageIndex: rootPageIndex,
                    targetPageIndex: currentPageIndex,
                    targetEntryIndex: entryIndex);
            }

            currentPageIndex = childPageIndex;
            level = level == X64PageTableLevel.Pml4e
                ? X64PageTableLevel.Pdpte
                : level == X64PageTableLevel.Pdpte
                    ? X64PageTableLevel.Pde
                    : X64PageTableLevel.Pte;
        }

        uint pteIndex = GetIndex(
            rawVirtual,
            X64PageTableLevel.Pte);
        ulong targetEntryIndex =
            pageImages[currentPageIndex].EntryStartIndex
            + pteIndex;
        ulong previousRaw = entries[targetEntryIndex];
        X64PageTableEntryResult previous =
            X64PageTableEntry.TryDecode(
                X64PageTableLevel.Pte,
                previousRaw,
                request.Preconditions);
        if (!previous.Succeeded)
        {
            return Fail(
                X64PageMapStatus.MalformedEntry,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity,
                previous.Status,
                rootPageIndex: rootPageIndex,
                targetPageIndex: currentPageIndex,
                targetEntryIndex: targetEntryIndex);
        }

        X64PageTableEntry replacementEntry;
        ulong essentialIndex = FindContainingEssentialRange(
            rawVirtual,
            essentialRanges,
            essentialRangeCount);
        if (essentialIndex != X64PageMapResult.NoIndex)
        {
            return Fail(
                X64PageMapStatus
                    .EssentialMappingMutationRejected,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity,
                rootPageIndex: rootPageIndex,
                targetPageIndex: currentPageIndex,
                targetEntryIndex: targetEntryIndex,
                invalidEssentialRangeIndex: essentialIndex);
        }

        if (request.Operation == X64PageMapOperation.Map)
        {
            if (previous.Kind
                != X64PageTableEntryKind.Unmapped)
            {
                return Fail(
                    X64PageMapStatus.TargetAlreadyMapped,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity,
                    rootPageIndex: rootPageIndex,
                    targetPageIndex: currentPageIndex,
                    targetEntryIndex: targetEntryIndex);
            }

            X64PageTableEntryResult encoded =
                X64PageTableEntry.TryEncode(
                    new X64PageTableEntryRequest(
                        X64PageTableLevel.Pte,
                        X64PageTableEntryKind.Leaf,
                        request.PhysicalAddress,
                        request.Protection,
                        request.CachePolicy),
                    request.Preconditions);
            if (!encoded.Succeeded)
            {
                return Fail(
                    X64PageMapStatus.EntryEncodingFailed,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity,
                    encoded.Status,
                    rootPageIndex: rootPageIndex,
                    targetPageIndex: currentPageIndex,
                    targetEntryIndex: targetEntryIndex);
            }

            replacementEntry = encoded.Entry;
        }
        else
        {
            if (previous.Kind
                != X64PageTableEntryKind.Leaf)
            {
                return Fail(
                    X64PageMapStatus.TargetNotMapped,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity,
                    rootPageIndex: rootPageIndex,
                    targetPageIndex: currentPageIndex,
                    targetEntryIndex: targetEntryIndex);
            }

            if (request.Operation
                == X64PageMapOperation.Unmap)
            {
                replacementEntry = default;
            }
            else
            {
                X64PageTableEntryResult replacement =
                    X64PageTableEntry
                        .TryReencodeLeafProtection(
                            X64PageTableLevel.Pte,
                            previousRaw,
                            request.Protection,
                            request.HardwareStatePolicy,
                            request.Preconditions);
                if (!replacement.Succeeded)
                {
                    return Fail(
                        X64PageMapStatus
                            .ProtectionRewriteFailed,
                        request,
                        pageImageCount,
                        pageImageCapacity,
                        entryCount,
                        entryCapacity,
                        essentialRangeCount,
                        essentialRangeCapacity,
                        replacement.Status,
                        rootPageIndex: rootPageIndex,
                        targetPageIndex: currentPageIndex,
                        targetEntryIndex: targetEntryIndex);
                }

                replacementEntry = replacement.Entry;
                if (replacementEntry.RawValue == previousRaw)
                {
                    return Fail(
                        X64PageMapStatus.ProtectionUnchanged,
                        request,
                        pageImageCount,
                        pageImageCapacity,
                        entryCount,
                        entryCapacity,
                        essentialRangeCount,
                        essentialRangeCapacity,
                        X64PageTableEntryStatus.Success,
                        rootPageIndex: rootPageIndex,
                        targetPageIndex: currentPageIndex,
                        targetEntryIndex: targetEntryIndex);
                }
            }
        }

        X64TlbInvalidationStatus invalidationStatus =
            X64TlbInvalidationPlan.TryCreate(
                request.VirtualAddress,
                PageSize,
                out X64TlbInvalidationRange invalidationRange);
        if (invalidationStatus
            != X64TlbInvalidationStatus.Success)
        {
            return Fail(
                X64PageMapStatus.InvalidationPlanFailed,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity,
                X64PageTableEntryStatus.Success,
                invalidationStatus,
                rootPageIndex,
                currentPageIndex,
                targetEntryIndex);
        }

        OS.Boot.PhysicalAddress pageImagePhysicalStart =
            pageImages[0].PhysicalStart;
        if (pageImages[pageImageCount - 1].PhysicalStart
                .TryGetEndExclusive(
                    PageSize,
                    out OS.Boot.PhysicalAddress
                        pageImagePhysicalEndExclusive)
            != OS.Boot.AddressValueStatus.Success)
        {
            return Fail(
                X64PageMapStatus.ContractLayoutMismatch,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity,
                X64PageTableEntryStatus.Success,
                invalidationStatus,
                rootPageIndex,
                currentPageIndex,
                targetEntryIndex);
        }

        // This is the sole write. Every request, storage, table-walk,
        // essential-range, encoding, and invalidation condition has already
        // succeeded, so all failures above preserve every caller byte.
        entries[targetEntryIndex] = replacementEntry.RawValue;

        return X64PageMapResult.Success(
            request,
            pageImageCount,
            pageImageCapacity,
            pageImagePhysicalStart,
            pageImagePhysicalEndExclusive,
            entryCount,
            entryCapacity,
            essentialRangeCount,
            essentialRangeCapacity,
            rootPageIndex,
            currentPageIndex,
            targetEntryIndex,
            previous.Entry,
            replacementEntry,
            invalidationRange);
    }

    private static X64PageMapResult ValidateStorage(
        X64PageMapRequest request,
        X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong pageImageCapacity,
        ulong* entries,
        ulong entryCount,
        ulong entryCapacity,
        X64EssentialMappingRange* essentialRanges,
        ulong essentialRangeCount,
        ulong essentialRangeCapacity)
    {
        if (pageImageCount == 0)
        {
            return Fail(
                X64PageMapStatus.InvalidPageImageCount,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (pageImageCount > MaximumPageImages)
        {
            return Fail(
                X64PageMapStatus.TooManyPageImages,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (pageImageCapacity < pageImageCount)
        {
            return Fail(
                X64PageMapStatus.PageImageCapacityTooSmall,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (pageImageCapacity > MaximumPageImages)
        {
            return Fail(
                X64PageMapStatus
                    .PageImageCapacityExceedsLimit,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (pageImages == null)
        {
            return Fail(
                X64PageMapStatus.MissingPageImages,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        ulong pageImagesAddress = (ulong)pageImages;
        if ((pageImagesAddress
                & (RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(
                X64PageMapStatus.MisalignedPageImages,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (!TryGetSpanEnd(
                pageImagesAddress,
                pageImageCapacity,
                X64PageTableImagePage.StructureSize,
                out ulong pageImagesEnd))
        {
            return Fail(
                X64PageMapStatus.PageImageSpanOverflow,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (entryCount == 0)
        {
            return Fail(
                X64PageMapStatus.InvalidEntryCount,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (pageImageCount
                > ~0UL / X64PageTableImagePage
                    .EntryCountPerPage ||
            entryCount
                != pageImageCount
                    * X64PageTableImagePage
                        .EntryCountPerPage)
        {
            return Fail(
                X64PageMapStatus.EntryCountMismatch,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (entryCapacity < entryCount)
        {
            return Fail(
                X64PageMapStatus.EntryCapacityTooSmall,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (entryCapacity > MaximumEntryCount)
        {
            return Fail(
                X64PageMapStatus.EntryCapacityExceedsLimit,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (entries == null)
        {
            return Fail(
                X64PageMapStatus.MissingEntries,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        ulong entriesAddress = (ulong)entries;
        if ((entriesAddress
                & (RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(
                X64PageMapStatus.MisalignedEntries,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (!TryGetSpanEnd(
                entriesAddress,
                entryCapacity,
                8,
                out ulong entriesEnd))
        {
            return Fail(
                X64PageMapStatus.EntrySpanOverflow,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (RangesOverlap(
                pageImagesAddress,
                pageImagesEnd,
                entriesAddress,
                entriesEnd))
        {
            return Fail(
                X64PageMapStatus.StorageOverlap,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (essentialRangeCount > MaximumEssentialRangeCount)
        {
            return Fail(
                X64PageMapStatus.TooManyEssentialRanges,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (essentialRangeCapacity < essentialRangeCount)
        {
            return Fail(
                X64PageMapStatus
                    .EssentialRangeCapacityTooSmall,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        if (essentialRangeCapacity > MaximumEssentialRangeCount)
        {
            return Fail(
                X64PageMapStatus
                    .EssentialRangeCapacityExceedsLimit,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        ulong essentialAddress = 0;
        ulong essentialEnd = 0;
        if (essentialRangeCapacity != 0)
        {
            if (essentialRanges == null)
            {
                return Fail(
                    X64PageMapStatus.MissingEssentialRanges,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity);
            }

            essentialAddress = (ulong)essentialRanges;
            if ((essentialAddress
                    & (RequiredPointerAlignment - 1)) != 0)
            {
                return Fail(
                    X64PageMapStatus
                        .MisalignedEssentialRanges,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity);
            }

            if (!TryGetSpanEnd(
                    essentialAddress,
                    essentialRangeCapacity,
                    X64EssentialMappingRange.StructureSize,
                    out essentialEnd))
            {
                return Fail(
                    X64PageMapStatus
                        .EssentialRangeSpanOverflow,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity);
            }

            if (RangesOverlap(
                    essentialAddress,
                    essentialEnd,
                    pageImagesAddress,
                    pageImagesEnd) ||
                RangesOverlap(
                    essentialAddress,
                    essentialEnd,
                    entriesAddress,
                    entriesEnd))
            {
                return Fail(
                    X64PageMapStatus
                        .EssentialRangeStorageOverlap,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity);
            }
        }
        else if (essentialRangeCount != 0)
        {
            return Fail(
                X64PageMapStatus.InvalidEssentialRangeCount,
                request,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                essentialRangeCount,
                essentialRangeCapacity);
        }

        ulong previousPhysicalStart = 0;
        for (ulong index = 0; index < pageImageCount; index++)
        {
            X64PageMapStatus pageStatus =
                pageImages[index].Validate(
                    index,
                    entryCount,
                    request.Preconditions.PhysicalAddressWidth);
            if (pageStatus != X64PageMapStatus.Success)
            {
                return Fail(
                    pageStatus,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity,
                    invalidPageImageIndex: index);
            }

            pageImages[index].PhysicalStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawPhysicalStart);
            if (index != 0)
            {
                if (rawPhysicalStart
                    <= previousPhysicalStart)
                {
                    return Fail(
                        X64PageMapStatus
                            .PageImageOrderInvalid,
                        request,
                        pageImageCount,
                        pageImageCapacity,
                        entryCount,
                        entryCapacity,
                        essentialRangeCount,
                        essentialRangeCapacity,
                        invalidPageImageIndex: index);
                }

                if (previousPhysicalStart
                        > ~0UL - PageSize ||
                    rawPhysicalStart
                        < previousPhysicalStart + PageSize)
                {
                    return Fail(
                        X64PageMapStatus
                            .PageImagePhysicalOverlap,
                        request,
                        pageImageCount,
                        pageImageCapacity,
                        entryCount,
                        entryCapacity,
                        essentialRangeCount,
                        essentialRangeCapacity,
                        invalidPageImageIndex: index);
                }
            }

            previousPhysicalStart = rawPhysicalStart;
        }

        ulong previousEssentialEnd = 0;
        for (ulong index = 0;
            index < essentialRangeCount;
            index++)
        {
            X64PageMapStatus rangeStatus =
                essentialRanges[index].Validate();
            if (rangeStatus != X64PageMapStatus.Success)
            {
                return Fail(
                    rangeStatus,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity,
                    invalidEssentialRangeIndex: index);
            }

            essentialRanges[index].VirtualStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawStart);
            essentialRanges[index].VirtualEndExclusive
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawEnd);
            if (index != 0 &&
                rawStart < previousEssentialEnd)
            {
                return Fail(
                    X64PageMapStatus
                        .EssentialRangeOrderInvalid,
                    request,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    essentialRangeCount,
                    essentialRangeCapacity,
                    invalidEssentialRangeIndex: index);
            }

            previousEssentialEnd = rawEnd;
        }

        return default;
    }

    private static X64PageMapResult Fail(
        X64PageMapStatus status,
        X64PageMapRequest request,
        ulong pageImageCount,
        ulong pageImageCapacity,
        ulong entryCount,
        ulong entryCapacity,
        ulong essentialRangeCount,
        ulong essentialRangeCapacity,
        X64PageTableEntryStatus entryStatus =
            X64PageTableEntryStatus.Uninitialized,
        X64TlbInvalidationStatus invalidationStatus =
            X64TlbInvalidationStatus.Uninitialized,
        ulong rootPageIndex = X64PageMapResult.NoIndex,
        ulong targetPageIndex = X64PageMapResult.NoIndex,
        ulong targetEntryIndex = X64PageMapResult.NoIndex,
        ulong invalidPageImageIndex = X64PageMapResult.NoIndex,
        ulong invalidEssentialRangeIndex =
            X64PageMapResult.NoIndex)
    {
        return X64PageMapResult.Fail(
            status,
            request,
            pageImageCount,
            pageImageCapacity,
            entryCount,
            entryCapacity,
            essentialRangeCount,
            essentialRangeCapacity,
            entryStatus,
            invalidationStatus,
            rootPageIndex,
            targetPageIndex,
            targetEntryIndex,
            invalidPageImageIndex,
            invalidEssentialRangeIndex);
    }

    private static ulong FindPageImage(
        OS.Boot.PhysicalAddress physicalAddress,
        X64PageTableImagePage* pageImages,
        ulong pageImageCount)
    {
        for (ulong index = 0; index < pageImageCount; index++)
        {
            if (pageImages[index].PhysicalStart.Equals(
                    physicalAddress))
            {
                return index;
            }
        }

        return X64PageMapResult.NoIndex;
    }

    private static ulong FindContainingEssentialRange(
        ulong rawVirtual,
        X64EssentialMappingRange* ranges,
        ulong rangeCount)
    {
        for (ulong index = 0; index < rangeCount; index++)
        {
            ranges[index].VirtualStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawStart);
            ranges[index].VirtualEndExclusive
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawEnd);
            if (rawVirtual >= rawStart &&
                rawVirtual < rawEnd)
            {
                return index;
            }
        }

        return X64PageMapResult.NoIndex;
    }

    private static uint GetIndex(
        ulong virtualAddress,
        X64PageTableLevel level)
    {
        if (level == X64PageTableLevel.Pml4e)
        {
            return (uint)((virtualAddress >> 39) & 0x1ffUL);
        }

        if (level == X64PageTableLevel.Pdpte)
        {
            return (uint)((virtualAddress >> 30) & 0x1ffUL);
        }

        if (level == X64PageTableLevel.Pde)
        {
            return (uint)((virtualAddress >> 21) & 0x1ffUL);
        }

        return (uint)((virtualAddress >> 12) & 0x1ffUL);
    }

    private static bool RangesOverlap(
        ulong firstStart,
        ulong firstEnd,
        ulong secondStart,
        ulong secondEnd)
    {
        return firstStart < secondEnd &&
            secondStart < firstEnd;
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
