using System.Diagnostics;

// Separate upstream managed workload; producer and platform evidence are still
// required. Draining app-owned operations never means runtime workers shut down.
internal static partial class Program
{
    private sealed class WorkState
    {
        internal int Active, Maximum, Completed, Mask;
        internal readonly TaskCompletionSource<bool> Staged = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
