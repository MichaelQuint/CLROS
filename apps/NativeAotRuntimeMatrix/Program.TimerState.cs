using System.Diagnostics;
using System.Runtime.CompilerServices;

// Normal upstream managed runtime behavior. Producer and executing-platform
// qualification remain separate; original pinned workloads are unchanged.
internal static partial class Program
{
    private sealed class TimerState(long before)
    {
        internal readonly long Before = before;
        internal readonly TaskCompletionSource<long> Fired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int Callbacks, Completed, Disposed, Stale;
        internal Exception? Failure;
    }
}
