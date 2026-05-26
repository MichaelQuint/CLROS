namespace OS.Std;

public readonly struct NativeUIntOperationResult
{
    public NativeUIntOperationResult(NativeUIntOperationStatus status, nuint value)
    {
        Status = status;
        Value = value;
        IsInitialized = true;
    }

    public NativeUIntOperationStatus Status { get; }

    public nuint Value { get; }

    public bool IsInitialized { get; }

    public bool Succeeded => IsInitialized && Status == NativeUIntOperationStatus.Success;
}

public enum NativeUIntOperationStatus
{
    Success = 0,
    Overflow = 1,
    Underflow = 2,
    InvalidAlignment = 3
}

public static class NativeInt
{
    public const CompatibilityStatus Status = CompatibilityStatus.Partial;

    public static NativeUIntOperationResult Add(nuint value, nuint offset)
    {
        nuint result = value + offset;
        if (result < value)
        {
            return new NativeUIntOperationResult(NativeUIntOperationStatus.Overflow, 0);
        }

        return new NativeUIntOperationResult(NativeUIntOperationStatus.Success, result);
    }

    public static NativeUIntOperationResult Subtract(nuint value, nuint offset)
    {
        if (offset > value)
        {
            return new NativeUIntOperationResult(NativeUIntOperationStatus.Underflow, 0);
        }

        return new NativeUIntOperationResult(NativeUIntOperationStatus.Success, value - offset);
    }

    public static NativeUIntOperationResult AlignUp(nuint value, nuint alignment)
    {
        if (!IsPowerOfTwo(alignment))
        {
            return new NativeUIntOperationResult(NativeUIntOperationStatus.InvalidAlignment, 0);
        }

        nuint mask = alignment - 1;
        NativeUIntOperationResult add = Add(value, mask);
        if (!add.Succeeded)
        {
            return add;
        }

        return new NativeUIntOperationResult(NativeUIntOperationStatus.Success, add.Value & ~mask);
    }

    public static NativeUIntOperationResult AlignDown(nuint value, nuint alignment)
    {
        if (!IsPowerOfTwo(alignment))
        {
            return new NativeUIntOperationResult(NativeUIntOperationStatus.InvalidAlignment, 0);
        }

        nuint mask = alignment - 1;
        return new NativeUIntOperationResult(NativeUIntOperationStatus.Success, value & ~mask);
    }

    public static bool IsAligned(nuint value, nuint alignment)
    {
        if (!IsPowerOfTwo(alignment))
        {
            return false;
        }

        return (value & (alignment - 1)) == 0;
    }

    public static bool IsPowerOfTwo(nuint value)
    {
        return value != 0 && (value & (value - 1)) == 0;
    }
}
