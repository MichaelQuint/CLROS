namespace OS.Boot;
internal static class AddressValueOperations
{
    private const ulong MaximumValue = ~0UL;
    public static AddressValueStatus ValidateAddress(ulong value)
    {
        return value == 0 ? AddressValueStatus.ZeroAddress : AddressValueStatus.Success;
    }

    public static AddressValueStatus TryGetRawValue(ulong value, out ulong result)
    {
        result = 0;
        if (value == 0)
        {
            return AddressValueStatus.InvalidAddress;
        }

        result = value;
        return AddressValueStatus.Success;
    }

    public static AddressValueStatus TryCompare(ulong left, ulong right, out int comparison)
    {
        comparison = 0;
        if (left == 0 || right == 0)
        {
            return AddressValueStatus.InvalidAddress;
        }

        if (left < right)
        {
            comparison = -1;
        }
        else if (left > right)
        {
            comparison = 1;
        }

        return AddressValueStatus.Success;
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

        return (value & (alignment - 1)) == 0 ? AddressValueStatus.Success : AddressValueStatus.MisalignedAddress;
    }

    public static bool IsAligned(ulong value, ulong alignment)
    {
        return value != 0 && IsPowerOfTwo(alignment) && (value & (alignment - 1)) == 0;
    }

    public static AddressValueStatus TryAdd(ulong value, ulong byteOffset, out ulong result)
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

    public static AddressValueStatus TryGetEndExclusive(ulong value, ulong byteLength, out ulong result)
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

    public static AddressValueStatus TryAlignUp(ulong value, ulong alignment, out ulong result)
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
