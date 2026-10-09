using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static void AssertM02Ud2VectorStateTerminalTranscript(string path)
    {
        Need(File.Exists(path), "m02_ud2_vector_state_serial_log_missing");
        string[] prefixes = ["OS_", "TEST_", "CLROS_", "M01_", "M02_", "M03_", "M04_", "EFI_", "UEFI_", "BOOT_"];
        string[] records = Regex.Split(DecodeUtf8(File.ReadAllBytes(path)), @"\r\n|\n|\r").Select(x => x.Trim()).Where(x => prefixes.Any(p => x.StartsWith(p, StringComparison.Ordinal))).ToArray();
        int smoke = Array.IndexOf(records, "OS_SMOKE_PASS");
        Need(smoke >= 0, "m02_ud2_vector_state_smoke_terminal_missing");
        Need(records.Length == smoke + 4 && records[smoke + 1] == "TEST_START m02.013" && records[smoke + 2] == QemuContracts.M02Ud2VectorStateReadyMarker[0] && records[smoke + 3] == "TEST_PASS m02.013", "m02_ud2_vector_state_post_smoke_sequence_invalid");
    }

    private static string[] M02TerminalPrefix(string text, int count, string reason)
    {
        Need(text.Length <= 1048576, reason + "_capture_extent");
        MatchCollection starts = Regex.Matches(text, @"(?m)^TEST_START efi\.main\r\n");
        Need(starts.Count == 1, reason + "_efi_start");
        string pre = text[..starts[0].Index];
        Need(pre.Length == 0 || pre.Length == FirmwarePrefixBytes && Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(pre))) == FirmwarePrefixSha256, reason + "_preamble_record");
        string body = text[starts[0].Index..];
        Need(!Regex.IsMatch(body, @"[^\x20-\x7e\r\n]|(?<!\r)\n|\r(?!\n)"), reason + "_raw_delimiter");
        string[] records = body.Split("\r\n", StringSplitOptions.None);
        Need(records.Length == count + 1 && records[^1].Length == 0, reason + "_complete_count");
        ExactInventory(records[..QemuContracts.M02TerminalPrefix.Count], QemuContracts.M02TerminalPrefix, 3, 9, 10);
        ValidateAllocator(records);
        return records[..^1];
    }

    private static ulong[] NamedHex(string line, string prefix, params string[] fields)
    {
        var record = ReadRecord(line, prefix, fields.Select(f => f + ":hex").ToArray());
        return fields.Select(f => record[f]).ToArray();
    }

    public static void AssertM02PageFaultCr2ErrorCodeTranscript(string path) => AssertM02FaultTranscript(path, false);
    public static void AssertM02DoubleFaultIstTranscript(string path) => AssertM02FaultTranscript(path, true);
    private static void AssertM02FaultTranscript(string path, bool doubleFault)
    {
        string reason = doubleFault ? "m02_double_fault" : "m02_page_fault", prefix = doubleFault ? "M02_DF" : "M02_PAGE_FAULT", id = doubleFault ? "015" : "014";
        string text = DecodeUtf8(File.ReadAllBytes(path)), candidate = Classify(text);
        Need(!Regex.IsMatch(candidate, "(?i)TEST_FAIL|OS_SMOKE_FAIL|" + (doubleFault ? "M02_DOUBLE_FAULT_FAILED" : "M02_PAGE_FAULT_CR2_ERROR_CODE_FAILED") + "|CLROS_M02_" + id + "_(?:HOST|PAGE_IMAGE_HOST|TRANSITION_HOST|EFI_HOST)_READY"), reason + "_failure_or_host_record");
        (string Pattern, int Count)[] counts = [(prefix + "_", doubleFault ? 11 : 9), ("CLROS_M02_" + id, 1), ("TEST_STARTm02\\." + id, 1), ("TEST_PASSm02\\." + id, 1), ("OS_SMOKE_PASS", 1)];
        foreach (var pair in counts) Need(Regex.Matches(candidate, "(?i)" + pair.Pattern).Count == pair.Count, reason + "_duplicate_or_disguised_record");
        if (doubleFault) M02TerminalPrefix(text, QemuContracts.M02DoubleFaultCaptureRecordCount, reason);
        const string anchor = "OS_BOOT_OK\r\nTEST_PASS boot.sequence\r\nOS_SMOKE_PASS\r\n";
        int start = text.IndexOf(anchor, StringComparison.Ordinal);
        Need(start >= 0, reason + "_smoke_terminal_missing");
        string[] lines = text[(start + anchor.Length)..].Split("\r\n", StringSplitOptions.None);
        int count = doubleFault ? 14 : 12;
        Need(lines.Length >= count + 1 && lines[count..].All(x => x.Length == 0) && (!doubleFault || lines.Length == count + 1), reason + "_exact_tail_extent");
        Need(lines[0] == "TEST_START m02." + id && lines[count - 2] == (doubleFault ? QemuContracts.M02DoubleFaultIstReadyMarker[0] : QemuContracts.M02PageFaultCr2ErrorCodeReadyMarker[0]) && lines[count - 1] == "TEST_PASS m02." + id, reason + "_tail_order");
        var v = new List<ulong>();
        const string suffix = " codePages=3 dataPages=10 tablePages=43 mappedPages=52";
        Need(lines[1].EndsWith(suffix, StringComparison.Ordinal), reason + "_layout_shape");
        v.AddRange(NamedHex(lines[1][..^suffix.Length], prefix + "_LAYOUT", "code", "data", "tables", "faultRip"));
        v.AddRange(NamedHex(lines[2], prefix + "_MODE", "firmwareCs", "cr0", "firmwareCr3", "cr4Before", "efer", "pat", "physicalBits"));
        v.AddRange(NamedHex(lines[3], prefix + "_ROOT", "cr3", "expectedCr3", "cr4", "expectedCr4"));
        string[] roles = ["zero", "df", "pf", "nmi", "transition"];
        for (int i = 0; i < roles.Length; i++) v.AddRange(NamedHex(lines[4 + i], prefix + "_ABSENT role=" + roles[i], "linear", "levelsRead", "pml4e", "pdpte", "pde", "pte"));
        string[] frame = ["vector", "hasErrorCode", "error", "cr2", "rip", "cs", "rflags", "savedRsp", "savedSs", "handlerRsp", "handlerSs", "cr3"];
        v.AddRange(NamedHex(lines[9], prefix + "_FRAME", doubleFault ? [.. frame, "handlerCs"] : frame));
        if (doubleFault)
        {
            v.AddRange(NamedHex(lines[10], prefix + "_DESCRIPTORS", "gdtrLimit", "gdtrBase", "tr", "idtrLimit", "idtrBase", "tssAccess", "ist1", "dfGateLow", "dfGateHigh", "pfGateLow", "pfGateHigh"));
            v.AddRange(NamedHex(lines[11], prefix + "_GPRS", "rax", "rbx", "rcx", "rdx", "rbp", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15"));
        }
        Need(v.Count == (doubleFault ? 84 : 57), reason + "_field_count");
        ulong c = v[0], d = v[1], t = v[2], rip = v[3], bits = v[10];
        Need(bits is >= 36 and <= 52, reason + "_physical_width");
        ulong limit = 1UL << (int)bits;
        ulong[] starts = [c, d, t], sizes = [12288, 40960, 176128];
        for (int i = 0; i < starts.Length; i++)
        {
            ulong a = starts[i], n = sizes[i];
            Need(a != 0 && a % 4096 == 0 && (decimal)a + n < 0x0000800000000000UL && (decimal)a + n < limit, reason + "_allocation_extent");
            for (int j = 0; j < i; j++) Need(a >= (decimal)starts[j] + sizes[j] || starts[j] >= (decimal)a + n, reason + "_allocation_overlap");
        }
        Need(rip == c + 4096 + (doubleFault ? 2698UL : 2841UL), reason + "_fault_rip");
        ulong cs = v[4], cr0 = v[5], firmwareRoot = v[6], before = v[7], efer = v[8], pat = v[9];
        Need(cs <= 65535 && (cs & 7) == 0 && (cs & 0xfff8) != 0 && (cr0 & 2147549185UL) == 2147549185UL && (before & 0x20) != 0 && (before & 0x01821000) == 0 && (efer & 0xd00) == 0xd00 && firmwareRoot < limit && (firmwareRoot & 0xfe7) == 0 && (firmwareRoot >> 12) != 0 && (pat & 255) == 6, reason + "_mode");
        for (int i = 0; i < 8; i++) Need(((pat >> (8 * i)) & 255) is 0 or 1 or 4 or 5 or 6 or 7, reason + "_pat");
        ulong after = before & ~0x80UL;
        Need(v[11] == t && v[12] == t && v[13] == after && v[14] == after, reason + "_root");
        ulong[] linear = [0, d + 0x2000, d + 0x4000, d + 0x6000, d + 0x8000];
        ulong[] mappedStarts = [c, d, d + 0x3000, d + 0x5000, d + 0x7000, d + 0x9000, t], mappedSizes = [12288, 8192, 4096, 4096, 4096, 4096, 176128];
        var slots = new Dictionary<(ulong Table, ulong Index), ulong>();
        var parents = new Dictionary<ulong, (ulong Table, ulong Index)>();
        for (int walk = 0; walk < linear.Length; walk++)
        {
            int offset = 15 + walk * 6; ulong levels = v[offset + 1];
            Need(v[offset] == linear[walk] && levels is >= 1 and <= 4, reason + "_absence_identity");
            int shift = 12 + 9 * (4 - (int)levels);
            ulong absentStart = (linear[walk] >> shift) << shift, absentEnd = absentStart + (1UL << shift);
            for (int i = 0; i < mappedStarts.Length; i++) Need(absentStart >= mappedStarts[i] + mappedSizes[i] || mappedStarts[i] >= absentEnd, reason + "_absence_overlaps_known_mapping");
            HashSet<ulong> visited = [t]; ulong table = t;
            for (int level = 0; level < 4; level++)
            {
                ulong entry = v[offset + 2 + level];
                if ((ulong)level >= levels) { Need(entry == 0, reason + "_nonzero_placeholder"); continue; }
                var slot = (table, (linear[walk] >> (39 - 9 * level)) & 511);
                ulong structural = entry & ~0x20UL;
                Need(!slots.TryGetValue(slot, out ulong known) || known == structural, reason + "_inconsistent_shared_slot"); slots[slot] = structural;
                if ((ulong)level == levels - 1) { Need(entry == 0, reason + "_nonzero_absence"); continue; }
                ulong flags = entry & 4095, target = (entry >> 12) << 12;
                Need(flags is 3 or 35 && entry < limit && target >= t && target < t + 176128 && visited.Add(target), reason + "_ancestor");
                Need(!parents.TryGetValue(target, out var parent) || parent == slot, reason + "_aliased_child_table"); parents[target] = slot; table = target;
            }
        }
        if (!doubleFault)
        {
            ulong[] expected = [14, 1, 0, 0, rip, cs, 0x10046, d + 0xa000, 0x10, d + 0x6000 - 192, 0, t];
            for (int i = 0; i < expected.Length; i++) Need(v[45 + i] == expected[i], reason + "_frame_" + i);
        }
        else
        {
            (int Index, ulong Value)[] expectedFrame = [(45, 8), (46, 1), (47, 0), (54, d + 0x4000 - 192), (55, 0), (56, t), (57, 8)];
            foreach (var pair in expectedFrame) Need(v[pair.Index] == pair.Value, reason + "_current_frame");
            var expected = new List<ulong> { 39, d + 4096 + 24, 24, 4095, d, 139, d + 0x4000 };
            (ulong Vector, ulong Ist, ulong Type)[] gates = [(8, 1, 142), (14, 2, 14)];
            foreach (var gate in gates) { ulong target = c + gate.Vector * 16; expected.Add((target & 65535) | (8UL << 16) | (gate.Ist << 32) | (gate.Type << 40) | ((target & 4294901760) << 32)); expected.Add(target >> 32); }
            for (int i = 0; i < expected.Count; i++) Need(v[58 + i] == expected[i], reason + "_descriptor_" + i);
        }
    }

    public static void AssertM02UnexpectedVectorPanicTranscript(string path)
    {
        const string reason = "m02_unexpected_vector_panic";
        string[] r = M02TerminalPrefix(DecodeUtf8(File.ReadAllBytes(path)), QemuContracts.M02UnexpectedCaptureRecordCount, reason);
        int first = QemuContracts.M02TerminalPrefix.Count;
        ulong[] m = NamedHex(r[first], "M02_UNEXPECTED_STATE", "c", "d", "context", "callback", "captureRsp", "firmwareCs", "cs", "ss", "ds", "es", "tr", "gdtrBase", "gdtrLimit", "idtrBase", "idtrLimit", "tssAccess", "gateLow", "gateHigh", "normalizer0", "normalizer1", "trigger", "cr3", "entryCr3", "lar");
        Need(r[first].Length == 648, reason + "_metadata_schema");
        ulong c = m[0], d = m[1], limit = 0x0000800000000000;
        Need(c != 0 && d != 0 && c % 4096 == 0 && d % 4096 == 0 && (decimal)c + 8192 <= limit && (decimal)d + 28672 <= limit && (c >= (decimal)d + 28672 || d >= (decimal)c + 8192) && m[3] != 0 && m[3] < limit && (m[3] < c || m[3] >= c + 8192) && (m[3] < d || m[3] >= d + 28672), reason + "_extent");
        ulong target = c + 512, gate = (target & 65535) | (8UL << 16) | (0x8eUL << 40) | ((target & 4294901760) << 32);
        Need(m[2] == d + 4096 && m[4] == d + 28488 && m[5] is > 0 and <= 65535 && (m[5] & 7) == 0 && m[6] == 8 && m[7] == 16 && m[8] == 16 && m[9] == 16 && m[10] == 24 && m[11] == d + 4120 && m[12] == 39 && m[13] == d && m[14] == 4095 && m[15] == 139 && m[16] == gate && m[17] == target >> 32 && m[18] == 0x00002068006a006a && m[19] == 0x909000000df2e900 && m[20] == c + 5056 && m[21] is > 0 and <= 0x000fffffffffffff && (m[21] & 0xfe7) == 0 && (m[21] & 0x000ffffffffff000) != 0 && m[22] == m[21] && m[23] <= uint.MaxValue && (m[23] & 0x00f0fe00) == 0x00a09a00, reason + "_current_state");
        ulong[] f = NamedHex(r[first + 1], "OS_SMOKE_FAIL subsystem=idt reason=unexpected-vector vector=32 errorCodePresent=false errorCode=0x0000000000000000", "rip", "rsp", "rbp", "rflags", "cr2", "cr3", "rax", "rbx", "rcx", "rdx", "rsi", "rdi", "r8", "r9", "r10", "r11", "r12", "r13", "r14", "r15");
        Need(f[0] == m[20] + 2 && f[1] == d + 28672 && (f[3] & 0x24702) == 2 && (f[3] & 0x8d5) == 0x44 && f[5] == m[21] && f[6] == 24 && f[8] == m[23] && f[9] == m[2] && f[12] == m[21], reason + "_fault_state");
    }
}
