using System.Globalization;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Clipped interval bytes retain the original first-seen record,
    /// direction, offset and sec/usec. StreamOffset is absolute in that direction;
    /// ByteCount may cover only part of Original. No clock origin is inferred.</summary>

    public readonly record struct ServiceQuotaHttpPacketRange(ServiceWireByteRange Original, int StreamOffset, int ByteCount);
}
