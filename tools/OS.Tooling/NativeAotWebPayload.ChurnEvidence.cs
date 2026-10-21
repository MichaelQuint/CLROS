using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotWebPayload
{
    /// <summary>Validate bounded copied churn JSON against explicit endpoint and
    /// health values. This checks shape, counters and order only; supplied text
    /// grants no original HTTP, file, process, native or guest authority.
    /// Malformed, incomplete or stale values throw; this starts no I/O.</summary>
    public static void ValidateChurnEvidence(string json, string expectedEndpoint, string expectedHealthBody)
    {
        ChurnNeed(json is { Length: > 0 and <= 65536 } && Encoding.UTF8.GetByteCount(json) <= 65536, "input");
        ChurnNeed(expectedEndpoint is { Length: > 0 and <= 64 } && Uri.TryCreate(expectedEndpoint, UriKind.Absolute, out var endpoint) &&
            endpoint.Scheme == "http" && endpoint.Host == "127.0.0.1" && endpoint.Port is >= 1 and <= 65535 &&
            expectedEndpoint == "http://127.0.0.1:" + endpoint.Port.ToString(CultureInfo.InvariantCulture), "endpoint");
        ChurnNeed(expectedHealthBody is { Length: > 0 and <= 1024 } && !expectedHealthBody.Any(char.IsControl), "health");
        using JsonDocument document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
        JsonElement root = document.RootElement;
        ChurnKeys(root, "schema", "endpoint", "samples");
        ChurnNeed(ChurnString(root.GetProperty("schema"), 64) == "clros.web.churn.v1" &&
            ChurnString(root.GetProperty("endpoint"), 64) == expectedEndpoint, "schema_endpoint");
        JsonElement samples = root.GetProperty("samples");
        ChurnNeed(samples.ValueKind == JsonValueKind.Array && samples.GetArrayLength() == 42, "samples");
        int position = 0; ulong[]? previous = null;
        JsonElement Row(string kind, int cycle, string caseName, int status)
        {
            ChurnNeed(position < 42, "sequence"); JsonElement row = samples[position++];
            ChurnKeys(row, "kind", "cycle", "case", "status", "body");
            ChurnNeed(ChurnString(row.GetProperty("kind"), 32) == kind && ChurnInt(row.GetProperty("cycle")) == cycle &&
                ChurnString(row.GetProperty("case"), 32) == caseName && ChurnInt(row.GetProperty("status")) == status, "sequence");
            _ = ChurnString(row.GetProperty("body"), 2048); return row;
        }
        void Body(string kind, int cycle, int status, string body, string caseName = "") =>
            ChurnNeed(ChurnString(Row(kind, cycle, caseName, status).GetProperty("body"), 2048) == body, "body");
        void Status(string kind, int cycle, ulong started, ulong completed, ulong canceled, ulong active, ulong waiting)
        {
            ulong[] value = ChurnStatus(ChurnString(Row(kind, cycle, "", 200).GetProperty("body"), 2048));
            ChurnNeed(value[0] == active && value[1] == waiting && value[3] == started && value[4] == completed && value[5] == canceled,
                "lifetime");
            if (previous is not null)
                foreach (int field in new[] { 2, 3, 4, 5, 6, 7, 8, 9, 10, 12 }) ChurnNeed(value[field] >= previous[field], "monotonic");
            if (kind == "baseline") ChurnNeed(value.All(n => n == 0), "baseline");
            if (kind == "final") ChurnNeed(previous is not null && value.SequenceEqual(previous), "final_progress");
            previous = value;
        }
        Status("baseline", -1, 0, 0, 0, 0, 0);
        foreach (string bad in new[] { "missing_header", "wrong_header", "duplicate_header", "body", "query", "empty_query", "empty_separator_query" })
            Body("bad", -1, 400, "CLROS_CHURN_BAD_REQUEST", bad);
        for (int cycle = 0; cycle < 3; cycle++)
        {
            ulong count = (ulong)cycle, started = count * 2 + 1;
            ulong[] beforeCancel = previous!;
            Status("cancel-pending", cycle, started, count, count, 1, 1);
            Body("cancel-health", cycle, 200, expectedHealthBody);
            Status("cancel-ready", cycle, started, count, count, 1, 1);
            Body("cancel", cycle, 0, "");
            Status("cancel-drained", cycle, started, count, count + 1, 0, 0);
            foreach (int field in new[] { 8, 9, 10 }) ChurnNeed(previous![field] - beforeCancel[field] <= 8, "cancel_progress");
            ulong[] beforeComplete = previous!;
            Status("complete-pending", cycle, started + 1, count, count + 1, 1, 1);
            Body("complete-health", cycle, 200, expectedHealthBody);
            Status("complete-ready", cycle, started + 1, count, count + 1, 1, 1);
            Body("complete", cycle, 200, "CLROS_CHURN_OK iterations=8 bytes=65536 timers=8 workers=8 collections=8");
            Body("complete-health-after", cycle, 200, expectedHealthBody);
            Status("complete-drained", cycle, started + 1, count + 1, count + 1, 0, 0);
            foreach (int field in new[] { 8, 9, 10 }) ChurnNeed(previous![field] - beforeComplete[field] == 8, "complete_progress");
        }
        Status("final", -1, 6, 3, 3, 0, 0);
        ChurnNeed(position == 42 && previous is not null && previous[8] is >= 24 and <= 48 &&
            previous[9] is >= 24 and <= 48 && previous[10] is >= 24 and <= 48, "final");
    }

    private static readonly string[] ChurnStatusKeys = ["active", "waiting", "peakActive", "started", "completed", "canceled",
        "failed", "rejected", "timers", "workers", "collections", "heldBytes", "peakHeldBytes"];
    private static ulong[] ChurnStatus(string body,bool continuous=false)
    {
        const string prefix = "CLROS_CHURN_STATUS "; ChurnNeed(body.StartsWith(prefix, StringComparison.Ordinal), "status_prefix");
        string[] fields = body[prefix.Length..].Split(' '); ChurnNeed(fields.Length == ChurnStatusKeys.Length, "status_fields");
        ulong[] value = new ulong[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            string key = ChurnStatusKeys[i] + "="; ChurnNeed(fields[i].StartsWith(key, StringComparison.Ordinal), "status_key");
            string number = fields[i][key.Length..];
            ChurnNeed(number.Length is > 0 and <= 20 && (number.Length == 1 || number[0] != '0') &&
                number.All(c => c is >= '0' and <= '9') && ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out value[i]), "status_number");
        }
        ChurnNeed(value[0] <= 2 && value[1] <= value[0] && value[2] >= value[0] && value[2] <= 2 &&
            value[3] <= 6 && value[4] <= 3 && value[5] <= 3 && value[6] == 0 && value[7] == 0 &&
            value[3] == value[0] + value[4] + value[5] && value[8] <= (continuous ? (ulong)long.MaxValue : value[3] * 8) && value[9] <= value[8] &&
            value[10] == value[9] && value[9] >= value[4] * 8 && value[8] - value[9] <= value[0] + value[5] &&
            value[11] % 65536 == 0 && value[11] <= value[0] * 65536 && value[12] % 65536 == 0 &&
            value[12] >= value[11] && value[12] <= 2 * 65536 && value[12] <= value[2] * 65536, "status_invariant");
        return value;
    }
    private static void ChurnKeys(JsonElement value, params string[] expected)
    {
        ChurnNeed(value.ValueKind == JsonValueKind.Object, "object");
        string[] names = value.EnumerateObject().Select(p => p.Name).ToArray();
        ChurnNeed(names.Length == expected.Length && names.Distinct(StringComparer.Ordinal).Count() == names.Length &&
            names.All(name => expected.Contains(name, StringComparer.Ordinal)), "properties");
    }
    private static string ChurnString(JsonElement value, int maximum)
    {
        ChurnNeed(value.ValueKind == JsonValueKind.String, "string"); string text = value.GetString()!;
        ChurnNeed(text.Length <= maximum, "string_bound"); return text;
    }
    private static int ChurnInt(JsonElement value)
    {
        ChurnNeed(value.ValueKind == JsonValueKind.Number, "integer");
        ChurnNeed(value.TryGetInt32(out int number) && value.GetRawText() == number.ToString(CultureInfo.InvariantCulture), "integer"); return number;
    }
    private static void ChurnNeed(bool condition, string reason)
    { if (!condition) throw new InvalidDataException("web_churn_" + reason); }
}
