namespace OS.Drivers;
/// <summary>Microsoft x64 ABI: ignore RCX context; read one DWORD from RDX into EAX.</summary>

public static unsafe class X64PciConfigurationRead
{
    public const uint Bytes = 16;
    public static bool Emit(byte* code, ulong capacity)
    {
        if (code == null || capacity < Bytes || (ulong)code > ~0UL - Bytes)
            return false;
        for (uint i = 0; i < Bytes; i++)
            code[i] = Byte(i);
        return Validate(code, capacity);
    }

    public static bool Validate(byte* code, ulong capacity)
    {
        if (code == null || capacity < Bytes || (ulong)code > ~0UL - Bytes)
            return false;
        for (uint i = 0; i < Bytes; i++)
            if (code[i] != Byte(i))
                return false;
        return true;
    }

    private static byte Byte(uint i) => i < 8 ? (byte)(0x00c3028bfa1e0ff3UL >> ((int)i * 8)) : (byte)0;
}
