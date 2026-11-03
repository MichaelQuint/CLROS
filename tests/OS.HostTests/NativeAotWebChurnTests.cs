using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static class NativeAotWebChurnTests
{
    private const string Endpoint = "http://127.0.0.1:23456", Health = "OS_MINIMAL_API_OK";
    // Literal copied observations: no helper constants, execution, file or
    // native/guest authority is manufactured by this independent JSON vector.
    private static JsonObject Evidence(int canceledIterations = 0, string health = Health, bool canceledTimerOnly = false)
    {
        JsonArray rows = [];
        int work = 0, extraTimers = 0;
        void Row(string kind, int cycle, int status, string body, string caseName = "") => rows.Add(new JsonObject
        {
            ["kind"] = kind,
            ["cycle"] = cycle,
            ["case"] = caseName,
            ["status"] = status,
            ["body"] = body
        });
        void Status(string kind, int cycle, int started, int completed, int canceled, int active)
        {
            Row(kind, cycle, 200, $"CLROS_CHURN_STATUS active={active} waiting={active} peakActive={(started == 0 ? 0 : 1)} started={started} completed={completed} canceled={canceled} failed=0 rejected=0 timers={work + extraTimers} workers={work} collections={work} heldBytes={active * 65536} peakHeldBytes={(started == 0 ? 0 : 65536)}");
        }
        Status("baseline", -1, 0, 0, 0, 0);
        foreach (string name in new[] { "missing_header", "wrong_header", "duplicate_header", "body", "query", "empty_query", "empty_separator_query" })
            Row("bad", -1, 400, "CLROS_CHURN_BAD_REQUEST", name);
        for (int cycle = 0; cycle < 3; cycle++)
        {
            Status("cancel-pending", cycle, 2 * cycle + 1, cycle, cycle, 1);
            Row("cancel-health", cycle, 200, health);
            Status("cancel-ready", cycle, 2 * cycle + 1, cycle, cycle, 1);
            Row("cancel", cycle, 0, "");
            work += canceledIterations;
            if (canceledTimerOnly)
                extraTimers++;
            Status("cancel-drained", cycle, 2 * cycle + 1, cycle, cycle + 1, 0);
            Status("complete-pending", cycle, 2 * cycle + 2, cycle, cycle + 1, 1);
            Row("complete-health", cycle, 200, health);
            Status("complete-ready", cycle, 2 * cycle + 2, cycle, cycle + 1, 1);
            Row("complete", cycle, 200, "CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8");
            Row("complete-health-after", cycle, 200, health);
            work += 8;
            Status("complete-drained", cycle, 2 * cycle + 2, cycle + 1, cycle + 1, 0);
        }
        Status("final", -1, 6, 3, 3, 0);
        return new()
        {
            ["schema"] = "clros.web.churn.v1",
            ["endpoint"] = Endpoint,
            ["samples"] = rows
        };
    }
    private static JsonArray Samples(JsonObject value) => value["samples"]!.AsArray();
    private static void Read(JsonObject value, string endpoint = Endpoint, string health = Health) =>
        NativeAotWebPayload.ValidateChurnEvidence(value.ToJsonString(), endpoint, health);

    [HostTest("web churn copied evidence checks all42 observations and permits bounded canceled partial progress")]
    private static void ValidValues()
    {
        foreach (int partial in new[] { 0, 3, 8 })
        {
            var value = Evidence(partial);
            string before = value.ToJsonString();
            Read(value);
            HostCheck.Equal(42, Samples(value).Count);
            HostCheck.Equal(before, value.ToJsonString());
        }
        Read(Evidence(health: "configured-\u00e9"), health: "configured-\u00e9");
        // A canceled timer can finish before its queued worker is canceled.
        Read(Evidence(canceledTimerOnly: true));
    }

    [HostTest("web churn copied evidence rejects missing duplicate unknown reordered and failed observations")]
    private static void ShapeAndSequence()
    {
        foreach (string name in new[] { "schema", "endpoint", "samples" })
        {
            var bad = Evidence();
            bad.Remove(name);
            Reject(bad);
        }
        foreach (string name in new[] { "kind", "cycle", "case", "status", "body" })
        {
            var bad = Evidence();
            Samples(bad)[8]!.AsObject().Remove(name);
            Reject(bad);
        }
        var unknown = Evidence();
        unknown["unknown"] = 1;
        Reject(unknown);
        unknown = Evidence();
        Samples(unknown)[8]!["unknown"] = 1;
        Reject(unknown);
        var missing = Evidence();
        Samples(missing).RemoveAt(8);
        Reject(missing);
        var extra = Evidence();
        Samples(extra).Add(Samples(extra)[41]!.DeepClone());
        Reject(extra);
        var reordered = Evidence();
        var rows = Samples(reordered);
        JsonNode saved = rows[8]!.DeepClone();
        rows[8] = rows[9]!.DeepClone();
        rows[9] = saved;
        Reject(reordered);
        foreach (var (at, field, replacement) in new[] { (8, "kind", "cancel-ready"), (1, "case", "missing-header"),
            (11, "body", "not canceled"), (16, "body", "CLROS_CHURN_FAIL worker_or_payload"), (9, "body", Health + "\n") })
        {
            var bad = Evidence();
            Samples(bad)[at]![field] = replacement;
            Reject(bad);
        }
        var badStatus = Evidence();
        Samples(badStatus)[11]!["status"] = 200;
        Reject(badStatus);
        string json = Evidence().ToJsonString();
        Reject(json.Insert(1, "\"schema\":\"clros.web.churn.v1\","));
        Reject(json.Replace("\"kind\":\"baseline\"", "\"kind\":\"baseline\",\"kind\":\"baseline\"", StringComparison.Ordinal));
    }

    [HostTest("web churn copied evidence rejects noncanonical counters bad bounds regressions and forged drains")]
    private static void StatusValues()
    {
        foreach (var (at, oldValue, replacement) in new[] {
            (8, "active=1", "active=3"), (8, "waiting=1", "waiting=2"), (8, "active=1", "active=01"),
            (8, "peakActive=1", "peakActive=3"), (8, "started=1", "started=2"), (8, "failed=0", "failed=1"),
            (8, "rejected=0", "rejected=1"), (8, "heldBytes=65536", "heldBytes=65537"),
            (8, "peakHeldBytes=65536", "peakHeldBytes=196608"), (8, "workers=0", "workers=1"),
            (8, "collections=0", "collections=1"), (8, "timers=0", "timers=9"),
            (12, "canceled=1", "canceled=0"), (41, "completed=3", "completed=2"),
            (8, "CLROS_CHURN_STATUS", "CLROS_CHURN_FAIL"), (8, "waiting=1", "waiting=1 unknown=0") })
        {
            var bad = Evidence();
            var row = Samples(bad)[at]!.AsObject();
            string body = row["body"]!.GetValue<string>();
            HostCheck.True(body.Contains(oldValue, StringComparison.Ordinal) && oldValue != replacement);
            row["body"] = body.Replace(oldValue, replacement, StringComparison.Ordinal);
            Reject(bad);
        }
        var regressed = Evidence();
        var pending = Samples(regressed)[8]!.AsObject();
        pending["body"] = pending["body"]!.GetValue<string>().Replace("timers=0", "timers=1", StringComparison.Ordinal);
        Reject(regressed);
        var wrongCycle = Evidence();
        Samples(wrongCycle)[8]!["cycle"] = 1;
        Reject(wrongCycle);
        var numericString = Evidence();
        Samples(numericString)[8]!["status"] = "200";
        Reject(numericString);
    }

    [HostTest("web churn copied evidence rejects stale endpoint configuration schema oversize and malformed JSON")]
    private static void BoundsAndConfiguration()
    {
        foreach (var (name, value) in new[] { ("schema", "clros.web.churn.v0"), ("endpoint", "http://127.0.0.1:23457") })
        {
            var bad = Evidence();
            bad[name] = value;
            Reject(bad);
        }
        foreach (string endpoint in new[] { "http://localhost:23456", Endpoint + "/", Endpoint + "?x=1", "http://127.0.0.1:023456" })
            Reject(Evidence().ToJsonString(), endpoint);
        Reject(Evidence().ToJsonString(), Endpoint, "stale body");
        var longBody = Evidence();
        Samples(longBody)[9]!["body"] = new string('x', 2049);
        Reject(longBody);
        var nullRow = Evidence();
        Samples(nullRow)[8] = null;
        Reject(nullRow);
        Reject("{");
        Reject("");
        Reject((string)null!);
        Reject(new string(' ', 65537));
        Reject(Evidence().ToJsonString().Replace("\"status\":200", "\"status\":2e2", StringComparison.Ordinal));
    }

    private static void Reject(JsonObject value) => Reject(value.ToJsonString());
    private static void Reject(string value, string endpoint = Endpoint, string health = Health)
    {
        bool refused = false;
        try
        {
            NativeAotWebPayload.ValidateChurnEvidence(value, endpoint, health);
        }
        catch (Exception failure) when (failure is InvalidDataException or JsonException)
        {
            refused = true;
        }
        HostCheck.True(refused);
    }
}
