namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public const string UnhandledFailure = UnhandledExceptionCapture.Failure;
    public const string UnhandledTerminal = UnhandledExceptionCapture.Terminal;
    public const int UnhandledRecordCount = UnhandledExceptionCapture.RecordCount;

    // 0 incomplete, 1 exact expected terminal, -1 rejected. Called only by the
    // explicitly selected unhandled runner; generic failure rules remain intact.
    public static int EvaluateM04UnhandledCapture(string text, out string prerequisites)
    {
        prerequisites = "";
        int framing = UnhandledExceptionCapture.Evaluate(text, out prerequisites);
        if (framing != 1)
            return framing;
        try
        {
            AssertManagedHeapText(text, includeGcStatics: true, includeUnhandled: true);
        }
        catch (ToolFailure)
        {
            prerequisites = "";
            return -1;
        }
        return 1;
    }

    public static void AssertM04UnhandledRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines,
        IReadOnlyDictionary<string, ulong> heap)
    {
        const string reason = "m04_unhandled";
        Need(lines.Count == 11 && gcLines.Count == 17 && lines[0] == "TEST_START m04.021.unhandled" &&
            lines[9] == UnhandledFailure && lines[10] == UnhandledTerminal, reason + "_count_or_terminal");
        ulong[][] r = Enumerable.Range(0, 8).Select(i => ReadHexRecord(lines[i + 1], "M04_UNHANDLED " + i, 4)).ToArray();
        var image = ReadHexRecord(gcLines[0], "M04_GC_ROOT 0", 4);
        var module = ReadHexRecord(gcLines[1], "M04_GC_ROOT 1", 4);
        var stack = ReadHexRecord(gcLines[2], "M04_GC_ROOT 7", 4);
        var canaries = ReadHexRecord(gcLines[7], "M04_GC_ROOT 8", 4);
        decimal end = (decimal)image[0] + image[1];
        var bind = r[0];
        var obj = r[1];
        var first = r[3];
        var second = r[4];
        var clause = r[5];
        var check = r[7];
        Need(bind[0] == module[3] && bind[1] % 8 == 0 && bind[1] >= image[0] && (decimal)bind[1] + 65536 <= end &&
            bind[2] is >= 288 and < 65536 && bind[3] is > 0 and <= 2048 && bind[2] >= 288 + 12 * (decimal)bind[3], reason + "_snapshot");
        Need(obj[0] % 8 == 0 && obj[0] >= (decimal)heap["retainedStart"] + 8 && (decimal)obj[0] + 16 <= heap["retainedEnd"] &&
            obj[2] == 9 && obj[3] == 3 && obj[1] != clause[0], reason + "_retained_object");
        foreach (ulong type in new[] { obj[1], clause[0] })
            Need(type % 8 == 0 && type >= image[0] && (decimal)type + 24 <= end, reason + "_type_range");
        Need(r[2].SequenceEqual(new ulong[] { 1, 1, 0, 0 }), reason + "_exported_predicate");
        Need(first[0] >= (decimal)image[0] + 9 && (decimal)first[0] + 1 <= end &&
            first[2] >= (decimal)image[0] + 35 && (decimal)first[2] + 11 <= end &&
            second[2] >= (decimal)image[0] + 15 && (decimal)second[2] + 41 <= end &&
            (first[0] - 9) % 16 == 0 && (first[2] - 35) % 16 == 0 && (second[2] - 15) % 16 == 0 &&
            clause[1] == (decimal)second[2] + 14, reason + "_compiler_code");
        Need(first[3] == (decimal)first[1] + 48 && second[0] == first[2] && second[1] == first[3] &&
            second[3] == (decimal)second[1] + 64 && clause[2] == (decimal)second[3] - 8 && clause[3] == 0, reason + "_physical_frames");
        foreach (var (at, size) in new[] { (first[1], 8UL), (first[3], 8UL), (second[3], 56UL), (check[1], 8UL) })
            Need(at % 8 == 0 && at >= (decimal)stack[0] + 16400 && (decimal)at + size <= stack[2], reason + "_stack_range");
        Need(r[6].SequenceEqual(new ulong[] { 3, 10, 0, 0 }), reason + "_no_handler_or_cleanup");
        Need(check[0] == 4 && check[1] <= first[1] && check[2] == canaries[1] && check[3] == canaries[2], reason + "_stack_witness");
    }
}
