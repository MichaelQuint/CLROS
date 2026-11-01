namespace OS.Boot;
/// <summary>
/// Identifies a nonzero device-visible address without implying that a DMA
/// mapping, device ownership record, or IOMMU translation exists.
/// Named raw-value boundary methods record audit intent; they do not authorize
/// a caller or establish ownership.
/// </summary>

public readonly struct DeviceAddress
{
    private readonly ulong _value;
    private DeviceAddress(ulong value)
    {
        _value = value;
    }

    public bool IsValid => _value != 0;

    public static AddressValueStatus TryCreateFromHardwareRegisterBoundary(ulong value, out DeviceAddress address)
    {
        return TryCreateBoundary(value, out address);
    }

    public static AddressValueStatus TryCreateAlignedFromHardwareRegisterBoundary(ulong value, ulong alignment, out DeviceAddress address)
    {
        return TryCreateAlignedBoundary(value, alignment, out address);
    }

    public AddressValueStatus TryGetRawValueForHardwareRegisterBoundary(out ulong value)
    {
        return AddressValueOperations.TryGetRawValue(_value, out value);
    }

    private static AddressValueStatus TryCreateBoundary(ulong value, out DeviceAddress address)
    {
        address = default;
        AddressValueStatus status = AddressValueOperations.ValidateAddress(value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new DeviceAddress(value);
        return AddressValueStatus.Success;
    }

    private static AddressValueStatus TryCreateAlignedBoundary(ulong value, ulong alignment, out DeviceAddress address)
    {
        address = default;
        AddressValueStatus status = AddressValueOperations.ValidateAlignedAddress(value, alignment);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new DeviceAddress(value);
        return AddressValueStatus.Success;
    }

    public bool IsAligned(ulong alignment)
    {
        return IsValid && AddressValueOperations.IsAligned(_value, alignment);
    }

    public AddressValueStatus TryAdd(ulong byteOffset, out DeviceAddress address)
    {
        address = default;
        AddressValueStatus status = AddressValueOperations.TryAdd(_value, byteOffset, out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new DeviceAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryGetEndExclusive(ulong byteLength, out DeviceAddress endExclusive)
    {
        endExclusive = default;
        AddressValueStatus status = AddressValueOperations.TryGetEndExclusive(_value, byteLength, out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        endExclusive = new DeviceAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryAlignUp(ulong alignment, out DeviceAddress address)
    {
        address = default;
        AddressValueStatus status = AddressValueOperations.TryAlignUp(_value, alignment, out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new DeviceAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryCompareTo(DeviceAddress other, out int comparison)
    {
        return AddressValueOperations.TryCompare(_value, other._value, out comparison);
    }

    public bool Equals(DeviceAddress other)
    {
        return _value == other._value;
    }
}
