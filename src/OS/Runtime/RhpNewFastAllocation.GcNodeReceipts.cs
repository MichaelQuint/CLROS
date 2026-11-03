#if NOSTDLIB_EFI && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static unsafe partial class RhpNewFastAllocation
{

    private struct GcNodeReceipts
    {
        internal RhpNewFastResult Node0;
#pragma warning disable CS0649 // Written through bounded sequential POD slots.

        internal RhpNewFastResult Node1, Node2, Node3, Node4, Node5;
#pragma warning restore CS0649
        internal ulong Count;
    }

}
#endif
