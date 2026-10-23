using System.Text;
using OS.Tooling;
using Q = OS.Tooling.QemuTranscripts;
using Values = NativeAotWebSignalRaceHostHttpTests.Values;

// Independent copied stream values and generated whole-PCAP controls. These
// tests supply no owned files/processes, native signal or guest acceptance.
internal static class NativeAotWebSignalRaceStreamJoinTests
{
    private const int Forward = 22080, Cap = 4 * 1024 * 1024;
    private const uint First = 43000, Second = 43001, Client = 1001, Server = 2001;
    private const string Endpoint = "http://127.0.0.1:22080", Health = "OS_MINIMAL_API_OK";
    private static string Data(string text) => Convert.ToBase64String(Encoding.ASCII.GetBytes(text));
    private static string Request(string path = "/__clros/churn/status", string method = "GET") =>
        method + " " + path + " HTTP/1.1\r\nHost: 127.0.0.1:22080\r\n" +
        (method == "POST" ? "Content-Length: 0\r\nX-CLROS-Signal: v1\r\n" : "") + "\r\n";
    private static string Response(string body = "ONE") =>
        "HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: " + body.Length + "\r\n\r\n" + body;
    private static Q.ServiceQuotaPacketDirection Direction(uint port, bool response, string bytes, int record, int offset = 0) =>
        bytes.Length == 0 ? new(0, 0, 0, "", Array.Empty<Q.ServiceQuotaPacketRange>()) :
        new(offset + bytes.Length, offset == 0 ? bytes.Length : 0, bytes.Length, offset == 0 ? Data(bytes) : "",
            new[] { new Q.ServiceQuotaPacketRange(new(port, response, offset, bytes.Length, record, 1, (uint)record), Data(bytes)) });
    private static Q.ServiceQuotaPacketStream Flow(uint port, string request, string response = "", int record = 10) =>
        new(port, Direction(port, false, request, record), Direction(port, true, response, record + 1),
            new(record - 3, true, true, false, false, false, false, false, 0, 0, 0));
    private static Q.ServiceQuotaPacketStreams Inventory(params Q.ServiceQuotaPacketStream[] streams) =>
        new(100, 0, streams.Sum(s => s.Requests.Extent + s.Responses.Extent), streams);
    private static Q.WebSignalRaceStreamJoinInspection Join(Values host, Q.ServiceQuotaPacketStreams guest) =>
        Q.InspectNativeAotWebSignalRaceStreamJoinForTests(host.Wire(), host.Attempts(), host.Selected(), Endpoint, Health, guest);
    private static void Reject(Action action, string? expected = null)
    {
        string? reason = null;
        try { action(); } catch (ToolFailure e) { reason = e.Reason; } catch (InvalidDataException e) { reason = e.Message; }
        HostCheck.True(reason is not null); if (expected is not null) HostCheck.Equal(expected, reason);
    }
    private static Values Complete()
    { Values host = new(Forward); host.Add(host.Connect(), rawRequest: Request(), rawResponse: Response()); return host; }

    [HostTest("web race stream join binds full original ordinary VLAN PCAP and persistent HTTP first ranges")]
    private static void WholeOriginalPackets()
    {
        var ordinary = NativeAotWebPacketTests.RaceControls();
        var frames = ordinary.Frames.Select(frame => frame.ToArray()).ToList(); int ordinaryCount = frames.Count;
        void Add(bool outgoing, uint port, uint seq, uint ack, byte flags, string bytes = "") =>
            frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing, (ushort)port, seq, ack, flags, Encoding.ASCII.GetBytes(bytes)));
        void Handshake(uint port)
        { Add(false, port, Client - 1, 0, 2); Add(true, port, Server - 1, Client, 0x12); Add(false, port, Client, Server, 0x10); }
        string request = Request(), response = Response(), raceRequest = Request("/__clros/signalrace", "POST"),
            raceResponse = Response("YES"), next = Request("/healthz"), nextResponse = Response(Health);
        Values host = new(Forward); int control = host.Connect(), race = host.Connect("race");
        host.Add(control, rawRequest: request, rawResponse: response);
        host.Add(race, "/__clros/signalrace", "YES", raceResponse, "POST", rawRequest: raceRequest);
        host.Add(control, "/healthz", Health, nextResponse, rawRequest: next); host.Select("baseline-churn", 1);
        Handshake(First); Handshake(Second); int cut = request.Length / 2;
        Add(false, First, Client, Server, 0x18, request[..cut]);
        Add(false, First, Client + (uint)cut, Server, 0x18, request[cut..]);
        Add(false, First, Client, Server, 0x18, request[..cut]); // retry retains the first original witness
        cut = response.Length / 2;
        Add(true, First, Server, Client + (uint)request.Length, 0x18, response[..cut]);
        Add(true, First, Server + (uint)cut, Client + (uint)request.Length, 0x18, response[cut..]);
        Add(false, Second, Client, Server, 0x18, raceRequest);
        Add(true, Second, Server, Client + (uint)raceRequest.Length, 0x18, raceResponse);
        Add(false, First, Client + (uint)request.Length, Server + (uint)response.Length, 0x18, next);
        Add(true, First, Server + (uint)response.Length, Client + (uint)(request.Length + next.Length), 0x18, nextResponse);
        byte[] raw = NativeAotSocketIntegrationTests.Capture(frames);
        var packets = Q.InspectNativeAotWebRacePacketsForTests(raw, ordinary.Anchor, ordinary.Arp,
            [.. ordinary.Ports, First, Second], Forward, "SELECTED", "{\"value\":\"SELECTED\"}", ordinary.Windows, ordinary.Controls);
        var streams = packets.Race.Streams.ToArray(); var ranges = streams[0].Requests.FirstRanges.ToArray();
        string originalRange = ranges[0].DataBase64; int originalRecord = ranges[0].First.Record;
        streams[0] = streams[0] with { Requests = streams[0].Requests with { FirstRanges = ranges } };
        var joined = Join(host, packets.Race with { Streams = streams });
        HostCheck.True(packets.Ordinary.ControlsChecked && packets.Ordinary.UtcChecked && packets.OrdinaryRecords.Contains(ordinary.VlanAt + 1));
        HostCheck.True(joined.Bindings.Count == 2 && joined.Bindings[0].ConnectionId == control && joined.Bindings[0].RemotePort == First &&
            joined.Bindings[1].ConnectionId == race && joined.Bindings[1].RemotePort == Second && joined.UnassignedHostConnections.Count == 0);
        HostCheck.True(joined.Intervals.Count == 6 && joined.Host.Messages.Count == 3 && joined.Host.Samples.Single().ExchangeId == 1);
        foreach (var interval in joined.Intervals)
        {
            HostCheck.True(interval.Ranges.Sum(r => r.ByteCount) == interval.End - interval.Start &&
                interval.Ranges.All(r => r.Original.Record > ordinaryCount && !packets.OrdinaryRecords.Contains(r.Original.Record)));
            HostCheck.True(interval.Ranges.All(r => r.Original.Seconds == 1 && r.Original.Microseconds == r.Original.Record - 1));
        }
        HostCheck.Equal(ordinaryCount + 7, joined.Intervals.First(i => i.ExchangeId == 1 && !i.Response).Ranges[0].Original.Record);
        HostCheck.True(joined.Intervals.Single(i => i.ExchangeId == 3 && !i.Response).Start == request.Length);
        streams[0] = streams[1]; ranges[0] = ranges[0] with { DataBase64 = "" }; Array.Clear(raw); host.Operations.Clear();
        HostCheck.Equal(First, joined.Guest.Streams[0].RemotePort);
        HostCheck.Equal(Data(request + next), joined.Guest.Streams[0].Requests.KnownPrefixBase64);
        HostCheck.Equal(originalRange, joined.Guest.Streams[0].Requests.FirstRanges[0].DataBase64);
        HostCheck.Equal(originalRecord, joined.Guest.Streams[0].Requests.FirstRanges[0].First.Record);
        bool immutable = false; try { ((IList<Q.ServiceQuotaPacketStream>)joined.Guest.Streams).Clear(); } catch (NotSupportedException) { immutable = true; }
        HostCheck.True(immutable);
    }

    [HostTest("web race stream join resolves global required hosts and rejects ambiguous or unmatched byte assignments")]
    private static void RequiredAndAmbiguous()
    {
        string request = Request(), response = Response(); Values host = new(Forward); int optional = host.Connect(), required = host.Connect();
        host.Op(optional, "write", 1, request[..20]);
        host.Exchanges.Add(new(1, "control", "GET", "/__clros/churn/status", 1, host.Operations[^1].EndedTicks, "faulted", 0, "", new[] { optional }));
        host.Add(required, rawRequest: request, rawResponse: response);
        var joined = Join(host, Inventory(Flow(First, request[..20]), Flow(Second, request, response, 20), Flow(44000, "", "", 30)));
        HostCheck.True(joined.Bindings.Select(b => b.ConnectionId).SequenceEqual(new[] { optional, required }) &&
            joined.EmptyGuestPorts.SequenceEqual(new[] { 44000U }) && joined.Intervals.Count == 2);
        var onlyRequired = Join(host, Inventory(Flow(Second, request, response, 20)));
        HostCheck.True(onlyRequired.UnassignedHostConnections.SequenceEqual(new[] { optional }));
        Values ambiguous = new(Forward);
        for (int i = 0; i < 2; i++) ambiguous.Add(ambiguous.Connect(), body: "", rawResponse: "", outcome: "faulted", status: 0, rawRequest: request);
        Reject(() => Join(ambiguous, Inventory(Flow(First, request))), "quota_stream_join_ambiguous");
        Reject(() => Join(Complete(), Inventory(Flow(First, request, Response("BAD")))), "quota_stream_join_unmatched");
        Reject(() => Join(Complete(), Inventory(Flow(First, request[..20]))), "quota_stream_join_unmatched");
    }

    [HostTest("web race stream join keeps unknown attempted writes holes and unread response suffixes explicit")]
    private static void UnknownAndHoles()
    {
        string request = Request(); int cut = request.Length / 2; Values host = new(Forward); int connection = host.Connect();
        host.Op(connection, "write", 1, request[..cut]); host.Op(connection, "write", 1, request[cut..], "failed");
        host.Exchanges.Add(new(1, "control", "GET", "/__clros/churn/status", 1, 100, "faulted", 0, "", new[] { connection }));
        var island = Flow(First, ""); island = island with { Requests = Direction(First, false, request[(cut + 2)..(cut + 5)], 10, cut + 2) };
        var joined = Join(host, Inventory(island)); var compared = joined.Bindings.Single().Compared;
        HostCheck.True(joined.Intervals.Count == 0 && joined.Host.Prefixes.Connections[0].Writes.UnknownWrite is not null &&
            joined.Guest.Streams[0].Requests.ContiguousBytes == 0 && compared.MinimumAttemptedWritePrefixBytes == 5);
        Reject(() => Join(host, Inventory(island with { Requests = Direction(First, false, "BAD", 10, cut + 2) })), "quota_stream_join_unmatched");
        Reject(() => Join(host, Inventory(island with { Requests = Direction(First, false, "X", 10, request.Length) })), "quota_stream_join_unmatched");
        var complete = Complete(); string response = Response();
        var unread = Join(complete, Inventory(Flow(First, request, response + "UNREAD")));
        HostCheck.True(unread.Intervals.Single(i => i.Response).End == response.Length &&
            unread.Bindings.Single().Compared.ResponseBytesCompared == response.Length && unread.Guest.Streams[0].Responses.Extent == response.Length + 6);
        var missingRead = Flow(First, request, response[..^1]); Reject(() => Join(complete, Inventory(missingRead)), "quota_stream_join_unmatched");
        var requestHole = Flow(First, "", response) with { Requests = Direction(First, false, request[1..], 10, 1) };
        Reject(() => Join(complete, Inventory(requestHole)), "quota_stream_join_unmatched");
    }

    [HostTest("web race stream join refuses false copied range budgets metadata and persistent packet order")]
    private static void ShapeAndOrderRefusals()
    {
        var host = Complete(); var flow = Flow(First, Request(), Response()); var valid = Inventory(flow);
        var requestRange = flow.Requests.FirstRanges[0];
        foreach (var malformed in new[]
        {
            valid with { QuietSyns = 1 }, valid with { TcpRecords = -1 }, valid with { AggregateExtent = valid.AggregateExtent + 1 },
            valid with { Streams = new[] { flow, flow } }, valid with { Streams = new[] { flow with { RemotePort = 0 } } },
            valid with { Streams = Enumerable.Repeat(flow, 505).ToArray() },
            Inventory(flow with { Facts = flow.Facts with { SynRecord = -1 } }),
            Inventory(flow with { Requests = flow.Requests with { FirstRanges = new[] { requestRange with { First = requestRange.First with { RemotePort = Second } } } } }),
            Inventory(flow with { Requests = flow.Requests with { FirstRanges = new[] { requestRange, requestRange } } })
        }) Reject(() => Join(host, malformed));
        Reject(() => Join(host, Inventory(flow with { Responses = flow.Responses with { FirstRanges = new[] {
            flow.Responses.FirstRanges[0] with { First = flow.Responses.FirstRanges[0].First with { Record = 9, Microseconds = 9 } }
        } } })), "web_race_stream_join_record_order");
        var hole = Flow(First, "") with { Requests = Direction(First, false, "X", 10, Cap - 1) };
        var another = Flow(Second, "X", record: 20);
        Reject(() => Join(host, Inventory(hole, another)), "web_race_stream_join_packet_bound");
        var overRanges = flow.Requests with { FirstRanges = Enumerable.Repeat(requestRange, 65537).ToArray() };
        Reject(() => Join(host, Inventory(flow with { Requests = overRanges })));
        Values persistent = new(Forward); int id = persistent.Connect(); persistent.Add(id, rawRequest: Request(), rawResponse: Response());
        persistent.Add(id, rawRequest: Request(), rawResponse: Response());
        string bothRequests = Request() + Request(), bothResponses = Response() + Response();
        Reject(() => Join(persistent, Inventory(Flow(First, bothRequests, bothResponses))), "web_race_stream_join_record_order");
    }
}
