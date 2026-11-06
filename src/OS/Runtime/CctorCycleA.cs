#if NOSTDLIB_EFI && M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static class CctorCycleA
{
    private static ulong s_value;
    static CctorCycleA()
    {
        RhpNewFastAllocation.CctorEntered(3);
        s_value = RhpNewFastAllocation.CctorSeed() + 41;
        s_value = CctorCycleB.Read() + 1;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] internal static ulong Read() => s_value;
}

#endif
