using System.Text.Json;
using OS.Tools.QemuRunner;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // The selected tuple has exactly two aborted pending handshakes followed
    // by this established flow. Borrow original checked packets; never invent
    // an HTTP close or a replacement capture for the reset lifetime.
    private static void ServiceUnreadPackets(ServiceControlPacket[] packets, ServiceSocketControls.UnreadClose control,
        WebFrameControl.ServiceReport frames, JsonElement expected)
    {
        Need(control is not null && control.Http is not null && packets.Length >= 8, "service_unread_inputs");
        var exchange = control!.Http!;
        byte[] request = ServiceHex(exchange.RequestHex), response = ServiceHex(exchange.ResponseHex);
        ServiceHttpExchange(request, response, expected);
        uint client = 0xfffffd01, server = unchecked(packets[1].Sequence + 1), next = unchecked(client + (uint)request.Length);
        var syn = packets[0];
        var synack = packets[1];
        var ready = packets[2];
        var get = packets[3];
        Need(!syn.Outgoing && syn.Flags == 2 && syn.Sequence == unchecked(client - 1) && syn.Ack == 0 && syn.Payload.Length == 0 && syn.Window == 8192 &&
            synack.Outgoing && synack.Flags == 0x12 && synack.Ack == client && synack.Payload.Length == 0 &&
            !ready.Outgoing && ready.Flags == 0x10 && ready.Sequence == client && ready.Ack == server && ready.Payload.Length == 0 &&
            !get.Outgoing && get.Flags == 0x18 && get.Sequence == client && get.Ack == server && get.Payload.AsSpan().SequenceEqual(request),
            "service_unread_handshake");
        uint end = unchecked(server + (uint)response.Length + 1), acknowledged = 0;
        int received = 0;
        bool fin = false, finAck = false, late = false, reset = false;
        ulong lateTime = 0;
        long lateOrdinal = 0, resetOrdinal = 0;
        foreach (var packet in packets.Skip(4))
        {
            Need(!reset, "service_unread_after_reset");
            if (!packet.Outgoing)
            {
                Need(packet.Sequence == next && packet.Window == 8192, "service_unread_client_sequence");
                if (packet.Payload.Length != 0)
                {
                    Need(!late && finAck && received == response.Length && packet.Flags == 0x18 && packet.Ack == end &&
                        packet.Payload.AsSpan().SequenceEqual("CLROS_UNREAD_LATE"u8), "service_unread_late_payload");
                    late = true;
                    lateTime = packet.Microseconds;
                    lateOrdinal = packet.Ordinal;
                }
                else
                {
                    uint relative = unchecked(packet.Ack - server);
                    // ACKs can lag a later guest packet already present in the
                    // capture. Require monotonic acknowledgements of bytes
                    // actually observed, including the FIN only after it exists.
                    Need(!late && packet.Flags == 0x10 && relative >= acknowledged && relative <= (uint)received + (fin ? 1U : 0),
                        "service_unread_response_ack");
                    acknowledged = relative;
                }
                if (fin && packet.Ack == end)
                    finAck = true;
            }
            else if (packet.Flags == 0x14)
            {
                Need(late && packet.Sequence == end && packet.Ack == unchecked(next + (uint)"CLROS_UNREAD_LATE"u8.Length) && packet.Payload.Length == 0 &&
                    packet.Microseconds >= lateTime && packet.Microseconds - lateTime < 30_000_000, "service_unread_reset");
                reset = true;
                resetOrdinal = packet.Ordinal;
            }
            else if (late)
                Need(packet.Flags == 0x10 && packet.Sequence == end && packet.Ack == unchecked(next + (uint)"CLROS_UNREAD_LATE"u8.Length) && packet.Payload.Length == 0,
                    "service_unread_late_ack");
            else
            {
                uint relative = unchecked(packet.Sequence - server);
                Need(packet.Flags is 0x10 or 0x18 or 0x11 or 0x19 && packet.Ack == next && relative <= (uint)received &&
                    (ulong)relative + (uint)packet.Payload.Length <= (uint)response.Length &&
                    response.AsSpan((int)relative, packet.Payload.Length).SequenceEqual(packet.Payload), "service_unread_response");
                received = Math.Max(received, checked((int)relative + packet.Payload.Length));
                if ((packet.Flags & 1) != 0)
                {
                    Need(relative + (uint)packet.Payload.Length == response.Length && received == response.Length, "service_unread_fin");
                    fin = true;
                }
            }
        }
        Need(reset && lateOrdinal == control.LateOrdinal && resetOrdinal == control.ResetOrdinal, "service_unread_complete");
        var injected = frames.Injections.SingleOrDefault(row => row.Tag == "unread.late");
        Need(injected.Forwarded && injected.Hex == Convert.ToHexString(packets.Single(row => row.Ordinal == lateOrdinal).Raw),
            "service_unread_injection");
        var close = ServiceUnreadCloseRow(control.CloseRow);
        Need(close["remotePort"] == (ulong)exchange.Port && close["remoteNext"] == next && close["unread"] == 0 &&
            (close["localNext"] == unchecked(end - 1) || close["localNext"] == end), "service_unread_close_sequences");
    }

    private static Dictionary<string, ulong> ServiceUnreadCloseRow(string row)
    {
        Need(row is not null && row.Length is > 0 and <= 4096, "service_unread_close_row");
        var value = ReadRecord(row!, "M07_034_CLOSE", [.. SocketLifetimeFields[..10], "localNext:dec", "remoteNext:dec", "unread:dec"]);
        Need(value["slot"] < 8 && value["index"] < 8 && value["owner"] >= 4096 && value["owner"] % 8 == 0 &&
            value["epoch"] != 0 && value["generation"] != 0 && value["local"] == 0x0a00020f && value["localPort"] == 8080 &&
            value["remote"] == 0x0a000202 && value["remotePort"] is >= 1024 and <= 65535 && value["clock"] <= long.MaxValue &&
            value["localNext"] <= uint.MaxValue && value["remoteNext"] <= uint.MaxValue && value["unread"] <= 8192,
            "service_unread_close_identity");
        return value;
    }

    /// <summary>Join the literal last-description observation to its third
    /// selected tuple lifetime, genuine retirement and subsequent slot reuse.
    /// The original native ResetUnread/ReapClosed bodies separately prove the
    /// unread branch and RX/TX scrub-before-generation-release contract. Borrowed
    /// rows and a driver report alone grant no execution or native authority.</summary>
    public static void InspectNativeAotServiceUnreadOwnerForTests(string[] rows, string controlsJson)
    {
        Need(rows is not null && rows.Length is > 0 and <= 1_000_000 && rows.All(row => row is not null && row.Length <= 4096) &&
            rows.Sum(row => (long)row.Length + 2) <= 128L * 1024 * 1024,
            "service_unread_owner_input");
        var control = ServiceControlJson<ServiceSocketControls.Report>(controlsJson);
        Need((control.Schema == "clros.nativeaot.service.controls.v2" && control.Flight is null || control.Schema == "clros.nativeaot.service.controls.v3" && control.Flight is not null) && control.Unread is not null && control.Unread.Http is not null, "service_unread_owner_report");
        var unread = control.Unread!;
        var close = ServiceUnreadCloseRow(unread.CloseRow);
        int[] literal = Enumerable.Range(0, rows!.Length).Where(at => rows[at] == unread.CloseRow).ToArray();
        Need(literal.Length == 1, "service_unread_owner_literal");
        bool Same(Dictionary<string, ulong> row) => SocketLifetimeFields[..9].All(field => row[field[..^4]] == close[field[..^4]]);
        var fresh = rows.Select((row, at) => (row, at)).Where(value => value.row.StartsWith("M16_011_SLOT event=0 ", StringComparison.Ordinal))
            .Select(value => (row: SocketTransport(value.row), value.at)).ToArray();
        var selected = fresh.Where(value => value.row["remotePort"] == (ulong)control.PendingPort).ToArray();
        Need(selected.Length == 3 && Same(selected[2].row) && selected[2].at < literal[0] &&
            selected[2].row["clock"] <= close["clock"] && unread.Http!.Port == control.PendingPort, "service_unread_owner_fresh");
        var retired = rows.Select((row, at) => (row, at)).Where(value => value.row.StartsWith("M16_011_SLOT event=1 ", StringComparison.Ordinal))
            .Select(value => (row: SocketTransport(value.row), value.at)).Where(value => Same(value.row)).ToArray();
        Need(retired.Length == 1 && retired[0].at > literal[0] && retired[0].row["clock"] >= close["clock"] &&
            retired[0].row["deadline"] == 0 && retired[0].row["duration"] == 0, "service_unread_owner_retire");
        var reused = fresh.FirstOrDefault(value => value.at > retired[0].at && value.row["slot"] == close["slot"]);
        Need(reused.row is not null && reused.row["clock"] >= retired[0].row["clock"] && reused.row["index"] == close["index"] &&
            close["generation"] < ulong.MaxValue && reused.row["generation"] == close["generation"] + 1, "service_unread_owner_reuse");
        Need(!rows.Where(row => row.StartsWith("M16_011_TIMEWAIT ", StringComparison.Ordinal)).Select(SocketTransport).Any(Same) &&
            !rows.Where(row => row.StartsWith("M07_030_TRANSFER ", StringComparison.Ordinal)).Any(row =>
                Same(ReadRecord(row, "M07_030_TRANSFER", [..SocketLifetimeFields[..10],"compactIndex:dec","compactGeneration:dec",
                    "originalDeadline:dec","deadline:dec","duration:dec"]))), "service_unread_owner_no_timewait");
    }
}
