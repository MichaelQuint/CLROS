using OS.Kernel.Memory;
using System.Runtime.CompilerServices;

namespace OS.Kernel.Runtime;
public enum ManagedHeapPageRangeLifecycle : byte
{
    Uninitialized = 0,
    ReservedNotReady = 1,
    Ready = 2
}
