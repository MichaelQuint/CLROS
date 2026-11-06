using System.Buffers.Binary;
using static OS.Tooling.EfiEvidence;
using static OS.Tooling.EfiPeImage;

namespace OS.Tooling;

public static partial class EfiVerify
{
    public static bool TestUnhandledEhTable(byte[]? table, ulong sibling) => table is { Length: 8 } && sibling is > 0 and <= uint.MaxValue && table.AsSpan(0, 4).SequenceEqual(new byte[] { 2, 20, 72, 58 }) && BinaryPrimitives.ReadUInt32LittleEndian(table.AsSpan(4)) == sibling;
    // Include the NativeAOT GC/EH marker following the Windows unwind codes.
    public static bool TestUnhandledUnwindBytes(byte[]? bytes, int part, ulong table)
    {
        byte[] prefix = part switch
        {
            0 => [1, 4, 1, 0, 4, 66, 0], // thrower: 40 bytes
            1 => [1, 5, 2, 0, 5, 82, 1, 48, 0], // middle: 48 + RBX
            2 => [1, 5, 2, 0, 5, 82, 1, 80, 4], // catch root: 48 + RBP, EH table
            3 => [1, 4, 1, 0, 4, 66, 1], // catch funclet: 40, root association
            _ => []
        };
        return prefix.Length != 0 && bytes is not null && bytes.Length == prefix.Length + (part == 2 ? 4 : 0) && bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix) && (part != 2 || table is > 0 and <= uint.MaxValue && BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(prefix.Length)) == table);
    }

    public static bool TestUnhandledCode(byte[]? bytes, int part)
    {
        string shape = part switch
        {
            0 => "4883EC28E800000000CC",
            1 => "534883EC30488BD9488B4B0848894C2428488D4C2428E800000000488BCBE800000000488B4424284883C4305BC3",
            2 => "554883EC30488D6C2430E800000000488945F8488B45F84883C4305DC34883EC28E80000000033C0488945F8488D05E0FFFFFF4883C428C3",
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
            1 => [23, 31],
            _ => [11, 34]
        })
            normalized.AsSpan(field, 4).Clear();
        return normalized.AsSpan().SequenceEqual(Convert.FromHexString(shape));
    }
}
