#if M10_017_PROBE || M10_018_PROBE
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {
#if M10_018_PROBE

        private struct LinuxStaticPending
        {
            internal LinuxStaticFileBefore Before;
            internal ulong Identity, Ordinal;
        }
#else

#endif

#if M10_018_PROBE

#endif

#if M10_018_PROBE

#endif

#if M10_018_PROBE

#endif

#if M10_018_TIMING_PROBE

#endif

#if M10_018_PROBE

#endif

#if M10_018_PROBE

#endif

#if M10_018_PROBE

#endif
#if M10_018_TIMING_PROBE

#endif

#if M10_018_TIMING_PROBE

#endif

    }
}
#endif
