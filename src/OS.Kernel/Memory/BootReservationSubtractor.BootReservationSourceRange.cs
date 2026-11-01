namespace OS.Kernel.Memory;
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
    private BootReservationSourceRange(int sourceRangeIndex, OS.Boot.PhysicalAddress physicalStart, OS.Boot.PhysicalAddress physicalEndExclusive, ulong pageCount, ulong byteLength)
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
    public OS.Boot.PhysicalAddress PhysicalEndExclusive => _physicalEndExclusive;
    public ulong PageCount => _pageCount;
    public ulong ByteLength => _byteLength;
    public bool IsValid => Validate() == BootReservationSourceRangeStatus.Success;

    public static BootReservationSourceRangeStatus TryCreate(int sourceRangeIndex, OS.Boot.PhysicalAddress physicalStart, ulong pageCount, out BootReservationSourceRange range)
    {
        range = default;
        if (sourceRangeIndex < 0)
        {
            return BootReservationSourceRangeStatus.InvalidSourceRangeIndex;
        }

        if (!physicalStart.IsValid)
        {
            return BootReservationSourceRangeStatus.InvalidPhysicalStart;
        }

        if (!physicalStart.IsAligned(PageSize))
        {
            return BootReservationSourceRangeStatus.MisalignedPhysicalStart;
        }

        if (pageCount == 0)
        {
            return BootReservationSourceRangeStatus.InvalidPageCount;
        }

        if (pageCount > MaximumUnsignedLong / PageSize)
        {
            return BootReservationSourceRangeStatus.PageByteCountOverflow;
        }

        ulong byteLength = pageCount * PageSize;
        OS.Boot.AddressValueStatus endStatus = physicalStart.TryGetEndExclusive(byteLength, out OS.Boot.PhysicalAddress physicalEndExclusive);
        if (endStatus != OS.Boot.AddressValueStatus.Success)
        {
            return endStatus == OS.Boot.AddressValueStatus.AddressOverflow ? BootReservationSourceRangeStatus.AddressRangeOverflow : BootReservationSourceRangeStatus.InvalidPhysicalStart;
        }

        if (!physicalEndExclusive.IsAligned(PageSize))
        {
            return BootReservationSourceRangeStatus.MisalignedPhysicalEndExclusive;
        }

        range = new BootReservationSourceRange(sourceRangeIndex, physicalStart, physicalEndExclusive, pageCount, byteLength);
        return BootReservationSourceRangeStatus.Success;
    }

    public bool Equals(BootReservationSourceRange other)
    {
        return _sourceRangeIndex == other._sourceRangeIndex && _reserved == other._reserved && _physicalStart.Equals(other._physicalStart) && _physicalEndExclusive.Equals(other._physicalEndExclusive) && _pageCount == other._pageCount && _byteLength == other._byteLength;
    }

    internal BootReservationSourceRangeStatus Validate()
    {
        if (_sourceRangeIndex < 0)
        {
            return BootReservationSourceRangeStatus.InvalidSourceRangeIndex;
        }

        if (_reserved != 0)
        {
            return BootReservationSourceRangeStatus.ReservedBitsSet;
        }

        if (!_physicalStart.IsValid)
        {
            return BootReservationSourceRangeStatus.InvalidPhysicalStart;
        }

        if (!_physicalStart.IsAligned(PageSize))
        {
            return BootReservationSourceRangeStatus.MisalignedPhysicalStart;
        }

        if (_pageCount == 0)
        {
            return BootReservationSourceRangeStatus.InvalidPageCount;
        }

        if (_pageCount > MaximumUnsignedLong / PageSize)
        {
            return BootReservationSourceRangeStatus.PageByteCountOverflow;
        }

        ulong expectedByteLength = _pageCount * PageSize;
        if (_byteLength != expectedByteLength)
        {
            return BootReservationSourceRangeStatus.ByteLengthMismatch;
        }

        if (!_physicalEndExclusive.IsValid)
        {
            return BootReservationSourceRangeStatus.InvalidPhysicalEndExclusive;
        }

        if (!_physicalEndExclusive.IsAligned(PageSize))
        {
            return BootReservationSourceRangeStatus.MisalignedPhysicalEndExclusive;
        }

        OS.Boot.AddressValueStatus endStatus = _physicalStart.TryGetEndExclusive(expectedByteLength, out OS.Boot.PhysicalAddress expectedEndExclusive);
        if (endStatus != OS.Boot.AddressValueStatus.Success)
        {
            return endStatus == OS.Boot.AddressValueStatus.AddressOverflow ? BootReservationSourceRangeStatus.AddressRangeOverflow : BootReservationSourceRangeStatus.InvalidPhysicalStart;
        }

        return _physicalEndExclusive.Equals(expectedEndExclusive) ? BootReservationSourceRangeStatus.Success : BootReservationSourceRangeStatus.PhysicalEndMismatch;
    }

    internal static BootReservationSourceRange CreateValidatedForSubtraction(int sourceRangeIndex, OS.Boot.PhysicalAddress physicalStart, OS.Boot.PhysicalAddress physicalEndExclusive, ulong pageCount, ulong byteLength)
    {
        return new BootReservationSourceRange(sourceRangeIndex, physicalStart, physicalEndExclusive, pageCount, byteLength);
    }
}
