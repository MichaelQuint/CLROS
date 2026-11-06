#if NOSTDLIB_EFI && M04_CONSTRUCTOR_FAILURE_PROBE
using OS.Boot;
using OS.Kernel.Runtime;
using System.Runtime.CompilerServices;

namespace OS.Runtime;

internal static unsafe partial class RhpNewFastAllocation
{

    private struct ConstructorFailureScratch
    {
        internal fixed ulong Words[210];
    }

}
#endif
