using System.Diagnostics;

// A separate upstream managed workload. This is neither a kernel collector nor
// proof of NativeAOT/CLROS execution. Original focused payloads remain unchanged.
internal static partial class Program
{
    private sealed class WorkerState
    {
        internal int Ready, Sleeping, SleepStarts, Roots, Interior, Tls, Completed;
        internal long Progress;
        internal Exception? Failure;
    }
}
