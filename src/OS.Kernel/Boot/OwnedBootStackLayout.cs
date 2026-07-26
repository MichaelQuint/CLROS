namespace OS.Boot;

/// <summary>
/// Identifies the caller-attested lifetime of one owned boot-stack layout.
/// Existing values are append-only and the zero-valued default is invalid.
/// </summary>
public enum OwnedBootStackLifetime : byte
{
    Uninitialized = 0,
    Reserved = 1,
    Ready = 2,
    Active = 3
}

/// <summary>
/// Reports construction or lifetime transition of an owned boot-stack layout.
/// Existing values are append-only and the zero-valued default is not success.
/// </summary>
public enum OwnedBootStackLayoutStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidReservationCount = 2,
    TooManyReservations = 3,
    ReservationCapacityTooSmall = 4,
    MissingReservationTable = 5,
    MisalignedReservationTable = 6,
    ReservationTableAddressRangeOverflow = 7,
    InvalidBackingStart = 8,
    MisalignedBackingStart = 9,
    InvalidBackingPageCount = 10,
    BackingByteCountOverflow = 11,
    BackingEndOverflow = 12,
    ReservationTableRejected = 13,
    InvalidStackTop = 14,
    MisalignedStackTop = 15,
    MissingGuardReservation = 16,
    MissingStackReservation = 17,
    BackingArenaMismatch = 18,
    StackTopMismatch = 19,
    InvalidLayout = 20,
    InvalidLifetime = 21,
    RepeatedLifetimeTransition = 22,
    IllegalLifetimeTransition = 23
}

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
    private readonly OS.Kernel.Memory.BootReservedRange
        _guardReservation;
    private readonly OS.Kernel.Memory.BootReservedRange
        _stackReservation;
    private readonly PhysicalAddress _physicalStackTop;
    private readonly OwnedBootStackLifetime _lifetime;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly byte _reserved5;
    private readonly byte _reserved6;

    private OwnedBootStackLayout(
        PhysicalAddress backingStart,
        PhysicalAddress backingEndExclusive,
        ulong backingPageCount,
        ulong backingByteLength,
        OS.Kernel.Memory.BootReservedRange guardReservation,
        OS.Kernel.Memory.BootReservedRange stackReservation,
        PhysicalAddress physicalStackTop,
        OwnedBootStackLifetime lifetime)
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

    public PhysicalAddress BackingEndExclusive =>
        _backingEndExclusive;

    public ulong BackingPageCount => _backingPageCount;

    public ulong BackingByteLength => _backingByteLength;

    public OS.Kernel.Memory.BootReservedRange GuardReservation =>
        _guardReservation;

    public OS.Kernel.Memory.BootReservedRange StackReservation =>
        _stackReservation;

    public PhysicalAddress PhysicalStackTop => _physicalStackTop;

    public OwnedBootStackLifetime Lifetime => _lifetime;

    public bool IsValid => Validate();

    public static unsafe OwnedBootStackLayoutResult Create(
        OS.Kernel.Memory.BootReservedRange* reservations,
        ulong reservationCount,
        ulong reservationCapacity,
        PhysicalAddress backingStart,
        ulong backingPageCount,
        PhysicalAddress physicalStackTop)
    {
        if (reservationCount < RequiredReservationCount)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.InvalidReservationCount,
                AddressValueStatus.Uninitialized,
                reservationCount,
                reservationCapacity);
        }

        if (reservationCount > RequiredReservationCount)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.TooManyReservations,
                AddressValueStatus.Uninitialized,
                reservationCount,
                reservationCapacity);
        }

        if (reservationCapacity < reservationCount)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus
                    .ReservationCapacityTooSmall,
                AddressValueStatus.Uninitialized,
                reservationCount,
                reservationCapacity);
        }

        if (reservations == null)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.MissingReservationTable,
                AddressValueStatus.Uninitialized,
                reservationCount,
                reservationCapacity);
        }

        ulong tableAddress = (ulong)reservations;
        if ((tableAddress & (RequiredTableAlignment - 1)) != 0)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.MisalignedReservationTable,
                AddressValueStatus.MisalignedAddress,
                reservationCount,
                reservationCapacity);
        }

        ulong tableByteLength =
            reservationCount
            * (ulong)sizeof(OS.Kernel.Memory.BootReservedRange);
        if (tableAddress > MaximumUnsignedLong - tableByteLength)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus
                    .ReservationTableAddressRangeOverflow,
                AddressValueStatus.AddressOverflow,
                reservationCount,
                reservationCapacity);
        }

        if (!backingStart.IsValid)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.InvalidBackingStart,
                AddressValueStatus.InvalidAddress,
                reservationCount,
                reservationCapacity);
        }

        if (!backingStart.IsAligned(
                OS.Kernel.Memory.BootReservedRange.PageSize))
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.MisalignedBackingStart,
                AddressValueStatus.MisalignedAddress,
                reservationCount,
                reservationCapacity);
        }

        if (backingPageCount == 0)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.InvalidBackingPageCount,
                AddressValueStatus.ZeroLength,
                reservationCount,
                reservationCapacity);
        }

        if (backingPageCount < RequiredReservationCount)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.InvalidBackingPageCount,
                AddressValueStatus.Uninitialized,
                reservationCount,
                reservationCapacity);
        }

        if (backingPageCount
            > MaximumUnsignedLong
                / OS.Kernel.Memory.BootReservedRange.PageSize)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.BackingByteCountOverflow,
                AddressValueStatus.AddressOverflow,
                reservationCount,
                reservationCapacity);
        }

        ulong backingByteLength =
            backingPageCount
            * OS.Kernel.Memory.BootReservedRange.PageSize;
        AddressValueStatus backingEndStatus =
            backingStart.TryGetEndExclusive(
                backingByteLength,
                out PhysicalAddress backingEndExclusive);
        if (backingEndStatus != AddressValueStatus.Success)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.BackingEndOverflow,
                backingEndStatus,
                reservationCount,
                reservationCapacity);
        }

        if (!physicalStackTop.IsValid)
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.InvalidStackTop,
                AddressValueStatus.InvalidAddress,
                reservationCount,
                reservationCapacity);
        }

        if (!physicalStackTop.IsAligned(X64StackAlignmentBytes))
        {
            return FailBeforeTable(
                OwnedBootStackLayoutStatus.MisalignedStackTop,
                AddressValueStatus.MisalignedAddress,
                reservationCount,
                reservationCapacity);
        }

        OS.Kernel.Memory.BootReservationTableValidationResult
            tableResult =
                OS.Kernel.Memory.BootReservationTableValidator.Validate(
                    reservations,
                    reservationCount,
                    reservationCapacity);
        if (!tableResult.Succeeded)
        {
            OwnedBootStackLayoutStatus tableFailureStatus =
                tableResult.Status
                    == OS.Kernel.Memory.BootReservationTableStatus
                        .MissingBootStackGuard
                    ? OwnedBootStackLayoutStatus.MissingGuardReservation
                    : tableResult.Status
                        == OS.Kernel.Memory.BootReservationTableStatus
                            .MissingBootStack
                        ? OwnedBootStackLayoutStatus
                            .MissingStackReservation
                        : OwnedBootStackLayoutStatus
                            .ReservationTableRejected;
            return FailWithTable(
                tableFailureStatus,
                AddressValueStatus.Uninitialized,
                tableResult,
                reservationCapacity);
        }

        if (tableResult.BootStackGuardIndex
            == OS.Kernel.Memory.BootReservationTableValidationResult
                .NoIndex)
        {
            return FailWithTable(
                OwnedBootStackLayoutStatus.MissingGuardReservation,
                AddressValueStatus.Uninitialized,
                tableResult,
                reservationCapacity);
        }

        if (tableResult.BootStackIndex
            == OS.Kernel.Memory.BootReservationTableValidationResult
                .NoIndex)
        {
            return FailWithTable(
                OwnedBootStackLayoutStatus.MissingStackReservation,
                AddressValueStatus.Uninitialized,
                tableResult,
                reservationCapacity);
        }

        OS.Kernel.Memory.BootReservedRange guardReservation =
            reservations[tableResult.BootStackGuardIndex];
        OS.Kernel.Memory.BootReservedRange stackReservation =
            reservations[tableResult.BootStackIndex];

        if (!guardReservation.PhysicalStart.Equals(backingStart)
            || !stackReservation.PhysicalEndExclusive.Equals(
                backingEndExclusive)
            || tableResult.BootStackArenaPages != backingPageCount
            || tableResult.BootStackArenaBytes != backingByteLength)
        {
            return FailWithTable(
                OwnedBootStackLayoutStatus.BackingArenaMismatch,
                AddressValueStatus.InvalidAddress,
                tableResult,
                reservationCapacity);
        }

        if (!stackReservation.PhysicalEndExclusive.Equals(
                physicalStackTop))
        {
            return FailWithTable(
                OwnedBootStackLayoutStatus.StackTopMismatch,
                AddressValueStatus.InvalidAddress,
                tableResult,
                reservationCapacity);
        }

        OwnedBootStackLayout layout = new OwnedBootStackLayout(
            backingStart,
            backingEndExclusive,
            backingPageCount,
            backingByteLength,
            guardReservation,
            stackReservation,
            physicalStackTop,
            OwnedBootStackLifetime.Reserved);
        if (!layout.Validate())
        {
            return FailWithTable(
                OwnedBootStackLayoutStatus.InvalidLayout,
                AddressValueStatus.InvalidAddress,
                tableResult,
                reservationCapacity);
        }

        return OwnedBootStackLayoutResult.Create(
            OwnedBootStackLayoutStatus.Success,
            AddressValueStatus.Success,
            tableResult.Status,
            tableResult.RangeStatus,
            OwnedBootStackLifetime.Uninitialized,
            OwnedBootStackLifetime.Reserved,
            OwnedBootStackLifetime.Reserved,
            reservationCount,
            tableResult.ValidatedReservationCount,
            reservationCapacity,
            tableResult.InvalidReservationIndex,
            tableResult.ConflictingReservationIndex,
            layout);
    }

    public OwnedBootStackLayoutResult TransitionTo(
        OwnedBootStackLifetime requestedLifetime)
    {
        if (!Validate())
        {
            return TransitionFailure(
                OwnedBootStackLayoutStatus.InvalidLayout,
                requestedLifetime);
        }

        if (requestedLifetime != OwnedBootStackLifetime.Reserved
            && requestedLifetime != OwnedBootStackLifetime.Ready
            && requestedLifetime != OwnedBootStackLifetime.Active)
        {
            return TransitionFailure(
                OwnedBootStackLayoutStatus.InvalidLifetime,
                requestedLifetime);
        }

        if (requestedLifetime == _lifetime)
        {
            return TransitionFailure(
                OwnedBootStackLayoutStatus.RepeatedLifetimeTransition,
                requestedLifetime);
        }

        if ((_lifetime != OwnedBootStackLifetime.Reserved
                || requestedLifetime != OwnedBootStackLifetime.Ready)
            && (_lifetime != OwnedBootStackLifetime.Ready
                || requestedLifetime != OwnedBootStackLifetime.Active))
        {
            return TransitionFailure(
                OwnedBootStackLayoutStatus.IllegalLifetimeTransition,
                requestedLifetime);
        }

        OwnedBootStackLayout transitioned = WithLifetime(
            requestedLifetime);
        return OwnedBootStackLayoutResult.Create(
            OwnedBootStackLayoutStatus.Success,
            AddressValueStatus.Success,
            OS.Kernel.Memory.BootReservationTableStatus.Uninitialized,
            OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
            _lifetime,
            requestedLifetime,
            requestedLifetime,
            0,
            0,
            0,
            OS.Kernel.Memory.BootReservationTableValidationResult
                .NoIndex,
            OS.Kernel.Memory.BootReservationTableValidationResult
                .NoIndex,
            transitioned);
    }

    public bool Equals(OwnedBootStackLayout other)
    {
        return _backingStart.Equals(other._backingStart)
            && _backingEndExclusive.Equals(
                other._backingEndExclusive)
            && _backingPageCount == other._backingPageCount
            && _backingByteLength == other._backingByteLength
            && _guardReservation.Equals(other._guardReservation)
            && _stackReservation.Equals(other._stackReservation)
            && _physicalStackTop.Equals(other._physicalStackTop)
            && _lifetime == other._lifetime
            && _reserved0 == other._reserved0
            && _reserved1 == other._reserved1
            && _reserved2 == other._reserved2
            && _reserved3 == other._reserved3
            && _reserved4 == other._reserved4
            && _reserved5 == other._reserved5
            && _reserved6 == other._reserved6;
    }

    internal bool Validate()
    {
        if (!_backingStart.IsValid
            || !_backingStart.IsAligned(
                OS.Kernel.Memory.BootReservedRange.PageSize)
            || !_backingEndExclusive.IsValid
            || !_backingEndExclusive.IsAligned(
                OS.Kernel.Memory.BootReservedRange.PageSize)
            || _backingPageCount < RequiredReservationCount
            || _backingPageCount
                > MaximumUnsignedLong
                    / OS.Kernel.Memory.BootReservedRange.PageSize
            || _backingByteLength
                != _backingPageCount
                    * OS.Kernel.Memory.BootReservedRange.PageSize
            || !_guardReservation.IsValid
            || _guardReservation.Kind
                != OS.Kernel.Memory.BootReservationKind.BootStackGuard
            || !_stackReservation.IsValid
            || _stackReservation.Kind
                != OS.Kernel.Memory.BootReservationKind.BootStack
            || !_physicalStackTop.IsValid
            || !_physicalStackTop.IsAligned(X64StackAlignmentBytes)
            || (_lifetime != OwnedBootStackLifetime.Reserved
                && _lifetime != OwnedBootStackLifetime.Ready
                && _lifetime != OwnedBootStackLifetime.Active)
            || _reserved0 != 0
            || _reserved1 != 0
            || _reserved2 != 0
            || _reserved3 != 0
            || _reserved4 != 0
            || _reserved5 != 0
            || _reserved6 != 0)
        {
            return false;
        }

        if (_guardReservation.PageCount
                > MaximumUnsignedLong - _stackReservation.PageCount
            || _guardReservation.ByteLength
                > MaximumUnsignedLong - _stackReservation.ByteLength)
        {
            return false;
        }

        ulong arenaPages =
            _guardReservation.PageCount
            + _stackReservation.PageCount;
        ulong arenaBytes =
            _guardReservation.ByteLength
            + _stackReservation.ByteLength;
        AddressValueStatus backingEndStatus =
            _backingStart.TryGetEndExclusive(
                _backingByteLength,
                out PhysicalAddress expectedBackingEnd);

        return backingEndStatus == AddressValueStatus.Success
            && expectedBackingEnd.Equals(_backingEndExclusive)
            && arenaPages == _backingPageCount
            && arenaBytes == _backingByteLength
            && _guardReservation.PhysicalStart.Equals(_backingStart)
            && _guardReservation.PhysicalEndExclusive.Equals(
                _stackReservation.PhysicalStart)
            && _stackReservation.PhysicalEndExclusive.Equals(
                _backingEndExclusive)
            && _stackReservation.PhysicalEndExclusive.Equals(
                _physicalStackTop);
    }

    private OwnedBootStackLayout WithLifetime(
        OwnedBootStackLifetime lifetime)
    {
        return new OwnedBootStackLayout(
            _backingStart,
            _backingEndExclusive,
            _backingPageCount,
            _backingByteLength,
            _guardReservation,
            _stackReservation,
            _physicalStackTop,
            lifetime);
    }

    private OwnedBootStackLayoutResult TransitionFailure(
        OwnedBootStackLayoutStatus status,
        OwnedBootStackLifetime requestedLifetime)
    {
        return OwnedBootStackLayoutResult.Create(
            status,
            AddressValueStatus.Uninitialized,
            OS.Kernel.Memory.BootReservationTableStatus.Uninitialized,
            OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
            _lifetime,
            requestedLifetime,
            _lifetime,
            0,
            0,
            0,
            OS.Kernel.Memory.BootReservationTableValidationResult
                .NoIndex,
            OS.Kernel.Memory.BootReservationTableValidationResult
                .NoIndex,
            this);
    }

    private static OwnedBootStackLayoutResult FailBeforeTable(
        OwnedBootStackLayoutStatus status,
        AddressValueStatus addressStatus,
        ulong reservationCount,
        ulong reservationCapacity)
    {
        return OwnedBootStackLayoutResult.Create(
            status,
            addressStatus,
            OS.Kernel.Memory.BootReservationTableStatus.Uninitialized,
            OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
            OwnedBootStackLifetime.Uninitialized,
            OwnedBootStackLifetime.Reserved,
            OwnedBootStackLifetime.Uninitialized,
            reservationCount,
            0,
            reservationCapacity,
            OS.Kernel.Memory.BootReservationTableValidationResult
                .NoIndex,
            OS.Kernel.Memory.BootReservationTableValidationResult
                .NoIndex,
            default);
    }

    private static OwnedBootStackLayoutResult FailWithTable(
        OwnedBootStackLayoutStatus status,
        AddressValueStatus addressStatus,
        OS.Kernel.Memory.BootReservationTableValidationResult tableResult,
        ulong reservationCapacity)
    {
        return OwnedBootStackLayoutResult.Create(
            status,
            addressStatus,
            tableResult.Status,
            tableResult.RangeStatus,
            OwnedBootStackLifetime.Uninitialized,
            OwnedBootStackLifetime.Reserved,
            OwnedBootStackLifetime.Uninitialized,
            tableResult.ReservationCount,
            tableResult.ValidatedReservationCount,
            reservationCapacity,
            tableResult.InvalidReservationIndex,
            tableResult.ConflictingReservationIndex,
            default);
    }
}

/// <summary>
/// Carries deterministic value-only evidence for layout construction or one
/// immutable lifetime transition.
/// </summary>
public readonly struct OwnedBootStackLayoutResult
{
    private readonly OwnedBootStackLayoutStatus _status;
    private readonly AddressValueStatus _addressStatus;
    private readonly OS.Kernel.Memory.BootReservationTableStatus
        _reservationTableStatus;
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

    private OwnedBootStackLayoutResult(
        OwnedBootStackLayoutStatus status,
        AddressValueStatus addressStatus,
        OS.Kernel.Memory.BootReservationTableStatus
            reservationTableStatus,
        OS.Kernel.Memory.BootReservedRangeStatus rangeStatus,
        OwnedBootStackLifetime previousLifetime,
        OwnedBootStackLifetime requestedLifetime,
        OwnedBootStackLifetime currentLifetime,
        ulong reservationCount,
        ulong validatedReservationCount,
        ulong availableReservationCapacity,
        ulong invalidReservationIndex,
        ulong conflictingReservationIndex,
        OwnedBootStackLayout layout)
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

    public OS.Kernel.Memory.BootReservationTableStatus
        ReservationTableStatus => _reservationTableStatus;

    public OS.Kernel.Memory.BootReservedRangeStatus RangeStatus =>
        _rangeStatus;

    public OwnedBootStackLifetime PreviousLifetime =>
        _previousLifetime;

    public OwnedBootStackLifetime RequestedLifetime =>
        _requestedLifetime;

    public OwnedBootStackLifetime CurrentLifetime =>
        _currentLifetime;

    public bool Succeeded => Validate();

    public ulong ReservationCount => _reservationCount;

    public ulong ValidatedReservationCount =>
        _validatedReservationCount;

    public ulong AvailableReservationCapacity =>
        _availableReservationCapacity;

    public ulong RequiredReservationCapacity =>
        OwnedBootStackLayout.RequiredReservationCount;

    public ulong InvalidReservationIndex =>
        _invalidReservationIndex;

    public ulong ConflictingReservationIndex =>
        _conflictingReservationIndex;

    public OwnedBootStackLayout Layout => _layout;

    public bool Equals(OwnedBootStackLayoutResult other)
    {
        return _status == other._status
            && _addressStatus == other._addressStatus
            && _reservationTableStatus
                == other._reservationTableStatus
            && _rangeStatus == other._rangeStatus
            && _previousLifetime == other._previousLifetime
            && _requestedLifetime == other._requestedLifetime
            && _currentLifetime == other._currentLifetime
            && _reserved0 == other._reserved0
            && _reservationCount == other._reservationCount
            && _validatedReservationCount
                == other._validatedReservationCount
            && _availableReservationCapacity
                == other._availableReservationCapacity
            && _invalidReservationIndex
                == other._invalidReservationIndex
            && _conflictingReservationIndex
                == other._conflictingReservationIndex
            && _layout.Equals(other._layout);
    }

    internal static OwnedBootStackLayoutResult Create(
        OwnedBootStackLayoutStatus status,
        AddressValueStatus addressStatus,
        OS.Kernel.Memory.BootReservationTableStatus
            reservationTableStatus,
        OS.Kernel.Memory.BootReservedRangeStatus rangeStatus,
        OwnedBootStackLifetime previousLifetime,
        OwnedBootStackLifetime requestedLifetime,
        OwnedBootStackLifetime currentLifetime,
        ulong reservationCount,
        ulong validatedReservationCount,
        ulong availableReservationCapacity,
        ulong invalidReservationIndex,
        ulong conflictingReservationIndex,
        OwnedBootStackLayout layout)
    {
        return new OwnedBootStackLayoutResult(
            status,
            addressStatus,
            reservationTableStatus,
            rangeStatus,
            previousLifetime,
            requestedLifetime,
            currentLifetime,
            reservationCount,
            validatedReservationCount,
            availableReservationCapacity,
            invalidReservationIndex,
            conflictingReservationIndex,
            layout);
    }

    internal bool Validate()
    {
        if (_status != OwnedBootStackLayoutStatus.Success
            || _addressStatus != AddressValueStatus.Success
            || _reserved0 != 0
            || _invalidReservationIndex
                != OS.Kernel.Memory.BootReservationTableValidationResult
                    .NoIndex
            || _conflictingReservationIndex
                != OS.Kernel.Memory.BootReservationTableValidationResult
                    .NoIndex
            || !_layout.Validate()
            || _currentLifetime != _layout.Lifetime
            || _requestedLifetime != _currentLifetime)
        {
            return false;
        }

        bool creation =
            _previousLifetime == OwnedBootStackLifetime.Uninitialized
            && _currentLifetime == OwnedBootStackLifetime.Reserved
            && _reservationTableStatus
                == OS.Kernel.Memory.BootReservationTableStatus.Success
            && _rangeStatus
                == OS.Kernel.Memory.BootReservedRangeStatus.Success
            && _reservationCount
                == OwnedBootStackLayout.RequiredReservationCount
            && _validatedReservationCount
                == OwnedBootStackLayout.RequiredReservationCount
            && _availableReservationCapacity
                >= OwnedBootStackLayout.RequiredReservationCount;
        bool transition =
            (_previousLifetime == OwnedBootStackLifetime.Reserved
                && _currentLifetime == OwnedBootStackLifetime.Ready
                || _previousLifetime == OwnedBootStackLifetime.Ready
                    && _currentLifetime == OwnedBootStackLifetime.Active)
            && _reservationTableStatus
                == OS.Kernel.Memory.BootReservationTableStatus
                    .Uninitialized
            && _rangeStatus
                == OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized
            && _reservationCount == 0
            && _validatedReservationCount == 0
            && _availableReservationCapacity == 0;
        return creation || transition;
    }
}
