using OS.Std.Time;

namespace OS.Kernel.Platform;

public static partial class OwnedRealtimeClock
{
    /// <summary>Zero-initialize once, retain exclusively and never copy a live
    /// owner. Calls must be serialized with monotonic reads. A failed Read makes
    /// this owner permanently unavailable; Begin failure leaves it unchanged.</summary>

    public struct State
    {
        internal uint Phase;
        internal long Seconds;
        internal uint Nanoseconds;
        internal ulong AnchorTicks, LastTicks, Frequency, MaximumElapsedSeconds;
        public readonly bool IsAvailable => Phase == 1;
        /// <summary>Admitted tick frequency, or zero when unavailable.</summary>

        public readonly ulong FrequencyHz => IsAvailable ? Frequency : 0;
        /// <summary>Explicit continuing admission, without a finite elapsed
        /// ceiling. Monotonic rollback, counter wrap and calendar overflow
        /// still invalidate this same owner permanently.</summary>

        public readonly bool IsContinuing => IsAvailable && MaximumElapsedSeconds == 0;
    }
}
