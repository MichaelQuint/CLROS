namespace OS.Std.Text;

/// <summary>Exact byte comparison with a packed ASCII token, up to 24 bytes.
/// Constants are little endian; callers own readable memory. No strings or allocation.</summary>
public static unsafe class AsciiPointerMatcher
{
    /// <summary>ASCII relative file path, at most 255 bytes. Rejects absolute,
    /// empty, dot and dot-dot segments, separators, drives and control bytes.</summary>
    public static bool IsRelativePath(byte* bytes, uint length)
    {
        if (length == 0 || length > 255 || bytes == null || (ulong)bytes > ulong.MaxValue - length)
            return false;
        uint start = 0;
        for (uint i = 0; i <= length; i++)
        {
            if (i < length)
            {
                byte b = bytes[i];
                if (b < 32 || b > 126 || b == '\\' || b == ':')
                    return false;
                if (b != '/')
                    continue;
            }
            uint count = i - start;
            if (count == 0 || count == 1 && bytes[start] == '.' ||
                count == 2 && bytes[start] == '.' && bytes[start + 1] == '.')
                return false;
            start = i + 1;
        }
        return true;
    }

    public static bool Matches(byte* bytes, uint length, uint expectedLength,
        ulong first, ulong second = 0, ulong third = 0)
    {
        if (length != expectedLength || expectedLength > 24 ||
            (length != 0 && (bytes == null || (ulong)bytes > ulong.MaxValue - length)))
            return false;
        for (uint i = 0; i < length; i++)
        {
            ulong word = i < 8 ? first : i < 16 ? second : third;
            if (bytes[i] != (byte)(word >> ((int)(i % 8) * 8)))
                return false;
        }
        return true;
    }
}
