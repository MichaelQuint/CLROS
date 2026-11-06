#if NOSTDLIB_EFI && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static unsafe partial class RhpNewFastAllocation
{

    private struct GcTypeStorage
    {
        internal fixed ulong Words[512];
    }

#pragma warning disable CS0649 // Written through bounded sequential POD slots.

#pragma warning restore CS0649

}
#endif
