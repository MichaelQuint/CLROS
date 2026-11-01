namespace OS.Kernel.Memory;
/// <summary>
/// Carries deterministic value-only evidence from reservation-table validation.
/// </summary>

public readonly struct BootReservationTableValidationResult
{
    public const ulong NoIndex = ~0UL;
    private readonly BootReservationTableStatus _status;
    private readonly BootReservedRangeStatus _rangeStatus;
    private readonly ulong _reservationCount;
    private readonly ulong _validatedReservationCount;
    private readonly ulong _requiredReservationCapacity;
    private readonly ulong _availableReservationCapacity;
    private readonly ulong _invalidReservationIndex;
    private readonly ulong _conflictingReservationIndex;
    private readonly ulong _firstLoadedImageIndex;
    private readonly ulong _bootStackGuardIndex;
    private readonly ulong _bootStackIndex;
    private readonly ulong _totalPages;
    private readonly ulong _totalBytes;
    private readonly ulong _bootStackArenaPages;
    private readonly ulong _bootStackArenaBytes;
    private BootReservationTableValidationResult(BootReservationTableStatus status, BootReservedRangeStatus rangeStatus, ulong reservationCount, ulong validatedReservationCount, ulong availableReservationCapacity, ulong invalidReservationIndex, ulong conflictingReservationIndex, ulong firstLoadedImageIndex, ulong bootStackGuardIndex, ulong bootStackIndex, ulong totalPages, ulong totalBytes, ulong bootStackArenaPages, ulong bootStackArenaBytes)
    {
        _status = status;
        _rangeStatus = rangeStatus;
        _reservationCount = reservationCount;
        _validatedReservationCount = validatedReservationCount;
        _requiredReservationCapacity = reservationCount;
        _availableReservationCapacity = availableReservationCapacity;
        _invalidReservationIndex = invalidReservationIndex;
        _conflictingReservationIndex = conflictingReservationIndex;
        _firstLoadedImageIndex = firstLoadedImageIndex;
        _bootStackGuardIndex = bootStackGuardIndex;
        _bootStackIndex = bootStackIndex;
        _totalPages = totalPages;
        _totalBytes = totalBytes;
        _bootStackArenaPages = bootStackArenaPages;
        _bootStackArenaBytes = bootStackArenaBytes;
    }

    public BootReservationTableStatus Status => _status;
    public BootReservedRangeStatus RangeStatus => _rangeStatus;
    public bool Succeeded => _status == BootReservationTableStatus.Success;
    public ulong ReservationCount => _reservationCount;
    public ulong ValidatedReservationCount => _validatedReservationCount;
    public ulong RequiredReservationCapacity => _requiredReservationCapacity;
    public ulong AvailableReservationCapacity => _availableReservationCapacity;
    public ulong InvalidReservationIndex => _invalidReservationIndex;
    public ulong ConflictingReservationIndex => _conflictingReservationIndex;
    public ulong FirstLoadedImageIndex => _firstLoadedImageIndex;
    public ulong BootStackGuardIndex => _bootStackGuardIndex;
    public ulong BootStackIndex => _bootStackIndex;
    public ulong TotalPages => _totalPages;
    public ulong TotalBytes => _totalBytes;
    public ulong BootStackArenaPages => _bootStackArenaPages;
    public ulong BootStackArenaBytes => _bootStackArenaBytes;

    internal static BootReservationTableValidationResult Create(BootReservationTableStatus status, BootReservedRangeStatus rangeStatus, ulong reservationCount, ulong validatedReservationCount, ulong availableReservationCapacity, ulong invalidReservationIndex, ulong conflictingReservationIndex, ulong firstLoadedImageIndex, ulong bootStackGuardIndex, ulong bootStackIndex, ulong totalPages, ulong totalBytes, ulong bootStackArenaPages, ulong bootStackArenaBytes)
    {
        return new BootReservationTableValidationResult(status, rangeStatus, reservationCount, validatedReservationCount, availableReservationCapacity, invalidReservationIndex, conflictingReservationIndex, firstLoadedImageIndex, bootStackGuardIndex, bootStackIndex, totalPages, totalBytes, bootStackArenaPages, bootStackArenaBytes);
    }
}
