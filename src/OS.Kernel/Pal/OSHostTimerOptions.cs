using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostTimerOptions
{
    public readonly uint Version;
    public readonly uint Flags;
    public readonly ulong DueTicks;
    public readonly ulong PeriodTicks;
    public readonly OSHostHandle CompletionWaitHandle;
    public OSHostTimerOptions(uint version, uint flags, ulong dueTicks, ulong periodTicks, OSHostHandle completionWaitHandle)
    {
        Version = version;
        Flags = flags;
        DueTicks = dueTicks;
        PeriodTicks = periodTicks;
        CompletionWaitHandle = completionWaitHandle;
    }
}
