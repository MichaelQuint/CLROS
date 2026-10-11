namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM04ConstructorFailureRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines,
        IReadOnlyDictionary<string,ulong> heap)
    {
        const string reason = "m04_ctor_failure";
        Need(lines.Count == 22 && lines[0] == "TEST_START m04.021.ctor-failure" &&
            lines[20] == "TEST_PASS m04.021.ctor-failure" && lines[21] == "CLROS_M04_021_CTOR_FAILURE_READY",reason+"_count");
        var rows = Enumerable.Range(0,19).Select(stage => ReadHexRecord(lines[stage+1],"M04_CTOR_FAILURE "+stage,4)).ToArray();
        var image = ReadHexRecord(gcLines[0],"M04_GC_ROOT 0",4);
        var module = ReadHexRecord(gcLines[1],"M04_GC_ROOT 1",4);
        var stack = ReadHexRecord(gcLines[2],"M04_GC_ROOT 7",4);
        var canaries = ReadHexRecord(gcLines[7],"M04_GC_ROOT 8",4);
        decimal imageEnd = (decimal)image[0]+image[1], constructor = rows[3][1], context = rows[3][0],
            invoke = (decimal)rows[5][2]-28, outer = (decimal)rows[6][0]-16;
        ulong seed = (module[3]^heap["backing"]) & 0xffff, original = rows[1][0], wrapper = rows[10][0];
        Need(rows[0][0] == module[3] && rows[0][1]%8 == 0 && rows[0][1] >= image[0] &&
            (decimal)rows[0][1]+65536 <= imageEnd && rows[0][2] is >=288 and <65536 && rows[0][3] is >0 and <=2048 &&
            rows[0][2] >= 288+12*(decimal)rows[0][3],reason+"_snapshot");
        Need(original%8 == 0 && original >= (decimal)heap["retainedStart"]+8 &&
            (decimal)original+16 <= heap["retainedEnd"] && wrapper == (decimal)original+24 &&
            (decimal)wrapper+24 <= heap["retainedEnd"] && rows[1][2] == 9 && rows[1][3] == 3 &&
            rows[1][1]%8 == 0 && rows[1][1] >= image[0] && (decimal)rows[1][1]+24 <= imageEnd,
            reason+"_original_and_wrapper_allocations");
        foreach (decimal type in new decimal[]{rows[7][2],rows[7][3]})
            Need(type%8 == 0 && type >= image[0] && type+24 <= imageEnd && type != rows[1][1],reason+"_typed_ranges");
        Need(rows[7][2] != rows[7][3] && rows[2].SequenceEqual(new ulong[]{1,1,0,0}) &&
            rows[11].SequenceEqual(new[]{wrapper,1UL,1UL,1UL}),reason+"_typed_predicates_and_reference_store");
        Need(context%8 == 0 && context >= image[0] && context+16 <= imageEnd && constructor%16 == 0 &&
            constructor >= image[0] && constructor+55 <= imageEnd && invoke >= image[0] && invoke+53 <= imageEnd &&
            outer >= image[0] && outer+101 <= imageEnd && constructor != invoke && constructor != outer && invoke != outer &&
            rows[3][2] == 0 && rows[3][3] == 0xffffffff && rows[4].SequenceEqual(new[]{rows[3][0],rows[3][1],1UL,seed}),
            reason+"_compiler_context_and_body");
        Need(rows[5][0] == constructor+54 && rows[5][3] == (decimal)rows[5][1]+64 &&
            rows[6][1] == (decimal)rows[5][1]+416 && rows[6][2] == (decimal)rows[6][1]+48 && rows[6][3] == context &&
            rows[7][0] == invoke+35 && rows[7][1] == outer+40 && rows[12][0] == invoke+52 &&
            rows[12][1] == (decimal)rows[5][1]-896 && rows[12][2] == rows[7][1] && rows[12][3] == rows[6][1] &&
            rows[14].SequenceEqual(new[]{(ulong)(outer+20),rows[6][1],rows[6][2],1UL}),reason+"_physical_frames");
        foreach (var (at,bytes) in new (decimal,decimal)[]{((decimal)rows[5][1]-848,848),
            ((decimal)rows[12][1]-848,848),(rows[5][3],56),(rows[6][1],72)})
            Need(at%8 == 0 && at >= (decimal)stack[0]+16400 && at+bytes <= stack[2],reason+"_stack_ranges");
        Need(rows[8].SequenceEqual(new[]{original,rows[3][0],1UL,1UL}) &&
            rows[9].SequenceEqual(new[]{0UL,rows[3][0],rows[3][1],1UL}) &&
            rows[10].SequenceEqual(new[]{wrapper,original,rows[3][0],4UL}) &&
            rows[13].SequenceEqual(new[]{wrapper,original,rows[3][0],1UL}) &&
            rows[15].SequenceEqual(new[]{wrapper,original,seed+1,1UL}) &&
            rows[16].SequenceEqual(new[]{(ulong)OS.Kernel.Runtime.CompilerClassConstructorStatus.InitializationFailed,rows[3][1],1UL,0UL}) &&
            rows[18].SequenceEqual(new[]{original,wrapper,9UL,10UL}),reason+"_failure_wrap_outer_catch_and_continuation");
        Need(rows[17][0] == 7 && rows[17][1]%8 == 0 && rows[17][1] >= (decimal)stack[0]+16 &&
            rows[17][1] < rows[12][1] && rows[17][2] == canaries[1] && rows[17][3] == canaries[2],reason+"_canaries");
    }
}
