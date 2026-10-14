namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private static void ValidateConstructorDispatchBlocks(string[] constructors, string[] dispatch, string[] gc, IReadOnlyDictionary<string, ulong> heap)
    {
        if (constructors.Length != 0) AssertM04ClassConstructorRecords(constructors, gc, heap);
        if (dispatch.Length != 0) AssertM04InterfaceDispatchRecords(dispatch, gc, constructors, heap);
    }
    public static void AssertM04ClassConstructorRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines, IReadOnlyDictionary<string, ulong> heap)
    {
        Need(lines.Count == 12 && lines[0] == "TEST_START m04.024" && lines[10] == "CLROS_M04_024_READY" && lines[11] == "TEST_PASS m04.024", "m04_cctor_record_count");
        var records = Enumerable.Range(0, 9).Select(i => ReadHexRecord(lines[i + 1], "M04_CCTOR " + i, 4)).ToArray();
        var image = ReadHexRecord(gcLines[0], "M04_GC_ROOT 0", 4); var module = ReadHexRecord(gcLines[1], "M04_GC_ROOT 1", 4); var root = ReadHexRecord(gcLines[3], "M04_GC_ROOT 3", 4);
        var binding = records[0]; var bases = records[6]; var reentry = records[8]; decimal seed = binding[3], end = (decimal)image[0] + image[1];
        Need(binding[0] == module[3] && binding[3] == (module[3] ^ heap["backing"]) && binding[2] == 8192 && binding[1] % 8 == 0 && binding[1] >= image[0] && (decimal)binding[1] + 8192 <= end && seed != 0 && seed + 52 <= ulong.MaxValue, "m04_cctor_binding");
        decimal[][] expected = [[1, 1, 1, seed + 11, seed + 11], [2, seed + 21, seed + 21, 1, 1], [3, seed + 31, seed + 32, seed + 31, 1], [4, seed + 41, seed + 42, seed + 43, 2], [5, seed + 51, seed + 52, seed + 51, 2], [7, 1, 7, 0, 8], [8, 5, 3, 0, root[1]]];
        foreach (var row in expected) for (int j = 0; j < 4; j++) Need(records[(int)row[0]][j] == row[j + 1], "m04_cctor_value_" + row[0]);
        Need(bases[0] is >= 1 and <= 64 && bases[1] is >= 1 and <= 64 && bases[2] % 8 == 0 && bases[2] >= image[0] && (decimal)bases[2] + 8 < end && bases[3] == (decimal)bases[2] + 8 && reentry[3] == (decimal)heap["retainedStart"] + 8, "m04_cctor_original_static_bases");
    }
    public static void AssertM04InterfaceDispatchRecords(IReadOnlyList<string> lines, IReadOnlyList<string> gcLines, IReadOnlyList<string> cctorLines, IReadOnlyDictionary<string, ulong> heap)
    {
        Need(lines.Count == 76 && lines[0] == "TEST_START m04.016" && lines[74] == "CLROS_M04_016_READY" && lines[75] == "TEST_PASS m04.016", "m04_dispatch_record_count");
        var rows = Enumerable.Range(0, 73).Select(i => ReadHexRecord(lines[i + 1], "M04_DISPATCH " + i, 4)).ToArray();
        var image = ReadHexRecord(gcLines[0], "M04_GC_ROOT 0", 4); var module = ReadHexRecord(gcLines[1], "M04_GC_ROOT 1", 4); var stack = ReadHexRecord(gcLines[2], "M04_GC_ROOT 7", 4); var root = ReadHexRecord(gcLines[3], "M04_GC_ROOT 3", 4);
        var collector = ReadHexRecord(gcLines[15], "M04_COLLECT 7", 4); var collectorStack = ReadHexRecord(gcLines[16], "M04_COLLECT 8", 4); var cctor = ReadHexRecord(cctorLines[1], "M04_CCTOR 0", 4);
        var binding = rows[0]; var cells = rows[1]; var objects = rows[2]; var original = rows[4];
        ulong seed = ((module[3] ^ heap["backing"] ^ stack[2]) & 65535) + 1;
        Need(binding[0] == module[3] && binding[2] == 32768 && binding[3] == seed && original[0] == root[1] && original[1] == collector[0] && original[2] == 0xC011EC7000000006 && module[3] == (decimal)original[3] + 272, "m04_dispatch_original_authority");
        bool InImage(ulong address, ulong bytes, ulong alignment) => bytes > 0 && address % alignment == 0 && address >= image[0] && (decimal)address + bytes <= (decimal)image[0] + image[1];
        Need(InImage(binding[1], 32768, 8) && InImage(original[3], 4096, 8) && InImage(cells[0], cells[1], 8) && cells[1] == 80 && cells[3] == 5 && InImage(cells[2], 1, 1), "m04_dispatch_image_ranges");
        DistinctExtents([(binding[1], 32768), (original[3], 4096), (cctor[1], 8192)], "m04_dispatch_owner");
        for (int i = 0; i < 2; i++) Need(objects[i] % 8 == 0 && objects[i] >= (decimal)heap["retainedStart"] + 8 && (decimal)objects[i] + 16 <= (decimal)heap["retainedStart"] + 176 && InImage(objects[i + 2], 24, 8), "m04_dispatch_receiver_extent");
        DistinctExtents([(objects[0] - 8, 24), (objects[1] - 8, 24), (original[0] - 8, 24), (original[1] - 8, 32)], "m04_dispatch_receiver");
        ulong[][] expected = [[3, 9, 10, 24, 24], [9, 7, 2, 10, 176], [71, 7, 7, original[0], original[2]]];
        foreach (var row in expected) for (int j = 0; j < 4; j++) Need(rows[row[0]][j] == row[j + 1], "m04_dispatch_fixed_accounting");
        Need(objects[2] != objects[3] && rows[6][0] != objects[2] && rows[6][0] != objects[3] && rows[7][0] == objects[2] && rows[8][0] == objects[3], "m04_dispatch_declaring_types");
        Need(InImage(rows[6][0], 24, 8) && InImage(rows[6][1], 4, 4) && rows[6][2] == 4 && rows[6][3] == 1 && rows[7][1] == 0 && rows[7][2] == 0 && rows[7][3] == 4 && rows[8][1] == 0 && rows[8][2] == 0 && rows[8][3] == 4, "m04_dispatch_sealed_extent");
        for (int i = 0; i < 3; i++) Need(rows[5][i] == stack[i], "m04_dispatch_original_stack");
        Need(rows[72][0] == 17 && rows[72][1] == collectorStack[1] && rows[72][2] == collectorStack[2], "m04_dispatch_stack_canary");
        foreach (ulong witness in new[] { rows[5][3], rows[72][3] }) Need(witness % 8 == 0 && witness >= (decimal)stack[0] + 16400 && (decimal)witness + 8 <= stack[2] && witness <= stack[3], "m04_dispatch_stack_extent");
        Need(rows[72][3] <= rows[5][3], "m04_dispatch_stack_minimum");
        // Compiler sorting is not an ABI guarantee. Five distinct identities in
        // this exact five-cell region exhaust it without accepting a terminator,
        // interior pointer, repeated callsite or an unrelated image address.
        HashSet<ulong> seenCells = [], seenInterfaces = []; ulong[] slotsExpected = [1, 0, 1, 2, 3];
        for (int i = 0; i < 15; i++)
        {
            int operation = i / 3, receiver = i % 3 == 1 ? 1 : 0, at = 10 + i * 4; bool second = receiver == 1;
            var call = rows[at]; var types = rows[at + 1]; var slots = rows[at + 2]; var value = rows[at + 3];
            ulong interfaceSlot = operation is 2 or 3 ? 1UL : 0;
            Need(call[0] == objects[receiver] && call[3] == (ulong)(9 + receiver) && types[0] == objects[receiver + 2] &&
                call[1] >= cells[0] && call[1] - cells[0] < cells[1] && (call[1] - cells[0]) % 16 == 0 &&
                InImage(call[2], 1, 1) && call[2] != cells[2] && InImage(types[1], 24, 8) && InImage(types[2], 24, 8) && slots[0] == interfaceSlot && slots[2] == (i % 3 == 2 ? 1UL : 0) && slots[3] == (ulong)i + 1, "m04_dispatch_call_" + i);
            Need(types[3] == (operation <= 1 ? rows[6][0] : objects[receiver + 2]) && slots[1] == slotsExpected[operation], "m04_dispatch_slot_owner");
            bool variant = operation is 2 or 3 && !second;
            Need(variant ? types[1] != types[2] : types[1] == types[2], "m04_dispatch_variance");
            if (i % 3 == 0) Need(seenCells.Add(call[1]) && seenInterfaces.Add(types[1]), "m04_dispatch_callsite_alias");
            else
            {
                int first = 10 + operation * 12;
                Need(call[1] == rows[first][1] && types[1] == rows[first + 1][1] && slots[0] == rows[first + 2][0], "m04_dispatch_callsite_changed");
                if (second) Need(operation == 0 ? call[2] == rows[first][2] : call[2] != rows[first][2], "m04_dispatch_receiver_target");
                else { Need(call.SequenceEqual(rows[first]) && types.SequenceEqual(rows[first + 1]) && slots[1] == rows[first + 2][1], "m04_dispatch_cache_identity"); }
            }
            ulong a = seed + (ulong)i + 1, d = a + 3, offset = operation == 0 ? 100UL : (ulong)(operation * 200 + 100 * receiver);
            ulong scalar = seed + 11 + (ulong)(11 * receiver) + offset + a + 3 * (a + 1) + 5 * (a + 2) + 7 * d;
            Need(value[0] == scalar && value[1] == scalar && value[2] == (ulong)operation && value[3] == d, "m04_dispatch_scalar_oracle");
        }
        Need(rows[70][0] == 15 && rows[70][1] == 10 && rows[70][2] == 5 && rows[70][3] is >= 1 and <= 1024, "m04_dispatch_final_accounting");
    }
}
