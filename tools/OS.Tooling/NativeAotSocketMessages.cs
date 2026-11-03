using System.Globalization;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;
/// <summary>Value-only decoding of synchronous checked message snapshots and
/// their next actual syscall records. Caller retains input; returned values are
/// owned and read-only. Malformed, duplicate, orphaned or mismatched records throw.
/// Complete scheduling, mappings, native source, payload and packet validation
/// remain separate. These observations grant no guest execution authority.</summary>

public static partial class NativeAotSocketMessages
{
    /// <summary>Require exact message/vector framing and current complete call
    /// envelopes. Every snapshot binds to the next call; intervening non-message
    /// protocol records remain the complete consumer's responsibility. No input
    /// strings, arrays or kernel/user pointers are retained by returned values.</summary>

    public static IReadOnlyList<Message> Read(string[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return Read(rows, QemuTranscripts.ReadLinuxCallReturns(rows, allowEpoll: true).ToDictionary(x => x.RecordIndex));
    }

    internal static IReadOnlyList<Message> Read(string[] rows, IReadOnlyDictionary<int, QemuTranscripts.LinuxCallReturnObservation> calls)
    {
        var joined = new HashSet<int>();
        var owners = new HashSet<(ulong, ulong)>();
        var result = new List<Message>();
        for (int i = 0; i < rows.Length; i++)
        {
            if (!rows[i].StartsWith("M16_013_", StringComparison.Ordinal))
                continue;
            int start = i;
            ulong[] header = Record(rows[i], "M16_013_MESSAGE", "identity", "ordinal", "nr", "fd", "generation", "header", "flags", "table", "vectors", "total");
            ulong identity = header[0], ordinal = header[1], number = header[2], flags = header[6];
            Need(identity != 0 && ordinal != 0 && number is 46 or 47 && header[3] <= uint.MaxValue && header[4] is> 0 and <= uint.MaxValue && header[8] <= LinuxSocketServices.MaximumMessageVectors && header[9] <= LinuxSocketServices.MaximumMessageBytes && header[5] != 0 && header[5] <= ulong.MaxValue - 56 && (header[8] == 0 || header[7] != 0 && header[7] <= ulong.MaxValue - header[8] * 16) && (flags & ~(number == 46 ? 0x4040UL : 0x42UL)) == 0, "header");
            Need(owners.Add((identity, ordinal)), "duplicate_owner_ordinal");
            uint count = (uint)header[8], total = 0;
            var vectors = new Vector[count];
            for (uint slot = 0; slot < count; slot++)
            {
                Need(++i < rows.Length, "missing_vector");
                ulong[] vector = Record(rows[i], "M16_013_VECTOR", "identity", "ordinal", "index", "address", "bytes");
                Need(vector[0] == identity && vector[1] == ordinal && vector[2] == slot && vector[4] <= LinuxSocketServices.MaximumMessageBytes - total && (vector[4] == 0 || vector[3] != 0 && vector[3] <= ulong.MaxValue - vector[4]), "vector");
                total += (uint)vector[4];
                vectors[slot] = new(vector[3], vector[4]);
            }

            Need(total == header[9], "total");
            int next = i + 1;
            while (next < rows.Length && !calls.ContainsKey(next))
            {
                Need(!rows[next].StartsWith("M16_013_", StringComparison.Ordinal), "orphan_or_reordered");
                next++;
            }

            Need(next < rows.Length && joined.Add(next), "missing_or_duplicate_call");
            var call = calls[next];
            var values = CallValues(rows[next]);
            Need(call.Identity == identity && call.Ordinal == ordinal && values["nr"] == number && values["a0"] == header[3] && values["a1"] == header[5] && values["a2"] == flags && values["valueBits"] == call.ValueBits, "call_binding");
            result.Add(new(start, next, identity, ordinal, number, (uint)header[3], (uint)header[4], header[5], flags, header[7], total, Array.AsReadOnly(vectors)));
        }

        return result.AsReadOnly();
    }

    private static Dictionary<string, ulong> CallValues(string row)
    {
        var values = new Dictionary<string, ulong>(StringComparer.Ordinal);
        foreach (string token in row.Split(' ').Skip(1))
        {
            int split = token.IndexOf('=');
            Need(split > 0 && split < token.Length - 1, "call_field");
            Need(values.TryAdd(token[..split], Number(token[(split + 1)..])), "duplicate_call_field");
        }

        foreach (string key in new[]
        {
            "nr",
            "a0",
            "a1",
            "a2",
            "valueBits"
        }

        )
            Need(values.ContainsKey(key), "missing_call_field");
        return values;
    }

    private static ulong[] Record(string row, string prefix, params string[] fields)
    {
        string[] tokens = row.Split(' ');
        Need(tokens.Length == fields.Length + 1 && tokens[0] == prefix, "record_shape");
        var values = new ulong[fields.Length];
        for (int i = 0; i < fields.Length; i++)
        {
            string expected = fields[i] + "=";
            Need(tokens[i + 1].StartsWith(expected, StringComparison.Ordinal), "record_field");
            values[i] = Number(tokens[i + 1][expected.Length..]);
        }

        return values;
    }

    private static ulong Number(string text)
    {
        Need(ulong.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out ulong value) && value.ToString(CultureInfo.InvariantCulture) == text, "number");
        return value;
    }

    private static void Need(bool valid, string reason)
    {
        if (!valid)
            throw new ToolFailure("socket_message_" + reason);
    }
}
