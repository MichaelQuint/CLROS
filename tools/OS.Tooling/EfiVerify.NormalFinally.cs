using System.Buffers.Binary;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

public static partial class EfiVerify
{
    public static bool TestNormalFinallyEhTable(byte[]? table, ulong type) => table is { Length: 11 } && type is > 0 and <= uint.MaxValue && table.AsSpan(0, 7).SequenceEqual(new byte[] { 4, 86, 106, 178, 86, 152, 242 }) && BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(7, 4)) == type;
    public static bool TestNormalFinallyUnwind(byte[]? bytes, int part, ulong table)
    {
        byte[] prefix = part switch
        {
            0 => [1, 5, 2, 0, 5, 114, 1, 80, 4],
            1 => [1, 4, 1, 0, 4, 66, 1],
            2 => [1, 4, 1, 0, 4, 66, 0],
            _ => []
        };
        return prefix.Length != 0 && bytes is not null && bytes.Length == prefix.Length + (part == 0 ? 4 : 0) && bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix) && (part != 0 || table is > 0 and <= uint.MaxValue && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(prefix.Length, 4)) == table);
    }

    public static bool TestNormalFinallyCode(byte[]? bytes, bool root)
    {
        const string rootShape = "554883EC40488D6C24400F57E40F1165E833C0488945F848894D1033C08BC0488945F833C08BC0488945F0488B4D10E800000000488945F8E81C00000090488B4D10488B55F04C8B45F8E800000000488B45F04883C4405DC34883EC28488D55F8488B4D10E800000000488B4D10E800000000904883C428C34883EC2848894DE8488B4DE8488B55F8E800000000488B45E8B9010000008BC94803480848894DF0488D0596FFFFFF4883C428C3";
        string shape = root ? rootShape : "4883EC28E800000000CC";
        if (bytes is null || bytes.Length != shape.Length / 2)
            return false;
        byte[] normalized = [.. bytes];
        foreach (int field in root ? new[]
        {
            48,
            75,
            102,
            111,
            138
        }

        : [5])
            normalized.AsSpan(field, 4).Clear();
        return normalized.AsSpan().SequenceEqual(Convert.FromHexString(shape));
    }
}
