namespace OS.Kernel.Memory;
public readonly struct BootReservationSubtractionResult
{
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private BootReservationSubtractionResult(BootReservationSubtractionStatus status, BootReservationSourceRangeStatus sourceRangeStatus, BootReservedRangeStatus reservationRangeStatus, ulong sourceRangeCount, ulong sourceRangeCapacity, ulong reservationCount, ulong reservationCapacity, ulong requiredOutputRangeCount, ulong writtenOutputRangeCount, ulong availableDestinationCapacity, ulong invalidSourceRangeIndex, ulong invalidReservationIndex, ulong conflictingRangeIndex, ulong pageSize, ulong totalSourcePages, ulong totalSourceBytes, ulong removedPages, ulong removedBytes, ulong remainingPages, ulong remainingBytes)
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
        AvailableDestinationCapacity = availableDestinationCapacity;
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
    public bool Succeeded => Status == BootReservationSubtractionStatus.Success && SourceRangeStatus == BootReservationSourceRangeStatus.Success && ReservationRangeStatus == BootReservedRangeStatus.Success && ReservedFieldsAreZero() && SourceRangeCount != 0 && SourceRangeCount <= BootReservationSubtractor.MaximumSourceRangeCount && SourceRangeCapacity >= SourceRangeCount && ReservationCount <= BootReservationSubtractor.MaximumReservationCount && ReservationCapacity >= ReservationCount && RequiredOutputRangeCount <= BootReservationSubtractor.MaximumOutputRangeCount && WrittenOutputRangeCount == RequiredOutputRangeCount && AvailableDestinationCapacity >= RequiredOutputRangeCount && InvalidSourceRangeIndex == BootReservationSubtractor.NoIndex && InvalidReservationIndex == BootReservationSubtractor.NoIndex && ConflictingRangeIndex == BootReservationSubtractor.NoIndex && PageSize == BootReservationSubtractor.RequiredPageSize && TotalSourcePages != 0 && BootReservationSubtractor.IsRepresentableSuccessSummary(SourceRangeCount, SourceRangeCapacity, ReservationCount, ReservationCapacity, RequiredOutputRangeCount, AvailableDestinationCapacity, PageSize, TotalSourcePages, TotalSourceBytes, RemovedPages, RemovedBytes, RemainingPages, RemainingBytes) && (ReservationCount != 0 || (RemovedPages == 0 && RemovedBytes == 0)) && (ReservationCount == 0 || (RemovedPages != 0 && RemovedBytes != 0)) && (RemainingPages != 0 || (RemainingBytes == 0 && RequiredOutputRangeCount == 0)) && (RemainingPages == 0 || (RemainingBytes != 0 && RequiredOutputRangeCount != 0));
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

    internal static BootReservationSubtractionResult Success(ulong sourceRangeCount, ulong sourceRangeCapacity, ulong reservationCount, ulong reservationCapacity, ulong requiredOutputRangeCount, ulong writtenOutputRangeCount, ulong availableDestinationCapacity, ulong pageSize, ulong totalSourcePages, ulong totalSourceBytes, ulong removedPages, ulong removedBytes, ulong remainingPages, ulong remainingBytes)
    {
        return new BootReservationSubtractionResult(BootReservationSubtractionStatus.Success, BootReservationSourceRangeStatus.Success, BootReservedRangeStatus.Success, sourceRangeCount, sourceRangeCapacity, reservationCount, reservationCapacity, requiredOutputRangeCount, writtenOutputRangeCount, availableDestinationCapacity, BootReservationSubtractor.NoIndex, BootReservationSubtractor.NoIndex, BootReservationSubtractor.NoIndex, pageSize, totalSourcePages, totalSourceBytes, removedPages, removedBytes, remainingPages, remainingBytes);
    }

    internal static BootReservationSubtractionResult Fail(BootReservationSubtractionStatus status, BootReservationSourceRangeStatus sourceRangeStatus, BootReservedRangeStatus reservationRangeStatus, ulong sourceRangeCount, ulong sourceRangeCapacity, ulong reservationCount, ulong reservationCapacity, ulong requiredOutputRangeCount, ulong availableDestinationCapacity, ulong invalidSourceRangeIndex, ulong invalidReservationIndex, ulong conflictingRangeIndex, ulong pageSize, ulong totalSourcePages, ulong totalSourceBytes, ulong removedPages, ulong removedBytes, ulong remainingPages, ulong remainingBytes)
    {
        return new BootReservationSubtractionResult(status, sourceRangeStatus, reservationRangeStatus, sourceRangeCount, sourceRangeCapacity, reservationCount, reservationCapacity, requiredOutputRangeCount, writtenOutputRangeCount: 0, availableDestinationCapacity, invalidSourceRangeIndex, invalidReservationIndex, conflictingRangeIndex, pageSize, totalSourcePages, totalSourceBytes, removedPages, removedBytes, remainingPages, remainingBytes);
    }

    private bool ReservedFieldsAreZero()
    {
        return _reserved0 == 0 && _reserved1 == 0 && _reserved2 == 0 && _reserved3 == 0 && _reserved4 == 0;
    }
}
