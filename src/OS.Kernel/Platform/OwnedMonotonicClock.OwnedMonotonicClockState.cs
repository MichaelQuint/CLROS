namespace OS.Kernel.Platform;
public unsafe struct OwnedMonotonicClockState
{
    internal OwnedClockStatus status, hpetStatus;
    internal OwnedClockSource source;
    internal ulong frequency, previous, ticks, capabilities;
    internal uint width, period, recoveries;
    internal OwnedHpetAccess hpet;
    internal OwnedTscAccess tsc;
    public readonly OwnedClockStatus Status => status;
    public readonly OwnedClockStatus HpetStatus => hpetStatus;
    public readonly OwnedClockSource Source => source;
    public readonly ulong FrequencyHz => frequency;
    public readonly ulong Ticks => ticks;
    public readonly uint PeriodFemtoseconds => period;
    public readonly uint CounterWidth => width;
    public readonly uint RecoveryAttempts => recoveries;
}
