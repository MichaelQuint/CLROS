using System.Text;
using OS.Tools.QemuRunner;

internal static class WebSocketControlsTests
{
    private const string Timeout = "M07_027_TIMEOUT slot=6 owner=65536 epoch=1 generation=1 index=6 local=167772687 localPort=8080 remote=167772674 remotePort=50000 clock=30000000000 sharedFailed=0";
    private const string Drop = "M07_028_DROP before=0 after=1 availableBefore=7 availableAfter=8 delivered=0 clock=1000000000 running=1";
    private static byte[] Response(bool close) => Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\n" + (close ? "Connection: close\r\n" : "") +
        "Content-Length: 17\r\nContent-Type: text/plain\r\nDate: Sat, 01 Jan 2000 00:00:00 GMT\r\nServer: Kestrel\r\n\r\nOS_MINIMAL_API_OK");

    [HostTest("web socket controls preserve exact requests and consume one keep-alive response without stealing its successor")]
    private static async Task Framing()
    {
        HostCheck.Equal("GET /healthz HTTP/1.1\r\nHost: 127.0.0.1:18881\r\nConnection: keep-alive\r\nAccept: */*\r\n\r\n",
            Encoding.ASCII.GetString(WebSocketControls.RequestBytes(18881, true)));
        HostCheck.Equal("GET /healthz HTTP/1.0\r\nHost: 127.0.0.1:18881\r\nConnection: close\r\nAccept: */*\r\n\r\n",
            Encoding.ASCII.GetString(WebSocketControls.RequestBytes(18881, false)));
        byte[] first = Response(false), last = Response(true);
        using MemoryStream wire = new([.. first, .. last]);
        HostCheck.True((await WebSocketControls.ReadHealthResponseAsync(wire, false, CancellationToken.None)).AsSpan().SequenceEqual(first));
        HostCheck.Equal((long)first.Length, wire.Position);
        HostCheck.True((await WebSocketControls.ReadHealthResponseAsync(wire, true, CancellationToken.None)).AsSpan().SequenceEqual(last));
    }

    [HostTest("web socket controls reject changed status headers body truncation and extra response bytes")]
    private static async Task InvalidHttp()
    {
        string valid = Encoding.ASCII.GetString(Response(true));
        foreach (string changed in new[]{valid.Replace("200 OK","409 Conflict",StringComparison.Ordinal),
            valid.Replace("Content-Length: 17","Content-Length: 16",StringComparison.Ordinal),
            valid.Replace("OS_MINIMAL_API_OK","OS_MINIMAL_API_NO",StringComparison.Ordinal),
            valid.Replace("Server: Kestrel","Server: Other",StringComparison.Ordinal),
            valid.Replace("Date: Sat, 01 Jan 2000 00:00:00 GMT","Date: stale",StringComparison.Ordinal),
            valid.Replace("Connection: close","Connection: keep-alive",StringComparison.Ordinal),
            valid.Replace("Content-Length: 17","Content-Length: 17\r\nContent-Length: 17",StringComparison.Ordinal),valid[..^1],valid+"x"})
        {
            bool rejected = false;
            using MemoryStream wire = new(Encoding.ASCII.GetBytes(changed));
            try
            {
                _ = await WebSocketControls.ReadHealthResponseAsync(wire, true, CancellationToken.None);
            }
            catch (Exception failure) when (failure is InvalidDataException or EndOfStreamException) { rejected = true; }
            HostCheck.True(rejected);
        }
    }

    [HostTest("web socket controls require isolated actual tuple timeout and one earlier device drop")]
    private static void Events()
    {
        HostCheck.True(WebSocketControls.ObserveEvents([], 50000) is null);
        HostCheck.True(WebSocketControls.ObserveEvents([Drop], 50000) is null);
        var result = WebSocketControls.ObserveEvents([Drop, Timeout], 50000)!.Value;
        HostCheck.Equal(30_000_000_000UL, result.TimeoutClock);
        HostCheck.Equal(1_000_000_000UL, result.DropClock);
        HostCheck.Equal(Timeout, result.TimeoutRow);
        HostCheck.Equal(Drop, result.DropRow);
        // The real driver may recycle the reject and deliver one following
        // valid frame in the same bounded Receive call.
        HostCheck.True(WebSocketControls.ObserveEvents([Drop.Replace("availableAfter=8 delivered=0", "availableAfter=9 delivered=1", StringComparison.Ordinal), Timeout], 50000).HasValue);
        HostCheck.True(WebSocketControls.ObserveEvents([Drop.Replace("availableBefore=7 availableAfter=8", "availableBefore=65535 availableAfter=0", StringComparison.Ordinal), Timeout], 50000).HasValue);
        foreach (var change in new (string From, string To)[]{("remotePort=50000","remotePort=50001"),("sharedFailed=0","sharedFailed=1"),
            ("generation=1","generation=0"),("slot=6","slot=8"),("owner=65536","owner=0"),
            ("availableAfter=8","availableAfter=9"),("delivered=0","delivered=1"),("running=1","running=0"),
            ("delivered=0","delivered=2"),
            ("clock=1000000000","clock=30000000001"),("clock=1000000000","clock=18446744073709551615")})
            Reject([Drop.Replace(change.From, change.To, StringComparison.Ordinal), Timeout.Replace(change.From, change.To, StringComparison.Ordinal)]);
        Reject([Drop, Drop]);
        Reject([Drop, Timeout, Timeout]);
        Reject([Drop + " extra=1", Timeout]);
        void Reject(string[] rows)
        {
            bool rejected = false;
            try
            {
                _ = WebSocketControls.ObserveEvents(rows, 50000);
            }
            catch (InvalidDataException) { rejected = true; }
            HostCheck.True(rejected);
        }
    }
}
