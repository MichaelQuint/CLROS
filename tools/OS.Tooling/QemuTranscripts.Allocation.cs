namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private static void ValidateAllocationBlocks(string[] fixedObjects, string[] arrays, string[] strings, string[] references, IReadOnlyDictionary<string, ulong> heap)
    {
        if (fixedObjects.Length != 0)
            AssertM04RhpNewFastRecords(fixedObjects, heap);
        if (arrays.Length != 0)
            AssertM04SzArrayAllocationRecords(arrays, heap);
        if (strings.Length != 0)
            AssertM04StringAllocationRecords(strings, heap);
        if (references.Length != 0)
            AssertM04CheckedReferenceBarrierRecords(references, heap);
    }

    private static readonly string[] BindFields = ["image:hex", "imageBytes:dec", "allocator:hex", "nodes:hex", "ledger:hex", "ledgerBytes:dec", "parentIndex:dec", "backing:hex", "backingBytes:dec", "heap:hex", "heapBytes:dec", "access:hex", "accessBytes:dec", "bump:hex", "bumpBytes:dec", "status:dec", "used:dec", "count:dec"];
    private static readonly string[] ObjectFields = ["mt:hex", "prefix0:hex", "prefix1:hex", "prefix2:hex", "flags:hex", "base:dec", "allocation:hex", "reference:hex", "end:hex", "usedBefore:dec", "usedAfter:dec", "count:dec", "sync:hex", "observedMt:hex", "zeroBytes:dec", "zeroOr:hex"];
    private static readonly string[] ElementFields = ["length:dec", "element:hex", "element0:hex", "element1:hex", "element2:hex"];
    private static readonly string[] RelatedFields = ["relatedObject:hex", "relatedObject0:hex", "relatedObject1:hex", "relatedObject2:hex"];
    private static readonly string[] RetainFields = ["allocator:hex", "nodes:hex", "ledger:hex", "parentIndex:dec", "backing:hex", "heap:hex", "bump:hex", "heapStart:hex", "heapEnd:hex", "initialPages:dec", "freePages:dec", "allocatedPages:dec", "readBytes:dec", "contentsZero:false", "used:dec", "count:dec", "status:dec"];
    private static (Dictionary<string, ulong> Bind, List<(ulong Start, ulong Length)> Extents) AllocationBinding(IReadOnlyList<string> lines, string prefix, IReadOnlyDictionary<string, ulong> heap)
    {
        var bind = ReadRecord(lines[2], prefix + "_BIND", BindFields);
        foreach (var (field, ancestor) in new[] { ("allocator", "allocatorState"), ("nodes", "nodes"), ("ledger", "ledger"), ("parentIndex", "parentIndex"), ("backing", "backing"), ("heap", "heapState") })
            Need(bind[field] == heap[ancestor], prefix + "_original_identity");
        Need(Fits(bind["image"], bind["imageBytes"]) && bind["imageBytes"] >= 64 && bind["ledgerBytes"] == 200 && bind["backingBytes"] == 8192 && bind["heapBytes"] == 216 && bind["accessBytes"] == 104 && bind["bumpBytes"] == 80 && bind["status"] == 1 && bind["used"] == 0 && bind["count"] == 0, prefix + "_bind_contract");
        var prep = ReadRecord(lines[1], prefix + "_PREP", "image:hex", "imageBytes:dec", "access:hex", "accessBytes:dec", "bump:hex", "bumpBytes:dec", "control:hex", "controlBytes:dec", "records:hex", "recordsBytes:dec");
        Need(new[] { "image", "imageBytes", "access", "accessBytes", "bump", "bumpBytes" }.All(x => prep[x] == bind[x]), prefix + "_preparation_identity");
        Need(prep["controlBytes"] == 96 && prep["recordsBytes"] == 6672, prefix + "_preparation_size");
        List<(ulong Start, ulong Length)> extents = [(bind["allocator"], 56), (bind["nodes"], 48), (bind["ledger"], 200), (bind["heap"], 216), (bind["backing"], 8192), (bind["access"], 104), (bind["bump"], 80), (prep["control"], 96), (prep["records"], 6672)];
        DistinctExtents(extents, prefix + "_storage");
        return (bind, extents);
    }
    private static void RetainedAllocation(string line, string prefix, IReadOnlyDictionary<string, ulong> bind, IReadOnlyDictionary<string, ulong> heap, ulong used, ulong count)
    {
        var r = ReadRecord(line, prefix + "_RETAIN", RetainFields);
        Need(new[] { "allocator", "nodes", "ledger", "parentIndex", "backing", "heap", "bump" }.All(x => r[x] == bind[x]), prefix + "_retained_identity");
        Need(r["heapStart"] == heap["retainedStart"] && r["heapEnd"] == heap["retainedEnd"] && r["initialPages"] == 2 && r["freePages"] == 1 && r["allocatedPages"] == 1 && r["readBytes"] == 4096 && r["used"] == used && r["count"] == count && r["status"] == 1, prefix + "_retained_accounting");
    }
    public static void AssertM04RhpNewFastRecords(IReadOnlyList<string> lines, IReadOnlyDictionary<string, ulong> heap) => AllocationRecords(lines, heap, "M04_RHP");
    public static void AssertM04SzArrayAllocationRecords(IReadOnlyList<string> lines, IReadOnlyDictionary<string, ulong> heap) => AllocationRecords(lines, heap, "M04_ARRAY");
    public static void AssertM04StringAllocationRecords(IReadOnlyList<string> lines, IReadOnlyDictionary<string, ulong> heap) => AllocationRecords(lines, heap, "M04_STRING");

    private static void AllocationRecords(IReadOnlyList<string> lines, IReadOnlyDictionary<string, ulong> heap, string prefix)
    {
        bool fixedMode = prefix == "M04_RHP", stringMode = prefix == "M04_STRING";
        int count = fixedMode ? 10 : stringMode ? 7 : 5;
        string test = fixedMode ? "m04.007.rhp_new_fast" : stringMode ? "m04.009" : "m04.008.sz_array";
        string ready = (fixedMode ? QemuContracts.M04RhpNewFastReadyMarker : stringMode ? QemuContracts.M04StringAllocationReadyMarker : QemuContracts.M04SzArrayAllocationReadyMarker)[0];
        Need(lines.Count == 2 * count + 6 && lines[0] == "TEST_START " + test && lines[^2] == ready && lines[^1] == "TEST_PASS " + test, prefix + "_exact_terminal");
        var (bind, extents) = AllocationBinding(lines, prefix, heap);
        ulong[] sizes = fixedMode ? [24, 32, 32, 32, 24, 24, 24, 32, 24, 24] : stringMode ? [24, 32, 24, 24, 32, 32, 32] : [24, 24, 48, 32, 32];
        ulong[] flags = fixedMode ? [0x50000000, 0x51000000, 0x53000000, 0x52000000, 0x20000080, 0x20000080, 0x40000000, 0x41000000, 0x43000000, 0x4A000200] : stringMode ? [0x50000000, 3758227457, 3489660930, 3489660930, 3489660930, 3489660930, 3489660930] : [1342177280, 3758227457, 3758227464, 1358954496, 3758227457];
        ulong[] lengths = stringMode ? [0, 1, 0, 1, 3, 3, 2] : [0, 0, 3, 0, 1];
        ulong[] offsets = stringMode ? [8, 16, 12, 12, 12, 12, 12] : [8, 0, 24, 16, 16];
        ulong[] widths = stringMode ? [8, 1, 0, 2, 6, 6, 4] : [8, 0, 8, 8, 1];
        ulong[] scalars = fixedMode ? [0x1111111111111111, 0x2222222222222222, 0x3333333333333333, 0x4444444444444444, 0, 0, 0, 0, 0, 0] : stringMode ? [0x1111111111111111, 90, 0, 65, 0x03A903A903A9, 0, 3623933952] : [0x1111111111111111, 0, 0x3333333333333333, 0x2222222222222222, 90];
        List<ulong> metadata = [];
        ulong used = 0;
        Dictionary<string, ulong>? repeated = null;
        for (int id = 1; id <= count; id++)
        {
            bool isString = stringMode && id >= 3, isArray = !fixedMode && (stringMode ? id == 2 : id is 2 or 3 or 5);
            string route = fixedMode ? id <= 4 ? "new" : "direct" : isString ? "compiled_string" : isArray ? !stringMode && id == 3 ? "compiled_long_array" : "compiled_byte_array" : "compiled_fixed";
            string[] fields = fixedMode ? ObjectFields : stringMode ? [.. ObjectFields, .. ElementFields, .. RelatedFields] : [.. ObjectFields, .. ElementFields];
            var o = ReadRecord(lines[id * 2 + 1], prefix + "_OBJECT id=" + id + " route=" + route, fields);
            ulong size = sizes[id - 1], flag = flags[id - 1], prefixSize = fixedMode ? size : isString ? 22UL : !stringMode && id == 4 ? 32UL : 24UL;
            ulong zeroBytes = size - 8 - (isArray || isString ? 4UL : 0);
            Need(o["flags"] == flag && (o["prefix0"] & uint.MaxValue) == flag && (o["prefix0"] >> 32) == prefixSize && o["base"] == size && o["allocation"] == (decimal)bind["backing"] + used && o["reference"] == (decimal)o["allocation"] + 8 && o["end"] == (decimal)o["allocation"] + size &&
                o["usedBefore"] == used && o["usedAfter"] == used + size && o["count"] == (ulong)id && o["sync"] == 0 && o["observedMt"] == o["mt"] && o["zeroBytes"] == zeroBytes && o["zeroOr"] == 0 && (fixedMode || o["length"] == lengths[id - 1]), prefix + "_object_" + id);
            if (!fixedMode)
            {
                if (isArray)
                    Need(o["prefix1"] == o["element"] && (o["element0"] >> 32) == 24 && ((o["element0"] >> 26) & 31) == (!stringMode && id == 3 ? 10UL : 5UL), prefix + "_element_association");
                else
                    Need(new[] { "element", "element0", "element1", "element2" }.All(x => o[x] == 0), prefix + "_element_noise");
            }
            if (stringMode)
            {
                if (isString)
                    Need(o["prefix1"] == o["relatedObject"] && (o["relatedObject0"] >> 32) == 24 && o["relatedObject1"] == 0, "m04_string_related_object_association");
                else
                    Need(new[] { "relatedObject", "relatedObject0", "relatedObject1", "relatedObject2" }.All(x => o[x] == 0), "m04_string_related_object_noise");
            }
            if (stringMode && id == 3 || !fixedMode && !stringMode && id == 2)
                repeated = o;
            bool repeatedType = stringMode && id > 3 || !fixedMode && !stringMode && id == 5;
            if (repeatedType)
            {
                string[] repeatedFields = stringMode ? ["mt", "prefix0", "prefix1", "prefix2", "flags", "relatedObject", "relatedObject0", "relatedObject1", "relatedObject2"] : ["mt", "prefix0", "prefix1", "prefix2", "flags", "element", "element0", "element1", "element2"];
                Need(repeatedFields.All(x => o[x] == repeated![x]), prefix + "_repeated_metadata");
            }
            else
            {
                List<ulong> tokens = [o["mt"]];
                if (isArray)
                    tokens.Add(o["element"]);
                if (isString)
                    tokens.Add(o["relatedObject"]);
                foreach (ulong token in tokens)
                {
                    Need(token != 0 && token % 8 == 0 && token >= bind["image"] && (decimal)token + 24 <= (decimal)bind["image"] + bind["imageBytes"], prefix + "_metadata_extent");
                    Need(extents.All(x => token >= (decimal)x.Start + x.Length || x.Start >= (decimal)token + 24), prefix + "_metadata_storage_alias");
                    Need(metadata.All(p => fixedMode ? token != p : token >= (decimal)p + 24 || p >= (decimal)token + 24), prefix + "_metadata_identity_alias");
                    metadata.Add(token);
                }
            }
            string[] readFields = fixedMode ? ["scalarOffset:dec", "scalar:hex", "referenceSlotBytes:dec", "referenceSlotOr:hex", "priorCount:dec", "priorMismatchBytes:dec", "canaryMismatchBytes:dec"] : stringMode ?
                ["length:dec", "scalarOffset:dec", "scalarWidth:dec", "scalar:hex", "terminator:hex", "terminatorValue:hex", "referenceSlotBytes:dec", "referenceSlotOr:hex", "constructorCount:dec", "priorCount:dec", "priorMismatchBytes:dec", "canaryMismatchBytes:dec"] :
                ["length:dec", "scalarOffset:dec", "scalarWidth:dec", "scalar:hex", "referenceSlotBytes:dec", "referenceSlotOr:hex", "priorCount:dec", "priorMismatchBytes:dec", "canaryMismatchBytes:dec"];
            var read = ReadRecord(lines[id * 2 + 2], prefix + "_READBACK id=" + id, readFields);
            ulong offset = fixedMode ? id == 1 ? 8UL : id <= 4 ? 16UL : 0 : offsets[id - 1];
            ulong untouched = size - 16 - (fixedMode ? id <= 4 ? 8UL : 0 : widths[id - 1]) - (isArray || isString ? 4UL : 0);
            Need(read["scalarOffset"] == offset && read["scalar"] == scalars[id - 1] && read["referenceSlotBytes"] == untouched && read["referenceSlotOr"] == 0 && read["priorCount"] == (ulong)id && read["priorMismatchBytes"] == 0 && read["canaryMismatchBytes"] == 0, prefix + "_readback_" + id);
            if (!fixedMode)
                Need(read["length"] == lengths[id - 1] && read["scalarWidth"] == widths[id - 1], prefix + "_readback_geometry");
            if (stringMode)
                Need(read["terminator"] == (isString ? (decimal)o["reference"] + 12 + 2 * lengths[id - 1] : 0) && read["terminatorValue"] == 0 && read["constructorCount"] == (id <= 2 ? 1UL : (ulong)id - 1), "m04_string_readback_terminator");
            used = o["usedAfter"];
        }
        Need(fixedMode || used == (stringMode ? 200UL : 160UL), prefix + "_used_total");
        RetainedAllocation(lines[2 * count + 3], prefix, bind, heap, used, (ulong)count);
    }

    public static void AssertM04CheckedReferenceBarrierRecords(IReadOnlyList<string> lines, IReadOnlyDictionary<string, ulong> heap)
    {
        const string prefix = "M04_REFERENCE";
        Need(lines.Count == 19 && lines[0] == "TEST_START m04.010.references" && lines[18] == "TEST_PASS m04.010.references", "m04_reference_exact_sequence");
        var (bind, extents) = AllocationBinding(lines, prefix, heap);
        Need(bind["image"] % 8 == 0, "m04_reference_bind_contract");
        var g = ReadRecord(lines[9], "M04_REFERENCE_GEOMETRY", "state:hex", "stateBytes:dec", "cellReference:dec", "cellTag:dec", "cellPayload:dec", "pairFirst:dec", "pairLast:dec", "pairTag:dec", "blockPayload:dec");
        Need(g["stateBytes"] == 432 && g["cellReference"] == 8 && g["cellTag"] == 16 && g["cellPayload"] >= 24 && g["cellPayload"] % 8 == 0 && g["pairTag"] == 16 &&
            (g["pairFirst"] == 0 && g["pairLast"] == 8 || g["pairFirst"] == 8 && g["pairLast"] == 0) && g["blockPayload"] >= 8 && g["blockPayload"] % 8 == 0, "m04_reference_geometry");
        extents.Add((g["state"], 432));
        DistinctExtents(extents, "m04_reference_storage");
        List<Dictionary<string, ulong>> objects = [];
        ulong used = 0;
        for (int id = 1; id <= 6; id++)
        {
            var o = ReadRecord(lines[id + 2], "M04_REFERENCE_OBJECT id=" + id, [.. ObjectFields, "scalarOffset:dec", "scalar:hex"]);
            ulong flags = id <= 2 ? 0x50000000UL : 0x51000000UL, offset = id <= 2 ? 8UL : id <= 4 ? g["cellTag"] : 0, scalar = id <= 4 ? 0x1111222233334400UL + (ulong)id : 0;
            decimal minimum = id <= 2 ? 24 : id <= 4 ? (decimal)g["cellPayload"] + 32 : (decimal)g["blockPayload"] + 200;
            Need(o["mt"] != 0 && o["mt"] % 8 == 0 && o["mt"] >= bind["image"] && (decimal)o["mt"] + 24 <= (decimal)bind["image"] + bind["imageBytes"] && o["flags"] == flags && (o["prefix0"] & uint.MaxValue) == flags && (o["prefix0"] >> 32) == o["base"] &&
                o["base"] >= minimum && o["base"] % 8 == 0 && o["base"] <= 4096 - used && o["allocation"] == (decimal)bind["backing"] + used && o["reference"] == (decimal)o["allocation"] + 8 && o["end"] == (decimal)o["allocation"] + o["base"] &&
                o["usedBefore"] == used && o["usedAfter"] == used + o["base"] && o["count"] == (ulong)id && o["sync"] == 0 && o["observedMt"] == o["mt"] && o["zeroBytes"] == o["base"] - 8 && o["zeroOr"] == 0 && o["scalarOffset"] == offset && o["scalar"] == scalar, "m04_reference_object_" + id);
            Need(extents.All(s => o["mt"] >= (decimal)s.Start + s.Length || s.Start >= (decimal)o["mt"] + 24), "m04_reference_metadata_alias");
            if (id % 2 == 0)
                Need(new[] { "mt", "prefix0", "prefix1", "prefix2", "flags", "base" }.All(x => o[x] == objects[id - 2][x]), "m04_reference_paired_type");
            else
                Need(objects.All(previous => o["mt"] >= (decimal)previous["mt"] + 24 || previous["mt"] >= (decimal)o["mt"] + 24), "m04_reference_distinct_type_alias");
            objects.Add(o);
            used = o["usedAfter"];
        }
        const ulong cookie = 0x179BCDE02468ACE0;
        ulong[][] operations = [[2, 18, cookie], [1, 19, cookie ^ 0x1111222233334404], [2, 20, cookie], [3, 22, cookie ^ 0x1928374655AABBCC], [4, 23, cookie ^ 0x3141592653589707]];
        for (int i = 0; i < operations.Length; i++)
        {
            var operation = ReadRecord(lines[i + 10], "M04_REFERENCE_OPERATION", "kind:dec", "calls:dec", "cookie:hex");
            Need(operation["kind"] == operations[i][0] && operation["calls"] == operations[i][1] && operation["cookie"] == operations[i][2], "m04_reference_operation_" + i);
        }
        RetainedAllocation(lines[15], prefix, bind, heap, used, 6);
        const string suffix = " preserved=true";
        Need(lines[16].EndsWith(suffix, StringComparison.Ordinal), "m04_reference_stack_shape");
        var stack = ReadRecord(lines[16][..^suffix.Length], "M04_REFERENCE_STACK", "start:hex", "end:hex", "initialRsp:hex", "witness:hex", "sentinel0:hex", "sentinel1:hex", "expected0:hex", "expected1:hex", "armed0:hex", "armed1:hex", "final0:hex", "final1:hex", "checks:dec", "words:dec");
        decimal low = stack["start"], high = stack["end"], witness = stack["witness"];
        Need(low != 0 && low % 4096 == 0 && high - low == 135168 && stack["initialRsp"] == high - 40 && witness % 8 == 0 && witness + 8 <= stack["initialRsp"] && witness - low - 16 >= 16384 &&
            stack["sentinel0"] == low && stack["sentinel1"] == low + 8 && stack["expected0"] == 0x195A86B4C327D0EF && stack["expected1"] == 0x62C79E1035AB48D6 && stack["armed0"] == stack["expected0"] && stack["armed1"] == stack["expected1"] &&
            stack["final0"] == stack["expected0"] && stack["final1"] == stack["expected1"] && stack["checks"] == 5 && stack["words"] == 2, "m04_reference_stack_contract");
        foreach (var (at, length) in extents.Append((bind["image"], bind["imageBytes"])))
            Need(low >= (decimal)at + length || at >= high, "m04_reference_stack_alias");
        Need(lines[17] == "CLROS_M04_010_NATIVE_READY allocations=6 usedBytes=" + used + " calls=23 initialStores=18 fieldStores=1 checkedStores=1 pairHelpers=2 bulkCalls=1 pairComplete=true blockComplete=true exclusiveNonCollecting=true gate4=Fail", "m04_reference_ready");
    }
}
