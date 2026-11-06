using OS.Drivers;

namespace OS.Kernel.Threading;

public unsafe partial struct SchedulerTimer
{
    /// <summary>Observed publication state of the interrupted context. The native
    /// owner sets this before each allocation/publication boundary with IF clear;
    /// Safe permits context redirection only after lifetime/root admission. Other
    /// phases retain the interrupted context and queue, for a bounded tick count.</summary>

    public enum Phase : byte
    {
        Safe,
        Allocating,
        Initializing,
        Publishing
    }
}
