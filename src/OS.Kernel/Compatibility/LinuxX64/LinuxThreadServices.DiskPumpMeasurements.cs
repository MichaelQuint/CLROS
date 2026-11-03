#if !NOSTDLIB_EFI || M16_025_PROBE
using OS.Kernel.Threading;

namespace OS.Kernel.Compatibility.LinuxX64;

public unsafe partial struct LinuxThreadServices
{
    /// <summary>Optional supervisor-owned diagnostic totals. Zero before the
    /// first call; retain aligned private storage and inspect/reset only between
    /// serialized invocations. These observations grant no device, scheduling
    /// or copy authority. Successful calls have exactly one yield reason;
    /// failures retain backing and make the diagnostic incomplete. Times include
    /// the small measurement boundary overhead and are not hardware latencies.</summary>

    public struct DiskPumpMeasurements
    {
        public ulong Calls, Steps, PumpNanoseconds, ScanNanoseconds, FlushNanoseconds;
        public ulong DiskReady, OtherReady, NoAdvance, Elapsed, StepLimit, Failures, FlushedPastBudget;
    }
}
#endif
