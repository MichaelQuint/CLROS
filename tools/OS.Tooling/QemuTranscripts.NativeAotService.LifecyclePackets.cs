using System.Globalization;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Borrow bounded original PCAP, parent ARP, ordered admitted remote
    /// ports and explicitly v2 peer text synchronously. Reconstruct at most512
    /// connections,32KiB echo requests and512KiB stream responses; require exact
    /// ordinary exchanges, lifecycle order, two client cancellations, drain/reuse and
    /// healthy EOF before the held stream's last new bytes. Invalid/incomplete
    /// observations throw. This value-only inspection creates no source, native,
    /// runner, resource-retirement or CLROS guest acceptance authority.</summary>
    public static ServiceHttpPacketInspection InspectNativeAotServiceLifecyclePacketsForTests(byte[] bytes, uint anchor,
        QemuArpReceiveReceipt arp, IReadOnlyList<uint> ports, string peerReport)
    {
        var peer = NativeAotServicePeer.InspectOutput(peerReport, lifecycle: true);
        using var document = JsonDocument.Parse(peerReport);
        var exchanges = document.RootElement.GetProperty("exchanges").EnumerateArray().ToArray();
        var wire = ServiceWirePackets(bytes, anchor, arp, ports, concurrent: true, lifecycle: true);
        int end = wire.Flows.Count - (exchanges.Length - 6);
        Need(end > 6 && peer.Lifecycle is not null, "service_lifecycle_wire_extent");
        int lastAwaited = 0;
        void Ordinary(int flowIndex, int exchangeIndex)
        {
            var flow = wire.Flows[flowIndex];
            Need(flow.SynRecord > lastAwaited, "service_lifecycle_wire_sequential");
            ServiceWireClose(flow);
            ServiceHttpExchange(flow.Request.AsSpan(0, flow.RequestExtent), flow.Response.AsSpan(0, flow.ResponseExtent), exchanges[exchangeIndex]);
            lastAwaited = flow.ServerFinRecord;
        }
        for (int i = 0; i < 6; i++)
            Ordinary(i, i);
        int cursor = 6;
        var firstDate = DateTimeOffset.ParseExact(exchanges[5].GetProperty("dateUtc").GetString()!, "O", CultureInfo.InvariantCulture);
        var lastDate = DateTimeOffset.ParseExact(exchanges[6].GetProperty("dateUtc").GetString()!, "O", CultureInfo.InvariantCulture);
        const string prefix = "/__clros/service/lifecycle/";

        (ServiceWireFlow Flow, byte[] Body) Take(string method, string path, int status = 200, bool held = false, bool reset = false)
        {
            Need(cursor < end, "service_lifecycle_wire_short");
            var flow = wire.Flows[cursor++];
            Need(flow.SynRecord > lastAwaited, "service_lifecycle_wire_sequential");
            var request = ServiceHttpHead(flow.Request.AsSpan(0, flow.RequestExtent));
            Need(request.First == method + " " + path + " HTTP/1.1" && request.Headers.GetValueOrDefault("Host") == "clros.service" &&
                request.Headers.GetValueOrDefault("Connection") == "close" && request.Headers.Count == (method == "POST" ? 3 : 2) &&
                (method == "POST" ? request.Headers.GetValueOrDefault("Content-Length") == "32768" && flow.RequestExtent == request.End + 32768 :
                    flow.RequestExtent == request.End), "service_lifecycle_wire_request");
            // Host aborts can arrive as client FIN. Preserve its full close
            // checks and count only literal RST packets as resets.
            if (reset && flow.ResetRecord != 0)
                Need(flow.ResetRecord > 0 && !flow.ClientFin && !flow.ServerFin && flow.ClientSeen == flow.RequestExtent &&
                    flow.ServerSeen == flow.ResponseExtent, "service_lifecycle_wire_reset");
            else
                ServiceWireClose(flow, serverFirst: !reset);
            var response = ServiceLifecycleResponse(flow.Response.AsSpan(0, flow.ResponseExtent), path == prefix + "stream" ? 524288 : 8192, reset);
            Need(response.Status == status && response.Date >= firstDate && response.Date <= lastDate, "service_lifecycle_wire_response");
            if (!held)
                lastAwaited = flow.ServerFinRecord;
            return (flow, response.Body);
        }

        Dictionary<string, ulong>? previous = null;
        (Dictionary<string, ulong> Values, ServiceWireFlow Flow, string Text) Status()
        {
            var result = Take("GET", prefix + "status");
            string text = Encoding.ASCII.GetString(result.Body);
            string[] names = ["active", "waiting", "waits", "canceled", "echoes", "streams", "rejected", "read", "written"];
            var values = ReadRecord(text, "CLROS_LIFECYCLE_STATUS", names.Select(name => name + ":dec").ToArray());
            Need(values.Values.All(value => value <= 1_000_000_000) && values["active"] <= 1 && values["waiting"] <= values["active"] &&
                values["waits"] <= 2 && values["canceled"] <= values["waits"] && values["echoes"] <= 1 && values["streams"] <= 1 &&
                values["rejected"] <= 1 && values["read"] <= 65536 && values["written"] <= 524288 &&
                (previous is null || names.Skip(2).All(name => values[name] >= previous[name])), "service_lifecycle_wire_status");
            previous = values;
            return (values, result.Flow, text);
        }
        (Dictionary<string, ulong> Values, ServiceWireFlow Flow, string Text) Until(Func<Dictionary<string, ulong>, bool> match)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                var status = Status();
                if (match(status.Values))
                    return status;
            }
            throw new ToolFailure("service_lifecycle_wire_status_progress");
        }
        void Health() => Need(Take("GET", "/healthz").Body.AsSpan().SequenceEqual("CLROS_SERVICE_HEALTH_OK"u8), "service_lifecycle_wire_health");
        Need(Status().Values.Values.All(value => value == 0), "service_lifecycle_wire_initial");
        for (int i = 0; i < 12; i++)
            Health();
        var echo = Take("POST", prefix + "echo");
        var bad = Take("POST", prefix + "echo", 400);
        CheckServiceLifecycleEcho(echo.Flow, false);
        CheckServiceLifecycleEcho(bad.Flow, true);
        Need(echo.Body.AsSpan().SequenceEqual("CLROS_LIFECYCLE_ECHO_OK bytes=32768 hash=628d9dc5"u8) &&
            bad.Body.AsSpan().SequenceEqual("CLROS_LIFECYCLE_BAD_BODY"u8), "service_lifecycle_wire_echo");
        Health();
        var stream = Take("GET", prefix + "stream", held: true);
        Need(stream.Body.Length == 524288, "service_lifecycle_wire_stream_extent");
        for (int i = 0; i < stream.Body.Length; i++)
            Need(stream.Body[i] == unchecked((byte)(i * 17 + 31)), "service_lifecycle_wire_stream_body");
        var pending = Until(values => values["active"] == 1 && values["written"] < 524288);
        Need(pending.Values["echoes"] == 1 && pending.Values["rejected"] == 1 && pending.Values["read"] >= 32768 &&
            pending.Values["streams"] == 0 && pending.Values["waits"] == 0, "service_lifecycle_wire_stream_pending");
        var healthy = Take("GET", "/healthz");
        Need(healthy.Body.AsSpan().SequenceEqual("CLROS_SERVICE_HEALTH_OK"u8) && stream.Flow.ResponseFirstRecord < healthy.Flow.ResponseFirstRecord &&
            healthy.Flow.ServerFinRecord < stream.Flow.ResponseLastRecord, "service_lifecycle_wire_healthy_progress");
        pending = Status();
        Need(pending.Values["active"] == 1 && pending.Values["streams"] == 0 && pending.Values["written"] < 524288 &&
            pending.Flow.ServerFinRecord < stream.Flow.ServerFinRecord, "service_lifecycle_wire_held_response");
        lastAwaited = stream.Flow.ServerFinRecord;
        for (ulong attempt = 1; attempt <= 2; attempt++)
        {
            var cancel = Take("GET", prefix + "cancel", held: true, reset: true);
            var waiting = Until(values => values["active"] == 1 && values["waiting"] == 1 && values["waits"] == attempt);
            int cancellationRecord = cancel.Flow.ResetRecord != 0 ? cancel.Flow.ResetRecord : cancel.Flow.ClientFinRecord;
            Need(waiting.Values["streams"] == 1 && waiting.Values["written"] == 524288 && waiting.Values["canceled"] == attempt - 1 &&
                waiting.Flow.ServerFinRecord < cancellationRecord, "service_lifecycle_wire_cancel_pending");
            lastAwaited = cancellationRecord;
            _ = Until(values => values["active"] == 0 && values["waiting"] == 0 && values["canceled"] == attempt);
            Health();
        }
        Need(Status().Text == peer.Lifecycle!.TerminalStatus && cursor == end, "service_lifecycle_wire_terminal");
        for (int i = 6; i < exchanges.Length; i++)
            Ordinary(end + i - 6, i);
        var result = ServiceWireInspection(wire, end - 6);
        Need(result.ClientResets <= 2 && result.ServerActiveCloses == result.Connections - 2 &&
            result.CompleteCloses == result.Connections - result.ClientResets, "service_lifecycle_wire_lifetimes");
        return result;
    }

    private static void CheckServiceLifecycleEcho(ServiceWireFlow flow, bool bad)
    {
        var head = ServiceHttpHead(flow.Request.AsSpan(0, flow.RequestExtent));
        for (int i = 0; i < 32768; i++)
            Need(flow.Request[head.End + i] == (byte)(unchecked((byte)(i * 17 + 31)) ^ (bad && i == 31 ? 1 : 0)), "service_lifecycle_wire_echo_body");
    }

    private static (int Status, DateTimeOffset Date, byte[] Body) ServiceLifecycleResponse(ReadOnlySpan<byte> response, int maximum, bool reset)
    {
        var head = ServiceHttpHead(response);
        var info = NativeAotServicePeer.ResponseHead(response[..(head.End - 4)], maximum, allowBadRequest: true);
        Need(head.Headers.GetValueOrDefault("Server") == "Kestrel", "service_lifecycle_wire_server");
        var entity = response[head.End..];
        if (reset)
        {
            Need(info.Length is 0 or -1 && entity.Length == 0, "service_lifecycle_wire_reset_body");
            return (info.Status, info.Date, []);
        }
        // The actual lifecycle peer requires Content-Length for every normal
        // exchange and permits unfinished chunking only on the cancel routes.
        Need(info.Length >= 0 && entity.Length == info.Length, "service_lifecycle_wire_content_length");
        return (info.Status, info.Date, entity.ToArray());
    }
}
