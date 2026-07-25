namespace OS.Boot;

/// <summary>
/// Reports the result of constructing or operating on a typed address value.
/// The zero-valued default is intentionally not success.
/// </summary>
public enum AddressValueStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidAddress = 2,
    ZeroAddress = 3,
    ZeroLength = 4,
    InvalidAlignment = 5,
    MisalignedAddress = 6,
    AddressOverflow = 7
}

/// <summary>
/// Identifies a nonzero physical address without implying that it is mapped,
/// owned, reserved, or valid for the current processor's address width.
/// </summary>
public readonly struct PhysicalAddress
{
    private readonly ulong _value;

    private PhysicalAddress(ulong value)
    {
        _value = value;
    }

    public ulong Value => _value;

    public bool IsValid => _value != 0;

    public static AddressValueStatus TryCreate(ulong value, out PhysicalAddress address)
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

    public static AddressValueStatus TryCreateAligned(
        ulong value,
        ulong alignment,
        out PhysicalAddress address)
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

        AddressValueStatus status = AddressValueOperations.TryAdd(
            _value,
            byteOffset,
            out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new PhysicalAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryGetEndExclusive(
        ulong byteLength,
        out PhysicalAddress endExclusive)
    {
        endExclusive = default;

        AddressValueStatus status = AddressValueOperations.TryGetEndExclusive(
            _value,
            byteLength,
            out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        endExclusive = new PhysicalAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryAlignUp(
        ulong alignment,
        out PhysicalAddress address)
    {
        address = default;

        AddressValueStatus status = AddressValueOperations.TryAlignUp(
            _value,
            alignment,
            out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new PhysicalAddress(value);
        return AddressValueStatus.Success;
    }

    public bool Equals(PhysicalAddress other)
    {
        return _value == other._value;
    }
}

/// <summary>
/// Identifies a nonzero virtual address without implying that it is canonical,
/// mapped, owned, or accessible with a particular page protection.
/// </summary>
public readonly struct VirtualAddress
{
    private readonly ulong _value;

    private VirtualAddress(ulong value)
    {
        _value = value;
    }

    public ulong Value => _value;

    public bool IsValid => _value != 0;

    public static AddressValueStatus TryCreate(ulong value, out VirtualAddress address)
    {
        address = default;

        AddressValueStatus status = AddressValueOperations.ValidateAddress(value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new VirtualAddress(value);
        return AddressValueStatus.Success;
    }

    public static AddressValueStatus TryCreateAligned(
        ulong value,
        ulong alignment,
        out VirtualAddress address)
    {
        address = default;

        AddressValueStatus status = AddressValueOperations.ValidateAlignedAddress(value, alignment);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new VirtualAddress(value);
        return AddressValueStatus.Success;
    }

    public bool IsAligned(ulong alignment)
    {
        return IsValid && AddressValueOperations.IsAligned(_value, alignment);
    }

    public AddressValueStatus TryAdd(ulong byteOffset, out VirtualAddress address)
    {
        address = default;

        AddressValueStatus status = AddressValueOperations.TryAdd(
            _value,
            byteOffset,
            out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new VirtualAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryGetEndExclusive(
        ulong byteLength,
        out VirtualAddress endExclusive)
    {
        endExclusive = default;

        AddressValueStatus status = AddressValueOperations.TryGetEndExclusive(
            _value,
            byteLength,
            out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        endExclusive = new VirtualAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryAlignUp(
        ulong alignment,
        out VirtualAddress address)
    {
        address = default;

        AddressValueStatus status = AddressValueOperations.TryAlignUp(
            _value,
            alignment,
            out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new VirtualAddress(value);
        return AddressValueStatus.Success;
    }

    public bool Equals(VirtualAddress other)
    {
        return _value == other._value;
    }
}

/// <summary>
/// Identifies a nonzero device-visible address without implying that a DMA
/// mapping, device ownership record, or IOMMU translation exists.
/// </summary>
public readonly struct DeviceAddress
{
    private readonly ulong _value;

    private DeviceAddress(ulong value)
    {
        _value = value;
    }

    public ulong Value => _value;

    public bool IsValid => _value != 0;

    public static AddressValueStatus TryCreate(ulong value, out DeviceAddress address)
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

    public static AddressValueStatus TryCreateAligned(
        ulong value,
        ulong alignment,
        out DeviceAddress address)
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

        AddressValueStatus status = AddressValueOperations.TryAdd(
            _value,
            byteOffset,
            out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new DeviceAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryGetEndExclusive(
        ulong byteLength,
        out DeviceAddress endExclusive)
    {
        endExclusive = default;

        AddressValueStatus status = AddressValueOperations.TryGetEndExclusive(
            _value,
            byteLength,
            out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        endExclusive = new DeviceAddress(value);
        return AddressValueStatus.Success;
    }

    public AddressValueStatus TryAlignUp(
        ulong alignment,
        out DeviceAddress address)
    {
        address = default;

        AddressValueStatus status = AddressValueOperations.TryAlignUp(
            _value,
            alignment,
            out ulong value);
        if (status != AddressValueStatus.Success)
        {
            return status;
        }

        address = new DeviceAddress(value);
        return AddressValueStatus.Success;
    }

    public bool Equals(DeviceAddress other)
    {
        return _value == other._value;
    }
}

internal static class AddressValueOperations
{
    private const ulong MaximumValue = ~0UL;

    public static AddressValueStatus ValidateAddress(ulong value)
    {
        return value == 0
            ? AddressValueStatus.ZeroAddress
            : AddressValueStatus.Success;
    }

    public static AddressValueStatus ValidateAlignedAddress(ulong value, ulong alignment)
    {
        AddressValueStatus addressStatus = ValidateAddress(value);
        if (addressStatus != AddressValueStatus.Success)
        {
            return addressStatus;
        }

        if (!IsPowerOfTwo(alignment))
        {
            return AddressValueStatus.InvalidAlignment;
        }

        return (value & (alignment - 1)) == 0
            ? AddressValueStatus.Success
            : AddressValueStatus.MisalignedAddress;
    }

    public static bool IsAligned(ulong value, ulong alignment)
    {
        return value != 0
            && IsPowerOfTwo(alignment)
            && (value & (alignment - 1)) == 0;
    }

    public static AddressValueStatus TryAdd(
        ulong value,
        ulong byteOffset,
        out ulong result)
    {
        result = 0;

        if (value == 0)
        {
            return AddressValueStatus.InvalidAddress;
        }

        if (byteOffset > MaximumValue - value)
        {
            return AddressValueStatus.AddressOverflow;
        }

        result = value + byteOffset;
        return AddressValueStatus.Success;
    }

    public static AddressValueStatus TryGetEndExclusive(
        ulong value,
        ulong byteLength,
        out ulong result)
    {
        result = 0;

        if (value == 0)
        {
            return AddressValueStatus.InvalidAddress;
        }

        if (byteLength == 0)
        {
            return AddressValueStatus.ZeroLength;
        }

        if (byteLength > MaximumValue - value)
        {
            return AddressValueStatus.AddressOverflow;
        }

        result = value + byteLength;
        return AddressValueStatus.Success;
    }

    public static AddressValueStatus TryAlignUp(
        ulong value,
        ulong alignment,
        out ulong result)
    {
        result = 0;

        if (value == 0)
        {
            return AddressValueStatus.InvalidAddress;
        }

        if (!IsPowerOfTwo(alignment))
        {
            return AddressValueStatus.InvalidAlignment;
        }

        ulong alignmentMask = alignment - 1;
        ulong remainder = value & alignmentMask;
        if (remainder == 0)
        {
            result = value;
            return AddressValueStatus.Success;
        }

        ulong increment = alignment - remainder;
        if (increment > MaximumValue - value)
        {
            return AddressValueStatus.AddressOverflow;
        }

        result = value + increment;
        return AddressValueStatus.Success;
    }

    private static bool IsPowerOfTwo(ulong value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }
}
