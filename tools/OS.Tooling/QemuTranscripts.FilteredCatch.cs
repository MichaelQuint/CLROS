namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM04FilteredCatchRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines,
        IReadOnlyDictionary<string,ulong> heap)
    {
        const string reason = "m04_filters";
        Need(lines.Count == 18 && lines[0] == "TEST_START m04.021.filters" &&
            lines[16] == "TEST_PASS m04.021.filters" && lines[17] == "CLROS_M04_021_FILTERS_READY",reason+"_count");
        var rows = Enumerable.Range(0,15).Select(stage => ReadHexRecord(lines[stage+1],"M04_FILTERS "+stage,4)).ToArray();
        var image = ReadHexRecord(gcLines[0],"M04_GC_ROOT 0",4);
        var module = ReadHexRecord(gcLines[1],"M04_GC_ROOT 1",4);
        var stack = ReadHexRecord(gcLines[2],"M04_GC_ROOT 7",4);
        var canaries = ReadHexRecord(gcLines[7],"M04_GC_ROOT 8",4);
        decimal imageEnd = (decimal)image[0]+image[1], catcher = (decimal)rows[3][2]-35,
            thrower = (decimal)rows[2][0]-9, parentRsp = rows[3][3], parentRbp = parentRsp+64, local = parentRbp-8;
        ulong seed = (module[3]^heap["backing"]) & 0xffff, original = rows[1][0];
        Need(rows[0][0] == module[3] && rows[0][1]%8 == 0 && rows[0][1] >= image[0] &&
            (decimal)rows[0][1]+65536 <= imageEnd && rows[0][2] is >=288 and <65536 && rows[0][3] is >0 and <=2048 &&
            rows[0][2] >= 288+12*(decimal)rows[0][3],reason+"_snapshot");
        Need(original%8 == 0 && original >= (decimal)heap["retainedStart"]+8 && (decimal)original+16 <= heap["retainedEnd"] &&
            rows[1][1]%8 == 0 && rows[1][1] >= image[0] && (decimal)rows[1][1]+24 <= imageEnd &&
            rows[1][2] == 9 && rows[1][3] == 3,reason+"_rooted_exception");
        Need(thrower >= image[0] && thrower+10 <= imageEnd && catcher >= image[0] && catcher+295 <= imageEnd &&
            (thrower+10 <= catcher || catcher+295 <= thrower) && rows[2][1] == (rows[2][0]^1) && rows[2][3] == 0 &&
            rows[3][0] == rows[2][0] && rows[3][1] == rows[2][2] && parentRsp == (decimal)rows[2][2]+48,
            reason+"_raw_capture_and_parent");
        Need(rows[4].SequenceEqual(new[]{(ulong)(catcher+60),(ulong)(catcher+178),(ulong)(catcher+127),(ulong)(catcher+245)}),
            reason+"_original_filter_and_handler_targets");
        Need(rows[5].SequenceEqual(new[]{original,(ulong)local,seed+1,0UL}) &&
            rows[6].SequenceEqual(new[]{rows[4][0],0UL,1UL,(ulong)parentRsp}) &&
            rows[7].SequenceEqual(new[]{original,(ulong)local,seed+2,1UL}) &&
            rows[8].SequenceEqual(new[]{rows[4][1],1UL,2UL,(ulong)parentRsp}),reason+"_ordered_actual_returns_and_shared_local");
        Need(rows[9].SequenceEqual(new[]{rows[4][3],1UL,(ulong)parentRbp,(ulong)local}) &&
            rows[10].SequenceEqual(new[]{original,seed+2,2UL,1UL}) &&
            rows[11].SequenceEqual(new[]{(ulong)(catcher+36),(ulong)parentRsp,(ulong)parentRbp,1UL}) &&
            rows[12].SequenceEqual(new[]{original,seed,seed+1,1UL}) &&
            rows[14].SequenceEqual(new[]{original,rows[1][1],2UL,1UL}),reason+"_selected_catch_and_compiled_continuation");
        foreach(var (at,bytes) in new (decimal,decimal)[]{((decimal)rows[2][2]-848,848),(parentRsp,88)})
            Need(at%8 == 0 && at >= (decimal)stack[0]+16400 && at+bytes <= stack[2],reason+"_stack_ranges");
        Need(rows[13][0] == 15 && rows[13][1]%8 == 0 && rows[13][1] >= (decimal)stack[0]+16 &&
            rows[13][1] < (decimal)rows[2][2]-848 && rows[13][2] == canaries[1] && rows[13][3] == canaries[2],reason+"_canaries");
    }
}
