using System.Diagnostics;
using System.Runtime.CompilerServices;

// Normal upstream managed runtime behavior. Producer and executing-platform
// qualification remain separate; original pinned workloads are unchanged.
internal static partial class Program
{
    private static class Broken
    {
        private static readonly int Value = 17;
        static Broken()
        {
            Interlocked.Increment(ref s_cctorRuns);
            throw CctorOriginal;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static int Read() => Value;
    }
}
