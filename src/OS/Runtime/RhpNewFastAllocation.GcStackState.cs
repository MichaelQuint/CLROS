#if NOSTDLIB_EFI && M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static unsafe partial class RhpNewFastAllocation
{

    private struct GcStackState
    {
        internal ulong Authority, Start, End, InitialRsp, Witness, MinimumWitness;
        internal ulong Checks, Final0, Final1;
    }

#pragma warning disable CS0649 // Written through bounded sequential POD slots.

#pragma warning restore CS0649

}
#endif
