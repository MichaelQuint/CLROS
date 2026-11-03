using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Std.Memory;

internal static unsafe partial class MemoryOrderingPrimitivesReplacingReadinessBooleansTests
{
    private struct CallbackState
    {
        public uint Trace, Calls;
        public byte Result;
    }
}
