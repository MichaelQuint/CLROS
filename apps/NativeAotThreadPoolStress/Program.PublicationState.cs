using System.Diagnostics;

// Separate upstream managed workload; producer and platform evidence are still
// required. Draining app-owned operations never means runtime workers shut down.
internal static partial class Program
{
    private sealed class PublicationState
    {
        internal int Winners, Count, Inline;
        internal readonly int[] Positions = new int[3];
    }
}
