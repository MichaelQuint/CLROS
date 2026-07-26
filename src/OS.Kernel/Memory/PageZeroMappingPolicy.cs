namespace OS.Kernel.Memory;

/// <summary>
/// Identifies which page-zero policy boundary produced a result. Values are
/// append-only.
/// </summary>
public enum PageZeroMappingPolicyOperation : byte
{
    Uninitialized = 0,
    MappingIntent = 1,
    TableImageAudit = 2
}

/// <summary>
/// Stable, fail-closed status contract for page-zero mapping intent and
/// caller-owned x64 table-image audits. Values are append-only.
/// </summary>
public enum PageZeroMappingPolicyStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidIntentState = 2,
    InvalidPresentFlag = 3,
    ZeroLength = 4,
    PresentRangeCoversPageZero = 5,
    RangeWrapsThroughPageZero = 6,
    MisalignedRangeStart = 7,
    MisalignedByteLength = 8,
    NonCanonicalRange = 9,
    ContractLayoutMismatch = 10,
    InvalidRootTable = 11,
    InvalidPreconditions = 12,
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
    RootPageNotFound = 30,
    MalformedEntry = 31,
    TablePointerTargetMissing = 32,
    PageZeroPresent = 33
}

/// <summary>
/// Raw mapping-boundary input used specifically because the typed
/// VirtualAddress contract intentionally cannot represent address zero.
/// Construction records hostile inputs; validation classifies them.
/// </summary>
public readonly struct PageZeroMappingIntent
{
    public const ulong StructureSize = 24;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _present;
    private readonly ulong _rawVirtualStart;
    private readonly ulong _byteLength;

    private PageZeroMappingIntent(
        ulong rawVirtualStart,
        ulong byteLength,
        bool present)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _present = present ? (byte)1 : (byte)0;
        _rawVirtualStart = rawVirtualStart;
        _byteLength = byteLength;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _present <= 1;

    public bool IsPresentMapping => _present == 1;

    public ulong RawVirtualStart => _rawVirtualStart;

    public ulong ByteLength => _byteLength;

    internal bool IsCanonicalDefault =>
        _structureSize == 0
        && _structureVersion == 0
        && _isInitialized == 0
        && _present == 0
        && _rawVirtualStart == 0
        && _byteLength == 0;

    public static PageZeroMappingIntent CreateAtRawMappingBoundary(
        ulong rawVirtualStart,
        ulong byteLength,
        bool present)
    {
        return new PageZeroMappingIntent(
            rawVirtualStart,
            byteLength,
            present);
    }

    internal PageZeroMappingPolicyStatus Validate()
    {
        if (_structureSize != StructureSize ||
            _structureVersion != StructureVersion ||
            _isInitialized != 1)
        {
            return PageZeroMappingPolicyStatus.InvalidIntentState;
        }

        if (_present > 1)
        {
            return PageZeroMappingPolicyStatus.InvalidPresentFlag;
        }

        if (_byteLength == 0)
        {
            return PageZeroMappingPolicyStatus.ZeroLength;
        }

        if (_rawVirtualStart > ~0UL - _byteLength)
        {
            return PageZeroMappingPolicyStatus
                .RangeWrapsThroughPageZero;
        }

        if (_present == 1 &&
            _rawVirtualStart < PageZeroMappingPolicy.PageSize)
        {
            return PageZeroMappingPolicyStatus
                .PresentRangeCoversPageZero;
        }

        if ((_rawVirtualStart
                & (PageZeroMappingPolicy.PageSize - 1)) != 0)
        {
            return PageZeroMappingPolicyStatus
                .MisalignedRangeStart;
        }

        if ((_byteLength
                & (PageZeroMappingPolicy.PageSize - 1)) != 0)
        {
            return PageZeroMappingPolicyStatus
                .MisalignedByteLength;
        }

        ulong rawEndExclusive =
            _rawVirtualStart + _byteLength;
        if (_rawVirtualStart >=
                PageZeroMappingPolicy.LowCanonicalEndExclusive ||
            rawEndExclusive >
                PageZeroMappingPolicy.LowCanonicalEndExclusive)
        {
            return PageZeroMappingPolicyStatus.NonCanonicalRange;
        }

        return PageZeroMappingPolicyStatus.Success;
    }
}

/// <summary>
/// Pointer-free evidence returned by the page-zero policy. A successful
/// mapping-intent result validates only the proposed interval. Only a
/// successful table-image audit can prove absence in the supplied image.
/// </summary>
public readonly struct PageZeroMappingPolicyResult
{
    public const ulong StructureSize = 128;
    public const ushort StructureVersion = 1;
    public const ulong NoIndex = ~0UL;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly PageZeroMappingPolicyStatus _status;
    private readonly PageZeroMappingPolicyOperation _operation;
    private readonly X64PageTableEntryStatus _entryStatus;
    private readonly X64PageTableLevel _absentAtLevel;
    private readonly byte _reserved0;
    private readonly uint _reserved1;
    private readonly PageZeroMappingIntent _intent;
    private readonly OS.Boot.PhysicalAddress _rootTable;
    private readonly ulong _pageImageCount;
    private readonly ulong _pageImageCapacity;
    private readonly ulong _entryCount;
    private readonly ulong _entryCapacity;
    private readonly ulong _rootPageIndex;
    private readonly ulong _terminalPageIndex;
    private readonly ulong _terminalEntryIndex;
    private readonly ulong _inspectedEntryCount;
    private readonly ulong _terminalRawEntry;
    private readonly ulong _presentLeafPageSize;

    private PageZeroMappingPolicyResult(
        PageZeroMappingPolicyStatus status,
        PageZeroMappingPolicyOperation operation,
        X64PageTableEntryStatus entryStatus,
        X64PageTableLevel absentAtLevel,
        PageZeroMappingIntent intent,
        OS.Boot.PhysicalAddress rootTable,
        ulong pageImageCount,
        ulong pageImageCapacity,
        ulong entryCount,
        ulong entryCapacity,
        ulong rootPageIndex,
        ulong terminalPageIndex,
        ulong terminalEntryIndex,
        ulong inspectedEntryCount,
        ulong terminalRawEntry,
        ulong presentLeafPageSize)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _operation = operation;
        _entryStatus = entryStatus;
        _absentAtLevel = absentAtLevel;
        _reserved0 = 0;
        _reserved1 = 0;
        _intent = intent;
        _rootTable = rootTable;
        _pageImageCount = pageImageCount;
        _pageImageCapacity = pageImageCapacity;
        _entryCount = entryCount;
        _entryCapacity = entryCapacity;
        _rootPageIndex = rootPageIndex;
        _terminalPageIndex = terminalPageIndex;
        _terminalEntryIndex = terminalEntryIndex;
        _inspectedEntryCount = inspectedEntryCount;
        _terminalRawEntry = terminalRawEntry;
        _presentLeafPageSize = presentLeafPageSize;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0;

    public PageZeroMappingPolicyStatus Status => _status;

    public PageZeroMappingPolicyOperation Operation => _operation;

    public X64PageTableEntryStatus EntryStatus => _entryStatus;

    public X64PageTableLevel AbsentAtLevel => _absentAtLevel;

    public PageZeroMappingIntent Intent => _intent;

    public OS.Boot.PhysicalAddress RootTable => _rootTable;

    public ulong PageImageCount => _pageImageCount;

    public ulong PageImageCapacity => _pageImageCapacity;

    public ulong EntryCount => _entryCount;

    public ulong EntryCapacity => _entryCapacity;

    public ulong RootPageIndex => _rootPageIndex;

    public ulong TerminalPageIndex => _terminalPageIndex;

    public ulong TerminalEntryIndex => _terminalEntryIndex;

    public ulong InspectedEntryCount => _inspectedEntryCount;

    public ulong TerminalRawEntry => _terminalRawEntry;

    public ulong PresentLeafPageSize => _presentLeafPageSize;

    public bool Succeeded =>
        Validate() == PageZeroMappingPolicyStatus.Success;

    public bool ProvesPageZeroAbsentInSuppliedImage =>
        ValidateAbsenceEvidence() ==
            PageZeroMappingPolicyStatus.Success;

    public PageZeroMappingPolicyStatus Validate()
    {
        if (!IsInitialized)
        {
            return PageZeroMappingPolicyStatus
                .ContractLayoutMismatch;
        }

        if (_status != PageZeroMappingPolicyStatus.Success)
        {
            return _status;
        }

        if (_operation ==
            PageZeroMappingPolicyOperation.MappingIntent)
        {
            PageZeroMappingPolicyStatus intentStatus =
                _intent.Validate();
            if (intentStatus !=
                PageZeroMappingPolicyStatus.Success)
            {
                return intentStatus;
            }

            return _entryStatus ==
                        X64PageTableEntryStatus.Uninitialized
                    && _absentAtLevel ==
                        X64PageTableLevel.Uninitialized
                    && _reserved0 == 0
                    && _reserved1 == 0
                    && _rootTable.Equals(default)
                    && _pageImageCount == 0
                    && _pageImageCapacity == 0
                    && _entryCount == 0
                    && _entryCapacity == 0
                    && _rootPageIndex == NoIndex
                    && _terminalPageIndex == NoIndex
                    && _terminalEntryIndex == NoIndex
                    && _inspectedEntryCount == 0
                    && _terminalRawEntry == 0
                    && _presentLeafPageSize == 0
                ? PageZeroMappingPolicyStatus.Success
                : PageZeroMappingPolicyStatus
                    .ContractLayoutMismatch;
        }

        if (_operation ==
            PageZeroMappingPolicyOperation.TableImageAudit)
        {
            return ValidateAbsenceEvidence();
        }

        return PageZeroMappingPolicyStatus.ContractLayoutMismatch;
    }

    public PageZeroMappingPolicyStatus ValidateAbsenceEvidence()
    {
        if (!IsInitialized ||
            _status != PageZeroMappingPolicyStatus.Success ||
            _operation !=
                PageZeroMappingPolicyOperation.TableImageAudit ||
            _entryStatus != X64PageTableEntryStatus.Success ||
            _absentAtLevel < X64PageTableLevel.Pte ||
            _absentAtLevel > X64PageTableLevel.Pml4e ||
            !_intent.IsCanonicalDefault ||
            !_rootTable.IsValid ||
            !_rootTable.IsAligned(PageZeroMappingPolicy.PageSize) ||
            _pageImageCount == 0 ||
            _pageImageCount >
                PageZeroMappingPolicy.MaximumPageImages ||
            _pageImageCapacity < _pageImageCount ||
            _pageImageCapacity >
                PageZeroMappingPolicy.MaximumPageImages ||
            _entryCount !=
                _pageImageCount *
                    X64PageTableImagePage.EntryCountPerPage ||
            _entryCapacity < _entryCount ||
            _entryCapacity >
                PageZeroMappingPolicy.MaximumEntryCount ||
            _rootPageIndex >= _pageImageCount ||
            _terminalPageIndex >= _pageImageCount ||
            _terminalPageIndex >
                ~0UL /
                    X64PageTableImagePage.EntryCountPerPage ||
            _terminalEntryIndex !=
                _terminalPageIndex *
                    X64PageTableImagePage.EntryCountPerPage ||
            _terminalEntryIndex >= _entryCount ||
            _inspectedEntryCount !=
                5UL - (byte)_absentAtLevel ||
            (_absentAtLevel == X64PageTableLevel.Pml4e &&
                _terminalPageIndex != _rootPageIndex) ||
            _terminalRawEntry != 0 ||
            _presentLeafPageSize != 0)
        {
            return PageZeroMappingPolicyStatus
                .ContractLayoutMismatch;
        }

        return PageZeroMappingPolicyStatus.Success;
    }

    internal static PageZeroMappingPolicyResult IntentResult(
        PageZeroMappingPolicyStatus status,
        PageZeroMappingIntent intent)
    {
        return new PageZeroMappingPolicyResult(
            status,
            PageZeroMappingPolicyOperation.MappingIntent,
            X64PageTableEntryStatus.Uninitialized,
            X64PageTableLevel.Uninitialized,
            intent,
            default,
            0,
            0,
            0,
            0,
            NoIndex,
            NoIndex,
            NoIndex,
            0,
            0,
            0);
    }

    internal static PageZeroMappingPolicyResult AuditResult(
        PageZeroMappingPolicyStatus status,
        X64PageTableEntryStatus entryStatus,
        X64PageTableLevel absentAtLevel,
        OS.Boot.PhysicalAddress rootTable,
        ulong pageImageCount,
        ulong pageImageCapacity,
        ulong entryCount,
        ulong entryCapacity,
        ulong rootPageIndex,
        ulong terminalPageIndex,
        ulong terminalEntryIndex,
        ulong inspectedEntryCount,
        ulong terminalRawEntry,
        ulong presentLeafPageSize)
    {
        return new PageZeroMappingPolicyResult(
            status,
            PageZeroMappingPolicyOperation.TableImageAudit,
            entryStatus,
            absentAtLevel,
            default,
            rootTable,
            pageImageCount,
            pageImageCapacity,
            entryCount,
            entryCapacity,
            rootPageIndex,
            terminalPageIndex,
            terminalEntryIndex,
            inspectedEntryCount,
            terminalRawEntry,
            presentLeafPageSize);
    }
}

/// <summary>
/// Validates raw mapping intents and audits page zero in a bounded,
/// caller-owned four-level x64 table image. It never reads CR3, touches live
/// page tables, mutates caller storage, or performs TLB invalidation.
/// </summary>
public static unsafe class PageZeroMappingPolicy
{
    public const ulong PageSize = 4096;
    public const ulong MaximumPageImages = 64;
    public const ulong MaximumEntryCount =
        MaximumPageImages
            * X64PageTableImagePage.EntryCountPerPage;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong LowCanonicalEndExclusive =
        0x0000_8000_0000_0000UL;
    public const bool PerformsLiveMutation = false;
    public const bool WritesTableImage = false;
    public const bool ReadsLivePageTables = false;
    public const bool ProvesInstalledRoot = false;

    public static PageZeroMappingPolicyResult ValidateMappingIntent(
        PageZeroMappingIntent intent)
    {
        if ((ulong)sizeof(PageZeroMappingIntent) !=
                PageZeroMappingIntent.StructureSize ||
            (ulong)sizeof(PageZeroMappingPolicyResult) !=
                PageZeroMappingPolicyResult.StructureSize)
        {
            return PageZeroMappingPolicyResult.IntentResult(
                PageZeroMappingPolicyStatus.ContractLayoutMismatch,
                intent);
        }

        return PageZeroMappingPolicyResult.IntentResult(
            intent.Validate(),
            intent);
    }

    public static PageZeroMappingPolicyResult AuditTableImage(
        OS.Boot.PhysicalAddress rootTable,
        X64PageTableEncodingPreconditions preconditions,
        X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong pageImageCapacity,
        ulong* entries,
        ulong entryCount,
        ulong entryCapacity)
    {
        PageZeroMappingPolicyStatus status =
            ValidateAuditStorage(
                rootTable,
                preconditions,
                pageImages,
                pageImageCount,
                pageImageCapacity,
                entries,
                entryCount,
                entryCapacity,
                out ulong rootPageIndex);
        if (status != PageZeroMappingPolicyStatus.Success)
        {
            return AuditFailure(
                status,
                rootTable,
                pageImageCount,
                pageImageCapacity,
                entryCount,
                entryCapacity,
                rootPageIndex);
        }

        ulong currentPageIndex = rootPageIndex;
        ulong inspectedEntryCount = 0;
        X64PageTableLevel level = X64PageTableLevel.Pml4e;
        while (level >= X64PageTableLevel.Pte)
        {
            ulong terminalEntryIndex =
                pageImages[currentPageIndex].EntryStartIndex;
            ulong rawEntry = entries[terminalEntryIndex];
            inspectedEntryCount++;

            if (rawEntry == 0)
            {
                return PageZeroMappingPolicyResult.AuditResult(
                    PageZeroMappingPolicyStatus.Success,
                    X64PageTableEntryStatus.Success,
                    level,
                    rootTable,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    rootPageIndex,
                    currentPageIndex,
                    terminalEntryIndex,
                    inspectedEntryCount,
                    rawEntry,
                    0);
            }

            if ((rawEntry & 1UL) == 0)
            {
                X64PageTableEntryResult malformed =
                    X64PageTableEntry.TryDecode(
                        level,
                        rawEntry,
                        preconditions);
                return PageZeroMappingPolicyResult.AuditResult(
                    PageZeroMappingPolicyStatus.MalformedEntry,
                    malformed.Status,
                    X64PageTableLevel.Uninitialized,
                    rootTable,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    rootPageIndex,
                    currentPageIndex,
                    terminalEntryIndex,
                    inspectedEntryCount,
                    rawEntry,
                    0);
            }

            bool presentLeaf =
                level == X64PageTableLevel.Pte ||
                ((level == X64PageTableLevel.Pde ||
                    level == X64PageTableLevel.Pdpte) &&
                    (rawEntry & (1UL << 7)) != 0);
            if (presentLeaf)
            {
                ulong leafPageSize =
                    level == X64PageTableLevel.Pte
                        ? X64PageTableEntry.PageSize4KiB
                        : level == X64PageTableLevel.Pde
                            ? X64PageTableEntry.PageSize2MiB
                            : X64PageTableEntry.PageSize1GiB;
                return PageZeroMappingPolicyResult.AuditResult(
                    PageZeroMappingPolicyStatus.PageZeroPresent,
                    X64PageTableEntryStatus.Success,
                    X64PageTableLevel.Uninitialized,
                    rootTable,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    rootPageIndex,
                    currentPageIndex,
                    terminalEntryIndex,
                    inspectedEntryCount,
                    rawEntry,
                    leafPageSize);
            }

            X64PageTableEntryResult decoded =
                X64PageTableEntry.TryDecode(
                    level,
                    rawEntry,
                    preconditions);
            if (!decoded.Succeeded ||
                decoded.Kind !=
                    X64PageTableEntryKind.TablePointer ||
                decoded.PhysicalAddress
                    .TryGetRawValueForHardwareRegisterBoundary(
                        out ulong childPhysical)
                    != OS.Boot.AddressValueStatus.Success)
            {
                return PageZeroMappingPolicyResult.AuditResult(
                    PageZeroMappingPolicyStatus.MalformedEntry,
                    decoded.Status,
                    X64PageTableLevel.Uninitialized,
                    rootTable,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    rootPageIndex,
                    currentPageIndex,
                    terminalEntryIndex,
                    inspectedEntryCount,
                    rawEntry,
                    0);
            }

            ulong childPageIndex = FindPageImage(
                pageImages,
                pageImageCount,
                childPhysical);
            if (childPageIndex ==
                PageZeroMappingPolicyResult.NoIndex)
            {
                return PageZeroMappingPolicyResult.AuditResult(
                    PageZeroMappingPolicyStatus
                        .TablePointerTargetMissing,
                    X64PageTableEntryStatus.Success,
                    X64PageTableLevel.Uninitialized,
                    rootTable,
                    pageImageCount,
                    pageImageCapacity,
                    entryCount,
                    entryCapacity,
                    rootPageIndex,
                    currentPageIndex,
                    terminalEntryIndex,
                    inspectedEntryCount,
                    rawEntry,
                    0);
            }

            currentPageIndex = childPageIndex;
            level = (X64PageTableLevel)((byte)level - 1);
        }

        return AuditFailure(
            PageZeroMappingPolicyStatus.MalformedEntry,
            rootTable,
            pageImageCount,
            pageImageCapacity,
            entryCount,
            entryCapacity,
            rootPageIndex);
    }

    private static PageZeroMappingPolicyStatus ValidateAuditStorage(
        OS.Boot.PhysicalAddress rootTable,
        X64PageTableEncodingPreconditions preconditions,
        X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong pageImageCapacity,
        ulong* entries,
        ulong entryCount,
        ulong entryCapacity,
        out ulong rootPageIndex)
    {
        rootPageIndex = PageZeroMappingPolicyResult.NoIndex;

        if ((ulong)sizeof(PageZeroMappingIntent) !=
                PageZeroMappingIntent.StructureSize ||
            (ulong)sizeof(PageZeroMappingPolicyResult) !=
                PageZeroMappingPolicyResult.StructureSize ||
            (ulong)sizeof(X64PageTableImagePage) !=
                X64PageTableImagePage.StructureSize)
        {
            return PageZeroMappingPolicyStatus.ContractLayoutMismatch;
        }

        if (!rootTable.IsValid ||
            !rootTable.IsAligned(PageSize))
        {
            return PageZeroMappingPolicyStatus.InvalidRootTable;
        }

        if (preconditions.Validate() !=
            X64PageTableEntryStatus.Success)
        {
            return PageZeroMappingPolicyStatus.InvalidPreconditions;
        }

        if (pageImageCount == 0)
        {
            return PageZeroMappingPolicyStatus.InvalidPageImageCount;
        }

        if (pageImageCount > MaximumPageImages)
        {
            return PageZeroMappingPolicyStatus.TooManyPageImages;
        }

        if (pageImageCapacity < pageImageCount)
        {
            return PageZeroMappingPolicyStatus
                .PageImageCapacityTooSmall;
        }

        if (pageImageCapacity > MaximumPageImages)
        {
            return PageZeroMappingPolicyStatus
                .PageImageCapacityExceedsLimit;
        }

        if (pageImages == null)
        {
            return PageZeroMappingPolicyStatus.MissingPageImages;
        }

        ulong pageImagesAddress = (ulong)pageImages;
        if ((pageImagesAddress
                & (RequiredPointerAlignment - 1)) != 0)
        {
            return PageZeroMappingPolicyStatus.MisalignedPageImages;
        }

        if (!TryGetSpanEnd(
                pageImagesAddress,
                pageImageCapacity,
                X64PageTableImagePage.StructureSize,
                out ulong pageImagesEnd))
        {
            return PageZeroMappingPolicyStatus.PageImageSpanOverflow;
        }

        if (entryCount == 0)
        {
            return PageZeroMappingPolicyStatus.InvalidEntryCount;
        }

        if (pageImageCount >
                ~0UL /
                    X64PageTableImagePage.EntryCountPerPage ||
            entryCount !=
                pageImageCount *
                    X64PageTableImagePage.EntryCountPerPage)
        {
            return PageZeroMappingPolicyStatus.EntryCountMismatch;
        }

        if (entryCapacity < entryCount)
        {
            return PageZeroMappingPolicyStatus
                .EntryCapacityTooSmall;
        }

        if (entryCapacity > MaximumEntryCount)
        {
            return PageZeroMappingPolicyStatus
                .EntryCapacityExceedsLimit;
        }

        if (entries == null)
        {
            return PageZeroMappingPolicyStatus.MissingEntries;
        }

        ulong entriesAddress = (ulong)entries;
        if ((entriesAddress
                & (RequiredPointerAlignment - 1)) != 0)
        {
            return PageZeroMappingPolicyStatus.MisalignedEntries;
        }

        if (!TryGetSpanEnd(
                entriesAddress,
                entryCapacity,
                8,
                out ulong entriesEnd))
        {
            return PageZeroMappingPolicyStatus.EntrySpanOverflow;
        }

        if (RangesOverlap(
                pageImagesAddress,
                pageImagesEnd,
                entriesAddress,
                entriesEnd))
        {
            return PageZeroMappingPolicyStatus.StorageOverlap;
        }

        ulong previousPhysicalStart = 0;
        for (ulong index = 0; index < pageImageCount; index++)
        {
            X64PageMapStatus pageStatus =
                pageImages[index].Validate(
                    index,
                    entryCount,
                    preconditions.PhysicalAddressWidth);
            if (pageStatus != X64PageMapStatus.Success)
            {
                return PageZeroMappingPolicyStatus
                    .InvalidPageImageRecord;
            }

            pageImages[index].PhysicalStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawPhysicalStart);
            if (index != 0)
            {
                if (rawPhysicalStart <= previousPhysicalStart)
                {
                    return PageZeroMappingPolicyStatus
                        .PageImageOrderInvalid;
                }

            }

            previousPhysicalStart = rawPhysicalStart;
            if (pageImages[index].PhysicalStart.Equals(rootTable))
            {
                rootPageIndex = index;
            }
        }

        if (rootPageIndex ==
            PageZeroMappingPolicyResult.NoIndex)
        {
            return PageZeroMappingPolicyStatus.RootPageNotFound;
        }

        return PageZeroMappingPolicyStatus.Success;
    }

    private static PageZeroMappingPolicyResult AuditFailure(
        PageZeroMappingPolicyStatus status,
        OS.Boot.PhysicalAddress rootTable,
        ulong pageImageCount,
        ulong pageImageCapacity,
        ulong entryCount,
        ulong entryCapacity,
        ulong rootPageIndex)
    {
        return PageZeroMappingPolicyResult.AuditResult(
            status,
            X64PageTableEntryStatus.Uninitialized,
            X64PageTableLevel.Uninitialized,
            rootTable,
            pageImageCount,
            pageImageCapacity,
            entryCount,
            entryCapacity,
            rootPageIndex,
            PageZeroMappingPolicyResult.NoIndex,
            PageZeroMappingPolicyResult.NoIndex,
            0,
            0,
            0);
    }

    private static ulong FindPageImage(
        X64PageTableImagePage* pageImages,
        ulong pageImageCount,
        ulong rawPhysicalStart)
    {
        for (ulong index = 0; index < pageImageCount; index++)
        {
            pageImages[index].PhysicalStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong candidate);
            if (candidate == rawPhysicalStart)
            {
                return index;
            }
        }

        return PageZeroMappingPolicyResult.NoIndex;
    }

    private static bool TryGetSpanEnd(
        ulong start,
        ulong count,
        ulong elementSize,
        out ulong end)
    {
        end = 0;
        if (count > ~0UL / elementSize)
        {
            return false;
        }

        ulong byteLength = count * elementSize;
        if (start > ~0UL - byteLength)
        {
            return false;
        }

        end = start + byteLength;
        return true;
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
}
