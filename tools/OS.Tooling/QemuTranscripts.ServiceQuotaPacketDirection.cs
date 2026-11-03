namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Logical extent includes holes; ContiguousBytes/KnownPrefixBase64
    /// stop at the first hole. FirstRanges retain every actual unique-byte island
    /// in original observation order. Their data is copied/read-only; an extent
    /// or byte count does not measure allocated component backing.</summary>

    public sealed record ServiceQuotaPacketDirection(int Extent, int ContiguousBytes, int KnownBytes, string KnownPrefixBase64, IReadOnlyList<ServiceQuotaPacketRange> FirstRanges);
}
