namespace OS.Kernel.Compatibility.LinuxX64;
/// <summary>
/// Fixed Microsoft-x64 bridge: save RBX/CR3, adopt RCX root, call R8 with RDX
/// context and 32-byte shadow space, then restore original CR3/RBX and byte AL.
/// The owner proves both roots retain the original code/stack/context, interrupts
/// disabled, PCID disabled and a genuine first-party nonthrowing callback.
/// Emit/read-back alone does not execute instructions or establish ownership.
/// </summary>

public static unsafe class X64LinuxElfReadBackBridge
{
    public const uint Bytes = 40, ReadCr3Offset = 32;
    public static bool Emit(byte* code, uint capacity)
    {
        if (code == null || capacity < Bytes || (ulong)code > ~0UL - Bytes)
            return false;
        for (uint i = 0; i < Bytes; i++)
            code[i] = Byte(i);
        return Validate(code, capacity);
    }

    public static bool Validate(byte* code, uint capacity)
    {
        if (code == null || capacity < Bytes || (ulong)code > ~0UL - Bytes)
            return false;
        for (uint i = 0; i < Bytes; i++)
            if (code[i] != Byte(i))
                return false;
        return true;
    }

    private static byte Byte(uint i)
    {
        ulong word = (i / 8) switch
        {
            0 => 0xec834853fa1e0ff3UL,
            1 => 0x48d9220fdb200f20UL,
            2 => 0xdb220fd0ff41d189UL,
            3 => 0x0000c35b20c48348UL,
            4 => 0xc3d8200ffa1e0ff3UL,
            _ => 0
        };
        return (byte)(word >> ((int)(i % 8) * 8));
    }
}
