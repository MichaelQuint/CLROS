using System.Diagnostics;

// Managed output alone grants no NativeAOT or CLROS execution evidence. The
// process owner must separately qualify the producer and executing platform.
// Drained below means only this app's awaited work and disposed timer; it never
// claims that runtime ThreadPool workers have stopped or released their stacks.
internal static partial class Program
{
    private sealed class TimerState(long before)
    {
        internal readonly long Before = before;
        internal readonly TaskCompletionSource<long> Fired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Callbacks, Stale, Disposed;
        internal Exception? Failure;
    }
}
