namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM04TypeManagerTranscript(string path, bool includeX64UnwindMetadata = false)
    {
        string[] all = FirmwareProtocol(path, "OS_SMOKE_PASS\r\n", true, false, "m04_type_manager");
        Need(all.Length == (includeX64UnwindMetadata ? QemuContracts.M04X64UnwindMetadataRequiredMarkers.Count : QemuContracts.M04TypeManagerRequiredMarkers.Count), "m04_type_manager_line_count");
        string[] unwind = includeX64UnwindMetadata ? all[37..59] : [];
        string[] lines = includeX64UnwindMetadata ? [.. all[..37], .. all[59..]] : all;
        ExactInventory(lines, QemuContracts.M04TypeManagerRequiredMarkers, 3, 9, 10, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35);
        Need(lines[27] == "CLROS_M04_011_REJECT kind=capacity status=7 unchanged=true" &&
            lines[33] == "CLROS_M04_011_REJECT kind=repeat status=9 unchanged=true" &&
            lines[34] == "CLROS_M04_011_REJECT kind=foreign status=10 unchanged=true" &&
            lines[35] == "CLROS_M04_011_READY modules=1 abiBytes=48 slotBytes=16 registered=true consumer=true tailCanaries=true",
            "m04_type_manager_rejection_and_ready_contract");
        ValidateAllocator(lines);
        var image = ReadRecord(lines[25], "CLROS_M04_011_IMAGE", "base:dec", "bytes:dec");
        var storage = ReadRecord(lines[26], "CLROS_M04_011_VALIDATE", "required:dec", "storage:dec", "capacity:dec");
        var registration = ReadRecord(lines[28], "CLROS_M04_011_REGISTER", "slot:dec", "handle:dec", "osModule:dec", "header:dec", "gc:dec", "tls:dec", "slotBytes:dec", "index:dec", "padding:dec", "abiBytes:dec", "classlib:dec", "classlibCount:dec");
        var r = registration;
        var v = storage;
        decimal imageEnd = (decimal)image["base"] + image["bytes"];
        Need(image["base"] != 0 && image["base"] % 4096 == 0 && image["bytes"] is >= 4096 and <= uint.MaxValue && imageEnd <= ulong.MaxValue && v["capacity"] == 4096 &&
            v["required"] is >= 320 and <= 4064 && v["storage"] % 8 == 0 && v["storage"] >= image["base"] && (decimal)v["storage"] + 4096 <= imageEnd &&
            r["handle"] == (decimal)v["storage"] + 272 && r["osModule"] == image["base"] && r["slotBytes"] == 16 && r["index"] == 0 && r["padding"] == 0 && r["abiBytes"] == 48 && r["classlib"] == 0 && r["classlibCount"] == 0, "m04_type_manager_geometry");
        foreach (var (at, length) in new[] { (r["slot"], 16UL), (r["header"], 16UL), (r["tls"], 8UL) })
            Need(at % 8 == 0 && at >= image["base"] && (decimal)at + length <= imageEnd && ((decimal)at >= (decimal)v["storage"] + 4096 || v["storage"] >= (decimal)at + length), "m04_type_manager_metadata_span");
        Need(r["gc"] == 0 || r["gc"] >= image["base"] && (decimal)r["gc"] + 8 <= imageEnd, "m04_type_manager_gc_span");
        ulong[] ids = [202, 204, 213, 0];
        for (int i = 0; i < ids.Length; i++)
        {
            var section = ReadRecord(lines[29 + i], "CLROS_M04_011_SECTION", "id:dec", "status:dec", "start:dec", "length:dec");
            Need(section["id"] == ids[i] && section["status"] == (i == 3 ? 11UL : 1UL) && section["length"] == (i == 0 ? 8UL : i == 1 ? 16UL : 0UL), "m04_type_manager_section_contract");
            Need(i != 0 || section["start"] == r["tls"], "m04_type_manager_section_identity");
            Need(i != 1 || section["start"] == r["slot"], "m04_type_manager_section_identity");
            Need(i != 3 || section["start"] == 0, "m04_type_manager_section_identity");
            Need(i != 2 || section["start"] >= image["base"] && section["start"] <= imageEnd, "m04_type_manager_present_empty");
        }
        if (includeX64UnwindMetadata)
            AssertM04X64UnwindMetadataTranscript(unwind, image, storage, registration);
    }

    public static void AssertM04X64UnwindMetadataTranscript(IReadOnlyList<string> lines, IReadOnlyDictionary<string, ulong> image,
        IReadOnlyDictionary<string, ulong> moduleStorage, IReadOnlyDictionary<string, ulong> registration)
    {
        Need(lines.Count == 22 && lines[0] == "TEST_START m04.020" && lines[20] == "CLROS_M04_020_READY methods=2 lookups=4 version=1 hostConformance=false guestIntegrated=true unwindExecution=false" && lines[21] == "TEST_PASS m04.020", "m04_x64_unwind_record_count");
        ulong[][] rows = Enumerable.Range(0, 19).Select(i =>
        {
            var r = ReadRecord(lines[i + 1], "CLROS_M04_020_DATA stage=" + i, "a:dec", "b:dec", "c:dec", "d:dec");
            return new[] { r["a"], r["b"], r["c"], r["d"] };
        }).ToArray();
        var original = rows[0];
        var directory = rows[1];
        var storage = rows[2];
        var stack = rows[3];
        var initial = rows[4];
        var finalStack = rows[16];
        var final = rows[17];
        var retained = rows[18];
        Need(original[0] == image["base"] && original[1] == image["bytes"] && original[2] == moduleStorage["storage"] && original[3] == registration["handle"] && retained[0] == original[2] && retained[1] == original[3] && retained[2] == registration["slot"] && retained[3] == original[3], "m04_x64_unwind_original_module");
        bool InImage(ulong at, ulong length, ulong alignment) => length > 0 && at % alignment == 0 && at >= image["base"] && (decimal)at + length <= (decimal)image["base"] + image["bytes"];
        bool Rva(ulong at, ulong length, ulong alignment) => at > 0 && at % alignment == 0 && at < image["bytes"] && (decimal)at + length <= image["bytes"];
        Need(storage[1] == 65536 && storage[2] == 65504 && storage[3] == 288 && InImage(storage[0], 65536, 8) && directory[2] is >= 1 and <= 2048 && (decimal)directory[2] * 12 == directory[1] && Rva(directory[0], directory[1], 4) &&
            directory[3] <= 65504 && directory[3] >= (decimal)storage[3] + directory[1] + (decimal)directory[2] * 4 && final[0] == directory[3] && final[1] == directory[3] && final[2] == directory[2] && final[3] == 4, "m04_x64_unwind_storage_directory");
        Need(InImage(stack[0], 768, 8) && stack[1] != 0 && stack[1] % 4096 == 0 && (decimal)stack[1] + 135168 == stack[2] && (decimal)stack[2] - 40 == stack[3] &&
            (stack[1] >= (decimal)image["base"] + image["bytes"] || image["base"] >= stack[2]), "m04_x64_unwind_original_stack");
        DistinctExtents([(moduleStorage["storage"], 4096), (storage[0], 65536), (stack[0], 768), (registration["slot"], 16)], "m04_x64_unwind_owner");
        foreach (ulong witness in new[] { initial[0], initial[1], finalStack[1] })
            Need(witness % 8 == 0 && witness >= (decimal)stack[1] + 16400 && witness <= (decimal)stack[3] - 8, "m04_x64_unwind_stack_witness");
        const ulong word0 = 0x3D947E21A6C850BF, word1 = 0xC2715AB809E634DF;
        Need(initial[1] <= initial[0] && finalStack[1] <= initial[1] && finalStack[0] == 7 && initial[2] == word0 && initial[3] == word1 && finalStack[2] == word0 && finalStack[3] == word1, "m04_x64_unwind_stack_preservation");
        ulong seed = image["base"] ^ registration["handle"];
        foreach (int first in new[] { 5, 10 })
        {
            var method = rows[first];
            var metadata = rows[first + 1];
            var frame = rows[first + 2];
            var flags = rows[first + 3];
            var call = rows[first + 4];
            Need(method[1] < directory[2] && method[2] < method[3] && Rva(method[2], method[3] - method[2], 1) && method[0] >= (decimal)image["base"] + method[2] && method[0] < (decimal)image["base"] + method[3] && call[3] == (decimal)image["base"] + method[3] - 1, "m04_x64_unwind_method_range");
            Need(Rva(metadata[0], 4, 4) && Rva(metadata[1], 4, 4) && metadata[2] <= 255 && metadata[2] <= method[3] - method[2] && metadata[3] is >= 1 and <= 255 &&
                new ulong[] { 0, 3, 5, 6, 7, 12, 13, 14, 15 }.Contains(frame[0]) && frame[1] <= 15 && (frame[0] != 0 || frame[1] == 0) && (decimal)frame[1] * 16 <= frame[2] && frame[2] % 8 == 0 && frame[2] <= frame[3] && frame[3] is > 0 and <= uint.MaxValue && frame[3] % 8 == 0 && flags[0] <= 4 && flags[1] <= 3 && flags[2] <= 31, "m04_x64_unwind_metadata_shape");
            Need(flags[2] == 0 ? metadata[0] == metadata[1] && flags[0] == flags[1] : metadata[0] != metadata[1] && flags[0] == 4, "m04_x64_unwind_chain_identity");
            ulong prefix = 4 + (metadata[3] + metadata[3] % 2) * 2, extra = flags[0] == 4 ? 12UL : flags[0] != 0 ? 4UL : 0, bytes = prefix + extra;
            Need(Rva(metadata[0], bytes, 4) && ((decimal)metadata[0] >= (decimal)directory[0] + directory[1] || (decimal)directory[0] >= (decimal)metadata[0] + bytes), "m04_x64_unwind_metadata_extent");
            if (flags[1] == 0)
                Need(flags[3] == 0 && call[0] == 0, "m04_x64_unwind_handler_absent");
            else
                Need(Rva(flags[3], 1, 1) && call[0] >= (decimal)metadata[1] + 8 && call[0] <= image["bytes"] && (flags[2] != 0 || call[0] == (decimal)metadata[0] + prefix + 4), "m04_x64_unwind_handler_range");
            Need(call[1] == seed && call[2] == (seed ^ (first == 5 ? 255UL : 85UL)), "m04_x64_unwind_actual_call");
        }
        Need(rows[5][0] != rows[10][0] && rows[5][1] != rows[10][1] && (rows[5][2] >= rows[10][3] || rows[10][2] >= rows[5][3]) && rows[15][0] == 15 && rows[15][1] == 15 && rows[15][2] == 1 && rows[15][3] == 2, "m04_x64_unwind_lookup_call_counts");
    }
}
