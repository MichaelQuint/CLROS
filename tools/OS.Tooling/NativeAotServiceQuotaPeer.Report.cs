using System.Text;
using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    private const string ReportSchema = "clros.nativeaot.service.quota-peer.v1", ReportTickUnit = "elapsed-100ns";
    private const int ReportMaximumBytes = 65_536;
    private static readonly string[] ReportSampleNames = ["health.startup", "lifecycle.initial", "quota.initial", "worker.initial",
        "cancel.headers", "lifecycle.cancel", "lifecycle.held", "worker.start", "worker.running", "worker.prepressure",
        "quota.prepressure", "quota.retained", "worker.after_retained", "health.overlap", "worker.progress",
        "lifecycle.overlap", "quota.overlap", "quota.last"];

    /// <summary>Snapshot supplied values and serialize one deterministic JSON
    /// object, at most65536 UTF-8 bytes. All elapsed ticks use100ns; retained
    /// memory uses bytes and the remaining counters are counts. Owns its bounded
    /// temporary allocations and returns an immutable string. Invalid supplied
    /// values throw. This adds no child, HTTP, packet, Completion or guest authority.</summary>
    public static string SerializeReport(Observation observation)
    {
        var value = CopyReport(observation);
        RequireReport(value);
        string text = JsonSerializer.Serialize(new
        {
            schema = ReportSchema,
            tickUnit = ReportTickUnit,
            port = value.Port,
            connectionLimit = value.ConnectionLimit,
            heldTicks = value.HeldTicks,
            pressureStartedTicks = value.PressureStartedTicks,
            retainedTicks = value.RetainedTicks,
            healthyTicks = value.HealthyTicks,
            workerProgressTicks = value.WorkerProgressTicks,
            overlapTicks = value.OverlapTicks,
            pressureAbortedTicks = value.PressureAbortedTicks,
            pressureError = value.PressureError,
            retainedBytes = value.RetainedBytes,
            gcCollections = value.GcCollections,
            workerCallsBefore = value.WorkerCallsBefore,
            workerCallsAfter = value.WorkerCallsAfter,
            workerIterationsBefore = value.WorkerIterationsBefore,
            workerIterationsAfter = value.WorkerIterationsAfter,
            samples = value.Samples.Select(sample => new { name = sample.Name, elapsedTicks = sample.ElapsedTicks, body = sample.Body })
        });
        Need(Encoding.UTF8.GetByteCount(text) <= ReportMaximumBytes, "report_bytes");
        return text;
    }

    /// <summary>Inspect one borrowed UTF-8 report of at most65536 bytes with
    /// exactly19 root fields and18 ordered samples, each with3 fields and at
    /// most8192 body characters. Reject duplicate/missing/unknown fields, malformed
    /// values and inconsistent overlap witnesses. Elapsed ticks are100ns within
    /// a900s quota report bound; counters retain their declared integer units. Returns
    /// copied immutable strings and a read-only sample array. Supplied values
    /// prove no original I/O, pending task, socket ownership, kernel quota,
    /// process exit, packet, native image, cleanup, Completion or guest run.</summary>
    public static Observation InspectReport(ReadOnlySpan<byte> utf8)
    {
        Need(!utf8.IsEmpty && utf8.Length <= ReportMaximumBytes, "report_bytes");
        try
        {
            using var document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { MaxDepth = 4 });
            var root = document.RootElement;
            ReportFields(root, "schema", "tickUnit", "port", "connectionLimit", "heldTicks", "pressureStartedTicks", "retainedTicks",
                "healthyTicks", "workerProgressTicks", "overlapTicks", "pressureAbortedTicks", "pressureError", "retainedBytes",
                "gcCollections", "workerCallsBefore", "workerCallsAfter", "workerIterationsBefore", "workerIterationsAfter", "samples");
            Need(ReportString(root, "schema") == ReportSchema && ReportString(root, "tickUnit") == ReportTickUnit, "report_schema");
            var rows = root.GetProperty("samples");
            Need(rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() == ReportSampleNames.Length, "report_samples");
            var samples = new Sample[ReportSampleNames.Length];
            int i = 0;
            foreach (var row in rows.EnumerateArray())
            {
                ReportFields(row, "name", "elapsedTicks", "body");
                samples[i++] = new(ReportString(row, "name"), ReportLong(row, "elapsedTicks"), ReportString(row, "body"));
            }
            long port = ReportLong(root, "port"), connections = ReportLong(root, "connectionLimit");
            Need(port is >= 1 and <= 65535 && connections == 4, "report_connection_values");
            var result = new Observation((int)port, (int)connections, ReportLong(root, "heldTicks"), ReportLong(root, "pressureStartedTicks"),
                ReportLong(root, "retainedTicks"), ReportLong(root, "healthyTicks"), ReportLong(root, "workerProgressTicks"),
                ReportLong(root, "overlapTicks"), ReportLong(root, "pressureAbortedTicks"), ReportString(root, "pressureError"),
                ReportUnsigned(root, "retainedBytes"), ReportUnsigned(root, "gcCollections"), ReportLong(root, "workerCallsBefore"),
                ReportLong(root, "workerCallsAfter"), ReportLong(root, "workerIterationsBefore"), ReportLong(root, "workerIterationsAfter"),
                Array.AsReadOnly(samples));
            RequireReport(result);
            return result;
        }
        catch (JsonException error)
        {
            throw new InvalidDataException("quota_peer_report_json", error);
        }
    }

    private static Observation CopyReport(Observation value)
    {
        Need(value is not null && value.Samples is not null && value.Samples.Count == ReportSampleNames.Length, "report_samples");
        var samples = new Sample[ReportSampleNames.Length];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = value!.Samples![i];
        return value! with
        {
            Samples = Array.AsReadOnly(samples)
        };
    }

    private static void RequireReport(Observation value)
    {
        const long maximumTicks = (long)MaximumPeerSeconds * TimeSpan.TicksPerSecond;
        Need(value.Port is >= 1 and <= 65535 && value.ConnectionLimit == 4 &&
            value.PressureError is "ConnectionError" or "ResponseEnded", "report_connection_values");
        var samples = value.Samples;
        long previous = 0;
        for (int i = 0; i < ReportSampleNames.Length; i++)
        {
            var sample = samples[i];
            Need(sample.Name == ReportSampleNames[i] && sample.ElapsedTicks > 0 && sample.ElapsedTicks >= previous &&
                sample.ElapsedTicks <= maximumTicks && sample.Body is not null && sample.Body.Length is > 0 and <= 8192 &&
                sample.Body.All(c => c is >= ' ' and <= '~' || i == 4 && (c == '\r' || c == '\n')), "report_sample_values");
            previous = sample.ElapsedTicks;
        }
        Need(value.HeldTicks == samples[6].ElapsedTicks && value.PressureStartedTicks >= samples[10].ElapsedTicks &&
            value.RetainedTicks == samples[11].ElapsedTicks && value.RetainedTicks >= value.PressureStartedTicks &&
            value.HealthyTicks == samples[13].ElapsedTicks && value.WorkerProgressTicks == samples[14].ElapsedTicks &&
            value.OverlapTicks >= samples[16].ElapsedTicks && value.PressureAbortedTicks > value.OverlapTicks &&
            value.PressureAbortedTicks <= maximumTicks, "report_tick_join");
        Need(samples[0].Body == "CLROS_SERVICE_HEALTH_OK" && samples[13].Body == "CLROS_SERVICE_HEALTH_OK" &&
            samples[7].Body == "CLROS_SERVICE_STARTED", "report_health_start");
        Need(ReadLifecycle(samples[1].Body).Values.All(v => v == 0) && HeldStatus(ReadLifecycle(samples[5].Body), false) &&
            HeldStatus(ReadLifecycle(samples[6].Body), true) && HeldStatus(ReadLifecycle(samples[15].Body), true), "report_held");
        string headers = samples[4].Body;
        Need(headers.EndsWith("\r\n\r\n", StringComparison.Ordinal), "report_cancel_headers");
        var head = NativeAotServicePeer.ResponseHead(Encoding.ASCII.GetBytes(headers[..^4]));
        Need(head.Status == 200 && head.Length is 0 or -1, "report_cancel_headers");

        Need(ReadQuota(samples[2].Body) == default && ReadQuota(samples[10].Body) == default, "report_quota_initial");
        QuotaStatus prior = default;
        foreach (int at in new[] { 11, 16, 17 })
        {
            var quota = ReadQuota(samples[at].Body);
            Progress(quota, prior);
            Need(quota.Phase == 1 && quota.RetainedBytes >= ChunkBytes && quota.Attempts >= quota.RetainedBytes / ChunkBytes &&
                quota.GcCollections >= Math.Min(quota.RetainedBytes / ChunkBytes, 4UL), "report_quota_overlap");
            if (at == 16)
                Need(value.RetainedBytes == quota.RetainedBytes && value.GcCollections == quota.GcCollections, "report_quota_join");
            prior = quota;
        }
        ServiceStatus worker = ServiceStatus.Parse(Encoding.ASCII.GetBytes(samples[3].Body));
        Need(worker.Phase == 0 && worker.Failure == 0 && worker.ManagedWorkerId == 0 && worker.Calls == 0 && worker.RandomRequests == 0 &&
            worker.RandomBytes == 0 && worker.Iterations == 0 && worker.ElapsedMilliseconds == 0, "report_worker_initial");
        foreach (int at in new[] { 8, 9, 12, 14 })
        {
            var next = ServiceStatus.Parse(Encoding.ASCII.GetBytes(samples[at].Body));
            next.RequireProgressFrom(worker);
            Need(next.Phase == 1 && next.Failure == 0 && next.ManagedWorkerId > 0 && next.Calls > 0 && next.Iterations > 0 &&
                next.RandomRequests > 32 && next.RandomBytes > 4096, "report_worker_running");
            if (at is 9 or 14)
                Need(next.Calls > worker.Calls && next.Iterations > worker.Iterations, "report_worker_progress");
            if (at == 12)
                Need(value.WorkerCallsBefore == next.Calls && value.WorkerIterationsBefore == next.Iterations, "report_worker_join");
            if (at == 14)
                Need(value.WorkerCallsAfter == next.Calls && value.WorkerIterationsAfter == next.Iterations, "report_worker_join");
            worker = next;
        }
    }

    private static void ReportFields(JsonElement value, params string[] fields)
    {
        Need(value.ValueKind == JsonValueKind.Object, "report_fields");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in value.EnumerateObject())
            Need(fields.Contains(field.Name, StringComparer.Ordinal) && seen.Add(field.Name), "report_fields");
        Need(seen.Count == fields.Length, "report_fields");
    }
    private static string ReportString(JsonElement value, string name)
    {
        var field = value.GetProperty(name);
        Need(field.ValueKind == JsonValueKind.String, "report_string");
        return field.GetString()!;
    }
    private static long ReportLong(JsonElement value, string name)
    {
        var field = value.GetProperty(name);
        Need(field.ValueKind == JsonValueKind.Number && field.TryGetInt64(out _), "report_integer");
        long number = field.GetInt64();
        Need(number >= 0, "report_integer");
        return number;
    }
    private static ulong ReportUnsigned(JsonElement value, string name)
    {
        var field = value.GetProperty(name);
        Need(field.ValueKind == JsonValueKind.Number && field.TryGetUInt64(out _), "report_integer");
        return field.GetUInt64();
    }
}
