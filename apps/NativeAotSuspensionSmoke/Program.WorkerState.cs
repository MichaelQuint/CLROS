using System.Diagnostics;

// This is an upstream managed workload, never freestanding kernel source.
// Output proves only what the executing runtime observed. Activation/suspension
// additionally requires the pinned native trace and CLROS thread/signal receipts.
internal static partial class Program
{
    private sealed class WorkerState
    {
        internal int Ready, Stop, Roots, Interior, Tls;
        internal long Progress;
        internal Exception? Failure;
    }
}
