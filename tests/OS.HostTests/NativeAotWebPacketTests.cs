using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static partial class NativeAotWebPacketTests
{
    private const string Body = "SELECTED", Config = "{\"value\":\"SELECTED\"}";
    private const string LogBody = "CLROS_LOG_OK workers=8 records=64 writev=32 invalid_errno=9";
    [HostTest("web transport requires all five distinct HTTP lifetimes while retaining live state")]
    private static void Lifetimes()
    {
        string Row(int i) => $"M16_011_SLOT event=0 slot={i} owner=8192 epoch=1 generation=1 index={i} local=167772687 localPort=8080 remote=167772674 remotePort={30000 + i} clock={i + 1} deadline=0 duration=0";
        string[] rows = Enumerable.Range(0, 5).Select(Row).ToArray();
        var report = QemuTranscripts.InspectSocketTransportForTests(rows, continuingWeb: true);
        HostCheck.True(report.Fresh == 5 && report.Retired == 0 && report.TimeWaitLifetimes == 0);
        foreach (var invalid in new[]
        {
            rows[..2],
            rows[..4],
            rows.Append(Row(5)).ToArray(),
            rows.Select(x => x.Replace("remotePort=30004", "remotePort=30000", StringComparison.Ordinal)).ToArray()
        }

        )
        {
            bool rejected = false;
            try
            {
                QemuTranscripts.InspectSocketTransportForTests(invalid, continuingWeb: true);
            }
            catch (ToolFailure error)
            {
                rejected = error.Message == "socket_transport_complete";
            }

            HostCheck.True(rejected);
        }
    }

    private static (List<byte[]> Frames, uint Anchor, QemuArpReceiveReceipt Arp) Vector(bool finalAck = false, bool clientHalfClose = false, string mutation = "none", bool quota = false, Func<int, string>? dates = null)
    {
        var original = NativeAotSocketIntegrationTests.Packets();
        var frames = original.Frames.Take(8).ToList();
        for (int i = 0; i < (quota ? 5 : 6); i++)
        {
            ushort port = (ushort)(41000 + i);
            uint client = (uint)(1000 + i * 10000), server = (uint)(2000 + i * 10000);
            void Add(bool outgoing, uint seq, uint ack, byte flags, ReadOnlySpan<byte> payload = default) => frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing, port, seq, ack, flags, payload));
            Add(false, client, 0, 2);
            Add(true, server, client + 1, 0x12);
            client++;
            server++;
            Add(false, client, server, 0x10);
            bool post = i is 2 or 3;
            if (i == 2 && mutation == "method")
                post = false;
            string path = i switch
            {
                1 => "/config",
                2 or 3 => "/__clros/logexercise",
                4 when !quota => "/__clros/vectors",
                _ => "/healthz"
            };
            if (i == 2 && mutation == "path")
                path = "/__clros/other";
            byte[] request = Encoding.ASCII.GetBytes($"{(post ? "POST" : "GET")} {path} HTTP/1.0\r\nHost: 127.0.0.1:22080\r\nConnection: close\r\nAccept: */*\r\n" + (post ? "Content-Length: 0\r\n" : "") + "\r\n");
            Add(false, client, server, 0x18, request);
            client += (uint)request.Length;
            Add(true, server, client, 0x10);
            bool early = clientHalfClose && i == 0;
            if (early)
            {
                Add(false, client, server, 0x11);
                client++;
                Add(true, server, client, 0x10);
            }

            string content = i switch
            {
                1 => Config,
                2 => quota ? "" : LogBody,
                3 => "",
                _ => Body
            };
            if (i == 2 && mutation == "logging-body" || i == (quota ? 4 : 5) && mutation == "later-health")
                content = "WRONG";
            bool quotaFailure = quota && i == 2;
            bool binary = !quota && i == 4;
            byte[] entity = binary ? Enumerable.Range(0, 49152).Select(at => (byte)(at % 251)).ToArray() : Encoding.ASCII.GetBytes(content);
            int declared = entity.Length;
            if (binary && mutation == "vector-byte")
                entity[32769] ^= 1;
            if (binary && mutation == "vector-truncated")
                entity = entity[..^1];
            if (binary && mutation == "vector-length")
                declared--;
            string type = binary && mutation != "vector-type" ? "application/octet-stream" : i == 1 ? "application/json; charset=utf-8" : "text/plain";
            byte[] header = Encoding.ASCII.GetBytes((i == 3 && mutation != "repeat-status" ? "HTTP/1.1 409 Conflict\r\n" : quotaFailure ? "HTTP/1.1 500 Internal Server Error\r\n" : "HTTP/1.1 200 OK\r\n") + (i != 1 ? $"Content-Length: {declared}\r\n" : "") + "Connection: close\r\n" + (i == 3 || quotaFailure ? "" : $"Content-Type: {type}\r\n") + "Date: " + (dates?.Invoke(i) ?? "Thu, 01 Jan 1970 00:00:00 GMT") + "\r\nServer: Kestrel\r\n\r\n");
            byte[] response = [..header, ..entity];
            Add(true, server, client, 0x18, response.AsSpan(0, 5));
            server += 5;
            for (int offset = 5; offset < response.Length;)
            {
                int count = binary ? Math.Min(1024, response.Length - offset) : response.Length - offset;
                Add(true, server, client, 0x18, response.AsSpan(offset, count));
                server += (uint)count;
                offset += count;
                Add(false, client, server, 0x10);
            }

            Add(true, server, client, 0x11);
            server++;
            Add(false, client, server, 0x10);
            if (!early)
            {
                Add(false, client, server, 0x11);
                client++;
            }

            if (i == 0 || finalAck)
                Add(true, server, client, 0x10);
        }

        return (frames, original.Anchor, original.Arp);
    }

    private static QemuTranscripts.WebPacketInspection Inspect(List<byte[]> frames, uint anchor, QemuArpReceiveReceipt arp, string body = Body, int forward = 22080, bool quota = false, IReadOnlyList<QemuTranscripts.WebUtcObservation>? utc = null) => QemuTranscripts.InspectNativeAotWebPacketsForTests(NativeAotSocketIntegrationTests.Capture(frames), anchor, arp, Enumerable.Range(41000, quota ? 5 : 6).Select(i => (uint)i).ToArray(), forward, body, Config, quota, utc);
    private static void Reject(Action action)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (ToolFailure)
        {
            rejected = true;
        }

        HostCheck.True(rejected);
    }

    // Fresh copied synthetic bytes only; no original capture/guest authority.
    internal static ControlPackets RaceControls()
    {
        var value = Controls();
        return value with
        {
            Frames = value.Frames.Select(frame => frame.ToArray()).ToList(),
            Ports = value.Ports.ToArray(),
            Windows = value.Windows.ToArray()
        };
    }

    private static ControlPackets Controls(bool quota = false)
    {
        var original = Vector(quota: quota, dates: i => Date(i));
        var frames = original.Frames;
        int ordinary = quota ? 5 : 6, vlanAt = 0;
        List<WebSocketControls.Exchange> exchanges = [];
        byte[] withheld = [];
        for (int i = 0; i < 2; i++)
        {
            ushort port = (ushort)(41000 + ordinary + i);
            uint client = (uint)(1000 + (ordinary + i) * 10000), server = client + 1000;
            void Add(bool outgoing, uint seq, uint ack, byte flags, ReadOnlySpan<byte> payload = default) => frames.Add(NativeAotSocketIntegrationTests.Tcp(outgoing, port, seq, ack, flags, payload));
            Add(false, client, 0, 2);
            Add(true, server, client + 1, 0x12);
            client++;
            server++;
            Add(false, client, server, 0x10);
            for (int requestIndex = 0; requestIndex < (i == 0 ? 1 : 2); requestIndex++)
            {
                bool keepAlive = i == 1 && requestIndex == 0;
                int second = i == 0 ? 12 : requestIndex == 0 ? 14 : 44;
                byte[] request = Encoding.ASCII.GetBytes("GET /healthz HTTP/1." + (keepAlive ? "1" : "0") + "\r\nHost: 127.0.0.1:22080\r\nConnection: " + (keepAlive ? "keep-alive" : "close") + "\r\nAccept: */*\r\n\r\n");
                var start = Utc.AddSeconds(second);
                byte[] response = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 17\r\n" + (keepAlive ? "" : "Connection: close\r\n") + "Content-Type: text/plain\r\nDate: " + start.ToString("r", CultureInfo.InvariantCulture) + "\r\nServer: Kestrel\r\n\r\nOS_MINIMAL_API_OK");
                Add(false, client, server, 0x18, request);
                client += (uint)request.Length;
                Add(true, server, client, 0x18, response);
                server += (uint)response.Length;
                if (i == 0)
                    withheld = NativeAotSocketIntegrationTests.Tcp(false, port, client, server, 0x10, default);
                else
                    Add(false, client, server, 0x10);
                exchanges.Add(new(port, start, start.AddMilliseconds(100), 100, start, Convert.ToHexString(request), Convert.ToHexString(response)));
                if (keepAlive)
                {
                    byte[] vlan = new byte[1518];
                    Convert.FromHexString("020000000001020000000002810000010800").CopyTo(vlan, 0);
                    vlanAt = frames.Count;
                    frames.Add(vlan);
                }
            }

            if (i == 1)
            {
                Add(true, server, client, 0x11);
                server++;
                Add(false, client, server, 0x11);
                client++;
                Add(true, server, client, 0x10);
            }
        }

        var sockets = new WebSocketControls.Report(exchanges[0], exchanges[1], exchanges[2], new("unused in packet seam", "unused in packet seam", 200, 100));
        var control = new QemuTranscripts.WebControlInspection(sockets, new(42000, 42001, [], 200, 1, [new(50, Convert.ToHexString(withheld))], true, true, true));
        var windows = Enumerable.Range(0, ordinary).Select(i => new QemuTranscripts.WebUtcObservation(Utc.AddSeconds(i * 2).AddMilliseconds(100), Utc.AddSeconds(i * 2).AddMilliseconds(300))).Concat(exchanges.Select(e => new QemuTranscripts.WebUtcObservation(e.StartedUtc, e.CompletedUtc))).ToArray();
        return new(frames, original.Anchor, original.Arp, Enumerable.Range(41000, ordinary + 2).Select(p => (uint)p).ToArray(), windows, control, vlanAt);
    }

    private static QemuTranscripts.WebPacketInspection Inspect(ControlPackets f, bool quota = false) => QemuTranscripts.InspectNativeAotWebPacketsForTests(NativeAotSocketIntegrationTests.Capture(f.Frames), f.Anchor, f.Arp, f.Ports, 22080, Body, Config, quota, f.Windows, f.Controls);
    [HostTest("web packets join bad timeout controls and two complete health responses on one retained healthy connection")]
    private static void ControlStreams()
    {
        foreach (bool quota in new[]
        {
            false,
            true
        }

        )
        {
            var f = Controls(quota);
            var report = Inspect(f, quota);
            HostCheck.True(report.ControlsChecked && report.UtcChecked && report.Connections == (quota ? 7 : 8) && report.Responses.Count == (quota ? 8 : 9));
            HostCheck.True(report.Responses[^1].DateUtc > report.Responses[^2].DateUtc && report.ServerFins == report.Connections - 1);
            Reject(() => QemuTranscripts.InspectNativeAotWebPacketsForTests(NativeAotSocketIntegrationTests.Capture(f.Frames), f.Anchor, f.Arp, f.Ports, 22080, Body, Config, quota, f.Windows));
        }
    }

    [HostTest("web control VLAN is exactly one literal frame between healthy before and after exchanges")]
    private static void ControlVlan()
    {
        var f = Controls();
        var absent = f.Frames.ToList();
        absent.RemoveAt(f.VlanAt);
        Reject(() => Inspect(f with { Frames = absent }));
        var twice = f.Frames.ToList();
        twice.Insert(f.VlanAt, twice[f.VlanAt].ToArray());
        Reject(() => Inspect(f with { Frames = twice }));
        var changed = f.Frames.Select(x => x.ToArray()).ToList();
        changed[f.VlanAt][20] = 1;
        Reject(() => Inspect(f with { Frames = changed }));
        var early = f.Frames.ToList();
        byte[] vlan = early[f.VlanAt];
        early.RemoveAt(f.VlanAt);
        early.Insert(8, vlan);
        Reject(() => Inspect(f with { Frames = early }));
        var late = f.Frames.ToList();
        late.RemoveAt(f.VlanAt);
        late.Add(vlan);
        Reject(() => Inspect(f with { Frames = late }));
        Reject(() => Inspect(f with { Controls = f.Controls with { Frames = f.Controls.Frames with { VlanFrames = 0 } } }));
    }

    [HostTest("web withheld controls require bad tuple checksum flags complete response ACK and bounded sequence")]
    private static void ControlWithheld()
    {
        var f = Controls();
        var original = Convert.FromHexString(f.Controls.Frames.DroppedFrames[0].Hex);
        foreach (byte[] changed in new[]
        {
            original[..^1],
            original.Select((b, i) => (byte)(i == 40 ? b ^ 1 : b)).ToArray(),
            NativeAotSocketIntegrationTests.Tcp(false, (ushort)f.Ports[^1], 1000, 2000, 0x10, default),
            NativeAotSocketIntegrationTests.Tcp(false, (ushort)f.Ports[^2], 1000, 2000, 0x18, "payload"u8)
        }

        )
            Reject(() => Inspect(f with { Controls = f.Controls with { Frames = f.Controls.Frames with { DroppedFrames = [new(50, Convert.ToHexString(changed))] } } }));
        Reject(() => Inspect(f with { Controls = f.Controls with { Frames = f.Controls.Frames with { DroppedFrames = [] } } }));
        var leaked = f.Frames.ToList();
        leaked.Insert(f.VlanAt - 6, original);
        Reject(() => Inspect(f with { Frames = leaked }));
        // Valid envelope/checksum with a wrong response ACK must also fail.
        // Tcp helper independently regenerates both checksums.
        uint seq = BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(38));
        byte[] independent = NativeAotSocketIntegrationTests.Tcp(false, (ushort)f.Ports[^2], seq, 0, 0x10, default);
        Reject(() => Inspect(f with { Controls = f.Controls with { Frames = f.Controls.Frames with { DroppedFrames = [new(50, Convert.ToHexString(independent))] } } }));
    }

    [HostTest("web control raw HTTP report UTC and retained source port must match complete packet streams")]
    private static void ControlIdentity()
    {
        var f = Controls();
        foreach (var sockets in new[]
        {
            f.Controls.Sockets with
            {
                After = f.Controls.Sockets.After with
                {
                    SourcePort = 41099
                }
            },
            f.Controls.Sockets with
            {
                Deadline = f.Controls.Sockets.Deadline with
                {
                    ResponseHex = f.Controls.Sockets.After.ResponseHex
                }
            },
            f.Controls.Sockets with
            {
                Before = f.Controls.Sockets.Before with
                {
                    RequestHex = f.Controls.Sockets.Deadline.RequestHex
                }
            },
            f.Controls.Sockets with
            {
                After = f.Controls.Sockets.After with
                {
                    ResponseHex = f.Controls.Sockets.Before.ResponseHex
                }
            }
        }

        )
            Reject(() => Inspect(f with { Controls = f.Controls with { Sockets = sockets } }));
        Reject(() => QemuTranscripts.InspectNativeAotWebPacketsForTests(NativeAotSocketIntegrationTests.Capture(f.Frames), f.Anchor, f.Arp, f.Ports, 22080, Body, Config, utcWindows: null, controls: f.Controls));
        var shifted = f.Windows.ToArray();
        shifted[^1] = new(Utc.AddSeconds(60), Utc.AddSeconds(60.1));
        Reject(() => Inspect(f with { Windows = shifted }));
    }

    private static readonly DateTimeOffset Utc = new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
    private static string Date(int i, int offset = 0) => Utc.AddSeconds(i * 2 + offset).ToString("r", CultureInfo.InvariantCulture);
    private static QemuTranscripts.WebUtcObservation[] Windows() => Enumerable.Range(0, 6).Select(i => new QemuTranscripts.WebUtcObservation(Utc.AddSeconds(i * 2).AddMilliseconds(100), Utc.AddSeconds(i * 2).AddMilliseconds(300))).ToArray();
    [HostTest("web Date joins every trusted UTC interval and hashes only the declared dynamic Date field")]
    private static void UtcAndHashes()
    {
        var v = Vector(dates: i => Date(i));
        var report = Inspect(v.Frames, v.Anchor, v.Arp, utc: Windows());
        var shifted = Vector(dates: i => Date(i, 1));
        var other = Inspect(shifted.Frames, shifted.Anchor, shifted.Arp, utc: Windows());
        HostCheck.True(report.UtcChecked && report.Responses.Count == 6 && report.Responses[^1].DateUtc > report.Responses[0].DateUtc);
        for (int i = 0; i < 6; i++)
        {
            HostCheck.Equal(Utc.AddSeconds(i * 2), report.Responses[i].DateUtc);
            HostCheck.True(report.Responses[i].RawSha256 != other.Responses[i].RawSha256);
            HostCheck.Equal(report.Responses[i].DateNormalizedSha256, other.Responses[i].DateNormalizedSha256);
        }

        string expected = Convert.ToHexString(SHA256.HashData("HTTP/1.1 200 OK\r\nContent-Length: 8\r\nConnection: close\r\nContent-Type: text/plain\r\nDate: Thu, 01 Jan 1970 00:00:00 GMT\r\nServer: Kestrel\r\n\r\nSELECTED"u8));
        HostCheck.Equal(expected, report.Responses[0].DateNormalizedSha256);
        var old = Vector();
        HostCheck.True(!Inspect(old.Frames, old.Anchor, old.Arp).UtcChecked);
        Reject(() => Inspect(old.Frames, old.Anchor, old.Arp, utc: Windows()));
    }

    [HostTest("web Date rejects well formed stale future regressing and frozen clocks")]
    private static void UtcFailures()
    {
        foreach (var dates in new Func<int, string>[]
        {
            i => Date(i, -6),
            i => Date(i, 6),
            i => i == 3 ? Date(2, -1) : Date(i)
        }

        )
        {
            var v = Vector(dates: dates);
            Reject(() => Inspect(v.Frames, v.Anchor, v.Arp, utc: Windows()));
        }

        var frozen = Vector(dates: _ => Date(0));
        var close = Enumerable.Range(0, 6).Select(i => new QemuTranscripts.WebUtcObservation(Utc.AddMilliseconds(i * 100), Utc.AddMilliseconds(i * 100 + 50))).ToArray();
        Reject(() => Inspect(frozen.Frames, frozen.Anchor, frozen.Arp, utc: close));
        // The finite quota run may complete within one cached header second.
        var quota = Vector(quota: true, dates: _ => Date(0));
        HostCheck.True(Inspect(quota.Frames, quota.Anchor, quota.Arp, quota: true, utc: close[..5]).UtcChecked);
    }

    [HostTest("web UTC windows reject reversal order offset count and overflow with inclusive five second bounds")]
    private static void UtcWindowFailures()
    {
        var v = Vector(dates: i => Date(i));
        var windows = Windows();
        var reversed = windows.ToArray();
        reversed[0] = new(windows[0].CompletedUtc, windows[0].StartedUtc);
        var order = windows.ToArray();
        order[3] = new(Utc.AddSeconds(3), Utc.AddSeconds(3.1));
        var offset = windows.ToArray();
        offset[0] = new(windows[0].StartedUtc.ToOffset(TimeSpan.FromHours(1)), windows[0].CompletedUtc);
        var lower = windows.ToArray();
        lower[0] = new(DateTimeOffset.MinValue, windows[0].CompletedUtc);
        var upper = windows.ToArray();
        upper[^1] = new(windows[^1].StartedUtc, DateTimeOffset.MaxValue);
        foreach (var invalid in new[]
        {
            windows[..5],
            reversed,
            order,
            offset,
            lower,
            upper
        }

        )
            Reject(() => Inspect(v.Frames, v.Anchor, v.Arp, utc: invalid));
        foreach (int seconds in new[]
        {
            -5,
            5
        }

        )
        {
            var exact = Enumerable.Range(0, 6).Select(i => new QemuTranscripts.WebUtcObservation(Utc.AddSeconds(i * 2 + seconds), Utc.AddSeconds(i * 2 + seconds))).ToArray();
            HostCheck.True(Inspect(v.Frames, v.Anchor, v.Arp, utc: exact).UtcChecked);
        }
    }

    [HostTest("web quota packets require empty 500 repeat 409 and later selected health")]
    private static void Quota()
    {
        var v = Vector(quota: true);
        HostCheck.Equal(5, Inspect(v.Frames, v.Anchor, v.Arp, quota: true).Connections);
        Reject(() => Inspect(v.Frames, v.Anchor, v.Arp));
        var normal = Vector();
        Reject(() => Inspect(normal.Frames, normal.Anchor, normal.Arp, quota: true));
        var failed = Vector(mutation: "later-health", quota: true);
        Reject(() => Inspect(failed.Frames, failed.Anchor, failed.Arp, quota: true));
    }

    private static List<byte[]> Change(List<byte[]> frames, int index, Func<byte[], byte[]> change)
    {
        var result = frames.ToList();
        result[index] = change((byte[])result[index].Clone());
        return result;
    }

    [HostTest("web vector packets require exact binary bytes type length and completion across segments")]
    private static void BinaryResponse()
    {
        foreach (string mutation in new[]
        {
            "vector-byte",
            "vector-truncated",
            "vector-length",
            "vector-type"
        }

        )
        {
            var v = Vector(mutation: mutation);
            Reject(() => Inspect(v.Frames, v.Anchor, v.Arp));
        }

        var valid = Vector();
        int at = valid.Frames.FindIndex(f => f.Length > 1000 && BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(36)) == 41004);
        HostCheck.True(at >= 0);
        var missing = valid.Frames.ToList();
        missing.RemoveAt(at);
        Reject(() => Inspect(missing, valid.Anchor, valid.Arp));
        var duplicate = valid.Frames.ToList();
        duplicate.Insert(at + 1, duplicate[at]);
        HostCheck.Equal(Inspect(valid.Frames, valid.Anchor, valid.Arp).Sent, Inspect(duplicate, valid.Anchor, valid.Arp).Sent);
    }

    [HostTest("web packets retain incomplete final close while proving exact selected HTTP streams")]
    private static void Positive()
    {
        var v = Vector();
        var r = Inspect(v.Frames, v.Anchor, v.Arp);
        HostCheck.True(r.Connections == 6 && r.Received == 463 + Encoding.ASCII.GetByteCount("GET /__clros/vectors HTTP/1.0\r\nHost: 127.0.0.1:22080\r\nConnection: close\r\nAccept: */*\r\n\r\n") && r.Sent > 49152 + Body.Length + Config.Length + LogBody.Length && r.ClientFins == 6 && r.ServerFins == 6 && r.ServerFinAcks == 6 && r.ClientFinAcks == 1);
        HostCheck.True(r.TimeWaitPorts.SequenceEqual(new uint[] { 41000 }));
        HostCheck.True(Inspect(Vector(clientHalfClose: true).Frames, v.Anchor, v.Arp).TimeWaitPorts.Count == 0);
        HostCheck.True(Inspect(Vector(true).Frames, v.Anchor, v.Arp).ClientFinAcks == 6);
        int at = v.Frames.FindIndex(f => f.Length > 100 && BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(34)) == 8080);
        var duplicate = v.Frames.ToList();
        duplicate.Insert(at + 1, duplicate[at]);
        HostCheck.Equal(r.Sent, Inspect(duplicate, v.Anchor, v.Arp).Sent);
        var reordered = v.Frames.ToList();
        (reordered[at - 1], reordered[at]) = (reordered[at], reordered[at - 1]);
        HostCheck.Equal(r.Sent, Inspect(reordered, v.Anchor, v.Arp).Sent);
    }

    [HostTest("web packets reject checksum foreign tuple ACK bounds gaps and conflicting retransmissions")]
    private static void Transport()
    {
        var v = Vector();
        int at = v.Frames.FindIndex(f => f.Length > 100 && BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(34)) == 8080);
        Reject(() => Inspect(Change(v.Frames, at, f =>
        {
            f[^1] ^= 1;
            return f;
        }), v.Anchor, v.Arp));
        var gap = v.Frames.ToList();
        gap.RemoveAt(at - 1);
        Reject(() => Inspect(gap, v.Anchor, v.Arp));
        byte[] original = v.Frames[at];
        uint sequence = BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(38));
        uint ack = BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(42));
        byte flags = original[47];
        byte[] payload = original[54..];
        payload[^1] ^= 1;
        var conflict = v.Frames.ToList();
        conflict.Insert(at + 1, NativeAotSocketIntegrationTests.Tcp(true, 41000, sequence, ack, flags, payload));
        Reject(() => Inspect(conflict, v.Anchor, v.Arp));
        Reject(() => Inspect(Change(v.Frames, at, _ => NativeAotSocketIntegrationTests.Tcp(true, 41002, sequence, ack, flags, original.AsSpan(54))), v.Anchor, v.Arp));
        Reject(() => Inspect(Change(v.Frames, at, _ => NativeAotSocketIntegrationTests.Tcp(true, 41000, sequence, ack + 100000, flags, original.AsSpan(54))), v.Anchor, v.Arp));
    }

    [HostTest("web packets reject wrong HTTP body host status headers and missing EOF completion")]
    private static void Http()
    {
        var v = Vector();
        Reject(() => Inspect(v.Frames, v.Anchor, v.Arp, body: "OTHER"));
        Reject(() => Inspect(v.Frames, v.Anchor, v.Arp, forward: 22081));
        int at = v.Frames.FindIndex(f => f.Length > 100 && BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(34)) == 8080);
        byte[] original = v.Frames[at];
        uint sequence = BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(38));
        uint ack = BinaryPrimitives.ReadUInt32BigEndian(original.AsSpan(42));
        foreach ((string from, string to)in new[]
        {
            ("200 OK", "500 OK"),
            ("Server: Kestrel", "Server: Invalid"),
            ("Content-Length: 8", "Content-Length: 9")
        }

        )
            Reject(() => Inspect(Change(v.Frames, at, _ => NativeAotSocketIntegrationTests.Tcp(true, 41000, sequence, ack, 0x18, Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(original, 54, original.Length - 54).Replace(from, to, StringComparison.Ordinal)))), v.Anchor, v.Arp));
        var missing = v.Frames.Where(f => f.Length < 54 || BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(34)) != 8080 || BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(36)) != 41001 || (f[47] & 1) == 0).ToList();
        Reject(() => Inspect(missing, v.Anchor, v.Arp));
    }

    [HostTest("web packets require logging POST repeat409 and selected health after the exercise")]
    private static void LoggingHttp()
    {
        foreach (string mutation in new[]
        {
            "method",
            "path",
            "logging-body",
            "repeat-status",
            "later-health"
        }

        )
        {
            var v = Vector(mutation: mutation);
            Reject(() => Inspect(v.Frames, v.Anchor, v.Arp));
        }

        var valid = Vector();
        foreach (ushort port in new ushort[]
        {
            41002,
            41003,
            41004,
            41005
        }

        )
        {
            var missing = valid.Frames.Where(f => f.Length < 54 || BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(34)) != port && BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(36)) != port).ToList();
            Reject(() => Inspect(missing, valid.Anchor, valid.Arp));
        }

        // Keep SYN order and every valid per-flow stream, but complete repeat
        // and final health before the logging response even begins.
        bool Later(byte[] f) => f.Length >= 54 && (BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(34))is 41003 or 41004 or 41005 || BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(36))is 41003 or 41004 or 41005);
        var later = valid.Frames.Where(Later).ToArray();
        var overlap = valid.Frames.Where(f => !Later(f)).ToList();
        int beforeLogResponse = overlap.FindIndex(f => f.Length > 54 && BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(34)) == 8080 && BinaryPrimitives.ReadUInt16BigEndian(f.AsSpan(36)) == 41002 && f[54] == 'H');
        HostCheck.True(beforeLogResponse >= 0 && later.Length > 0);
        overlap.InsertRange(beforeLogResponse, later);
        Reject(() => Inspect(overlap, valid.Anchor, valid.Arp));
    }
}
