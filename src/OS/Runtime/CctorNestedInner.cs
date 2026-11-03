#if NOSTDLIB_EFI && M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static class CctorNestedInner
{
    private static ulong s_value;
    static CctorNestedInner()
    {
        RhpNewFastAllocation.CctorEntered(6);
        s_value = RhpNewFastAllocation.CctorSeed() + 51;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] internal static ulong Read() => s_value;
}

#endif
