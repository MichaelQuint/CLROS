using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM03MemoryMapCaptureTranscript(string path)
    {
        string[] lines = FirmwareProtocol(path, "OS_SMOKE_PASS\r\n", false, false, "m03_capture");
        ExactInventory(lines, QemuContracts.M03MemoryMapCaptureRequiredMarkers, 3, 4, 5, 6, 7, 8, 9, 13, 19, 20);
        var a = ReadRecord(lines[3], "M03_CAPTURE_AUTH", "observer:rawhex", "observerBytes:dec", "guard:rawhex", "guardSpan:dec", "payload:rawhex", "capacity:dec", "services:rawhex", "get:rawhex", "allocate:rawhex");
        var q = ReadRecord(lines[4], "M03_CAPTURE_QUERY", "calls:dec", "order:dec", "inputBytes:dec", "buffer:rawhex", "status:rawhex", "required:dec", "key:rawhex", "stride:dec", "version:dec");
        var p = ReadRecord(lines[5], "M03_CAPTURE_ALLOC", "calls:dec", "order:dec", "type:dec", "bytes:dec", "status:rawhex", "pool:rawhex", "admitted:dec");
        var f = ReadRecord(lines[6], "M03_CAPTURE_FINAL", "calls:dec", "order:dec", "inputBytes:dec", "buffer:rawhex", "status:rawhex", "bytes:dec", "key:rawhex", "stride:dec", "version:dec");
        var c = ReadRecord(lines[7], "M03_CAPTURE_COPY", "source:rawhex", "sourceBytes:dec", "owned:rawhex", "capacity:dec", "bytes:dec", "count:dec", "flag:dec", "compared:dec", "mismatches:dec", "firstMismatch:rawhex");
        var g = ReadRecord(lines[8], "M03_CAPTURE_GUARDS", "bytes:dec", "mismatches:dec", "tailBytes:dec", "tailMismatches:dec");
        var h = ReadRecord(lines[9], "M03_CAPTURE_HANDOFF", "snapshotKey:rawhex", "handoffKey:rawhex", "flag:dec", "matched:dec");
        Need(a["observerBytes"] == 488 && a["guardSpan"] == 131104 && a["capacity"] == 131072 && (decimal)a["guard"] + 16 == a["payload"] && a["get"] != 0 && a["allocate"] != 0, "m03_capture_geometry");
        List<(decimal Start, decimal End)> extents = [];
        foreach (var (start, length) in new[] { (a["observer"], a["observerBytes"]), (a["guard"], a["guardSpan"]), (a["services"], 328UL), (p["pool"], p["bytes"]) })
        {
            decimal end = (decimal)start + length;
            Need(start != 0 && length != 0 && end <= ulong.MaxValue, "m03_capture_extent");
            Need(extents.All(x => start >= x.End || x.Start >= end), "m03_capture_alias");
            extents.Add((start, end));
        }
        Need(q["calls"] == 1 && q["order"] == 1 && q["inputBytes"] == 0 && q["buffer"] == 0 && q["status"] == 0x8000000000000005 && q["required"] != 0 && q["stride"] != 0 && q["version"] <= uint.MaxValue &&
            (decimal)q["required"] + 8 * (decimal)q["stride"] <= ulong.MaxValue && (decimal)q["required"] + 8 * (decimal)q["stride"] == p["bytes"] && p["calls"] == 1 && p["order"] == 2 && p["type"] == 2 && p["status"] == 0 && p["admitted"] == 1, "m03_capture_query_allocation");
        Need(f["calls"] == 1 && f["order"] == 3 && f["inputBytes"] == p["bytes"] && f["buffer"] == p["pool"] && f["status"] == 0 && f["bytes"] != 0 && f["bytes"] <= p["bytes"] && f["bytes"] <= a["capacity"] && f["stride"] >= 40 && f["version"] is > 0 and <= uint.MaxValue && f["bytes"] % f["stride"] == 0 &&
            c["source"] == p["pool"] && c["sourceBytes"] == p["bytes"] && c["owned"] == a["payload"] && c["capacity"] == a["capacity"] && c["bytes"] == f["bytes"] && c["count"] == f["bytes"] / f["stride"] && c["count"] is >= 1 and <= 4096 && c["flag"] == 1 && c["compared"] == c["bytes"] && c["mismatches"] == 0 && c["firstMismatch"] == ulong.MaxValue &&
            g["bytes"] == 32 && g["mismatches"] == 0 && g["tailBytes"] == a["capacity"] - f["bytes"] && g["tailMismatches"] == 0 && h["snapshotKey"] == f["key"] && h["handoffKey"] == f["key"] && h["flag"] == 1 && h["matched"] == 1, "m03_capture_copy_handoff");
        ValidateAllocator(lines, 13, 19, 20);
        Need(lines[13] == "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT " + c["count"], "m03_capture_descriptor_count");
    }

    public static void AssertM04LayoutContractTranscript(string path)
    {
        string[] expected = [.. QemuContracts.M04LayoutContractRequiredMarkers, "OS_SMOKE_PASS"];
        string[] lines = DecodeUtf8(File.ReadAllBytes(path)).Split('\n');
        int next = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            bool complete = i < lines.Length - 1;
            string line = lines[i];
            if (complete && line.EndsWith('\r'))
                line = line[..^1];
            string candidate = Classify(line);
            Need(!QemuContracts.M04LayoutContractForbiddenMarkers.Any(f => candidate.Contains(f.Replace(" ", "", StringComparison.Ordinal), StringComparison.OrdinalIgnoreCase)), "m04_layout_contract_failure_record");
            bool owned = candidate.Contains("CLROS_M04", StringComparison.OrdinalIgnoreCase) || candidate.Contains("M04_", StringComparison.OrdinalIgnoreCase) ||
                new[] { "EFI_CONSOLE_READY", "UEFI_MEMORY_MAP_READY", "OS_SMOKE_PASS" }.Any(b => candidate.Contains(b, StringComparison.OrdinalIgnoreCase));
            if (Controls.IsMatch(line))
                Need(!new[] { "CLROS_", "M01_", "M02_", "M04_", "TEST_", "OS_", "EFI_", "UEFI_", "BOOT_" }.Any(p => candidate.Contains(p, StringComparison.OrdinalIgnoreCase)), "m04_layout_contract_control_in_record");
            if (owned)
            {
                Need(complete && next < expected.Length && line == expected[next], "m04_layout_contract_exact_sequence_invalid");
                next++;
            }
        }
        Need(next == expected.Length, "m04_layout_contract_exact_sequence_incomplete");
    }
}
