namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM04RethrowRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines, IReadOnlyDictionary<string, ulong> heap)
    {
        Need(lines.Count == 18 && lines[0] == "TEST_START m04.021.rethrow" && lines[16] == "TEST_PASS m04.021.rethrow" && lines[17] == "CLROS_M04_RETHROW_READY", "m04_rethrow_count");
        int[] stages = [0, 1, 2, 3, 4, 9, 10, 5, 14, 11, 12, 13, 6, 7, 8];
        var rows = stages.Select((stage, i) => (stage, values: ReadHexRecord(lines[i + 1], "M04_RETHROW " + stage, 4))).ToDictionary(p => p.stage, p => p.values);
        var image = ReadHexRecord(gcLines[0], "M04_GC_ROOT 0", 4);
        var module = ReadHexRecord(gcLines[1], "M04_GC_ROOT 1", 4);
        var stack = ReadHexRecord(gcLines[2], "M04_GC_ROOT 7", 4);
        var canaries = ReadHexRecord(gcLines[7], "M04_GC_ROOT 8", 4);
        var bind = rows[0];
        var obj = rows[1];
        var rejected = rows[2];
        var frames = rows[3];
        var match = rows[4];
        var cleanup = rows[9];
        var validated = rows[10];
        var caught = rows[5];
        var active = rows[14];
        var outerFrame = rows[11];
        var outerMatch = rows[12];
        var outerCatch = rows[13];
        var resume = rows[6];
        var continued = rows[7];
        var check = rows[8];
        decimal end = (decimal)image[0] + image[1], inner = (decimal)frames[2] - 25, outer = (decimal)outerFrame[2] - 26;
        Need(bind[0] == module[3] && bind[1] % 8 == 0 && bind[1] >= image[0] && (decimal)bind[1] + 65536 <= end &&
            bind[2] is >= 288 and < 65536 && bind[3] is > 0 and <= 2048 && bind[2] >= 288 + 12 * (decimal)bind[3], "m04_rethrow_snapshot");
        Need(obj[0] % 8 == 0 && obj[0] >= (decimal)heap["retainedStart"] + 8 && (decimal)obj[0] + 16 <= heap["retainedEnd"] &&
            obj[2] == 9 && obj[3] == 3 && obj[1] != match[0] && outerMatch[0] == match[0], "m04_rethrow_original_receipt");
        foreach (ulong at in new[] { obj[1], match[0] })
            Need(at % 8 == 0 && at >= image[0] && (decimal)at + 24 <= end, "m04_rethrow_type_range");
        Need((decimal)frames[0] - 9 >= image[0] && (decimal)frames[0] + 1 <= end && inner >= image[0] && inner + 88 <= end &&
            outer >= image[0] && outer + 89 <= end && (inner >= outer + 89 || outer >= inner + 88), "m04_rethrow_code_range");
        Need(rejected[0] == frames[0] && rejected[1] == (frames[0] ^ 1UL) && rejected[2] == frames[1] && rejected[3] == 0, "m04_rethrow_local_admission");
        Need(frames[3] == (decimal)frames[1] + 48 && match[2] == (decimal)frames[3] + 48 && match[3] == (decimal)frames[1] + 40 && match[1] == inner + 69 &&
            outerFrame[0] == frames[2] && outerFrame[1] == frames[3] && outerFrame[3] == (decimal)frames[3] + 64 && outerMatch[2] == (decimal)outerFrame[3] + 48 &&
            outerMatch[3] == (decimal)frames[3] + 56 && outerMatch[1] == outer + 51 && resume[0] == outer + 27 && resume[1] == outerFrame[3] &&
            resume[2] == outerMatch[2] && resume[3] == 1, "m04_rethrow_native_frames");
        Need(active[0] == inner + 87 && active[2] % 16 == 0 && active[2] >= 1168 && active[1] == (decimal)active[2] - 320 &&
            frames[3] == (decimal)active[2] + 624 && active[3] == obj[0], "m04_rethrow_active_context");
        foreach (var (at, length) in new (decimal, decimal)[] { (frames[1], 8), (frames[3], 72), (outerFrame[3], 72), (check[1], 8), ((decimal)active[2] - 1168, 1736) })
            Need(at % 8 == 0 && at >= (decimal)stack[0] + 16400 && at + length <= stack[2], "m04_rethrow_stack_range");
        ulong seed = (module[3] ^ heap["backing"]) & 0xffff;
        Need(cleanup[0] == obj[0] && cleanup[1] == (decimal)match[2] - 8 && cleanup[2] == seed + 2 && cleanup[3] == 1 &&
            validated[0] == inner + 46 && validated[1] == match[1] && validated[2] == cleanup[1] && validated[3] == cleanup[2] &&
            caught[0] == obj[0] && caught[1] == seed && caught[2] == seed + 2 && caught[3] == 1 &&
            outerCatch[0] == obj[0] && outerCatch[1] == seed && outerCatch[2] == outerMatch[1] && outerCatch[3] == 1 &&
            continued[0] == obj[0] && continued[1] == seed && continued[2] == seed + 1 && continued[3] == 1 &&
            check[0] == 10 && check[2] == canaries[1] && check[3] == canaries[2], "m04_rethrow_execution_identity");
    }
}
