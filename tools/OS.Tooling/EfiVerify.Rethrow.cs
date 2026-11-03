using System.Buffers.Binary;
using System.Text.RegularExpressions;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;
public static partial class EfiVerify
{
    public static bool TestRethrowEhTable(byte[]? table, bool inner, ulong typeRva)
    {
        ReadOnlySpan<byte> prefix = inner ? [4, 40, 50, 92, 40, 160, 138] : [2, 42, 48, 102];
        return table is not null && table.Length == prefix.Length + 4 && typeRva is> 0 and <= uint.MaxValue && table.AsSpan(0, prefix.Length).SequenceEqual(prefix) && BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(prefix.Length)) == typeRva;
    }

    public static bool TestRethrowUnwindBytes(byte[]? data, bool inner, bool root, ulong tableRva)
    {
        ReadOnlySpan<byte> prefix = !root ? [1, 4, 1, 0, 4, 66, 1] : inner ? [1, 5, 2, 0, 5, 82, 1, 80, 4] : [1, 6, 3, 0, 6, 66, 2, 48, 1, 80, 4];
        return data is not null && data.Length == prefix.Length + (root ? 4 : 0) && data.AsSpan(0, prefix.Length).SequenceEqual(prefix) && (!root || tableRva is> 0 and <= uint.MaxValue && BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(prefix.Length)) == tableRva);
    }
}
