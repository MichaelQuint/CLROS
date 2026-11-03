using OS.Std.Time;

namespace OS.Kernel.Platform;

public static partial class OwnedRealtimeClock
{
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
}
