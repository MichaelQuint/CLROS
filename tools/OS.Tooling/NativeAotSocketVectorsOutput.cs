using System.Globalization;

namespace OS.Tooling;

/// <summary>Value-only decoding of the finite BufferList fixture's stdout.
/// Validates ordered request progress and joined counters. It supplies no native
/// vector, package, syscall, scheduler, packet or guest execution authority.</summary>
public static class NativeAotSocketVectorsOutput
{
    public readonly record struct Connection(uint Index, uint ReceiveCalls, uint ReceivePartial, uint ReceiveBlocked,
        uint SendCalls, uint SendPartial, uint SendBlocked);
    public sealed record Observation(IReadOnlyList<Connection> Connections);

    /// <summary>Borrow complete LF/CRLF stdout for the selected port. Require
    /// four ordered exact request/response totals, the observed reset, pending
    /// receive disposal and joined final totals. Reject malformed, omitted or
    /// extra rows with ToolFailure. Returned numeric observations are read-only;
    /// caller text remains owned by the caller and is not retained.</summary>
    public static Observation Read(string text, int port = 8080)
    {
        Need(port is >= 1 and <= 65535 && text.Length is > 0 and <= 32768 && text.EndsWith('\n'), "extent");
        string[] rows = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n'); int at = 0;
        string Next() { Need(at < rows.Length, "missing_row"); return rows[at++]; }
        Need(Next() == "TEST_START nativeaot.socket.vectors", "start");
        Need(Values(Next(), "SOCKET_VECTOR_LISTEN", "port", "connections", "deadline_ms", "max_operations")
            .SequenceEqual(new uint[] { (uint)port, 4, 30000, 100000 }), "listen");
        Connection[] connections = new Connection[4];
        for (uint index = 0; index < 4; index++)
        {
            uint received = 0, reads = 0, partial = 0;
            while (received < 64)
            {
                var progress = Values(Next(), "SOCKET_VECTOR_RECEIVE", "index", "bytes", "total", "remaining");
                Need(progress[0] == index && progress[1] > 0 && progress[1] <= 64 - received &&
                    progress[2] == received + progress[1] && progress[3] == 64 - progress[2], "receive_progress");
                if (progress[1] < 64 - received) partial++;
                received += progress[1]; reads++;
            }
            Need(Values(Next(), "SOCKET_VECTOR_REQUEST", "index", "bytes").SequenceEqual(new uint[] { index, 64 }), "request");
            if (index == 2) Need(Next() == "SOCKET_VECTOR_PENDING index=2 pending=1", "pending");
            uint[] value = Values(Next(), "SOCKET_VECTOR_CONNECTION", "index", "received", "sent", "accept_blocked",
                "receive_calls", "receive_partial", "receive_blocked", "send_calls", "send_partial", "send_blocked", "reset", "pending", "aborted");
            uint sent = index == 0 ? 65536u : index == 3 ? 64u : 0;
            Need(value[0] == index && value[1] == 64 && value[2] == sent &&
                value[4] == reads + value[6] + 1 && value[5] == partial &&
                value[10] == (index == 1 ? 1u : 0) && value[11] == (index == 2 ? 1u : 0) && value[12] == value[11], "connection");
            Need((ulong)value[3] + value[4] + value[7] + 1 <= 100000 &&
                (sent == 0 ? value[7] == 0 && value[8] == 0 && value[9] == 0 :
                    value[7] > value[9] && value[7] - value[9] <= sent && value[8] == value[7] - value[9] - 1), "operations");
            connections[index] = new(index, value[4], partial, value[6], value[7], value[8], value[9]);
        }
        Need(Next() == "TEST_PASS nativeaot.socket.vectors connections=4 received=256 sent=65600 reset=1 pending=1 aborted=1" &&
            Next() == "APP_EXIT nativeaot.socket.vectors 0" && Next().Length == 0 && at == rows.Length, "terminal");
        return new(Array.AsReadOnly(connections));
    }

    private static uint[] Values(string row, string prefix, params string[] fields)
    {
        Need(row.StartsWith(prefix + " ", StringComparison.Ordinal), "prefix");
        string[] words = row[(prefix.Length + 1)..].Split(' '); Need(words.Length == fields.Length, "fields");
        uint[] values = new uint[words.Length];
        for (int i = 0; i < words.Length; i++)
        {
            string key = fields[i] + "="; Need(words[i].StartsWith(key, StringComparison.Ordinal), "key");
            string number = words[i][key.Length..];
            Need(number.Length > 0 && (number.Length == 1 || number[0] != '0') &&
                uint.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out values[i]) && values[i] <= 100000, "number");
        }
        return values;
    }
    private static void Need(bool value, string reason) { if (!value) throw new ToolFailure("socket_vectors_stdout_" + reason); }
}
