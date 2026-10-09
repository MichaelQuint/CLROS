using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM03AcpiBootstrapTranscript(string path, bool firstRealExit = false, bool firmwareRevocation = false,
        bool ownedSurvival = false, bool ownedPanic = false, bool normalBoot = false, bool physicalMapping = false, bool ownedAcpiSelection = false)
    {
        Need(!ownedSurvival || firstRealExit && !firmwareRevocation, "m03_owned_profile_invalid");
        Need(!ownedPanic || ownedSurvival, "m03_owned_profile_invalid");
        Need(!normalBoot || firstRealExit && ownedSurvival && !firmwareRevocation && !ownedPanic, "m03_normal_profile_invalid");
        Need(!firmwareRevocation || firstRealExit, "m03_revocation_requires_first_exit");
        Need(!physicalMapping || firstRealExit && ownedSurvival && !ownedPanic && !normalBoot && !firmwareRevocation, "m05_mapping_profile_invalid");
        Need(!ownedAcpiSelection || firstRealExit && ownedSurvival && !ownedPanic && !normalBoot && !firmwareRevocation && !physicalMapping, "m05_acpi_profile_invalid");
        string terminal = ownedAcpiSelection ? "TEST_PASS m05.002\r\n" : physicalMapping ? "TEST_PASS m05.001\r\n" : normalBoot || !firstRealExit ? "OS_SMOKE_PASS\r\n" : ownedPanic ? "" : ownedSurvival ? "TEST_PASS m03.010\r\n" : firmwareRevocation ? "TEST_PASS m03.008\r\n" : "TEST_PASS m03.009\r\n";
        string[] lines = FirmwareProtocol(path, terminal, firstRealExit, false, "m03_acpi");
        IReadOnlyList<string> expected = ownedAcpiSelection ? QemuContracts.M05OwnedAcpiSelectionMarkers : physicalMapping ? QemuContracts.M05PhysicalMappingMarkers : normalBoot ? QemuContracts.M03NormalVerifiedExitMarkers : ownedPanic ? QemuContracts.M03OwnedSubsystemPanicMarkers :
            ownedSurvival ? QemuContracts.M03OwnedSubsystemMarkers : firmwareRevocation ? QemuContracts.M03RevocationRequiredMarkers :
            firstRealExit ? QemuContracts.M03FirstExitRequiredMarkers : QemuContracts.M03AcpiBootstrapRequiredMarkers;
        int ownedStart = normalBoot ? 63 : 66;
        int[] varying = [3, 9, 10, 19, 20, 21, 22, 23, 24, .. ownedSurvival ? Enumerable.Range(ownedStart, ownedPanic ? 11 : 10) : [], .. physicalMapping ? Enumerable.Range(77, 7) : [], .. ownedAcpiSelection ? Enumerable.Range(77, 4) : []];
        if (ownedPanic) expected = [.. expected, lines[^1]];
        ExactInventory(lines, expected, varying);
        ValidateAllocator(lines);
        string[] roles = ["rsdp", "root", "mcfg", "apic", "hpet"];
        var rows = new List<Dictionary<string, ulong>>();
        var physical = new List<(decimal Start, decimal End)>();
        var indices = new HashSet<ulong>();
        decimal total = 0, ownerBase = 0; ulong pointerCount = 0;
        for (int i = 0; i < roles.Length; i++)
        {
            var r = ReadRecord(lines[19 + i], "CLROS_M03_002_TABLE role=" + roles[i], "physical:dec", "owned:dec", "offset:dec", "length:dec", "signature:dec", "rootIndex:dec", "compared:dec", "mismatches:dec");
            Need(r["length"] >= (i == 0 ? 20UL : 36UL) && r["length"] <= (i == 0 ? 4096UL : 65536UL) &&
                Fits(r["physical"], r["length"]) && r["owned"] != 0 && r["offset"] == total && r["compared"] == r["length"] && r["mismatches"] == 0, "m03_acpi_row_extent");
            if (i == 0)
            {
                ownerBase = r["owned"];
                Need(ownerBase >= 16 && (ownerBase - 16) % 8 == 0 && ownerBase + 266256 <= ulong.MaxValue, "m03_acpi_owned_capacity");
            }
            Need(r["owned"] == ownerBase + total, "m03_acpi_owned_offset");
            decimal lo = r["physical"], hi = lo + r["length"];
            Need(lo >= ownerBase + 266256 || ownerBase - 16 >= hi, "m03_acpi_source_destination_alias");
            Need(physical.All(p => lo >= p.End || p.Start >= hi), "m03_acpi_source_alias");
            physical.Add((lo, hi));
            if (i < 2) Need(r["rootIndex"] == ulong.MaxValue, "m03_acpi_root_index_sentinel");
            if (i == 0) Need(r["signature"] == 0x2052545020445352 && (r["length"] == 20 || r["length"] >= 36), "m03_acpi_rsdp_envelope");
            else if (i == 1)
            {
                ulong stride = r["signature"] == 0x54445358 ? 8UL : r["signature"] == 0x54445352 ? 4UL : 0;
                Need(stride != 0, "m03_acpi_root_signature");
                Need((r["length"] - 36) % stride == 0, "m03_acpi_root_stride");
                pointerCount = (r["length"] - 36) / stride;
                Need(pointerCount is >= 3 and <= 256 && (rows[0]["length"] != 20 || stride == 4), "m03_acpi_root_count");
            }
            else
            {
                ulong[] signatures = [0x4746434D, 0x43495041, 0x54455048];
                Need(r["signature"] == signatures[i - 2] && r["rootIndex"] < pointerCount && indices.Add(r["rootIndex"]), "m03_acpi_selected_association");
                if (i == 2) Need(r["length"] >= 60 && (r["length"] - 44) % 16 == 0 && (r["length"] - 44) / 16 <= 16, "m03_acpi_mcfg_length");
            }
            total += r["length"]; rows.Add(r);
        }
        var retained = ReadRecord(lines[24], "CLROS_M03_002_RETAIN", "total:dec", "descriptors:dec", "guards:dec", "tail:dec", "mismatches:dec");
        Need(total <= 266240 && retained["total"] == total && retained["descriptors"] == 5 && retained["guards"] == 32 &&
            retained["tail"] == 266240 - total && retained["mismatches"] == 0, "m03_acpi_retained_accounting");
        if (ownedSurvival) AssertM03OwnedSubsystemRecords(physicalMapping || ownedAcpiSelection ? lines[..76] : lines, rows, ownedPanic, normalBoot);
        if (physicalMapping) AssertM05PhysicalMappingRecords(lines);
        if (ownedAcpiSelection) AssertM05OwnedAcpiSelectionRecords(lines);
    }

    public static void AssertM03OwnedSubsystemRecords(IReadOnlyList<string> lines, IReadOnlyList<Dictionary<string, ulong>> acpiRows, bool panic = false, bool normalBoot = false)
    {
        Need(!(normalBoot && panic), "m03_normal_panic_conflict");
        int start = normalBoot ? 63 : 66, count = normalBoot ? 79 : panic ? 77 : 76;
        Need(lines.Count == count && lines[start] == "TEST_START m03.010" && acpiRows.Count == 5, "m03_owned_record_count");
        Need(panic || lines[start + 8] == "CLROS_M03_010_READY" && lines[start + 9] == "TEST_PASS m03.010", "m03_owned_terminal");
        ulong[][] rows = Enumerable.Range(0, panic ? 9 : 7).Select(i => ReadHexRecord(lines[start + 1 + i], "M03_OWNED " + i, 8)).ToArray();
        const ulong limit = 140737488355328;
        ulong[] a = rows[0], d = rows[6];
        Need(a[0] != 0 && a[0] % 8 == 0 && (decimal)a[0] + 312 < limit && a[2] != 0 && a[2] % 4096 == 0 &&
            (decimal)a[2] + 8192 < limit && a[1] == a[2] && a[3] == 2 && a[4] == 2 && a[5] == 8192 && a[6] == 0 && a[7] == 0, "m03_owned_allocator");
        ulong workspace = rows[1][0], copied = rows[1][1]; decimal offset = 0;
        Need(workspace != 0 && workspace % 8 == 0 && workspace < limit && copied >= 16 && (copied - 16) % 8 == 0 && (decimal)copied + 266256 < limit, "m03_owned_acpi_owner");
        for (int i = 0; i < 5; i++)
        {
            ulong[] r = rows[i + 1]; var previous = acpiRows[i];
            Need(r[0] == workspace && r[1] == copied && r[2] == previous["physical"] && r[3] == offset && r[3] == previous["offset"] &&
                copied + offset == previous["owned"] && r[4] == previous["length"] && r[5] == previous["signature"] && r[6] == previous["rootIndex"] &&
                r[7] == r[4] && r[7] == previous["compared"] && previous["mismatches"] == 0, "m03_owned_acpi_association_" + i);
            offset += r[4];
        }
        Need(d[0] != 0 && d[0] % 4096 == 0 && (decimal)d[0] + 430080 < limit && d[1] != 0 && d[1] % 4096 == 0 && (decimal)d[1] + 225280 < 4294967296 &&
            d[2] != 0 && d[2] % 4096 == 0 && (decimal)d[2] + 12288 < limit && d[3] == d[0] + 4096 && d[4] == d[0] + 8192 && d[5] == d[0] + 12288 && d[6] == 24 &&
            d[7] > d[0] + 90120 && d[7] < d[0] + 352240 && d[7] % 16 == 8, "m03_owned_descriptors");
        DistinctExtents([(a[0], 312), (copied - 16, 266272), (a[2], 8192), (d[0], 430080), (d[1], 225280), (d[2], 12288)], "m03_owned");
        if (!panic) return;
        ulong[] arm = rows[7], entered = rows[8];
        Need(arm[0] == d[0] + 86016 && arm[1] == d[1] && arm[2] == d[0] + 1024 && arm[3] == d[2] && arm[4] == d[0] &&
            arm[5] == d[0] + 90120 && arm[6] == d[0] + 352240 && arm[7] == d[0] + 401392 && entered[0] == arm[0] && entered[1] == arm[1] && entered[2] == arm[2] &&
            entered[4] == d[0] + 385032 && entered[5] == arm[7] && entered[3] >= entered[4] && entered[3] <= entered[5] - 184 && entered[3] % 8 == 0 && entered[6] == 14 && entered[7] == 0, "m03_owned_installed_gate");
        string[] fields = "rip rsp rbp rflags cr2 cr3 rax rbx rcx rdx rsi rdi r8 r9 r10 r11 r12 r13 r14 r15".Split(' ');
        var fault = ReadRecord(lines[76], "OS_SMOKE_FAIL subsystem=boot reason=exit-fault vector=14 errorCodePresent=true errorCode=0x0000000000000000", fields.Select(x => x + ":hex").ToArray());
        Need(fault["rip"] != 0 && fault["rip"] < limit && fault["rsp"] >= arm[5] && fault["rsp"] < arm[6] && (fault["rflags"] & 514) == 2 && fault["cr2"] == arm[0] && fault["cr3"] == arm[1], "m03_owned_fault_association");
    }

    public static void AssertM03FirmwareEntropyTranscript(string path, bool missing = false)
    {
        string[] lines = FirmwareProtocol(path, missing ? "TEST_FAIL m03.003.efi source_unavailable\r\n" : "OS_SMOKE_PASS\r\n", missing, true, "m03_entropy");
        string[] expected = missing ? [.. QemuContracts.M03FirmwareEntropyRequiredMarkers.Take(14),
            "M03_ENTROPY_DISCOVERY status=9223372036854775822 calls=1 found=0", "M03_ENTROPY_ACQUIRE status=0 calls=0 requested=32",
            "M03_ENTROPY_RETAIN bytes=0 scratchCleared=0 guards=64 tails=64 published=0", "M03_ENTROPY_UNAVAILABLE retainedUnchanged=1 published=0 rngCalls=0",
            "TEST_FAIL m03.003.efi source_unavailable"] : [.. QemuContracts.M03FirmwareEntropyRequiredMarkers];
        if (!missing)
        {
            expected[14] = "M03_ENTROPY_DISCOVERY status=0 calls=1 found=1";
            expected[15] = "M03_ENTROPY_ACQUIRE status=0 calls=1 requested=32";
            expected[16] = "M03_ENTROPY_RETAIN bytes=32 scratchCleared=32 guards=64 tails=64 published=1";
        }
        ExactInventory(lines, expected, 3, 9, 10); ValidateAllocator(lines);
    }

    public static void AssertM03TypedExitBootServicesTranscript(string path)
    {
        string[] lines = FirmwareProtocol(path, "OS_SMOKE_PASS\r\n", false, true, "m03_typed_ebs");
        ExactInventory(lines, QemuContracts.M03TypedExitBootServicesRequiredMarkers, 3, 9, 10, 14, 15, 16, 17, 18, 19, 20, 23, 24, 25, 26, 29, 30, 31, 32, 33, 34, 35);
        foreach (var pair in new[] { (15, QemuContracts.M03TypedAdmissionRecord), (20, QemuContracts.M03TypedReadyRecord), (26, QemuContracts.M03FinalMapReadyRecord), (35, QemuContracts.M03FinalMapRetryReadyRecord) })
            Need(lines[pair.Item1] == pair.Item2, "m03_typed_ebs_literal_" + pair.Item1);
        ValidateAllocator(lines);
        var image = ReadRecord(lines[14], "M03_TYPED_EBS_IMAGE", "handle:dec", "callback:dec", "storage:dec", "storageBytes:dec", "stateBytes:dec");
        Need(image["handle"] != 0 && image["callback"] != 0 && image["storage"] % 8 == 0 && Fits(image["storage"], 600) && image["storageBytes"] == 600 && image["stateBytes"] == 40, "m03_typed_ebs_geometry");
        ulong[] keys = [0, 9223372036854775808, ulong.MaxValue, 9223372036854775808];
        ulong[] statuses = [0, 9223372036854775810, 13835058055282164003, 0];
        for (int i = 0; i < keys.Length; i++)
        {
            var row = ReadRecord(lines[16 + i], "M03_TYPED_EBS_CASE", "id:dec", "state:dec", "handle:dec", "key:dec", "status:dec", "calls:dec", "phase:dec", "reuseAttempt:dec", "reusePrepare:dec", "unchanged:dec", "reentryAttempt:dec", "reentryPrepare:dec", "reentryUnchanged:dec");
            ulong reentry = i == 3 ? 8UL : 0, preserved = i == 3 ? 1UL : 0;
            Need(row["id"] == (ulong)i && row["state"] == (decimal)image["storage"] + 40 * i && row["handle"] == image["handle"] && row["key"] == keys[i] && row["status"] == statuses[i] && row["calls"] == 1 && row["phase"] == 2 &&
                row["reuseAttempt"] == 9 && row["reusePrepare"] == 9 && row["unchanged"] == 1 && row["reentryAttempt"] == reentry && row["reentryPrepare"] == reentry && row["reentryUnchanged"] == preserved, "m03_typed_ebs_case_" + i);
        }
        var map = ReadRecord(lines[23], "M03_FINAL_MAP_IMAGE", "handle:dec", "getMemoryMap:dec", "buffer:dec", "capacity:dec");
        Need(map["handle"] == image["handle"] && map["getMemoryMap"] != 0 && map["buffer"] % 8 == 0 && map["capacity"] == 131072 && Fits(map["buffer"], map["capacity"]), "m03_final_map_geometry");
        int[] first = [24, 29, 32];
        for (int id = 0; id < first.Length; id++)
        {
            int calls = id == 0 ? 1 : 2;
            for (int i = 0; i < calls; i++)
            {
                var row = ReadRecord(lines[first[id] + i], "M03_FINAL_MAP_CALL", "case:dec", "index:dec", "enter:dec", "returned:dec", "exit:dec", "captureStatus:dec", "key:dec", "mapBytes:dec", "descriptorBytes:dec", "descriptorVersion:dec", "exitStatus:dec");
                ulong status = id == 2 || id == 1 && i == 0 ? 9223372036854775810 : 0;
                Need(row["case"] == (ulong)id && row["index"] == (ulong)i && row["enter"] == (ulong)(3 * i + 1) && row["returned"] == (ulong)(3 * i + 2) && row["exit"] == (ulong)(3 * i + 3) && row["captureStatus"] == 0 &&
                    row["exitStatus"] == status && row["descriptorBytes"] >= 40 && row["mapBytes"] >= row["descriptorBytes"] && row["mapBytes"] <= map["capacity"] && row["mapBytes"] % row["descriptorBytes"] == 0 && row["mapBytes"] / row["descriptorBytes"] <= 4096 &&
                    row["descriptorVersion"] is > 0 and <= uint.MaxValue, "m03_final_map_call_" + id + "_" + i);
            }
            var result = ReadRecord(lines[first[id] + calls], "M03_FINAL_MAP_RESULT", "case:dec", "status:dec", "windows:dec", "captures:dec", "exits:dec", "reuse:dec", "preserved:dec");
            Need(result["case"] == (ulong)id && result["status"] == (id == 2 ? 4UL : 1UL) && result["windows"] == (ulong)calls && result["captures"] == (ulong)calls && result["exits"] == (ulong)calls && result["reuse"] == (id == 0 ? 3UL : 6UL) && result["preserved"] == 1, "m03_final_map_result_" + id);
        }
    }
}
