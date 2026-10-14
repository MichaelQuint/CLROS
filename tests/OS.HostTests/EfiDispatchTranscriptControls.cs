using OS.Tooling;

internal static class EfiDispatchTranscriptControls
{
    internal static void Run()
    {
        string[] baseline = M04GCStaticRootMaterializationEfiIntegrationTests.Lines;
        int gc = Array.FindIndex(baseline, x => x.StartsWith("M04_GC_ROOT ", StringComparison.Ordinal));
        ulong[] gcImage = EfiTranscriptControls.Fields(baseline[gc]), module = EfiTranscriptControls.Fields(baseline[gc + 1]),
            stack = EfiTranscriptControls.Fields(baseline[gc + 2]), gcRoot = EfiTranscriptControls.Fields(baseline[gc + 3]);
        ulong image = gcImage[0], heap = gcRoot[1] - 8, handle = module[3], seed = ((handle ^ heap ^ stack[2]) & 65535) + 1;
        string[] cctor = EfiConstructorTranscriptControls.Records(EfiConstructorTranscriptControls.Rows(handle, image + 0x90000, handle ^ heap, image + 0x4000, gcRoot[1]));
        int cctorAt = gc + 3;
        string[] parent = [.. baseline[..cctorAt], .. cctor, .. baseline[cctorAt..]];
        ulong[][] rows = new ulong[73][];
        rows[0] = [handle, image + 0x70000, 32768, seed]; rows[1] = [image + 0x10000, 80, image + 0x18000, 5];
        rows[2] = [heap + 64, heap + 88, image + 0x5100, image + 0x5200]; rows[3] = [9, 10, 24, 24];
        const ulong tag = 0xC011EC7000000006;
        rows[4] = [gcRoot[1], heap + 32, tag, handle - 272]; rows[5] = [stack[0], stack[1], stack[2], stack[3] - 512];
        rows[6] = [image + 0x5000, image + 0x6000, 4, 1]; rows[7] = [rows[2][2], 0, 0, 4];
        rows[8] = [rows[2][3], 0, 0, 4]; rows[9] = [7, 2, 10, 176];
        for (int i = 0; i < 15; i++)
        {
            int op = i / 3, second = i % 3 == 1 ? 1 : 0, row = 10 + 4 * i;
            ulong requested = image + 0xA000 + (ulong)op * 128;
            ulong implemented = op is 2 or 3 && second == 0 ? image + 0xB000 + (ulong)op * 128 : requested;
            ulong owner = op <= 1 ? rows[6][0] : rows[2][2 + second];
            ulong target = image + 0x20000 + (ulong)op * 256 + (op == 0 ? 0UL : (ulong)second * 16);
            rows[row] = [rows[2][second], rows[1][0] + new ulong[] { 0, 16, 48, 64, 32 }[op], target, (ulong)(9 + second)];
            rows[row + 1] = [rows[2][2 + second], requested, implemented, owner];
            ulong slot = new ulong[] { 1, 0, 1, 2, 3 }[op], interfaceSlot = op is 2 or 3 ? 1UL : 0;
            rows[row + 2] = [interfaceSlot, slot, i % 3 == 2 ? 1UL : 0, (ulong)i + 1];
            ulong a = seed + (ulong)i + 1, d = a + 3, offset = op == 0 ? 100UL : (ulong)(op * 200 + 100 * second);
            ulong expected = seed + 11 + 11 * (ulong)second + offset + a + 3 * (a + 1) + 5 * (a + 2) + 7 * d;
            rows[row + 3] = [expected, expected, (ulong)op, d];
        }
        rows[70] = [15, 10, 5, 128];
        ulong[] canary = EfiTranscriptControls.Fields(baseline[Array.FindIndex(baseline, x => x.StartsWith("M04_COLLECT 8 ", StringComparison.Ordinal))]);
        rows[71] = [7, 7, gcRoot[1], tag]; rows[72] = [17, canary[1], canary[2], stack[3] - 768];
        string[] block = ["TEST_START m04.016", .. rows.Select((v, i) => EfiTranscriptControls.Row("M04_DISPATCH", i, v)), "CLROS_M04_016_READY", "TEST_PASS m04.016"];
        int insert = Array.IndexOf(parent, "TEST_START boot.sequence");
        string[] good = [.. parent[..insert], .. block, .. parent[insert..]];
        var controls = new EfiTranscriptControls(Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts/issue572-dispatch-tests", Guid.NewGuid().ToString("N")),
            path => QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(path, includeGcStatics: true, includeClassConstructors: true, includeInterfaceDispatch: true));
        controls.Check(good, true, "original dispatch oracle");
        string[] reorderedCells = [.. good];
        for (int i = 0; i < 15; i++)
        {
            int row = 10 + 4 * i;
            ulong[] call = [.. rows[row]];
            call[1] = rows[1][0] + (ulong)(4 - i / 3) * 16;
            reorderedCells[insert + 1 + row] = EfiTranscriptControls.Row("M04_DISPATCH", row, call);
        }
        controls.Check(reorderedCells, true, "compiler cell order is independent of callsite identity");
        ulong shallower = canary[3] + 8;
        HostCheck.True(shallower <= stack[3], "distinct phase minima control");
        string[] differentFrames = [.. good]; ulong[] first = [.. rows[5]], last = [.. rows[72]];
        first[3] = last[3] = shallower;
        differentFrames[insert + 6] = EfiTranscriptControls.Row("M04_DISPATCH", 5, first);
        differentFrames[insert + 73] = EfiTranscriptControls.Row("M04_DISPATCH", 72, last);
        controls.Check(differentFrames, true, "independent valid phase minima");
        foreach (int i in Enumerable.Range(insert, block.Length))
        {
            controls.Check(good.Where((_, index) => index != i).ToArray(), false, "missing row " + i);
            controls.Check(good.Take(i + 1).Concat(good.Skip(i)).ToArray(), false, "duplicate row " + i);
            string[] swapped = [.. good]; (swapped[i], swapped[i + 1]) = (swapped[i + 1], swapped[i]);
            controls.Check(swapped, false, "swapped row " + i);
            controls.Check(good[..(i + 1)], false, "truncated row " + i);
        }
        foreach (int i in Enumerable.Range(insert + 1, rows.Length))
        { string[] bad = [.. good]; bad[i] += " "; controls.Check(bad, false, "record shape " + i); }
        void Mutate(int stage, params (int Field, ulong Value)[] fields)
        {
            string[] bad = [.. good]; ulong[] values = [.. rows[stage]];
            foreach (var field in fields) values[field.Field] = field.Value;
            bad[insert + 1 + stage] = EfiTranscriptControls.Row("M04_DISPATCH", stage, values);
            controls.Check(bad, false, "numeric stage " + stage);
        }
        foreach ((int row, int field, ulong value) in new (int, int, ulong)[]
        {
            (0,0,handle+8),(0,3,seed+1),(1,0,heap),(2,0,gcRoot[1]),(3,0,8),(4,1,heap+56),(4,3,image),
            (5,0,stack[0]+8),(6,2,0),(7,0,rows[6][0]),(9,0,8),(10,0,rows[2][1]),(10,1,rows[1][0]+1),
            (10,2,0),(10,3,10),(11,2,image+0xB000),(11,3,rows[2][2]),(12,0,1),(12,1,0),(12,2,1),
            (12,3,2),(13,3,0),(18,1,rows[1][0]+16),(70,0,14),(70,1,9),(70,2,4),(70,3,1025),
            (71,1,6),(72,0,16),(72,1,0),(72,3,stack[2]),
            (1,1,96),(1,1,112),(1,3,6),(7,1,image+0x6100),(8,2,4),(34,1,rows[1][0]+80),(36,0,0),(48,0,0),
            (60,1,4),(14,2,rows[10][2]+16)
        }) Mutate(row, (field, value));
        Mutate(4, (3, rows[4][3] + 8));
        Mutate(13, (0, rows[13][0] + 1), (1, rows[13][1] + 1));
        controls.Check(string.Join("\r\n", good), false, "missing EOF delimiter");
        controls.Check(EfiTranscriptControls.Text(good) + "\r\n", false, "extra delimiter");
        controls.Check([.. good, "M04_DISPATCH 255 0 0 0 0"], false, "late failure");
        controls.Check([.. good, "noise"], false, "late noise");
        foreach (int i in Enumerable.Range(insert + block.Length, parent.Length - insert))
            controls.Check(good[..i], false, "final parent rows retained " + i);
        foreach (string claim in new[] { "M04_DISPATCH 0", "CLROS_M04_016_READY", "TEST_START m04.016", "TEST_PASS m04.016", "M04_DIS\0PATCH 0", "test_pass M04.016" })
        {
            bool accepted = true;
            try { QemuTranscripts.AssertM04InterfaceDispatchSelection(claim, false); }
            catch (ToolFailure) { accepted = false; }
            HostCheck.True(!accepted, "unselected dispatch claim");
            QemuTranscripts.AssertM04InterfaceDispatchSelection(claim, true);
        }
        QemuTranscripts.AssertM04InterfaceDispatchSelection("POST_FIRMWARE_DIAGNOSTICS_READY", false);
        controls.Complete("dispatch");
    }
}
