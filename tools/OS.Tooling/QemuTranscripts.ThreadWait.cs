namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Admit the exact mechanical schedule independently of its native
    /// emitter. This validates serial structure and joins only; guest/native and
    /// frozen source provenance remain separate evidence requirements.</summary>
    public static string[] ValidateThreadWaitTail(string[] lines, bool tls)
    {
        string id = tls ? "010" : "009", name = "m09." + id, prefix = "M09_" + id + "_";
        void Check(bool value, string reason) { if (!value) throw new ToolFailure("m09_" + id + "_transcript_" + reason); }
        int start = Array.IndexOf(lines, "TEST_START " + name), at = start;
        Check(start >= 0 && Array.LastIndexOf(lines, "TEST_START " + name) == start, "start");
        Check(!lines.Take(start).Any(s => s.StartsWith(prefix, StringComparison.Ordinal) || s == "CLROS_" + prefix + "READY" || s.StartsWith("TEST_PASS " + name, StringComparison.Ordinal)), "premature_record");
        void Line(string text) { Check(at < lines.Length && lines[at++] == text, "ordered_marker"); }
        ulong[] Row(string suffix)
        {
            Check(at < lines.Length, "missing_record"); string[] fields = lines[at++].Split(' ', StringSplitOptions.None);
            Check(fields.Length == 5 && fields[0] == prefix + suffix, "ordered_" + suffix);
            ulong[] values = new ulong[4];
            for (int i = 0; i < 4; i++) Check(fields[i + 1].Length == 18 && fields[i + 1].StartsWith("0x", StringComparison.Ordinal) &&
                ulong.TryParse(fields[i + 1].AsSpan(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out values[i]), "hex");
            return values;
        }
        void Exact(string suffix, ulong a, ulong b, ulong c, ulong d) => Check(Row(suffix).SequenceEqual(new[] { a, b, c, d }), "values_" + suffix);
        Line("TEST_START " + name); var preparation = Row("PREPARED"); ulong pool = preparation[0];
        Check(pool >= 4096 && (pool & 4095) == 0 && pool <= 0x100000000UL - 32 * 4096 && preparation[1] == 24 &&
            preparation[2] == pool + 13 * 4096 && preparation[3] == (pool ^ (tls ? 0x4d30393031304f57UL : 0x4d30393030394f57UL)), "preparation");
        var capacity = Row("REJECT"); Check(capacity[0] == 0 && capacity[1] == (ulong)OS.Kernel.Threading.KernelThreadTable.Status.Full && capacity[2] != 0 && capacity[3] == 2, "capacity");
        const ulong supervisor = 0x1ffffffff; ulong transfer = 0, epoch = 0; ulong? kernelGs = null;
        for (ulong cycle = 0; cycle < 2; cycle++)
        {
            ulong a = ((cycle + 1) << 32) | 1, b = ((cycle + 1) << 32) | 2;
            ulong addressA = ((cycle + 1) << 32) | 1, addressB = 0x100000002;
            void Wake(bool second, ulong maximum, ulong count) => Exact("WAKE", second ? addressB : addressA, maximum, count, epoch);
            void Observe(ulong thread, ulong state, ulong completion, ulong sequence) => Exact("OBSERVE", thread, state, completion, sequence);
            void Dispatch(ulong thread, uint action)
            {
                bool first = (uint)thread == 1; ulong worker = first ? 1UL : 2, body = first ? 0x600UL : 0x700;
                Exact("TRANSFER", transfer++, supervisor, thread, pool + (first ? 26UL : 0x21a));
                if (action < 4)
                {
                    if (action == 0 && first)
                    {
                        Exact("REJECT", 1, (ulong)OS.Kernel.Threading.WaitAddress.Status.ValueChanged, thread, cycle);
                        Wake(false, 1, 1);
                    }
                    Exact("WAIT", thread, action + 1UL, (action is 0 or 2) && !first ? addressB : addressA, action);
                }
                var segment = Row("TLS");
                kernelGs ??= segment[3];
                Check(segment[0] == thread && segment[1] == pool + 0x3f00 + (worker - 1) * 128 && segment[2] == segment[1] + 64 &&
                    segment[3] == kernelGs && !(segment[3] >= pool && segment[3] < pool + 32 * 4096), "tls_owner");
                Exact("TRANSFER", transfer++, thread, supervisor, pool + (action == 4 ? first ? 0x4a5UL : 0x5a5UL : body + 0x2c + action * 0x30));
            }
            Dispatch(a, 0); Dispatch(b, 0); Observe(a, 1, 1, 1); Observe(b, 4, 0, 1); Wake(false, uint.MaxValue, 0); Wake(true, 1, 1);
            Dispatch(a, 1); Dispatch(b, 1); Wake(false, 1, 1); Observe(a, 1, 1, 2); Observe(b, 4, 0, 2); Wake(false, uint.MaxValue, 1);
            Dispatch(a, 2); Dispatch(b, 2); Wake(false, 1, 1); Exact("TICK", ++epoch, 3, 2, b); Wake(true, uint.MaxValue, 0);
            Observe(a, 1, 1, 3); Observe(b, 1, 2, 3);
            Dispatch(a, 3); Dispatch(b, 3); Exact("CANCEL", a, 4, 4, 1); Exact("TICK", ++epoch, 3, 2, b); Exact("CANCEL", b, 4, 2, 2);
            Wake(false, uint.MaxValue, 0); Dispatch(a, 4); Dispatch(b, 4);
            Exact("REAP", a, cycle * 2 + 1, 688, 128); Exact("REAP", b, cycle * 2 + 2, 688, 128);
            if (cycle == 0) Exact("REUSE", a, 0x200000001, addressA, 0x200000001);
        }
        Exact("COMPLETE", 4, transfer, 4, 4); Exact("RESTORED", 24, 32, 4, 1);
        Line("TEST_PASS " + name); Line("CLROS_" + prefix + "READY"); Check(at == lines.Length, "trailing_record");
        return lines.Take(start).ToArray();
    }
}
