namespace OS.Drivers;
/// <summary>Microsoft x64 ABI leaves: RCX context ignored, RDX address,
/// R8 output pointer/value. Return 1 after one DWORD read or DWORD/WORD write.
/// ENDBR64 entry, no stack/nonvolatile changes. Adapter authenticates RX storage.</summary>

public static unsafe class X64PciBarAccess
{
    public const uint StubBytes = 16, Bytes = 48;
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

    private static byte Byte(uint index)
    {
        uint stub = index / 16, offset = index % 16;
        ulong word = stub == 0 ? (offset < 8 ? 0x8941028bfa1e0ff3UL : 0x00c300000001b800UL) : stub == 1 ? (offset < 8 ? 0xb8028944fa1e0ff3UL : 0x000000c300000001UL) : (offset < 8 ? 0x02894466fa1e0ff3UL : 0x0000c300000001b8UL);
        return (byte)(word >> ((int)(offset & 7) * 8));
    }
}
