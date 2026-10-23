using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static class NativeAotServiceQuotaPeerReportTests
{
    [HostTest("quota peer report round trips immutable copied values without execution authority")]
    private static void Complete()
    {
        var fixture = Fixture(); var samples = fixture.Samples.ToArray(); fixture = fixture with { Samples = samples };
        string text = NativeAotServiceQuotaPeer.SerializeReport(fixture); byte[] bytes = Encoding.UTF8.GetBytes(text);
        var result = NativeAotServiceQuotaPeer.InspectReport(bytes);
        HostCheck.Equal(text, NativeAotServiceQuotaPeer.SerializeReport(result));
        HostCheck.True(result.Port == 8080 && result.ConnectionLimit == 4 && result.Samples.Count == 18 &&
            result.RetainedBytes == 2UL * 1048576 && result.GcCollections == 2 && result.WorkerCallsBefore == 102 && result.WorkerCallsAfter == 103);
        bytes.AsSpan().Clear(); samples[0] = samples[0] with { Body = "mutated" };
        HostCheck.Equal(text, NativeAotServiceQuotaPeer.SerializeReport(result));
        bool immutable = false;
        try { ((IList<NativeAotServiceQuotaPeer.Sample>)result.Samples)[0] = default; }
        catch (NotSupportedException) { immutable = true; }
        HostCheck.True(immutable);
        var late = Root(); Row(late, 17)["elapsedTicks"] = 20;
        HostCheck.True(Inspect(late).Samples[17].ElapsedTicks > Inspect(late).PressureAbortedTicks);
        var unchanged = Root(); Row(unchanged, 17)["elapsedTicks"] = 17; Row(unchanged, 17)["body"] = Row(unchanged, 16)["body"]!.GetValue<string>();
        HostCheck.Equal(17L, Inspect(unchanged).Samples[17].ElapsedTicks);
    }

    [HostTest("quota peer report rejects missing duplicate unknown mistyped and oversized fields or samples")]
    private static void Shape()
    {
        string text = NativeAotServiceQuotaPeer.SerializeReport(Fixture());
        foreach (string bad in new[] { "", "null", "[]", "{}", text + "{}", new string(' ', 65537),
            text.Replace("\"port\":8080", "\"port\":8080,\"port\":8080", StringComparison.Ordinal),
            text.Replace("\"port\":8080", "\"port\":8080,\"\\u0070ort\":8080", StringComparison.Ordinal),
            text.Replace("\"name\":\"health.startup\"", "\"name\":\"health.startup\",\"\\u006eame\":\"health.startup\"", StringComparison.Ordinal),
            text.Replace("\"elapsedTicks\":1,", "\"elapsedTicks\":1,\"elapsedTicks\":1,", StringComparison.Ordinal) })
            Reject(() => NativeAotServiceQuotaPeer.InspectReport(Encoding.UTF8.GetBytes(bad)));
        Reject(() => NativeAotServiceQuotaPeer.InspectReport(new byte[] { 0xff }));
        foreach (string field in Root().Select(field => field.Key).ToArray())
        { var root = Root(); root.Remove(field); Reject(root); }
        foreach (string field in new[] { "name", "elapsedTicks", "body" })
        { var root = Root(); Row(root, 0).Remove(field); Reject(root); }
        foreach (Action<JsonObject> change in new Action<JsonObject>[] {
            r => r["extra"] = 1, r => Row(r, 0)["extra"] = 1, r => r["schema"] = "other", r => r["tickUnit"] = "milliseconds",
            r => r["port"] = "8080", r => r["port"] = -1, r => r["port"] = 65536, r => r["port"] = 8080.5,
            r => r["connectionLimit"] = 5, r => r["workerCallsBefore"] = ulong.MaxValue, r => r["retainedBytes"] = -1,
            r => r["pressureError"] = null, r => r["samples"] = null, r => r["samples"]!.AsArray().RemoveAt(17),
            r => r["samples"]!.AsArray().Add(Row(r, 17).DeepClone()), r => Row(r, 0)["name"] = "quota.unknown",
            r => Row(r, 1)["name"] = "health.startup", r => Row(r, 0)["body"] = new string('x', 8193),
            r => Row(r, 0)["body"] = "CLROS_SERVICE_HEALTH_OK\n", r => Row(r, 0)["body"] = "\u0100",
            r => Row(r, 0)["elapsedTicks"] = 0, r => Row(r, 17)["elapsedTicks"] = 9_000_000_001L })
        { var root = Root(); change(root); Reject(root); }
    }

    [HostTest("quota peer report rejoins pressure health worker held and retained GC overlap witnesses")]
    private static void Witnesses()
    {
        foreach (Action<JsonObject> change in new Action<JsonObject>[] {
            r => r["heldTicks"] = 6, r => r["pressureStartedTicks"] = 6, r => r["pressureStartedTicks"] = 13,
            r => r["retainedTicks"] = 13, r => r["healthyTicks"] = 15, r => r["workerProgressTicks"] = 16,
            r => r["overlapTicks"] = 16, r => r["pressureAbortedTicks"] = 17, r => r["pressureError"] = "Timeout",
            r => r["retainedBytes"] = 1048576, r => r["gcCollections"] = 1,
            r => r["workerCallsBefore"] = 101, r => r["workerCallsAfter"] = 104, r => r["workerIterationsBefore"] = 3,
            r => r["workerIterationsAfter"] = 6, r => Row(r, 13)["body"] = "wrong health",
            r => Row(r, 7)["body"] = "CLROS_SERVICE_STOPPED", r => Row(r, 13)["elapsedTicks"] = 11,
            r => Row(r, 17)["elapsedTicks"] = 16,
            r => Row(r, 4)["body"] = Headers().Replace("200 OK", "409 Conflict", StringComparison.Ordinal),
            r => Row(r, 4)["body"] = Headers().Replace("Content-Length: 0", "Content-Length: 1", StringComparison.Ordinal),
            r => Row(r, 4)["body"] = Headers() + "extra", r => Row(r, 1)["body"] = Lifecycle(1, 0, 0, 0),
            r => Row(r, 5)["body"] = Lifecycle(1, 0, 1, 0), r => Row(r, 6)["body"] = Lifecycle(1, 1, 1, 64),
            r => Row(r, 15)["body"] = Lifecycle(2, 1, 1, 63),
            r => Row(r, 15)["body"] = Lifecycle(2, 1, 1, 64).Replace("canceled=0", "canceled=1", StringComparison.Ordinal),
            r => Row(r, 2)["body"] = Quota(1, 1, 0, 0), r => Row(r, 10)["body"] = Quota(1, 1, 0, 0),
            r => Row(r, 11)["body"] = Quota(1, 1, 1048576, 0), r => Row(r, 16)["body"] = Quota(1, 1, 2097152, 2),
            r => Row(r, 17)["body"] = Quota(2, 3, 3145728, 3), r => Row(r, 17)["body"] = Quota(1, 1, 1048576, 1),
            r => Row(r, 3)["body"] = Worker(0, 0, 0, 0, 1, 0, 0),
            r => Row(r, 8)["body"] = Worker(1, 7, 100, 32, 4097, 2, 100),
            r => Row(r, 9)["body"] = Worker(1, 7, 100, 33, 4097, 3, 200),
            r => Row(r, 12)["body"] = Worker(1, 7, 102, 33, 4097, 4, 300).Replace("\"failure\":0", "\"failure\":1", StringComparison.Ordinal),
            r => Row(r, 14)["body"] = Worker(1, 8, 103, 33, 4097, 5, 400),
            r => Row(r, 14)["body"] = Worker(1, 7, 102, 33, 4097, 5, 400) })
        { var root = Root(); change(root); Reject(root); }
    }

    [HostTest("quota peer serializer refuses invalid source values before emitting a report")]
    private static void SerializerRefusals()
    {
        var fixture = Fixture();
        foreach (var bad in new[] { fixture with { Port = 0 }, fixture with { PressureError = "ResponseHeaders" },
            fixture with { PressureAbortedTicks = fixture.OverlapTicks }, fixture with { GcCollections = 0 },
            fixture with { WorkerCallsBefore = 0 }, fixture with { Samples = [] } })
            Reject(() => NativeAotServiceQuotaPeer.SerializeReport(bad));
        Reject(() => NativeAotServiceQuotaPeer.SerializeReport(null!));
    }

    [HostTest("quota peer copied report accepts beyond600 through900 seconds and refuses the next tick")]
    private static void TimeBoundary()
    {
        const long maximum = 900L * TimeSpan.TicksPerSecond;
        foreach (long end in new[] { 600L * TimeSpan.TicksPerSecond + 1, maximum })
        {
            string json = NativeAotServiceQuotaPeer.SerializeReport(AtEnd(end));
            var copy = NativeAotServiceQuotaPeer.InspectReport(Encoding.UTF8.GetBytes(json));
            HostCheck.True(copy.PressureAbortedTicks == end && copy.Samples[^1].ElapsedTicks == end && copy.HeldTicks > 600L * TimeSpan.TicksPerSecond - 19);
            HostCheck.Equal(json, NativeAotServiceQuotaPeer.SerializeReport(copy));
        }
        Reject(() => NativeAotServiceQuotaPeer.SerializeReport(AtEnd(maximum + 1)));
        var lateSample = JsonNode.Parse(NativeAotServiceQuotaPeer.SerializeReport(AtEnd(maximum)))!.AsObject();
        Row(lateSample, 17)["elapsedTicks"] = maximum + 1; Reject(lateSample);
        var lateAbort = JsonNode.Parse(NativeAotServiceQuotaPeer.SerializeReport(AtEnd(maximum)))!.AsObject();
        lateAbort["pressureAbortedTicks"] = maximum + 1; Reject(lateAbort);
    }

    private static NativeAotServiceQuotaPeer.Observation AtEnd(long end)
    {
        var original = Fixture(); long shift = end - original.PressureAbortedTicks;
        var samples = original.Samples.Select(s => s with { ElapsedTicks = s.ElapsedTicks + shift }).ToArray();
        samples[^1] = samples[^1] with { ElapsedTicks = end };
        return original with { HeldTicks = original.HeldTicks + shift, PressureStartedTicks = original.PressureStartedTicks + shift,
            RetainedTicks = original.RetainedTicks + shift, HealthyTicks = original.HealthyTicks + shift,
            WorkerProgressTicks = original.WorkerProgressTicks + shift, OverlapTicks = original.OverlapTicks + shift,
            PressureAbortedTicks = end, Samples = samples };
    }

    private static NativeAotServiceQuotaPeer.Observation Fixture()
    {
        string[] names = ["health.startup", "lifecycle.initial", "quota.initial", "worker.initial", "cancel.headers", "lifecycle.cancel",
            "lifecycle.held", "worker.start", "worker.running", "worker.prepressure", "quota.prepressure", "quota.retained",
            "worker.after_retained", "health.overlap", "worker.progress", "lifecycle.overlap", "quota.overlap", "quota.last"];
        string[] bodies = ["CLROS_SERVICE_HEALTH_OK", Lifecycle(0, 0, 0, 0), Quota(0, 0, 0, 0), Worker(0, 0, 0, 0, 0, 0, 0), Headers(),
            Lifecycle(1, 1, 1, 0), Lifecycle(2, 1, 1, 64), "CLROS_SERVICE_STARTED", Worker(1, 7, 100, 33, 4097, 2, 100),
            Worker(1, 7, 101, 33, 4097, 3, 200), Quota(0, 0, 0, 0), Quota(1, 1, 1048576, 1), Worker(1, 7, 102, 33, 4097, 4, 300),
            "CLROS_SERVICE_HEALTH_OK", Worker(1, 7, 103, 33, 4097, 5, 400), Lifecycle(2, 1, 1, 64), Quota(1, 2, 2097152, 2), Quota(1, 3, 3145728, 3)];
        var samples = names.Select((name, i) => new NativeAotServiceQuotaPeer.Sample(name, i + 1, bodies[i])).ToArray();
        return new(8080, 4, 7, 11, 12, 14, 15, 17, 19, "ConnectionError", 2097152, 2, 102, 103, 4, 5, samples);
    }
    private static string Headers() => "HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 0\r\nDate: Tue, 20 Oct 2026 00:00:00 GMT\r\n\r\n";
    private static string Lifecycle(int active, int waiting, int waits, int read) => FormattableString.Invariant(
        $"CLROS_LIFECYCLE_STATUS active={active} waiting={waiting} waits={waits} canceled=0 echoes=0 streams=0 rejected=0 read={read} written=0");
    private static string Quota(int phase, int attempts, long bytes, int gc) => FormattableString.Invariant(
        $"CLROS_QUOTA_STATUS phase={phase} attempts={attempts} retainedBytes={bytes} gcCollections={gc}");
    private static string Worker(int phase, int id, long calls, long requests, long bytes, long iterations, long ms) => new JsonObject {
        ["contract"] = "clros.service.v1", ["phase"] = phase, ["failure"] = 0, ["managedWorkerId"] = id, ["calls"] = calls,
        ["randomRequests"] = requests, ["randomBytes"] = bytes, ["iterations"] = iterations, ["elapsedMilliseconds"] = ms }.ToJsonString();
    private static JsonObject Root() => JsonNode.Parse(NativeAotServiceQuotaPeer.SerializeReport(Fixture()))!.AsObject();
    private static JsonObject Row(JsonObject root, int index) => root["samples"]!.AsArray()[index]!.AsObject();
    private static NativeAotServiceQuotaPeer.Observation Inspect(JsonObject root) => NativeAotServiceQuotaPeer.InspectReport(Encoding.UTF8.GetBytes(root.ToJsonString()));
    private static void Reject(JsonObject root) => Reject(() => Inspect(root));
    private static void Reject(Action action)
    {
        bool refused = false; try { action(); } catch (Exception error) when (error is ToolFailure or InvalidDataException) { refused = true; }
        HostCheck.True(refused);
    }
}
