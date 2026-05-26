using OS.Std;

namespace OS.Std.Text;

public enum AsciiDecimalParseStatus
{
    Success = 0,
    Empty = 1,
    InvalidDigit = 2,
    Overflow = 3,
    SignOnly = 4
}

public readonly struct AsciiUInt32ParseResult
{
    private AsciiUInt32ParseResult(
        bool isInitialized,
        AsciiDecimalParseStatus status,
        uint value,
        int bytesConsumed,
        int invalidInputIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        Value = value;
        BytesConsumed = bytesConsumed;
        InvalidInputIndex = invalidInputIndex;
    }

    public bool IsInitialized { get; }

    public AsciiDecimalParseStatus Status { get; }

    public uint Value { get; }

    public int BytesConsumed { get; }

    public int InvalidInputIndex { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == AsciiDecimalParseStatus.Success &&
        BytesConsumed > 0 &&
        InvalidInputIndex < 0;

    public static AsciiUInt32ParseResult Success(uint value, int bytesConsumed)
    {
        return new AsciiUInt32ParseResult(
            true,
            AsciiDecimalParseStatus.Success,
            value,
            bytesConsumed,
            -1);
    }

    public static AsciiUInt32ParseResult Fail(
        AsciiDecimalParseStatus status,
        int bytesConsumed,
        int invalidInputIndex)
    {
        return new AsciiUInt32ParseResult(
            true,
            status,
            0,
            bytesConsumed,
            invalidInputIndex);
    }
}

public readonly struct AsciiUInt64ParseResult
{
    private AsciiUInt64ParseResult(
        bool isInitialized,
        AsciiDecimalParseStatus status,
        ulong value,
        int bytesConsumed,
        int invalidInputIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        Value = value;
        BytesConsumed = bytesConsumed;
        InvalidInputIndex = invalidInputIndex;
    }

    public bool IsInitialized { get; }

    public AsciiDecimalParseStatus Status { get; }

    public ulong Value { get; }

    public int BytesConsumed { get; }

    public int InvalidInputIndex { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == AsciiDecimalParseStatus.Success &&
        BytesConsumed > 0 &&
        InvalidInputIndex < 0;

    public static AsciiUInt64ParseResult Success(ulong value, int bytesConsumed)
    {
        return new AsciiUInt64ParseResult(
            true,
            AsciiDecimalParseStatus.Success,
            value,
            bytesConsumed,
            -1);
    }

    public static AsciiUInt64ParseResult Fail(
        AsciiDecimalParseStatus status,
        int bytesConsumed,
        int invalidInputIndex)
    {
        return new AsciiUInt64ParseResult(
            true,
            status,
            0,
            bytesConsumed,
            invalidInputIndex);
    }
}

public readonly struct AsciiInt32ParseResult
{
    private AsciiInt32ParseResult(
        bool isInitialized,
        AsciiDecimalParseStatus status,
        int value,
        int bytesConsumed,
        int invalidInputIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        Value = value;
        BytesConsumed = bytesConsumed;
        InvalidInputIndex = invalidInputIndex;
    }

    public bool IsInitialized { get; }

    public AsciiDecimalParseStatus Status { get; }

    public int Value { get; }

    public int BytesConsumed { get; }

    public int InvalidInputIndex { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == AsciiDecimalParseStatus.Success &&
        BytesConsumed > 0 &&
        InvalidInputIndex < 0;

    public static AsciiInt32ParseResult Success(int value, int bytesConsumed)
    {
        return new AsciiInt32ParseResult(
            true,
            AsciiDecimalParseStatus.Success,
            value,
            bytesConsumed,
            -1);
    }

    public static AsciiInt32ParseResult Fail(
        AsciiDecimalParseStatus status,
        int bytesConsumed,
        int invalidInputIndex)
    {
        return new AsciiInt32ParseResult(
            true,
            status,
            0,
            bytesConsumed,
            invalidInputIndex);
    }
}

public readonly struct AsciiInt64ParseResult
{
    private AsciiInt64ParseResult(
        bool isInitialized,
        AsciiDecimalParseStatus status,
        long value,
        int bytesConsumed,
        int invalidInputIndex)
    {
        IsInitialized = isInitialized;
        Status = status;
        Value = value;
        BytesConsumed = bytesConsumed;
        InvalidInputIndex = invalidInputIndex;
    }

    public bool IsInitialized { get; }

    public AsciiDecimalParseStatus Status { get; }

    public long Value { get; }

    public int BytesConsumed { get; }

    public int InvalidInputIndex { get; }

    public bool Succeeded =>
        IsInitialized &&
        Status == AsciiDecimalParseStatus.Success &&
        BytesConsumed > 0 &&
        InvalidInputIndex < 0;

    public static AsciiInt64ParseResult Success(long value, int bytesConsumed)
    {
        return new AsciiInt64ParseResult(
            true,
            AsciiDecimalParseStatus.Success,
            value,
            bytesConsumed,
            -1);
    }

    public static AsciiInt64ParseResult Fail(
        AsciiDecimalParseStatus status,
        int bytesConsumed,
        int invalidInputIndex)
    {
        return new AsciiInt64ParseResult(
            true,
            status,
            0,
            bytesConsumed,
            invalidInputIndex);
    }
}

public static class AsciiDecimalParser
{
    private const byte Zero = (byte)'0';
    private const byte Minus = (byte)'-';
    private const byte Plus = (byte)'+';
    private const uint UInt32MaxDiv10 = uint.MaxValue / 10U;
    private const uint UInt32MaxMod10 = uint.MaxValue % 10U;
    private const ulong UInt64MaxDiv10 = ulong.MaxValue / 10UL;
    private const ulong UInt64MaxMod10 = ulong.MaxValue % 10UL;
    private const uint Int32MaxMagnitude = int.MaxValue;
    private const uint Int32MinMagnitude = 2147483648U;
    private const ulong Int64MaxMagnitude = long.MaxValue;
    private const ulong Int64MinMagnitude = 9223372036854775808UL;

    public const CompatibilityStatus Status = CompatibilityStatus.Partial;

    public static AsciiUInt32ParseResult ParseUInt32(ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
        {
            return AsciiUInt32ParseResult.Fail(AsciiDecimalParseStatus.Empty, 0, -1);
        }

        uint value = 0;
        for (int index = 0; index < source.Length; index++)
        {
            byte current = source[index];
            if (!Ascii.IsDigit(current))
            {
                return AsciiUInt32ParseResult.Fail(
                    AsciiDecimalParseStatus.InvalidDigit,
                    index,
                    index);
            }

            uint digit = (uint)(current - Zero);
            if (value > UInt32MaxDiv10 ||
                (value == UInt32MaxDiv10 && digit > UInt32MaxMod10))
            {
                return AsciiUInt32ParseResult.Fail(
                    AsciiDecimalParseStatus.Overflow,
                    index,
                    index);
            }

            value = (value * 10U) + digit;
        }

        return AsciiUInt32ParseResult.Success(value, source.Length);
    }

    public static AsciiUInt64ParseResult ParseUInt64(ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
        {
            return AsciiUInt64ParseResult.Fail(AsciiDecimalParseStatus.Empty, 0, -1);
        }

        ulong value = 0;
        for (int index = 0; index < source.Length; index++)
        {
            byte current = source[index];
            if (!Ascii.IsDigit(current))
            {
                return AsciiUInt64ParseResult.Fail(
                    AsciiDecimalParseStatus.InvalidDigit,
                    index,
                    index);
            }

            ulong digit = (ulong)(current - Zero);
            if (value > UInt64MaxDiv10 ||
                (value == UInt64MaxDiv10 && digit > UInt64MaxMod10))
            {
                return AsciiUInt64ParseResult.Fail(
                    AsciiDecimalParseStatus.Overflow,
                    index,
                    index);
            }

            value = (value * 10UL) + digit;
        }

        return AsciiUInt64ParseResult.Success(value, source.Length);
    }

    public static AsciiInt32ParseResult ParseInt32(ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
        {
            return AsciiInt32ParseResult.Fail(AsciiDecimalParseStatus.Empty, 0, -1);
        }

        int digitStart = 0;
        bool isNegative = false;
        byte first = source[0];
        if (first == Minus)
        {
            isNegative = true;
            digitStart = 1;
        }
        else if (first == Plus)
        {
            return AsciiInt32ParseResult.Fail(
                AsciiDecimalParseStatus.InvalidDigit,
                0,
                0);
        }

        if (digitStart == source.Length)
        {
            return AsciiInt32ParseResult.Fail(
                AsciiDecimalParseStatus.SignOnly,
                source.Length,
                -1);
        }

        uint limit = isNegative ? Int32MinMagnitude : Int32MaxMagnitude;
        uint limitDiv10 = limit / 10U;
        uint limitMod10 = limit % 10U;
        uint magnitude = 0;

        for (int index = digitStart; index < source.Length; index++)
        {
            byte current = source[index];
            if (!Ascii.IsDigit(current))
            {
                return AsciiInt32ParseResult.Fail(
                    AsciiDecimalParseStatus.InvalidDigit,
                    index,
                    index);
            }

            uint digit = (uint)(current - Zero);
            if (magnitude > limitDiv10 ||
                (magnitude == limitDiv10 && digit > limitMod10))
            {
                return AsciiInt32ParseResult.Fail(
                    AsciiDecimalParseStatus.Overflow,
                    index,
                    index);
            }

            magnitude = (magnitude * 10U) + digit;
        }

        int value = isNegative
            ? magnitude == Int32MinMagnitude ? int.MinValue : -(int)magnitude
            : (int)magnitude;
        return AsciiInt32ParseResult.Success(value, source.Length);
    }

    public static AsciiInt64ParseResult ParseInt64(ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
        {
            return AsciiInt64ParseResult.Fail(AsciiDecimalParseStatus.Empty, 0, -1);
        }

        int digitStart = 0;
        bool isNegative = false;
        byte first = source[0];
        if (first == Minus)
        {
            isNegative = true;
            digitStart = 1;
        }
        else if (first == Plus)
        {
            return AsciiInt64ParseResult.Fail(
                AsciiDecimalParseStatus.InvalidDigit,
                0,
                0);
        }

        if (digitStart == source.Length)
        {
            return AsciiInt64ParseResult.Fail(
                AsciiDecimalParseStatus.SignOnly,
                source.Length,
                -1);
        }

        ulong limit = isNegative ? Int64MinMagnitude : Int64MaxMagnitude;
        ulong limitDiv10 = limit / 10UL;
        ulong limitMod10 = limit % 10UL;
        ulong magnitude = 0;

        for (int index = digitStart; index < source.Length; index++)
        {
            byte current = source[index];
            if (!Ascii.IsDigit(current))
            {
                return AsciiInt64ParseResult.Fail(
                    AsciiDecimalParseStatus.InvalidDigit,
                    index,
                    index);
            }

            ulong digit = (ulong)(current - Zero);
            if (magnitude > limitDiv10 ||
                (magnitude == limitDiv10 && digit > limitMod10))
            {
                return AsciiInt64ParseResult.Fail(
                    AsciiDecimalParseStatus.Overflow,
                    index,
                    index);
            }

            magnitude = (magnitude * 10UL) + digit;
        }

        long value = isNegative
            ? magnitude == Int64MinMagnitude ? long.MinValue : -(long)magnitude
            : (long)magnitude;
        return AsciiInt64ParseResult.Success(value, source.Length);
    }

    public static bool TryParseUInt32(ReadOnlySpan<byte> source, out uint value)
    {
        AsciiUInt32ParseResult result = ParseUInt32(source);
        value = result.Succeeded ? result.Value : 0;
        return result.Succeeded;
    }

    public static bool TryParseUInt64(ReadOnlySpan<byte> source, out ulong value)
    {
        AsciiUInt64ParseResult result = ParseUInt64(source);
        value = result.Succeeded ? result.Value : 0;
        return result.Succeeded;
    }

    public static bool TryParseInt32(ReadOnlySpan<byte> source, out int value)
    {
        AsciiInt32ParseResult result = ParseInt32(source);
        value = result.Succeeded ? result.Value : 0;
        return result.Succeeded;
    }

    public static bool TryParseInt64(ReadOnlySpan<byte> source, out long value)
    {
        AsciiInt64ParseResult result = ParseInt64(source);
        value = result.Succeeded ? result.Value : 0;
        return result.Succeeded;
    }

}
