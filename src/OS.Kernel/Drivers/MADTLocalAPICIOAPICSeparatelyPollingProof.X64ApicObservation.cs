using OS.Std.Binary;

namespace OS.Drivers;
/// <summary>uint (XApicCpuObservation*), Microsoft x64 ABI. Preserves RBX, checks
/// CPL0 and IF=0 before CPUID.1/MSR access, and requires MSR/APIC feature bits before
/// RDMSR IA32_APIC_BASE. Partial observations on refusal are not valid. The caller
/// authenticates writable output and immutable RX code; never invoke on the host.</summary>

public static unsafe class X64ApicObservation
{
    public const uint Bytes = 96;
    public static bool Emit(byte* code, ulong capacity)
    {
        if (!Storage(code, capacity))
            return false;
        for (uint i = 0; i < Bytes; i++)
            code[i] = Byte(i);
        return true;
    }

    public static bool Validate(byte* code, ulong capacity)
    {
        if (!Storage(code, capacity))
            return false;
        for (uint i = 0; i < Bytes; i++)
            if (code[i] != Byte(i))
                return false;
        return true;
    }

    private static bool Storage(byte* p, ulong capacity) => p != null && capacity >= Bytes && (ulong)p <= ~0UL - Bytes;
    private static byte Byte(uint i)
    {
        // Independent decoded review: endbr64; preserve RBX; CPL/IF admission;
        // CPUID.1 -> EDX and EBX[31:24]; feature admission; RDMSR 0x1b; restore.
        ulong word = (i / 8) switch
        {
            0 => 0xc8894953fa1e0ff3UL,
            1 => 0x9c487503a8c88c66UL,
            2 => 0x000200c1f7415941UL,
            3 => 0x00000001b83c7500UL,
            4 => 0x18ebc1108941a20fUL,
            5 => 0x1048894d04588941UL,
            6 => 0xfa8100000220e281UL,
            7 => 0x1bb9197500000220UL,
            8 => 0xe2c148320f000000UL,
            9 => 0x08408949d0094820UL,
            10 => 0x31c35b00000001b8UL,
            11 => 0x0000000000c35bc0UL,
            _ => 0
        };
        return (byte)(word >> ((int)(i & 7) * 8));
    }
}
