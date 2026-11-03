using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;
public static partial class NativeAotDiskStaticBrowser
{
    private sealed partial class Capture : IDisposable
    {
        private readonly object gate = new();
        private readonly Dictionary<string, Row> rows = new(StringComparer.Ordinal);
        private readonly List<string> errors = [];
        private long last = Stopwatch.GetTimestamp();
        private int events;
        public bool Quiet
        {
            get
            {
                lock (gate)
                    return rows.Count > 0 && rows.Values.All(r => r.Finished.ValueKind != JsonValueKind.Undefined) && Stopwatch.GetElapsedTime(last) >= TimeSpan.FromSeconds(2);
            }
        }

        public void Event(string method, JsonElement value)
        {
            lock (gate)
            {
                Need(++events <= 8192, "event_capacity");
                if (method is "Runtime.exceptionThrown" or "Network.loadingFailed" or "Network.requestServedFromCache" || method == "Runtime.consoleAPICalled" && Text(value, "type") == "error")
                    errors.Add(method + " " + value.GetRawText());
                if (method == "Log.entryAdded")
                {
                    var entry = value.GetProperty("entry");
                    if (Text(entry, "level") == "error" && !(Text(entry, "source") == "network" && entry.TryGetProperty("url", out var url) && url.GetString()!.EndsWith("/favicon.ico", StringComparison.Ordinal)))
                        errors.Add(method + " " + value.GetRawText());
                }

                if (method is not ("Network.requestWillBeSent" or "Network.requestWillBeSentExtraInfo" or "Network.responseReceived" or "Network.responseReceivedExtraInfo" or "Network.loadingFinished"))
                    return;
                last = Stopwatch.GetTimestamp();
                string id = Text(value, "requestId");
                if (!rows.TryGetValue(id, out var row))
                {
                    Need(rows.Count < MaximumRequests, "request_capacity");
                    rows.Add(id, row = new(id));
                }

                void Set(ref JsonElement slot)
                {
                    Need(slot.ValueKind == JsonValueKind.Undefined, "duplicate_network_event");
                    slot = value.Clone();
                }

                switch (method)
                {
                    case "Network.requestWillBeSent":
                        Set(ref row.Request);
                        break;
                    case "Network.requestWillBeSentExtraInfo":
                        Set(ref row.RequestExtra);
                        break;
                    case "Network.responseReceived":
                        Set(ref row.Response);
                        break;
                    case "Network.responseReceivedExtraInfo":
                        Set(ref row.ResponseExtra);
                        break;
                    default:
                        Set(ref row.Finished);
                        break;
                }
            }
        }

        public Row[] Snapshot()
        {
            lock (gate)
            {
                RequireComplete(rows.Count);
                return rows.Values.ToArray();
            }
        }

        public void RequireComplete(int count)
        {
            lock (gate)
                Need(rows.Count == count && errors.Count == 0 && rows.Values.All(r => new[] { r.Request, r.RequestExtra, r.Response, r.ResponseExtra, r.Finished }.All(v => v.ValueKind == JsonValueKind.Object)), "network_incomplete_or_errors");
        }

        public string[] Errors()
        {
            lock (gate)
                return errors.ToArray();
        }

        public void Dispose()
        {
        }
    }
}
