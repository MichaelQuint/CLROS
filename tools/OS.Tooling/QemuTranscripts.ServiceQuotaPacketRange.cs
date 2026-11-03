namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied bytes first seen at the original range's PCAP record.
    /// A range is an observed island, never invented hole contents. Original
    /// sec/usec values grant no host/guest-clock or execution authority.</summary>

    public sealed record ServiceQuotaPacketRange(ServiceWireByteRange First, string DataBase64);
}
