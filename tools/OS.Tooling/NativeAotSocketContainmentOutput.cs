using System.Globalization;

namespace OS.Tooling;
/// <summary>Strict value validation for the dedicated containment app's raw
/// stdout. Does not grant package, syscall, scheduler, packet or guest authority;
/// those original-source and parent evidence joins remain mandatory.</summary>

public static partial class NativeAotSocketContainmentOutput
{
    /// <summary>Read exactly the six app rows for the selected port. Accept LF or
    /// CRLF; reject extra/malformed rows, early timeout, missing healthy peers,
    /// inconsistent counters or absent worker progress. Throws ToolFailure;
    /// caller-owned text remains unchanged and returned peers are read-only.</summary>

    public static Observation Read(string text, int port = 8080)
    {
        Need(port is >= 1 and <= 65535 && text.Length <= 8192 && text.EndsWith('\n'), "extent");
        string[] rows = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        Need(rows.Length == 7 && rows[^1].Length == 0 && rows[0] == "TEST_START nativeaot.socket.containment", "rows");
        var listen = Values(rows[1], "SOCKET_CONTAINMENT_LISTEN", "port", "peers", "rounds", "interval_ms", "deadline_ms");
        Need(listen.SequenceEqual(new ulong[] { (ulong)port, 3, 15, 5000, 100000 }), "listen");
        var timeout = Values(rows[2], "SOCKET_CONTAINMENT_TIMEOUT", "elapsed_ms", "queued", "worker");
        Need(timeout[0] is >= 60000 and < 100000 && timeout[1] is >= 32768 and < 65536 && timeout[2] is> 0 and <= int.MaxValue, "timeout");
        Healthy[] peers = new Healthy[2];
        for (int i = 0; i < peers.Length; i++)
        {
            var value = Values(rows[3 + i], "SOCKET_CONTAINMENT_HEALTHY", "role", "rounds", "elapsed_ms", "worker");
            Need(value[0] is 1 or 2 && value[1] == 15 && value[2] is >= 70000 and < 100000 && value[3] > timeout[2] && value[3] <= int.MaxValue, "healthy");
            peers[i] = new(value[0], value[2], value[3]);
        }

        Need(peers[0].Role != peers[1].Role && peers[1].Worker >= peers[0].Worker, "healthy_order");
        var pass = Values(rows[5], "TEST_PASS nativeaot.socket.containment", "peers", "timedout", "queued", "eof", "shutdown", "worker_before", "worker_timeout", "worker", "elapsed_ms");
        Need(pass[0] == 3 && pass[1] == 1 && pass[2] == timeout[1] && pass[3] == 1 && pass[4] == 1 && pass[5] < timeout[2] && pass[6] == timeout[2] && pass[7] >= peers[1].Worker && pass[7] <= int.MaxValue && pass[8] > timeout[0] && pass[8] >= peers.Max(peer => peer.ElapsedMilliseconds) && pass[8] < 100000, "complete");
        return new(pass[2], timeout[0], pass[5], pass[6], pass[7], pass[8], Array.AsReadOnly(peers));
    }

    private static ulong[] Values(string row, string prefix, params string[] fields)
    {
        Need(row.StartsWith(prefix + " ", StringComparison.Ordinal), "prefix");
        string[] values = row[(prefix.Length + 1)..].Split(' ');
        Need(values.Length == fields.Length, "fields");
        ulong[] result = new ulong[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            string key = fields[i] + "=";
            Need(values[i].StartsWith(key, StringComparison.Ordinal), "key");
            string number = values[i][key.Length..];
            Need(number.Length > 0 && (number.Length == 1 || number[0] != '0') && ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out result[i]), "number");
        }

        return result;
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("socket_containment_stdout_" + reason);
    }
}
