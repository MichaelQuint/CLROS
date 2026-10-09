namespace OS.Kernel.Platform;

public enum MonotonicClockMathStatus : byte
{
    Uninitialized = 0,
    Success = 1,
    InvalidPeriod = 2,
    InvalidWidth = 3,
    InvalidCounter = 4,
    AmbiguousDelta = 5,
    AccumulatedOverflow = 6
}

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

public readonly struct MonotonicClockDeltaResult
{
    public readonly MonotonicClockMathStatus Status;
    public readonly ulong DeltaTicks;
    public readonly ulong AccumulatedTicks;
    public bool Succeeded => Status == MonotonicClockMathStatus.Success;

    internal MonotonicClockDeltaResult(MonotonicClockMathStatus status, ulong delta, ulong accumulated)
    {
        Status = status;
        DeltaTicks = delta;
        AccumulatedTicks = accumulated;
    }
}

/// <summary>Pure counter arithmetic; no device admission, MMIO, TSC calibration or clock ownership.</summary>
public static class MonotonicHpetTscClockMath
{
    /// <summary>
    /// Decodes HPET General Capabilities bits 63:32 and bit 13 (HPET 1.0a section 2.3.4).
    /// FrequencyHz is rounded down; retain PeriodFemtoseconds for exact time conversion.
    /// Numeric validity alone does not establish a usable HPET device.
    /// </summary>
    public static HpetClockCapabilitiesResult DecodeHpetCapabilities(ulong capabilities)
    {
        uint period = (uint)(capabilities >> 32);
        if (period == 0 || period > 100000000)
            return new(MonotonicClockMathStatus.InvalidPeriod, 0, 0, 0);
        return new(MonotonicClockMathStatus.Success, period, 1000000000000000UL / period,
            (capabilities & (1UL << 13)) != 0 ? 64U : 32U);
    }

    /// <summary>
    /// Accumulates one forward modular delta. Callers must sample within half the counter
    /// range and guarantee the counter was not reset. Arbitrary missed wraps cannot be
    /// detected from two samples. A zero delta is valid; half-range or larger is rejected
    /// as ambiguous/backwards. Failures return zero payload without changing caller state.
    /// </summary>
    public static MonotonicClockDeltaResult Accumulate(
        ulong previousRaw, ulong currentRaw, uint counterWidth, ulong accumulatedTicks)
    {
        if (counterWidth != 32 && counterWidth != 64)
            return new(MonotonicClockMathStatus.InvalidWidth, 0, 0);
        if (counterWidth == 32 && ((previousRaw | currentRaw) >> 32) != 0)
            return new(MonotonicClockMathStatus.InvalidCounter, 0, 0);

        ulong delta = unchecked(currentRaw - previousRaw);
        if (counterWidth == 32) delta &= 0xffffffffUL;
        ulong halfRange = counterWidth == 32 ? 0x80000000UL : 0x8000000000000000UL;
        if (delta >= halfRange)
            return new(MonotonicClockMathStatus.AmbiguousDelta, 0, 0);
        if (accumulatedTicks > ~0UL - delta)
            return new(MonotonicClockMathStatus.AccumulatedOverflow, 0, 0);
        return new(MonotonicClockMathStatus.Success, delta, accumulatedTicks + delta);
    }
}
