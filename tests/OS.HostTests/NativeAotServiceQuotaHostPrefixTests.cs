using System.Text;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static class NativeAotServiceQuotaHostPrefixTests
{
    private static NativeAotServiceQuotaPeer.WireTrace Fixture() => new(8080, "stream-control", 26, 12,
        new[]{
            new NativeAotServiceQuotaPeer.WireConnection(1,"control","127.0.0.1",40001,"127.0.0.1",8080,1,25),
            new NativeAotServiceQuotaPeer.WireConnection(2,"control","127.0.0.1",40002,"127.0.0.1",8080,1,25)},
        Array.Empty<NativeAotServiceQuotaPeer.WireExchange>(), new[]{
            Op(1,1,"write",0,2,2,"complete","ab"),
            Op(2,2,"read",0,5,2,"complete","XY"),
            Op(3,1,"read",0,3,2,"complete","cd"),
            Op(4,1,"write",2,1,1,"complete","e"),
            Op(5,1,"read",2,0,0,"zero",""),
            Op(6,2,"write",0,2,2,"complete","12"),
            Op(7,1,"read",2,1,0,"eof",""),
            Op(8,1,"write",3,3,-1,"failed","fgh"),
            Op(9,2,"read",2,4,-1,"canceled","")},
        Array.Empty<NativeAotServiceQuotaPeer.WireSample>());
    private static NativeAotServiceQuotaPeer.WireOperation Op(int id, int connection, string direction, long offset,
        int requested, int transferred, string outcome, string data) =>
        new(id, connection, direction, offset, requested, transferred, id * 2, id * 2 + 1, outcome,
            Convert.ToBase64String(Encoding.ASCII.GetBytes(data)), 0);
    private static byte[] Json(NativeAotServiceQuotaPeer.WireTrace trace) =>
        Encoding.UTF8.GetBytes(NativeAotServiceQuotaPeer.SerializeWireReport(trace));

    [HostTest("quota host prefixes preserve original directional ranges without transmitting failed write attempts")]
    private static void Exact()
    {
        var result = QemuTranscripts.InspectNativeAotServiceQuotaHostPrefixesForTests(Json(Fixture()));
        HostCheck.True(result.KnownReadBytes == 4 && result.KnownWriteBytes == 5 && result.UnknownAttemptedWriteBytes == 3);
        HostCheck.True(result.Connections.Select(c => c.ConnectionId).SequenceEqual(new[] { 1, 2 }));
        var first = result.Connections[0];
        var second = result.Connections[1];
        HostCheck.True(first.Reads.KnownBytes == 2 && first.Reads.KnownBase64 == "Y2Q=" &&
            first.Writes.KnownBytes == 3 && first.Writes.KnownBase64 == "YWJl" &&
            second.Reads.KnownBase64 == "WFk=" && second.Writes.KnownBase64 == "MTI=");
        HostCheck.True(first.Writes.Operations.SequenceEqual(new[]{
            new QemuTranscripts.ServiceQuotaHostRange(1,0,2,2,"complete"),
            new QemuTranscripts.ServiceQuotaHostRange(4,2,1,1,"complete"),
            new QemuTranscripts.ServiceQuotaHostRange(8,3,3,-1,"failed")}));
        HostCheck.True(first.Reads.Operations.SequenceEqual(new[]{
            new QemuTranscripts.ServiceQuotaHostRange(3,0,3,2,"complete"),
            new QemuTranscripts.ServiceQuotaHostRange(5,2,0,0,"zero"),
            new QemuTranscripts.ServiceQuotaHostRange(7,2,1,0,"eof")}));
        var attempted = first.Writes.UnknownWrite;
        HostCheck.True(attempted is { OperationId: 8, Offset: 3, Requested: 3, Transferred: -1, Outcome: "failed", AttemptedBase64: "Zmdo" });
        HostCheck.True(first.Reads.UnknownWrite is null && second.Reads.UnknownWrite is null && second.Writes.UnknownWrite is null &&
            second.Reads.Operations[^1] is { OperationId: 9, Offset: 2, Requested: 4, Transferred: -1, Outcome: "canceled" });
    }

    [HostTest("quota host prefixes copy immutable canceled attempts empty connections and empty partial reports")]
    private static void Copies()
    {
        var fixture = Fixture();
        var ops = fixture.Operations.ToArray();
        ops[7] = ops[7] with
        {
            Outcome = "canceled"
        };
        var extra = new NativeAotServiceQuotaPeer.WireConnection(3, "control", "127.0.0.1", 40003, "127.0.0.1", 8080, 1, 25);
        byte[] bytes = Json(fixture with
        {
            Operations = ops,
            Connections = fixture.Connections.Append(extra).ToArray()
        });
        var result = QemuTranscripts.InspectNativeAotServiceQuotaHostPrefixesForTests(bytes);
        bytes.AsSpan().Clear();
        ops[7] = ops[7] with
        {
            DataBase64 = "eHh4"
        };
        var first = result.Connections[0];
        HostCheck.True(first.Writes.KnownBase64 == "YWJl" && first.Writes.UnknownWrite is { Outcome: "canceled", AttemptedBase64: "Zmdo" });
        HostCheck.True(result.Connections[2].Reads.KnownBytes == 0 && result.Connections[2].Writes.KnownBytes == 0 &&
            result.Connections[2].Reads.Operations.Count == 0 && result.Connections[2].Writes.UnknownWrite is null);
        bool list = false, ranges = false;
        try
        {
            ((IList<QemuTranscripts.ServiceQuotaHostConnection>)result.Connections)[0] = result.Connections[2];
        }
        catch (NotSupportedException)
        {
            list = true;
        }
        try
        {
            ((IList<QemuTranscripts.ServiceQuotaHostRange>)first.Reads.Operations)[0] = default;
        }
        catch (NotSupportedException)
        {
            ranges = true;
        }
        HostCheck.True(list && ranges);
        var zeroOps = fixture.Operations.ToArray();
        zeroOps[7] = zeroOps[7] with
        {
            Requested = 0,
            DataBase64 = ""
        };
        var zero = QemuTranscripts.InspectNativeAotServiceQuotaHostPrefixesForTests(Json(fixture with
        {
            Operations = zeroOps,
            RawBytes = 9
        }));
        HostCheck.True(zero.UnknownAttemptedWriteBytes == 0 && zero.Connections[0].Writes.UnknownWrite is { Requested: 0, Transferred: -1 });
        var empty = fixture with
        {
            Purpose = "quota-partial",
            RawBytes = 0,
            Connections = Array.Empty<NativeAotServiceQuotaPeer.WireConnection>(),
            Operations = Array.Empty<NativeAotServiceQuotaPeer.WireOperation>()
        };
        var none = QemuTranscripts.InspectNativeAotServiceQuotaHostPrefixesForTests(Json(empty));
        HostCheck.True(none.Connections.Count == 0 && none.KnownReadBytes == 0 && none.KnownWriteBytes == 0 &&
            none.UnknownAttemptedWriteBytes == 0);
    }

    [HostTest("quota host prefixes retain strict original JSON offset count identity and unknown direction rejection")]
    private static void Rejections()
    {
        foreach (string bad in new[] { "", "null", "{}" })
            Reject(() => QemuTranscripts.InspectNativeAotServiceQuotaHostPrefixesForTests(Encoding.UTF8.GetBytes(bad)));
        foreach (Action<JsonObject> change in new Action<JsonObject>[]{
            r=>r["rawBytes"]=11,r=>r["extra"]=1,r=>r.Remove("operations"),
            r=>r["connections"]![1]!["id"]=1,
            r=>r["operations"]![7]!["offset"]=4,r=>r["operations"]![7]!["transferred"]=0,
            r=>r["operations"]![8]!["dataBase64"]="QQ==",
            r=>
        {
            r["rawBytes"] = 13;
            r["operations"]!.AsArray().Add(JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(new { id = 10, connectionId = 1, direction = "write", offset = 3, requested = 1, transferred = 1, startedTicks = 20, endedTicks = 21, outcome = "complete", dataBase64 = "eA==", exchangeId = 0 })));
        }})
        {
            var root = JsonNode.Parse(Json(Fixture()))!.AsObject();
            change(root);
            Reject(() => QemuTranscripts.InspectNativeAotServiceQuotaHostPrefixesForTests(Encoding.UTF8.GetBytes(root.ToJsonString())));
        }
    }

    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (Exception error) when (error is ToolFailure or InvalidDataException)
        {
            rejected = true;
        }
        HostCheck.True(rejected);
    }
}
