namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM04NormalFinallyRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines,
        IReadOnlyDictionary<string, ulong> heap)
    {
        const string reason = "m04_normal_finally";
        Need(lines.Count == 18 && lines[0] == "TEST_START m04.021.normal-finally" &&
            lines[16] == "TEST_PASS m04.021.normal-finally" && lines[17] == "CLROS_M04_021_NORMAL_FINALLY_READY",
            reason + "_count");
        Need(gcLines.Count == 17, reason + "_original_gc_count");
        var rows = Enumerable.Range(0, 15).Select(stage => ReadHexRecord(lines[stage + 1], "M04_NORMAL_FINALLY " + stage, 4)).ToArray();
        var image = ReadHexRecord(gcLines[0], "M04_GC_ROOT 0", 4);
        var module = ReadHexRecord(gcLines[1], "M04_GC_ROOT 1", 4);
        var stack = ReadHexRecord(gcLines[2], "M04_GC_ROOT 7", 4);
        var canaries = ReadHexRecord(gcLines[7], "M04_GC_ROOT 8", 4);
        var bind = rows[0]; var obj = rows[1]; var entered = rows[3]; var rejected = rows[4];
        var own = rows[5]; var parent = rows[6]; var bounds = rows[7]; var selected = rows[8];
        var caught = rows[9]; var resume = rows[10]; var continued = rows[11]; var witness = rows[12];
        var check = rows[13]; var released = rows[14];
        decimal imageEnd = (decimal)image[0] + image[1], method = bounds[0];
        bool ImageRange(ulong at, ulong size) => at >= image[0] && size > 0 && (decimal)at + size <= imageEnd;
        Need(bind[0] == module[3] && bind[1] % 8 == 0 && ImageRange(bind[1], 65536) &&
            bind[2] is >= 288 and < 65536 && bind[3] is > 0 and <= 2048 &&
            bind[2] >= 288 + 12 * (decimal)bind[3], reason + "_snapshot");
        Need(obj[0] % 8 == 0 && obj[0] >= (decimal)heap["retainedStart"] + 8 &&
            (decimal)obj[0] + 16 <= heap["retainedEnd"] && obj[2] == 9 && obj[3] == 3 &&
            obj[1] % 8 == 0 && ImageRange(obj[1], 24), reason + "_original_receipt");
        Need(rows[2].SequenceEqual(new ulong[] { 1, 1, 0, 0 }), reason + "_typed_receipts");
        Need(bounds[1] == 173 && bounds[3] == 11 && ImageRange(bounds[0], bounds[1]) &&
            ImageRange(bounds[2], bounds[3]) && (decimal)own[0] - 9 >= image[0] &&
            (decimal)own[0] + 1 <= imageEnd, reason + "_original_contributions");
        decimal thrower = (decimal)own[0] - 9;
        Need(method + 173 <= thrower || thrower + 10 <= method, reason + "_method_alias");
        Need((decimal)bounds[2] + 11 <= method || method + 173 <= bounds[2], reason + "_eh_code_alias");
        foreach (var (at, length) in new (decimal, decimal)[] { (method, 173), (thrower, 10), (bounds[2], 11) })
            Need(at + length <= bind[1] || (decimal)bind[1] + 65536 <= at, reason + "_snapshot_alias");
        Need(rejected.SequenceEqual(new[] { own[0], own[0] ^ 1UL, own[1], 0UL }), reason + "_local_rejection");
        Need(own[2] == method + 115 && own[3] == (decimal)own[1] + 48 &&
            parent[0] == own[2] && parent[1] == own[3] && parent[2] == method + 61 &&
            parent[3] == (decimal)own[1] + 96 && selected[0] % 8 == 0 && ImageRange(selected[0], 24) &&
            selected[1] == method + 121 && selected[2] == (decimal)parent[3] + 64 && selected[3] == 0 &&
            resume.SequenceEqual(new[] { bounds[0] + 62, parent[3], selected[2], 1UL }), reason + "_compiled_parent_association");
        foreach (var (at, length) in new (decimal, decimal)[] { ((decimal)own[1] - 848, 848), (parent[3], 88), (check[1], 8) })
            Need(at % 8 == 0 && at >= (decimal)stack[0] + 16400 && at + length <= stack[2], reason + "_stack_range");
        ulong seed = (module[3] ^ heap["backing"]) & 0xffff;
        Need(entered.SequenceEqual(new[] { obj[0], selected[2] - 8, seed + 2, 1UL }) &&
            caught.SequenceEqual(new[] { obj[0], seed, seed + 2, 1UL }) &&
            continued.SequenceEqual(new[] { obj[0], seed, seed + 1, 1UL }) &&
            witness.SequenceEqual(new[] { entered[1], seed + 2, 1UL, 1UL }), reason + "_exactly_once_execution");
        Need(check[0] == 7 && check[2] == canaries[1] && check[3] == canaries[2] &&
            released.SequenceEqual(new[] { obj[0], obj[1], module[3], obj[2] }), reason + "_retained_execution_identity");
    }
}
