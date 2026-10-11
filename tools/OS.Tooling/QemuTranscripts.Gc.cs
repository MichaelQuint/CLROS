namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM04GcStaticRootRecords(IReadOnlyList<string> lines, IReadOnlyDictionary<string, ulong> heap)
    {
        Need(lines.Count == 17, "m04_gc_static_record_count");
        int[] stages = [0, 1, 7, 3, 4, 5, 6, 8];
        ulong[][] r = stages.Select((stage, i) => ReadHexRecord(lines[i], "M04_GC_ROOT " + stage, 4)).ToArray();
        var image = r[0]; var module = r[1]; var stack = r[2]; var root = r[3]; var value = r[4]; var stored = r[5]; var cleared = r[6]; var check = r[7];
        decimal imageEnd = (decimal)image[0] + image[1];
        Need(image[0] != 0 && image[0] % 4096 == 0 && image[1] != 0 && imageEnd <= ulong.MaxValue && image[2] == 0 && image[3] == 0 && module[0] == 1 && module[2] == 4 && module[3] != 0, "m04_gc_static_image_module");
        foreach (var (at, length, alignment) in new[] { (module[1], 4UL, 4UL), (root[0], 8UL, 8UL), (value[1], 24UL, 8UL) })
            Need(at % alignment == 0 && at >= image[0] && (decimal)at + length <= imageEnd, "m04_gc_static_image_range");
        Need(root[3] == 24 && root[1] == (decimal)heap["retainedStart"] + 8 && root[2] == (decimal)root[1] + 8 && value[0] == (decimal)heap["retainedStart"] + 32 && value[2] == 24 && value[3] == 48 && (decimal)heap["retainedStart"] + 48 <= heap["retainedEnd"], "m04_gc_static_allocations");
        Need(stored[0] == 1 && stored[1] == 1 && stored[2] == value[0] && stored[3] == 0x7143B6A952D08E2F && cleared[0] == 1 && cleared[1] == 2 && cleared[2] == 2 && cleared[3] == 48, "m04_gc_static_store_readback");
        Need(stack[0] != 0 && stack[0] % 4096 == 0 && (decimal)stack[1] - stack[0] == 135168 && stack[2] == (decimal)stack[1] - 40 && check[0] == 5 && check[1] == 0x195A86B4C327D0EF && check[2] == 0x62C79E1035AB48D6 && check[3] <= stack[3], "m04_gc_static_stack_identity");
        foreach (ulong witness in new[] { stack[3], check[3] }) Need(witness % 8 == 0 && witness >= (decimal)stack[0] + 16400 && (decimal)witness + 8 <= stack[2], "m04_gc_static_stack_witness");
        foreach (var (start, end) in new[] { ((decimal)image[0], imageEnd), ((decimal)heap["retainedStart"], (decimal)heap["retainedEnd"]) })
            Need(stack[0] >= end || start >= stack[1], "m04_gc_static_stack_overlap");
        ulong[][] c = Enumerable.Range(0, 9).Select(i => ReadHexRecord(lines[8 + i], "M04_COLLECT " + i, 4)).ToArray();
        var map = c[0]; var nodes = c[1]; var reuse = c[3]; var final = c[7]; var last = c[8];
        Need(map[0] % 8 == 0 && map[0] >= image[0] && (decimal)map[0] + 24 <= imageEnd && map[0] != value[1] && map[1] == 32 && map[2] == 8 && map[3] == 8, "m04_collector_type");
        for (int i = 0; i < 4; i++) Need(nodes[i] == (decimal)heap["retainedStart"] + 56 + 32 * i, "m04_collector_graph");
        foreach (ulong[] expected in new ulong[][] { [2, 1, 4, 2, 64], [4, 1, 2, 3, 88], [5, 1, 1, 1, 32], [6, 1, 2, 0, 0] })
            for (int j = 0; j < 4; j++) Need(c[expected[0]][j] == expected[j + 1], "m04_collector_collection");
        Need(reuse[0] == nodes[2] && reuse[1] == reuse[0] && reuse[2] == 5 && reuse[3] == 7 && final[0] == value[0] && final[1] == 176 && final[2] == 8 && final[3] == 7 && (decimal)heap["retainedStart"] + 176 <= heap["retainedEnd"], "m04_collector_reuse");
        Need(last[0] == 10 && last[1] == check[1] && last[2] == check[2] && last[3] % 8 == 0 && last[3] <= check[3] && last[3] >= (decimal)stack[0] + 16400 && (decimal)last[3] + 8 <= stack[2], "m04_collector_stack");
    }

    public static void AssertM04SeededCollectorChurnRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines)
    {
        Need(lines.Count == 12 && gcLines.Count == 17 && lines[11] == "CLROS_M04_014_READY", "m04_churn_record_count_or_ready");
        var stack = ReadHexRecord(gcLines[2], "M04_GC_ROOT 7", 4); var root = ReadHexRecord(gcLines[3], "M04_GC_ROOT 3", 4);
        var plain = ReadHexRecord(gcLines[4], "M04_GC_ROOT 4", 4); var final = ReadHexRecord(gcLines[15], "M04_COLLECT 7", 4); var check = ReadHexRecord(gcLines[16], "M04_COLLECT 8", 4);
        const ulong tag = 0xC011EC7000000006;
        var records = Enumerable.Range(0, 11).Select(i => ReadHexRecord(lines[i], "M04_CHURN " + i, 4)).ToArray();
        Need(records[0][0] == plain[1] && records[0][1] == root[1] && records[0][2] == final[0] && records[0][3] == tag, "m04_churn_original_identity");
        ulong[] seeds = [0, 1, 0xDEADBEEF], allocations = [161, 154, 161], reuses = [103, 103, 106], fingerprints = [0x6159605787538563, 0xF6D01A7B41E92C30, 0x747C5A6156456DA2];
        decimal identity = final[2]; ulong minimumWitness = check[3];
        for (int i = 0; i < seeds.Length; i++)
        {
            var counts = records[1 + i * 3]; var collection = records[2 + i * 3]; var retained = records[3 + i * 3];
            Need(counts[0] == seeds[i] && counts[1] == 64 && counts[2] == allocations[i] && counts[3] == allocations[i] && collection[0] == 2 && collection[1] == 66 && collection[2] == reuses[i] && collection[3] == fingerprints[i], "m04_churn_seed_accounting");
            identity += allocations[i];
            Need(identity <= ulong.MaxValue && retained[0] == identity && retained[1] == final[0] && retained[2] == tag, "m04_churn_retention");
            Need(retained[3] % 8 == 0 && retained[3] <= minimumWitness && retained[3] >= (decimal)stack[0] + 16400 && (decimal)retained[3] + 8 <= stack[2], "m04_churn_stack_witness"); minimumWitness = retained[3];
        }
        var last = records[10];
        Need(last[0] == 4 && last[1] == check[1] && last[2] == check[2] && last[3] == minimumWitness, "m04_churn_stack_canaries");
    }

    public static void AssertM04SimpleCatchRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines, IReadOnlyDictionary<string, ulong> heap) => AssertCatchRecords(lines, gcLines, heap, false);
    public static void AssertM04NestedFinallyRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines, IReadOnlyDictionary<string,ulong> heap)
    {
        const string reason="m04_nested_finally";
        Need(lines.Count==19 && lines[0]=="TEST_START m04.021.nested-finally" &&
            lines[17]=="TEST_PASS m04.021.nested-finally" && lines[18]=="CLROS_M04_NESTED_FINALLY_READY",reason+"_count");
        int[] stages=[0,1,2,14,3,4,5,6,7,8,9,10,11,12,13,15];
        var rows=stages.Select((stage,i)=>(stage,values:ReadHexRecord(lines[i+1],"M04_NESTED "+stage,4))).ToDictionary(p=>p.stage,p=>p.values);
        var image=ReadHexRecord(gcLines[0],"M04_GC_ROOT 0",4); var module=ReadHexRecord(gcLines[1],"M04_GC_ROOT 1",4);
        var stack=ReadHexRecord(gcLines[2],"M04_GC_ROOT 7",4); var canaries=ReadHexRecord(gcLines[7],"M04_GC_ROOT 8",4);
        var bind=rows[0];var obj=rows[1];var rejected=rows[2];var frames=rows[3];var match=rows[4];var plan=rows[5];
        var caught=rows[10];var resume=rows[11];var continued=rows[12];var check=rows[13];var last=rows[15];
        decimal end=(decimal)image[0]+image[1], method=(decimal)frames[2]-32;
        Need(bind[0]==module[3] && bind[1]%8==0 && bind[1]>=image[0] && (decimal)bind[1]+65536<=end &&
            bind[2] is >=288 and <65536 && bind[3] is >0 and <=2048 && bind[2]>=288+12*(decimal)bind[3],reason+"_snapshot");
        Need(obj[0]%8==0 && obj[0]>=(decimal)heap["retainedStart"]+8 && (decimal)obj[0]+16<=heap["retainedEnd"] &&
            obj[2]==9 && obj[3]==3 && obj[1]%8==0 && obj[1]>=image[0] && (decimal)obj[1]+24<=end,reason+"_original_receipt");
        Need(method>=image[0] && method+222<=end && (decimal)frames[0]-9>=image[0] && (decimal)frames[0]+1<=end,reason+"_code_range");
        Need(rejected[0]==frames[0] && rejected[1]==(frames[0]^1UL) && rejected[2]==frames[1] && rejected[3]==0 &&
            rows[14][0]==(ulong)OS.Kernel.Runtime.BoundedExceptionDispatchStatus.InvalidClause && rows[14].Skip(1).All(v=>v==0),reason+"_local_rejection");
        Need(match.SequenceEqual(new ulong[]{1,0,1,1}),reason+"_typed_search");
        Need(frames[3]==(decimal)frames[1]+48 && plan[0]==method+101 && plan[1]==method+130 &&
            plan[2]==method+180 && plan[3]==(decimal)frames[3]+40 && resume[0]==method+73 && resume[1]==frames[3] &&
            resume[2]==(decimal)frames[3]+64 && resume[3]==1,reason+"_compiled_frame_association");
        foreach(var (at,length) in new (decimal,decimal)[]{((decimal)frames[1]-864,864),(frames[3],88),(check[1],8)})
            Need(at%8==0 && at>=(decimal)stack[0]+16400 && at+length<=stack[2],reason+"_stack_range");
        ulong seed=(module[3]^heap["backing"])&0xffff;
        for(int ordinal=1;ordinal<=2;ordinal++)
        {
            var cleanup=rows[ordinal==1?6:8];var validated=rows[ordinal==1?7:9];
            Need(cleanup[0]==obj[0] && cleanup[1]==plan[3] && cleanup[2]==seed+1+(ulong)ordinal && cleanup[3]==(ulong)ordinal &&
                validated[0]==plan[0] && validated[1]==plan[1] && validated[2]==cleanup[1] && validated[3]==cleanup[2],reason+"_cleanup_order");
        }
        Need(caught.SequenceEqual(new[]{obj[0],seed,seed+3,1UL}) && continued.SequenceEqual(new[]{obj[0],seed,seed+1,1UL}) &&
            check[0]==10 && check[2]==canaries[1] && check[3]==canaries[2] && last.SequenceEqual(new[]{obj[0],obj[1],module[3],obj[2]}),reason+"_execution_identity");
    }
    public static void AssertM04SingleFinallyRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines, IReadOnlyDictionary<string, ulong> heap) => AssertCatchRecords(lines, gcLines, heap, true);
    private static void AssertCatchRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines, IReadOnlyDictionary<string, ulong> heap, bool hasFinally)
    {
        string id = hasFinally ? "single-finally" : "simple-catch", prefix = hasFinally ? "M04_FINALLY " : "M04_CATCH ";
        string reason = hasFinally ? "m04_single_finally" : "m04_simple_catch";
        int count = hasFinally ? 14 : 12;
        Need(lines.Count == count && lines[0] == "TEST_START m04.021." + id && lines[count - 2] == "TEST_PASS m04.021." + id && lines[count - 1] == (hasFinally ? "CLROS_M04_SINGLE_FINALLY_READY" : "CLROS_M04_SIMPLE_CATCH_READY"), reason + "_count");
        int[] stages = hasFinally ? [0, 1, 2, 3, 4, 9, 10, 5, 6, 7, 8] : [0, 1, 2, 3, 4, 5, 6, 7, 8];
        var rows = stages.Select((stage, i) => (stage, values: ReadHexRecord(lines[i + 1], prefix + stage, 4))).ToDictionary(p => p.stage, p => p.values);
        var image = ReadHexRecord(gcLines[0], "M04_GC_ROOT 0", 4); var module = ReadHexRecord(gcLines[1], "M04_GC_ROOT 1", 4);
        var stack = ReadHexRecord(gcLines[2], "M04_GC_ROOT 7", 4); var canaries = ReadHexRecord(gcLines[7], "M04_GC_ROOT 8", 4);
        var bind = rows[0]; var obj = rows[1]; var rejected = rows[2]; var frames = rows[3]; var match = rows[4]; var caught = rows[5]; var resume = rows[6]; var continued = rows[7]; var check = rows[8];
        decimal end = (decimal)image[0] + image[1], catcher = (decimal)frames[2] - 32;
        Need(bind[0] == module[3] && bind[1] % 8 == 0 && bind[1] >= image[0] && (decimal)bind[1] + 65536 <= end && bind[2] is >= 288 and < 65536 && bind[3] is > 0 and <= 2048 && bind[2] >= 288 + 12 * (decimal)bind[3], reason + "_snapshot");
        Need(obj[0] % 8 == 0 && obj[0] >= (decimal)heap["retainedStart"] + 8 && (decimal)obj[0] + 16 <= heap["retainedEnd"] && obj[2] == 9 && obj[3] == 3 && obj[1] != match[0], reason + "_original_receipt");
        foreach (ulong at in new[] { obj[1], match[0] }) Need(at % 8 == 0 && at >= image[0] && (decimal)at + 24 <= end, reason + "_type_range");
        if (hasFinally) Need((decimal)frames[0] - 9 >= image[0] && (decimal)frames[0] + 1 <= end && catcher >= image[0] && catcher + 136 <= end, reason + "_code_range");
        else foreach (ulong at in new[] { frames[0], frames[2], match[1], resume[0] }) Need(at >= image[0] && at < end, reason + "_code_range");
        Need(rejected[0] == frames[0] && rejected[1] == (frames[0] ^ 1UL) && rejected[2] == frames[1] && rejected[3] == 0, reason + "_local_admission");
        Need(frames[3] == (decimal)frames[1] + 48 && match[2] == (decimal)frames[3] + (hasFinally ? 64 : 48) && match[3] == (decimal)frames[1] + 40 &&
            match[1] == (hasFinally ? catcher + 94 : (decimal)frames[2] + 25) && resume[0] == (hasFinally ? catcher + 47 : (decimal)frames[2] + 1) && resume[1] == frames[3] && resume[2] == match[2] && resume[3] == 1, reason + "_native_frame_association");
        foreach (var (at, length) in new[] { (frames[1], 8UL), (frames[3], hasFinally ? 88UL : 72UL), (check[1], 8UL) }) Need(at % 8 == 0 && at >= (decimal)stack[0] + 16400 && (decimal)at + length <= stack[2], reason + "_stack_range");
        ulong seed = (module[3] ^ heap["backing"]) & 0xffff;
        if (hasFinally)
        {
            var cleanup = rows[9]; var validated = rows[10];
            Need(cleanup[0] == obj[0] && cleanup[1] == (decimal)match[2] - 24 && cleanup[2] == seed + 2 && cleanup[3] == 1 && validated[0] == catcher + 71 && validated[1] == match[1] && validated[2] == cleanup[1] && validated[3] == cleanup[2] && caught[2] == cleanup[2], reason + "_cleanup_identity");
        }
        else Need(caught[2] == match[1], reason + "_caught_identity");
        Need(caught[0] == obj[0] && caught[1] == seed && caught[3] == 1 && continued[0] == obj[0] && continued[1] == seed && continued[2] == seed + 1 && continued[3] == 1 && check[0] == (hasFinally ? 8UL : 6UL) && check[2] == canaries[1] && check[3] == canaries[2], reason + "_execution_identity");
    }
}
