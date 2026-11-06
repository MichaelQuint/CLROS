using OS.Std;

namespace OS.Std.Text;

public static class AsciiTokenSplitter
{
    public const CompatibilityStatus Status = CompatibilityStatus.Partial;
    public static AsciiTokenSplitResult Split(ReadOnlySpan<byte> source, byte delimiter, Span<AsciiTokenRange> destination)
    {
        return SplitRange(source, 0, source.Length, delimiter, destination, AsciiTokenSplitOptions.None);
    }

    public static AsciiTokenSplitResult Split(ReadOnlySpan<byte> source, byte delimiter, Span<AsciiTokenRange> destination, AsciiTokenSplitOptions options)
    {
        return SplitRange(source, 0, source.Length, delimiter, destination, options);
    }

    public static AsciiTokenSplitResult SplitRange(ReadOnlySpan<byte> source, int start, int length, byte delimiter, Span<AsciiTokenRange> destination)
    {
        return SplitRange(source, start, length, delimiter, destination, AsciiTokenSplitOptions.None);
    }

    public static AsciiTokenSplitResult SplitRange(ReadOnlySpan<byte> source, int start, int length, byte delimiter, Span<AsciiTokenRange> destination, AsciiTokenSplitOptions options)
    {
        if (!IsValidRange(source.Length, start, length))
        {
            return AsciiTokenSplitResult.Fail(AsciiTokenSplitStatus.InvalidRange, start, length, 0, delimiter);
        }

        if (!IsAllowedDelimiter(delimiter))
        {
            return AsciiTokenSplitResult.Fail(AsciiTokenSplitStatus.InvalidDelimiter, start, length, 0, delimiter);
        }

        if (!AreValidOptions(options))
        {
            return AsciiTokenSplitResult.Fail(AsciiTokenSplitStatus.InvalidOptions, start, length, 0, delimiter);
        }

        int end = start + length;
        for (int index = start; index < end; index++)
        {
            byte current = source[index];
            if (current == delimiter)
            {
                continue;
            }

            if (!IsAllowedTokenByte(current))
            {
                return AsciiTokenSplitResult.Fail(AsciiTokenSplitStatus.NonAsciiInput, start, length, 0, delimiter, index);
            }
        }

        int requiredTokenCount = CountTokens(source, start, end, delimiter, options);
        if (destination.Length < requiredTokenCount)
        {
            return AsciiTokenSplitResult.Fail(AsciiTokenSplitStatus.DestinationTooSmall, start, length, requiredTokenCount, delimiter);
        }

        int tokenIndex = WriteTokens(source, start, end, delimiter, options, destination);
        return AsciiTokenSplitResult.Success(start, length, tokenIndex, delimiter);
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
        const AsciiTokenSplitOptions allowed = AsciiTokenSplitOptions.TrimWhiteSpace | AsciiTokenSplitOptions.RemoveEmptyEntries;
        return (options & ~allowed) == 0;
    }

    private static int CountTokens(ReadOnlySpan<byte> source, int start, int end, byte delimiter, AsciiTokenSplitOptions options)
    {
        int tokenCount = 0;
        int tokenStart = start;
        for (int index = start; index <= end; index++)
        {
            if (index != end && source[index] != delimiter)
            {
                continue;
            }

            GetTokenRange(source, tokenStart, index - tokenStart, options, out _, out int tokenLength);
            if (!ShouldSkipToken(tokenLength, options))
            {
                tokenCount++;
            }

            tokenStart = index + 1;
        }

        return tokenCount;
    }

    private static int WriteTokens(ReadOnlySpan<byte> source, int start, int end, byte delimiter, AsciiTokenSplitOptions options, Span<AsciiTokenRange> destination)
    {
        int tokenIndex = 0;
        int tokenStart = start;
        for (int index = start; index <= end; index++)
        {
            if (index != end && source[index] != delimiter)
            {
                continue;
            }

            GetTokenRange(source, tokenStart, index - tokenStart, options, out int normalizedStart, out int tokenLength);
            if (!ShouldSkipToken(tokenLength, options))
            {
                destination[tokenIndex] = AsciiTokenRange.Create(normalizedStart, tokenLength);
                tokenIndex++;
            }

            tokenStart = index + 1;
        }

        return tokenIndex;
    }

    private static void GetTokenRange(ReadOnlySpan<byte> source, int start, int length, AsciiTokenSplitOptions options, out int normalizedStart, out int normalizedLength)
    {
        int tokenStart = start;
        int tokenEnd = start + length;
        if ((options & AsciiTokenSplitOptions.TrimWhiteSpace) != 0)
        {
            while (tokenStart < tokenEnd && Ascii.IsWhiteSpace(source[tokenStart]))
            {
                tokenStart++;
            }

            while (tokenEnd > tokenStart && Ascii.IsWhiteSpace(source[tokenEnd - 1]))
            {
                tokenEnd--;
            }
        }

        normalizedStart = tokenStart;
        normalizedLength = tokenEnd - tokenStart;
    }

    private static bool ShouldSkipToken(int tokenLength, AsciiTokenSplitOptions options)
    {
        return tokenLength == 0 && (options & AsciiTokenSplitOptions.RemoveEmptyEntries) != 0;
    }

    private static bool IsAllowedTokenByte(byte value)
    {
        return Ascii.IsPrintable(value) || value == (byte)'\t' || value == (byte)'\r' || value == (byte)'\n';
    }
}
