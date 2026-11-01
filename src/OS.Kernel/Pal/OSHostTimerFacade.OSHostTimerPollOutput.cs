using System.Runtime.InteropServices;

namespace OS.Pal;
[StructLayout(LayoutKind.Sequential)]
public readonly struct OSHostTimerPollOutput
{
    public readonly ulong FiredCount;
    public readonly ulong NextDueTicks;
    public readonly uint Signaled;
    public readonly uint Reserved;
    public OSHostTimerPollOutput(ulong firedCount, ulong nextDueTicks, uint signaled, uint reserved)
    {
        FiredCount = firedCount;
        NextDueTicks = nextDueTicks;
        Signaled = signaled;
        Reserved = reserved;
    }

    public bool IsSignaled => Signaled != 0;

    public static OSHostTimerPollOutput FromResult(OSHostTimerPollResult result)
    {
        return new OSHostTimerPollOutput(result.FiredCount, result.NextDueTicks, result.Signaled ? 1U : 0U, reserved: 0);
    }
}
