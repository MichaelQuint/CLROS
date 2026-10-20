namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied application values only. These authenticate neither the
    /// corresponding mmap/GC calls nor peer overlap, native policy or cleanup.</summary>
    public readonly record struct ServiceQuotaOutputInspection(ServiceGcMeasurement StartupGc,
        ulong CompletedChunks, ulong Attempts, ulong RetainedBytes, ulong ForcedCollections, ulong LastCommittedBytes);

    /// <summary>Check borrowed reassembled fd1/fd2 text from the explicit v3
    /// fixture. Require original startup, recovered managed pressure, sequential
    /// native retention and a final attempt with no return-to-user observation.
    /// Reject survival, application failure, other contracts and wrong channels.
    /// This value-only seam executes no workload and grants no guest authority.
    /// Complete original framing, call/mapping/GC ownership, peer/packet overlap,
    /// native image, process and cleanup joins remain separate requirements.</summary>
    public static ServiceQuotaOutputInspection InspectNativeAotServiceQuotaOutputForTests(string stdout, string stderr)
    {
        Need(stdout is not null && stderr is not null && stdout.Length == 0 &&
            stderr.Length is > 0 and <= 65536 && stderr.EndsWith('\n'), "service_quota_output_extent");
        string text = stderr!.Replace("\r\n", "\n", StringComparison.Ordinal);
        Need(text.All(c => c == '\n' || c is >= ' ' and <= '~'), "service_quota_output_ascii");
        string[] rows = text[..^1].Split('\n');
        Need(rows.Length is >= 11 and <= 140 && rows.All(r => r.Length is > 0 and <= 4096), "service_quota_output_rows");
        int position = 0;
        string Take() { Need(position < rows.Length, "service_quota_output_truncated"); return rows[position++]; }
        void Exact(string expected) => Need(Take() == expected, "service_quota_output_order");
        Exact("CLROS_APP_ENTRY minimal.api.service");
        Exact("CLROS_APP_CCTOR minimal.api.service");
        Exact("CLROS_APP_CTOR minimal.api.service");
        Exact("CLROS_APP_BUILT minimal.api.service");
        var gc = ReadServiceGcMeasurement(Take(), "CLROS_SERVICE_GC_BEGIN");
        Exact("CLROS_SERVICE_LISTEN contract=v3-quota endpoint=0.0.0.0:8080");
        Exact("CLROS_M10_011_READY");
        Exact("CLROS_SERVICE_QUOTA_BEGIN chunkBytes=1048576 maximumChunks=64");
        const ulong chunk = 1_048_576, limit = 16 * chunk;
        // The reviewed startup performs three full collections outside the
        // measured ExhaustGc delta. Later absolute counters cannot precede it.
        ulong completed = 0, attempts = 0, collections = 0, priorCollection = gc.PressureCollections + 3, committed = 0;
        bool finalAttempt = false;
        while (position < rows.Length)
        {
            var attempt = ReadRecord(Take(), "CLROS_SERVICE_QUOTA_ATTEMPT", "index:dec", "retainedBytes:dec", "gcCommitted:dec");
            Need(completed < 64 && attempt["index"] == completed && attempt["retainedBytes"] == completed * chunk &&
                attempt["gcCommitted"] <= limit, "service_quota_output_attempt");
            attempts++; committed = attempt["gcCommitted"];
            if (position == rows.Length) { finalAttempt = true; break; }
            var retained = ReadRecord(Take(), "CLROS_SERVICE_QUOTA_RETAINED", "index:dec", "retainedBytes:dec",
                "collectionsBefore:dec", "collectionsAfter:dec", "gcCommitted:dec");
            ulong before = retained["collectionsBefore"], after = retained["collectionsAfter"];
            Need(retained["index"] == completed && retained["retainedBytes"] == (completed + 1) * chunk &&
                before >= priorCollection && after >= before && after <= int.MaxValue &&
                (completed >= 4 || after > before) && retained["gcCommitted"] <= limit,
                "service_quota_output_retained");
            if (completed < 4) collections += after - before;
            priorCollection = after; committed = retained["gcCommitted"]; completed++;
        }
        Need(finalAttempt && completed is > 0 and < 64 && attempts == completed + 1 && collections > 0,
            "service_quota_output_incomplete_pressure");
        return new(gc, completed, attempts, completed * chunk, collections, committed);
    }
}
