namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM04FrameDispatchRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines,
        IReadOnlyDictionary<string,ulong> heap)
    {
        const string reason = "m04_frames";
        Need(lines.Count == 18 && lines[0] == "TEST_START m04.021.frames" &&
            lines[16] == "TEST_PASS m04.021.frames" && lines[17] == "CLROS_M04_021_FRAMES_READY",reason+"_count");
        var rows = Enumerable.Range(0,15).Select(stage => ReadHexRecord(lines[stage+1],"M04_FRAMES "+stage,4)).ToArray();
        var image = ReadHexRecord(gcLines[0],"M04_GC_ROOT 0",4);
        var module = ReadHexRecord(gcLines[1],"M04_GC_ROOT 1",4);
        var stack = ReadHexRecord(gcLines[2],"M04_GC_ROOT 7",4);
        var canaries = ReadHexRecord(gcLines[7],"M04_GC_ROOT 8",4);
        var bind = rows[0]; var obj = rows[1]; var first = rows[3]; var second = rows[4]; var plan = rows[7];
        var cleanup = rows[8]; var validated = rows[9]; var caught = rows[10]; var resume = rows[11];
        var continued = rows[12]; var check = rows[13]; var last = rows[14];
        decimal imageEnd = (decimal)image[0]+image[1], middle = (decimal)first[2]-25, catcher = (decimal)second[2]-46;
        Need(bind[0] == module[3] && bind[1]%8 == 0 && bind[1] >= image[0] && (decimal)bind[1]+65536 <= imageEnd &&
            bind[2] is >=288 and <65536 && bind[3] is >0 and <=2048 && bind[2] >= 288+12*(decimal)bind[3],reason+"_snapshot");
        Need(obj[0]%8 == 0 && obj[0] >= (decimal)heap["retainedStart"]+8 && (decimal)obj[0]+16 <= heap["retainedEnd"] &&
            obj[2] == 9 && obj[3] == 3 && obj[1]%8 == 0 && obj[1] >= image[0] && (decimal)obj[1]+24 <= imageEnd,
            reason+"_original_receipt");
        Need(rows[2].SequenceEqual(new ulong[]{1,1,0,0}) && rows[6].SequenceEqual(new ulong[]{1,0,1,1}),reason+"_typed_receipts");
        Need(rows[5].SequenceEqual(new[]{(ulong)OS.Kernel.Runtime.BoundedExceptionFrameSearchStatus.InvalidFrame,
            (ulong)OS.Kernel.Runtime.BoundedExceptionFrameSearchStatus.DispatchRejected,
            (ulong)OS.Kernel.Runtime.BoundedExceptionDispatchStatus.InvalidClause,0UL}),reason+"_local_rejections");
        Need((decimal)first[0]-9 >= image[0] && (decimal)first[0]+1 <= imageEnd &&
            middle >= image[0] && middle+72 <= imageEnd && catcher >= image[0] && catcher+138 <= imageEnd &&
            middle != catcher,reason+"_code_ranges");
        Need(first[3] == (decimal)first[1]+48 && second[0] == first[2] && second[1] == first[3] &&
            second[3] == (decimal)first[3]+64 && plan[0] == middle+49 && plan[1] == catcher+96 &&
            plan[2] == (decimal)first[3]+40 && plan[3] == (decimal)second[3]+40 && plan[2] != plan[3] &&
            resume[0] == catcher+47 && resume[1] == second[3] && resume[2] == (decimal)second[3]+64 && resume[3] == 1,
            reason+"_physical_frame_association");
        foreach (var (at,length) in new (decimal,decimal)[]{((decimal)first[1]-1088,1088),(first[3],72),(second[3],88),(check[1],8)})
            Need(at%8 == 0 && at >= (decimal)stack[0]+16400 && at+length <= stack[2],reason+"_stack_ranges");
        ulong seed = (module[3]^heap["backing"]) & 0xffff;
        Need(cleanup.SequenceEqual(new[]{obj[0],plan[2],seed+4,1UL}) &&
            validated.SequenceEqual(new[]{plan[0],first[3],second[3],obj[0]}),reason+"_earlier_cleanup");
        Need(caught.SequenceEqual(new[]{obj[0],seed,seed+3,1UL}) && continued.SequenceEqual(new[]{obj[0],seed+1,seed+3,1UL}) &&
            check[0] == 10 && check[2] == canaries[1] && check[3] == canaries[2] &&
            last.SequenceEqual(new[]{obj[0],obj[1],module[3],obj[2]}),reason+"_execution_identity");
    }
}
