using System.Diagnostics;
using System.Runtime.CompilerServices;

// Normal upstream managed runtime behavior. Producer and executing-platform
// qualification remain separate; original pinned workloads are unchanged.
internal static partial class Program
{
    private sealed class Finalized(int bit)
    {
        ~Finalized()
        {
            if ((Interlocked.Or(ref s_finalizedMask, bit) & bit) != 0)
                Interlocked.Increment(ref s_duplicateFinalizers);
        }
    }
}
