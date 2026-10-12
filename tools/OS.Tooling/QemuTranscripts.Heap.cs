using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM04ManagedHeapPageRangeTranscript(string path, bool includeFixedAllocation = false, bool includeArrayAllocation = false,
        bool includeStringAllocation = false, bool includeCheckedReferences = false, bool includeGcStatics = false, bool includeSeededChurn = false,
        bool includeClassConstructors = false, bool includeInterfaceDispatch = false, bool includeSimpleCatch = false, bool includeSingleFinally = false, bool includeRethrow = false, bool includeNestedFinally = false, bool includeUnhandled = false, bool includeFrameDispatch = false, bool includeConstructorFailure = false, bool includeFilteredCatch = false, bool includeEhCatalog = false)
    {
        AssertManagedHeapText(Encoding.UTF8.GetString(File.ReadAllBytes(path)), includeFixedAllocation, includeArrayAllocation,
            includeStringAllocation, includeCheckedReferences, includeGcStatics, includeSeededChurn, includeClassConstructors,
            includeInterfaceDispatch, includeSimpleCatch, includeSingleFinally, includeRethrow, includeNestedFinally, includeUnhandled, includeFrameDispatch, includeConstructorFailure, includeFilteredCatch, includeEhCatalog);
    }

    private static void AssertManagedHeapText(string text, bool includeFixedAllocation = false, bool includeArrayAllocation = false,
        bool includeStringAllocation = false, bool includeCheckedReferences = false, bool includeGcStatics = false, bool includeSeededChurn = false,
        bool includeClassConstructors = false, bool includeInterfaceDispatch = false, bool includeSimpleCatch = false, bool includeSingleFinally = false,
        bool includeRethrow = false, bool includeNestedFinally = false, bool includeUnhandled = false, bool includeFrameDispatch = false, bool includeConstructorFailure = false, bool includeFilteredCatch = false, bool includeEhCatalog = false)
    {
        Need(new[] { includeFixedAllocation, includeArrayAllocation, includeStringAllocation, includeCheckedReferences, includeGcStatics }.Count(x => x) <= 1, "m04_allocation_profile_collision");
        Need(!includeSeededChurn || includeGcStatics, "m04_churn_requires_gc_records");
        Need(!includeClassConstructors || includeGcStatics && !includeSeededChurn, "m04_cctor_profile_collision");
        Need(!includeInterfaceDispatch || includeClassConstructors, "m04_dispatch_requires_cctor_records");
        Need(!includeSimpleCatch || includeGcStatics && !includeSeededChurn && !includeClassConstructors && !includeInterfaceDispatch, "m04_simple_catch_profile_collision");
        Need(!includeSingleFinally || includeGcStatics && !includeSimpleCatch && !includeSeededChurn && !includeClassConstructors && !includeInterfaceDispatch, "m04_single_finally_profile_collision");
        Need(!includeRethrow || includeGcStatics && !includeSingleFinally && !includeSimpleCatch && !includeSeededChurn && !includeClassConstructors && !includeInterfaceDispatch, "m04_rethrow_profile_collision");
        Need(!includeNestedFinally || includeGcStatics && !includeRethrow && !includeSingleFinally && !includeSimpleCatch && !includeSeededChurn && !includeClassConstructors && !includeInterfaceDispatch, "m04_nested_finally_profile_collision");
        Need(!includeUnhandled || includeGcStatics && !includeNestedFinally && !includeRethrow && !includeSingleFinally && !includeSimpleCatch && !includeSeededChurn && !includeClassConstructors && !includeInterfaceDispatch, "m04_unhandled_profile_collision");
        Need(!includeFrameDispatch || includeGcStatics && !includeUnhandled && !includeNestedFinally && !includeRethrow && !includeSingleFinally && !includeSimpleCatch && !includeSeededChurn && !includeClassConstructors && !includeInterfaceDispatch, "m04_frames_profile_collision");
        Need(!includeConstructorFailure || includeGcStatics && !includeFrameDispatch && !includeUnhandled && !includeNestedFinally && !includeRethrow && !includeSingleFinally && !includeSimpleCatch && !includeSeededChurn && !includeClassConstructors && !includeInterfaceDispatch, "m04_ctor_failure_profile_collision");
        Need(!includeFilteredCatch || includeGcStatics && !includeConstructorFailure && !includeFrameDispatch && !includeUnhandled && !includeNestedFinally && !includeRethrow && !includeSingleFinally && !includeSimpleCatch && !includeSeededChurn && !includeClassConstructors && !includeInterfaceDispatch, "m04_filters_profile_collision");
        string[] expected = ["TEST_START efi.main", .. QemuContracts.M04LayoutContractRequiredMarkers,
            "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY", "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT", "BOOT_PAGE_ALLOCATOR_READY", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY",
            .. QemuContracts.M03FirstExitStageMarkers, "TEST_START m04.006", "M04_HEAP_PAGE_BOUND", "M04_HEAP_PAGE_INITIALIZED", "M04_HEAP_PAGE_RETAINED", "M04_HEAP_PAGE_CURRENT", .. QemuContracts.M04ManagedHeapPageRangeReadyMarker, "TEST_PASS m04.006",
            "TEST_START boot.sequence", "BOOT_PHASE PhaseA.Audit START", "BOOT_PHASE PhaseA.Audit PASS", "BOOT_PHASE PhaseA.Diagnostics START", "BOOT_PHASE PhaseA.Diagnostics PASS", "OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS"];
        if(includeUnhandled) expected=expected[..^8];
        Need(!includeEhCatalog || includeFrameDispatch, "m04_eh_catalog_requires_frames");
        int lineCount = (includeEhCatalog ? 7 : 0) + expected.Length + (includeFixedAllocation ? 26 : includeArrayAllocation ? 16 : includeStringAllocation ? 20 : includeCheckedReferences ? 19 : includeGcStatics ? 18 : 0) +
            (includeSeededChurn ? 12 : 0) + (includeClassConstructors ? 12 : 0) + (includeInterfaceDispatch ? 76 : 0) + (includeSimpleCatch ? 12 : 0) + (includeSingleFinally ? 14 : 0) + (includeRethrow ? 18 : 0) + (includeNestedFinally ? 19 : 0) + (includeUnhandled ? 11 : 0) + (includeFrameDispatch ? 18 : 0) + (includeConstructorFailure ? 22 : 0) + (includeFilteredCatch ? 18 : 0);
        const string failures = @"(?i)TEST_FAIL|OS_SMOKE_FAIL|M04_.*FAILED|BOOT_PAGE_ALLOCATOR_NOT_READY|UEFI_ACPI_RSDP_(?:MISSING|INVALID)";
        string failureText=includeUnhandled ? text.Replace(UnhandledFailure+"\r\n","",StringComparison.Ordinal).Replace(UnhandledTerminal+"\r\n","",StringComparison.Ordinal) : text;
        Need(!Regex.IsMatch(Classify(failureText), failures), "m04_heap_failure_record");
        string[] raw = text.Split('\n'); List<string> captured = []; StringBuilder preamble = new(); bool started = false;
        for (int i = 0; i < raw.Length; i++)
        {
            bool complete = i < raw.Length - 1 && raw[i].EndsWith('\r'); string line = complete ? raw[i][..^1] : raw[i]; string candidate = Classify(line);
            Need(includeUnhandled && (line==UnhandledFailure || line==UnhandledTerminal) || !Regex.IsMatch(candidate, failures), "m04_heap_failure_record");
            if (!started)
            {
                if (line == "TEST_START efi.main" && complete) started = true;
                else { preamble.Append(candidate); Need(!Regex.IsMatch(preamble.ToString(), @"(?i)CLROS_|M01_|M02_|M04_|TEST_|OS_|EFI_|UEFI_|BOOT_"), "m04_heap_disguised_preamble_record"); continue; }
            }
            if (captured.Count >= lineCount)
            {
                Need(!includeUnhandled && !includeInterfaceDispatch && !includeSimpleCatch && !includeSingleFinally && !includeRethrow && !includeNestedFinally && !includeFrameDispatch && !includeConstructorFailure && !includeFilteredCatch && complete && line.Length == 0 || i == raw.Length - 1 && line.Length == 0, "m04_heap_post_terminal_noise"); continue;
            }
            Need(complete && !Controls.IsMatch(line), "m04_heap_incomplete_or_control_record"); captured.Add(line);
        }
        Need(captured.Count == lineCount, "m04_heap_incomplete_sequence");
        string[] Remove(int at, int count) { string[] block = captured.GetRange(at, count).ToArray(); captured.RemoveRange(at, count); return block; }
        string[] frameRecords = includeFrameDispatch ? Remove(62,includeEhCatalog ? 25 : 18) : [], catalogRecords = [];
        if (includeEhCatalog)
        {
            catalogRecords = [.. frameRecords.Skip(6).Take(6), frameRecords[24]];
            frameRecords = [.. frameRecords.Take(6), .. frameRecords.Skip(12).Take(12)];
        }
        string[] constructorFailureRecords = includeConstructorFailure ? Remove(62,22) : [];
        string[] filteredCatchRecords = includeFilteredCatch ? Remove(62,18) : [];
        string[] unhandledRecords=includeUnhandled ? Remove(62,11) : [], nestedRecords = includeNestedFinally ? Remove(62,19) : [], rethrowRecords = includeRethrow ? Remove(62, 18) : [], finallyRecords = includeSingleFinally ? Remove(62, 14) : [], catchRecords = includeSimpleCatch ? Remove(62, 12) : [],
            dispatchRecords = includeInterfaceDispatch ? Remove(74, 76) : [], cctorRecords = includeClassConstructors ? Remove(47, 12) : [],
            churnRecords = includeSeededChurn ? Remove(62, 12) : [], gcRecords = [];
        if (includeGcStatics) { gcRecords = captured.GetRange(44, 17).ToArray(); Need(captured[61] == "CLROS_M04_013_READY", "m04_collector_ready"); captured.RemoveRange(44, 18); }
        string[] references = includeCheckedReferences ? Remove(44, 19) : [], strings = includeStringAllocation ? Remove(44, 20) : [],
            arrays = includeArrayAllocation ? Remove(44, 16) : [], fixedObjects = includeFixedAllocation ? Remove(44, 26) : [];
        string[] identityFields = ["allocatorState", "nodes", "heapState", "ledger", "parentIndex", "parentStart", "parentEnd", "backing", "retainedStart", "retainedEnd"];
        Dictionary<string, string[]> numeric = new(StringComparer.Ordinal)
        {
            ["UEFI_MEMORY_MAP_DESCRIPTOR_COUNT"] = ["descriptorCount"], ["BOOT_MEMORY_REGION_COUNT"] = ["regionCount"],
            ["BOOT_PAGE_ALLOCATOR_READY"] = ["regions", "conventionalPages", "conventionalBytes", "freeNodes", "freePages", "freeBytes"],
            ["M04_HEAP_PAGE_BOUND"] = identityFields, ["M04_HEAP_PAGE_RETAINED"] = identityFields,
            ["M04_HEAP_PAGE_INITIALIZED"] = ["pages", "bytes", "freePagesBefore", "freeBytesBefore", "allocatedPagesBefore", "allocatedBytesBefore", "freePagesAfter", "freeBytesAfter", "allocatedPagesAfter", "allocatedBytesAfter", "readBytes"],
            ["M04_HEAP_PAGE_CURRENT"] = ["freePages", "freeBytes", "allocatedPages", "allocatedBytes", "readBytes"]
        };
        Dictionary<string, Dictionary<string, ulong>> values = new(StringComparer.Ordinal);
        Need(captured.Count == expected.Length, "m04_heap_base_count");
        for (int i = 0; i < expected.Length; i++)
        {
            string key = expected[i];
            if (!numeric.TryGetValue(key, out string[]? fields)) { Need(captured[i] == key, "m04_heap_exact_sequence_" + i); continue; }
            string pattern = "\\A" + Regex.Escape(key);
            if (key == "M04_HEAP_PAGE_RETAINED") pattern += " checkpoint=beforeBootPlan";
            if (key == "BOOT_PAGE_ALLOCATOR_READY") pattern += " status=Ready";
            foreach (string field in fields)
            {
                pattern += " " + (key is "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT" or "BOOT_MEMORY_REGION_COUNT" ? "" : field + "=");
                pattern += key is "M04_HEAP_PAGE_BOUND" or "M04_HEAP_PAGE_RETAINED" && field != "parentIndex" ? "(0x[0-9A-F]{16})" : "(0|[1-9][0-9]*)";
            }
            if (key == "BOOT_PAGE_ALLOCATOR_READY") pattern += " pageSize=4096 statusCode=0";
            if (key is "M04_HEAP_PAGE_INITIALIZED" or "M04_HEAP_PAGE_CURRENT") pattern += " contentsZero=true";
            Match match = Regex.Match(captured[i], pattern + "\\z"); Need(match.Success, "m04_heap_numeric_shape_" + key);
            values[key] = fields.Select((field, j) => (field, n: Number(match.Groups[j + 1].Value))).ToDictionary(x => x.field, x => x.n, StringComparer.Ordinal);
        }
        var map = values["BOOT_PAGE_ALLOCATOR_READY"]; var bound = values["M04_HEAP_PAGE_BOUND"]; var retained = values["M04_HEAP_PAGE_RETAINED"];
        Need(map["regions"] is >= 1 and <= 4096 && map["regions"] == values["UEFI_MEMORY_MAP_DESCRIPTOR_COUNT"]["descriptorCount"] && map["regions"] == values["BOOT_MEMORY_REGION_COUNT"]["regionCount"] && map["freeNodes"] >= 1 && map["freeNodes"] <= map["regions"] && map["freePages"] >= map["freeNodes"] && map["freePages"] == map["conventionalPages"] && (decimal)map["freePages"] * 4096 == map["freeBytes"] && map["freeBytes"] == map["conventionalBytes"], "m04_heap_map_accounting");
        Need(identityFields.All(f => bound[f] == retained[f]), "m04_heap_retained_identity");
        Need(bound["parentIndex"] <= 4 && bound["parentStart"] != 0 && bound["parentStart"] % 4096 == 0 && (decimal)bound["parentStart"] + 8192 <= ulong.MaxValue && (decimal)bound["parentStart"] + 8192 == bound["parentEnd"] && bound["backing"] == bound["parentStart"] && bound["retainedStart"] == bound["parentStart"] && (decimal)bound["retainedStart"] + 4096 == bound["retainedEnd"], "m04_heap_parent_interval");
        var extents = new[] { (bound["allocatorState"], 56UL), (bound["nodes"], 48UL), (bound["ledger"], 200UL), (bound["heapState"], 216UL) };
        DistinctExtents(extents, "m04_heap_metadata");
        foreach (var (at, length) in extents) Need(at >= bound["parentEnd"] || bound["parentStart"] >= (decimal)at + length, "m04_heap_metadata_extent");
        ulong[] initialExpected = [1, 4096, 2, 8192, 0, 0, 1, 4096, 1, 4096, 4096], currentExpected = [1, 4096, 1, 4096, 4096];
        for (int i = 0; i < initialExpected.Length; i++) Need(values["M04_HEAP_PAGE_INITIALIZED"][numeric["M04_HEAP_PAGE_INITIALIZED"][i]] == initialExpected[i], "m04_heap_initial_accounting");
        for (int i = 0; i < currentExpected.Length; i++) Need(values["M04_HEAP_PAGE_CURRENT"][numeric["M04_HEAP_PAGE_CURRENT"][i]] == currentExpected[i], "m04_heap_current_accounting");
        if (includeGcStatics) AssertM04GcStaticRootRecords(gcRecords, retained);
        if (includeSeededChurn) AssertM04SeededCollectorChurnRecords(churnRecords, gcRecords);
        if (includeSimpleCatch) AssertM04SimpleCatchRecords(catchRecords, gcRecords, retained);
        if (includeSingleFinally) AssertM04SingleFinallyRecords(finallyRecords, gcRecords, retained);
        if (includeRethrow) AssertM04RethrowRecords(rethrowRecords, gcRecords, retained);
        if (includeNestedFinally) AssertM04NestedFinallyRecords(nestedRecords,gcRecords,retained);
        if (includeFrameDispatch) AssertM04FrameDispatchRecords(frameRecords,gcRecords,retained);
        if (includeEhCatalog) AssertM04EhCatalogRecords(catalogRecords,frameRecords,gcRecords);
        if (includeConstructorFailure) AssertM04ConstructorFailureRecords(constructorFailureRecords,gcRecords,retained);
        if (includeFilteredCatch) AssertM04FilteredCatchRecords(filteredCatchRecords,gcRecords,retained);
        if (includeUnhandled) AssertM04UnhandledRecords(unhandledRecords,gcRecords,retained);
        ValidateAllocationBlocks(fixedObjects, arrays, strings, references, retained);
        ValidateConstructorDispatchBlocks(cctorRecords, dispatchRecords, gcRecords, retained);
    }

    public static void AssertM04RhpNewFastTranscript(string path) => AssertM04ManagedHeapPageRangeTranscript(path, includeFixedAllocation: true);
    public static void AssertM04SzArrayAllocationTranscript(string path) => AssertM04ManagedHeapPageRangeTranscript(path, includeArrayAllocation: true);
    public static void AssertM04StringAllocationTranscript(string path)
    { StrictHeapFraming(path, 72, "m04_string"); AssertM04ManagedHeapPageRangeTranscript(path, includeStringAllocation: true); }
    public static void AssertM04CheckedReferenceBarrierTranscript(string path)
    { StrictHeapFraming(path, 71, "m04_checked_reference"); AssertM04ManagedHeapPageRangeTranscript(path, includeCheckedReferences: true); }
    private static void StrictHeapFraming(string path, int count, string reason)
    {
        byte[] bytes = File.ReadAllBytes(path);
        Need(bytes.Length <= 32768, reason + "_raw_extent");
        string text = DecodeUtf8(bytes);
        int start = text.IndexOf("TEST_START efi.main\r\n", StringComparison.Ordinal);
        Need(start == 0 || start == FirmwarePrefixBytes && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes.AsSpan(0, start))) == FirmwarePrefixSha256, reason + "_firmware_prefix");
        string body = text[start..];
        Need(!Regex.IsMatch(body, @"[^\x20-\x7e\r\n]|(?<!\r)\n|\r(?!\n)"), reason + "_raw_delimiter");
        string[] records = body.Split("\r\n", StringSplitOptions.None);
        Need(records.Length == count + 1 && records[^1].Length == 0, reason + "_complete_count");
    }
}
