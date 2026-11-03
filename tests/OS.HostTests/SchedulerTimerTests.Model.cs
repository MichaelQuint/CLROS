using System.Runtime.InteropServices;
using OS.Drivers;
using OS.Kernel.Threading;
using OS.Pal;
using Timer = OS.Kernel.Threading.SchedulerTimer;

internal static unsafe partial class SchedulerTimerTests
{
    // Register model independently applies one-shot expiration and EOI effects.
    // It deliberately permits a callback failure AFTER a write has taken effect.
    private struct Model
    {
        public fixed uint Registers[1024];
        public fixed uint Offsets[512];
        public fixed uint Values[512];
        public uint Reads, Writes, FailRead, FailWrite, CorruptWrite;
        public byte RetainIsr;
    }
}
