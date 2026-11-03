#if NOSTDLIB_EFI && M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static class CctorLazyFixture
{
    private static ulong s_value;
    static CctorLazyFixture()
    {
        RhpNewFastAllocation.CctorEntered(1);
        s_value = RhpNewFastAllocation.CctorSeed() + 21;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] internal static ulong Read() => s_value;
}

#endif
