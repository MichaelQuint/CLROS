using System.Runtime.InteropServices;

namespace OS.Pal;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostTimeSnapshot
{
    public readonly ulong MonotonicTicks;
    public readonly ulong TickFrequency;
    public readonly long WallClockUnixNanoseconds;
    public OSHostTimeSnapshot(ulong monotonicTicks, ulong tickFrequency, long wallClockUnixNanoseconds)
    {
        MonotonicTicks = monotonicTicks;
        TickFrequency = tickFrequency;
        WallClockUnixNanoseconds = wallClockUnixNanoseconds;
    }
}
