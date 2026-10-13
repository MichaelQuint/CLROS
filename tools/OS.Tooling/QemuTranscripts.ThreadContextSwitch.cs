namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static IReadOnlyList<string> M09CooperativeMarkers { get; } = Array.AsReadOnly(new[]
    { "TEST_START m09.004", "M09_004_ALLOC", "M09_004_TRANSFER", "M09_004_RESTORE", "M09_004_CLEANUP", "CLROS_M09_004_READY", "TEST_PASS m09.004" });

    // This parser checks actual serial records when called by smoke validation.
    // Synthetic inputs test the parser; they establish no transfer or cleanup.
    public static string[] ValidateCooperativeTail(string[] lines)
    {
        void Need(bool value, string reason) { if (!value) throw new ToolFailure("m09_004_transcript_" + reason); }
        int start = Array.IndexOf(lines, "TEST_START m09.004");
        Need(start >= 0 && Array.LastIndexOf(lines, "TEST_START m09.004") == start, "start");
        Need(!lines.Take(start).Any(line => line.StartsWith("M09_004_", StringComparison.Ordinal) ||
            line == "CLROS_M09_004_READY" || line.StartsWith("TEST_PASS m09.004", StringComparison.Ordinal)), "premature_record");
        Need(lines.Length - start == 23, "record_count");
        ulong[] Row(int index, string marker)
        {
            string[] fields = lines[index].Split(' ', StringSplitOptions.None);
            Need(fields.Length == 5 && fields[0] == marker, "record");
            ulong[] values = new ulong[4];
            for (int i = 0; i < values.Length; i++)
                Need(fields[i + 1].Length == 18 && fields[i + 1].StartsWith("0x", StringComparison.Ordinal) &&
                    ulong.TryParse(fields[i + 1].AsSpan(2), System.Globalization.NumberStyles.HexNumber,
                        System.Globalization.CultureInfo.InvariantCulture, out values[i]), "hex");
            return values;
        }
        ulong[] allocation = Row(start + 1, "M09_004_ALLOC"); ulong pool = allocation[0];
        Need(pool >= 4096 && (pool & 4095) == 0 && pool <= 0x100000000UL - 32 * 4096 &&
            allocation[1] == 32 && allocation[2] == 20 && allocation[3] == 12, "allocation");
        ulong originalRsp = 0;
        for (int step = 0; step < 17; step++)
        {
            ulong[] transfer = Row(start + 2 + step, "M09_004_TRANSFER");
            uint from = step == 0 ? 0U : (step & 1) == 1 ? 1U : 2U;
            uint to = step == 16 ? 0U : from == 1 ? 2U : 1U;
            ulong rip = pool + (from == 0 ? 26UL : from == 1 ? 0x209UL : step == 16 ? 0x324UL : 0x35cUL);
            Need(transfer[0] == (uint)step && transfer[1] == ((ulong)from << 32 | to) && transfer[2] == rip, "sequence");
            if (step == 0) { originalRsp = transfer[3]; Need(originalRsp != 0 && (originalRsp & 15) == 0, "original_stack"); }
            else Need(transfer[3] == pool + (from == 1 ? 16UL : 19UL) * 4096 - 80, "worker_stack");
        }
        ulong[] restored = Row(start + 19, "M09_004_RESTORE");
        Need(restored[0] == 0xffff && (restored[1] & 2) != 0 && (restored[1] & ~0x00200cd7UL) == 0 &&
            restored[2] == originalRsp && restored[3] == pool + 26, "restoration");
        ulong[] cleanup = Row(start + 20, "M09_004_CLEANUP");
        Need(cleanup[0] == 32 && cleanup[1] == 0 && cleanup[2] == 1 && cleanup[3] == 20 * 4096, "cleanup");
        Need(lines[start + 21] == "CLROS_M09_004_READY" && lines[start + 22] == "TEST_PASS m09.004", "terminal");
        return lines[..start];
    }
}
