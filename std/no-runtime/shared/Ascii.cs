namespace OS.Std;

public static class Ascii
{
    public static bool IsDigit(byte value)
    {
        return value >= (byte)'0' && value <= (byte)'9';
    }

    public static bool IsDigit(char value)
    {
        return value >= '0' && value <= '9';
    }

    public static bool IsLetter(byte value)
    {
        return IsUpperLetter(value) || IsLowerLetter(value);
    }

    public static bool IsLetter(char value)
    {
        return IsUpperLetter(value) || IsLowerLetter(value);
    }

    public static bool IsUpperLetter(byte value)
    {
        return value >= (byte)'A' && value <= (byte)'Z';
    }

    public static bool IsUpperLetter(char value)
    {
        return value >= 'A' && value <= 'Z';
    }

    public static bool IsLowerLetter(byte value)
    {
        return value >= (byte)'a' && value <= (byte)'z';
    }

    public static bool IsLowerLetter(char value)
    {
        return value >= 'a' && value <= 'z';
    }

    public static bool IsLetterOrDigit(byte value)
    {
        return IsLetter(value) || IsDigit(value);
    }

    public static bool IsLetterOrDigit(char value)
    {
        return IsLetter(value) || IsDigit(value);
    }

    public static bool IsWhiteSpace(byte value)
    {
        return value == (byte)' '
            || value == (byte)'\t'
            || value == (byte)'\n'
            || value == (byte)'\v'
            || value == (byte)'\f'
            || value == (byte)'\r';
    }

    public static bool IsWhiteSpace(char value)
    {
        return value == ' '
            || value == '\t'
            || value == '\n'
            || value == '\v'
            || value == '\f'
            || value == '\r';
    }

    public static bool IsSpaceOrTab(byte value)
    {
        return value == (byte)' ' || value == (byte)'\t';
    }

    public static bool IsSpaceOrTab(char value)
    {
        return value == ' ' || value == '\t';
    }

    public static bool IsPrintable(byte value)
    {
        return value >= 0x20 && value <= 0x7E;
    }

    public static bool IsVisible(byte value)
    {
        return value >= 0x21 && value <= 0x7E;
    }

    public static byte ToUpperInvariant(byte value)
    {
        if (value >= (byte)'a' && value <= (byte)'z')
        {
            return (byte)(value - 32);
        }

        return value;
    }

    public static char ToUpperInvariant(char value)
    {
        if (value >= 'a' && value <= 'z')
        {
            return (char)(value - 32);
        }

        return value;
    }

    public static byte ToLowerInvariant(byte value)
    {
        if (value >= (byte)'A' && value <= (byte)'Z')
        {
            return (byte)(value + 32);
        }

        return value;
    }

    public static char ToLowerInvariant(char value)
    {
        if (value >= 'A' && value <= 'Z')
        {
            return (char)(value + 32);
        }

        return value;
    }

    public static bool EqualsIgnoreCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (ToUpperInvariant(left[i]) != ToUpperInvariant(right[i]))
            {
                return false;
            }
        }

        return true;
    }

    public static bool EqualsIgnoreCase(string? left, string? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        if (left is null || right is null || left.Length != right.Length)
        {
            return false;
        }

        for (int i = 0; i < left.Length; i++)
        {
            if (ToUpperInvariant(left[i]) != ToUpperInvariant(right[i]))
            {
                return false;
            }
        }

        return true;
    }
}
