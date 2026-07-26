namespace OS.Kernel.Memory;

/// <summary>
/// Reports construction or materialization of one caller-owned x64
/// page-granular TLB invalidation plan. Values are append-only.
/// </summary>
public enum X64TlbInvalidationStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidRange = 2,
    InvalidVirtualStart = 3,
    PageZeroRejected = 4,
    MisalignedVirtualStart = 5,
    InvalidByteLength = 6,
    ByteLengthNotPageAligned = 7,
    RangeOverflow = 8,
    NonCanonicalRange = 9,
    TooManyPages = 10,
    InvalidPlanState = 11,
    DestinationCapacityTooSmall = 12,
    CapacityExceedsLimit = 13,
    MissingDestination = 14,
    MisalignedDestination = 15,
    DestinationSpanOverflow = 16,
    InvalidRecord = 17
}

/// <summary>
/// Immutable value evidence for one exact page-aligned virtual range whose
/// translations must be invalidated after a successful page-table mutation.
/// It does not perform an architectural invalidation.
/// </summary>
public readonly struct X64TlbInvalidationRange
{
    public const ulong StructureSize = 40;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _reserved;
    private readonly OS.Boot.VirtualAddress _virtualStart;
    private readonly OS.Boot.VirtualAddress _virtualEndExclusive;
    private readonly ulong _byteLength;
    private readonly ulong _pageCount;

    internal X64TlbInvalidationRange(
        OS.Boot.VirtualAddress virtualStart,
        OS.Boot.VirtualAddress virtualEndExclusive,
        ulong byteLength,
        ulong pageCount)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved = 0;
        _virtualStart = virtualStart;
        _virtualEndExclusive = virtualEndExclusive;
        _byteLength = byteLength;
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

    public ulong ByteLength => _byteLength;

    public ulong PageCount => _pageCount;

    public X64TlbInvalidationStatus Validate()
    {
        if (!IsInitialized)
        {
            return X64TlbInvalidationStatus.InvalidPlanState;
        }

        return X64TlbInvalidationPlan.ValidateRangeFields(
            _virtualStart,
            _virtualEndExclusive,
            _byteLength,
            _pageCount);
    }
}

/// <summary>
/// One exact target for a future checked INVLPG invocation.
/// </summary>
public readonly struct X64TlbInvalidationRecord
{
    public const ulong StructureSize = 24;
    public const ushort StructureVersion = 1;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly byte _reserved;
    private readonly OS.Boot.VirtualAddress _target;
    private readonly ulong _ordinal;

    internal X64TlbInvalidationRecord(
        OS.Boot.VirtualAddress target,
        ulong ordinal)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _reserved = 0;
        _target = target;
        _ordinal = ordinal;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved == 0;

    public OS.Boot.VirtualAddress Target => _target;

    public ulong Ordinal => _ordinal;

    public X64TlbInvalidationStatus Validate()
    {
        if (!IsInitialized)
        {
            return X64TlbInvalidationStatus.InvalidRecord;
        }

        if (!_target.IsValid ||
            !_target.IsAligned(X64TlbInvalidationPlan.PageSize))
        {
            return X64TlbInvalidationStatus.InvalidRecord;
        }

        if (_target.TryGetRawValueForHardwareRegisterBoundary(
                out ulong rawTarget)
            != OS.Boot.AddressValueStatus.Success ||
            rawTarget == 0 ||
            rawTarget >= X64TlbInvalidationPlan
                .LowCanonicalEndExclusive)
        {
            return X64TlbInvalidationStatus.InvalidRecord;
        }

        return X64TlbInvalidationStatus.Success;
    }
}

/// <summary>
/// Status-coded materialization evidence. The zero/default value is never
/// successful.
/// </summary>
public readonly struct X64TlbInvalidationPlanResult
{
    public const ulong StructureSize = 88;
    public const ushort StructureVersion = 1;
    public const ulong NoIndex = ~0UL;

    private readonly uint _structureSize;
    private readonly ushort _structureVersion;
    private readonly byte _isInitialized;
    private readonly X64TlbInvalidationStatus _status;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly ushort _reserved2;
    private readonly uint _reserved3;
    private readonly X64TlbInvalidationRange _range;
    private readonly ulong _destinationCapacity;
    private readonly ulong _requiredRecordCount;
    private readonly ulong _writtenRecordCount;
    private readonly ulong _invalidRecordIndex;

    private X64TlbInvalidationPlanResult(
        X64TlbInvalidationStatus status,
        X64TlbInvalidationRange range,
        ulong destinationCapacity,
        ulong requiredRecordCount,
        ulong writtenRecordCount,
        ulong invalidRecordIndex)
    {
        _structureSize = (uint)StructureSize;
        _structureVersion = StructureVersion;
        _isInitialized = 1;
        _status = status;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _range = range;
        _destinationCapacity = destinationCapacity;
        _requiredRecordCount = requiredRecordCount;
        _writtenRecordCount = writtenRecordCount;
        _invalidRecordIndex = invalidRecordIndex;
    }

    public bool IsInitialized =>
        _structureSize == StructureSize
        && _structureVersion == StructureVersion
        && _isInitialized == 1
        && _reserved0 == 0
        && _reserved1 == 0
        && _reserved2 == 0
        && _reserved3 == 0;

    public X64TlbInvalidationStatus Status => _status;

    public X64TlbInvalidationRange Range => _range;

    public ulong DestinationCapacity => _destinationCapacity;

    public ulong RequiredRecordCount => _requiredRecordCount;

    public ulong WrittenRecordCount => _writtenRecordCount;

    public ulong InvalidRecordIndex => _invalidRecordIndex;

    public bool Succeeded =>
        IsInitialized
        && _status == X64TlbInvalidationStatus.Success
        && _range.Validate() == X64TlbInvalidationStatus.Success
        && _requiredRecordCount == _range.PageCount
        && _writtenRecordCount == _requiredRecordCount
        && _destinationCapacity >= _requiredRecordCount
        && _destinationCapacity
            <= X64TlbInvalidationPlan.MaximumPageCount
        && _invalidRecordIndex == NoIndex;

    internal static X64TlbInvalidationPlanResult Success(
        X64TlbInvalidationRange range,
        ulong destinationCapacity)
    {
        return new X64TlbInvalidationPlanResult(
            X64TlbInvalidationStatus.Success,
            range,
            destinationCapacity,
            range.PageCount,
            range.PageCount,
            NoIndex);
    }

    internal static X64TlbInvalidationPlanResult Fail(
        X64TlbInvalidationStatus status,
        X64TlbInvalidationRange range = default,
        ulong destinationCapacity = 0,
        ulong requiredRecordCount = 0,
        ulong invalidRecordIndex = NoIndex)
    {
        return new X64TlbInvalidationPlanResult(
            status,
            range,
            destinationCapacity,
            requiredRecordCount,
            0,
            invalidRecordIndex);
    }
}

/// <summary>
/// Builds and materializes exact target-only invalidation metadata. It never
/// executes INVLPG, reloads CR3, or observes a processor TLB.
/// </summary>
public static unsafe class X64TlbInvalidationPlan
{
    public const ulong PageSize = 4096;
    public const ulong MaximumPageCount = 4096;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong LowCanonicalEndExclusive =
        0x0000_8000_0000_0000UL;
    public const bool PerformsLiveInvalidation = false;
    public const bool ReloadsCr3 = false;

    public static X64TlbInvalidationStatus TryCreate(
        OS.Boot.VirtualAddress virtualStart,
        ulong byteLength,
        out X64TlbInvalidationRange range)
    {
        range = default;
        if (!virtualStart.IsValid)
        {
            return X64TlbInvalidationStatus.InvalidVirtualStart;
        }

        if (!virtualStart.IsAligned(PageSize))
        {
            return X64TlbInvalidationStatus.MisalignedVirtualStart;
        }

        if (byteLength == 0)
        {
            return X64TlbInvalidationStatus.InvalidByteLength;
        }

        if ((byteLength & (PageSize - 1)) != 0)
        {
            return X64TlbInvalidationStatus.ByteLengthNotPageAligned;
        }

        ulong pageCount = byteLength / PageSize;
        if (pageCount == 0 || pageCount > MaximumPageCount)
        {
            return X64TlbInvalidationStatus.TooManyPages;
        }

        if (virtualStart.TryGetRawValueForHardwareRegisterBoundary(
                out ulong rawStart)
            != OS.Boot.AddressValueStatus.Success)
        {
            return X64TlbInvalidationStatus.InvalidVirtualStart;
        }

        if (rawStart == 0)
        {
            return X64TlbInvalidationStatus.PageZeroRejected;
        }

        if (rawStart >= LowCanonicalEndExclusive)
        {
            return X64TlbInvalidationStatus.NonCanonicalRange;
        }

        if (virtualStart.TryGetEndExclusive(
                byteLength,
                out OS.Boot.VirtualAddress endExclusive)
            != OS.Boot.AddressValueStatus.Success ||
            endExclusive.TryGetRawValueForHardwareRegisterBoundary(
                out ulong rawEnd)
            != OS.Boot.AddressValueStatus.Success)
        {
            return X64TlbInvalidationStatus.RangeOverflow;
        }

        if (rawEnd > LowCanonicalEndExclusive ||
            rawEnd <= rawStart)
        {
            return X64TlbInvalidationStatus.NonCanonicalRange;
        }

        range = new X64TlbInvalidationRange(
            virtualStart,
            endExclusive,
            byteLength,
            pageCount);
        return X64TlbInvalidationStatus.Success;
    }

    public static X64TlbInvalidationPlanResult Materialize(
        X64TlbInvalidationRange range,
        X64TlbInvalidationRecord* destination,
        ulong destinationCapacity)
    {
        X64TlbInvalidationStatus rangeStatus = range.Validate();
        if (rangeStatus != X64TlbInvalidationStatus.Success)
        {
            return X64TlbInvalidationPlanResult.Fail(
                rangeStatus,
                range,
                destinationCapacity);
        }

        if (destinationCapacity > MaximumPageCount)
        {
            return X64TlbInvalidationPlanResult.Fail(
                X64TlbInvalidationStatus.CapacityExceedsLimit,
                range,
                destinationCapacity,
                range.PageCount);
        }

        if (destinationCapacity < range.PageCount)
        {
            return X64TlbInvalidationPlanResult.Fail(
                X64TlbInvalidationStatus.DestinationCapacityTooSmall,
                range,
                destinationCapacity,
                range.PageCount);
        }

        if (destination == null)
        {
            return X64TlbInvalidationPlanResult.Fail(
                X64TlbInvalidationStatus.MissingDestination,
                range,
                destinationCapacity,
                range.PageCount);
        }

        ulong destinationAddress = (ulong)destination;
        if ((destinationAddress & (RequiredPointerAlignment - 1)) != 0)
        {
            return X64TlbInvalidationPlanResult.Fail(
                X64TlbInvalidationStatus.MisalignedDestination,
                range,
                destinationCapacity,
                range.PageCount);
        }

        if (!TryGetSpanEnd(
                destinationAddress,
                destinationCapacity,
                X64TlbInvalidationRecord.StructureSize,
                out _))
        {
            return X64TlbInvalidationPlanResult.Fail(
                X64TlbInvalidationStatus.DestinationSpanOverflow,
                range,
                destinationCapacity,
                range.PageCount);
        }

        if (range.VirtualStart
                .TryGetRawValueForHardwareRegisterBoundary(
                    out ulong rawStart)
            != OS.Boot.AddressValueStatus.Success)
        {
            return X64TlbInvalidationPlanResult.Fail(
                X64TlbInvalidationStatus.InvalidRange,
                range,
                destinationCapacity,
                range.PageCount);
        }

        for (ulong index = 0; index < range.PageCount; index++)
        {
            ulong offset = index * PageSize;
            if (rawStart > ~0UL - offset)
            {
                return X64TlbInvalidationPlanResult.Fail(
                    X64TlbInvalidationStatus.RangeOverflow,
                    range,
                    destinationCapacity,
                    range.PageCount,
                    index);
            }

            OS.Boot.AddressValueStatus addressStatus =
                OS.Boot.VirtualAddress
                    .TryCreateAlignedFromHardwareRegisterBoundary(
                        rawStart + offset,
                        PageSize,
                        out OS.Boot.VirtualAddress target);
            if (addressStatus != OS.Boot.AddressValueStatus.Success)
            {
                return X64TlbInvalidationPlanResult.Fail(
                    X64TlbInvalidationStatus.InvalidRecord,
                    range,
                    destinationCapacity,
                    range.PageCount,
                    index);
            }
        }

        for (ulong index = 0; index < range.PageCount; index++)
        {
            ulong offset = index * PageSize;
            OS.Boot.VirtualAddress
                .TryCreateAlignedFromHardwareRegisterBoundary(
                    rawStart + offset,
                    PageSize,
                    out OS.Boot.VirtualAddress target);
            destination[index] =
                new X64TlbInvalidationRecord(target, index);
        }

        return X64TlbInvalidationPlanResult.Success(
            range,
            destinationCapacity);
    }

    internal static X64TlbInvalidationStatus ValidateRangeFields(
        OS.Boot.VirtualAddress virtualStart,
        OS.Boot.VirtualAddress virtualEndExclusive,
        ulong byteLength,
        ulong pageCount)
    {
        if (TryCreate(
                virtualStart,
                byteLength,
                out X64TlbInvalidationRange recreated)
            != X64TlbInvalidationStatus.Success)
        {
            return X64TlbInvalidationStatus.InvalidRange;
        }

        if (!virtualEndExclusive.IsValid ||
            !recreated.VirtualEndExclusive.Equals(
                virtualEndExclusive) ||
            recreated.PageCount != pageCount)
        {
            return X64TlbInvalidationStatus.InvalidRange;
        }

        return X64TlbInvalidationStatus.Success;
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
