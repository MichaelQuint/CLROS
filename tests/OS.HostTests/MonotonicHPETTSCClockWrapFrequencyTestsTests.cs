using OS.Kernel.Platform;

internal static class MonotonicHPETTSCClockWrapFrequencyTestsTests
{
    [HostTest("clock.math decodes hpet period frequency and width", HostLane.Fast, 0)]
    private static bool DecodesHpetCapabilities()
    {
        (ulong Capabilities, uint Period, ulong Frequency, uint Width)[] vectors =
        [
            (0x0000000100000000, 1, 1000000000000000, 32),
            (0x05F5E10000002000, 100000000, 10000000, 64),
            (0x00000003FFFFDFFF, 3, 333333333333333, 32),
            (0x00989680FFFFFFFF, 10000000, 100000000, 64)
        ];
        foreach (var vector in vectors)
        {
            var result = MonotonicHpetTscClockMath.DecodeHpetCapabilities(vector.Capabilities);
            HostCheck.True(result.Succeeded && result.Status == MonotonicClockMathStatus.Success);
            HostCheck.Equal(vector.Period, result.PeriodFemtoseconds);
            HostCheck.Equal(vector.Frequency, result.FrequencyHz);
            HostCheck.Equal(vector.Width, result.CounterWidth);
        }
        foreach (ulong capabilities in new ulong[] { 0, 0xFFFFFFFF, 0x05F5E10100002000, ulong.MaxValue })
        {
            var result = MonotonicHpetTscClockMath.DecodeHpetCapabilities(capabilities);
            HostCheck.Equal(MonotonicClockMathStatus.InvalidPeriod, result.Status);
            HostCheck.True(!result.Succeeded && result.PeriodFemtoseconds == 0 && result.FrequencyHz == 0 && result.CounterWidth == 0);
        }
        return true;
    }

    [HostTest("clock.math accumulates zero forward and wrapped counters", HostLane.Fast, 1)]
    private static bool AccumulatesForwardAndWrappedCounters()
    {
        (ulong Previous, ulong Current, uint Width, ulong Total, ulong Delta, ulong Expected)[] vectors =
        [
            (7, 7, 32, 900, 0, 900),
            (7, 12, 64, 900, 5, 905),
            (0xFFFFFFFD, 2, 32, 100, 5, 105),
            (ulong.MaxValue - 2, 2, 64, 100, 5, 105),
            (0, 0x7FFFFFFF, 32, 0, 0x7FFFFFFF, 0x7FFFFFFF),
            (0, 0x7FFFFFFFFFFFFFFF, 64, 0, 0x7FFFFFFFFFFFFFFF, 0x7FFFFFFFFFFFFFFF),
            (0, 5, 32, ulong.MaxValue - 5, 5, ulong.MaxValue),
            (ulong.MaxValue, ulong.MaxValue, 64, ulong.MaxValue, 0, ulong.MaxValue)
        ];
        foreach (var vector in vectors)
        {
            var result = MonotonicHpetTscClockMath.Accumulate(vector.Previous, vector.Current, vector.Width, vector.Total);
            HostCheck.True(result.Succeeded && result.Status == MonotonicClockMathStatus.Success);
            HostCheck.Equal(vector.Delta, result.DeltaTicks);
            HostCheck.Equal(vector.Expected, result.AccumulatedTicks);
        }
        return true;
    }

    [HostTest("clock.math rejects ambiguous backwards invalid and overflowing samples", HostLane.Fast, 2)]
    private static bool RejectsInvalidSamples()
    {
        (ulong Previous, ulong Current, uint Width, ulong Total, MonotonicClockMathStatus Status)[] vectors =
        [
            (0, 0x80000000, 32, 0, MonotonicClockMathStatus.AmbiguousDelta),
            (0, 0x8000000000000000, 64, 0, MonotonicClockMathStatus.AmbiguousDelta),
            (42, 41, 32, 100, MonotonicClockMathStatus.AmbiguousDelta),
            (42, 41, 64, 100, MonotonicClockMathStatus.AmbiguousDelta),
            (0x100000000, 1, 32, 100, MonotonicClockMathStatus.InvalidCounter),
            (1, 0x100000000, 32, 100, MonotonicClockMathStatus.InvalidCounter),
            (0, 1, 0, 100, MonotonicClockMathStatus.InvalidWidth),
            (0, 1, 33, 100, MonotonicClockMathStatus.InvalidWidth),
            (0, 1, 65, 100, MonotonicClockMathStatus.InvalidWidth),
            (0, 1, 32, ulong.MaxValue, MonotonicClockMathStatus.AccumulatedOverflow),
            (ulong.MaxValue - 2, 2, 64, ulong.MaxValue - 4, MonotonicClockMathStatus.AccumulatedOverflow)
        ];
        foreach (var vector in vectors)
        {
            var result = MonotonicHpetTscClockMath.Accumulate(vector.Previous, vector.Current, vector.Width, vector.Total);
            HostCheck.Equal(vector.Status, result.Status);
            HostCheck.True(!result.Succeeded && result.DeltaTicks == 0 && result.AccumulatedTicks == 0);
        }
        return true;
    }

    [HostTest("clock.math defaults fail and rejected sample preserves caller state", HostLane.Fast, 3)]
    private static bool DefaultsAndFailureAtomicity()
    {
        HpetClockCapabilitiesResult capabilities = default;
        MonotonicClockDeltaResult delta = default;
        HostCheck.True(!capabilities.Succeeded && capabilities.Status == MonotonicClockMathStatus.Uninitialized &&
            capabilities.PeriodFemtoseconds == 0 && capabilities.FrequencyHz == 0 && capabilities.CounterWidth == 0);
        HostCheck.True(!delta.Succeeded && delta.Status == MonotonicClockMathStatus.Uninitialized &&
            delta.DeltaTicks == 0 && delta.AccumulatedTicks == 0);

        ulong previous = 100, accumulated = 1000;
        var rejected = MonotonicHpetTscClockMath.Accumulate(previous, 99, 32, accumulated);
        HostCheck.True(!rejected.Succeeded && rejected.DeltaTicks == 0 && rejected.AccumulatedTicks == 0);
        HostCheck.True(previous == 100 && accumulated == 1000);
        var next = MonotonicHpetTscClockMath.Accumulate(previous, 105, 32, accumulated);
        return HostCheck.Result(next.Succeeded && next.DeltaTicks == 5 && next.AccumulatedTicks == 1005);
    }
}
