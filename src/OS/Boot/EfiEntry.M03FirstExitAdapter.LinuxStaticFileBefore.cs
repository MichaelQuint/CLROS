#if M10_017_PROBE || M10_018_PROBE
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Boot;

public static unsafe partial class EfiEntry
{
    private static partial class M03FirstExitAdapter
    {

        // These synchronous stack borrows never survive Dispatch or a handoff.
        // Preserve the actual input path before a stat output can overwrite it.
        private struct LinuxStaticFileBefore
        {
            internal LinuxStaticFileIdentity File;
            internal fixed byte Path[(int)LinuxDescriptorServices.MaximumPath];
            internal uint PathBytes;
            internal bool HasPath;
        }

    }
}
#endif
