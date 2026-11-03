namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied stream/range inventory; no HTTP, flow mapping, close,
    /// file/process, native, clock or guest acceptance verdict.</summary>

    public sealed record ServiceQuotaPacketStreams(int TcpRecords, int QuietSyns, int AggregateExtent, IReadOnlyList<ServiceQuotaPacketStream> Streams);
}
