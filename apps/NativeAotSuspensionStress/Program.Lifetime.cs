using System.Diagnostics;

// A separate upstream managed workload. This is neither a kernel collector nor
// proof of NativeAOT/CLROS execution. Original focused payloads remain unchanged.
internal static partial class Program
{
    private sealed class Lifetime
    {
        // Disable the managed spin phase so blocked contexts reach a real wait.
        internal readonly ManualResetEventSlim Release = new(false, 0), Exit = new(false, 0);
        internal int Stop;
    }
}
