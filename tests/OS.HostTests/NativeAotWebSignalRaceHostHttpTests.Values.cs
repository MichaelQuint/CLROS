using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;
using Peer = OS.Tooling.NativeAotServiceQuotaPeer;

// Independent copied byte/metadata vectors only. No actual HTTP execution,
// packet identity, native signal, owned completion or guest evidence is supplied.
internal static partial class NativeAotWebSignalRaceHostHttpTests
{
    internal sealed class Values(int port = 23460)
    {
        internal readonly List<Peer.WireConnection> Connections = [];
        internal readonly List<Peer.WireOperation> Operations = [];
        internal readonly List<Peer.WireExchange> Exchanges = [];
        internal readonly List<JsonObject> Samples = [], Links = [];
        internal readonly Dictionary<int, int> Completions = [];
        private long tick = 1;
        internal int Connect(string role = "control")
        {
            int id = Connections.Count + 1;
            Connections.Add(new(id, role, "127.0.0.1", 41000 + id, "127.0.0.1", port, 0, 10000));
            return id;
        }

        internal void Op(int connection, string direction, int exchange, string data, string outcome = "complete")
        {
            long offset = Operations.Where(op => op.ConnectionId == connection && op.Direction == direction && op.Transferred >= 0).Sum(op => (long)op.Transferred);
            Operations.Add(new(Operations.Count + 1, connection, direction, offset, direction == "read" && outcome != "complete" ? 1024 : data.Length, outcome == "complete" ? data.Length : -1, ++tick, ++tick, outcome, B64(data), direction == "write" ? exchange : 0));
        }

        internal int Add(int connection, string path = "/__clros/churn/status", string body = "ONE", string? rawResponse = null, string method = "GET", string outcome = "response", int status = 200, string? rawRequest = null)
        {
            int id = Exchanges.Count + 1;
            long start = ++tick;
            Op(connection, "write", id, rawRequest ?? Request(path, method, port));
            string response = rawResponse ?? Response(body, status);
            if (response.Length > 0)
            {
                Op(connection, "read", id, response[..^1]);
                Op(connection, "read", id, response[^1..]);
            }

            Exchanges.Add(new(id, Connections[connection - 1].Role, method, path, start, ++tick, outcome, status, B64(body), new[] { connection }));
            return id;
        }

        internal void Select(string kind, int exchange)
        {
            var value = Exchanges[exchange - 1];
            Samples.Add(new() { ["kind"] = kind, ["status"] = value.Status, ["body"] = Encoding.ASCII.GetString(Convert.FromBase64String(value.BodyBase64)) });
            Links.Add(new() { ["sample"] = Links.Count, ["attempt"] = exchange, ["related"] = 0 });
        }

        internal void Cancel(int exchange)
        {
            Samples.Add(new() { ["kind"] = "churn-cancel", ["status"] = 0, ["body"] = "" });
            Links.Add(new() { ["sample"] = Links.Count, ["attempt"] = 0, ["related"] = exchange });
        }

        internal byte[] Wire() => Encoding.UTF8.GetBytes(NativeAotWebPayload.SerializeSignalRaceWire(new(port, "signalrace-partial", 10001, Operations.Sum(op => (long)Convert.FromBase64String(op.DataBase64).Length), Connections, Exchanges, Operations, [])));
        internal string Selected() => new JsonObject
        {
            ["schema"] = "clros.web.signalrace.v1",
            ["endpoint"] = "http://127.0.0.1:" + port,
            ["samples"] = new JsonArray(Samples.Select(value => (JsonNode? )value.DeepClone()).ToArray())
        }.ToJsonString();
        internal string Attempts()
        {
            var rows = Exchanges.Select(e => (JsonNode? )new JsonObject { ["id"] = e.Id, ["method"] = e.Method, ["path"] = e.Path, ["completion"] = Completions.GetValueOrDefault(e.Id, e.Id), ["headers"] = e.Status > 0, ["status"] = e.Status, ["mediaType"] = e.Status > 0 ? "text/plain" : "", ["mediaTypeComplete"] = true, ["bodyBase64"] = e.BodyBase64, ["bodyComplete"] = e.Outcome == "response", ["outcome"] = e.Outcome, ["error"] = e.Outcome == "response" ? "" : e.Outcome == "cancelled" ? "cancelled" : "IOException" }).ToArray();
            return new JsonObject
            {
                ["schema"] = "clros.web.signalrace.attempts.v1",
                ["endpoint"] = "http://127.0.0.1:" + port,
                ["semanticComplete"] = false,
                ["failure"] = "copied_failure",
                ["attempts"] = new JsonArray(rows),
                ["selections"] = new JsonArray(Links.Select(value => (JsonNode? )value.DeepClone()).ToArray())
            }.ToJsonString();
        }
    }
}
