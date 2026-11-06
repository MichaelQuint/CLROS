using System.Runtime.CompilerServices;

namespace OS.Std.Memory;

/// <summary>Erase caller-owned mapped writable bytes without allocation. The
/// serialized caller owns lifetime and exclusion; invalid range/null rejects
/// before writing. This clears addressed storage, not registers or other copies.</summary>
public static unsafe class SensitiveMemory
{
    // NoOptimization (0x40) and NoInlining retain the observable erase loop in
    // both CLR and the freestanding compiler surface. Native review remains required.
    [MethodImpl(MethodImplOptions.NoInlining | (MethodImplOptions)0x40)]
    public static bool Clear(void* address, uint bytes)
    {
        if (bytes != 0 && (address == null || (ulong)address > ulong.MaxValue - bytes))
            return false;
        byte* target = (byte*)address;
        for (uint i = 0; i < bytes; i++)
            target[i] = 0;
        return true;
    }
}
