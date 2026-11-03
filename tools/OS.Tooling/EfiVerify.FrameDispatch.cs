using System.Buffers.Binary;
using System.Text.RegularExpressions;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

public static partial class EfiVerify
{
    public static bool TestFrameDispatchEhTable(byte[]? bytes, int part, ulong sibling, ulong baseType)
    {
        if (part == 0)
            return bytes is not null && bytes.AsSpan().SequenceEqual(new byte[] { 2, 40, 50, 98 });
        return part == 1 && bytes is { Length: 15 } && sibling is > 0 and <= uint.MaxValue && baseType is > 0 and <= uint.MaxValue && sibling != baseType && bytes.AsSpan(0, 4).SequenceEqual(new byte[] { 4, 74, 80, 150 }) && bytes.AsSpan(8, 3).SequenceEqual(new byte[] { 74, 80, 192 }) && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4)) == sibling && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(11)) == baseType;
    }

    public static bool TestFrameDispatchUnwindBytes(byte[]? bytes, int part, ulong table)
    {
        byte[] prefix = part switch
        {
            0 => [1, 4, 1, 0, 4, 66, 0],
            1 => [1, 5, 2, 0, 5, 82, 1, 80, 4],
            3 => [1, 6, 3, 0, 6, 98, 2, 48, 1, 80, 4],
            2 or 4 or 5 => [1, 4, 1, 0, 4, 66, 1],
            _ => []
        };
        bool root = part is 1 or 3;
        return prefix.Length != 0 && bytes is not null && bytes.Length == prefix.Length + (root ? 4 : 0) && bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix) && (!root || table is > 0 and <= uint.MaxValue && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(prefix.Length)) == table);
    }

    public static bool TestFrameDispatchCode(byte[]? bytes, int part)
    {
        string shape = part switch
        {
            0 => "4883EC28E800000000CC",
            1 => "554883EC30488D6C243033C0488945F848894D10E80000000090488D55F8488B4D10E800000000488B45F84883C4305DC34883EC28488D55F8488B4D10E800000000904883C428C3",
            2 => "55534883EC38488D6C244033C0488945E848894D1033D2488955F0488D55E8E80000000090488B4D10E80000000090488B4D10488B55F04C8B45E8E800000000488B45F04883C4385B5DC34883EC28E800000000488D05D4FFFFFF4883C428C34883EC28488BD9488D55E8488BCBE800000000488B4B0848FFC148894DF0488D05AAFFFFFF4883C428C3",
            _ => ""
        };
        if (shape.Length == 0 || bytes is null || bytes.Length != shape.Length / 2)
            return false;
        byte[] normalized = [.. bytes];
        foreach (int field in part switch
        {
            0 => new[]
            {
                5
            },
            1 => [21, 35, 62],
            _ => [32, 42, 60, 80, 111]
        })
            normalized.AsSpan(field, 4).Clear();
        return normalized.AsSpan().SequenceEqual(Convert.FromHexString(shape));
    }
}
