using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

/// <summary>Independent admission of captured bytes. Classification may strip noise; accepted records never do.</summary>
public static partial class QemuTranscripts
{
    public const int FirmwarePrefixBytes = QemuContracts.FirmwarePrefixBytes;
    public const string FirmwarePrefixSha256 = QemuContracts.FirmwarePrefixSha256;
    public const string FirmwareSuffixSha256 = "BFBB6B92A5EEE7728E1AA54A2A4D262135215D7D2F267696E508A23BD514C61D";
    public const string ShortFirmwareSuffixSha256 = "011BC86409F7A01C1494F57146920B37184A02063B7E995B44A99C6B083E6615";
    internal static readonly UTF8Encoding StrictUtf8 = new(false, true);
    internal static string DecodeUtf8(byte[] bytes)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException) { throw new ToolFailure("transcript_malformed_utf8"); }
    }
    internal static readonly Regex Controls = new(@"[\x00-\x1f\x7f-\uffff]", RegexOptions.CultureInvariant);
    internal static void Need(bool value, string reason)
    {
        if (!value)
            throw new ToolFailure(reason);
    }
    internal static string Classify(string text) => Regex.Replace(text, @"[\x00-\x20\x7f-\uffff]", "");
    internal static ulong Number(string token)
    {
        bool hex = token.StartsWith("0x", StringComparison.Ordinal);
        Need(ulong.TryParse(hex ? token[2..] : token, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
            CultureInfo.InvariantCulture, out ulong n), "transcript_numeric_overflow");
        return n;
    }
    public static Dictionary<string, ulong> ReadRecord(string line, string prefix, params string[] fields)
    {
        string pattern = "\\A" + Regex.Escape(prefix);
        foreach (string f in fields)
        {
            string[] p = f.Split(':');
            pattern += " " + Regex.Escape(p[0]) + "=" + (p[1] switch
            {
                "hex" => "(0x[0-9A-F]{16})",
                "rawhex" => "([0-9A-F]{16})",
                "false" => "(false)",
                "true" => "(true)",
                _ => "(0|[1-9][0-9]*)"
            });
        }
        Match match = Regex.Match(line, pattern + "\\z", RegexOptions.CultureInvariant);
        Need(match.Success, "transcript_record_shape_" + prefix);
        Dictionary<string, ulong> result = new(StringComparer.Ordinal);
        for (int i = 0; i < fields.Length; i++)
        {
            string[] f = fields[i].Split(':');
            result.Add(f[0], f[1] == "false" ? 0 : f[1] == "true" ? 1 : Number((f[1] == "rawhex" ? "0x" : "") + match.Groups[i + 1].Value));
        }
        return result;
    }
    public static ulong[] ReadHexRecord(string line, string prefix, int count)
    {
        Match m = Regex.Match(line, "\\A" + Regex.Escape(prefix) + "((?: 0x[0-9A-F]{16}){" + count + "})\\z", RegexOptions.CultureInvariant);
        Need(m.Success, "transcript_hex_record_shape_" + prefix);
        return m.Groups[1].Value[1..].Split(' ').Select(Number).ToArray();
    }
    internal static bool Fits(ulong start, ulong length, ulong limit = ulong.MaxValue) => start != 0 && (decimal)start + length <= limit;
    internal static void DistinctExtents(IEnumerable<(ulong Start, ulong Length)> extents, string reason)
    {
        var seen = new List<(decimal Start, decimal End)>();
        foreach (var (start, length) in extents)
        {
            decimal end = (decimal)start + length;
            Need(start != 0 && start % 8 == 0 && end <= ulong.MaxValue, reason + "_extent");
            Need(seen.All(p => start >= p.End || p.Start >= end), reason + "_alias");
            seen.Add((start, end));
        }
    }
    internal static string[] FirmwareProtocol(string path, string terminal, bool noSuffix, bool allowShortSuffix, string reason, bool blockBoot = false,
        NativeAotHelloProgram? helloProgram = null, NativeAotThreadingProgram? threadingProgram = null, NativeAotRuntimeProgram? runtimeProgram = null,
        bool linuxThreads = false, NativeAotSocketProgram? socketProgram = null, NativeAotSocketContainmentProgram? containmentProgram = null, NativeAotSocketVectorsProgram? vectorsProgram = null)
    {
        byte[] bytes = File.ReadAllBytes(path);
        int prefixBytes = blockBoot ? QemuContracts.BlockFirmwarePrefixBytes : FirmwarePrefixBytes;
        string prefixHash = blockBoot ? QemuContracts.BlockFirmwarePrefixSha256 : FirmwarePrefixSha256;
        Need(helloProgram is null || threadingProgram is null, "threading_hello_conflict");
        Need(runtimeProgram is null || helloProgram is null && threadingProgram is null, "runtime_program_conflict");
        Need(socketProgram is null || helloProgram is null && threadingProgram is null && runtimeProgram is null, "socket_program_conflict");
        Need(containmentProgram is null || helloProgram is null && threadingProgram is null && runtimeProgram is null && socketProgram is null, "socket_containment_program_conflict");
        Need(vectorsProgram is null || helloProgram is null && threadingProgram is null && runtimeProgram is null && socketProgram is null && containmentProgram is null, "socket_vectors_program_conflict");
        // Thread diagnostics include complete call/return/wait images. The
        // declared profile still requires its exact bounded leaf afterward.
        Need(bytes.Length >= prefixBytes && bytes.Length <= (!linuxThreads && helloProgram is null && threadingProgram is null && runtimeProgram is null ? 32768 : 8 * 1024 * 1024), reason + "_raw_extent");
        string text = DecodeUtf8(bytes);
        int start = text.IndexOf("TEST_START efi.main\r\n", StringComparison.Ordinal);
        int terminalAt = terminal.Length == 0 ? text.Length : text.IndexOf(terminal, StringComparison.Ordinal);
        int end = terminalAt + terminal.Length;
        Need(start == prefixBytes && terminalAt >= start, reason + "_protocol_extent");
        Need(Convert.ToHexString(SHA256.HashData(bytes.AsSpan(0, prefixBytes))) == prefixHash,
            reason + "_firmware_prefix");
        int suffixLength = bytes.Length - end;
        if (noSuffix)
            Need(suffixLength == 0, reason + "_firmware_eof");
        else
        {
            string digest = Convert.ToHexString(SHA256.HashData(bytes.AsSpan(end)));
            Need(suffixLength == 6945 && digest == FirmwareSuffixSha256 ||
                allowShortSuffix && suffixLength == 251 && digest == ShortFirmwareSuffixSha256, reason + "_firmware_suffix");
        }
        string owned = text[start..end];
        if (helloProgram is not null)
            owned = NormalizeHelloWrites(owned);
        if (threadingProgram is not null)
            owned = NormalizeThreadingWrites(owned, threadingProgram.Application);
        if (runtimeProgram is not null)
            owned = NormalizeRuntimeWrites(owned, runtimeProgram.Application);
        if (socketProgram is not null)
            owned = NormalizeSocketWrites(owned, socketProgram.Asynchronous);
        if (containmentProgram is not null)
            owned = NormalizeSocketContainmentWrites(owned);
        if (vectorsProgram is not null)
            owned = NormalizeSocketVectorWrites(owned);
        Need(!Regex.IsMatch(owned, @"[^\x20-\x7E\r\n]"), reason + "_non_ascii");
        string[] split = owned.Split("\r\n", StringSplitOptions.None);
        Need(split[^1].Length == 0 && split[..^1].All(x => !x.Contains('\r') && !x.Contains('\n')), reason + "_framing");
        return split[..^1];
    }
    internal static void ExactInventory(IReadOnlyList<string> lines, IReadOnlyList<string> expected, params int[] variable)
    {
        Need(lines.Count == expected.Count, "transcript_record_count");
        HashSet<int> exceptions = [.. variable];
        for (int i = 0; i < lines.Count; i++)
            if (!exceptions.Contains(i))
                Need(lines[i] == expected[i], "transcript_exact_record_" + i);
    }
    internal static void ValidateAllocator(IReadOnlyList<string> lines, int descriptor = 3, int regions = 9, int allocator = 10)
    {
        ulong Count(int index, string prefix)
        {
            Match m = Regex.Match(lines[index], "\\A" + Regex.Escape(prefix) + " ([1-9][0-9]*)\\z");
            Need(m.Success, "transcript_region_shape");
            ulong n = Number(m.Groups[1].Value);
            Need(n <= 4096, "transcript_region_bound");
            return n;
        }
        ulong count = Count(descriptor, "UEFI_MEMORY_MAP_DESCRIPTOR_COUNT");
        Need(count == Count(regions, "BOOT_MEMORY_REGION_COUNT"), "transcript_region_association");
        var a = ReadRecord(lines[allocator], "BOOT_PAGE_ALLOCATOR_READY status=Ready", "regions:dec", "conventionalPages:dec",
            "conventionalBytes:dec", "freeNodes:dec", "freePages:dec", "freeBytes:dec", "pageSize:dec", "statusCode:dec");
        Need(a["regions"] == count && a["freeNodes"] >= 1 && a["freeNodes"] <= count && a["conventionalPages"] >= a["freeNodes"] &&
            a["conventionalPages"] == a["freePages"] && (decimal)a["conventionalPages"] * 4096 == a["conventionalBytes"] &&
            a["conventionalBytes"] == a["freeBytes"] && a["pageSize"] == 4096 && a["statusCode"] == 0, "transcript_allocator_accounting");
    }
    public static void AssertRunnerTerminated(IReadOnlyList<string> lines, string reason = "qemu")
    {
        Need(!lines.Any(l => Regex.IsMatch(Classify(l), @"(?i)\A(?:TEST_FAIL|OS_SMOKE_FAIL|QEMU_PROCESS_CLEANUP_FAILED)")), reason + "_runner_failure");
        string[] results = lines.Where(l => l.StartsWith("QEMU_PROCESS_RESULT ", StringComparison.Ordinal)).ToArray();
        string[] drains = lines.Where(l => l.StartsWith("SERIAL_MARKER_DRAIN ", StringComparison.Ordinal)).ToArray();
        Need(results.Length == 1 && drains.Length == 1 && drains[0] == "SERIAL_MARKER_DRAIN milliseconds=750", reason + "_terminal_disposition");
        Match m = Regex.Match(results[0], @"\AQEMU_PROCESS_RESULT disposition=runner-terminated exitCode=(0|-?[1-9][0-9]*)\z");
        Need(m.Success && int.TryParse(m.Groups[1].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _), reason + "_native_exit_invalid");
    }
    public static void AssertM04ManagedHeapRunnerResult(string[] lines) => AssertRunnerTerminated(lines, "m04_heap");
    public static void AssertM02PageFaultRunnerResult(string[] lines) => AssertRunnerTerminated(lines, "m02_page_fault");
    public static void AssertM02DoubleFaultIstRunnerResult(string[] lines) => AssertRunnerTerminated(lines, "m02_double_fault");
    public static void AssertM03MemoryMapCaptureRunnerResult(string[] lines) => AssertRunnerTerminated(lines, "m03_capture");
}
