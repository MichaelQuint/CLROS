using OS.Std;

namespace OS.Std.Text;

public static class AsciiKeyValueSplitter
{
    public const CompatibilityStatus Status = CompatibilityStatus.Partial;
    public static AsciiKeyValueSplitResult Split(ReadOnlySpan<byte> source, byte delimiter)
    {
        return SplitRange(source, 0, source.Length, delimiter, AsciiTokenSplitOptions.None);
    }

    public static AsciiKeyValueSplitResult Split(ReadOnlySpan<byte> source, byte delimiter, AsciiTokenSplitOptions options)
    {
        return SplitRange(source, 0, source.Length, delimiter, options);
    }

    public static AsciiKeyValueSplitResult SplitRange(ReadOnlySpan<byte> source, int start, int length, byte delimiter)
    {
        return SplitRange(source, start, length, delimiter, AsciiTokenSplitOptions.None);
    }

    public static AsciiKeyValueSplitResult SplitRange(ReadOnlySpan<byte> source, int start, int length, byte delimiter, AsciiTokenSplitOptions options)
    {
        if (!IsValidRange(source.Length, start, length))
        {
            return AsciiKeyValueSplitResult.Fail(AsciiKeyValueSplitStatus.InvalidRange, start, length, delimiter);
        }

        if (!IsAllowedDelimiter(delimiter))
        {
            return AsciiKeyValueSplitResult.Fail(AsciiKeyValueSplitStatus.InvalidDelimiter, start, length, delimiter);
        }

        if (!AreValidOptions(options))
        {
            return AsciiKeyValueSplitResult.Fail(AsciiKeyValueSplitStatus.InvalidOptions, start, length, delimiter);
        }

        int end = start + length;
        int delimiterIndex = -1;
        for (int index = start; index < end; index++)
        {
            byte current = source[index];
            if (!IsAllowedInputByte(current, delimiter))
            {
                return AsciiKeyValueSplitResult.Fail(AsciiKeyValueSplitStatus.NonAsciiInput, start, length, delimiter, index);
            }

            if (delimiterIndex < 0 && current == delimiter)
            {
                delimiterIndex = index;
            }
        }

        if (delimiterIndex < 0)
        {
            return AsciiKeyValueSplitResult.Fail(AsciiKeyValueSplitStatus.MissingDelimiter, start, length, delimiter);
        }

        GetRange(source, start, delimiterIndex - start, options, out int keyStart, out int keyLength);
        if (keyLength == 0)
        {
            return AsciiKeyValueSplitResult.Fail(AsciiKeyValueSplitStatus.EmptyKey, start, length, delimiter);
        }

        int valueStart = delimiterIndex + 1;
        GetRange(source, valueStart, end - valueStart, options, out int normalizedValueStart, out int valueLength);
        return AsciiKeyValueSplitResult.Success(start, length, delimiterIndex, delimiter, AsciiTokenRange.Create(keyStart, keyLength), AsciiTokenRange.Create(normalizedValueStart, valueLength));
    }

    private static bool IsValidRange(int sourceLength, int start, int length)
    {
        return start >= 0 && length >= 0 && start <= sourceLength && length <= sourceLength - start;
    }

    private static bool IsAllowedDelimiter(byte value)
    {
        return value != 0 && value <= 0x7F && (Ascii.IsPrintable(value) || Ascii.IsWhiteSpace(value));
    }

    private static bool AreValidOptions(AsciiTokenSplitOptions options)
    {
        const AsciiTokenSplitOptions allowed = AsciiTokenSplitOptions.TrimWhiteSpace;
        return (options & ~allowed) == 0;
    }

    private static void GetRange(ReadOnlySpan<byte> source, int start, int length, AsciiTokenSplitOptions options, out int normalizedStart, out int normalizedLength)
    {
        int rangeStart = start;
        int rangeEnd = start + length;
        if ((options & AsciiTokenSplitOptions.TrimWhiteSpace) != 0)
        {
            while (rangeStart < rangeEnd && Ascii.IsWhiteSpace(source[rangeStart]))
            {
                rangeStart++;
            }

            while (rangeEnd > rangeStart && Ascii.IsWhiteSpace(source[rangeEnd - 1]))
            {
                rangeEnd--;
            }
        }

        normalizedStart = rangeStart;
        normalizedLength = rangeEnd - rangeStart;
    }

    private static bool IsAllowedInputByte(byte value, byte delimiter)
    {
        return value == delimiter || Ascii.IsPrintable(value) || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
    }
}
