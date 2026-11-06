using OS.Manifest;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied denial/trailer values only. These authenticate no package,
    /// scheduler, mapping, application output, peer, native image or guest run.</summary>

    public readonly record struct ServiceQuotaInspection(ulong Identity, ulong Ordinal, ulong BackedPages, ulong PoolPages, ulong Reaped);
}
