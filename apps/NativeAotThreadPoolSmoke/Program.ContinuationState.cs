using System.Diagnostics;

// Managed output alone grants no NativeAOT or CLROS execution evidence. The
// process owner must separately qualify the producer and executing platform.
// Drained below means only this app's awaited work and disposed timer; it never
// claims that runtime ThreadPool workers have stopped or released their stacks.
internal static partial class Program
{
    private sealed class ContinuationState
    {
        internal int Before, Race, After, Count, PublicationInline;
    }
}
