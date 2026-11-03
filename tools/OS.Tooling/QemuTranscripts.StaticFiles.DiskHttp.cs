using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;

namespace OS.Tooling;
public static partial class QemuTranscripts
{
    /// <summary>Borrow the original small-development HTTP receipt and five
    /// body files, PCAP, parent anchors, independently extracted files and full
    /// scheduler/file replay. Check strict one-flow request/response order,
    /// checksums/retries/ACK/FIN, host receipt/headers/entities, JSON, physical
    /// file bytes/metadata and read-before-body-send offsets. Bounds retain the
    /// existing4MiB packet/64KiB entity limits. No process/native/source/guest or
    /// full middleware/Blazor authority is created by supplied values.</summary>

    public static DiskStaticHttpInspection InspectNativeAotDiskStaticHttpValues(byte[] packets, uint anchor, QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports, ReadOnlySpan<byte> receiptJson, IReadOnlyDictionary<string, byte[]> bodies, WebRootDiskInventory.Inspection inventory, DiskStaticReplayReport replay)
    {
        Need(packets is { Length: > 24 and <= 4 * 1024 * 1024 } && ports is { Count: 1 } && receiptJson.Length is> 0 and <= 65536 && bodies is { Count: 5 } && inventory is not null && replay is not null && replay.Static.Connections.Count == 1, "disk_http_input");
        ArgumentNullException.ThrowIfNull(inventory);
        ArgumentNullException.ThrowIfNull(replay);
        using var document = JsonDocument.Parse(receiptJson.ToArray(), new() { MaxDepth = 8 });
        var root = document.RootElement;
        StaticObject(root, "Schema", "Scope", "Port", "Complete", "ClockFrequency", "Responses");
        int port = root.GetProperty("Port").GetInt32();
        long frequency = root.GetProperty("ClockFrequency").GetInt64();
        var rows = root.GetProperty("Responses");
        Need(root.GetProperty("Schema").GetString() == DiskStaticHttpContract.Schema && root.GetProperty("Scope").GetString() == DiskStaticHttpContract.Scope && root.GetProperty("Complete").GetBoolean() && port is> 0 and <= 65535 && frequency is> 0 and <= 1_000_000_000 && rows.ValueKind == JsonValueKind.Array && rows.GetArrayLength() == 5, "disk_http_receipt");
        List<ServiceWireByteRange> ranges = [];
        var capture = ServiceWirePackets(packets, anchor, arp, ports, concurrent: true, lifecycle: true, observe: r =>
        {
            Need(ranges.Count < MaximumServiceWireRanges, "disk_http_packet_range_bound");
            ranges.Add(r);
        }, quotaStreams: true);
        Need(capture.QuietSyns == 0 && capture.Flows.Count == 1, "disk_http_flow_inventory");
        var flow = capture.Flows[0];
        ServiceWireClose(flow, serverFirst: false);
        var connection = replay.Static.Connections[0];
        Need(connection.Connection == 0 && connection.Reset == 0 && connection.Sent == (ulong)flow.ResponseExtent && connection.Received == (ulong)flow.RequestExtent, "disk_http_syscall_wire_totals");
        var writes = replay.SocketWrites.Where(w => w.Connection == 0).ToArray();
        ulong queued = 0;
        int previousCall = -1;
        foreach (var write in writes)
        {
            Need(write.CallRecordIndex > previousCall && write.Before == queued && write.After > write.Before && write.After <= (ulong)flow.ResponseExtent, "disk_http_queued_stream");
            queued = write.After;
            previousCall = write.CallRecordIndex;
        }

        Need(queued == (ulong)flow.ResponseExtent, "disk_http_queued_extent");
        int requestAt = 0, responseAt = 0, previousResponsePacket = 0, previousSend = -1, usedReads = 0;
        long finished = 0, firstStarted = 0;
        List<DiskStaticHttpDataJoin> joined = [];
        for (int ordinal = 0; ordinal < 5; ordinal++)
        {
            var row = rows[ordinal];
            StaticObject(row, "Ordinal", "Method", "Path", "Status", "RequestHeaders", "ResponseHeaders", "Bytes", "Sha256", "BodyFile", "Started", "Finished");
            string name = ordinal.ToString("D3", CultureInfo.InvariantCulture) + ".body", path = DiskStaticHttpContract.Paths[ordinal];
            long started = row.GetProperty("Started").GetInt64(), ended = row.GetProperty("Finished").GetInt64();
            Need(row.GetProperty("Ordinal").GetInt32() == ordinal && row.GetProperty("Method").GetString() == "GET" && row.GetProperty("Path").GetString() == path && row.GetProperty("Status").GetInt32() == 200 && row.GetProperty("BodyFile").GetString() == name && started > 0 && ended >= started && started >= finished && (Int128)ended - started <= (Int128)60 * frequency && bodies.TryGetValue(name, out var body) && body.Length <= DiskStaticHttpContract.MaximumBodyBytes, "disk_http_peer_row");
            byte[] entity = bodies[name];
            finished = ended;
            if (ordinal == 0)
                firstStarted = started;
            Need(row.GetProperty("Bytes").GetInt32() == entity.Length && row.GetProperty("Sha256").GetString() == Convert.ToHexString(SHA256.HashData(entity)), "disk_http_peer_body");
            var request = ServiceHttpHead(flow.Request.AsSpan(requestAt, flow.RequestExtent - requestAt));
            Need(request.First == "GET " + path + " HTTP/1.1" && request.Headers.Count == 1 && request.Headers.GetValueOrDefault("Host") == "127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) && StaticHeaderList(row.GetProperty("RequestHeaders")).Count == 0, "disk_http_wire_request");
            var response = StaticResponse(flow.Response.AsSpan(responseAt, flow.ResponseExtent - responseAt), "GET");
            var headers = StaticHeaderList(row.GetProperty("ResponseHeaders"));
            Need(response.Status == 200 && response.Body.AsSpan().SequenceEqual(entity) && StaticHeadersEqual(headers, response.Headers) && headers.GetValueOrDefault("Server") == "Kestrel" && StaticDate(headers.GetValueOrDefault("Date", "")), "disk_http_wire_response");
            int requestFirst = StaticRecords(ranges, flow.Port, false, requestAt, requestAt + request.End, false), requestLast = StaticRecords(ranges, flow.Port, false, requestAt, requestAt + request.End, true), responseFirst = StaticRecords(ranges, flow.Port, true, responseAt, responseAt + response.End, false), responseLast = StaticRecords(ranges, flow.Port, true, responseAt, responseAt + response.End, true);
            Need(requestFirst > previousResponsePacket && responseFirst >= requestLast, "disk_http_sequential_requests");
            previousResponsePacket = responseLast;
            string type = headers.GetValueOrDefault("Content-Type", "");
            if (ordinal == 0)
                Need(entity.AsSpan().SequenceEqual("OS_MINIMAL_API_OK"u8) && type == "text/plain", "disk_http_health");
            else if (ordinal is 1 or 2)
            {
                Need(type == "application/json; charset=utf-8", "disk_http_json_media");
                StaticJsonBody(entity, ordinal + 1);
            }
            else
            {
                var file = inventory!.Find(WebRootDiskInventory.Root + path);
                Need(file is { IsDirectory: false } && entity.AsSpan().SequenceEqual(file.CopyBytes()) && headers.GetValueOrDefault("Content-Length") == file.Bytes.ToString(CultureInfo.InvariantCulture) && !headers.ContainsKey("Transfer-Encoding") && headers.GetValueOrDefault("Accept-Ranges") == "bytes" && headers.GetValueOrDefault("Cache-Control") == "no-store" && type == (ordinal == 3 ? "text/html" : "text/plain"), "disk_http_physical_asset");
                var modified = DateTimeOffset.FromUnixTimeSeconds(file!.ModifiedSeconds);
                string etag = "\"" + Convert.ToString(modified.ToFileTime() ^ file.Bytes, 16) + "\"";
                Need(headers.GetValueOrDefault("Last-Modified") == modified.ToString("r", CultureInfo.InvariantCulture) && headers.GetValueOrDefault("ETag") == etag, "disk_http_physical_metadata");
                var windows = replay.Reads.Where(r => r.Inode == file.Inode).ToArray();
                Need(windows.Length == 1 && windows[0].Offset == 0 && windows[0].ReturnedBytes == file.Bytes && windows[0].BeginRecordIndex > replay.ReadinessRecordIndex && windows[0].BeginRecordIndex > previousSend, "disk_http_unique_cold_read");
                int bodyStart = responseAt + response.HeaderEnd, bodyEnd = responseAt + response.End;
                var sends = writes.Where(w => w.Before < (ulong)bodyEnd && w.After > (ulong)bodyStart).ToArray();
                Need(sends.Length > 0 && windows[0].CompleteRecordIndex < sends[0].CallRecordIndex, "disk_http_read_before_send");
                usedReads++;
                var window = windows[0];
                joined.Add(new(ordinal, file.Path, file.Inode, window.DescriptorGeneration, window.RequestGeneration, window.CompleteRecordIndex, sends[0].CallRecordIndex, requestFirst, responseLast));
            }

            previousSend = writes.Where(w => w.Before < (ulong)(responseAt + response.End) && w.After > (ulong)responseAt).Max(w => w.CallRecordIndex);
            requestAt += request.End;
            responseAt += response.End;
        }

        Need(requestAt == flow.RequestExtent && responseAt == flow.ResponseExtent && usedReads == replay.Reads.Count && (Int128)finished - firstStarted <= (Int128)DiskStaticHttpContract.MatrixTimeoutSeconds * frequency, "disk_http_complete_inventory");
        return new(1, 5, capture.TcpRecords, requestAt, responseAt, Array.AsReadOnly(joined.ToArray()), Array.AsReadOnly(new[] { new StaticFileHttpFlow(flow.Port, requestAt, responseAt, false) }));
    }
}
