namespace OS.Kernel.Platform;

public readonly struct HpetClockCapabilitiesResult
{
    public readonly MonotonicClockMathStatus Status;
    public readonly uint PeriodFemtoseconds;
    public readonly ulong FrequencyHz;
    public readonly uint CounterWidth;
    public bool Succeeded => Status == MonotonicClockMathStatus.Success;

    internal HpetClockCapabilitiesResult(MonotonicClockMathStatus status, uint period, ulong frequency, uint width)
    {
        Status = status;
        PeriodFemtoseconds = period;
        FrequencyHz = frequency;
        CounterWidth = width;
    }
}
