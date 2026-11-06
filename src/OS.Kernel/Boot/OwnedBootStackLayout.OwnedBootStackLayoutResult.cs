namespace OS.Boot;
/// <summary>
/// Carries deterministic value-only evidence for layout construction or one
/// immutable lifetime transition.
/// </summary>

public readonly struct OwnedBootStackLayoutResult
{
    private readonly OwnedBootStackLayoutStatus _status;
    private readonly AddressValueStatus _addressStatus;
    private readonly OS.Kernel.Memory.BootReservationTableStatus _reservationTableStatus;
    private readonly OS.Kernel.Memory.BootReservedRangeStatus _rangeStatus;
    private readonly OwnedBootStackLifetime _previousLifetime;
    private readonly OwnedBootStackLifetime _requestedLifetime;
    private readonly OwnedBootStackLifetime _currentLifetime;
    private readonly byte _reserved0;
    private readonly ulong _reservationCount;
    private readonly ulong _validatedReservationCount;
    private readonly ulong _availableReservationCapacity;
    private readonly ulong _invalidReservationIndex;
    private readonly ulong _conflictingReservationIndex;
    private readonly OwnedBootStackLayout _layout;
    private OwnedBootStackLayoutResult(OwnedBootStackLayoutStatus status, AddressValueStatus addressStatus, OS.Kernel.Memory.BootReservationTableStatus reservationTableStatus, OS.Kernel.Memory.BootReservedRangeStatus rangeStatus, OwnedBootStackLifetime previousLifetime, OwnedBootStackLifetime requestedLifetime, OwnedBootStackLifetime currentLifetime, ulong reservationCount, ulong validatedReservationCount, ulong availableReservationCapacity, ulong invalidReservationIndex, ulong conflictingReservationIndex, OwnedBootStackLayout layout)
    {
        _status = status;
        _addressStatus = addressStatus;
        _reservationTableStatus = reservationTableStatus;
        _rangeStatus = rangeStatus;
        _previousLifetime = previousLifetime;
        _requestedLifetime = requestedLifetime;
        _currentLifetime = currentLifetime;
        _reserved0 = 0;
        _reservationCount = reservationCount;
        _validatedReservationCount = validatedReservationCount;
        _availableReservationCapacity = availableReservationCapacity;
        _invalidReservationIndex = invalidReservationIndex;
        _conflictingReservationIndex = conflictingReservationIndex;
        _layout = layout;
    }

    public OwnedBootStackLayoutStatus Status => _status;
    public AddressValueStatus AddressStatus => _addressStatus;
    public OS.Kernel.Memory.BootReservationTableStatus ReservationTableStatus => _reservationTableStatus;
    public OS.Kernel.Memory.BootReservedRangeStatus RangeStatus => _rangeStatus;
    public OwnedBootStackLifetime PreviousLifetime => _previousLifetime;
    public OwnedBootStackLifetime RequestedLifetime => _requestedLifetime;
    public OwnedBootStackLifetime CurrentLifetime => _currentLifetime;
    public bool Succeeded => Validate();
    public ulong ReservationCount => _reservationCount;
    public ulong ValidatedReservationCount => _validatedReservationCount;
    public ulong AvailableReservationCapacity => _availableReservationCapacity;
    public ulong RequiredReservationCapacity => OwnedBootStackLayout.RequiredReservationCount;
    public ulong InvalidReservationIndex => _invalidReservationIndex;
    public ulong ConflictingReservationIndex => _conflictingReservationIndex;
    public OwnedBootStackLayout Layout => _layout;

    public bool Equals(OwnedBootStackLayoutResult other)
    {
        return _status == other._status && _addressStatus == other._addressStatus && _reservationTableStatus == other._reservationTableStatus && _rangeStatus == other._rangeStatus && _previousLifetime == other._previousLifetime && _requestedLifetime == other._requestedLifetime && _currentLifetime == other._currentLifetime && _reserved0 == other._reserved0 && _reservationCount == other._reservationCount && _validatedReservationCount == other._validatedReservationCount && _availableReservationCapacity == other._availableReservationCapacity && _invalidReservationIndex == other._invalidReservationIndex && _conflictingReservationIndex == other._conflictingReservationIndex && _layout.Equals(other._layout);
    }

    internal static OwnedBootStackLayoutResult Create(OwnedBootStackLayoutStatus status, AddressValueStatus addressStatus, OS.Kernel.Memory.BootReservationTableStatus reservationTableStatus, OS.Kernel.Memory.BootReservedRangeStatus rangeStatus, OwnedBootStackLifetime previousLifetime, OwnedBootStackLifetime requestedLifetime, OwnedBootStackLifetime currentLifetime, ulong reservationCount, ulong validatedReservationCount, ulong availableReservationCapacity, ulong invalidReservationIndex, ulong conflictingReservationIndex, OwnedBootStackLayout layout)
    {
        return new OwnedBootStackLayoutResult(status, addressStatus, reservationTableStatus, rangeStatus, previousLifetime, requestedLifetime, currentLifetime, reservationCount, validatedReservationCount, availableReservationCapacity, invalidReservationIndex, conflictingReservationIndex, layout);
    }

    internal bool Validate()
    {
        if (_status != OwnedBootStackLayoutStatus.Success || _addressStatus != AddressValueStatus.Success || _reserved0 != 0 || _invalidReservationIndex != OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex || _conflictingReservationIndex != OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex || !_layout.Validate() || _currentLifetime != _layout.Lifetime || _requestedLifetime != _currentLifetime)
        {
            return false;
        }

        bool creation = _previousLifetime == OwnedBootStackLifetime.Uninitialized && _currentLifetime == OwnedBootStackLifetime.Reserved && _reservationTableStatus == OS.Kernel.Memory.BootReservationTableStatus.Success && _rangeStatus == OS.Kernel.Memory.BootReservedRangeStatus.Success && _reservationCount == OwnedBootStackLayout.RequiredReservationCount && _validatedReservationCount == OwnedBootStackLayout.RequiredReservationCount && _availableReservationCapacity >= OwnedBootStackLayout.RequiredReservationCount;
        bool transition = (_previousLifetime == OwnedBootStackLifetime.Reserved && _currentLifetime == OwnedBootStackLifetime.Ready || _previousLifetime == OwnedBootStackLifetime.Ready && _currentLifetime == OwnedBootStackLifetime.Active) && _reservationTableStatus == OS.Kernel.Memory.BootReservationTableStatus.Uninitialized && _rangeStatus == OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized && _reservationCount == 0 && _validatedReservationCount == 0 && _availableReservationCapacity == 0;
        return creation || transition;
    }
}
