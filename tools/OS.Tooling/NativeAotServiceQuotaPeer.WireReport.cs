using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class NativeAotServiceQuotaPeer
{
    private const string WireSchema = "clros.nativeaot.service.quota-wire.v1";

    /// <summary>Serialize copied bounded host stream observations only. Raw data
    /// is actual successful reads or attempted writes; failed write byte counts
    /// remain unknown. This companion is separate from the existing peer report
    /// and owned CLI. No HTTP framing/PCAP/cross-clock/guest verdict is produced.</summary>
    public static string SerializeWireReport(WireTrace trace) => SerializeWireReportCore(trace, false);
    internal static string SerializeSignalRaceWireReport(WireTrace trace) => SerializeWireReportCore(trace, true);
    private static string SerializeWireReportCore(WireTrace trace, bool signalRace)
    {
        var copy = CopyWire(trace);
        RequireWire(copy, signalRace);
        string json = JsonSerializer.Serialize(new
        {
            schema = signalRace ? "clros.web.signalrace.wire.v1" : WireSchema,
            tickUnit = "elapsed-100ns",
            port = copy.Port,
            purpose = copy.Purpose,
            snapshotTicks = copy.SnapshotTicks,
            rawBytes = copy.RawBytes,
            connections = copy.Connections.Select(c => new
            {
                id = c.Id,
                role = c.Role,
                localAddress = c.LocalAddress,
                localPort = c.LocalPort,
                remoteAddress = c.RemoteAddress,
                remotePort = c.RemotePort,
                connectedTicks = c.ConnectedTicks,
                disposedTicks = c.DisposedTicks
            }),
            exchanges = copy.Exchanges.Select(e => new
            {
                id = e.Id,
                role = e.Role,
                method = e.Method,
                path = e.Path,
                startedTicks = e.StartedTicks,
                endedTicks = e.EndedTicks,
                outcome = e.Outcome,
                status = e.Status,
                bodyBase64 = e.BodyBase64,
                connectionIds = e.ConnectionIds
            }),
            operations = copy.Operations.Select(o => new
            {
                id = o.Id,
                connectionId = o.ConnectionId,
                direction = o.Direction,
                offset = o.Offset,
                requested = o.Requested,
                transferred = o.Transferred,
                startedTicks = o.StartedTicks,
                endedTicks = o.EndedTicks,
                outcome = o.Outcome,
                dataBase64 = o.DataBase64,
                exchangeId = o.ExchangeId
            }),
            samples = copy.Samples.Select(s => new
            {
                name = s.Name,
                elapsedTicks = s.ElapsedTicks,
                bodyBase64 = s.BodyBase64,
                exchangeId = s.ExchangeId,
                connectionId = s.ConnectionId
            })
        });
        WireNeed(Encoding.UTF8.GetByteCount(json) <= WireMaximumReportBytes, "report_bound");
        return json;
    }

    /// <summary>Inspect one borrowed versioned wire companion, at most32MiB.
    /// Reject missing/duplicate/unknown fields, noncanonical data, aliasing IDs,
    /// impossible offsets/counts/times and inconsistent selected sample bindings.
    /// Returned lists are copied/read-only. This checks supplied values only;
    /// HTTP framing, original owned I/O, PCAP and kernel timing remain separate.</summary>
    public static WireTrace InspectWireReport(ReadOnlySpan<byte> utf8) => InspectWireReportCore(utf8, false);
    internal static WireTrace InspectSignalRaceWireReport(ReadOnlySpan<byte> utf8) => InspectWireReportCore(utf8, true);
    private static WireTrace InspectWireReportCore(ReadOnlySpan<byte> utf8, bool signalRace)
    {
        WireNeed(!utf8.IsEmpty && utf8.Length <= WireMaximumReportBytes, "report_bound");
        try
        {
            using var document = JsonDocument.Parse(utf8.ToArray(), new JsonDocumentOptions { MaxDepth = 7 });
            var root = document.RootElement;
            ReportFields(root, "schema", "tickUnit", "port", "purpose", "snapshotTicks", "rawBytes",
                "connections", "exchanges", "operations", "samples");
            WireNeed(ReportString(root, "schema") == (signalRace ? "clros.web.signalrace.wire.v1" : WireSchema) && ReportString(root, "tickUnit") == "elapsed-100ns", "schema");
            JsonElement[] Items(string key, int maximum)
            {
                var value = root.GetProperty(key);
                WireNeed(value.ValueKind == JsonValueKind.Array && value.GetArrayLength() <= maximum, "array_bound");
                return value.EnumerateArray().ToArray();
            }
            var connections = Items("connections", WireMaximumConnections).Select(c =>
            {
                ReportFields(c, "id", "role", "localAddress", "localPort", "remoteAddress", "remotePort", "connectedTicks", "disposedTicks");
                return new WireConnection(WireInt(c, "id", 1, WireMaximumConnections), ReportString(c, "role"),
                    ReportString(c, "localAddress"), WireInt(c, "localPort", 1, 65535), ReportString(c, "remoteAddress"),
                    WireInt(c, "remotePort", 1, 65535), ReportLong(c, "connectedTicks"), ReportLong(c, "disposedTicks"));
            }).ToArray();
            int connectionReferences = 0;
            var exchanges = Items("exchanges", WireMaximumExchanges).Select(e =>
            {
                ReportFields(e, "id", "role", "method", "path", "startedTicks", "endedTicks", "outcome", "status", "bodyBase64", "connectionIds");
                var ids = e.GetProperty("connectionIds");
                WireNeed(ids.ValueKind == JsonValueKind.Array && ids.GetArrayLength() <= WireMaximumConnections, "exchange_connections");
                connectionReferences += ids.GetArrayLength();
                WireNeed(connectionReferences <= WireMaximumOperations, "exchange_connection_bound");
                int[] connected = ids.EnumerateArray().Select(v =>
                {
                    WireNeed(v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out int id) && id > 0 && id <= WireMaximumConnections, "connection_id");
                    return v.GetInt32();
                }).ToArray();
                return new WireExchange(WireInt(e, "id", 1, WireMaximumExchanges), ReportString(e, "role"),
                    ReportString(e, "method"), ReportString(e, "path"), ReportLong(e, "startedTicks"), ReportLong(e, "endedTicks"),
                    ReportString(e, "outcome"), WireInt(e, "status", 0, signalRace ? 999 : 599), ReportString(e, "bodyBase64"), Array.AsReadOnly(connected));
            }).ToArray();
            var operations = Items("operations", WireMaximumOperations).Select(o =>
            {
                ReportFields(o, "id", "connectionId", "direction", "offset", "requested", "transferred", "startedTicks", "endedTicks", "outcome", "dataBase64", "exchangeId");
                return new WireOperation(WireInt(o, "id", 1, WireMaximumOperations), WireInt(o, "connectionId", 1, WireMaximumConnections),
                    ReportString(o, "direction"), ReportLong(o, "offset"), WireInt(o, "requested", 0, WireMaximumOperationBytes),
                    WireInt(o, "transferred", -1, WireMaximumOperationBytes), ReportLong(o, "startedTicks"), ReportLong(o, "endedTicks"),
                    ReportString(o, "outcome"), ReportString(o, "dataBase64"), WireInt(o, "exchangeId", 0, WireMaximumExchanges));
            }).ToArray();
            var samples = Items("samples", ReportSampleNames.Length).Select(s =>
            {
                ReportFields(s, "name", "elapsedTicks", "bodyBase64", "exchangeId", "connectionId");
                return new WireSample(ReportString(s, "name"), ReportLong(s, "elapsedTicks"), ReportString(s, "bodyBase64"),
                    WireInt(s, "exchangeId", 0, WireMaximumExchanges), WireInt(s, "connectionId", 0, WireMaximumConnections));
            }).ToArray();
            var result = new WireTrace(WireInt(root, "port", 1, 65535), ReportString(root, "purpose"),
                ReportLong(root, "snapshotTicks"), ReportLong(root, "rawBytes"), Array.AsReadOnly(connections),
                Array.AsReadOnly(exchanges), Array.AsReadOnly(operations), Array.AsReadOnly(samples));
            RequireWire(result, signalRace);
            return result;
        }
        catch (JsonException error) { throw new InvalidDataException("quota_wire_json", error); }
    }

    private static int WireInt(JsonElement value, string name, int minimum, int maximum)
    {
        var field = value.GetProperty(name);
        WireNeed(field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out int number) && number >= minimum && number <= maximum, "integer");
        return field.GetInt32();
    }

    private static WireTrace CopyWire(WireTrace trace)
    {
        WireNeed(trace is not null && trace.Connections is not null && trace.Exchanges is not null &&
            trace.Operations is not null && trace.Samples is not null, "null");
        WireNeed(trace.Connections.Count <= WireMaximumConnections && trace.Exchanges.Count <= WireMaximumExchanges &&
            trace.Operations.Count <= WireMaximumOperations && trace.Samples.Count <= ReportSampleNames.Length, "array_bound");
        var connections = trace.Connections.ToArray();
        var operations = trace.Operations.ToArray();
        var samples = trace.Samples.ToArray();
        int connectionReferences = 0;
        var exchanges = trace.Exchanges.Select(e =>
        {
            WireNeed(e is not null && e.ConnectionIds is not null && e.ConnectionIds.Count <= WireMaximumConnections, "null");
            connectionReferences += e.ConnectionIds.Count;
            WireNeed(connectionReferences <= WireMaximumOperations, "exchange_connection_bound");
            return e with
            {
                ConnectionIds = Array.AsReadOnly(e.ConnectionIds.ToArray())
            };
        }).ToArray();
        return trace with
        {
            Connections = Array.AsReadOnly(connections),
            Exchanges = Array.AsReadOnly(exchanges),
            Operations = Array.AsReadOnly(operations),
            Samples = Array.AsReadOnly(samples)
        };
    }

    private static byte[] WireData(string text, int maximum)
    {
        WireNeed(text is not null && text.Length <= ((maximum + 2) / 3) * 4, "data_bound");
        byte[] result;
        try
        {
            result = Convert.FromBase64String(text);
        }
        catch (FormatException error) { throw new InvalidDataException("quota_wire_base64", error); }
        WireNeed(result.Length <= maximum && Convert.ToBase64String(result) == text, "data_canonical");
        return result;
    }

    private static void RequireWire(WireTrace trace, bool signalRace = false)
    {
        long maximumSeconds = !signalRace && trace.Purpose is ("quota-run" or "quota-partial") ? MaximumPeerSeconds : 600;
        long maximumTicks = maximumSeconds * TimeSpan.TicksPerSecond;
        WireNeed(trace.Port is >= 1 and <= 65535 && (signalRace ? trace.Purpose is "signalrace-run" or "signalrace-partial" : trace.Purpose is "quota-run" or "quota-partial" or "stream-control") &&
            trace.SnapshotTicks > 0 && trace.SnapshotTicks <= maximumTicks && trace.RawBytes is >= 0 and <= WireMaximumRawBytes, "trace");
        WireNeed(trace.Connections.Count >= (trace.Purpose is "quota-partial" or "signalrace-partial" ? 0 : 1) && trace.Connections.Count <= WireMaximumConnections && trace.Exchanges.Count <= (signalRace ? 1024 : WireMaximumExchanges) &&
            trace.Operations.Count <= WireMaximumOperations && trace.Samples.Count <= ReportSampleNames.Length, "array_bound");
        long prior = 0;
        var live = new List<WireConnection>();
        for (int i = 0; i < trace.Connections.Count; i++)
        {
            var c = trace.Connections[i];
            WireNeed(c is not null && c.Id == i + 1 && WireRole(c.Role, signalRace) && c.LocalAddress == "127.0.0.1" && c.RemoteAddress == "127.0.0.1" &&
                c.LocalPort is >= 1 and <= 65535 && c.RemotePort == trace.Port && c.ConnectedTicks >= prior &&
                c.ConnectedTicks >= 0 && c.DisposedTicks >= c.ConnectedTicks && c.DisposedTicks > 0 &&
                c.DisposedTicks <= trace.SnapshotTicks, "connection");
            live.RemoveAll(p => p.DisposedTicks <= c.ConnectedTicks);
            WireNeed(live.Count < 4 && !live.Any(p => p.LocalPort == c.LocalPort && p.RemotePort == c.RemotePort), "connection_alias");
            live.Add(c);
            prior = c.ConnectedTicks;
        }
        var offsets = new Dictionary<(int, string), long>();
        var ends = new Dictionary<(int, string), long>();
        var unknown = new HashSet<(int, string)>();
        var byExchange = new Dictionary<int, List<WireOperation>>();
        var payloads = new byte[trace.Operations.Count][];
        long raw = 0;
        prior = 0;
        for (int i = 0; i < trace.Operations.Count; i++)
        {
            var o = trace.Operations[i];
            WireNeed(o is not null && o.Id == i + 1 && o.ConnectionId > 0 && o.ConnectionId <= trace.Connections.Count &&
                o.Direction is "read" or "write" && o.Requested is >= 0 and <= WireMaximumOperationBytes &&
                o.StartedTicks >= prior && o.EndedTicks >= o.StartedTicks && o.EndedTicks <= trace.SnapshotTicks, "operation");
            var c = trace.Connections[o.ConnectionId - 1];
            var key = (o.ConnectionId, o.Direction);
            WireNeed(o.StartedTicks >= c.ConnectedTicks && o.StartedTicks <= c.DisposedTicks &&
                o.Offset == offsets.GetValueOrDefault(key) && !unknown.Contains(key) &&
                o.StartedTicks >= ends.GetValueOrDefault(key), "offset_or_overlap");
            byte[] bytes = WireData(o.DataBase64, WireMaximumOperationBytes);
            payloads[i] = bytes;
            raw += bytes.Length;
            WireNeed(raw <= WireMaximumRawBytes, "raw_bound");
            bool write = o.Direction == "write", failed = o.Outcome is "failed" or "canceled";
            WireNeed(failed ? o.Transferred == -1 && bytes.Length == (write ? o.Requested : 0) :
                o.Outcome == "complete" ? o.Transferred >= (write ? 0 : 1) && o.Transferred <= o.Requested &&
                    bytes.Length == o.Transferred && (!write || o.Transferred == o.Requested) :
                o.Outcome == "zero" ? !write && o.Requested == 0 && o.Transferred == 0 && bytes.Length == 0 :
                o.Outcome == "eof" && !write && o.Requested > 0 && o.Transferred == 0 && bytes.Length == 0, "operation_result");
            WireNeed(write ? o.ExchangeId >= 0 && o.ExchangeId <= trace.Exchanges.Count : o.ExchangeId == 0, "operation_exchange");
            if (o.ExchangeId > 0)
            {
                if (!byExchange.TryGetValue(o.ExchangeId, out var group))
                    byExchange.Add(o.ExchangeId, group = []);
                group.Add(o);
            }
            if (failed)
                unknown.Add(key);
            else
                offsets[key] = o.Offset + o.Transferred;
            ends[key] = o.EndedTicks;
            prior = o.StartedTicks;
        }
        WireNeed(raw == trace.RawBytes, "raw_count");
        prior = 0;
        var roleEnds = new Dictionary<string, long>(StringComparer.Ordinal);
        for (int i = 0; i < trace.Exchanges.Count; i++)
        {
            var e = trace.Exchanges[i];
            WireNeed(e is not null && e.Id == i + 1 && WireRequest(e.Role, e.Method, e.Path, signalRace) &&
                e.StartedTicks >= prior && e.EndedTicks >= e.StartedTicks && e.EndedTicks <= trace.SnapshotTicks &&
                e.ConnectionIds is not null && e.ConnectionIds.Count <= trace.Connections.Count &&
                e.ConnectionIds.Distinct().Count() == e.ConnectionIds.Count, "exchange");
            byte[] body = WireData(e.BodyBase64, 8192);
            WireNeed(signalRace ? (e.Outcome == "response" ? e.Status is >= 100 and <= 999 : (e.Outcome is "faulted" or "cancelled") && e.Status is 0 or >= 100 and <= 999) && body.Length <= 4096 :
                e.Outcome == "complete" ? e.Role == "control" && e.Status == 200 && body.Length > 0 &&
                body.All(b => b is >= 32 and <= 126) :
                e.Outcome == "response" ? e.Role == "pressure" && e.Status is >= 100 and <= 599 && body.Length == 0 :
                (e.Outcome is "failed" or "canceled" or "io-error" or "incomplete") && e.Status == 0 && body.Length == 0, "exchange_result");
            var writtenOps = byExchange.GetValueOrDefault(e.Id) ?? [];
            int[] written = writtenOps.Select(o => o.ConnectionId).Distinct().ToArray();
            WireNeed(written.SequenceEqual(e.ConnectionIds) && e.ConnectionIds.All(id => id > 0 && id <= trace.Connections.Count &&
                trace.Connections[id - 1].Role == e.Role), "exchange_connection_join");
            WireNeed(writtenOps.All(o => o.Direction == "write" &&
                o.StartedTicks >= e.StartedTicks && o.EndedTicks <= e.EndedTicks), "exchange_io_time");
            if (e.Outcome is "complete" or "io-error" or "response" || e.Outcome == "incomplete" && trace.Purpose == "quota-run")
                WireNeed(written.Length > 0, "exchange_missing_io");
            WireNeed(roleEnds.GetValueOrDefault(e.Role) <= e.StartedTicks, "exchange_overlap");
            roleEnds[e.Role] = e.EndedTicks;
            prior = e.StartedTicks;
        }
        if (signalRace)
        {
            WireNeed(trace.Samples.Count == 0 && trace.Operations.Where(o => o.Direction == "write").All(o => o.ExchangeId > 0), "signalrace_shape");
            if (trace.Purpose == "signalrace-run")
                WireNeed(trace.Exchanges.Count > 0 && trace.Exchanges.Count(e => e.Outcome == "cancelled" && e.Role == "churn") == 1 &&
                trace.Exchanges.All(e => e.Outcome == "response" || e.Outcome == "cancelled" && e.Role == "churn"), "signalrace_run");
        }
        else if (trace.Purpose == "stream-control")
            WireNeed(trace.Exchanges.Count == 0 && trace.Samples.Count == 0 &&
            trace.Operations.All(o => o.ExchangeId == 0), "control_shape");
        else
        {
            if (trace.Purpose == "quota-run")
                WireNeed(trace.Samples.Count == ReportSampleNames.Length && trace.Exchanges.Count(e => e.Role == "pressure") == 1 &&
                trace.Exchanges.Single(e => e.Role == "pressure").Outcome == "io-error" &&
                trace.Exchanges.Count(e => e.Role == "cancel") == 1 && trace.Exchanges.Single(e => e.Role == "cancel").Outcome == "incomplete" &&
                trace.Exchanges.Count(e => e.Role == "echo") == 1 && trace.Exchanges.Single(e => e.Role == "echo").Outcome == "incomplete" &&
                trace.Operations.Where(o => o.Direction == "write").All(o => o.ExchangeId > 0), "quota_shape");
            var used = new HashSet<int>();
            prior = 0;
            for (int i = 0; i < trace.Samples.Count; i++)
            {
                var s = trace.Samples[i];
                WireNeed(s is not null && s.Name == ReportSampleNames[i] && s.ElapsedTicks >= prior && s.ElapsedTicks > 0 &&
                    s.ElapsedTicks <= trace.SnapshotTicks, "sample");
                byte[] body = WireData(s.BodyBase64, 8192);
                WireNeed(body.Length > 0, "sample_body");
                prior = s.ElapsedTicks;
                if (i == 4)
                {
                    WireNeed(s.ExchangeId == 0 && s.ConnectionId > 0 && s.ConnectionId <= trace.Connections.Count &&
                        trace.Connections[s.ConnectionId - 1].Role == "cancel", "sample_connection");
                    byte[] read = trace.Operations.Where(o => o.ConnectionId == s.ConnectionId && o.Direction == "read" &&
                        o.Outcome == "complete" && o.EndedTicks <= s.ElapsedTicks).SelectMany(o => payloads[o.Id - 1]).ToArray();
                    WireNeed(read.AsSpan().SequenceEqual(body), "sample_headers");
                }
                else
                {
                    WireNeed(s.ConnectionId == 0 && s.ExchangeId > 0 && s.ExchangeId <= trace.Exchanges.Count, "sample_exchange");
                    var e = trace.Exchanges[s.ExchangeId - 1];
                    WireNeed(e.Role == "control" && e.Outcome == "complete" && e.EndedTicks == s.ElapsedTicks &&
                        e.BodyBase64 == s.BodyBase64 && (used.Add(e.Id) || i == ReportSampleNames.Length - 1), "sample_join");
                }
            }
        }
    }
}
