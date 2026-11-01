using OS.Pal;

namespace OS.Kernel.Compatibility.LinuxX64;
public static unsafe partial class LinuxSignalServices
{
    private struct ThreadState
    {
        internal ulong Identity;
        internal AlternateStack Stack;
        internal ulong Depth;
        internal fixed ulong Frames[4];
        internal ulong PendingStandard, RealtimeCount;
        internal fixed uint Realtime[4];
        // One owner request per bound generation; pending SIGTERM provenance
        // is separate so consume/ignore never rearms the owner request.
        internal ulong Termination;
    }
}
