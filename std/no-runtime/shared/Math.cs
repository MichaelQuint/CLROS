namespace OS.Std;

public static class Math
{
    public const CompatibilityStatus Status = CompatibilityStatus.Partial;

    public static int Abs(int value)
    {
        if (value == int.MinValue)
        {
            throw new OverflowException("Negating the minimum value is not supported.");
        }

        return value < 0 ? -value : value;
    }

    public static long Abs(long value)
    {
        if (value == long.MinValue)
        {
            throw new OverflowException("Negating the minimum value is not supported.");
        }

        return value < 0 ? -value : value;
    }

    public static int Clamp(int value, int min, int max)
    {
        ValidateClampBounds(min, max);

        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    public static uint Clamp(uint value, uint min, uint max)
    {
        ValidateClampBounds(min, max);

        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    public static long Clamp(long value, long min, long max)
    {
        ValidateClampBounds(min, max);

        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    public static ulong Clamp(ulong value, ulong min, ulong max)
    {
        ValidateClampBounds(min, max);

        if (value < min)
        {
            return min;
        }

        return value > max ? max : value;
    }

    public static int Max(int value1, int value2)
    {
        return value1 >= value2 ? value1 : value2;
    }

    public static uint Max(uint value1, uint value2)
    {
        return value1 >= value2 ? value1 : value2;
    }

    public static long Max(long value1, long value2)
    {
        return value1 >= value2 ? value1 : value2;
    }

    public static ulong Max(ulong value1, ulong value2)
    {
        return value1 >= value2 ? value1 : value2;
    }

    public static int Min(int value1, int value2)
    {
        return value1 <= value2 ? value1 : value2;
    }

    public static uint Min(uint value1, uint value2)
    {
        return value1 <= value2 ? value1 : value2;
    }

    public static long Min(long value1, long value2)
    {
        return value1 <= value2 ? value1 : value2;
    }

    public static ulong Min(ulong value1, ulong value2)
    {
        return value1 <= value2 ? value1 : value2;
    }

    private static void ValidateClampBounds(int min, int max)
    {
        if (min > max)
        {
            throw new ArgumentException("Minimum bound must not be greater than maximum bound.", nameof(min));
        }
    }

    private static void ValidateClampBounds(uint min, uint max)
    {
        if (min > max)
        {
            throw new ArgumentException("Minimum bound must not be greater than maximum bound.", nameof(min));
        }
    }

    private static void ValidateClampBounds(long min, long max)
    {
        if (min > max)
        {
            throw new ArgumentException("Minimum bound must not be greater than maximum bound.", nameof(min));
        }
    }

    private static void ValidateClampBounds(ulong min, ulong max)
    {
        if (min > max)
        {
            throw new ArgumentException("Minimum bound must not be greater than maximum bound.", nameof(min));
        }
    }
}
