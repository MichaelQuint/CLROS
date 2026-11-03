using System.Buffers.Binary;
using System.Text.RegularExpressions;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;
public static partial class EfiVerify
{
    public static bool TestNestedFinallyEhTable(byte[]? table, ulong sibling, ulong baseType)
    {
        ReadOnlySpan<byte> first = [8, 54, 50, 202, 54, 210, 9, 2, 54, 225, 2, 125, 2], second = [54, 225, 2, 209, 2];
        return table is not null && table.Length == 26 && sibling is> 0 and <= uint.MaxValue && baseType is> 0 and <= uint.MaxValue && sibling != baseType && table.AsSpan(0, 13).SequenceEqual(first) && table.AsSpan(17, 5).SequenceEqual(second) && BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(13)) == sibling && BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(22)) == baseType;
    }

    public static bool TestNestedFinallyUnwindBytes(byte[]? bytes, bool root, ulong table)
    {
        ReadOnlySpan<byte> prefix = root ? [1, 6, 3, 0, 6, 98, 2, 48, 1, 80, 4] : [1, 4, 1, 0, 4, 66, 1];
        return bytes is not null && bytes.Length == prefix.Length + (root ? 4 : 0) && bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix) && (!root || table is> 0 and <= uint.MaxValue && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(prefix.Length)) == table);
    }

    public static bool TestNestedFinallyCode(byte[]? bytes)
    {
        if (bytes is null || bytes.Length != 222)
            return false;
        byte[] normalized = [..bytes];
        foreach (int field in new[]
        {
            28,
            48,
            68,
            86,
            120,
            149,
            164,
            195
        }

        )
            normalized.AsSpan(field, 4).Clear();
        return normalized.AsSpan().SequenceEqual(Convert.FromHexString("55534883EC38488D6C244033C0488945E848894D1033C0488945F0E80000000090488D55E8488B4D1041B801000000E80000000090488D55E8488B4D1041B802000000E80000000090488B4D10488B55F04C8B45E8E800000000488B45F04883C4385B5DC34883EC28488D55E8488B4D1041B801000000E800000000904883C428C34883EC28488D55E8488B4D1041B802000000E800000000904883C428C34883EC28E800000000488D059AFFFFFF4883C428C34883EC28488BD9488BCB488B55E8E800000000488B4B0848FFC148894DF0488D0570FFFFFF4883C428C3"));
    }
}
