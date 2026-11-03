using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static class NativeAotServiceQuotaWireReportTests
{
    private static NativeAotServiceQuotaPeer.WireTrace Fixture() => new(8080, "stream-control", 11, 6,
        new[] { new NativeAotServiceQuotaPeer.WireConnection(1, "control", "127.0.0.1", 40001, "127.0.0.1", 8080, 1, 10) },
        Array.Empty<NativeAotServiceQuotaPeer.WireExchange>(),
        new[] {
            new NativeAotServiceQuotaPeer.WireOperation(1, 1, "write", 0, 3, 3, 2, 3, "complete", "YWJj", 0),
            new NativeAotServiceQuotaPeer.WireOperation(2, 1, "read", 0, 3, 3, 4, 5, "complete", "eHl6", 0) },
        Array.Empty<NativeAotServiceQuotaPeer.WireSample>());

    [HostTest("quota wire report copies immutable raw values and permits explicit empty failed prefix")]
    private static void Copies()
    {
        var original = Fixture();
        var operations = original.Operations.ToArray();
        original = original with
        {
            Operations = operations
        };
        string json = NativeAotServiceQuotaPeer.SerializeWireReport(original);
        byte[] bytes = Encoding.UTF8.GetBytes(json);
        var copy = NativeAotServiceQuotaPeer.InspectWireReport(bytes);
        operations[0] = operations[0] with
        {
            DataBase64 = "eHh4"
        };
        bytes.AsSpan().Clear();
        HostCheck.Equal(json, NativeAotServiceQuotaPeer.SerializeWireReport(copy));
        bool immutable = false;
        try
        {
            ((IList<NativeAotServiceQuotaPeer.WireOperation>)copy.Operations)[0] = operations[0];
        }
        catch (NotSupportedException) { immutable = true; }
        HostCheck.True(immutable);
        var partial = original with
        {
            Purpose = "quota-partial",
            RawBytes = 0,
            Connections = Array.Empty<NativeAotServiceQuotaPeer.WireConnection>(),
            Operations = Array.Empty<NativeAotServiceQuotaPeer.WireOperation>()
        };
        HostCheck.Equal(0, Inspect(JsonNode.Parse(NativeAotServiceQuotaPeer.SerializeWireReport(partial))!.AsObject()).Operations.Count);
    }

    [HostTest("quota wire report rejects malformed duplicate missing unknown alias count tick offset and result values")]
    private static void Rejects()
    {
        string text = NativeAotServiceQuotaPeer.SerializeWireReport(Fixture());
        foreach (string bad in new[] { "", "null", "[]", "{}", text + "{}",
            text.Replace("\"port\":8080", "\"port\":8080,\"port\":8080", StringComparison.Ordinal),
            text.Replace("\"port\":8080", "\"port\":8080,\"\\u0070ort\":8080", StringComparison.Ordinal) })
            Reject(() => NativeAotServiceQuotaPeer.InspectWireReport(Encoding.UTF8.GetBytes(bad)));
        foreach (string key in Root().Select(p => p.Key).ToArray())
        {
            var root = Root();
            root.Remove(key);
            Reject(() => Inspect(root));
        }
        foreach (Action<JsonObject> change in new Action<JsonObject>[] {
            r => r["extra"] = 1, r => r["schema"] = "other", r => r["tickUnit"] = "milliseconds",
            r => r["port"] = "8080", r => r["rawBytes"] = 5, r => r["snapshotTicks"] = 9,
            r => Connection(r)["id"] = 2, r => Connection(r)["remotePort"] = 8081,
            r => Connection(r)["localAddress"] = "::1", r => Connection(r)["disposedTicks"] = 0,
            r => { var c = Connection(r).DeepClone().AsObject(); c["id"] = 2; r["connections"]!.AsArray().Add(c); },
            r => Operation(r)["id"] = 2, r => Operation(r)["connectionId"] = 2,
            r => Operation(r)["direction"] = "send", r => Operation(r)["offset"] = 1,
            r => Operation(r)["startedTicks"] = 0, r => Operation(r)["endedTicks"] = 1,
            r => Operation(r)["requested"] = 65537, r => Operation(r)["transferred"] = 2,
            r => Operation(r)["outcome"] = "pending", r => Operation(r)["exchangeId"] = 1,
            r => Operation(r)["dataBase64"] = "YWJj ", r => Operation(r)["dataBase64"] = "!",
            r => Operation(r).Remove("offset"), r => Operation(r)["extra"] = 1 })
        {
            var root = Root();
            change(root);
            Reject(() => Inspect(root));
        }
    }

    [HostTest("quota wire failed write preserves attempted bytes unknown count and rejects later writes")]
    private static void UnknownWrite()
    {
        var trace = Fixture();
        var operations = trace.Operations.ToArray();
        operations[0] = operations[0] with
        {
            Outcome = "failed",
            Transferred = -1
        };
        trace = trace with
        {
            Operations = operations
        };
        var copy = NativeAotServiceQuotaPeer.InspectWireReport(Encoding.UTF8.GetBytes(NativeAotServiceQuotaPeer.SerializeWireReport(trace)));
        HostCheck.True(copy.Operations[0].Transferred == -1 && copy.Operations[0].DataBase64 == "YWJj" && copy.RawBytes == 6);
        trace = trace with
        {
            RawBytes = 9,
            Operations = operations.Append(operations[0] with
            {
                Id = 3,
                Offset = 0,
                StartedTicks = 6,
                EndedTicks = 7,
                Outcome = "complete",
                Transferred = 3
            }).ToArray()
        };
        Reject(() => NativeAotServiceQuotaPeer.SerializeWireReport(trace));
    }

    [HostTest("quota partial wire uses900 second ceiling while stream controls retain exact600 second ceiling")]
    private static void TimeProfiles()
    {
        const long quotaMaximum = 900L * TimeSpan.TicksPerSecond, controlMaximum = 600L * TimeSpan.TicksPerSecond;
        foreach (long end in new[] { controlMaximum + 1, quotaMaximum })
        {
            string json = NativeAotServiceQuotaPeer.SerializeWireReport(AtEnd("quota-partial", end));
            var copy = NativeAotServiceQuotaPeer.InspectWireReport(Encoding.UTF8.GetBytes(json));
            HostCheck.True(copy.SnapshotTicks == end && copy.Connections[0].DisposedTicks == end - 1 && copy.Operations[1].EndedTicks == end - 6);
            HostCheck.Equal(json, NativeAotServiceQuotaPeer.SerializeWireReport(copy));
        }
        Reject(() => NativeAotServiceQuotaPeer.SerializeWireReport(AtEnd("quota-partial", quotaMaximum + 1)));
        var lateQuota = JsonNode.Parse(NativeAotServiceQuotaPeer.SerializeWireReport(AtEnd("quota-partial", quotaMaximum)))!.AsObject();
        lateQuota["snapshotTicks"] = quotaMaximum + 1;
        Reject(() => Inspect(lateQuota));
        string control = NativeAotServiceQuotaPeer.SerializeWireReport(AtEnd("stream-control", controlMaximum));
        HostCheck.Equal(controlMaximum, NativeAotServiceQuotaPeer.InspectWireReport(Encoding.UTF8.GetBytes(control)).SnapshotTicks);
        Reject(() => NativeAotServiceQuotaPeer.SerializeWireReport(AtEnd("stream-control", controlMaximum + 1)));
        var lateControl = JsonNode.Parse(control)!.AsObject();
        lateControl["snapshotTicks"] = controlMaximum + 1;
        Reject(() => Inspect(lateControl));
    }

    private static NativeAotServiceQuotaPeer.WireTrace AtEnd(string purpose, long end)
    {
        var original = Fixture();
        long shift = end - original.SnapshotTicks;
        return original with
        {
            Purpose = purpose,
            SnapshotTicks = end,
            Connections = original.Connections.Select(c => c with { ConnectedTicks = c.ConnectedTicks + shift, DisposedTicks = c.DisposedTicks + shift }).ToArray(),
            Operations = original.Operations.Select(o => o with { StartedTicks = o.StartedTicks + shift, EndedTicks = o.EndedTicks + shift }).ToArray()
        };
    }

    private static JsonObject Root() => JsonNode.Parse(NativeAotServiceQuotaPeer.SerializeWireReport(Fixture()))!.AsObject();
    private static JsonObject Operation(JsonObject r) => r["operations"]![0]!.AsObject();
    private static JsonObject Connection(JsonObject r) => r["connections"]![0]!.AsObject();
    private static NativeAotServiceQuotaPeer.WireTrace Inspect(JsonObject root) =>
        NativeAotServiceQuotaPeer.InspectWireReport(Encoding.UTF8.GetBytes(root.ToJsonString()));
    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (Exception error) when (error is ToolFailure or InvalidDataException) { rejected = true; }
        HostCheck.True(rejected);
    }
}
