namespace OS.Kernel.Platform;
/// <summary>
/// Fixed x64 leaf stubs, Microsoft ABI. Caller proves exclusive UC MMIO, CPL/CPU
/// features, RX publication and single-CPU lifetime before calling them. CPUID
/// preserves RBX; the ordered TSC read requires the admitted Intel/SSE2 profile.
/// </summary>

public static unsafe class X64OwnedClockAccess
{
    public const uint Bytes = 96, ReadOffset = 0, WriteOffset = 16, CpuidOffset = 32, TscOffset = 64;
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

    private static byte Byte(uint offset)
    {
        ulong word = (offset / 8) switch
        {
            0 => 0x8b48d089fa1e0ff3UL,
            1 => 0x0000000000c30104UL,
            2 => 0x894cd289fa1e0ff3UL,
            3 => 0x0000000000c31104UL,
            4 => 0x89c88953fa1e0ff3UL,
            5 => 0x8941008941a20fd1UL,
            6 => 0x8941084889410458UL,
            7 => 0x00000000c35b0c50UL,
            8 => 0x0fe8ae0ffa1e0ff3UL,
            9 => 0xd0094820e2c14831UL,
            10 => 0x00000000c3e8ae0fUL,
            11 => 0x0000000000000000UL,
            _ => 0
        };
        return (byte)(word >> ((int)(offset % 8) * 8));
    }
}
