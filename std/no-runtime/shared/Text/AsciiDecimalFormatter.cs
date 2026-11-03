using OS.Std;

namespace OS.Std.Text;

public static class AsciiDecimalFormatter
{
    private const byte Zero = (byte)'0';
    public const CompatibilityStatus Status = CompatibilityStatus.Partial;
    public static AsciiDecimalFormatResult FormatUInt32(Span<byte> destination, uint value)
    {
        int requiredBytes = CountUInt32Digits(value);
        if (destination.Length < requiredBytes)
        {
            return AsciiDecimalFormatResult.Fail(AsciiDecimalFormatStatus.DestinationTooSmall, destination.Length, requiredBytes);
        }

        WriteUInt32(destination.Slice(0, requiredBytes), value);
        return AsciiDecimalFormatResult.Success(destination.Length, requiredBytes);
    }

    public static AsciiDecimalFormatResult FormatUInt64(Span<byte> destination, ulong value)
    {
        int requiredBytes = CountUInt64Digits(value);
        if (destination.Length < requiredBytes)
        {
            return AsciiDecimalFormatResult.Fail(AsciiDecimalFormatStatus.DestinationTooSmall, destination.Length, requiredBytes);
        }

        WriteUInt64(destination.Slice(0, requiredBytes), value);
        return AsciiDecimalFormatResult.Success(destination.Length, requiredBytes);
    }

    public static AsciiDecimalFormatResult FormatInt32(Span<byte> destination, int value)
    {
        bool isNegative = value < 0;
        uint magnitude = isNegative ? (uint)(-(long)value) : (uint)value;
        int requiredBytes = CountInt32Digits(value);
        if (destination.Length < requiredBytes)
        {
            return AsciiDecimalFormatResult.Fail(AsciiDecimalFormatStatus.DestinationTooSmall, destination.Length, requiredBytes);
        }

        Span<byte> digits = destination.Slice(0, requiredBytes);
        if (isNegative)
        {
            digits[0] = (byte)'-';
            WriteUInt32(digits.Slice(1), magnitude);
        }
        else
        {
            WriteUInt32(digits, magnitude);
        }

        return AsciiDecimalFormatResult.Success(destination.Length, requiredBytes);
    }

    public static AsciiDecimalFormatResult FormatInt64(Span<byte> destination, long value)
    {
        bool isNegative = value < 0;
        ulong magnitude = isNegative ? (ulong)(-(value + 1L)) + 1UL : (ulong)value;
        int requiredBytes = CountInt64Digits(value);
        if (destination.Length < requiredBytes)
        {
            return AsciiDecimalFormatResult.Fail(AsciiDecimalFormatStatus.DestinationTooSmall, destination.Length, requiredBytes);
        }

        Span<byte> digits = destination.Slice(0, requiredBytes);
        if (isNegative)
        {
            digits[0] = (byte)'-';
            WriteUInt64(digits.Slice(1), magnitude);
        }
        else
        {
            WriteUInt64(digits, magnitude);
        }

        return AsciiDecimalFormatResult.Success(destination.Length, requiredBytes);
    }

    public static int CountUInt32Digits(uint value)
    {
        int digits = 1;
        while (value >= 10U)
        {
            value /= 10U;
            digits++;
        }

        return digits;
    }

    public static int CountUInt64Digits(ulong value)
    {
        int digits = 1;
        while (value >= 10UL)
        {
            value /= 10UL;
            digits++;
        }

        return digits;
    }

    public static int CountInt32Digits(int value)
    {
        bool isNegative = value < 0;
        uint magnitude = isNegative ? (uint)(-(long)value) : (uint)value;
        return CountUInt32Digits(magnitude) + (isNegative ? 1 : 0);
    }

    public static int CountInt64Digits(long value)
    {
        bool isNegative = value < 0;
        ulong magnitude = isNegative ? (ulong)(-(value + 1L)) + 1UL : (ulong)value;
        return CountUInt64Digits(magnitude) + (isNegative ? 1 : 0);
    }

    private static void WriteUInt32(Span<byte> destination, uint value)
    {
        for (int index = destination.Length - 1; index >= 0; index--)
        {
            destination[index] = (byte)(Zero + (byte)(value % 10U));
            value /= 10U;
        }
    }

    private static void WriteUInt64(Span<byte> destination, ulong value)
    {
        for (int index = destination.Length - 1; index >= 0; index--)
        {
            destination[index] = (byte)(Zero + (byte)(value % 10UL));
            value /= 10UL;
        }
    }
}
