namespace OS.Boot;

/// <summary>
/// Reports validation of one supplied EFI loaded-image observation.
/// Existing values are append-only and the zero-valued default is not success.
/// </summary>
public enum EfiLoadedImageRangeCaptureStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidObservationCount = 2,
    TooManyObservations = 3,
    ObservationCapacityTooSmall = 4,
    MissingObservationBuffer = 5,
    MisalignedObservationBuffer = 6,
    ObservationAddressRangeOverflow = 7,
    InvalidPageSize = 8,
    InvalidImageHandle = 9,
    FirmwareFailure = 10,
    MissingProtocolPointer = 11,
    MisalignedProtocolPointer = 12,
    InvalidImageBase = 13,
    InvalidImageSize = 14,
    ImageEndOverflow = 15,
    ReservationStartZero = 16,
    ReservationRoundingOverflow = 17,
    ReservationRangeInvalid = 18,
    ReservationRejected = 19,
    ReservationEndMismatch = 20
}

/// <summary>
/// Holds one untrusted transcript of an attempted loaded-image protocol
/// acquisition. It is a compact CLROS observation, not the 96-byte x64
/// EFI_LOADED_IMAGE_PROTOCOL and must never be cast over firmware memory.
/// </summary>
public readonly struct EfiLoadedImageProtocolObservation
{
    private readonly ulong _firmwareStatus;
    private readonly ulong _imageHandleFromUefi;
    private readonly ulong _protocolAddressFromUefi;
    private readonly ulong _imageBaseFromUefi;
    private readonly ulong _imageSizeFromUefi;

    private EfiLoadedImageProtocolObservation(
        ulong firmwareStatus,
        ulong imageHandleFromUefi,
        ulong protocolAddressFromUefi,
        ulong imageBaseFromUefi,
        ulong imageSizeFromUefi)
    {
        _firmwareStatus = firmwareStatus;
        _imageHandleFromUefi = imageHandleFromUefi;
        _protocolAddressFromUefi = protocolAddressFromUefi;
        _imageBaseFromUefi = imageBaseFromUefi;
        _imageSizeFromUefi = imageSizeFromUefi;
    }

    internal ulong FirmwareStatus => _firmwareStatus;

    internal ulong ImageHandleFromUefi => _imageHandleFromUefi;

    internal ulong ProtocolAddressFromUefi => _protocolAddressFromUefi;

    internal ulong ImageBaseFromUefi => _imageBaseFromUefi;

    internal ulong ImageSizeFromUefi => _imageSizeFromUefi;

    public static EfiLoadedImageProtocolObservation CaptureFromUefiBoundary(
        ulong firmwareStatus,
        ulong imageHandle,
        ulong protocolAddress,
        ulong imageBase,
        ulong imageSize)
    {
        return new EfiLoadedImageProtocolObservation(
            firmwareStatus,
            imageHandle,
            protocolAddress,
            imageBase,
            imageSize);
    }

    public bool Equals(EfiLoadedImageProtocolObservation other)
    {
        return _firmwareStatus == other._firmwareStatus
            && _imageHandleFromUefi == other._imageHandleFromUefi
            && _protocolAddressFromUefi
                == other._protocolAddressFromUefi
            && _imageBaseFromUefi == other._imageBaseFromUefi
            && _imageSizeFromUefi == other._imageSizeFromUefi;
    }
}

/// <summary>
/// Carries deterministic value-only evidence for deriving one exact and
/// outward-rounded loaded-image range from a caller-owned observation.
/// Firmware pointers are validated transiently and are never retained.
/// </summary>
public readonly struct EfiLoadedImageRangeCapturePlan
{
    public const ulong EfiSuccess = 0;
    public const ulong RequiredObservationCount = 1;
    public const ulong MaximumObservationCount = 1;
    public const ulong NoIndex = ~0UL;

    private const ulong RequiredObservationAlignment = 8;
    private const ulong RequiredProtocolPointerAlignment = 8;
    private const ulong MaximumUnsignedLong = ~0UL;

    private readonly EfiLoadedImageRangeCaptureStatus _status;
    private readonly AddressValueStatus _addressStatus;
    private readonly OS.Kernel.Memory.BootReservedRangeStatus
        _reservationStatus;
    private readonly byte _reserved0;
    private readonly byte _reserved1;
    private readonly byte _reserved2;
    private readonly byte _reserved3;
    private readonly byte _reserved4;
    private readonly ulong _observationCount;
    private readonly ulong _validatedObservationCount;
    private readonly ulong _availableObservationCapacity;
    private readonly ulong _invalidObservationIndex;
    private readonly ulong _pageSize;
    private readonly ulong _firmwareStatus;
    private readonly ulong _imageHandle;
    private readonly EfiLoadedImageRange _range;

    private EfiLoadedImageRangeCapturePlan(
        EfiLoadedImageRangeCaptureStatus status,
        AddressValueStatus addressStatus,
        OS.Kernel.Memory.BootReservedRangeStatus reservationStatus,
        ulong observationCount,
        ulong validatedObservationCount,
        ulong availableObservationCapacity,
        ulong invalidObservationIndex,
        ulong pageSize,
        ulong firmwareStatus,
        ulong imageHandle,
        EfiLoadedImageRange range)
    {
        _status = status;
        _addressStatus = addressStatus;
        _reservationStatus = reservationStatus;
        _reserved0 = 0;
        _reserved1 = 0;
        _reserved2 = 0;
        _reserved3 = 0;
        _reserved4 = 0;
        _observationCount = observationCount;
        _validatedObservationCount = validatedObservationCount;
        _availableObservationCapacity = availableObservationCapacity;
        _invalidObservationIndex = invalidObservationIndex;
        _pageSize = pageSize;
        _firmwareStatus = firmwareStatus;
        _imageHandle = imageHandle;
        _range = range;
    }

    public EfiLoadedImageRangeCaptureStatus Status => _status;

    public AddressValueStatus AddressStatus => _addressStatus;

    public OS.Kernel.Memory.BootReservedRangeStatus ReservationStatus =>
        _reservationStatus;

    public bool Succeeded => Validate();

    public ulong ObservationCount => _observationCount;

    public ulong ValidatedObservationCount =>
        _validatedObservationCount;

    public ulong AvailableObservationCapacity =>
        _availableObservationCapacity;

    public ulong RequiredObservationCapacity =>
        RequiredObservationCount;

    public ulong InvalidObservationIndex => _invalidObservationIndex;

    public ulong PageSize => _pageSize;

    public ulong FirmwareStatus => _firmwareStatus;

    public ulong ImageHandle => _imageHandle;

    public EfiLoadedImageRange Range => _range;

    public static unsafe EfiLoadedImageRangeCapturePlan Create(
        EfiLoadedImageProtocolObservation* observations,
        ulong observationCount,
        ulong observationCapacity,
        ulong pageSize)
    {
        if (observationCount == 0)
        {
            return FailBeforeObservation(
                EfiLoadedImageRangeCaptureStatus.InvalidObservationCount,
                AddressValueStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize);
        }

        if (observationCount > MaximumObservationCount)
        {
            return FailBeforeObservation(
                EfiLoadedImageRangeCaptureStatus.TooManyObservations,
                AddressValueStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize);
        }

        if (observationCapacity < observationCount)
        {
            return FailBeforeObservation(
                EfiLoadedImageRangeCaptureStatus
                    .ObservationCapacityTooSmall,
                AddressValueStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize);
        }

        if (observations == null)
        {
            return FailBeforeObservation(
                EfiLoadedImageRangeCaptureStatus.MissingObservationBuffer,
                AddressValueStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize);
        }

        ulong observationAddress = (ulong)observations;
        if ((observationAddress
                & (RequiredObservationAlignment - 1)) != 0)
        {
            return FailBeforeObservation(
                EfiLoadedImageRangeCaptureStatus
                    .MisalignedObservationBuffer,
                AddressValueStatus.MisalignedAddress,
                observationCount,
                observationCapacity,
                pageSize);
        }

        ulong observationByteLength =
            observationCount
            * (ulong)sizeof(EfiLoadedImageProtocolObservation);
        if (observationAddress
            > MaximumUnsignedLong - observationByteLength)
        {
            return FailBeforeObservation(
                EfiLoadedImageRangeCaptureStatus
                    .ObservationAddressRangeOverflow,
                AddressValueStatus.AddressOverflow,
                observationCount,
                observationCapacity,
                pageSize);
        }

        if (pageSize != EfiLoadedImageRange.PageSize)
        {
            return FailBeforeObservation(
                EfiLoadedImageRangeCaptureStatus.InvalidPageSize,
                AddressValueStatus.InvalidAlignment,
                observationCount,
                observationCapacity,
                pageSize);
        }

        EfiLoadedImageProtocolObservation observation = observations[0];

        if (observation.ImageHandleFromUefi == 0)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.InvalidImageHandle,
                AddressValueStatus.ZeroAddress,
                OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        if (observation.FirmwareStatus != EfiSuccess)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.FirmwareFailure,
                AddressValueStatus.Uninitialized,
                OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        if (observation.ProtocolAddressFromUefi == 0)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.MissingProtocolPointer,
                AddressValueStatus.ZeroAddress,
                OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        AddressValueStatus protocolStatus =
            VirtualAddress.TryCreateAlignedFromUefiBoundary(
                observation.ProtocolAddressFromUefi,
                RequiredProtocolPointerAlignment,
                out VirtualAddress protocolAddress);
        if (protocolStatus != AddressValueStatus.Success
            || !protocolAddress.IsValid)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.MisalignedProtocolPointer,
                protocolStatus,
                OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        AddressValueStatus imageBaseStatus =
            PhysicalAddress.TryCreateFromUefiBoundary(
                observation.ImageBaseFromUefi,
                out PhysicalAddress imageBase);
        if (imageBaseStatus != AddressValueStatus.Success)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.InvalidImageBase,
                imageBaseStatus,
                OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        if (observation.ImageSizeFromUefi == 0)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.InvalidImageSize,
                AddressValueStatus.ZeroLength,
                OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        if (observation.ImageSizeFromUefi
            > MaximumUnsignedLong - observation.ImageBaseFromUefi)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.ImageEndOverflow,
                AddressValueStatus.AddressOverflow,
                OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        ulong imageEndValue =
            observation.ImageBaseFromUefi
            + observation.ImageSizeFromUefi;
        AddressValueStatus imageEndStatus =
            PhysicalAddress.TryCreateFromUefiBoundary(
                imageEndValue,
                out PhysicalAddress imageEndExclusive);
        if (imageEndStatus != AddressValueStatus.Success)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.ImageEndOverflow,
                imageEndStatus,
                OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        ulong pageMask = pageSize - 1;
        ulong reservationStartValue =
            observation.ImageBaseFromUefi & ~pageMask;
        if (reservationStartValue == 0)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.ReservationStartZero,
                AddressValueStatus.ZeroAddress,
                OS.Kernel.Memory.BootReservedRangeStatus
                    .InvalidPhysicalStart,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        ulong reservationEndValue = imageEndValue;
        ulong endRemainder = imageEndValue & pageMask;
        if (endRemainder != 0)
        {
            ulong roundingIncrement = pageSize - endRemainder;
            if (roundingIncrement
                > MaximumUnsignedLong - imageEndValue)
            {
                return FailObservation(
                    EfiLoadedImageRangeCaptureStatus
                        .ReservationRoundingOverflow,
                    AddressValueStatus.AddressOverflow,
                    OS.Kernel.Memory.BootReservedRangeStatus
                        .AddressRangeOverflow,
                    observationCount,
                    observationCapacity,
                    pageSize,
                    observation,
                    default);
            }

            reservationEndValue = imageEndValue + roundingIncrement;
        }

        AddressValueStatus reservationStartStatus =
            PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                reservationStartValue,
                pageSize,
                out PhysicalAddress reservationStart);
        AddressValueStatus reservationEndStatus =
            PhysicalAddress.TryCreateAlignedFromUefiBoundary(
                reservationEndValue,
                pageSize,
                out PhysicalAddress reservationEndExclusive);
        if (reservationStartStatus != AddressValueStatus.Success
            || reservationEndStatus != AddressValueStatus.Success
            || reservationEndValue <= reservationStartValue)
        {
            AddressValueStatus rangeStatus =
                reservationStartStatus != AddressValueStatus.Success
                    ? reservationStartStatus
                    : reservationEndStatus != AddressValueStatus.Success
                        ? reservationEndStatus
                        : AddressValueStatus.InvalidAddress;
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.ReservationRangeInvalid,
                rangeStatus,
                OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        ulong reservationByteLength =
            reservationEndValue - reservationStartValue;
        ulong reservationPageCount = reservationByteLength / pageSize;
        OS.Kernel.Memory.BootReservedRangeStatus reservationStatus =
            OS.Kernel.Memory.BootReservedRange.TryCreate(
                OS.Kernel.Memory.BootReservationKind.LoadedImage,
                reservationStart,
                reservationPageCount,
                out OS.Kernel.Memory.BootReservedRange reservation);
        if (reservationStatus
            != OS.Kernel.Memory.BootReservedRangeStatus.Success)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.ReservationRejected,
                AddressValueStatus.Uninitialized,
                reservationStatus,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        if (!reservation.PhysicalEndExclusive.Equals(
                reservationEndExclusive)
            || reservation.ByteLength != reservationByteLength)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.ReservationEndMismatch,
                AddressValueStatus.InvalidAddress,
                reservationStatus,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        ulong leadingPaddingBytes =
            observation.ImageBaseFromUefi - reservationStartValue;
        ulong trailingPaddingBytes =
            reservationEndValue - imageEndValue;
        EfiLoadedImageRange range =
            EfiLoadedImageRange.CreateValidated(
                imageBase,
                imageEndExclusive,
                observation.ImageSizeFromUefi,
                reservation,
                leadingPaddingBytes,
                trailingPaddingBytes);
        if (!range.IsValid)
        {
            return FailObservation(
                EfiLoadedImageRangeCaptureStatus.ReservationRangeInvalid,
                AddressValueStatus.InvalidAddress,
                reservationStatus,
                observationCount,
                observationCapacity,
                pageSize,
                observation,
                default);
        }

        return new EfiLoadedImageRangeCapturePlan(
            EfiLoadedImageRangeCaptureStatus.Success,
            AddressValueStatus.Success,
            reservationStatus,
            observationCount,
            RequiredObservationCount,
            observationCapacity,
            NoIndex,
            pageSize,
            observation.FirmwareStatus,
            observation.ImageHandleFromUefi,
            range);
    }

    public bool Equals(EfiLoadedImageRangeCapturePlan other)
    {
        return _status == other._status
            && _addressStatus == other._addressStatus
            && _reservationStatus == other._reservationStatus
            && _reserved0 == other._reserved0
            && _reserved1 == other._reserved1
            && _reserved2 == other._reserved2
            && _reserved3 == other._reserved3
            && _reserved4 == other._reserved4
            && _observationCount == other._observationCount
            && _validatedObservationCount
                == other._validatedObservationCount
            && _availableObservationCapacity
                == other._availableObservationCapacity
            && _invalidObservationIndex
                == other._invalidObservationIndex
            && _pageSize == other._pageSize
            && _firmwareStatus == other._firmwareStatus
            && _imageHandle == other._imageHandle
            && _range.Equals(other._range);
    }

    internal bool Validate()
    {
        return _status == EfiLoadedImageRangeCaptureStatus.Success
            && _addressStatus == AddressValueStatus.Success
            && _reservationStatus
                == OS.Kernel.Memory.BootReservedRangeStatus.Success
            && _reserved0 == 0
            && _reserved1 == 0
            && _reserved2 == 0
            && _reserved3 == 0
            && _reserved4 == 0
            && _observationCount == RequiredObservationCount
            && _validatedObservationCount == RequiredObservationCount
            && _availableObservationCapacity
                >= RequiredObservationCount
            && _invalidObservationIndex == NoIndex
            && _pageSize == EfiLoadedImageRange.PageSize
            && _firmwareStatus == EfiSuccess
            && _imageHandle != 0
            && _range.IsValid;
    }

    private static EfiLoadedImageRangeCapturePlan FailBeforeObservation(
        EfiLoadedImageRangeCaptureStatus status,
        AddressValueStatus addressStatus,
        ulong observationCount,
        ulong observationCapacity,
        ulong pageSize)
    {
        return new EfiLoadedImageRangeCapturePlan(
            status,
            addressStatus,
            OS.Kernel.Memory.BootReservedRangeStatus.Uninitialized,
            observationCount,
            0,
            observationCapacity,
            NoIndex,
            pageSize,
            EfiSuccess,
            0,
            default);
    }

    private static EfiLoadedImageRangeCapturePlan FailObservation(
        EfiLoadedImageRangeCaptureStatus status,
        AddressValueStatus addressStatus,
        OS.Kernel.Memory.BootReservedRangeStatus reservationStatus,
        ulong observationCount,
        ulong observationCapacity,
        ulong pageSize,
        EfiLoadedImageProtocolObservation observation,
        EfiLoadedImageRange range)
    {
        return new EfiLoadedImageRangeCapturePlan(
            status,
            addressStatus,
            reservationStatus,
            observationCount,
            0,
            observationCapacity,
            0,
            pageSize,
            observation.FirmwareStatus,
            observation.ImageHandleFromUefi,
            range);
    }
}
