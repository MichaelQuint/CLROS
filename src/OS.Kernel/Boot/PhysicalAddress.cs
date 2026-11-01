namespace OS.Boot;
/// <summary>
/// Identifies a nonzero physical address without implying that it is mapped,
/// owned, reserved, or valid for the current processor's address width.
/// Named raw-value boundary methods record audit intent; they do not authorize
/// a caller or establish ownership.
/// </summary>

public readonly struct PhysicalAddress
{
    private readonly ulong _value;
    private PhysicalAddress(ulong value)
    {
        _value = value;
    }

    public bool IsValid => _value != 0;

    public static AddressValueStatus TryCreateFromUefiBoundary(ulong value, out PhysicalAddress address)
    {
        return TryCreateBoundary(value, out address);
    }

    public static AddressValueStatus TryCreateAlignedFromUefiBoundary(ulong value, ulong alignment, out PhysicalAddress address)
    {
        return TryCreateAlignedBoundary(value, alignment, out address);
    }

    public static AddressValueStatus TryCreateFromHardwareRegisterBoundary(ulong value, out PhysicalAddress address)
    {
        return TryCreateBoundary(value, out address);
    }

    public static AddressValueStatus TryCreateAlignedFromHardwareRegisterBoundary(ulong value, ulong alignment, out PhysicalAddress address)
    {
        return TryCreateAlignedBoundary(value, alignment, out address);
    }

    public AddressValueStatus TryGetRawValueForUefiBoundary(out ulong value)
    {
        return AddressValueOperations.TryGetRawValue(_value, out value);
    }

    public AddressValueStatus TryGetRawValueForHardwareRegisterBoundary(out ulong value)
    {
        return AddressValueOperations.TryGetRawValue(_value, out value);
    }

    private static AddressValueStatus TryCreateBoundary(ulong value, out PhysicalAddress address)
    {
        address = default;
        AddressValueStatus status = AddressValueOperations.ValidateAddress(value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new PhysicalAddress(value);
        return AddressValueStatus.Success;
    }

    private static AddressValueStatus TryCreateAlignedBoundary(ulong value, ulong alignment, out PhysicalAddress address)
    {
        address = default;
        AddressValueStatus status = AddressValueOperations.ValidateAlignedAddress(value, alignment);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new PhysicalAddress(value);
        return AddressValueStatus.Success;
    }

    public bool IsAligned(ulong alignment)
    {
        return IsValid && AddressValueOperations.IsAligned(_value, alignment);
    }

    public AddressValueStatus TryAdd(ulong byteOffset, out PhysicalAddress address)
    {
        address = default;
        AddressValueStatus status = AddressValueOperations.TryAdd(_value, byteOffset, out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new PhysicalAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryGetEndExclusive(ulong byteLength, out PhysicalAddress endExclusive)
    {
        endExclusive = default;
        AddressValueStatus status = AddressValueOperations.TryGetEndExclusive(_value, byteLength, out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        endExclusive = new PhysicalAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryAlignUp(ulong alignment, out PhysicalAddress address)
    {
        address = default;
        AddressValueStatus status = AddressValueOperations.TryAlignUp(_value, alignment, out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new PhysicalAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryCompareTo(PhysicalAddress other, out int comparison)
    {
        return AddressValueOperations.TryCompare(_value, other._value, out comparison);
    }

    public bool Equals(PhysicalAddress other)
    {
        return _value == other._value;
    }
}
