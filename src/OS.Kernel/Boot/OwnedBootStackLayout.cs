namespace OS.Boot;
/// <summary>
/// Describes one checked physical boot-stack arena. The guard and stack
/// reservations exactly partition the caller-declared backing range.
/// Lifetime values are caller attestations; this value never allocates,
/// maps, protects, clears, activates, or reclaims a page.
/// </summary>

public readonly struct OwnedBootStackLayout
{
    public const ulong RequiredReservationCount = 2;
    public const ulong X64StackAlignmentBytes = 16;
    private const ulong MaximumUnsignedLong = ~0UL;
    private const ulong RequiredTableAlignment = 8;
    private readonly PhysicalAddress _backingStart;
    private readonly PhysicalAddress _backingEndExclusive;
    private readonly ulong _backingPageCount;
    private readonly ulong _backingByteLength;
    private readonly OS.Kernel.Memory.BootReservedRange _guardReservation;
    private readonly OS.Kernel.Memory.BootReservedRange _stackReservation;
    private readonly PhysicalAddress _physicalStackTop;
    private readonly OwnedBootStackLifetime _lifetime;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly byte _reserved6;
    private OwnedBootStackLayout(PhysicalAddress backingStart, PhysicalAddress backingEndExclusive, ulong backingPageCount, ulong backingByteLength, OS.Kernel.Memory.BootReservedRange guardReservation, OS.Kernel.Memory.BootReservedRange stackReservation, PhysicalAddress physicalStackTop, OwnedBootStackLifetime lifetime)
    {
        _backingStart = backingStart;
        _backingEndExclusive = backingEndExclusive;
        _backingPageCount = backingPageCount;
        _backingByteLength = backingByteLength;
        _guardReservation = guardReservation;
        _stackReservation = stackReservation;
        _physicalStackTop = physicalStackTop;
        _lifetime = lifetime;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _reserved5 = 0;
        _reserved6 = 0;
    }

    public PhysicalAddress BackingStart => _backingStart;
    public PhysicalAddress BackingEndExclusive => _backingEndExclusive;
    public ulong BackingPageCount => _backingPageCount;
    public ulong BackingByteLength => _backingByteLength;
    public OS.Kernel.Memory.BootReservedRange GuardReservation => _guardReservation;
    public OS.Kernel.Memory.BootReservedRange StackReservation => _stackReservation;
    public PhysicalAddress PhysicalStackTop => _physicalStackTop;
    public OwnedBootStackLifetime Lifetime => _lifetime;
    public bool IsValid => Validate();

    public static unsafe OwnedBootStackLayoutResult Create(OS.Kernel.Memory.BootReservedRange* reservations, ulong reservationCount, ulong reservationCapacity, PhysicalAddress backingStart, ulong backingPageCount, PhysicalAddress physicalStackTop)
    {
        if (reservationCount < RequiredReservationCount)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.InvalidReservationCount, AddressValueStatus.Uninitialized, reservationCount, reservationCapacity);
        }

        if (reservationCount > RequiredReservationCount)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.TooManyReservations, AddressValueStatus.Uninitialized, reservationCount, reservationCapacity);
        }

        if (reservationCapacity < reservationCount)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.ReservationCapacityTooSmall, AddressValueStatus.Uninitialized, reservationCount, reservationCapacity);
        }

        if (reservations == null)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.MissingReservationTable, AddressValueStatus.Uninitialized, reservationCount, reservationCapacity);
        }

        ulong tableAddress = (ulong)reservations;
        if ((tableAddress & (RequiredTableAlignment - 1)) != 0)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.MisalignedReservationTable, AddressValueStatus.MisalignedAddress, reservationCount, reservationCapacity);
        }

        ulong tableByteLength = reservationCount * (ulong)sizeof(OS.Kernel.Memory.BootReservedRange);
        if (tableAddress > MaximumUnsignedLong - tableByteLength)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.ReservationTableAddressRangeOverflow, AddressValueStatus.AddressOverflow, reservationCount, reservationCapacity);
        }

        if (!backingStart.IsValid)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.InvalidBackingStart, AddressValueStatus.InvalidAddress, reservationCount, reservationCapacity);
        }

        if (!backingStart.IsAligned(OS.Kernel.Memory.BootReservedRange.PageSize))
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.MisalignedBackingStart, AddressValueStatus.MisalignedAddress, reservationCount, reservationCapacity);
        }

        if (backingPageCount == 0)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.InvalidBackingPageCount, AddressValueStatus.ZeroLength, reservationCount, reservationCapacity);
        }

        if (backingPageCount < RequiredReservationCount)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.InvalidBackingPageCount, AddressValueStatus.Uninitialized, reservationCount, reservationCapacity);
        }

        if (backingPageCount > MaximumUnsignedLong / OS.Kernel.Memory.BootReservedRange.PageSize)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.BackingByteCountOverflow, AddressValueStatus.AddressOverflow, reservationCount, reservationCapacity);
        }

        ulong backingByteLength = backingPageCount * OS.Kernel.Memory.BootReservedRange.PageSize;
        AddressValueStatus backingEndStatus = backingStart.TryGetEndExclusive(backingByteLength, out PhysicalAddress backingEndExclusive);
        if (backingEndStatus != AddressValueStatus.Success)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.BackingEndOverflow, backingEndStatus, reservationCount, reservationCapacity);
        }

        if (!physicalStackTop.IsValid)
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.InvalidStackTop, AddressValueStatus.InvalidAddress, reservationCount, reservationCapacity);
        }

        if (!physicalStackTop.IsAligned(X64StackAlignmentBytes))
        {
            return FailBeforeTable(OwnedBootStackLayoutStatus.MisalignedStackTop, AddressValueStatus.MisalignedAddress, reservationCount, reservationCapacity);
        }

        OS.Kernel.Memory.BootReservationTableValidationResult tableResult = OS.Kernel.Memory.BootReservationTableValidator.Validate(reservations, reservationCount, reservationCapacity);
        if (!tableResult.Succeeded)
        {
            OwnedBootStackLayoutStatus tableFailureStatus = tableResult.Status == OS.Kernel.Memory.BootReservationTableStatus.MissingBootStackGuard ? OwnedBootStackLayoutStatus.MissingGuardReservation : tableResult.Status == OS.Kernel.Memory.BootReservationTableStatus.MissingBootStack ? OwnedBootStackLayoutStatus.MissingStackReservation : OwnedBootStackLayoutStatus.ReservationTableRejected;
            return FailWithTable(tableFailureStatus, AddressValueStatus.Uninitialized, tableResult, reservationCapacity);
        }

        if (tableResult.BootStackGuardIndex == OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex)
        {
            return FailWithTable(OwnedBootStackLayoutStatus.MissingGuardReservation, AddressValueStatus.Uninitialized, tableResult, reservationCapacity);
        }

        if (tableResult.BootStackIndex == OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex)
        {
            return FailWithTable(OwnedBootStackLayoutStatus.MissingStackReservation, AddressValueStatus.Uninitialized, tableResult, reservationCapacity);
        }

        OS.Kernel.Memory.BootReservedRange guardReservation = reservations[tableResult.BootStackGuardIndex];
        OS.Kernel.Memory.BootReservedRange stackReservation = reservations[tableResult.BootStackIndex];
        if (!guardReservation.PhysicalStart.Equals(backingStart) || !stackReservation.PhysicalEndExclusive.Equals(backingEndExclusive) || tableResult.BootStackArenaPages != backingPageCount || tableResult.BootStackArenaBytes != backingByteLength)
        {
            return FailWithTable(OwnedBootStackLayoutStatus.BackingArenaMismatch, AddressValueStatus.InvalidAddress, tableResult, reservationCapacity);
        }

        if (!stackReservation.PhysicalEndExclusive.Equals(physicalStackTop))
        {
            return FailWithTable(OwnedBootStackLayoutStatus.StackTopMismatch, AddressValueStatus.InvalidAddress, tableResult, reservationCapacity);
        }

        OwnedBootStackLayout layout = new OwnedBootStackLayout(backingStart, backingEndExclusive, backingPageCount, backingByteLength, guardReservation, stackReservation, physicalStackTop, OwnedBootStackLifetime.Reserved);
        if (!layout.Validate())
        {
            return FailWithTable(OwnedBootStackLayoutStatus.InvalidLayout, AddressValueStatus.InvalidAddress, tableResult, reservationCapacity);
        }

        return OwnedBootStackLayoutResult.Create(OwnedBootStackLayoutStatus.Success, AddressValueStatus.Success, tableResult.Status, tableResult.RangeStatus, OwnedBootStackLifetime.Uninitialized, OwnedBootStackLifetime.Reserved, OwnedBootStackLifetime.Reserved, reservationCount, tableResult.ValidatedReservationCount, reservationCapacity, tableResult.InvalidReservationIndex, tableResult.ConflictingReservationIndex, layout);
    }

    public OwnedBootStackLayoutResult TransitionTo(OwnedBootStackLifetime requestedLifetime)
    {
        if (!Validate())
        {
            return TransitionFailure(OwnedBootStackLayoutStatus.InvalidLayout, requestedLifetime);
        }

        if (requestedLifetime != OwnedBootStackLifetime.Reserved && requestedLifetime != OwnedBootStackLifetime.Ready && requestedLifetime != OwnedBootStackLifetime.Active)
        {
            return TransitionFailure(OwnedBootStackLayoutStatus.InvalidLifetime, requestedLifetime);
        }

        if (requestedLifetime == _lifetime)
        {
            return TransitionFailure(OwnedBootStackLayoutStatus.RepeatedLifetimeTransition, requestedLifetime);
        }

        if ((_lifetime != OwnedBootStackLifetime.Reserved || requestedLifetime != OwnedBootStackLifetime.Ready) && (_lifetime != OwnedBootStackLifetime.Ready || requestedLifetime != OwnedBootStackLifetime.Active))
        {
            return TransitionFailure(OwnedBootStackLayoutStatus.IllegalLifetimeTransition, requestedLifetime);
        }

        OwnedBootStackLayout transitioned = WithLifetime(requestedLifetime);
        return OwnedBootStackLayoutResult.Create(OwnedBootStackLayoutStatus.Success, AddressValueStatus.Success, OS.Kernel.Memory.BootReservationTableStatus.Uninitialized, OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized, _lifetime, requestedLifetime, requestedLifetime, 0, 0, 0, OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex, OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex, transitioned);
    }

    public bool Equals(OwnedBootStackLayout other)
    {
        return _backingStart.Equals(other._backingStart) && _backingEndExclusive.Equals(other._backingEndExclusive) && _backingPageCount == other._backingPageCount && _backingByteLength == other._backingByteLength && _guardReservation.Equals(other._guardReservation) && _stackReservation.Equals(other._stackReservation) && _physicalStackTop.Equals(other._physicalStackTop) && _lifetime == other._lifetime && _reserved0 == other._reserved0 && _reserved1 == other._reserved1 && _reserved2 == other._reserved2 && _reserved3 == other._reserved3 && _reserved4 == other._reserved4 && _reserved5 == other._reserved5 && _reserved6 == other._reserved6;
    }

    internal bool Validate()
    {
        if (!_backingStart.IsValid || !_backingStart.IsAligned(OS.Kernel.Memory.BootReservedRange.PageSize) || !_backingEndExclusive.IsValid || !_backingEndExclusive.IsAligned(OS.Kernel.Memory.BootReservedRange.PageSize) || _backingPageCount < RequiredReservationCount || _backingPageCount > MaximumUnsignedLong / OS.Kernel.Memory.BootReservedRange.PageSize || _backingByteLength != _backingPageCount * OS.Kernel.Memory.BootReservedRange.PageSize || !_guardReservation.IsValid || _guardReservation.Kind != OS.Kernel.Memory.BootReservationKind.BootStackGuard || !_stackReservation.IsValid || _stackReservation.Kind != OS.Kernel.Memory.BootReservationKind.BootStack || !_physicalStackTop.IsValid || !_physicalStackTop.IsAligned(X64StackAlignmentBytes) || (_lifetime != OwnedBootStackLifetime.Reserved && _lifetime != OwnedBootStackLifetime.Ready && _lifetime != OwnedBootStackLifetime.Active) || _reserved0 != 0 || _reserved1 != 0 || _reserved2 != 0 || _reserved3 != 0 || _reserved4 != 0 || _reserved5 != 0 || _reserved6 != 0)
        {
            return false;
        }

        if (_guardReservation.PageCount > MaximumUnsignedLong - _stackReservation.PageCount || _guardReservation.ByteLength > MaximumUnsignedLong - _stackReservation.ByteLength)
        {
            return false;
        }

        ulong arenaPages = _guardReservation.PageCount + _stackReservation.PageCount;
        ulong arenaBytes = _guardReservation.ByteLength + _stackReservation.ByteLength;
        AddressValueStatus backingEndStatus = _backingStart.TryGetEndExclusive(_backingByteLength, out PhysicalAddress expectedBackingEnd);
        return backingEndStatus == AddressValueStatus.Success && expectedBackingEnd.Equals(_backingEndExclusive) && arenaPages == _backingPageCount && arenaBytes == _backingByteLength && _guardReservation.PhysicalStart.Equals(_backingStart) && _guardReservation.PhysicalEndExclusive.Equals(_stackReservation.PhysicalStart) && _stackReservation.PhysicalEndExclusive.Equals(_backingEndExclusive) && _stackReservation.PhysicalEndExclusive.Equals(_physicalStackTop);
    }

    private OwnedBootStackLayout WithLifetime(OwnedBootStackLifetime lifetime)
    {
        return new OwnedBootStackLayout(_backingStart, _backingEndExclusive, _backingPageCount, _backingByteLength, _guardReservation, _stackReservation, _physicalStackTop, lifetime);
    }

    private OwnedBootStackLayoutResult TransitionFailure(OwnedBootStackLayoutStatus status, OwnedBootStackLifetime requestedLifetime)
    {
        return OwnedBootStackLayoutResult.Create(status, AddressValueStatus.Uninitialized, OS.Kernel.Memory.BootReservationTableStatus.Uninitialized, OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized, _lifetime, requestedLifetime, _lifetime, 0, 0, 0, OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex, OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex, this);
    }

    private static OwnedBootStackLayoutResult FailBeforeTable(OwnedBootStackLayoutStatus status, AddressValueStatus addressStatus, ulong reservationCount, ulong reservationCapacity)
    {
        return OwnedBootStackLayoutResult.Create(status, addressStatus, OS.Kernel.Memory.BootReservationTableStatus.Uninitialized, OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized, OwnedBootStackLifetime.Uninitialized, OwnedBootStackLifetime.Reserved, OwnedBootStackLifetime.Uninitialized, reservationCount, 0, reservationCapacity, OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex, OS.Kernel.Memory.BootReservationTableValidationResult.NoIndex, default);
    }

    private static OwnedBootStackLayoutResult FailWithTable(OwnedBootStackLayoutStatus status, AddressValueStatus addressStatus, OS.Kernel.Memory.BootReservationTableValidationResult tableResult, ulong reservationCapacity)
    {
        return OwnedBootStackLayoutResult.Create(status, addressStatus, tableResult.Status, tableResult.RangeStatus, OwnedBootStackLifetime.Uninitialized, OwnedBootStackLifetime.Reserved, OwnedBootStackLifetime.Uninitialized, tableResult.ReservationCount, tableResult.ValidatedReservationCount, reservationCapacity, tableResult.InvalidReservationIndex, tableResult.ConflictingReservationIndex, default);
    }
}
