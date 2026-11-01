using OS.Drivers;

namespace OS.Kernel.Threading;
public unsafe partial struct SchedulerTimer
{
    public enum Action : byte
    {
        None,
        Rearmed,
        Deferred,
        WakeReady
    }
}
