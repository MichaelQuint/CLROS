namespace OS.Tooling;

public static partial class NativeAotDiskTimingPeer
{
    /// <summary>
    /// Explicit host diagnostic request controls. The timeout covers headers and
    /// complete streamed body receipt. It may shorten the existing 180-second
    /// limit. The separate peer and runner deadlines still apply.
    /// </summary>
    public sealed class BenchmarkOptions
    {
        /// <summary>Positive header/body receipt budget, at most 180 seconds.</summary>
        public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(180);

        /// <summary>Original evidence unless a diagnostic profile is explicitly selected.</summary>
        public DiskEvidenceMode DiskEvidence { get; init; }

        /// <summary>
        /// Exact original image bytes for Trace/Summary only. RunAsync copies and
        /// hashes them before creating artifacts or starting a runner. The caller
        /// retains original image/source/native authority until the runner joins.
        /// </summary>
        public ReadOnlyMemory<byte> DiskImageBytes { get; init; }

        internal static BenchmarkOptions ForProfile(IReadOnlyDictionary<string, bool> flags, Func<byte[]> readImage)
        {
            bool trace = flags.TryGetValue("EnableM10018EvidenceTraceProbe", out bool traceSelected) && traceSelected;
            bool summary = flags.TryGetValue("EnableM10018EvidenceSummaryProbe", out bool summarySelected) && summarySelected;
            if (trace && summary)
                throw new ToolFailure("disk_timing_evidence_mode");
            var mode = trace ? DiskEvidenceMode.Trace : summary ? DiskEvidenceMode.Summary : DiskEvidenceMode.Original;
            return new BenchmarkOptions
            {
                DiskEvidence = mode,
                DiskImageBytes = mode == DiskEvidenceMode.Original ? ReadOnlyMemory<byte>.Empty : readImage()
            };
        }

        internal void Validate()
        {
            if (RequestTimeout <= TimeSpan.Zero || RequestTimeout > TimeSpan.FromSeconds(180))
                throw new ToolFailure("disk_timing_request_timeout");
            if (DiskEvidence is not (DiskEvidenceMode.Original or DiskEvidenceMode.Trace or DiskEvidenceMode.Summary) ||
                (DiskEvidence == DiskEvidenceMode.Original ? !DiskImageBytes.IsEmpty : DiskImageBytes.IsEmpty))
                throw new ToolFailure("disk_timing_evidence_mode");
        }
    }
}
