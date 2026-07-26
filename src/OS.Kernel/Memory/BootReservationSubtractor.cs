namespace OS.Kernel.Memory;

public enum BootReservationSourceRangeStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidSourceRangeIndex = 2,
    ReservedBitsSet = 3,
    InvalidPhysicalStart = 4,
    MisalignedPhysicalStart = 5,
    InvalidPageCount = 6,
    PageByteCountOverflow = 7,
    ByteLengthMismatch = 8,
    InvalidPhysicalEndExclusive = 9,
    MisalignedPhysicalEndExclusive = 10,
    AddressRangeOverflow = 11,
    PhysicalEndMismatch = 12
}

public readonly struct BootReservationSourceRange
{
    public const ulong StructureSize = 40;

    private const ulong PageSize = 4096;
    private const ulong MaximumUnsignedLong = ~0UL;

    private readonly int _sourceRangeIndex;
    private readonly uint _reserved;
    private readonly OS.Boot.PhysicalAddress _physicalStart;
    private readonly OS.Boot.PhysicalAddress _physicalEndExclusive;
    private readonly ulong _pageCount;
    private readonly ulong _byteLength;

    private BootReservationSourceRange(
        int sourceRangeIndex,
        OS.Boot.PhysicalAddress physicalStart,
        OS.Boot.PhysicalAddress physicalEndExclusive,
        ulong pageCount,
        ulong byteLength)
    {
        _sourceRangeIndex = sourceRangeIndex;
        _reserved = 0;
        _physicalStart = physicalStart;
        _physicalEndExclusive = physicalEndExclusive;
        _pageCount = pageCount;
        _byteLength = byteLength;
    }

    public int SourceRangeIndex => _sourceRangeIndex;

    public OS.Boot.PhysicalAddress PhysicalStart => _physicalStart;

    public OS.Boot.PhysicalAddress PhysicalEndExclusive =>
        _physicalEndExclusive;

    public ulong PageCount => _pageCount;

    public ulong ByteLength => _byteLength;

    public bool IsValid =>
        Validate() == BootReservationSourceRangeStatus.Success;

    public static BootReservationSourceRangeStatus TryCreate(
        int sourceRangeIndex,
        OS.Boot.PhysicalAddress physicalStart,
        ulong pageCount,
        out BootReservationSourceRange range)
    {
        range = default;

        if (sourceRangeIndex < 0)
        {
            return BootReservationSourceRangeStatus
                .InvalidSourceRangeIndex;
        }

        if (!physicalStart.IsValid)
        {
            return BootReservationSourceRangeStatus
                .InvalidPhysicalStart;
        }

        if (!physicalStart.IsAligned(PageSize))
        {
            return BootReservationSourceRangeStatus
                .MisalignedPhysicalStart;
        }

        if (pageCount == 0)
        {
            return BootReservationSourceRangeStatus.InvalidPageCount;
        }

        if (pageCount > MaximumUnsignedLong / PageSize)
        {
            return BootReservationSourceRangeStatus
                .PageByteCountOverflow;
        }

        ulong byteLength = pageCount * PageSize;
        OS.Boot.AddressValueStatus endStatus =
            physicalStart.TryGetEndExclusive(
                byteLength,
                out OS.Boot.PhysicalAddress physicalEndExclusive);
        if (endStatus != OS.Boot.AddressValueStatus.Success)
        {
            return endStatus == OS.Boot.AddressValueStatus.AddressOverflow
                ? BootReservationSourceRangeStatus.AddressRangeOverflow
                : BootReservationSourceRangeStatus.InvalidPhysicalStart;
        }

        if (!physicalEndExclusive.IsAligned(PageSize))
        {
            return BootReservationSourceRangeStatus
                .MisalignedPhysicalEndExclusive;
        }

        range = new BootReservationSourceRange(
            sourceRangeIndex,
            physicalStart,
            physicalEndExclusive,
            pageCount,
            byteLength);
        return BootReservationSourceRangeStatus.Success;
    }

    public bool Equals(BootReservationSourceRange other)
    {
        return _sourceRangeIndex == other._sourceRangeIndex
            && _reserved == other._reserved
            && _physicalStart.Equals(other._physicalStart)
            && _physicalEndExclusive.Equals(
                other._physicalEndExclusive)
            && _pageCount == other._pageCount
            && _byteLength == other._byteLength;
    }

    internal BootReservationSourceRangeStatus Validate()
    {
        if (_sourceRangeIndex < 0)
        {
            return BootReservationSourceRangeStatus
                .InvalidSourceRangeIndex;
        }

        if (_reserved != 0)
        {
            return BootReservationSourceRangeStatus.ReservedBitsSet;
        }

        if (!_physicalStart.IsValid)
        {
            return BootReservationSourceRangeStatus
                .InvalidPhysicalStart;
        }

        if (!_physicalStart.IsAligned(PageSize))
        {
            return BootReservationSourceRangeStatus
                .MisalignedPhysicalStart;
        }

        if (_pageCount == 0)
        {
            return BootReservationSourceRangeStatus.InvalidPageCount;
        }

        if (_pageCount > MaximumUnsignedLong / PageSize)
        {
            return BootReservationSourceRangeStatus
                .PageByteCountOverflow;
        }

        ulong expectedByteLength = _pageCount * PageSize;
        if (_byteLength != expectedByteLength)
        {
            return BootReservationSourceRangeStatus.ByteLengthMismatch;
        }

        if (!_physicalEndExclusive.IsValid)
        {
            return BootReservationSourceRangeStatus
                .InvalidPhysicalEndExclusive;
        }

        if (!_physicalEndExclusive.IsAligned(PageSize))
        {
            return BootReservationSourceRangeStatus
                .MisalignedPhysicalEndExclusive;
        }

        OS.Boot.AddressValueStatus endStatus =
            _physicalStart.TryGetEndExclusive(
                expectedByteLength,
                out OS.Boot.PhysicalAddress expectedEndExclusive);
        if (endStatus != OS.Boot.AddressValueStatus.Success)
        {
            return endStatus == OS.Boot.AddressValueStatus.AddressOverflow
                ? BootReservationSourceRangeStatus.AddressRangeOverflow
                : BootReservationSourceRangeStatus.InvalidPhysicalStart;
        }

        return _physicalEndExclusive.Equals(expectedEndExclusive)
            ? BootReservationSourceRangeStatus.Success
            : BootReservationSourceRangeStatus.PhysicalEndMismatch;
    }

    internal static BootReservationSourceRange
        CreateValidatedForSubtraction(
            int sourceRangeIndex,
            OS.Boot.PhysicalAddress physicalStart,
            OS.Boot.PhysicalAddress physicalEndExclusive,
            ulong pageCount,
            ulong byteLength)
    {
        return new BootReservationSourceRange(
            sourceRangeIndex,
            physicalStart,
            physicalEndExclusive,
            pageCount,
            byteLength);
    }
}

public enum BootReservationSubtractionStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    ContractLayoutMismatch = 2,
    InvalidPageSize = 3,
    InvalidSourceRangeCount = 4,
    SourceRangeTableByteCountOverflow = 5,
    TooManySourceRanges = 6,
    SourceRangeCapacityTooSmall = 7,
    MissingSourceRanges = 8,
    MisalignedSourceRanges = 9,
    SourceRangeTableAddressRangeOverflow = 10,
    ReservationTableByteCountOverflow = 11,
    TooManyReservations = 12,
    ReservationCapacityTooSmall = 13,
    MissingReservations = 14,
    MisalignedReservations = 15,
    ReservationTableAddressRangeOverflow = 16,
    SourceReservationTablesOverlap = 17,
    InvalidSourceRange = 18,
    UnorderedSourceProvenance = 19,
    UnsortedSourceRange = 20,
    OverlappingSourceRange = 21,
    TotalSourcePageCountOverflow = 22,
    TotalSourceByteCountOverflow = 23,
    InvalidReservation = 24,
    UnsortedReservation = 25,
    OverlappingReservation = 26,
    ReservationOutsideSourceRange = 27,
    RemovedPageCountOverflow = 28,
    RemovedByteCountOverflow = 29,
    RequiredOutputRangeCountOverflow = 30,
    RemainingPageCountOverflow = 31,
    RemainingByteCountOverflow = 32,
    DestinationCapacityTooSmall = 33,
    DestinationTableByteCountOverflow = 34,
    MissingDestination = 35,
    MisalignedDestination = 36,
    DestinationTableAddressRangeOverflow = 37,
    DestinationOverlapsSourceRanges = 38,
    DestinationOverlapsReservations = 39,
    OutputRangeConstructionFailed = 40,
    AccountingMismatch = 41
}

public readonly struct BootReservationSubtractionResult
{
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;

    private BootReservationSubtractionResult(
        BootReservationSubtractionStatus status,
        BootReservationSourceRangeStatus sourceRangeStatus,
        BootReservedRangeStatus reservationRangeStatus,
        ulong sourceRangeCount,
        ulong sourceRangeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong requiredOutputRangeCount,
        ulong writtenOutputRangeCount,
        ulong availableDestinationCapacity,
        ulong invalidSourceRangeIndex,
        ulong invalidReservationIndex,
        ulong conflictingRangeIndex,
        ulong pageSize,
        ulong totalSourcePages,
        ulong totalSourceBytes,
        ulong removedPages,
        ulong removedBytes,
        ulong remainingPages,
        ulong remainingBytes)
    {
        Status = status;
        SourceRangeStatus = sourceRangeStatus;
        ReservationRangeStatus = reservationRangeStatus;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        SourceRangeCount = sourceRangeCount;
        SourceRangeCapacity = sourceRangeCapacity;
        ReservationCount = reservationCount;
        ReservationCapacity = reservationCapacity;
        RequiredOutputRangeCount = requiredOutputRangeCount;
        WrittenOutputRangeCount = writtenOutputRangeCount;
        AvailableDestinationCapacity =
            availableDestinationCapacity;
        InvalidSourceRangeIndex = invalidSourceRangeIndex;
        InvalidReservationIndex = invalidReservationIndex;
        ConflictingRangeIndex = conflictingRangeIndex;
        PageSize = pageSize;
        TotalSourcePages = totalSourcePages;
        TotalSourceBytes = totalSourceBytes;
        RemovedPages = removedPages;
        RemovedBytes = removedBytes;
        RemainingPages = remainingPages;
        RemainingBytes = remainingBytes;
    }

    public BootReservationSubtractionStatus Status { get; }

    public BootReservationSourceRangeStatus SourceRangeStatus { get; }

    public BootReservedRangeStatus ReservationRangeStatus { get; }

    public bool Succeeded =>
        Status == BootReservationSubtractionStatus.Success
        && SourceRangeStatus
            == BootReservationSourceRangeStatus.Success
        && ReservationRangeStatus == BootReservedRangeStatus.Success
        && ReservedFieldsAreZero()
        && SourceRangeCount != 0
        && SourceRangeCount
            <= BootReservationSubtractor.MaximumSourceRangeCount
        && SourceRangeCapacity >= SourceRangeCount
        && ReservationCount
            <= BootReservationSubtractor.MaximumReservationCount
        && ReservationCapacity >= ReservationCount
        && RequiredOutputRangeCount
            <= BootReservationSubtractor.MaximumOutputRangeCount
        && WrittenOutputRangeCount == RequiredOutputRangeCount
        && AvailableDestinationCapacity
            >= RequiredOutputRangeCount
        && InvalidSourceRangeIndex
            == BootReservationSubtractor.NoIndex
        && InvalidReservationIndex
            == BootReservationSubtractor.NoIndex
        && ConflictingRangeIndex
            == BootReservationSubtractor.NoIndex
        && PageSize == BootReservationSubtractor.RequiredPageSize
        && TotalSourcePages != 0
        && BootReservationSubtractor
            .IsRepresentableSuccessSummary(
                SourceRangeCount,
                SourceRangeCapacity,
                ReservationCount,
                ReservationCapacity,
                RequiredOutputRangeCount,
                AvailableDestinationCapacity,
                PageSize,
                TotalSourcePages,
                TotalSourceBytes,
                RemovedPages,
                RemovedBytes,
                RemainingPages,
                RemainingBytes)
        && (ReservationCount != 0
            || (RemovedPages == 0 && RemovedBytes == 0))
        && (ReservationCount == 0
            || (RemovedPages != 0 && RemovedBytes != 0))
        && (RemainingPages != 0
            || (RemainingBytes == 0
                && RequiredOutputRangeCount == 0))
        && (RemainingPages == 0
            || (RemainingBytes != 0
                && RequiredOutputRangeCount != 0));

    public ulong SourceRangeCount { get; }

    public ulong SourceRangeCapacity { get; }

    public ulong ReservationCount { get; }

    public ulong ReservationCapacity { get; }

    public ulong RequiredOutputRangeCount { get; }

    public ulong WrittenOutputRangeCount { get; }

    public ulong AvailableDestinationCapacity { get; }

    public ulong InvalidSourceRangeIndex { get; }

    public ulong InvalidReservationIndex { get; }

    public ulong ConflictingRangeIndex { get; }

    public ulong PageSize { get; }

    public ulong TotalSourcePages { get; }

    public ulong TotalSourceBytes { get; }

    public ulong RemovedPages { get; }

    public ulong RemovedBytes { get; }

    public ulong RemainingPages { get; }

    public ulong RemainingBytes { get; }

    internal static BootReservationSubtractionResult Success(
        ulong sourceRangeCount,
        ulong sourceRangeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong requiredOutputRangeCount,
        ulong writtenOutputRangeCount,
        ulong availableDestinationCapacity,
        ulong pageSize,
        ulong totalSourcePages,
        ulong totalSourceBytes,
        ulong removedPages,
        ulong removedBytes,
        ulong remainingPages,
        ulong remainingBytes)
    {
        return new BootReservationSubtractionResult(
            BootReservationSubtractionStatus.Success,
            BootReservationSourceRangeStatus.Success,
            BootReservedRangeStatus.Success,
            sourceRangeCount,
            sourceRangeCapacity,
            reservationCount,
            reservationCapacity,
            requiredOutputRangeCount,
            writtenOutputRangeCount,
            availableDestinationCapacity,
            BootReservationSubtractor.NoIndex,
            BootReservationSubtractor.NoIndex,
            BootReservationSubtractor.NoIndex,
            pageSize,
            totalSourcePages,
            totalSourceBytes,
            removedPages,
            removedBytes,
            remainingPages,
            remainingBytes);
    }

    internal static BootReservationSubtractionResult Fail(
        BootReservationSubtractionStatus status,
        BootReservationSourceRangeStatus sourceRangeStatus,
        BootReservedRangeStatus reservationRangeStatus,
        ulong sourceRangeCount,
        ulong sourceRangeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong requiredOutputRangeCount,
        ulong availableDestinationCapacity,
        ulong invalidSourceRangeIndex,
        ulong invalidReservationIndex,
        ulong conflictingRangeIndex,
        ulong pageSize,
        ulong totalSourcePages,
        ulong totalSourceBytes,
        ulong removedPages,
        ulong removedBytes,
        ulong remainingPages,
        ulong remainingBytes)
    {
        return new BootReservationSubtractionResult(
            status,
            sourceRangeStatus,
            reservationRangeStatus,
            sourceRangeCount,
            sourceRangeCapacity,
            reservationCount,
            reservationCapacity,
            requiredOutputRangeCount,
            writtenOutputRangeCount: 0,
            availableDestinationCapacity,
            invalidSourceRangeIndex,
            invalidReservationIndex,
            conflictingRangeIndex,
            pageSize,
            totalSourcePages,
            totalSourceBytes,
            removedPages,
            removedBytes,
            remainingPages,
            remainingBytes);
    }

    private bool ReservedFieldsAreZero()
    {
        return _reserved0 == 0
            && _reserved1 == 0
            && _reserved2 == 0
            && _reserved3 == 0
            && _reserved4 == 0;
    }
}

public static unsafe class BootReservationSubtractor
{
    public const ulong RequiredPageSize = 4096;
    public const ulong MaximumSourceRangeCount = 4096;
    public const ulong MaximumReservationCount = 256;
    public const ulong MaximumOutputRangeCount = 4096;
    public const ulong RequiredPointerAlignment = 8;
    public const ulong NoIndex = ~0UL;

    private const ulong MaximumUnsignedLong = ~0UL;
    private const ulong ExpectedBootReservedRangeStructureSize = 40;

    internal static ulong ActualSourceRangeStructureSize =>
        (ulong)sizeof(BootReservationSourceRange);

    internal static ulong ActualReservationStructureSize =>
        (ulong)sizeof(BootReservedRange);

    public static BootReservationSubtractionResult Subtract(
        BootReservationSourceRange* sourceRanges,
        ulong sourceRangeCount,
        ulong sourceRangeCapacity,
        BootReservedRange* reservations,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong pageSize,
        BootReservationSourceRange* destinationRanges,
        ulong destinationRangeCapacity)
    {
        BootReservationSourceRangeStatus sourceRangeStatus =
            BootReservationSourceRangeStatus.Success;
        BootReservedRangeStatus reservationRangeStatus =
            BootReservedRangeStatus.Success;
        ulong totalSourcePages = 0;
        ulong totalSourceBytes = 0;
        ulong removedPages = 0;
        ulong removedBytes = 0;
        ulong remainingPages = 0;
        ulong remainingBytes = 0;
        ulong requiredOutputRangeCount = 0;

        if (BootReservationSourceRange.StructureSize
                != ActualSourceRangeStructureSize
            || ExpectedBootReservedRangeStructureSize
                != ActualReservationStructureSize)
        {
            return Fail(
                BootReservationSubtractionStatus.ContractLayoutMismatch,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                NoIndex,
                NoIndex,
                NoIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        if (pageSize != RequiredPageSize)
        {
            return Fail(
                BootReservationSubtractionStatus.InvalidPageSize,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                NoIndex,
                NoIndex,
                NoIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        BootReservationSubtractionStatus storageStatus =
            ValidateSourceStorage(
                sourceRanges,
                sourceRangeCount,
                sourceRangeCapacity,
                out ulong sourceTableStart,
                out ulong sourceTableEndExclusive);
        if (storageStatus != BootReservationSubtractionStatus.Success)
        {
            return Fail(
                storageStatus,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                NoIndex,
                NoIndex,
                NoIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        storageStatus = ValidateReservationStorage(
            reservations,
            reservationCount,
            reservationCapacity,
            out ulong reservationTableStart,
            out ulong reservationTableEndExclusive);
        if (storageStatus != BootReservationSubtractionStatus.Success)
        {
            return Fail(
                storageStatus,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                NoIndex,
                NoIndex,
                NoIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        if (StorageRangesOverlap(
                sourceTableStart,
                sourceTableEndExclusive,
                reservationTableStart,
                reservationTableEndExclusive))
        {
            return Fail(
                BootReservationSubtractionStatus
                    .SourceReservationTablesOverlap,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                NoIndex,
                NoIndex,
                NoIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        BootReservationSubtractionStatus validationStatus =
            ValidateSourceRanges(
                sourceRanges,
                sourceRangeCount,
                out sourceRangeStatus,
                out ulong invalidSourceRangeIndex,
                out ulong conflictingRangeIndex,
                out totalSourcePages,
                out totalSourceBytes);
        if (validationStatus
            != BootReservationSubtractionStatus.Success)
        {
            return Fail(
                validationStatus,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                invalidSourceRangeIndex,
                NoIndex,
                conflictingRangeIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        validationStatus = ValidateReservations(
            reservations,
            reservationCount,
            out reservationRangeStatus,
            out ulong invalidReservationIndex,
            out conflictingRangeIndex,
            out removedPages,
            out removedBytes);
        if (validationStatus
            != BootReservationSubtractionStatus.Success)
        {
            return Fail(
                validationStatus,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                NoIndex,
                invalidReservationIndex,
                conflictingRangeIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        validationStatus = DryRunSubtraction(
            sourceRanges,
            sourceRangeCount,
            reservations,
            reservationCount,
            out requiredOutputRangeCount,
            out remainingPages,
            out remainingBytes,
            out sourceRangeStatus,
            out invalidSourceRangeIndex,
            out invalidReservationIndex,
            out conflictingRangeIndex);
        if (validationStatus
            != BootReservationSubtractionStatus.Success)
        {
            return Fail(
                validationStatus,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                invalidSourceRangeIndex,
                invalidReservationIndex,
                conflictingRangeIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        if (!AccountingIsExact(
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes))
        {
            return Fail(
                BootReservationSubtractionStatus.AccountingMismatch,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                NoIndex,
                NoIndex,
                NoIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        if (destinationRangeCapacity < requiredOutputRangeCount)
        {
            return Fail(
                BootReservationSubtractionStatus
                    .DestinationCapacityTooSmall,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                NoIndex,
                NoIndex,
                NoIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        storageStatus = ValidateDestinationStorage(
            destinationRanges,
            destinationRangeCapacity,
            out ulong destinationTableStart,
            out ulong destinationTableEndExclusive);
        if (storageStatus != BootReservationSubtractionStatus.Success)
        {
            return Fail(
                storageStatus,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                NoIndex,
                NoIndex,
                NoIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        if (StorageRangesOverlap(
                destinationTableStart,
                destinationTableEndExclusive,
                sourceTableStart,
                sourceTableEndExclusive))
        {
            return Fail(
                BootReservationSubtractionStatus
                    .DestinationOverlapsSourceRanges,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                NoIndex,
                NoIndex,
                NoIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        if (StorageRangesOverlap(
                destinationTableStart,
                destinationTableEndExclusive,
                reservationTableStart,
                reservationTableEndExclusive))
        {
            return Fail(
                BootReservationSubtractionStatus
                    .DestinationOverlapsReservations,
                sourceRangeStatus,
                reservationRangeStatus,
                sourceRangeCount,
                sourceRangeCapacity,
                reservationCount,
                reservationCapacity,
                requiredOutputRangeCount,
                destinationRangeCapacity,
                NoIndex,
                NoIndex,
                NoIndex,
                pageSize,
                totalSourcePages,
                totalSourceBytes,
                removedPages,
                removedBytes,
                remainingPages,
                remainingBytes);
        }

        ulong writtenOutputRangeCount = EmitRangesUnchecked(
            sourceRanges,
            sourceRangeCount,
            reservations,
            reservationCount,
            destinationRanges);

        return BootReservationSubtractionResult.Success(
            sourceRangeCount,
            sourceRangeCapacity,
            reservationCount,
            reservationCapacity,
            requiredOutputRangeCount,
            writtenOutputRangeCount,
            destinationRangeCapacity,
            pageSize,
            totalSourcePages,
            totalSourceBytes,
            removedPages,
            removedBytes,
            remainingPages,
            remainingBytes);
    }

    internal static bool IsRepresentableSuccessSummary(
        ulong sourceRangeCount,
        ulong sourceRangeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong requiredOutputRangeCount,
        ulong destinationRangeCapacity,
        ulong pageSize,
        ulong totalSourcePages,
        ulong totalSourceBytes,
        ulong removedPages,
        ulong removedBytes,
        ulong remainingPages,
        ulong remainingBytes)
    {
        ulong sourceStructureSize = ActualSourceRangeStructureSize;
        ulong reservationStructureSize =
            ActualReservationStructureSize;

        if (BootReservationSourceRange.StructureSize
                != sourceStructureSize
            || ExpectedBootReservedRangeStructureSize
                != reservationStructureSize
            || pageSize != RequiredPageSize
            || sourceRangeCount == 0
            || sourceRangeCount > MaximumSourceRangeCount
            || sourceRangeCapacity < sourceRangeCount
            || reservationCount > MaximumReservationCount
            || reservationCapacity < reservationCount
            || requiredOutputRangeCount > MaximumOutputRangeCount
            || destinationRangeCapacity < requiredOutputRangeCount
            || sourceRangeCapacity
                > MaximumUnsignedLong / sourceStructureSize
            || reservationCapacity
                > MaximumUnsignedLong / reservationStructureSize
            || destinationRangeCapacity
                > MaximumUnsignedLong / sourceStructureSize
            || totalSourcePages == 0
            || totalSourcePages < sourceRangeCount
            || removedPages < reservationCount
            || remainingPages < requiredOutputRangeCount
            || requiredOutputRangeCount
                > sourceRangeCount + reservationCount
            || requiredOutputRangeCount + reservationCount
                < sourceRangeCount
            || totalSourcePages > MaximumUnsignedLong / pageSize
            || removedPages > MaximumUnsignedLong / pageSize
            || remainingPages > MaximumUnsignedLong / pageSize
            || totalSourcePages * pageSize != totalSourceBytes
            || removedPages * pageSize != removedBytes
            || remainingPages * pageSize != remainingBytes
            || (reservationCount == 0
                && (requiredOutputRangeCount != sourceRangeCount
                    || removedPages != 0
                    || removedBytes != 0
                    || remainingPages != totalSourcePages
                    || remainingBytes != totalSourceBytes)))
        {
            return false;
        }

        return removedPages <= totalSourcePages
            && remainingPages == totalSourcePages - removedPages
            && removedBytes <= totalSourceBytes
            && remainingBytes == totalSourceBytes - removedBytes;
    }

    private static BootReservationSubtractionStatus
        ValidateSourceStorage(
            BootReservationSourceRange* sourceRanges,
            ulong sourceRangeCount,
            ulong sourceRangeCapacity,
            out ulong tableStart,
            out ulong tableEndExclusive)
    {
        tableStart = 0;
        tableEndExclusive = 0;
        ulong structureSize = ActualSourceRangeStructureSize;

        if (sourceRangeCount == 0)
        {
            return BootReservationSubtractionStatus
                .InvalidSourceRangeCount;
        }

        if (sourceRangeCount
                > MaximumUnsignedLong / structureSize
            || sourceRangeCapacity
                > MaximumUnsignedLong / structureSize)
        {
            return BootReservationSubtractionStatus
                .SourceRangeTableByteCountOverflow;
        }

        if (sourceRangeCount > MaximumSourceRangeCount)
        {
            return BootReservationSubtractionStatus
                .TooManySourceRanges;
        }

        if (sourceRangeCapacity < sourceRangeCount)
        {
            return BootReservationSubtractionStatus
                .SourceRangeCapacityTooSmall;
        }

        tableStart = (ulong)sourceRanges;
        if (tableStart == 0)
        {
            return BootReservationSubtractionStatus
                .MissingSourceRanges;
        }

        if ((tableStart & (RequiredPointerAlignment - 1)) != 0)
        {
            return BootReservationSubtractionStatus
                .MisalignedSourceRanges;
        }

        ulong tableByteCount = sourceRangeCapacity * structureSize;
        if (tableStart > MaximumUnsignedLong - tableByteCount)
        {
            return BootReservationSubtractionStatus
                .SourceRangeTableAddressRangeOverflow;
        }

        tableEndExclusive = tableStart + tableByteCount;
        return BootReservationSubtractionStatus.Success;
    }

    private static BootReservationSubtractionStatus
        ValidateReservationStorage(
            BootReservedRange* reservations,
            ulong reservationCount,
            ulong reservationCapacity,
            out ulong tableStart,
            out ulong tableEndExclusive)
    {
        tableStart = 0;
        tableEndExclusive = 0;
        ulong structureSize = ActualReservationStructureSize;

        if (reservationCount
                > MaximumUnsignedLong / structureSize
            || reservationCapacity
                > MaximumUnsignedLong / structureSize)
        {
            return BootReservationSubtractionStatus
                .ReservationTableByteCountOverflow;
        }

        if (reservationCount > MaximumReservationCount)
        {
            return BootReservationSubtractionStatus
                .TooManyReservations;
        }

        if (reservationCapacity < reservationCount)
        {
            return BootReservationSubtractionStatus
                .ReservationCapacityTooSmall;
        }

        if (reservationCapacity == 0)
        {
            return BootReservationSubtractionStatus.Success;
        }

        tableStart = (ulong)reservations;
        if (tableStart == 0)
        {
            return BootReservationSubtractionStatus
                .MissingReservations;
        }

        if ((tableStart & (RequiredPointerAlignment - 1)) != 0)
        {
            return BootReservationSubtractionStatus
                .MisalignedReservations;
        }

        ulong tableByteCount = reservationCapacity * structureSize;
        if (tableStart > MaximumUnsignedLong - tableByteCount)
        {
            return BootReservationSubtractionStatus
                .ReservationTableAddressRangeOverflow;
        }

        tableEndExclusive = tableStart + tableByteCount;
        return BootReservationSubtractionStatus.Success;
    }

    private static BootReservationSubtractionStatus
        ValidateDestinationStorage(
            BootReservationSourceRange* destinationRanges,
            ulong destinationRangeCapacity,
            out ulong tableStart,
            out ulong tableEndExclusive)
    {
        tableStart = 0;
        tableEndExclusive = 0;
        ulong structureSize = ActualSourceRangeStructureSize;

        if (destinationRangeCapacity
            > MaximumUnsignedLong / structureSize)
        {
            return BootReservationSubtractionStatus
                .DestinationTableByteCountOverflow;
        }

        if (destinationRangeCapacity == 0)
        {
            return BootReservationSubtractionStatus.Success;
        }

        tableStart = (ulong)destinationRanges;
        if (tableStart == 0)
        {
            return BootReservationSubtractionStatus.MissingDestination;
        }

        if ((tableStart & (RequiredPointerAlignment - 1)) != 0)
        {
            return BootReservationSubtractionStatus
                .MisalignedDestination;
        }

        ulong tableByteCount =
            destinationRangeCapacity * structureSize;
        if (tableStart > MaximumUnsignedLong - tableByteCount)
        {
            return BootReservationSubtractionStatus
                .DestinationTableAddressRangeOverflow;
        }

        tableEndExclusive = tableStart + tableByteCount;
        return BootReservationSubtractionStatus.Success;
    }

    private static BootReservationSubtractionStatus
        ValidateSourceRanges(
            BootReservationSourceRange* sourceRanges,
            ulong sourceRangeCount,
            out BootReservationSourceRangeStatus sourceRangeStatus,
            out ulong invalidSourceRangeIndex,
            out ulong conflictingRangeIndex,
            out ulong totalSourcePages,
            out ulong totalSourceBytes)
    {
        sourceRangeStatus = BootReservationSourceRangeStatus.Success;
        invalidSourceRangeIndex = NoIndex;
        conflictingRangeIndex = NoIndex;
        totalSourcePages = 0;
        totalSourceBytes = 0;
        ulong previousStart = 0;
        ulong previousEndExclusive = 0;

        for (ulong sourceIndex = 0;
            sourceIndex < sourceRangeCount;
            sourceIndex++)
        {
            BootReservationSourceRange range =
                sourceRanges[sourceIndex];
            sourceRangeStatus = range.Validate();
            if (sourceRangeStatus
                != BootReservationSourceRangeStatus.Success)
            {
                invalidSourceRangeIndex = sourceIndex;
                return BootReservationSubtractionStatus
                    .InvalidSourceRange;
            }

            if (range.SourceRangeIndex != (int)sourceIndex)
            {
                invalidSourceRangeIndex = sourceIndex;
                return BootReservationSubtractionStatus
                    .UnorderedSourceProvenance;
            }

            if (!TryGetRawBounds(
                    range,
                    out ulong start,
                    out ulong endExclusive))
            {
                sourceRangeStatus =
                    BootReservationSourceRangeStatus
                        .InvalidPhysicalStart;
                invalidSourceRangeIndex = sourceIndex;
                return BootReservationSubtractionStatus
                    .InvalidSourceRange;
            }

            if (sourceIndex != 0)
            {
                if (start < previousStart)
                {
                    invalidSourceRangeIndex = sourceIndex;
                    conflictingRangeIndex = sourceIndex - 1;
                    return BootReservationSubtractionStatus
                        .UnsortedSourceRange;
                }

                if (start < previousEndExclusive)
                {
                    invalidSourceRangeIndex = sourceIndex;
                    conflictingRangeIndex = sourceIndex - 1;
                    return BootReservationSubtractionStatus
                        .OverlappingSourceRange;
                }
            }

            if (!TryAdd(
                    totalSourcePages,
                    range.PageCount,
                    out ulong nextTotalSourcePages))
            {
                invalidSourceRangeIndex = sourceIndex;
                return BootReservationSubtractionStatus
                    .TotalSourcePageCountOverflow;
            }

            if (!TryAdd(
                    totalSourceBytes,
                    range.ByteLength,
                    out ulong nextTotalSourceBytes))
            {
                invalidSourceRangeIndex = sourceIndex;
                return BootReservationSubtractionStatus
                    .TotalSourceByteCountOverflow;
            }

            totalSourcePages = nextTotalSourcePages;
            totalSourceBytes = nextTotalSourceBytes;
            previousStart = start;
            previousEndExclusive = endExclusive;
        }

        return BootReservationSubtractionStatus.Success;
    }

    private static BootReservationSubtractionStatus
        ValidateReservations(
            BootReservedRange* reservations,
            ulong reservationCount,
            out BootReservedRangeStatus reservationRangeStatus,
            out ulong invalidReservationIndex,
            out ulong conflictingRangeIndex,
            out ulong removedPages,
            out ulong removedBytes)
    {
        reservationRangeStatus = BootReservedRangeStatus.Success;
        invalidReservationIndex = NoIndex;
        conflictingRangeIndex = NoIndex;
        removedPages = 0;
        removedBytes = 0;
        ulong previousStart = 0;
        ulong previousEndExclusive = 0;

        for (ulong reservationIndex = 0;
            reservationIndex < reservationCount;
            reservationIndex++)
        {
            BootReservedRange reservation =
                reservations[reservationIndex];
            reservationRangeStatus = reservation.Validate();
            if (reservationRangeStatus != BootReservedRangeStatus.Success)
            {
                invalidReservationIndex = reservationIndex;
                return BootReservationSubtractionStatus
                    .InvalidReservation;
            }

            if (!TryGetRawBounds(
                    reservation,
                    out ulong start,
                    out ulong endExclusive))
            {
                reservationRangeStatus =
                    BootReservedRangeStatus.InvalidPhysicalStart;
                invalidReservationIndex = reservationIndex;
                return BootReservationSubtractionStatus
                    .InvalidReservation;
            }

            if (reservationIndex != 0)
            {
                if (start < previousStart)
                {
                    invalidReservationIndex = reservationIndex;
                    conflictingRangeIndex = reservationIndex - 1;
                    return BootReservationSubtractionStatus
                        .UnsortedReservation;
                }

                if (start < previousEndExclusive)
                {
                    invalidReservationIndex = reservationIndex;
                    conflictingRangeIndex = reservationIndex - 1;
                    return BootReservationSubtractionStatus
                        .OverlappingReservation;
                }
            }

            if (!TryAdd(
                    removedPages,
                    reservation.PageCount,
                    out ulong nextRemovedPages))
            {
                invalidReservationIndex = reservationIndex;
                return BootReservationSubtractionStatus
                    .RemovedPageCountOverflow;
            }

            if (!TryAdd(
                    removedBytes,
                    reservation.ByteLength,
                    out ulong nextRemovedBytes))
            {
                invalidReservationIndex = reservationIndex;
                return BootReservationSubtractionStatus
                    .RemovedByteCountOverflow;
            }

            removedPages = nextRemovedPages;
            removedBytes = nextRemovedBytes;
            previousStart = start;
            previousEndExclusive = endExclusive;
        }

        return BootReservationSubtractionStatus.Success;
    }

    private static BootReservationSubtractionStatus
        DryRunSubtraction(
            BootReservationSourceRange* sourceRanges,
            ulong sourceRangeCount,
            BootReservedRange* reservations,
            ulong reservationCount,
            out ulong requiredOutputRangeCount,
            out ulong remainingPages,
            out ulong remainingBytes,
            out BootReservationSourceRangeStatus sourceRangeStatus,
            out ulong invalidSourceRangeIndex,
            out ulong invalidReservationIndex,
            out ulong conflictingRangeIndex)
    {
        requiredOutputRangeCount = 0;
        remainingPages = 0;
        remainingBytes = 0;
        sourceRangeStatus = BootReservationSourceRangeStatus.Success;
        invalidSourceRangeIndex = NoIndex;
        invalidReservationIndex = NoIndex;
        conflictingRangeIndex = NoIndex;
        ulong reservationIndex = 0;

        for (ulong sourceIndex = 0;
            sourceIndex < sourceRangeCount;
            sourceIndex++)
        {
            BootReservationSourceRange source =
                sourceRanges[sourceIndex];
            TryGetRawBounds(
                source,
                out ulong sourceStart,
                out ulong sourceEndExclusive);
            ulong cursor = sourceStart;

            while (reservationIndex < reservationCount)
            {
                BootReservedRange reservation =
                    reservations[reservationIndex];
                TryGetRawBounds(
                    reservation,
                    out ulong reservationStart,
                    out ulong reservationEndExclusive);

                if (reservationStart < sourceStart)
                {
                    invalidReservationIndex = reservationIndex;
                    conflictingRangeIndex = sourceIndex;
                    return BootReservationSubtractionStatus
                        .ReservationOutsideSourceRange;
                }

                if (reservationStart >= sourceEndExclusive)
                {
                    break;
                }

                if (reservationEndExclusive > sourceEndExclusive)
                {
                    invalidReservationIndex = reservationIndex;
                    conflictingRangeIndex = sourceIndex;
                    return BootReservationSubtractionStatus
                        .ReservationOutsideSourceRange;
                }

                if (cursor < reservationStart)
                {
                    BootReservationSubtractionStatus fragmentStatus =
                        ValidateFragment(
                            source.SourceRangeIndex,
                            cursor,
                            reservationStart,
                            ref requiredOutputRangeCount,
                            ref remainingPages,
                            ref remainingBytes,
                            out sourceRangeStatus);
                    if (fragmentStatus
                        != BootReservationSubtractionStatus.Success)
                    {
                        invalidSourceRangeIndex = sourceIndex;
                        invalidReservationIndex = reservationIndex;
                        return fragmentStatus;
                    }
                }

                cursor = reservationEndExclusive;
                reservationIndex++;
            }

            if (cursor < sourceEndExclusive)
            {
                BootReservationSubtractionStatus fragmentStatus =
                    ValidateFragment(
                        source.SourceRangeIndex,
                        cursor,
                        sourceEndExclusive,
                        ref requiredOutputRangeCount,
                        ref remainingPages,
                        ref remainingBytes,
                        out sourceRangeStatus);
                if (fragmentStatus
                    != BootReservationSubtractionStatus.Success)
                {
                    invalidSourceRangeIndex = sourceIndex;
                    return fragmentStatus;
                }
            }
        }

        if (reservationIndex != reservationCount)
        {
            invalidReservationIndex = reservationIndex;
            return BootReservationSubtractionStatus
                .ReservationOutsideSourceRange;
        }

        return BootReservationSubtractionStatus.Success;
    }

    private static BootReservationSubtractionStatus ValidateFragment(
        int sourceRangeIndex,
        ulong start,
        ulong endExclusive,
        ref ulong requiredOutputRangeCount,
        ref ulong remainingPages,
        ref ulong remainingBytes,
        out BootReservationSourceRangeStatus sourceRangeStatus)
    {
        sourceRangeStatus = BootReservationSourceRangeStatus.Success;
        ulong byteLength = endExclusive - start;
        ulong pageCount = byteLength / RequiredPageSize;

        if (requiredOutputRangeCount >= MaximumOutputRangeCount)
        {
            return BootReservationSubtractionStatus
                .RequiredOutputRangeCountOverflow;
        }

        if (!TryAdd(
                remainingPages,
                pageCount,
                out ulong nextRemainingPages))
        {
            return BootReservationSubtractionStatus
                .RemainingPageCountOverflow;
        }

        if (!TryAdd(
                remainingBytes,
                byteLength,
                out ulong nextRemainingBytes))
        {
            return BootReservationSubtractionStatus
                .RemainingByteCountOverflow;
        }

        OS.Boot.AddressValueStatus startStatus =
            OS.Boot.PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                start,
                RequiredPageSize,
                out OS.Boot.PhysicalAddress typedStart);
        if (startStatus != OS.Boot.AddressValueStatus.Success)
        {
            sourceRangeStatus =
                BootReservationSourceRangeStatus.InvalidPhysicalStart;
            return BootReservationSubtractionStatus
                .OutputRangeConstructionFailed;
        }

        sourceRangeStatus = BootReservationSourceRange.TryCreate(
            sourceRangeIndex,
            typedStart,
            pageCount,
            out BootReservationSourceRange fragment);
        if (sourceRangeStatus
                != BootReservationSourceRangeStatus.Success
            || !fragment.IsValid)
        {
            return BootReservationSubtractionStatus
                .OutputRangeConstructionFailed;
        }

        if (!TryGetRawBounds(
                fragment,
                out ulong fragmentStart,
                out ulong fragmentEndExclusive)
            || fragmentStart != start
            || fragmentEndExclusive != endExclusive)
        {
            sourceRangeStatus =
                BootReservationSourceRangeStatus.PhysicalEndMismatch;
            return BootReservationSubtractionStatus
                .OutputRangeConstructionFailed;
        }

        requiredOutputRangeCount++;
        remainingPages = nextRemainingPages;
        remainingBytes = nextRemainingBytes;
        return BootReservationSubtractionStatus.Success;
    }

    private static ulong EmitRangesUnchecked(
        BootReservationSourceRange* sourceRanges,
        ulong sourceRangeCount,
        BootReservedRange* reservations,
        ulong reservationCount,
        BootReservationSourceRange* destinationRanges)
    {
        ulong writtenRangeCount = 0;
        ulong reservationIndex = 0;

        for (ulong sourceIndex = 0;
            sourceIndex < sourceRangeCount;
            sourceIndex++)
        {
            BootReservationSourceRange source =
                sourceRanges[sourceIndex];
            TryGetRawBounds(
                source,
                out ulong sourceStart,
                out ulong sourceEndExclusive);
            ulong cursor = sourceStart;
            OS.Boot.PhysicalAddress cursorAddress =
                source.PhysicalStart;

            while (reservationIndex < reservationCount)
            {
                BootReservedRange reservation =
                    reservations[reservationIndex];
                TryGetRawBounds(
                    reservation,
                    out ulong reservationStart,
                    out ulong reservationEndExclusive);

                if (reservationStart >= sourceEndExclusive)
                {
                    break;
                }

                if (cursor < reservationStart)
                {
                    WriteFragmentUnchecked(
                        source.SourceRangeIndex,
                        cursorAddress,
                        reservation.PhysicalStart,
                        cursor,
                        reservationStart,
                        destinationRanges,
                        writtenRangeCount);
                    writtenRangeCount++;
                }

                cursor = reservationEndExclusive;
                cursorAddress = reservation.PhysicalEndExclusive;
                reservationIndex++;
            }

            if (cursor < sourceEndExclusive)
            {
                WriteFragmentUnchecked(
                    source.SourceRangeIndex,
                    cursorAddress,
                    source.PhysicalEndExclusive,
                    cursor,
                    sourceEndExclusive,
                    destinationRanges,
                    writtenRangeCount);
                writtenRangeCount++;
            }
        }

        return writtenRangeCount;
    }

    private static void WriteFragmentUnchecked(
        int sourceRangeIndex,
        OS.Boot.PhysicalAddress physicalStart,
        OS.Boot.PhysicalAddress physicalEndExclusive,
        ulong rawStart,
        ulong rawEndExclusive,
        BootReservationSourceRange* destinationRanges,
        ulong destinationIndex)
    {
        ulong byteLength = rawEndExclusive - rawStart;
        ulong pageCount = byteLength / RequiredPageSize;
        destinationRanges[destinationIndex] =
            BootReservationSourceRange
                .CreateValidatedForSubtraction(
                    sourceRangeIndex,
                    physicalStart,
                    physicalEndExclusive,
                    pageCount,
                    byteLength);
    }

    private static bool AccountingIsExact(
        ulong totalSourcePages,
        ulong totalSourceBytes,
        ulong removedPages,
        ulong removedBytes,
        ulong remainingPages,
        ulong remainingBytes)
    {
        return totalSourcePages != 0
            && removedPages <= totalSourcePages
            && remainingPages == totalSourcePages - removedPages
            && removedBytes <= totalSourceBytes
            && remainingBytes == totalSourceBytes - removedBytes
            && totalSourcePages
                <= MaximumUnsignedLong / RequiredPageSize
            && removedPages
                <= MaximumUnsignedLong / RequiredPageSize
            && remainingPages
                <= MaximumUnsignedLong / RequiredPageSize
            && totalSourcePages * RequiredPageSize
                == totalSourceBytes
            && removedPages * RequiredPageSize == removedBytes
            && remainingPages * RequiredPageSize == remainingBytes;
    }

    private static bool TryGetRawBounds(
        BootReservationSourceRange range,
        out ulong start,
        out ulong endExclusive)
    {
        start = 0;
        endExclusive = 0;
        return range.PhysicalStart
                .TryGetRawValueForUefiBoundary(out start)
                == OS.Boot.AddressValueStatus.Success
            && range.PhysicalEndExclusive
                .TryGetRawValueForUefiBoundary(out endExclusive)
                == OS.Boot.AddressValueStatus.Success;
    }

    private static bool TryGetRawBounds(
        BootReservedRange range,
        out ulong start,
        out ulong endExclusive)
    {
        start = 0;
        endExclusive = 0;
        return range.PhysicalStart
                .TryGetRawValueForUefiBoundary(out start)
                == OS.Boot.AddressValueStatus.Success
            && range.PhysicalEndExclusive
                .TryGetRawValueForUefiBoundary(out endExclusive)
                == OS.Boot.AddressValueStatus.Success;
    }

    private static bool StorageRangesOverlap(
        ulong firstStart,
        ulong firstEndExclusive,
        ulong secondStart,
        ulong secondEndExclusive)
    {
        return firstStart != firstEndExclusive
            && secondStart != secondEndExclusive
            && firstStart < secondEndExclusive
            && secondStart < firstEndExclusive;
    }

    private static bool TryAdd(
        ulong left,
        ulong right,
        out ulong value)
    {
        if (left > MaximumUnsignedLong - right)
        {
            value = 0;
            return false;
        }

        value = left + right;
        return true;
    }

    private static BootReservationSubtractionResult Fail(
        BootReservationSubtractionStatus status,
        BootReservationSourceRangeStatus sourceRangeStatus,
        BootReservedRangeStatus reservationRangeStatus,
        ulong sourceRangeCount,
        ulong sourceRangeCapacity,
        ulong reservationCount,
        ulong reservationCapacity,
        ulong requiredOutputRangeCount,
        ulong availableDestinationCapacity,
        ulong invalidSourceRangeIndex,
        ulong invalidReservationIndex,
        ulong conflictingRangeIndex,
        ulong pageSize,
        ulong totalSourcePages,
        ulong totalSourceBytes,
        ulong removedPages,
        ulong removedBytes,
        ulong remainingPages,
        ulong remainingBytes)
    {
        return BootReservationSubtractionResult.Fail(
            status,
            sourceRangeStatus,
            reservationRangeStatus,
            sourceRangeCount,
            sourceRangeCapacity,
            reservationCount,
            reservationCapacity,
            requiredOutputRangeCount,
            availableDestinationCapacity,
            invalidSourceRangeIndex,
            invalidReservationIndex,
            conflictingRangeIndex,
            pageSize,
            totalSourcePages,
            totalSourceBytes,
            removedPages,
            removedBytes,
            remainingPages,
            remainingBytes);
    }
}
