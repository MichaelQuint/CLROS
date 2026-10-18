using System.Text;
using OS.Tooling;

internal static class NativeAotSocketVectorsOutputTests
{
    private static string Output()
    {
        List<string> rows = ["TEST_START nativeaot.socket.vectors",
            "SOCKET_VECTOR_LISTEN port=8080 connections=4 deadline_ms=30000 max_operations=100000"];
        for (int i = 0; i < 4; i++)
        {
            if (i == 0) rows.Add("SOCKET_VECTOR_RECEIVE index=0 bytes=3 total=3 remaining=61");
            rows.Add($"SOCKET_VECTOR_RECEIVE index={i} bytes={(i == 0 ? 61 : 64)} total=64 remaining=0");
            rows.Add($"SOCKET_VECTOR_REQUEST index={i} bytes=64");
            if (i == 2) rows.Add("SOCKET_VECTOR_PENDING index=2 pending=1");
            rows.Add($"SOCKET_VECTOR_CONNECTION index={i} received=64 sent={(i == 0 ? 65536 : i == 3 ? 64 : 0)} accept_blocked=0 receive_calls={(i == 0 ? 4 : 2)} receive_partial={(i == 0 ? 1 : 0)} receive_blocked={(i == 0 ? 1 : 0)} send_calls={(i == 0 ? 10 : i == 3 ? 1 : 0)} send_partial={(i == 0 ? 7 : 0)} send_blocked={(i == 0 ? 2 : 0)} reset={(i == 1 ? 1 : 0)} pending={(i == 2 ? 1 : 0)} aborted={(i == 2 ? 1 : 0)}");
        }
        rows.Add("TEST_PASS nativeaot.socket.vectors connections=4 received=256 sent=65600 reset=1 pending=1 aborted=1");
        rows.Add("APP_EXIT nativeaot.socket.vectors 0"); return string.Join('\n', rows) + "\n";
    }

    [HostTest("m16-013 vector stdout joins partial progress reset disposal and immutable counters")]
    private static void Valid()
    {
        foreach (string text in new[] { Output(), Output().Replace("\n", "\r\n", StringComparison.Ordinal) })
        {
            var result = NativeAotSocketVectorsOutput.Read(text);
            HostCheck.Equal(4, result.Connections.Count); HostCheck.Equal(1u, result.Connections[0].ReceivePartial);
            HostCheck.Equal(7u, result.Connections[0].SendPartial);
            HostCheck.True(((IList<NativeAotSocketVectorsOutput.Connection>)result.Connections).IsReadOnly);
        }
        _ = NativeAotSocketVectorsOutput.Read(Output().Replace("port=8080", "port=8081", StringComparison.Ordinal), 8081);
    }

    [HostTest("m16-013 vector stdout rejects unjoined counters malformed rows and invented completion")]
    private static void RejectInvalid()
    {
        string output = Output();
        foreach (var change in new (string From, string To)[] {
            ("total=3 remaining=61", "total=4 remaining=60"), ("bytes=61 total=64", "bytes=62 total=64"),
            ("receive_calls=4", "receive_calls=3"), ("receive_partial=1", "receive_partial=0"),
            ("receive_blocked=1", "receive_blocked=2"), ("send_partial=7", "send_partial=8"),
            ("send_partial=7", "send_partial=0"), ("send_calls=1 send_partial=0", "send_calls=65 send_partial=64"),
            ("send_calls=10", "send_calls=2"), ("reset=1 pending=0", "reset=0 pending=0"),
            ("pending=1 aborted=1", "pending=1 aborted=0"), ("sent=65536", "sent=65535"),
            ("index=1 bytes=64", "index=0 bytes=64"), ("bytes=3 total=3", "bytes=03 total=3"),
            ("accept_blocked=0", "accept_blocked=100000"), ("APP_EXIT nativeaot.socket.vectors 0", "APP_EXIT nativeaot.socket.vectors 1") })
            Reject(output.Replace(change.From, change.To, StringComparison.Ordinal));
        Reject(output.Replace("accept_blocked=0 receive_calls=4", "accept_blocked=99986 receive_calls=4", StringComparison.Ordinal));
        foreach (string text in new[] { "", output.TrimEnd('\n'), output + "\n", output + "TEST_PASS invented\n",
            output.Replace("SOCKET_VECTOR_PENDING index=2 pending=1\n", "", StringComparison.Ordinal),
            output.Replace(" index=3 ", " index=4294967296 ", StringComparison.Ordinal), output.Replace("\n", "\r", StringComparison.Ordinal) }) Reject(text);
    }

    [HostTest("m16-013 vector raw output preserves framing beyond4KiB without widening other apps")]
    private static void Framed()
    {
        string progress = string.Join('\n', Enumerable.Range(1, 64).Select(i =>
            $"SOCKET_VECTOR_RECEIVE index=0 bytes=1 total={i} remaining={64 - i}")) + "\n";
        string output = Output().Replace("SOCKET_VECTOR_RECEIVE index=0 bytes=3 total=3 remaining=61\nSOCKET_VECTOR_RECEIVE index=0 bytes=61 total=64 remaining=0\n", progress, StringComparison.Ordinal)
            .Replace("receive_calls=4 receive_partial=1", "receive_calls=66 receive_partial=63", StringComparison.Ordinal);
        byte[] payload = Encoding.UTF8.GetBytes(output); HostCheck.True(payload.Length > 4096);
        using MemoryStream raw = new();
        for (int at = 0; at < payload.Length; at += 257)
        {
            int count = Math.Min(257, payload.Length - at);
            raw.Write(Encoding.ASCII.GetBytes($"M16_005_WRITE fd=1 bytes={count} data="));
            raw.Write(payload, at, count); raw.Write("\r\n"u8);
        }
        byte[] bytes = raw.ToArray(), before = (byte[])bytes.Clone();
        var result = NativeAotThreadingOutput.Read("NativeAotSocketVectors", bytes);
        HostCheck.Equal(output, result.Stdout); HostCheck.True(bytes.AsSpan().SequenceEqual(before));
        bool rejected = false;
        try { _ = NativeAotThreadingOutput.Read("NativeAotSocketSmoke", bytes); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
        rejected = false;
        try { _ = NativeAotThreadingOutput.Read("NativeAotSocketVectors", bytes.AsSpan(0, bytes.Length - 1)); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }

    private static void Reject(string text)
    {
        bool rejected = false; try { _ = NativeAotSocketVectorsOutput.Read(text); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
    }
}
