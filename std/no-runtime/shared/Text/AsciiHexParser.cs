using OS.Std;

namespace OS.Std.Text;
public static partial class AsciiHexParser
{
    private const byte Zero = (byte)'0';
    private const byte UpperA = (byte)'A';
    private const byte LowerA = (byte)'a';
    private const byte PrefixZero = (byte)'0';
    private const byte PrefixLowerX = (byte)'x';
    private const byte PrefixUpperX = (byte)'X';
    private const uint UInt32MaxDiv16 = uint.MaxValue >> 4;
    private const uint UInt32MaxMod16 = uint.MaxValue & 0xFU;
    private const ulong UInt64MaxDiv16 = ulong.MaxValue >> 4;
    private const ulong UInt64MaxMod16 = ulong.MaxValue & 0xFUL;
    public const CompatibilityStatus Status = CompatibilityStatus.Partial;
    public static AsciiHexUInt32ParseResult ParseUInt32(ReadOnlySpan<byte> source)
    {
        int digitStart = GetDigitStart(source);
        if (digitStart < 0)
        {
            return AsciiHexUInt32ParseResult.Fail(AsciiHexParseStatus.Empty, 0, -1);
        }

        if (digitStart == source.Length)
        {
            return AsciiHexUInt32ParseResult.Fail(AsciiHexParseStatus.PrefixOnly, source.Length, -1);
        }

        uint value = 0;
        for (int index = digitStart; index < source.Length; index++)
        {
            if (!TryGetHexValue(source[index], out byte digit))
            {
                return AsciiHexUInt32ParseResult.Fail(AsciiHexParseStatus.NonHexDigit, index, index);
            }

            if (value > UInt32MaxDiv16 || (value == UInt32MaxDiv16 && digit > UInt32MaxMod16))
            {
                return AsciiHexUInt32ParseResult.Fail(AsciiHexParseStatus.Overflow, index, index);
            }

            value = (value << 4) | digit;
        }

        return AsciiHexUInt32ParseResult.Success(value, source.Length);
    }

    public static AsciiHexUInt64ParseResult ParseUInt64(ReadOnlySpan<byte> source)
    {
        int digitStart = GetDigitStart(source);
        if (digitStart < 0)
        {
            return AsciiHexUInt64ParseResult.Fail(AsciiHexParseStatus.Empty, 0, -1);
        }

        if (digitStart == source.Length)
        {
            return AsciiHexUInt64ParseResult.Fail(AsciiHexParseStatus.PrefixOnly, source.Length, -1);
        }

        ulong value = 0;
        for (int index = digitStart; index < source.Length; index++)
        {
            if (!TryGetHexValue(source[index], out byte digit))
            {
                return AsciiHexUInt64ParseResult.Fail(AsciiHexParseStatus.NonHexDigit, index, index);
            }

            if (value > UInt64MaxDiv16 || (value == UInt64MaxDiv16 && digit > UInt64MaxMod16))
            {
                return AsciiHexUInt64ParseResult.Fail(AsciiHexParseStatus.Overflow, index, index);
            }

            value = (value << 4) | digit;
        }

        return AsciiHexUInt64ParseResult.Success(value, source.Length);
    }

    public static bool TryParseUInt32(ReadOnlySpan<byte> source, out uint value)
    {
        AsciiHexUInt32ParseResult result = ParseUInt32(source);
        value = result.Succeeded ? result.Value : 0;
        return result.Succeeded;
    }

    public static bool TryParseUInt64(ReadOnlySpan<byte> source, out ulong value)
    {
        AsciiHexUInt64ParseResult result = ParseUInt64(source);
        value = result.Succeeded ? result.Value : 0;
        return result.Succeeded;
    }

    private static int GetDigitStart(ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
        {
            return -1;
        }

        if (source.Length >= 2 && source[0] == PrefixZero && (source[1] == PrefixLowerX || source[1] == PrefixUpperX))
        {
            return 2;
        }

        return 0;
    }

    private static bool TryGetHexValue(byte value, out byte digit)
    {
        if (Ascii.IsDigit(value))
        {
            digit = (byte)(value - Zero);
            return true;
        }

        if (value >= UpperA && value <= (byte)'F')
        {
            digit = (byte)(value - UpperA + 10);
            return true;
        }

        if (value >= LowerA && value <= (byte)'f')
        {
            digit = (byte)(value - LowerA + 10);
            return true;
        }

        digit = 0;
        return false;
    }
}
