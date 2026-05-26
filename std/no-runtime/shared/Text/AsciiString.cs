using OS.Std;

namespace OS.Std.Text;

public static class AsciiString
{
    public const CompatibilityStatus Status = CompatibilityStatus.Partial;

    public static bool Equals(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    public static bool EqualsIgnoreCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (Ascii.ToUpperInvariant(left[i]) != Ascii.ToUpperInvariant(right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool StartsWithIgnoreCase(ReadOnlySpan<byte> source, ReadOnlySpan<byte> prefix)
    {
        if (prefix.Length > source.Length)
        {
            return false;
        }

        return EqualsIgnoreCaseAt(source, prefix, 0);
    }

    public static bool EndsWithIgnoreCase(ReadOnlySpan<byte> source, ReadOnlySpan<byte> suffix)
    {
        if (suffix.Length > source.Length)
        {
            return false;
        }

        return EqualsIgnoreCaseAt(source, suffix, source.Length - suffix.Length);
    }

    public static int IndexOfIgnoreCase(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return 0;
        }

        if (value.Length > source.Length)
        {
            return -1;
        }

        int lastStart = source.Length - value.Length;
        for (int start = 0; start <= lastStart; start++)
        {
            if (EqualsIgnoreCaseAt(source, value, start))
            {
                return start;
            }
        }

        return -1;
    }

    public static ReadOnlySpan<byte> TrimSpaceOrTab(ReadOnlySpan<byte> source)
    {
        int start = 0;
        int endExclusive = source.Length;

        while (start < endExclusive && Ascii.IsSpaceOrTab(source[start]))
        {
            start++;
        }

        while (endExclusive > start && Ascii.IsSpaceOrTab(source[endExclusive - 1]))
        {
            endExclusive--;
        }

        return source.Slice(start, endExclusive - start);
    }

    public static ReadOnlySpan<byte> TrimTrailingSpace(ReadOnlySpan<byte> source)
    {
        int endExclusive = source.Length;
        while (endExclusive > 0 && source[endExclusive - 1] == 0x20)
        {
            endExclusive--;
        }

        return source[..endExclusive];
    }

    private static bool EqualsIgnoreCaseAt(ReadOnlySpan<byte> source, ReadOnlySpan<byte> value, int start)
    {
        for (int i = 0; i < value.Length; i++)
        {
            if (Ascii.ToUpperInvariant(source[start + i]) != Ascii.ToUpperInvariant(value[i]))
            {
                return false;
            }
        }

        return true;
    }

}
