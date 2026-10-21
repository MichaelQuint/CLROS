using System.Globalization;
using System.Text;
using OS.Tooling;
using Q = OS.Tooling.QemuTranscripts;
using Values = NativeAotWebSignalRaceHostHttpTests.Values;

internal static class NativeAotWebSignalRaceDateTests
{
    internal const uint Port = 43000;
    internal const string Request = "GET /__clros/churn/status HTTP/1.1\r\nHost: 127.0.0.1:22080\r\n\r\n";
    private static readonly DateTimeOffset Epoch = new(2000, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Q.WebUtcObservation Window = new(Epoch.AddSeconds(10), Epoch.AddSeconds(40));
    internal static string Response(string body = "ONE", string? date = null) =>
        "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: " + body.Length + "\r\n" +
        (date is null ? "" : "Date: " + date + "\r\n") + "\r\n" + body;
    private static string Date(int second) => Epoch.AddSeconds(second).ToString("r", CultureInfo.InvariantCulture);
    private static string Data(string bytes) => Convert.ToBase64String(Encoding.ASCII.GetBytes(bytes));

    // These strict copied wire/HTTP/byte joins are not original I/O, PCAP,
    // UTC, native-call, process ownership or guest execution evidence.
    internal static Q.WebSignalRaceStreamJoinInspection Build(string[] responses, string[]? bodies = null,
        bool separate = false, string outcome = "response", int status = 200)
    {
        Values values = new(22080); List<Q.ServiceQuotaPacketStream> streams = [];
        int first = values.Connect();
        var requests = new Dictionary<int, List<Q.ServiceQuotaPacketRange>>();
        var received = new Dictionary<int, List<Q.ServiceQuotaPacketRange>>();
        Dictionary<int, string> requestBytes = [], responseBytes = [];
        for (int i = 0; i < responses.Length; i++)
        {
            int connection = separate && i > 0 ? values.Connect() : first;
            values.Add(connection, body: bodies?[i] ?? "ONE", rawResponse: responses[i], outcome: outcome, status: status, rawRequest: Request);
            if (!requests.ContainsKey(connection)) { requests.Add(connection, []); received.Add(connection, []); }
            uint port = Port + (uint)connection - 1;
            void Range(bool response, string bytes)
            {
                var buffer = response ? responseBytes : requestBytes; var ranges = response ? received : requests;
                int offset = buffer.GetValueOrDefault(connection, "").Length;
                if (bytes.Length > 0) ranges[connection].Add(new(new(port, response, offset, bytes.Length, 10 + i * 4 + (response ? 1 : 0), 1, (uint)(10 + i * 4 + (response ? 1 : 0))), Data(bytes)));
                buffer[connection] = buffer.GetValueOrDefault(connection, "") + bytes;
            }
            Range(false, Request); Range(true, responses[i]);
        }
        foreach (int id in requests.Keys)
        {
            Q.ServiceQuotaPacketDirection Direction(bool response)
            {
                string bytes = (response ? responseBytes : requestBytes).GetValueOrDefault(id, "");
                return new(bytes.Length, bytes.Length, bytes.Length, Data(bytes), (response ? received : requests)[id].ToArray());
            }
            streams.Add(new(Port + (uint)id - 1, Direction(false), Direction(true), new(1, true, true, false, false, false, false, false, 0, 0, 0)));
        }
        var guest = new Q.ServiceQuotaPacketStreams(100, 0, streams.Sum(s => s.Requests.Extent + s.Responses.Extent), streams);
        return Q.InspectNativeAotWebSignalRaceStreamJoinForTests(values.Wire(), values.Attempts(), values.Selected(),
            "http://127.0.0.1:22080", "OS_MINIMAL_API_OK", guest);
    }
    private static Q.WebSignalRaceDateInspection Read(Q.WebSignalRaceStreamJoinInspection joined,
        Q.WebUtcObservation? window = null, DateTimeOffset? baseline = null) =>
        Q.InspectNativeAotWebSignalRaceDatesForTests(joined, window ?? Window, baseline ?? Epoch);
    private static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }

    [HostTest("web race Dates check whole owner window cached seconds concurrent streams and incomplete headers")]
    private static void BoundsAndPrefixes()
    {
        var joined = Build([Response(date: Date(5)), Response(date: Date(45))]); var checkedDates = Read(joined);
        HostCheck.True(checkedDates.Responses.Count == 2 && checkedDates.PendingHeaders == 0 && checkedDates.ProducerWindow == Window && checkedDates.OrdinaryLastDateUtc == Epoch);
        HostCheck.True(checkedDates.Responses[0].DateUtc == Epoch.AddSeconds(5) && checkedDates.Responses[1].DateUtc == Epoch.AddSeconds(45));
        foreach (var response in checkedDates.Responses)
            HostCheck.True(response.HeaderEnd <= response.End && response.Ranges.Sum(r => r.ByteCount) == response.HeaderEnd - response.Start &&
                response.Ranges.All(r => r.Original.RemotePort == response.RemotePort && r.Original.ServerToClient));
        var cached = Read(Build([Response(date: Date(20)), Response(date: Date(20))]));
        HostCheck.Equal(cached.Responses[0].DateUtc, cached.Responses[1].DateUtc);
        var concurrent = Read(Build([Response(date: Date(30)), Response(date: Date(10))], separate: true));
        HostCheck.True(concurrent.Responses[0].DateUtc > concurrent.Responses[1].DateUtc && concurrent.Responses[0].ConnectionId != concurrent.Responses[1].ConnectionId);
        string partialBody = Response("FOUR", Date(20))[..^1];
        var partial = Read(Build([partialBody], ["FOU"], outcome: "faulted"));
        HostCheck.True(partial.Responses.Single().State == "body" && partial.Responses.Single().End > partial.Responses.Single().HeaderEnd);
        var pending = Read(Build(["HTTP/1.1 200 OK\r\nDate: Sat, 01 Jan 2000 "], [""], outcome: "faulted", status: 0));
        HostCheck.True(pending.PendingHeaders == 1 && pending.Responses.Count == 0);
        bool immutable = false; try { ((IList<Q.ServiceQuotaHttpPacketRange>)checkedDates.Responses[0].Ranges).Clear(); } catch (NotSupportedException) { immutable = true; }
        HostCheck.True(immutable);
    }

    [HostTest("web race Dates refuse stale noncanonical missing regressing and unbound UTC values")]
    private static void Refusals()
    {
        foreach (string? date in new[] { null, "", "Sat, 01 Jan 2000 12:00:20 UTC", "Sat, 1 Jan 2000 12:00:20 GMT", Date(4), Date(46) })
            Reject(() => Read(Build([Response(date: date)])));
        Reject(() => Read(Build([Response(date: Date(20)), Response(date: Date(19))])));
        var boundary = Build([Response(date: Date(5))]);
        Reject(() => Read(boundary, Window with { StartedUtc = Window.StartedUtc.AddTicks(1) }));
        Reject(() => Read(Build([Response(date: Date(45))]), Window with { CompletedUtc = Window.CompletedUtc.AddTicks(-1) }));
        Reject(() => Read(boundary, Window with { StartedUtc = Window.StartedUtc.ToOffset(TimeSpan.FromHours(1)) }));
        Reject(() => Read(boundary, new(Window.StartedUtc, Window.StartedUtc)));
        Reject(() => Read(boundary, new(Window.StartedUtc, Window.StartedUtc.AddSeconds(45).AddTicks(1))));
        Reject(() => Read(boundary, baseline: Epoch.AddTicks(1)));
        Reject(() => Read(boundary, baseline: Epoch.AddSeconds(16)));
        Reject(() => Read(Build([Response(date: Date(20)).Replace("Date: ", "Date: " + Date(20) + "\r\nDate: ", StringComparison.Ordinal)])));
    }
}
