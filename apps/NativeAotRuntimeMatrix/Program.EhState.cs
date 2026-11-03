using System.Diagnostics;
using System.Runtime.CompilerServices;

// Normal upstream managed runtime behavior. Producer and executing-platform
// qualification remain separate; original pinned workloads are unchanged.
internal static partial class Program
{
    private sealed class EhState
    {
        internal int Filters, Catches, Cleanups, Order;
    }
}
