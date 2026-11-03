using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

namespace OS.Kernel.Diagnostics;

public static unsafe partial class WritableStorageTiming
{
    public enum Bucket : byte
    {
        Audit,
        Pair,
        Pump
    }
}
