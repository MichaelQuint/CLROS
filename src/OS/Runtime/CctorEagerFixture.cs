#if NOSTDLIB_EFI && M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

// Runtime-fed primitive fixtures preserve the existing single GC-static cell.
// None of the binding/diagnostic state below has a bootstrap constructor.
[EagerStaticClassConstruction]
internal static class CctorEagerFixture
{
    internal static ulong Value;
    static CctorEagerFixture()
    {
        RhpNewFastAllocation.CctorEntered(0);
        Value = RhpNewFastAllocation.CctorSeed() + 11;
    }
    [MethodImpl(MethodImplOptions.NoInlining)] internal static ulong Read() => Value;
}

#endif
