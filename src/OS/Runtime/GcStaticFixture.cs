#if NOSTDLIB_EFI && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

// One compiler-emitted GC-static cell. Ordinary GC profiles have no class
// constructor; the explicit cctor profile adds one only after retained binding.
// RunGcStatics publishes the base before any accessor is called.
internal static class GcStaticFixture
{
    private static object s_value;
#if M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE
    static GcStaticFixture()
    {
        RhpNewFastAllocation.CctorEntered(7);
    }
#endif

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Set(object value) => s_value = value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static object Get() => s_value;

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static ref object Address() => ref s_value;
}





#pragma warning disable CS0649 // Written through bounded sequential POD slots.

#pragma warning restore CS0649

#endif
