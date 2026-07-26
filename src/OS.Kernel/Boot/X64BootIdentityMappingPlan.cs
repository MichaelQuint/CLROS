namespace OS.Kernel.Boot;

/// <summary>
/// Identifies why one physical range must remain identity mapped during the
/// owned-page-table transition. Values are append-only.
/// </summary>
public enum X64BootIdentityMappingPurpose : byte
{
    Uninitialized = 0,
    ExecutingImage = 1,
    OwnedStack = 2,
    BootHandoff = 3,
    SerialStub = 4,
    AcpiScratch = 5,
    FramebufferMetadata = 6,
    BootStackGuard = 7,
    AdditionalRequired = 8
}

/// <summary>
/// Stable status contract for the bounded identity-mapping planner. Values
/// are append-only and the zero-valued default is not success.
/// </summary>
public enum X64BootIdentityMappingStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidSourceCount = 2,
    TooManySources = 3,
    SourceCapacityTooSmall = 4,
    CapacityExceedsLimit = 5,
    MissingSource = 6,
    MissingDestination = 7,
    MisalignedSource = 8,
    MisalignedDestination = 9,
    SourceSpanOverflow = 10,
    DestinationSpanOverflow = 11,
    SourceDestinationOverlap = 12,
    ContractLayoutMismatch = 13,
    InvalidRequirementState = 14,
    InvalidPurpose = 15,
    InvalidPhysicalStart = 16,
    InvalidPhysicalEnd = 17,
    EmptyOrReversedRange = 18,
    MisalignedRange = 19,
    GeometryOverflow = 20,
    GeometryMismatch = 21,
    InvalidProtection = 22,
    WritableExecutableRejected = 23,
    InvalidCachePolicy = 24,
    InvalidGuardPolicy = 25,
    MultipleGuards = 26,
    MissingGuard = 27,
    GuardOverlap = 28,
    MissingRequiredPurpose = 29,
    PageZeroMappingRejected = 30,
    NonCanonicalIdentityRange = 31,
    PhysicalAddressOutsideWidth = 32,
    IncompatibleProtectionOverlap = 33,
    IncompatibleCacheOverlap = 34,
    DestinationCapacityTooSmall = 35,
    TooManyOutputRanges = 36,
    UnionPageCountOverflow = 37,
    UnionByteCountOverflow = 38,
    TablePageCountOverflow = 39,
    TableByteCountOverflow = 40,
    TableCapacityTooSmall = 41,
    InvalidPreconditions = 42,
    PageWalkCacheNotWriteBack = 43,
    LeafEncodingRejected = 44,
    OutputConstructionFailure = 45,
    AccountingMismatch = 46
}

/// <summary>
/// Caller-owned, value-only description of one required or guarded physical
/// interval. Both ends and both geometry values are carried so the boundary
/// can reject disagreement instead of silently normalizing it.
/// </summary>
public readonly struct X64BootIdentityMappingRequirement
{
    public const ulong StructureSize = 40;

    private readonly OS.Boot.PhysicalAddress _physicalStart;
    private readonly OS.Boot.PhysicalAddress _physicalEndExclusive;
    private readonly ulong _pageCount;
    private readonly ulong _byteLength;
    private readonly byte _isInitialized;
    private readonly X64BootIdentityMappingPurpose _purpose;
    private readonly OS.Kernel.Memory.PageProtection _protection;
    private readonly OS.Kernel.Memory.PageCachePolicy _cachePolicy;
    private readonly uint _reserved;

    public X64BootIdentityMappingRequirement(
        X64BootIdentityMappingPurpose purpose,
        OS.Boot.PhysicalAddress physicalStart,
        OS.Boot.PhysicalAddress physicalEndExclusive,
        ulong pageCount,
        ulong byteLength,
        OS.Kernel.Memory.PageProtection protection,
        OS.Kernel.Memory.PageCachePolicy cachePolicy)
    {
        _physicalStart = physicalStart;
        _physicalEndExclusive = physicalEndExclusive;
        _pageCount = pageCount;
        _byteLength = byteLength;
        _isInitialized = 1;
        _purpose = purpose;
        _protection = protection;
        _cachePolicy = cachePolicy;
        _reserved = 0;
    }

    public bool IsInitialized =>
        _isInitialized == 1 && _reserved == 0;

    public X64BootIdentityMappingPurpose Purpose => _purpose;

    public OS.Boot.PhysicalAddress PhysicalStart => _physicalStart;

    public OS.Boot.PhysicalAddress PhysicalEndExclusive =>
        _physicalEndExclusive;

    public ulong PageCount => _pageCount;

    public ulong ByteLength => _byteLength;

    public OS.Kernel.Memory.PageProtection Protection => _protection;

    public OS.Kernel.Memory.PageCachePolicy CachePolicy => _cachePolicy;

    internal bool HasCanonicalState =>
        IsInitialized;
}

/// <summary>
/// One normalized 4 KiB identity-mapping interval. This is a plan record only;
/// constructing it does not allocate tables or change a live mapping.
/// </summary>
public readonly struct X64BootIdentityMappingRange
{
    public const ulong StructureSize = 64;

    private readonly OS.Boot.PhysicalAddress _physicalStart;
    private readonly OS.Boot.PhysicalAddress _physicalEndExclusive;
    private readonly OS.Boot.VirtualAddress _virtualStart;
    private readonly OS.Boot.VirtualAddress _virtualEndExclusive;
    private readonly ulong _pageCount;
    private readonly ulong _byteLength;
    private readonly ulong _contributingPurposeMask;
    private readonly byte _isInitialized;
    private readonly OS.Kernel.Memory.PageProtection _protection;
    private readonly OS.Kernel.Memory.PageCachePolicy _cachePolicy;
    private readonly byte _reserved0;
    private readonly uint _reserved1;

    internal X64BootIdentityMappingRange(
        OS.Boot.PhysicalAddress physicalStart,
        OS.Boot.PhysicalAddress physicalEndExclusive,
        OS.Boot.VirtualAddress virtualStart,
        OS.Boot.VirtualAddress virtualEndExclusive,
        ulong pageCount,
        ulong byteLength,
        ulong contributingPurposeMask,
        OS.Kernel.Memory.PageProtection protection,
        OS.Kernel.Memory.PageCachePolicy cachePolicy)
    {
        _physicalStart = physicalStart;
        _physicalEndExclusive = physicalEndExclusive;
        _virtualStart = virtualStart;
        _virtualEndExclusive = virtualEndExclusive;
        _pageCount = pageCount;
        _byteLength = byteLength;
        _contributingPurposeMask = contributingPurposeMask;
        _isInitialized = 1;
        _protection = protection;
        _cachePolicy = cachePolicy;
        _reserved0 = 0;
        _reserved1 = 0;
    }

    public bool IsInitialized =>
        _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0;

    public OS.Boot.PhysicalAddress PhysicalStart => _physicalStart;

    public OS.Boot.PhysicalAddress PhysicalEndExclusive =>
        _physicalEndExclusive;

    public OS.Boot.VirtualAddress VirtualStart => _virtualStart;

    public OS.Boot.VirtualAddress VirtualEndExclusive =>
        _virtualEndExclusive;

    public ulong PageCount => _pageCount;

    public ulong ByteLength => _byteLength;

    public ulong ContributingPurposeMask => _contributingPurposeMask;

    public OS.Kernel.Memory.PageProtection Protection => _protection;

    public OS.Kernel.Memory.PageCachePolicy CachePolicy => _cachePolicy;

    internal bool HasCanonicalState =>
        IsInitialized;
}

/// <summary>
/// Complete value-only outcome of one bounded planning attempt.
/// </summary>
public readonly struct X64BootIdentityMappingPlanResult
{
    public const ulong StructureSize = 176;
    public const ulong NoIndex = ~0UL;

    private readonly byte _isInitialized;
    private readonly X64BootIdentityMappingStatus _status;
    private readonly OS.Boot.AddressValueStatus _addressStatus;
    private readonly OS.Kernel.Memory.X64PageTableEntryStatus
        _pageTableEntryStatus;
    private readonly uint _reserved;
    private readonly ulong _sourceCount;
    private readonly ulong _validatedSourceCount;
    private readonly ulong _sourceCapacity;
    private readonly ulong _destinationCapacity;
    private readonly ulong _requiredDestinationCapacity;
    private readonly ulong _outputRangeCount;
    private readonly ulong _availableTablePageCapacity;
    private readonly ulong _requiredPurposeMask;
    private readonly ulong _observedPurposeMask;
    private readonly ulong _missingPurposeMask;
    private readonly ulong _invalidSourceIndex;
    private readonly ulong _conflictingSourceIndex;
    private readonly ulong _guardSourceIndex;
    private readonly ulong _unionPageCount;
    private readonly ulong _unionByteLength;
    private readonly ulong _pml4PageCount;
    private readonly ulong _pdptPageCount;
    private readonly ulong _pdPageCount;
    private readonly ulong _ptPageCount;
    private readonly ulong _totalTablePageCount;
    private readonly ulong _totalTableByteLength;

    internal X64BootIdentityMappingPlanResult(
        X64BootIdentityMappingStatus status,
        OS.Boot.AddressValueStatus addressStatus,
        OS.Kernel.Memory.X64PageTableEntryStatus pageTableEntryStatus,
        ulong sourceCount,
        ulong validatedSourceCount,
        ulong sourceCapacity,
        ulong destinationCapacity,
        ulong requiredDestinationCapacity,
        ulong outputRangeCount,
        ulong availableTablePageCapacity,
        ulong requiredPurposeMask,
        ulong observedPurposeMask,
        ulong missingPurposeMask,
        ulong invalidSourceIndex,
        ulong conflictingSourceIndex,
        ulong guardSourceIndex,
        ulong unionPageCount,
        ulong unionByteLength,
        ulong pml4PageCount,
        ulong pdptPageCount,
        ulong pdPageCount,
        ulong ptPageCount,
        ulong totalTablePageCount,
        ulong totalTableByteLength)
    {
        _isInitialized = 1;
        _status = status;
        _addressStatus = addressStatus;
        _pageTableEntryStatus = pageTableEntryStatus;
        _reserved = 0;
        _sourceCount = sourceCount;
        _validatedSourceCount = validatedSourceCount;
        _sourceCapacity = sourceCapacity;
        _destinationCapacity = destinationCapacity;
        _requiredDestinationCapacity = requiredDestinationCapacity;
        _outputRangeCount = outputRangeCount;
        _availableTablePageCapacity = availableTablePageCapacity;
        _requiredPurposeMask = requiredPurposeMask;
        _observedPurposeMask = observedPurposeMask;
        _missingPurposeMask = missingPurposeMask;
        _invalidSourceIndex = invalidSourceIndex;
        _conflictingSourceIndex = conflictingSourceIndex;
        _guardSourceIndex = guardSourceIndex;
        _unionPageCount = unionPageCount;
        _unionByteLength = unionByteLength;
        _pml4PageCount = pml4PageCount;
        _pdptPageCount = pdptPageCount;
        _pdPageCount = pdPageCount;
        _ptPageCount = ptPageCount;
        _totalTablePageCount = totalTablePageCount;
        _totalTableByteLength = totalTableByteLength;
    }

    public bool IsInitialized =>
        _isInitialized == 1 && _reserved == 0;

    public X64BootIdentityMappingStatus Status => _status;

    public bool Succeeded =>
        IsInitialized
        && _status == X64BootIdentityMappingStatus.Success;

    public OS.Boot.AddressValueStatus AddressStatus => _addressStatus;

    public OS.Kernel.Memory.X64PageTableEntryStatus PageTableEntryStatus =>
        _pageTableEntryStatus;

    public ulong SourceCount => _sourceCount;

    public ulong ValidatedSourceCount => _validatedSourceCount;

    public ulong SourceCapacity => _sourceCapacity;

    public ulong DestinationCapacity => _destinationCapacity;

    public ulong RequiredDestinationCapacity =>
        _requiredDestinationCapacity;

    public ulong OutputRangeCount => _outputRangeCount;

    public ulong AvailableTablePageCapacity =>
        _availableTablePageCapacity;

    public ulong RequiredPurposeMask => _requiredPurposeMask;

    public ulong ObservedPurposeMask => _observedPurposeMask;

    public ulong MissingPurposeMask => _missingPurposeMask;

    public ulong InvalidSourceIndex => _invalidSourceIndex;

    public ulong ConflictingSourceIndex => _conflictingSourceIndex;

    public ulong GuardSourceIndex => _guardSourceIndex;

    public ulong UnionPageCount => _unionPageCount;

    public ulong UnionByteLength => _unionByteLength;

    public ulong Pml4PageCount => _pml4PageCount;

    public ulong PdptPageCount => _pdptPageCount;

    public ulong PdPageCount => _pdPageCount;

    public ulong PtPageCount => _ptPageCount;

    public ulong TotalTablePageCount => _totalTablePageCount;

    public ulong TotalTableByteLength => _totalTableByteLength;

    internal bool HasCanonicalState =>
        IsInitialized;
}

/// <summary>
/// Builds a deterministic, allocation-free description of the low canonical
/// identity ranges and fresh four-level hierarchy required by early boot.
/// It never allocates table pages, writes page tables, loads CR3, or changes a
/// live mapping.
/// </summary>
public static unsafe class X64BootIdentityMappingPlan
{
    public const ulong PageSize = 4096;
    public const ulong MaximumSourceCount = 256;
    public const ulong MaximumDestinationCount = 512;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong LowCanonicalEndExclusive = 0x0000800000000000UL;
    public const ulong RequiredPurposeMask = 0x7e;
    public const bool UsesOnly4KiBLeaves = true;
    public const bool PerformsLiveMapping = false;
    public const bool AllocatesPageTables = false;
    public const bool PopulatesPageTables = false;

    private const ulong MaximumUnsignedLong = ~0UL;

    public static X64BootIdentityMappingPlanResult Create(
        X64BootIdentityMappingRequirement* source,
        ulong sourceCount,
        ulong sourceCapacity,
        X64BootIdentityMappingRange* destination,
        ulong destinationCapacity,
        ulong availableTablePageCapacity,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions preconditions)
    {
        PlannerState state = new PlannerState(
            sourceCount,
            sourceCapacity,
            destinationCapacity,
            availableTablePageCapacity);

        if (sourceCount == 0)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.InvalidSourceCount);
        }

        if (sourceCount > MaximumSourceCount)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.TooManySources);
        }

        if (sourceCapacity > MaximumSourceCount ||
            destinationCapacity > MaximumDestinationCount)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.CapacityExceedsLimit);
        }

        if (sourceCount > sourceCapacity)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.SourceCapacityTooSmall);
        }

        if ((ulong)sizeof(X64BootIdentityMappingRequirement)
                != X64BootIdentityMappingRequirement.StructureSize ||
            (ulong)sizeof(X64BootIdentityMappingRange)
                != X64BootIdentityMappingRange.StructureSize ||
            (ulong)sizeof(X64BootIdentityMappingPlanResult)
                != X64BootIdentityMappingPlanResult.StructureSize)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.ContractLayoutMismatch);
        }

        if (source == null)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.MissingSource);
        }

        if (destination == null)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.MissingDestination);
        }

        ulong sourceAddress = (ulong)source;
        ulong destinationAddress = (ulong)destination;
        if ((sourceAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.MisalignedSource,
                OS.Boot.AddressValueStatus.MisalignedAddress);
        }

        if ((destinationAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.MisalignedDestination,
                OS.Boot.AddressValueStatus.MisalignedAddress);
        }

        if (!TryGetSpanEnd(
                sourceAddress,
                sourceCapacity,
                X64BootIdentityMappingRequirement.StructureSize,
                out ulong sourceEnd))
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.SourceSpanOverflow,
                OS.Boot.AddressValueStatus.AddressOverflow);
        }

        if (!TryGetSpanEnd(
                destinationAddress,
                destinationCapacity,
                X64BootIdentityMappingRange.StructureSize,
                out ulong destinationEnd))
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.DestinationSpanOverflow,
                OS.Boot.AddressValueStatus.AddressOverflow);
        }

        if (destinationCapacity != 0 &&
            sourceAddress < destinationEnd &&
            destinationAddress < sourceEnd)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.SourceDestinationOverlap);
        }

        OS.Kernel.Memory.X64PageTableEntryStatus entryStatus =
            preconditions.Validate();
        if (entryStatus
            != OS.Kernel.Memory.X64PageTableEntryStatus.Success)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.InvalidPreconditions,
                OS.Boot.AddressValueStatus.Uninitialized,
                entryStatus);
        }

        entryStatus =
            preconditions.PatMapping.TryValidateMemoryTypeAtIndex(
                0,
                OS.Kernel.Memory.PageCachePolicy.WriteBack);
        if (entryStatus
            != OS.Kernel.Memory.X64PageTableEntryStatus.Success)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.PageWalkCacheNotWriteBack,
                OS.Boot.AddressValueStatus.Uninitialized,
                entryStatus);
        }

        for (ulong index = 0; index < sourceCount; index++)
        {
            if (!TryNormalizeRequirement(
                    source[index],
                    preconditions,
                    out RequirementView view,
                    out X64BootIdentityMappingStatus status,
                    out OS.Boot.AddressValueStatus addressStatus,
                    out entryStatus))
            {
                return Fail(
                    state,
                    status,
                    addressStatus,
                    entryStatus,
                    index);
            }

            state.ValidatedSourceCount = index + 1;
            state.ObservedPurposeMask |= view.PurposeMask;

            if (view.IsGuard)
            {
                if (state.GuardSourceIndex
                    != X64BootIdentityMappingPlanResult.NoIndex)
                {
                    return Fail(
                        state,
                        X64BootIdentityMappingStatus.MultipleGuards,
                        OS.Boot.AddressValueStatus.Success,
                        OS.Kernel.Memory.X64PageTableEntryStatus.Success,
                        index,
                        state.GuardSourceIndex);
                }

                state.GuardSourceIndex = index;
            }
        }

        state.MissingPurposeMask =
            RequiredPurposeMask
            & ~state.ObservedPurposeMask;

        if (state.GuardSourceIndex
            == X64BootIdentityMappingPlanResult.NoIndex)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.MissingGuard,
                OS.Boot.AddressValueStatus.Success,
                OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        if (state.MissingPurposeMask != 0)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.MissingRequiredPurpose,
                OS.Boot.AddressValueStatus.Success,
                OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        for (ulong leftIndex = 0;
            leftIndex < sourceCount;
            leftIndex++)
        {
            TryReadValidatedRequirement(
                source[leftIndex],
                out RequirementView left);
            for (ulong rightIndex = leftIndex + 1;
                rightIndex < sourceCount;
                rightIndex++)
            {
                TryReadValidatedRequirement(
                    source[rightIndex],
                    out RequirementView right);
                if (!RangesOverlap(
                        left.Start,
                        left.EndExclusive,
                        right.Start,
                        right.EndExclusive))
                {
                    continue;
                }

                if (left.IsGuard || right.IsGuard)
                {
                    ulong guardIndex = left.IsGuard
                        ? leftIndex
                        : rightIndex;
                    ulong mappedIndex = left.IsGuard
                        ? rightIndex
                        : leftIndex;
                    return Fail(
                        state,
                        X64BootIdentityMappingStatus.GuardOverlap,
                        OS.Boot.AddressValueStatus.Success,
                        OS.Kernel.Memory.X64PageTableEntryStatus.Success,
                        guardIndex,
                        mappedIndex);
                }

                if (left.Protection != right.Protection)
                {
                    return Fail(
                        state,
                        X64BootIdentityMappingStatus
                            .IncompatibleProtectionOverlap,
                        OS.Boot.AddressValueStatus.Success,
                        OS.Kernel.Memory.X64PageTableEntryStatus.Success,
                        rightIndex,
                        leftIndex);
                }

                if (left.CachePolicy != right.CachePolicy)
                {
                    return Fail(
                        state,
                        X64BootIdentityMappingStatus
                            .IncompatibleCacheOverlap,
                        OS.Boot.AddressValueStatus.Success,
                        OS.Kernel.Memory.X64PageTableEntryStatus.Success,
                        rightIndex,
                        leftIndex);
                }
            }
        }

        X64BootIdentityMappingStatus analysisStatus =
            AnalyzeNormalizedOutput(source, sourceCount, ref state);
        if (analysisStatus != X64BootIdentityMappingStatus.Success)
        {
            return Fail(
                state,
                analysisStatus,
                OS.Boot.AddressValueStatus.Success,
                OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        if (state.RequiredDestinationCapacity
            > MaximumDestinationCount)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.TooManyOutputRanges,
                OS.Boot.AddressValueStatus.Success,
                OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        if (state.RequiredDestinationCapacity > destinationCapacity)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus
                    .DestinationCapacityTooSmall,
                OS.Boot.AddressValueStatus.Success,
                OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        if (state.TotalTablePageCount > availableTablePageCapacity)
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.TableCapacityTooSmall,
                OS.Boot.AddressValueStatus.Success,
                OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        if (!CanConstructNormalizedOutput(
                source,
                sourceCount,
                state.RequiredDestinationCapacity))
        {
            return Fail(
                state,
                X64BootIdentityMappingStatus.OutputConstructionFailure,
                OS.Boot.AddressValueStatus.InvalidAddress,
                OS.Kernel.Memory.X64PageTableEntryStatus.Success);
        }

        // The caller must keep the complete declared source span stable for
        // the duration of this call. All fallible work, including a second
        // reconstruction check, has completed before this write-only pass.
        WriteNormalizedOutput(
            source,
            sourceCount,
            destination);
        state.OutputRangeCount =
            state.RequiredDestinationCapacity;
        return BuildResult(
            state,
            X64BootIdentityMappingStatus.Success,
            OS.Boot.AddressValueStatus.Success,
            OS.Kernel.Memory.X64PageTableEntryStatus.Success,
            X64BootIdentityMappingPlanResult.NoIndex,
            X64BootIdentityMappingPlanResult.NoIndex);
    }

    private static bool TryNormalizeRequirement(
        X64BootIdentityMappingRequirement requirement,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions preconditions,
        out RequirementView view,
        out X64BootIdentityMappingStatus status,
        out OS.Boot.AddressValueStatus addressStatus,
        out OS.Kernel.Memory.X64PageTableEntryStatus entryStatus)
    {
        view = default;
        status = X64BootIdentityMappingStatus.Success;
        addressStatus = OS.Boot.AddressValueStatus.Success;
        entryStatus =
            OS.Kernel.Memory.X64PageTableEntryStatus.Success;

        if (!requirement.HasCanonicalState)
        {
            status =
                X64BootIdentityMappingStatus.InvalidRequirementState;
            return false;
        }

        X64BootIdentityMappingPurpose purpose = requirement.Purpose;
        if (!IsValidPurpose(purpose))
        {
            status = X64BootIdentityMappingStatus.InvalidPurpose;
            return false;
        }

        bool isGuard =
            purpose == X64BootIdentityMappingPurpose.BootStackGuard;
        if (!requirement.PhysicalStart.IsValid)
        {
            status = isGuard
                ? X64BootIdentityMappingStatus.InvalidPhysicalStart
                : X64BootIdentityMappingStatus.PageZeroMappingRejected;
            addressStatus = OS.Boot.AddressValueStatus.ZeroAddress;
            return false;
        }

        addressStatus =
            requirement.PhysicalStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong start);
        if (addressStatus != OS.Boot.AddressValueStatus.Success)
        {
            status = X64BootIdentityMappingStatus.InvalidPhysicalStart;
            return false;
        }

        addressStatus =
            requirement.PhysicalEndExclusive
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong endExclusive);
        if (addressStatus != OS.Boot.AddressValueStatus.Success)
        {
            status = X64BootIdentityMappingStatus.InvalidPhysicalEnd;
            return false;
        }

        if (endExclusive <= start)
        {
            status = X64BootIdentityMappingStatus.EmptyOrReversedRange;
            addressStatus = OS.Boot.AddressValueStatus.InvalidAddress;
            return false;
        }

        if ((start & (PageSize - 1)) != 0 ||
            (endExclusive & (PageSize - 1)) != 0)
        {
            status = X64BootIdentityMappingStatus.MisalignedRange;
            addressStatus = OS.Boot.AddressValueStatus.MisalignedAddress;
            return false;
        }

        ulong pageCount = requirement.PageCount;
        ulong byteLength = requirement.ByteLength;
        if (pageCount == 0 || byteLength == 0)
        {
            status = X64BootIdentityMappingStatus.GeometryMismatch;
            addressStatus = OS.Boot.AddressValueStatus.ZeroLength;
            return false;
        }

        if (pageCount > MaximumUnsignedLong / PageSize)
        {
            status = X64BootIdentityMappingStatus.GeometryOverflow;
            addressStatus = OS.Boot.AddressValueStatus.AddressOverflow;
            return false;
        }

        ulong expectedByteLength = pageCount * PageSize;
        if (expectedByteLength != byteLength)
        {
            status = X64BootIdentityMappingStatus.GeometryMismatch;
            addressStatus = OS.Boot.AddressValueStatus.InvalidAddress;
            return false;
        }

        if (start > MaximumUnsignedLong - byteLength)
        {
            status = X64BootIdentityMappingStatus.GeometryOverflow;
            addressStatus = OS.Boot.AddressValueStatus.AddressOverflow;
            return false;
        }

        if (start + byteLength != endExclusive)
        {
            status = X64BootIdentityMappingStatus.GeometryMismatch;
            addressStatus = OS.Boot.AddressValueStatus.InvalidAddress;
            return false;
        }

        if (!isGuard && start < PageSize)
        {
            status =
                X64BootIdentityMappingStatus.PageZeroMappingRejected;
            addressStatus = OS.Boot.AddressValueStatus.ZeroAddress;
            return false;
        }

        if (endExclusive > LowCanonicalEndExclusive)
        {
            status =
                X64BootIdentityMappingStatus.NonCanonicalIdentityRange;
            addressStatus = OS.Boot.AddressValueStatus.InvalidAddress;
            return false;
        }

        ulong physicalLimit =
            1UL << preconditions.PhysicalAddressWidth;
        if (endExclusive > physicalLimit)
        {
            status =
                X64BootIdentityMappingStatus.PhysicalAddressOutsideWidth;
            addressStatus = OS.Boot.AddressValueStatus.InvalidAddress;
            entryStatus = OS.Kernel.Memory.X64PageTableEntryStatus
                .PhysicalAddressOutsideWidth;
            return false;
        }

        OS.Kernel.Memory.PageProtection protection =
            requirement.Protection;
        OS.Kernel.Memory.PageCachePolicy cachePolicy =
            requirement.CachePolicy;
        if (isGuard)
        {
            if (protection != OS.Kernel.Memory.PageProtection.None ||
                cachePolicy
                    != OS.Kernel.Memory.PageCachePolicy.Uninitialized)
            {
                status =
                    X64BootIdentityMappingStatus.InvalidGuardPolicy;
                return false;
            }
        }
        else
        {
            if (!IsValidLeafProtection(protection))
            {
                status = HasProtection(
                        protection,
                        OS.Kernel.Memory.PageProtection.Write)
                    && HasProtection(
                        protection,
                        OS.Kernel.Memory.PageProtection.Execute)
                    ? X64BootIdentityMappingStatus
                        .WritableExecutableRejected
                    : X64BootIdentityMappingStatus.InvalidProtection;
                entryStatus = status
                        == X64BootIdentityMappingStatus
                            .WritableExecutableRejected
                    ? OS.Kernel.Memory.X64PageTableEntryStatus
                        .WritableExecutableRejected
                    : OS.Kernel.Memory.X64PageTableEntryStatus
                        .InvalidProtection;
                return false;
            }

            if (!IsValidCachePolicy(cachePolicy))
            {
                status = X64BootIdentityMappingStatus.InvalidCachePolicy;
                entryStatus = OS.Kernel.Memory.X64PageTableEntryStatus
                    .InvalidCachePolicy;
                return false;
            }

            if (!TryValidateLeafEndpoint(
                    requirement.PhysicalStart,
                    protection,
                    cachePolicy,
                    preconditions,
                    out entryStatus))
            {
                status =
                    X64BootIdentityMappingStatus.LeafEncodingRejected;
                return false;
            }

            ulong lastPageStart = endExclusive - PageSize;
            addressStatus =
                OS.Boot.PhysicalAddress
                    .TryCreateAlignedFromHardwareRegisterBoundary(
                        lastPageStart,
                        PageSize,
                        out OS.Boot.PhysicalAddress lastPageAddress);
            if (addressStatus != OS.Boot.AddressValueStatus.Success)
            {
                status =
                    X64BootIdentityMappingStatus.InvalidPhysicalEnd;
                return false;
            }

            if (!TryValidateLeafEndpoint(
                    lastPageAddress,
                    protection,
                    cachePolicy,
                    preconditions,
                    out entryStatus))
            {
                status =
                    X64BootIdentityMappingStatus.LeafEncodingRejected;
                return false;
            }
        }

        view = new RequirementView(
            start,
            endExclusive,
            pageCount,
            byteLength,
            PurposeBit(purpose),
            protection,
            cachePolicy,
            isGuard);
        addressStatus = OS.Boot.AddressValueStatus.Success;
        entryStatus =
            OS.Kernel.Memory.X64PageTableEntryStatus.Success;
        return true;
    }

    private static bool TryValidateLeafEndpoint(
        OS.Boot.PhysicalAddress address,
        OS.Kernel.Memory.PageProtection protection,
        OS.Kernel.Memory.PageCachePolicy cachePolicy,
        OS.Kernel.Memory.X64PageTableEncodingPreconditions preconditions,
        out OS.Kernel.Memory.X64PageTableEntryStatus entryStatus)
    {
        OS.Kernel.Memory.X64PageTableEntryResult result =
            OS.Kernel.Memory.X64PageTableEntry.TryEncode(
                new OS.Kernel.Memory.X64PageTableEntryRequest(
                    OS.Kernel.Memory.X64PageTableLevel.Pte,
                    OS.Kernel.Memory.X64PageTableEntryKind.Leaf,
                    address,
                    protection,
                    cachePolicy),
                preconditions);
        entryStatus = result.Status;
        return result.Succeeded;
    }

    private static void TryReadValidatedRequirement(
        X64BootIdentityMappingRequirement requirement,
        out RequirementView view)
    {
        requirement.PhysicalStart
            .TryGetRawValueForHardwareRegisterBoundary(
                out ulong start);
        requirement.PhysicalEndExclusive
            .TryGetRawValueForHardwareRegisterBoundary(
                out ulong endExclusive);
        X64BootIdentityMappingPurpose purpose = requirement.Purpose;
        view = new RequirementView(
            start,
            endExclusive,
            requirement.PageCount,
            requirement.ByteLength,
            PurposeBit(purpose),
            requirement.Protection,
            requirement.CachePolicy,
            purpose == X64BootIdentityMappingPurpose.BootStackGuard);
    }

    private static X64BootIdentityMappingStatus AnalyzeNormalizedOutput(
        X64BootIdentityMappingRequirement* source,
        ulong sourceCount,
        ref PlannerState state)
    {
        ulong cursor = PageSize;
        ulong outputCount = 0;
        ulong lastPdptKey = 0;
        ulong lastPdKey = 0;
        ulong lastPtKey = 0;

        while (TryBuildNextMergedRange(
            source,
            sourceCount,
            cursor,
            out MergedRange range))
        {
            if (outputCount >= MaximumDestinationCount)
            {
                state.RequiredDestinationCapacity = outputCount + 1;
                return X64BootIdentityMappingStatus
                    .TooManyOutputRanges;
            }

            if (!TryValidateOutputConstruction(range))
            {
                return X64BootIdentityMappingStatus
                    .OutputConstructionFailure;
            }

            if (state.UnionPageCount
                > MaximumUnsignedLong - range.PageCount)
            {
                return X64BootIdentityMappingStatus
                    .UnionPageCountOverflow;
            }

            if (state.UnionByteLength
                > MaximumUnsignedLong - range.ByteLength)
            {
                return X64BootIdentityMappingStatus
                    .UnionByteCountOverflow;
            }

            state.UnionPageCount += range.PageCount;
            state.UnionByteLength += range.ByteLength;

            ulong lastAddress = range.EndExclusive - 1;
            if (!TryAccumulateDistinctKeys(
                    range.Start >> 39,
                    lastAddress >> 39,
                    outputCount != 0,
                    ref lastPdptKey,
                    ref state.PdptPageCount) ||
                !TryAccumulateDistinctKeys(
                    range.Start >> 30,
                    lastAddress >> 30,
                    outputCount != 0,
                    ref lastPdKey,
                    ref state.PdPageCount) ||
                !TryAccumulateDistinctKeys(
                    range.Start >> 21,
                    lastAddress >> 21,
                    outputCount != 0,
                    ref lastPtKey,
                    ref state.PtPageCount))
            {
                return X64BootIdentityMappingStatus
                    .TablePageCountOverflow;
            }

            outputCount++;
            cursor = range.EndExclusive;
        }

        state.Pml4PageCount = 1;
        state.RequiredDestinationCapacity = outputCount;

        ulong tablePageCount = state.Pml4PageCount;
        if (!TryAdd(
                tablePageCount,
                state.PdptPageCount,
                out tablePageCount) ||
            !TryAdd(
                tablePageCount,
                state.PdPageCount,
                out tablePageCount) ||
            !TryAdd(
                tablePageCount,
                state.PtPageCount,
                out tablePageCount))
        {
            return X64BootIdentityMappingStatus
                .TablePageCountOverflow;
        }

        state.TotalTablePageCount = tablePageCount;
        if (tablePageCount > MaximumUnsignedLong / PageSize)
        {
            return X64BootIdentityMappingStatus
                .TableByteCountOverflow;
        }

        state.TotalTableByteLength = tablePageCount * PageSize;
        if (state.UnionPageCount > MaximumUnsignedLong / PageSize ||
            state.UnionPageCount * PageSize
                != state.UnionByteLength)
        {
            return X64BootIdentityMappingStatus.AccountingMismatch;
        }

        return X64BootIdentityMappingStatus.Success;
    }

    private static bool CanConstructNormalizedOutput(
        X64BootIdentityMappingRequirement* source,
        ulong sourceCount,
        ulong expectedOutputCount)
    {
        ulong cursor = PageSize;
        ulong outputIndex = 0;
        while (TryBuildNextMergedRange(
            source,
            sourceCount,
            cursor,
            out MergedRange range))
        {
            if (outputIndex >= expectedOutputCount ||
                !TryCreateOutputRange(
                    range,
                    out X64BootIdentityMappingRange output) ||
                !output.HasCanonicalState)
            {
                return false;
            }

            outputIndex++;
            cursor = range.EndExclusive;
        }

        return outputIndex == expectedOutputCount;
    }

    private static void WriteNormalizedOutput(
        X64BootIdentityMappingRequirement* source,
        ulong sourceCount,
        X64BootIdentityMappingRange* destination)
    {
        ulong cursor = PageSize;
        ulong outputIndex = 0;
        while (TryBuildNextMergedRange(
            source,
            sourceCount,
            cursor,
            out MergedRange range))
        {
            TryCreateOutputRange(
                range,
                out X64BootIdentityMappingRange output);
            destination[outputIndex] = output;
            outputIndex++;
            cursor = range.EndExclusive;
        }
    }

    private static bool TryBuildNextMergedRange(
        X64BootIdentityMappingRequirement* source,
        ulong sourceCount,
        ulong cursor,
        out MergedRange merged)
    {
        merged = default;
        bool found = false;
        RequirementView seed = default;
        for (ulong index = 0; index < sourceCount; index++)
        {
            TryReadValidatedRequirement(
                source[index],
                out RequirementView current);
            if (current.IsGuard || current.Start < cursor)
            {
                continue;
            }

            if (!found || current.Start < seed.Start)
            {
                seed = current;
                found = true;
            }
        }

        if (!found)
        {
            return false;
        }

        ulong start = seed.Start;
        ulong endExclusive = seed.EndExclusive;
        ulong purposeMask = seed.PurposeMask;
        bool changed = true;
        while (changed)
        {
            changed = false;
            for (ulong index = 0; index < sourceCount; index++)
            {
                TryReadValidatedRequirement(
                    source[index],
                    out RequirementView current);
                if (current.IsGuard ||
                    current.Protection != seed.Protection ||
                    current.CachePolicy != seed.CachePolicy ||
                    current.Start > endExclusive ||
                    current.EndExclusive <= start)
                {
                    continue;
                }

                purposeMask |= current.PurposeMask;
                if (current.EndExclusive > endExclusive)
                {
                    endExclusive = current.EndExclusive;
                    changed = true;
                }
            }
        }

        ulong byteLength = endExclusive - start;
        merged = new MergedRange(
            start,
            endExclusive,
            byteLength / PageSize,
            byteLength,
            purposeMask,
            seed.Protection,
            seed.CachePolicy);
        return true;
    }

    private static bool TryValidateOutputConstruction(
        MergedRange range)
    {
        return TryCreateOutputRange(
            range,
            out X64BootIdentityMappingRange output)
            && output.HasCanonicalState;
    }

    private static bool TryCreateOutputRange(
        MergedRange range,
        out X64BootIdentityMappingRange output)
    {
        output = default;
        OS.Boot.AddressValueStatus status =
            OS.Boot.PhysicalAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    range.Start,
                    PageSize,
                    out OS.Boot.PhysicalAddress physicalStart);
        if (status != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        status =
            OS.Boot.PhysicalAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    range.EndExclusive,
                    PageSize,
                    out OS.Boot.PhysicalAddress physicalEnd);
        if (status != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        status =
            OS.Boot.VirtualAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    range.Start,
                    PageSize,
                    out OS.Boot.VirtualAddress virtualStart);
        if (status != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        status =
            OS.Boot.VirtualAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    range.EndExclusive,
                    PageSize,
                    out OS.Boot.VirtualAddress virtualEnd);
        if (status != OS.Boot.AddressValueStatus.Success)
        {
            return false;
        }

        output = new X64BootIdentityMappingRange(
            physicalStart,
            physicalEnd,
            virtualStart,
            virtualEnd,
            range.PageCount,
            range.ByteLength,
            range.PurposeMask,
            range.Protection,
            range.CachePolicy);
        return true;
    }

    private static bool TryAccumulateDistinctKeys(
        ulong firstKey,
        ulong finalKey,
        bool hasPrevious,
        ref ulong previousFinalKey,
        ref ulong count)
    {
        ulong addition = 0;
        if (!hasPrevious)
        {
            addition = finalKey - firstKey + 1;
        }
        else if (finalKey > previousFinalKey)
        {
            ulong nextUncounted = previousFinalKey + 1;
            ulong effectiveFirst = firstKey > nextUncounted
                ? firstKey
                : nextUncounted;
            addition = finalKey - effectiveFirst + 1;
        }

        if (count > MaximumUnsignedLong - addition)
        {
            return false;
        }

        count += addition;
        if (!hasPrevious || finalKey > previousFinalKey)
        {
            previousFinalKey = finalKey;
        }

        return true;
    }

    private static bool IsValidPurpose(
        X64BootIdentityMappingPurpose purpose)
    {
        return purpose
                == X64BootIdentityMappingPurpose.ExecutingImage
            || purpose == X64BootIdentityMappingPurpose.OwnedStack
            || purpose == X64BootIdentityMappingPurpose.BootHandoff
            || purpose == X64BootIdentityMappingPurpose.SerialStub
            || purpose == X64BootIdentityMappingPurpose.AcpiScratch
            || purpose
                == X64BootIdentityMappingPurpose.FramebufferMetadata
            || purpose == X64BootIdentityMappingPurpose.BootStackGuard
            || purpose
                == X64BootIdentityMappingPurpose.AdditionalRequired;
    }

    private static bool IsValidLeafProtection(
        OS.Kernel.Memory.PageProtection protection)
    {
        return protection == OS.Kernel.Memory.PageProtection.Read
            || protection == (OS.Kernel.Memory.PageProtection)(
                (byte)OS.Kernel.Memory.PageProtection.Read
                | (byte)OS.Kernel.Memory.PageProtection.Write)
            || protection == (OS.Kernel.Memory.PageProtection)(
                (byte)OS.Kernel.Memory.PageProtection.Read
                | (byte)OS.Kernel.Memory.PageProtection.Execute);
    }

    private static bool IsValidCachePolicy(
        OS.Kernel.Memory.PageCachePolicy cachePolicy)
    {
        return cachePolicy
                == OS.Kernel.Memory.PageCachePolicy.WriteBack
            || cachePolicy
                == OS.Kernel.Memory.PageCachePolicy.Uncached
            || cachePolicy
                == OS.Kernel.Memory.PageCachePolicy.WriteCombining;
    }

    private static bool HasProtection(
        OS.Kernel.Memory.PageProtection value,
        OS.Kernel.Memory.PageProtection flag)
    {
        return ((byte)value & (byte)flag) != 0;
    }

    private static ulong PurposeBit(
        X64BootIdentityMappingPurpose purpose)
    {
        return 1UL << (byte)purpose;
    }

    private static bool RangesOverlap(
        ulong leftStart,
        ulong leftEnd,
        ulong rightStart,
        ulong rightEnd)
    {
        return leftStart < rightEnd && rightStart < leftEnd;
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

    private static bool TryAdd(
        ulong left,
        ulong right,
        out ulong result)
    {
        result = 0;
        if (left > MaximumUnsignedLong - right)
        {
            return false;
        }

        result = left + right;
        return true;
    }

    private static X64BootIdentityMappingPlanResult Fail(
        PlannerState state,
        X64BootIdentityMappingStatus status,
        OS.Boot.AddressValueStatus addressStatus =
            OS.Boot.AddressValueStatus.Uninitialized,
        OS.Kernel.Memory.X64PageTableEntryStatus pageTableEntryStatus =
            OS.Kernel.Memory.X64PageTableEntryStatus.Uninitialized,
        ulong invalidSourceIndex =
            X64BootIdentityMappingPlanResult.NoIndex,
        ulong conflictingSourceIndex =
            X64BootIdentityMappingPlanResult.NoIndex)
    {
        return BuildResult(
            state,
            status,
            addressStatus,
            pageTableEntryStatus,
            invalidSourceIndex,
            conflictingSourceIndex);
    }

    private static X64BootIdentityMappingPlanResult BuildResult(
        PlannerState state,
        X64BootIdentityMappingStatus status,
        OS.Boot.AddressValueStatus addressStatus,
        OS.Kernel.Memory.X64PageTableEntryStatus pageTableEntryStatus,
        ulong invalidSourceIndex,
        ulong conflictingSourceIndex)
    {
        return new X64BootIdentityMappingPlanResult(
            status,
            addressStatus,
            pageTableEntryStatus,
            state.SourceCount,
            state.ValidatedSourceCount,
            state.SourceCapacity,
            state.DestinationCapacity,
            state.RequiredDestinationCapacity,
            state.OutputRangeCount,
            state.AvailableTablePageCapacity,
            RequiredPurposeMask,
            state.ObservedPurposeMask,
            state.MissingPurposeMask,
            invalidSourceIndex,
            conflictingSourceIndex,
            state.GuardSourceIndex,
            state.UnionPageCount,
            state.UnionByteLength,
            state.Pml4PageCount,
            state.PdptPageCount,
            state.PdPageCount,
            state.PtPageCount,
            state.TotalTablePageCount,
            state.TotalTableByteLength);
    }

    private readonly struct RequirementView
    {
        public RequirementView(
            ulong start,
            ulong endExclusive,
            ulong pageCount,
            ulong byteLength,
            ulong purposeMask,
            OS.Kernel.Memory.PageProtection protection,
            OS.Kernel.Memory.PageCachePolicy cachePolicy,
            bool isGuard)
        {
            Start = start;
            EndExclusive = endExclusive;
            PageCount = pageCount;
            ByteLength = byteLength;
            PurposeMask = purposeMask;
            Protection = protection;
            CachePolicy = cachePolicy;
            IsGuard = isGuard;
        }

        public ulong Start { get; }

        public ulong EndExclusive { get; }

        public ulong PageCount { get; }

        public ulong ByteLength { get; }

        public ulong PurposeMask { get; }

        public OS.Kernel.Memory.PageProtection Protection { get; }

        public OS.Kernel.Memory.PageCachePolicy CachePolicy { get; }

        public bool IsGuard { get; }
    }

    private readonly struct MergedRange
    {
        public MergedRange(
            ulong start,
            ulong endExclusive,
            ulong pageCount,
            ulong byteLength,
            ulong purposeMask,
            OS.Kernel.Memory.PageProtection protection,
            OS.Kernel.Memory.PageCachePolicy cachePolicy)
        {
            Start = start;
            EndExclusive = endExclusive;
            PageCount = pageCount;
            ByteLength = byteLength;
            PurposeMask = purposeMask;
            Protection = protection;
            CachePolicy = cachePolicy;
        }

        public ulong Start { get; }

        public ulong EndExclusive { get; }

        public ulong PageCount { get; }

        public ulong ByteLength { get; }

        public ulong PurposeMask { get; }

        public OS.Kernel.Memory.PageProtection Protection { get; }

        public OS.Kernel.Memory.PageCachePolicy CachePolicy { get; }
    }

    private struct PlannerState
    {
        public PlannerState(
            ulong sourceCount,
            ulong sourceCapacity,
            ulong destinationCapacity,
            ulong availableTablePageCapacity)
        {
            SourceCount = sourceCount;
            ValidatedSourceCount = 0;
            SourceCapacity = sourceCapacity;
            DestinationCapacity = destinationCapacity;
            RequiredDestinationCapacity = 0;
            OutputRangeCount = 0;
            AvailableTablePageCapacity =
                availableTablePageCapacity;
            ObservedPurposeMask = 0;
            MissingPurposeMask = RequiredPurposeMask;
            GuardSourceIndex =
                X64BootIdentityMappingPlanResult.NoIndex;
            UnionPageCount = 0;
            UnionByteLength = 0;
            Pml4PageCount = 0;
            PdptPageCount = 0;
            PdPageCount = 0;
            PtPageCount = 0;
            TotalTablePageCount = 0;
            TotalTableByteLength = 0;
        }

        public ulong SourceCount;
        public ulong ValidatedSourceCount;
        public ulong SourceCapacity;
        public ulong DestinationCapacity;
        public ulong RequiredDestinationCapacity;
        public ulong OutputRangeCount;
        public ulong AvailableTablePageCapacity;
        public ulong ObservedPurposeMask;
        public ulong MissingPurposeMask;
        public ulong GuardSourceIndex;
        public ulong UnionPageCount;
        public ulong UnionByteLength;
        public ulong Pml4PageCount;
        public ulong PdptPageCount;
        public ulong PdPageCount;
        public ulong PtPageCount;
        public ulong TotalTablePageCount;
        public ulong TotalTableByteLength;
    }
}
