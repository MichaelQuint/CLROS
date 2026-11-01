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
