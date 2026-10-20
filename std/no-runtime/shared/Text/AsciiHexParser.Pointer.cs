namespace OS.Std.Text;

public static unsafe partial class AsciiHexParser
{
    /// <summary>Parse the complete borrowed ASCII range, optionally prefixed by
    ///0x/0X, without allocation or runtime services. The caller retains readable
    /// storage for the call. Null, empty, wrapping, malformed or overflowing
    /// input fails with value zero; signs and whitespace are unsupported.</summary>
    public static bool TryParseUInt64(byte* source, uint length, out ulong value)
    {
        value = 0;
        if (source == null || length == 0 || (ulong)source > ulong.MaxValue - length) return false;
        uint start = length >= 2 && source[0] == '0' && (source[1] == 'x' || source[1] == 'X') ? 2U : 0U;
        if (start == length) return false;
        ulong parsed = 0;
        for (uint i = start; i < length; i++)
        {
            byte c = source[i];
            uint digit = c >= '0' && c <= '9' ? (uint)(c - '0') : c >= 'a' && c <= 'f' ? (uint)(c - 'a' + 10) :
                c >= 'A' && c <= 'F' ? (uint)(c - 'A' + 10) : 16U;
            if (digit == 16 || parsed > ulong.MaxValue >> 4) return false;
            parsed = (parsed << 4) | digit;
        }
        value = parsed; return true;
    }
}
