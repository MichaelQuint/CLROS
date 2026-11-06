using System.Diagnostics;

// Separate upstream managed workload; producer and platform evidence are still
// required. Draining app-owned operations never means runtime workers shut down.
internal static partial class Program
{
    private sealed class TimerState(long before)
    {
        internal readonly long Before = before;
        internal readonly TaskCompletionSource<long> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal readonly ManualResetEventSlim Release = new(false, 0);
        internal int Callbacks, Completed, Stale, Disposed;
        internal Exception? Failure;
    }
}
