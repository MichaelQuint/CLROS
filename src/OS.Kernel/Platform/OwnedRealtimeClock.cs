using OS.Std.Time;

namespace OS.Kernel.Platform;

/// <summary>Finite UTC anchored once to an independently admitted monotonic clock.
/// The serialized owner supplies ticks from that same clock/frequency; no firmware
/// pointers, callbacks, allocation or exceptions are used. This does not establish
/// the authenticity or accuracy of the supplied civil time or monotonic source.</summary>
public static class OwnedRealtimeClock
{
    public const long MaximumUnixSeconds = 253402300799;
    private const ulong Billion = 1000000000;

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
    }

    /// <summary>Copied 16-byte UEFI EFI_TIME value, without firmware ownership.
    /// Conversion validates all fields including padding. TimeZone is minutes
    /// added to local time to obtain UTC; Daylight must be zero.</summary>
    public struct FirmwareTime
    {
        public ushort Year;
        public byte Month, Day, Hour, Minute, Second, Pad1;
        public uint Nanosecond;
        public short TimeZone;
        public byte Daylight, Pad2;
    }

    /// <summary>Validate a copied firmware calendar in years1900..9999 and
    /// convert to UTC in 1970..9999. Unspecified timezone2047 requires an explicit
    /// UTC policy; seasonal rules are unsupported. Failure zeros both outputs.</summary>
    public static bool TryFirmwareUtc(FirmwareTime time, bool unspecifiedTimezoneIsUtc,
        out long seconds, out uint nanoseconds)
    {
        seconds = 0; nanoseconds = 0;
        if (time.Year is < 1900 or > 9999 || time.Nanosecond >= Billion ||
            time.Daylight != 0 || time.Pad1 != 0 || time.Pad2 != 0) return false;
        long offset = 0;
        if (time.TimeZone == 2047)
        {
            if (!unspecifiedTimezoneIsUtc) return false;
        }
        else
        {
            if (time.TimeZone is < -1440 or > 1440) return false;
            offset = time.TimeZone * 60L;
        }
        if (!GregorianUtc.TryUnixSeconds(time.Year, time.Month, time.Day, time.Hour,
            time.Minute, time.Second, out long local)) return false;
        long utc = local + offset; // Calendar and timezone bounds make addition safe.
        if (utc < 0 || utc > MaximumUnixSeconds) return false;
        seconds = utc; nanoseconds = time.Nanosecond;
        return true;
    }

    /// <summary>Admit a single UTC/monotonic anchor for an exclusive lifetime
    /// of1..86400 seconds at1MHz..10GHz. No firmware access occurs. The UTC value
    /// must be in1970..9999; every admission failure preserves the entire state.</summary>
    public static bool Begin(ref State state, long seconds, uint nanoseconds,
        ulong anchorTicks, ulong frequencyHz, ulong maximumElapsedSeconds)
    {
        if (state.Phase != 0 || seconds < 0 || seconds > MaximumUnixSeconds ||
            nanoseconds >= Billion || frequencyHz is < 1000000 or > 10000000000 ||
            maximumElapsedSeconds is < 1 or > 86400) return false;
        state = new State
        {
            Phase = 1, Seconds = seconds, Nanoseconds = nanoseconds,
            AnchorTicks = anchorTicks, LastTicks = anchorTicks,
            Frequency = frequencyHz, MaximumElapsedSeconds = maximumElapsedSeconds
        };
        return true;
    }

    /// <summary>Read anchored UTC without accumulating rounding error. Ticks
    /// before the anchor/last read, at or beyond expiry, or beyond year9999 fail
    /// permanently with zero outputs. Equal ticks are allowed. The caller must
    /// retain the exact admitted monotonic source; counter wrap is unsupported.</summary>
    public static bool Read(ref State state, ulong ticks, out long seconds, out uint nanoseconds)
    {
        seconds = 0; nanoseconds = 0;
        if (state.Phase != 1) return Invalidate(ref state);
        if (ticks < state.AnchorTicks || ticks < state.LastTicks) return Invalidate(ref state);
        ulong elapsed = ticks - state.AnchorTicks;
        if (elapsed >= state.MaximumElapsedSeconds * state.Frequency) return Invalidate(ref state);
        ulong whole = elapsed / state.Frequency;
        // Only the remainder is scaled: (<10GHz)*1e9 fits ulong, even when
        // elapsed ticks or a large absolute anchor could not be scaled safely.
        ulong fraction = elapsed % state.Frequency * Billion / state.Frequency;
        ulong nanos = state.Nanoseconds + fraction;
        whole += nanos / Billion;
        if (whole > (ulong)(MaximumUnixSeconds - state.Seconds)) return Invalidate(ref state);
        seconds = state.Seconds + (long)whole;
        nanoseconds = (uint)(nanos % Billion);
        state.LastTicks = ticks;
        return true;
    }

    private static bool Invalidate(ref State state) { state.Phase = 2; return false; }
}
