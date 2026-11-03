using OS.Tooling;

internal static class EfiExceptionTranscriptControls
{
    internal static void Run(bool finallyProfile)
    {
        string name = finallyProfile ? "single-finally" : "simple-catch";
        string prefix = finallyProfile ? "M04_FINALLY" : "M04_CATCH";
        string[] ancestor = M04GCStaticRootMaterializationEfiIntegrationTests.Lines;
        int gc = Array.FindIndex(ancestor, x => x.StartsWith("M04_GC_ROOT ", StringComparison.Ordinal));
        int insert = Array.IndexOf(ancestor, "TEST_START boot.sequence");
        ulong[] image = EfiTranscriptControls.Fields(ancestor[gc]), module = EfiTranscriptControls.Fields(ancestor[gc + 1]),
            stack = EfiTranscriptControls.Fields(ancestor[gc + 2]), old = EfiTranscriptControls.Fields(ancestor[gc + 4]),
            checkedRoot = EfiTranscriptControls.Fields(ancestor[gc + 7]);
        ulong heap = old[0] - 32, seed = (module[3] ^ heap) & 0xFFFF, sp = stack[1] - 1024, rip = image[0] + 4105;
        ulong catcher = image[0] + 8192, parent = sp + 48, rbp = parent + 64, stamp = rbp - 24;
        int[] stages = finallyProfile ? [0, 1, 2, 3, 4, 9, 10, 5, 6, 7, 8] : Enumerable.Range(0, 9).ToArray();
        Dictionary<int, ulong[]> rows = finallyProfile ? new()
        {
            [0] = [module[3], image[0] + 65536, 512, 3],
            [1] = [old[0], old[1] + 64, 9, 3],
            [2] = [rip, rip ^ 1UL, sp, 0],
            [3] = [rip, sp, catcher + 32, parent],
            [4] = [old[1] + 32, catcher + 94, rbp, sp + 40],
            [9] = [old[0], stamp, seed + 2, 1],
            [10] = [catcher + 71, catcher + 94, stamp, seed + 2],
            [5] = [old[0], seed, seed + 2, 1],
            [6] = [catcher + 47, parent, rbp, 1],
            [7] = [old[0], seed, seed + 1, 1],
            [8] = [8, sp - 4096, checkedRoot[1], checkedRoot[2]]
        } : new()
        {
            [0] = [module[3], image[0] + 65536, 512, 3],
            [1] = [old[0], old[1] + 64, 9, 3],
            [2] = [rip, rip ^ 1UL, sp, 0],
            [3] = [rip, sp, image[0] + 8218, sp + 48],
            [4] = [old[1] + 32, image[0] + 8243, sp + 96, sp + 40],
            [5] = [old[0], seed, image[0] + 8243, 1],
            [6] = [image[0] + 8219, sp + 48, sp + 96, 1],
            [7] = [old[0], seed, seed + 1, 1],
            [8] = [6, sp - 4096, checkedRoot[1], checkedRoot[2]]
        };
        string[] child = ["TEST_START m04.021." + name, ..stages.Select(stage => EfiTranscriptControls.Row(prefix, stage, rows[stage])),
            "TEST_PASS m04.021." + name, finallyProfile ? "CLROS_M04_SINGLE_FINALLY_READY" : "CLROS_M04_SIMPLE_CATCH_READY"];
        string[] lines = [.. ancestor[..insert], .. child, .. ancestor[insert..]];
        string good = EfiTranscriptControls.Text(lines);
        var controls = new EfiTranscriptControls(Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", name + "-transcript", Guid.NewGuid().ToString("N")),
            path => QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(path, includeGcStatics: true, includeSimpleCatch: !finallyProfile, includeSingleFinally: finallyProfile));
        controls.Check(good, true, "original measured vector");
        controls.Structure(lines, insert, child.Length);
        void Mutate(int stage, int field, ulong value, string reason)
        {
            string[] bad = [.. lines];
            ulong[] values = [.. rows[stage]];
            values[field] = value;
            bad[insert + 1 + Array.IndexOf(stages, stage)] = EfiTranscriptControls.Row(prefix, stage, values);
            controls.Check(bad, false, reason);
        }
        foreach (int stage in stages)
            foreach (int field in Enumerable.Range(0, 4))
                if (finallyProfile || !(stage == 3 && field is 0 or 2 || stage == 4 && field == 0))
                    Mutate(stage, field, ulong.MaxValue, "numeric " + stage + ":" + field);
        foreach (ulong wrongRip in new[] { rip, rip ^ 2UL })
            Mutate(2, 1, wrongRip, "invalid local rejection RIP");
        if (!finallyProfile)
            Mutate(2, 3, 1, "invalid local rejection admitted");
        else
        {
            foreach (int offset in Enumerable.Range(5, 6))
            {
                string[] bad = [.. lines];
                (bad[insert + offset], bad[insert + offset + 1]) = (bad[insert + offset + 1], bad[insert + offset]);
                controls.Check(bad, false, "ordered stage swap " + offset);
            }
            controls.Check("M04_\tFINALLY 9\r\n" + good, false, "disguised preamble");
            controls.Check(good.Replace("M04_FINALLY 9 ", "M04_FINALLY\t9 ", StringComparison.Ordinal), false, "disguised record");
            controls.Check(good.Replace("TEST_START m04.021.single-finally", "TEST_START m04.021.simple-catch", StringComparison.Ordinal), false, "sibling record");
        }
        foreach (string tail in finallyProfile ? new[] { "\r\n", "noise\r\n", "TEST_FAIL m04.021.single-finally 1\r\n", "T E S T_\tFAIL bad\r\n" }
            : new[] { "\r\n", "TEST_FAIL m04.021.simple-catch 1\r\n" })
            controls.Check(good + tail, false, "late tail");
        controls.Check(good.Replace("\r\n", "\n", StringComparison.Ordinal), false, "LF framing");
        controls.Check(good.TrimEnd('\n'), false, "incomplete final record");
        if (finallyProfile)
        {
            QemuTranscripts.AssertM04SingleFinallySelection(good, true);
            QemuTranscripts.AssertM04SimpleCatchSelection(good, false, finallyEnabled: true);
            foreach (string claim in new[] { "M04_FINALLY", "CLROS_M04_SINGLE_FINALLY_READY", "m04.\t021.single-finally" })
                Rejected(() => QemuTranscripts.AssertM04SingleFinallySelection(claim, false), "unselected finally claim");
            Rejected(() => QemuTranscripts.AssertM04SimpleCatchSelection("M04_CATCH", false, finallyEnabled: true), "sibling collision");
        }
        else
        {
            QemuTranscripts.AssertM04SimpleCatchSelection(good, true);
            foreach (string claim in new[] { "M04_CATCH", "CLROS_M04_SIMPLE_CATCH_READY", "m04.\t021" })
                Rejected(() => QemuTranscripts.AssertM04SimpleCatchSelection(claim, false), "unselected catch claim");
        }
        controls.Complete(name);
    }

    private static void Rejected(Action action, string reason)
    {
        bool accepted = true;
        try
        {
            action();
        }
        catch (ToolFailure)
        {
            accepted = false;
        }
        HostCheck.True(!accepted, reason);
    }
}
