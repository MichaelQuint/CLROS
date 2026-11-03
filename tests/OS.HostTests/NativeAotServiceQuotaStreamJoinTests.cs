using System.Text;
using OS.Tooling;

internal static partial class NativeAotServiceQuotaStreamJoinTests
{
    private const uint Port = 43000, Client = 1001, Server = 2001;
    private const string FirstRequest = "GET /healthz HTTP/1.1\r\nHost: 127.0.0.1:8080\r\n\r\n", NextRequest = "GET /__clros/service/quota/status HTTP/1.1\r\nHost: 127.0.0.1:8080\r\n\r\n";
    private static string Response(string body) => "HTTP/1.1 200 OK\r\nServer: Kestrel\r\nDate: Tue, 20 Oct 2026 12:00:00 GMT\r\nContent-Length: 3\r\n\r\n" + body;
    private static string Data(string text) => Convert.ToBase64String(Encoding.ASCII.GetBytes(text));
    private static NativeAotServiceQuotaPeer.WireOperation Op(int id, int connection, bool response, int offset, string data, int exchange, long start, bool failed = false) => new(id, connection, response ? "read" : "write", offset, failed ? 8192 : data.Length, failed ? -1 : data.Length, start, start + 1, failed ? "failed" : "complete", Data(data), response ? 0 : exchange);
    private static byte[] Wire(bool wrong = false)
    {
        string first = Response(wrong ? "BAD" : "ONE"), next = Response("TWO");
        NativeAotServiceQuotaPeer.WireOperation[] ops = [Op(1, 1, false, 0, FirstRequest, 1, 2), Op(2, 1, true, 0, "", 1, 4, true), Op(3, 2, false, 0, FirstRequest, 2, 7), Op(4, 2, true, 0, first, 2, 9), Op(5, 2, false, FirstRequest.Length, NextRequest, 3, 12), Op(6, 2, true, first.Length, next, 3, 14)];
        var trace = new NativeAotServiceQuotaPeer.WireTrace(8080, "quota-partial", 20, ops.Sum(o => (long)Convert.FromBase64String(o.DataBase64).Length), new[] { new NativeAotServiceQuotaPeer.WireConnection(1, "control", "127.0.0.1", 40001, "127.0.0.1", 8080, 1, 6), new NativeAotServiceQuotaPeer.WireConnection(2, "control", "127.0.0.1", 40002, "127.0.0.1", 8080, 6, 18) }, new[] { new NativeAotServiceQuotaPeer.WireExchange(1, "control", "GET", "/healthz", 1, 6, "failed", 0, "", new[] { 1 }), new NativeAotServiceQuotaPeer.WireExchange(2, "control", "GET", "/healthz", 6, 11, "complete", 200, Data(wrong ? "BAD" : "ONE"), new[] { 2 }), new NativeAotServiceQuotaPeer.WireExchange(3, "control", "GET", "/__clros/service/quota/status", 11, 16, "complete", 200, Data("TWO"), new[] { 2 }) }, ops, new[] { new NativeAotServiceQuotaPeer.WireSample("health.startup", 11, Data(wrong ? "BAD" : "ONE"), 2, 0) });
        return Encoding.UTF8.GetBytes(NativeAotServiceQuotaPeer.SerializeWireReport(trace));
    }

    private static Capture Packets(bool pipelined = false, bool duplicateFlow = false)
    {
        var original = NativeAotSocketIntegrationTests.Packets();
        var frames = original.Frames.Take(8).Select(f => f.ToArray()).ToList();
        void Add(bool outgoing, uint sequence, uint ack, byte flags, string data = "", uint port = Port) => frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing, (ushort)port, sequence, ack, flags, Encoding.ASCII.GetBytes(data)));
        void Handshake(uint port)
        {
            Add(false, Client - 1, 0, 2, port: port);
            Add(true, Server - 1, Client, 0x12, port: port);
            Add(false, Client, Server, 0x10, port: port);
        }

        void Conversation(uint port)
        {
            string first = Response("ONE"), next = Response("TWO");
            int half = FirstRequest.Length / 2;
            Handshake(port);
            Add(false, Client, Server, 0x18, FirstRequest[..half], port);
            Add(false, Client + (uint)half, Server, 0x18, FirstRequest[half..], port);
            if (pipelined)
                Add(false, Client + (uint)FirstRequest.Length, Server, 0x18, NextRequest, port);
            half = first.Length / 2;
            Add(true, Server, Client + (uint)FirstRequest.Length, 0x18, first[..half], port);
            Add(true, Server + (uint)half, Client + (uint)FirstRequest.Length, 0x18, first[half..], port);
            Add(false, Client + (uint)(FirstRequest.Length + (pipelined ? NextRequest.Length : 0)), Server + (uint)first.Length, 0x10, port: port);
            if (!pipelined)
                Add(false, Client + (uint)FirstRequest.Length - 2, Server + (uint)first.Length, 0x18, FirstRequest[^2..] + NextRequest, port);
            // Old response bytes retry in the same packet as the new response;
            // the first-range observer must preserve only the new witness.
            Add(true, Server + (uint)first.Length - 2, Client + (uint)(FirstRequest.Length + NextRequest.Length), 0x18, first[^2..] + next, port);
            Add(false, Client + (uint)(FirstRequest.Length + NextRequest.Length), Server + (uint)(first.Length + next.Length), 0x10, port: port);
        }

        Conversation(Port);
        if (duplicateFlow)
            Conversation(Port + 1);
        else
            Handshake(Port + 1);
        return new(NativeAotSocketIntegrationTests.Capture(frames), original.Anchor, original.Arp);
    }

    private static QemuTranscripts.ServiceQuotaStreamJoinInspection Join(Capture packet, byte[]? wire = null) => QemuTranscripts.InspectNativeAotServiceQuotaStreamJoinForTests(wire ?? Wire(), packet.Bytes, packet.Anchor, packet.Arp, [Port, Port + 1]);
    [HostTest("quota stream join maps original HTTP and every first range without host port or clock identity")]
    private static void OriginalIntervals()
    {
        var packet = Packets();
        byte[] bytes = Wire(), before = packet.Bytes.ToArray();
        var joined = Join(packet, bytes);
        HostCheck.True(joined.Bindings.Count == 1 && joined.Bindings[0].ConnectionId == 2 && joined.Bindings[0].RemotePort == Port && joined.UnassignedHostConnections.SequenceEqual(new[] { 1 }) && joined.EmptyGuestPorts.SequenceEqual(new[] { Port + 1 }));
        HostCheck.True(joined.Intervals.Count == 4 && joined.Intervals.Select(i => i.ExchangeId).SequenceEqual(new[] { 2, 2, 3, 3 }));
        foreach (var interval in joined.Intervals)
            HostCheck.True(interval.Ranges.Sum(r => r.ByteCount) == interval.End - interval.Start && interval.Ranges.All(r => r.Original.RemotePort == Port && r.Original.ServerToClient == interval.Response && r.StreamOffset >= interval.Start && r.StreamOffset + r.ByteCount <= interval.End && r.Original.Record > 0));
        HostCheck.True(joined.Intervals[0].Ranges.Count == 2 && joined.Intervals[1].Ranges.Count == 2 && joined.Intervals[2].Ranges.Single().StreamOffset == FirstRequest.Length && joined.Intervals[3].Ranges.Single().StreamOffset == Response("ONE").Length);
        HostCheck.True(packet.Bytes.AsSpan().SequenceEqual(before));
        Array.Clear(packet.Bytes);
        Array.Clear(bytes);
        bool immutable = false;
        try
        {
            ((IList<QemuTranscripts.ServiceQuotaFlowBinding>)joined.Bindings).Clear();
        }
        catch (NotSupportedException)
        {
            immutable = true;
        }

        HostCheck.True(immutable && joined.Intervals[3].Ranges[0].Original.Record > joined.Intervals[1].Ranges[^1].Original.Record);
    }

    [HostTest("quota stream join rejects byte mismatch duplicate guest claims and pipelined packet order")]
    private static void InvalidJoins()
    {
        Reject(() => Join(Packets(), Wire(wrong: true)), "quota_stream_join_unmatched");
        Reject(() => Join(Packets(duplicateFlow: true)), "quota_stream_join_unmatched");
        Reject(() => Join(Packets(pipelined: true)), "quota_stream_join_record_order");
        var packet = Packets();
        Reject(() => QemuTranscripts.InspectNativeAotServiceQuotaStreamJoinForTests(Wire(), packet.Bytes, packet.Anchor, packet.Arp, [Port, Port]), "service_wire_ports");
    }

    [HostTest("quota unique flow graph validates IDs duplicate claims limits and impossible required coverage")]
    private static void GraphShapes()
    {
        IReadOnlyList<int> Match(IReadOnlyList<IReadOnlyList<int>> rows, int hosts, IReadOnlyList<int> required) => QemuTranscripts.InspectNativeAotServiceQuotaUniqueFlowAssignmentForTests(rows, hosts, required);
        Reject(() => Match([[1, 1]], 2, []), "quota_stream_join_graph");
        Reject(() => Match([[0]], 1, []), "quota_stream_join_graph");
        Reject(() => Match([[1]], 2, [1, 1]), "quota_stream_join_graph");
        Reject(() => Match([[1]], 2, [2]), "quota_stream_join_unmatched");
        Reject(() => Match([], 8193, []), "quota_stream_join_graph");
        var edges = Enumerable.Range(1, 129).ToArray();
        Reject(() => Match(Enumerable.Repeat<IReadOnlyList<int>>(edges, 512).ToArray(), 129, []), "quota_stream_join_graph");
    }

    [HostTest("quota unique flow assignment matches independent exhaustive required-host injective oracle")]
    private static void UniqueAssignmentOracle()
    {
        int checkedGraphs = 0;
        for (int hosts = 0; hosts <= 4; hosts++)
            for (int guests = 0; guests <= 3; guests++)
                for (int graph = 0; graph < (1 << (hosts * guests)); graph++)
                {
                    var candidates = new IReadOnlyList<int>[guests];
                    for (int guest = 0; guest < guests; guest++)
                    {
                        int mask = (graph >> (guest * hosts)) & ((1 << hosts) - 1);
                        candidates[guest] = Enumerable.Range(1, hosts).Where(id => (mask & (1 << (id - 1))) != 0).ToArray();
                    }

                    for (int requiredMask = 0; requiredMask < (1 << hosts); requiredMask++)
                    {
                        int[] required = Enumerable.Range(1, hosts).Where(id => (requiredMask & (1 << (id - 1))) != 0).ToArray();
                        CheckQuotaAssignmentOracle(candidates, hosts, required);
                        checkedGraphs++;
                    }
                }

        // Local alternative1->2 disappears because the second guest needs2.
        CheckQuotaAssignmentOracle([[1, 2], [2]], 2, [], expectedCount: 1, expectedUnique: [1, 2]);
        // Alternating path moves the unmatched optional host1 to3 while required2 stays covered.
        CheckQuotaAssignmentOracle([[1, 2], [2, 3]], 3, [2], expectedCount: 2);
        // Required-host coverage forces the sole guest away from optional1.
        CheckQuotaAssignmentOracle([[1, 2]], 2, [2], expectedCount: 1, expectedUnique: [2]);
        // Alternating cycle, even with every host required, leaves two assignments.
        CheckQuotaAssignmentOracle([[1, 2], [1, 2]], 2, [1, 2], expectedCount: 2);
        CheckQuotaAssignmentOracle([[1, 2]], 3, [3], expectedCount: 0);
        HostCheck.True(checkedGraphs > 0);
        var original = new IReadOnlyList<int>[]
        {
            new[]
            {
                1,
                2
            },
            new[]
            {
                2
            }
        };
        var immutable = QemuTranscripts.InspectNativeAotServiceQuotaUniqueFlowAssignmentForTests(original, 2, []);
        ((int[])original[0])[0] = 2;
        HostCheck.True(immutable.SequenceEqual(new[] { 1, 2 }));
        if (immutable is IList<int> mutable)
        {
            bool rejected = false;
            try
            {
                mutable[0] = 2;
            }
            catch (NotSupportedException)
            {
                rejected = true;
            }

            HostCheck.True(rejected && immutable.SequenceEqual(new[] { 1, 2 }));
        }
    }

    private static void CheckQuotaAssignmentOracle(IReadOnlyList<IReadOnlyList<int>> candidates, int hosts, IReadOnlyList<int> required, int? expectedCount = null, int[]? expectedUnique = null)
    {
        var oracle = BruteQuotaAssignments(candidates, hosts, required);
        if (expectedCount.HasValue && oracle.Count != expectedCount.Value || expectedUnique is not null && (oracle.Unique is null || !oracle.Unique.SequenceEqual(expectedUnique)))
            throw new InvalidOperationException("independent quota assignment fixture");
        IReadOnlyList<int>? actual = null;
        string? reason = null;
        try
        {
            actual = QemuTranscripts.InspectNativeAotServiceQuotaUniqueFlowAssignmentForTests(candidates, hosts, required);
        }
        catch (ToolFailure error)
        {
            reason = error.Reason;
        }

        string? expected = oracle.Count switch
        {
            0 => "quota_stream_join_unmatched",
            1 => null,
            _ => "quota_stream_join_ambiguous"
        };
        if (reason != expected || oracle.Count == 1 && (actual is null || !actual.SequenceEqual(oracle.Unique!)) || oracle.Count != 1 && actual is not null)
            throw new InvalidOperationException("quota assignment oracle mismatch hosts=" + hosts + " required=" + string.Join(',', required) + " graph=" + string.Join(';', candidates.Select(row => string.Join(',', row))) + " expected=" + (expected ?? string.Join(',', oracle.Unique!)) + " actual=" + (reason ?? (actual is null ? "null" : string.Join(',', actual))));
    }

    // Exhaustively enumerate guest choices and test required coverage only at
    // completed leaves. This does not use matching, reachability, alternating
    // graphs, production helpers, or candidate elimination. Count stops at2.
    private static (int Count, int[]? Unique) BruteQuotaAssignments(IReadOnlyList<IReadOnlyList<int>> candidates, int hosts, IReadOnlyList<int> required)
    {
        int count = 0;
        int[]? first = null;
        var used = new bool[hosts + 1];
        var choice = new int[candidates.Count];
        void Visit(int guest)
        {
            if (count == 2)
                return;
            if (guest == candidates.Count)
            {
                foreach (int id in required)
                    if (!used[id])
                        return;
                count++;
                if (count == 1)
                    first = choice.ToArray();
                return;
            }

            foreach (int id in candidates[guest])
            {
                if (used[id])
                    continue;
                used[id] = true;
                choice[guest] = id;
                Visit(guest + 1);
                used[id] = false;
                if (count == 2)
                    return;
            }
        }

        Visit(0);
        return (count, count == 1 ? first : null);
    }

    private static void Reject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (ToolFailure error)
        {
            HostCheck.Equal(reason, error.Reason);
            return;
        }

        throw new InvalidOperationException("expected rejection " + reason);
    }
}
