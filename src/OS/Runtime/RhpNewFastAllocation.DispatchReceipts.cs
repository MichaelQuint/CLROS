#if NOSTDLIB_EFI && M04_INTERFACE_DISPATCH_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static unsafe partial class RhpNewFastAllocation
{

    private struct DispatchReceipts
    {
        internal RhpNewFastResult A, B;
    }

}
#endif
