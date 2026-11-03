using System.Diagnostics;
using System.Runtime.CompilerServices;

// Normal upstream managed runtime behavior. Producer and executing-platform
// qualification remain separate; original pinned workloads are unchanged.
internal static partial class Program
{
    private sealed class Target(int seed)
    {
        private readonly byte[] root = [37, 41];
        internal int Calls;
        internal int Add(int value)
        {
            Require(root[0] == 37 && root[1] == 41, "delegate_root");
            Calls++;
            return seed + value;
        }
    }
}
