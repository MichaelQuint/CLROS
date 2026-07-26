namespace OS.Boot;

/// <summary>
/// Describes the exact EFI loaded-image extent and the enclosing canonical
/// 4 KiB physical reservation derived from it. The reservation has not
/// necessarily been inserted into a live reservation table.
/// </summary>
public readonly struct EfiLoadedImageRange
{
    public const ulong PageSize =
        OS.Kernel.Memory.BootReservedRange.PageSize;

    private const ulong MaximumUnsignedLong = ~0UL;

    private readonly PhysicalAddress _imageBase;
    private readonly PhysicalAddress _imageEndExclusive;
    private readonly ulong _imageSize;
    private readonly OS.Kernel.Memory.BootReservedRange _reservation;
    private readonly ulong _leadingPaddingBytes;
    private readonly ulong _trailingPaddingBytes;

    private EfiLoadedImageRange(
        PhysicalAddress imageBase,
        PhysicalAddress imageEndExclusive,
        ulong imageSize,
        OS.Kernel.Memory.BootReservedRange reservation,
        ulong leadingPaddingBytes,
        ulong trailingPaddingBytes)
    {
        _imageBase = imageBase;
        _imageEndExclusive = imageEndExclusive;
        _imageSize = imageSize;
        _reservation = reservation;
        _leadingPaddingBytes = leadingPaddingBytes;
        _trailingPaddingBytes = trailingPaddingBytes;
    }

    public PhysicalAddress ImageBase => _imageBase;

    public PhysicalAddress ImageEndExclusive => _imageEndExclusive;

    public ulong ImageSize => _imageSize;

    public OS.Kernel.Memory.BootReservedRange Reservation => _reservation;

    public ulong LeadingPaddingBytes => _leadingPaddingBytes;

    public ulong TrailingPaddingBytes => _trailingPaddingBytes;

    public bool IsValid => Validate();

    public bool Equals(EfiLoadedImageRange other)
    {
        return _imageBase.Equals(other._imageBase)
            && _imageEndExclusive.Equals(other._imageEndExclusive)
            && _imageSize == other._imageSize
            && _reservation.Equals(other._reservation)
            && _leadingPaddingBytes == other._leadingPaddingBytes
            && _trailingPaddingBytes == other._trailingPaddingBytes;
    }

    internal static EfiLoadedImageRange CreateValidated(
        PhysicalAddress imageBase,
        PhysicalAddress imageEndExclusive,
        ulong imageSize,
        OS.Kernel.Memory.BootReservedRange reservation,
        ulong leadingPaddingBytes,
        ulong trailingPaddingBytes)
    {
        return new EfiLoadedImageRange(
            imageBase,
            imageEndExclusive,
            imageSize,
            reservation,
            leadingPaddingBytes,
            trailingPaddingBytes);
    }

    internal bool Validate()
    {
        if (!_imageBase.IsValid
            || !_imageEndExclusive.IsValid
            || _imageSize == 0
            || !_reservation.IsValid
            || _reservation.Kind
                != OS.Kernel.Memory.BootReservationKind.LoadedImage
            || _leadingPaddingBytes
                >= OS.Kernel.Memory.BootReservedRange.PageSize
            || _trailingPaddingBytes
                >= OS.Kernel.Memory.BootReservedRange.PageSize)
        {
            return false;
        }

        AddressValueStatus imageEndStatus = _imageBase.TryGetEndExclusive(
            _imageSize,
            out PhysicalAddress expectedImageEnd);
        if (imageEndStatus != AddressValueStatus.Success
            || !expectedImageEnd.Equals(_imageEndExclusive))
        {
            return false;
        }

        AddressValueStatus imageBaseStatus =
            _reservation.PhysicalStart.TryAdd(
                _leadingPaddingBytes,
                out PhysicalAddress expectedImageBase);
        if (imageBaseStatus != AddressValueStatus.Success
            || !expectedImageBase.Equals(_imageBase))
        {
            return false;
        }

        AddressValueStatus paddedEndStatus = _imageEndExclusive.TryAdd(
            _trailingPaddingBytes,
            out PhysicalAddress expectedPaddedEnd);
        if (paddedEndStatus != AddressValueStatus.Success
            || !expectedPaddedEnd.Equals(
                _reservation.PhysicalEndExclusive))
        {
            return false;
        }

        if (_leadingPaddingBytes > MaximumUnsignedLong - _imageSize)
        {
            return false;
        }

        ulong coveredBytes = _leadingPaddingBytes + _imageSize;
        if (_trailingPaddingBytes > MaximumUnsignedLong - coveredBytes)
        {
            return false;
        }

        return coveredBytes + _trailingPaddingBytes
            == _reservation.ByteLength;
    }
}
