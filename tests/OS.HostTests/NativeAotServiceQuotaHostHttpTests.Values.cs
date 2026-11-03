using System.Text;
using OS.Tooling;

internal static partial class NativeAotServiceQuotaHostHttpTests
{
    private sealed class Values
    {
        internal readonly List<NativeAotServiceQuotaPeer.WireConnection> Connections = [];
        internal readonly List<NativeAotServiceQuotaPeer.WireExchange> Exchanges = [];
        internal readonly List<NativeAotServiceQuotaPeer.WireOperation> Operations = [];
        internal readonly List<NativeAotServiceQuotaPeer.WireSample> Samples = [];
        private long tick = 1;
        internal int Connect(string role = "control")
        {
            int id = Connections.Count + 1;
            Connections.Add(new(id, role, "127.0.0.1", 40000 + id, "127.0.0.1", 8080, 1, 1000));
            return id;
        }

        internal void Operation(int connection, string direction, int exchange, string data, string outcome = "complete")
        {
            long offset = Operations.Where(o => o.ConnectionId == connection && o.Direction == direction && o.Transferred >= 0).Sum(o => (long)o.Transferred);
            Operations.Add(new(Operations.Count + 1, connection, direction, offset, outcome == "failed" && direction == "read" ? 8192 : data.Length, outcome == "failed" ? -1 : data.Length, ++tick, ++tick, outcome, B64(data), direction == "write" ? exchange : 0));
        }

        internal int Exchange(int connection, string path, string body, string? response = null, string role = "control", string method = "GET", string? request = null)
        {
            int id = Exchanges.Count + 1;
            long start = ++tick;
            Operation(connection, "write", id, request ?? Request(path, role, method));
            if (response is null)
                response = Response(body);
            if (response.Length > 0)
            {
                Operation(connection, "read", id, response[..^2]);
                Operation(connection, "read", id, response[^2..]);
            }

            Exchanges.Add(new(id, role, method, path, start, ++tick, role == "control" ? "complete" : "incomplete", role == "control" ? 200 : 0, role == "control" ? B64(body) : "", new[] { connection }));
            return id;
        }

        internal void Sample(string name, int exchange)
        {
            var e = Exchanges[exchange - 1];
            Samples.Add(new(name, e.EndedTicks, e.BodyBase64, exchange, 0));
        }

        internal NativeAotServiceQuotaPeer.WireTrace Trace() => new(8080, "quota-partial", 1001, Operations.Sum(o => (long)Convert.FromBase64String(o.DataBase64).Length), Connections, Exchanges, Operations, Samples);
    }
}
