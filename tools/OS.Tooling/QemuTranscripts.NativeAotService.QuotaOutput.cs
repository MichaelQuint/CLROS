using System.Globalization;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Check borrowed reassembled fd1/fd2 text from the explicit v3
    /// fixture. Require original startup, recovered managed pressure, sequential
    /// native retention and a final attempt with no return-to-user observation.
    /// Reject survival, application failure, other contracts and wrong channels.
    /// This value-only seam executes no workload and grants no guest authority.
    /// Complete original framing, call/mapping/GC ownership, peer/packet overlap,
    /// native image, process and cleanup joins remain separate requirements.</summary>

    public static ServiceQuotaOutputInspection InspectNativeAotServiceQuotaOutputForTests(string stdout, string stderr)
    {
        Need(stdout is not null && stderr is not null && stderr.Length is> 0 and <= 65536 && stdout.Length <= 65536 - stderr.Length && stderr.EndsWith('\n'), "service_quota_output_extent");
        RequireQuotaHeartbeatWarnings(stdout!);
        string text = stderr!.Replace("\r\n", "\n", StringComparison.Ordinal);
        Need(text.All(c => c == '\n' || c is >= ' ' and <= '~'), "service_quota_output_ascii");
        string[] rows = text[..^1].Split('\n');
        Need(rows.Length is >= 12 and <= 140 && rows.All(r => r.Length is> 0 and <= 4096), "service_quota_output_rows");
        int position = 0;
        string Take()
        {
            Need(position < rows.Length, "service_quota_output_truncated");
            return rows[position++];
        }

        void Exact(string expected) => Need(Take() == expected, "service_quota_output_order");
        Exact("CLROS_APP_ENTRY minimal.api.service");
        Exact("CLROS_APP_CCTOR minimal.api.service");
        Exact("CLROS_APP_CTOR minimal.api.service");
        Exact("CLROS_APP_BUILT minimal.api.service");
        var gc = ReadServiceGcMeasurement(Take(), "CLROS_SERVICE_GC_BEGIN");
        Exact("CLROS_SERVICE_LISTEN contract=v3-quota endpoint=0.0.0.0:8080");
        Exact("CLROS_M10_011_READY");
        Exact("CLROS_SERVICE_RANDOM_CONTROLS zero=0 invalid=-22 fault=-14");
        Exact("CLROS_SERVICE_QUOTA_BEGIN chunkBytes=1048576 maximumChunks=64");
        const ulong chunk = 1_048_576, limit = 16 * chunk;
        // The reviewed startup performs three full collections outside the
        // measured ExhaustGc delta. Later absolute counters cannot precede it.
        ulong completed = 0, attempts = 0, collections = 0, priorCollection = gc.PressureCollections + 3, committed = 0;
        bool finalAttempt = false;
        while (position < rows.Length)
        {
            var attempt = ReadRecord(Take(), "CLROS_SERVICE_QUOTA_ATTEMPT", "index:dec", "retainedBytes:dec", "gcCommitted:dec");
            Need(completed < 64 && attempt["index"] == completed && attempt["retainedBytes"] == completed * chunk && attempt["gcCommitted"] <= limit, "service_quota_output_attempt");
            attempts++;
            committed = attempt["gcCommitted"];
            if (position == rows.Length)
            {
                finalAttempt = true;
                break;
            }

            var retained = ReadRecord(Take(), "CLROS_SERVICE_QUOTA_RETAINED", "index:dec", "retainedBytes:dec", "collectionsBefore:dec", "collectionsAfter:dec", "gcCommitted:dec");
            ulong before = retained["collectionsBefore"], after = retained["collectionsAfter"];
            Need(retained["index"] == completed && retained["retainedBytes"] == (completed + 1) * chunk && before >= priorCollection && after >= before && after <= int.MaxValue && (completed >= 4 || after > before) && retained["gcCommitted"] <= limit, "service_quota_output_retained");
            if (completed < 4)
                collections += after - before;
            priorCollection = after;
            committed = retained["gcCommitted"];
            completed++;
        }

        Need(finalAttempt && completed is> 0 and < 64 && attempts == completed + 1 && collections > 0, "service_quota_output_incomplete_pressure");
        return new(gc, completed, attempts, completed * chunk, collections, committed);
    }

    private static void RequireQuotaHeartbeatWarnings(string stdout)
    {
        if (stdout.Length == 0)
            return;
        string text = stdout.Replace("\r\n", "\n", StringComparison.Ordinal);
        Need(text.EndsWith('\n') && text.All(c => c == '\n' || c is >= ' ' and <= '~'), "service_quota_warning_ascii");
        string[] rows = text[..^1].Split('\n');
        Need(rows.Length >= 2 && rows.Length % 2 == 0, "service_quota_warning_rows");
        const string prefix = "      As of \"", middle = "\", the heartbeat has been running for \"", suffix = "\" which is longer than \"00:00:01\". This could be caused by thread pool starvation.";
        for (int index = 0; index < rows.Length; index += 2)
        {
            Need(rows[index] == "warn: Microsoft.AspNetCore.Server.Kestrel[22]", "service_quota_warning_category");
            string body = rows[index + 1];
            int split = body.IndexOf(middle, StringComparison.Ordinal);
            Need(body.StartsWith(prefix, StringComparison.Ordinal) && body.EndsWith(suffix, StringComparison.Ordinal) && split > prefix.Length && body.Length - suffix.Length > split + middle.Length, "service_quota_warning_message");
            string timestamp = body[prefix.Length..split], duration = body[(split + middle.Length)..^suffix.Length];
            const string dateFormat = "MM/dd/yyyy HH:mm:ss zzz";
            Need(DateTimeOffset.TryParseExact(timestamp, dateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var at) && at.Offset == TimeSpan.Zero && at.ToString(dateFormat, CultureInfo.InvariantCulture) == timestamp, "service_quota_warning_time");
            Need(TimeSpan.TryParseExact(duration, "c", CultureInfo.InvariantCulture, out var elapsed) && elapsed > TimeSpan.FromSeconds(1) && elapsed <= TimeSpan.FromSeconds(NativeAotServiceQuotaPeer.MaximumPeerSeconds) && elapsed.ToString("c", CultureInfo.InvariantCulture) == duration, "service_quota_warning_duration");
        }
    }
}
