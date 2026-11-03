namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // Exact mechanical protocol, independent of the encoder. Synthetic admission
    // tests cannot substitute for native object identities and real guest logs.
    public static string[] ValidateKernelThreadTail(string[] lines)
    {
        void Check(bool ok, string reason)
        {
            if (!ok)
                throw new ToolFailure("m09_006_transcript_" + reason);
        }
        int start = Array.IndexOf(lines, "TEST_START m09.006"), at = start;
        Check(start >= 0 && Array.LastIndexOf(lines, "TEST_START m09.006") == start, "start");
        Check(!lines.Take(start).Any(s => s.StartsWith("M09_006_", StringComparison.Ordinal) || s == "CLROS_M09_006_READY" || s.StartsWith("TEST_PASS m09.006", StringComparison.Ordinal)), "premature_record");
        void Line(string text)
        {
            Check(at < lines.Length && lines[at++] == text, "ordered_marker");
        }
        ulong[] Row(string marker)
        {
            Check(at < lines.Length, "missing_record");
            string[] fields = lines[at++].Split(' ', StringSplitOptions.None);
            Check(fields.Length == 5 && fields[0] == marker, "ordered_record_" + marker);
            ulong[] values = new ulong[4];
            for (int i = 0; i < 4; i++)
                Check(fields[i + 1].Length == 18 && fields[i + 1].StartsWith("0x", StringComparison.Ordinal) &&
                ulong.TryParse(fields[i + 1].AsSpan(2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out values[i]), "hex");
            return values;
        }
        void Exact(string marker, ulong a, ulong b, ulong c, ulong d)
        {
            var row = Row(marker);
            Check(row.SequenceEqual(new[] { a, b, c, d }), "values_" + marker);
        }
        Line("TEST_START m09.006");
        var prepared = Row("M09_006_PREPARED");
        ulong pool = prepared[0];
        Check(pool >= 4096 && (pool & 4095) == 0 && pool <= 0x100000000UL - 32 * 4096 && prepared[1] == 23 &&
            prepared[2] == pool + 13 * 4096 && prepared[3] == (pool ^ 0x4d30393030364f57UL), "preparation");
        const ulong supervisor = 0x1ffffffff, a = 0x100000001, b = 0x100000002;
        ulong transfers = 0;
        void Up(ulong id) => Exact("M09_006_TRANSFER", transfers++, supervisor, id, pool + ((uint)id == 1 ? 26UL : 0x21a));
        void Down(ulong id, ulong offset) => Exact("M09_006_TRANSFER", transfers++, id, supervisor, pool + offset);
        void Wait(ulong id, ulong sequence, ulong deadline) => Exact("M09_006_WAIT", id, sequence, deadline, 4);
        void Tick(ulong tick, ulong action, ulong ready, ulong due) => Exact("M09_006_TICK", tick, action, ready, due);
        void Rollback(ulong cycle, OS.Kernel.Threading.KernelThreadTable.Status status, ulong ready)
        {
            var r = Row("M09_006_ROLLBACK");
            Check(r[0] == cycle && r[1] == (ulong)status && r[2] != 0 && r[3] == ready, "rollback");
        }
        Rollback(0, OS.Kernel.Threading.KernelThreadTable.Status.Full, 2);
        Up(a);
        Down(a, 0x61a);
        Up(b);
        Down(b, 0x71a);
        Up(a);
        Wait(a, 1, 2);
        Down(a, 0x62a);
        Up(b);
        Wait(b, 1, 3);
        Down(b, 0x72a);
        Tick(1, 1, 0, 0);
        Tick(2, 3, 1, a);
        Up(a);
        Down(a, 0x63a);
        Up(a);
        Wait(a, 2, 4);
        Down(a, 0x64a);
        Tick(3, 3, 1, b);
        Up(b);
        Down(b, 0x73a);
        Up(b);
        Wait(b, 2, 5);
        Down(b, 0x74a);
        Tick(4, 3, 1, a);
        Up(a);
        Down(a, 0x65a);
        Tick(5, 3, 1, b);
        Up(b);
        Down(b, 0x75a);
        Up(a);
        Exact("M09_006_JOIN", a, b, 23, 1);
        Down(a, 0x4a5);
        ulong old = a, joins = 1;
        for (ulong cycle = 1; cycle <= 2; cycle++)
        {
            ulong current = ((cycle + 1) << 32) | 1;
            Exact("M09_006_REAP", old, cycle, 688, 4096);
            Rollback(cycle, OS.Kernel.Threading.KernelThreadTable.Status.InvalidContext, 0);
            Exact("M09_006_REUSE", old, current, (ulong)OS.Kernel.Threading.KernelThreadTable.Status.StaleIdentity, cycle);
            Up(current);
            Exact("M09_006_JOIN", current, b, 23, ++joins);
            Down(current, 0xa1a);
            Up(current);
            Exact("M09_006_JOIN", current, b, 23, ++joins);
            Down(current, 0xa2a);
            Up(current);
            Wait(current, 1, 5 + cycle);
            Down(current, 0xa3a);
            if (cycle == 1)
            {
                Exact("M09_006_CANCEL", current, 1, 4, 1);
                Tick(6, 1, 1, 0);
            }
            else
            {
                Tick(7, 3, 1, current);
                Exact("M09_006_CANCEL", current, 1, 2, 2);
            }
            Up(current);
            Down(current, 0x4a5);
            old = current;
        }
        Exact("M09_006_REAP", old, 3, 688, 4096);
        Exact("M09_006_REAP", b, 4, 688, 4096);
        Exact("M09_006_COMPLETE", 7, transfers, joins, 5);
        Exact("M09_006_RESTORED", 23, 32, 4, 1);
        Line("TEST_PASS m09.006");
        Line("CLROS_M09_006_READY");
        Check(at == lines.Length, "trailing_record");
        return lines[..start];
    }
}
