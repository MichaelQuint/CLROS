#if M16_025_PROBE
using OS.Storage;
using OS.Kernel.Compatibility.LinuxX64;
using OS.Kernel.Threading;
using Block = OS.Storage.ContinuingReadOnlyBlockService;

namespace OS.Boot;

#pragma warning disable 649 // Contiguous caller-owned records are written through admitted native pointers.

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {

        private struct NativeDiskEvidence
        {
            internal NativeDiskRecord A, B, C, D, E, F, G, H;
#if M10_018_FULL_PROBE || M10_018_TIMING_PROBE
            internal NativeDiskRecord I, J, K, L, M, N, O, P, Q, R;
#endif
            internal ulong Generation, CopiedRecords;
            internal uint Count, Total;
            internal bool Enabled, Failed;
        }

    }
}
#pragma warning restore 649
#endif
