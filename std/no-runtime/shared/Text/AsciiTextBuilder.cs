using OS.Std;
using OS.Std.Memory;

namespace OS.Std.Text;
public static class AsciiTextBuilder
{
    public const CompatibilityStatus Status = CompatibilityStatus.Partial;
    public static AsciiTextBuilderResult AppendByte(Span<byte> destination, int offset, byte value)
    {
        if (!TryValidateOffset(destination, offset, out AsciiTextBuilderResult offsetFailure))
        {
            return offsetFailure;
        }

        if (!IsAllowedAsciiText(value))
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.NonAsciiInput, offset, destination.Length, offset, 0);
        }

        if (!TryCalculateRequiredBytes(offset, 1, out int requiredBytes))
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.LengthOverflow, offset, destination.Length, 0);
        }

        if (requiredBytes > destination.Length)
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.DestinationTooSmall, offset, destination.Length, requiredBytes);
        }

        destination[offset] = value;
        return AsciiTextBuilderResult.Success(offset, destination.Length, 1);
    }

    public static AsciiTextBuilderResult AppendBytes(Span<byte> destination, int offset, ReadOnlySpan<byte> value)
    {
        if (!TryValidateOffset(destination, offset, out AsciiTextBuilderResult offsetFailure))
        {
            return offsetFailure;
        }

        for (int index = 0; index < value.Length; index++)
        {
            if (!IsAllowedAsciiText(value[index]))
            {
                return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.NonAsciiInput, offset, destination.Length, offset + index, index);
            }
        }

        if (!TryCalculateRequiredBytes(offset, value.Length, out int requiredBytes))
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.LengthOverflow, offset, destination.Length, 0);
        }

        if (requiredBytes > destination.Length)
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.DestinationTooSmall, offset, destination.Length, requiredBytes);
        }

        BufferOperations.MemoryCopy(value, destination.Slice(offset, value.Length));
        return AsciiTextBuilderResult.Success(offset, destination.Length, value.Length);
    }

    public static AsciiTextBuilderResult AppendChars(Span<byte> destination, int offset, ReadOnlySpan<char> value)
    {
        if (!TryValidateOffset(destination, offset, out AsciiTextBuilderResult offsetFailure))
        {
            return offsetFailure;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (current > 0x7F || !IsAllowedAsciiText((byte)current))
            {
                return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.NonAsciiInput, offset, destination.Length, offset + index, index);
            }
        }

        if (!TryCalculateRequiredBytes(offset, value.Length, out int requiredBytes))
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.LengthOverflow, offset, destination.Length, 0);
        }

        if (requiredBytes > destination.Length)
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.DestinationTooSmall, offset, destination.Length, requiredBytes);
        }

        for (int index = 0; index < value.Length; index++)
        {
            destination[offset + index] = (byte)value[index];
        }

        return AsciiTextBuilderResult.Success(offset, destination.Length, value.Length);
    }

    public static AsciiTextBuilderResult AppendUInt64Decimal(Span<byte> destination, int offset, ulong value)
    {
        if (!TryValidateOffset(destination, offset, out AsciiTextBuilderResult offsetFailure))
        {
            return offsetFailure;
        }

        int digitCount = CountDecimalDigits(value);
        if (!TryCalculateRequiredBytes(offset, digitCount, out int requiredBytes))
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.LengthOverflow, offset, destination.Length, 0);
        }

        if (requiredBytes > destination.Length)
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.DestinationTooSmall, offset, destination.Length, requiredBytes);
        }

        WriteUInt64Decimal(destination.Slice(offset, digitCount), value);
        return AsciiTextBuilderResult.Success(offset, destination.Length, digitCount);
    }

    public static AsciiTextBuilderResult AppendInt64Decimal(Span<byte> destination, int offset, long value)
    {
        if (!TryValidateOffset(destination, offset, out AsciiTextBuilderResult offsetFailure))
        {
            return offsetFailure;
        }

        bool negative = value < 0;
        ulong magnitude;
        if (value == long.MinValue)
        {
            magnitude = ((ulong)long.MaxValue) + 1UL;
        }
        else if (negative)
        {
            magnitude = (ulong)(-value);
        }
        else
        {
            magnitude = (ulong)value;
        }

        int bytesToWrite = CountDecimalDigits(magnitude) + (negative ? 1 : 0);
        if (!TryCalculateRequiredBytes(offset, bytesToWrite, out int requiredBytes))
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.LengthOverflow, offset, destination.Length, 0);
        }

        if (requiredBytes > destination.Length)
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.DestinationTooSmall, offset, destination.Length, requiredBytes);
        }

        int digitOffset = offset;
        if (negative)
        {
            destination[offset] = (byte)'-';
            digitOffset++;
        }

        WriteUInt64Decimal(destination.Slice(digitOffset, bytesToWrite - (negative ? 1 : 0)), magnitude);
        return AsciiTextBuilderResult.Success(offset, destination.Length, bytesToWrite);
    }

    public static AsciiTextBuilderResult AppendUInt64Hex(Span<byte> destination, int offset, ulong value, int minimumDigits, bool includePrefix)
    {
        if (!TryValidateOffset(destination, offset, out AsciiTextBuilderResult offsetFailure))
        {
            return offsetFailure;
        }

        if (minimumDigits < 1 || minimumDigits > 16)
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.InvalidMinimumDigits, offset, destination.Length, offset);
        }

        int significantDigits = CountHexDigits(value);
        int digitCount = significantDigits > minimumDigits ? significantDigits : minimumDigits;
        int bytesToWrite = digitCount + (includePrefix ? 2 : 0);
        if (!TryCalculateRequiredBytes(offset, bytesToWrite, out int requiredBytes))
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.LengthOverflow, offset, destination.Length, 0);
        }

        if (requiredBytes > destination.Length)
        {
            return AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.DestinationTooSmall, offset, destination.Length, requiredBytes);
        }

        int writeOffset = offset;
        if (includePrefix)
        {
            destination[writeOffset] = (byte)'0';
            destination[writeOffset + 1] = (byte)'x';
            writeOffset += 2;
        }

        for (int index = 0; index < digitCount; index++)
        {
            int shift = (digitCount - index - 1) * 4;
            byte nibble = (byte)((value >> shift) & 0xFUL);
            destination[writeOffset + index] = ToUpperHexDigit(nibble);
        }

        return AsciiTextBuilderResult.Success(offset, destination.Length, bytesToWrite);
    }

    private static bool TryValidateOffset(Span<byte> destination, int offset, out AsciiTextBuilderResult failure)
    {
        if (offset < 0 || offset > destination.Length)
        {
            failure = AsciiTextBuilderResult.Fail(AsciiTextBuilderStatus.InvalidOffset, offset, destination.Length, 0);
            return false;
        }

        failure = default;
        return true;
    }

    private static bool TryCalculateRequiredBytes(int offset, int appendLength, out int requiredBytes)
    {
        if (appendLength < 0 || offset > int.MaxValue - appendLength)
        {
            requiredBytes = 0;
            return false;
        }

        requiredBytes = offset + appendLength;
        return true;
    }

    private static bool IsAllowedAsciiText(byte value)
    {
        return Ascii.IsPrintable(value) || value == (byte)'\r' || value == (byte)'\n' || value == (byte)'\t';
    }

    private static int CountDecimalDigits(ulong value)
    {
        int digits = 1;
        while (value >= 10)
        {
            value /= 10;
            digits++;
        }

        return digits;
    }

    private static void WriteUInt64Decimal(Span<byte> destination, ulong value)
    {
        for (int index = destination.Length - 1; index >= 0; index--)
        {
            destination[index] = (byte)('0' + (byte)(value % 10));
            value /= 10;
        }
    }

    private static int CountHexDigits(ulong value)
    {
        int digits = 1;
        while (value > 0xFUL)
        {
            value >>= 4;
            digits++;
        }

        return digits;
    }

    private static byte ToUpperHexDigit(byte value)
    {
        return value < 10 ? (byte)('0' + value) : (byte)('A' + (value - 10));
    }
}
