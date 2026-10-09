using OS.Tooling;

internal static class EfiConstructorTranscriptControls
{
    internal static ulong[][] Rows(ulong handle, ulong context, ulong seed, ulong target, ulong root) =>
    [
        [handle, context, 8192, seed], [1, 1, seed + 11, seed + 11], [seed + 21, seed + 21, 1, 1],
        [seed + 31, seed + 32, seed + 31, 1], [seed + 41, seed + 42, seed + 43, 2],
        [seed + 51, seed + 52, seed + 51, 2], [9, 1, target, target + 8], [1, 7, 0, 8], [5, 3, 0, root]
    ];
    internal static string[] Records(ulong[][] rows) => ["TEST_START m04.024",
        ..rows.Select((v, i) => EfiTranscriptControls.Row("M04_CCTOR", i, v)), "CLROS_M04_024_READY", "TEST_PASS m04.024"];

    internal static void Run()
    {
        string[] gc = [EfiTranscriptControls.Row("M04_GC_ROOT", 0, 4096, 65536, 0, 0),
            EfiTranscriptControls.Row("M04_GC_ROOT", 1, 1, 16384, 4, 3), "",
            EfiTranscriptControls.Row("M04_GC_ROOT", 3, 20480, 131080, 131088, 24)];
        ulong[][] values = Rows(3, 32768, 3UL ^ 131072UL, 6144, 131080);
        string[] good = Records(values);
        var heap = new Dictionary<string, ulong> { ["retainedStart"] = 131072, ["backing"] = 131072 };
        int count = 0;
        void Check(string[] lines, bool accept)
        {
            bool accepted = true;
            try { QemuTranscripts.AssertM04ClassConstructorRecords(lines, gc, heap); }
            catch (ToolFailure) when (!accept) { accepted = false; }
            HostCheck.Equal(accept, accepted); count++;
        }
        Check(good, true);
        ulong[][] shifted = values.Select(x => x.ToArray()).ToArray(); shifted[0][3]++;
        foreach ((int row, int field) in new[] { (1, 2), (1, 3), (2, 0), (2, 1), (3, 0), (3, 1), (3, 2), (4, 0), (4, 1), (4, 2), (5, 0), (5, 1), (5, 2) }) shifted[row][field]++;
        Check(Records(shifted), false);
        for (int i = 0; i < good.Length; i++)
        {
            Check(good.Where((_, index) => index != i).ToArray(), false);
            string[] bad = [.. good]; bad[i] += " "; Check(bad, false);
        }
        foreach ((int row, int field, ulong value) in new (int, int, ulong)[]
        { (1, 0, 2), (3, 0, 130), (4, 0, 140), (6, 3, 6160), (7, 1, 6), (8, 0, 0), (8, 1, 0), (8, 2, 1), (8, 3, 131088) })
        {
            ulong[] mutated = [.. values[row]]; mutated[field] = value;
            string[] bad = [.. good]; bad[row + 1] = EfiTranscriptControls.Row("M04_CCTOR", row, mutated); Check(bad, false);
        }
        Console.WriteLine("TRANSCRIPT_CONTROLS cctor cases=" + count);
    }
}
