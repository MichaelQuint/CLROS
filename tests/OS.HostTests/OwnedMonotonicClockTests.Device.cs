using System.Buffers.Binary;
using System.Runtime.InteropServices;
using OS.Kernel.Platform;

internal static unsafe partial class OwnedMonotonicClockTests
{
    private struct Device
    {
        internal ulong Capabilities, Config, Counter;
        internal fixed ulong Timers[32];
        internal uint Reads, Writes, CounterReads, Resets;
        internal bool Stalled, PermanentStall, ChangeCapabilities, IgnoreTimerWrites;
    }
}
