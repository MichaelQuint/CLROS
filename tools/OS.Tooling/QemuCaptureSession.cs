using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public sealed class QemuCaptureSession : IDisposable
{
    public string Executable { get; private init; } = "";
    public string ObservationPath { get; private init; } = "";
    public string PublicSerialPath { get; private init; } = "";
    public string PrivateSerialPath { get; private init; } = "";
    public string QmpTranscriptPath { get; private init; } = "";
    public string SupportDirectory { get; private init; } = "";
    public string ScratchDirectory { get; private init; } = "";
    public int GuestTimeoutSeconds { get; private init; }
    public string Accelerator { get; private init; } = "tcg";
    public const int CaptureTimeoutSeconds = 60;
    /// <summary>Extra runner lifetime reserved for raw capture and final cleanup,
    /// after the selected guest execution budget.</summary>
    public const int RunnerCaptureAllowanceSeconds = CaptureTimeoutSeconds + 15;
    public int RunnerTimeoutSeconds => GuestTimeoutSeconds + RunnerCaptureAllowanceSeconds;
    public IReadOnlyDictionary<string, string?> Environment { get; private init; } = new Dictionary<string, string?>();
    public IReadOnlyDictionary<string, string> Identities { get; private init; } = new Dictionary<string, string>();
    private static void Need(bool value, string reason) => QemuTranscripts.Need(value, reason);
    private static string Hash(string path) { using var file = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(file)); }

    public static QemuCaptureSession Create(string root, string qemu, string observation, string serial, string summary, int timeout, bool safeStub, string accelerator = "tcg")
    {
        Need(accelerator is "whpx" or "tcg", "capture_accelerator_invalid");
        Need(timeout is >= 1 and <= 300, "guest_timeout_out_of_range");
        string publicPath = Path.GetFullPath(serial, root);
        string observationPath = string.IsNullOrWhiteSpace(observation) ? publicPath + ".m01-raw-" + Guid.NewGuid().ToString("N") + ".log" : Path.GetFullPath(observation, root);
        Need(!File.Exists(observationPath) && !Directory.Exists(observationPath), "observation_log_already_exists");
        string parent = Path.GetDirectoryName(observationPath)!;
        string support = Path.Combine(parent, "m01-026-raw-" + Guid.NewGuid().ToString("N"));
        string privatePath = Path.Combine(support, "private-serial.log"), qmp = Path.Combine(support, "qmp-transcript.log"), scratch = Path.Combine(support, "capture-scratch");
        string[] paths = [observationPath, publicPath, Path.GetFullPath(summary, root), privatePath, qmp, scratch];
        Need(paths.Distinct(StringComparer.OrdinalIgnoreCase).Count() == paths.Length, "capture_path_overlap");
        string dll = HostTools.ManagedTool(root, "OS.QemuCapture");
        string executable = Path.ChangeExtension(dll, OperatingSystem.IsWindows() ? ".exe" : null);
        Need(File.Exists(executable), "shim_executable_missing");
        var identities = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["realQemuSha256"] = Hash(qemu), ["sourceSha256"] = Hash(Path.Combine(root, "tools", "OS.QemuCapture", "Program.cs")),
            ["projectSha256"] = Hash(Path.Combine(root, "tools", "OS.QemuCapture", "OS.QemuCapture.csproj")),
            ["contractSha256"] = Hash(Path.Combine(root, "tools", "OS.Tooling", "QemuContracts.cs")),
            ["shimSha256"] = Hash(executable), ["assemblySha256"] = Hash(dll),
            ["runtimeConfigSha256"] = Hash(Path.ChangeExtension(dll, ".runtimeconfig.json")),
            ["dependenciesSha256"] = Hash(Path.ChangeExtension(dll, ".deps.json"))
        };
        Directory.CreateDirectory(support);
        return new()
        {
            Executable = executable, ObservationPath = observationPath, PublicSerialPath = publicPath, PrivateSerialPath = privatePath,
            QmpTranscriptPath = qmp, SupportDirectory = support, ScratchDirectory = scratch, GuestTimeoutSeconds = timeout, Accelerator = accelerator, Identities = identities,
            Environment = new Dictionary<string, string?>
            {
                ["CLROS_M01_REAL_QEMU"] = Path.GetFullPath(qemu), ["CLROS_M01_PRIVATE_SERIAL"] = privatePath,
                ["CLROS_M01_RAW_OBSERVATION_LOG"] = observationPath, ["CLROS_M01_QMP_TRANSCRIPT_LOG"] = qmp,
                ["CLROS_M01_CAPTURE_SCRATCH"] = scratch, ["CLROS_M01_GUEST_TIMEOUT_SECONDS"] = timeout.ToString(CultureInfo.InvariantCulture),
                ["CLROS_M01_CAPTURE_TIMEOUT_SECONDS"] = CaptureTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
                ["CLROS_M01_ACCELERATOR"] = accelerator,
                ["CLROS_M01_CAPTURE_EXECUTABLE"] = Path.GetFullPath(executable),
                ["CLROS_M01_CAPTURE_SHA256"] = identities["shimSha256"],
                ["CLROS_M02_SAFE_STUB_PROBE"] = safeStub ? "true" : "false"
            }
        };
    }

    public static void ValidateFiles(string observation, string privateSerial, string publicSerial, string qmpTranscript, string scratch, int timeout = 15)
        => new QemuCaptureSession { ObservationPath = observation, PrivateSerialPath = privateSerial, PublicSerialPath = publicSerial,
            QmpTranscriptPath = qmpTranscript, ScratchDirectory = scratch, GuestTimeoutSeconds = timeout }.Validate();

    public void Validate()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in File.ReadAllLines(ObservationPath, new UTF8Encoding(false, true)))
        {
            int at = line.IndexOf('='); Need(at > 0 && at < line.Length - 1, "observation_sidecar_record_invalid");
            Need(values.TryAdd(line[..at], line[(at + 1)..]), "observation_sidecar_duplicate_key");
        }
        string[] keys = ("schema captureMethod guestMemoryBytes captureChunkBytes guestTimeoutSeconds captureTimeoutSeconds terminalWithheld qmpPaused qmpMemoryShapeValidated memoryDumpRetained memorySha256 preflightCandidateCount preflightPhysicalAddress preflightPhysicalAddresses preflightPodSha256 preflightPodBase64 cr0Before firmwareCr3Before rawCr4Before ia32EferBefore cpuidMaximumExtendedLeaf cpuidAddressSizesEax physicalAddressWidth physicalAddressMask ownedCr3 rflagsBefore runtimeStatePhysicalAddress runtimeStatePodSha256 runtimeStatePodBase64 transitionCanaryPhysicalAddress transitionCanaryValue callGatePhysicalAddress callGatePodSha256 callGatePodBase64 firmwareCr3After rawCr4After rflagsAfter runtimeFirmwareCr3Readback runtimeRestoredCr4Readback runtimeRestoredRflagsIntent privateSerialSha256 predictedPublicSerialSha256 exactEqualityValidation terminalReleaseAuthorized").Split(' ');
        Need(values.Count == keys.Length && keys.All(values.ContainsKey), "observation_sidecar_key_count_mismatch");
        var fixedValues = new Dictionary<string, string>
        {
            ["schema"] = "clros.m01.raw-observations.v1", ["captureMethod"] = "qmp-pmemsave-stopped-before-terminal-release",
            ["guestMemoryBytes"] = "268435456", ["captureChunkBytes"] = "16777216",
            ["guestTimeoutSeconds"] = GuestTimeoutSeconds.ToString(CultureInfo.InvariantCulture), ["captureTimeoutSeconds"] = CaptureTimeoutSeconds.ToString(CultureInfo.InvariantCulture),
            ["terminalWithheld"] = "true", ["qmpPaused"] = "true", ["qmpMemoryShapeValidated"] = "true", ["memoryDumpRetained"] = "false",
            ["exactEqualityValidation"] = "pass", ["terminalReleaseAuthorized"] = "true"
        };
        foreach (var pair in fixedValues) Need(values[pair.Key] == pair.Value, "observation_sidecar_value_" + pair.Key);
        Need(int.TryParse(values["preflightCandidateCount"], NumberStyles.None, CultureInfo.InvariantCulture, out int count) && count >= 1, "observation_preflight_candidate_count_invalid");
        string[] addresses = values["preflightPhysicalAddresses"].Split(';');
        Need(addresses.Length == count && addresses.Distinct(StringComparer.Ordinal).Count() == count, "observation_preflight_address_count_mismatch");
        foreach (string address in addresses)
        {
            Need(Regex.IsMatch(address, @"\A0x[0-9A-F]{16}\z"), "observation_preflight_address_invalid");
            ulong n = QemuTranscripts.Number(address);
            Need(n <= 268435456 - 56 && n % 8 == 0, "observation_preflight_address_out_of_range");
        }
        Need(values["preflightPhysicalAddress"] == addresses[0], "observation_preflight_primary_address_mismatch");
        foreach (string key in new[] { "memorySha256", "preflightPodSha256", "runtimeStatePodSha256", "callGatePodSha256", "privateSerialSha256", "predictedPublicSerialSha256" })
            Need(Regex.IsMatch(values[key], @"\A[0-9A-F]{64}\z"), "observation_hash_invalid_" + key);
        string[] hex64Keys = "preflightPhysicalAddress cr0Before firmwareCr3Before rawCr4Before ia32EferBefore physicalAddressMask ownedCr3 rflagsBefore runtimeStatePhysicalAddress transitionCanaryPhysicalAddress transitionCanaryValue callGatePhysicalAddress firmwareCr3After rawCr4After rflagsAfter runtimeFirmwareCr3Readback runtimeRestoredCr4Readback runtimeRestoredRflagsIntent".Split(' ');
        foreach (string key in hex64Keys) Need(Regex.IsMatch(values[key], @"\A0x[0-9A-F]{16}\z"), "observation_hex64_invalid_" + key);
        foreach (string key in new[] { "cpuidMaximumExtendedLeaf", "cpuidAddressSizesEax" }) Need(Regex.IsMatch(values[key], @"\A0x[0-9A-F]{8}\z"), "observation_hex32_invalid_" + key);
        byte[] Pod(string name, int length)
        {
            byte[] bytes;
            try { bytes = Convert.FromBase64String(values[name + "Base64"]); } catch (FormatException) { throw new ToolFailure("observation_pod_base64_invalid"); }
            Need(bytes.Length == length, "observation_pod_length_invalid");
            Need(Convert.ToHexString(SHA256.HashData(bytes)) == values[name + "Sha256"], "observation_pod_hash_mismatch"); return bytes;
        }
        byte[] pre = Pod("preflightPod", 56), runtime = Pod("runtimeStatePod", 480), gate = Pod("callGatePod", 160);
        ulong P(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(pre.AsSpan(offset));
        ulong R(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(runtime.AsSpan(offset));
        ulong G(int offset) => BinaryPrimitives.ReadUInt64LittleEndian(gate.AsSpan(offset));
        uint P32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(pre.AsSpan(offset));
        uint R32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(runtime.AsSpan(offset));
        uint G32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(gate.AsSpan(offset));
        ulong cr0 = P(0), cr3 = P(8), cr4 = P(16), efer = P(24), rflags = P(40);
        uint maxLeaf = P32(32), addressSizes = P32(36);
        Need(int.TryParse(values["physicalAddressWidth"], NumberStyles.None, CultureInfo.InvariantCulture, out int width) && width == (addressSizes & 255) && width is >= 36 and <= 52, "observation_physical_width_mismatch");
        Need(P32(48) == 0x31464c50 && P32(52) == 0, "observation_preflight_structure_invalid");
        ulong mask = ulong.MaxValue >> (64 - width), pageMask = mask & ~0xfffUL, allowedCr3 = pageMask | 0x18, root = cr3 & pageMask;
        Need(maxLeaf >= 2147483656 && (cr0 & 2147549185) == 2147549185 && (cr4 & 0x20) != 0 && (cr4 & 0x00821000) == 0 && (efer & 0xd00) == 0xd00 &&
            (rflags & 2) != 0 && (rflags & 0x500) == 0 && R(240) == mask && (cr3 & allowedCr3) == cr3 && root != 0, "observation_mode_or_root_invalid");
        ulong runtimeAddress = QemuTranscripts.Number(values["runtimeStatePhysicalAddress"]), canaryAddress = QemuTranscripts.Number(values["transitionCanaryPhysicalAddress"]), gateAddress = QemuTranscripts.Number(values["callGatePhysicalAddress"]);
        Need(runtimeAddress != 0 && runtimeAddress % 4096 == 0 && runtimeAddress <= 268435456 - 0x3a0, "observation_runtime_address_invalid");
        Need(canaryAddress == runtimeAddress + 480, "observation_transition_canary_address_mismatch");
        Need(gateAddress == runtimeAddress + 0x300, "observation_call_gate_address_mismatch");
        ulong codeBase = R(48), codeEnd = R(56), returnAddress = R(80), expectedCanary = R(160), replacementCanary = R(168), ownedCr3 = R(8), runtimeFlags = R(200), runtimeCr3 = R(216), runtimeCr4 = R(224);
        byte pge = (runtimeCr4 & 0x80) != 0 ? (byte)1 : (byte)0, interrupts = (runtimeFlags & 0x200) != 0 ? (byte)1 : (byte)0;
        Need(R32(0) == 480 && BinaryPrimitives.ReadUInt16LittleEndian(runtime.AsSpan(4)) == 1 && runtime[6] == 1 && runtime[7] == 0 && R32(180) == 0 && R(184) == runtimeAddress && R(472) == 0, "observation_runtime_structure_invalid");
        Need(R32(176) == 0x4f534f4b && R(360) == 0x4f534f4b && R(448) == 1 && R(456) == 1 && runtime[464] == 1 && runtime[465] == 17 && runtime[466] is >= 1 and <= 4 && runtime[467] == 1 && runtime[468] == pge && runtime[469] == 1 && runtime[470] == 1 && runtime[471] == 12, "observation_runtime_completion_invalid");
        Need(pageMask != 0 && (ownedCr3 & allowedCr3) == ownedCr3 && (ownedCr3 & pageMask) != 0 && (ownedCr3 & pageMask) != root &&
            (R(272) & ~0xad5UL) == (runtimeFlags & ~0xad5UL) && (R(272) & 0x200) == 0 && R(280) == (runtimeCr4 & ~0x80UL) && R(288) == ownedCr3 &&
            R(296) == expectedCanary && R(304) == replacementCanary && R(416) == replacementCanary && R(408) == runtimeCr3 && R(424) == runtimeCr4 && R(432) == runtimeFlags && R(248) == R(440), "observation_runtime_readback_invalid");
        ulong gateCanary = G(72);
        Need(G32(0) == 160 && BinaryPrimitives.ReadUInt16LittleEndian(gate.AsSpan(4)) == 1 && gate[6] == 1 && gate[7] == 3 && G(8) == gateAddress && G(16) == codeBase && G(24) == codeBase && G(32) == codeBase + 0xf00 && G(40) == returnAddress &&
            returnAddress == codeBase + 0xfc9 && codeEnd == codeBase + 0x1000 && G(48) == runtimeFlags && G(56) == runtimeCr3 && G(64) == runtimeCr4 && gateCanary != 0 && G32(80) == 1 && gate[84] == interrupts && gate[85] == pge && gate[86] == 1 && gate[87] == 1 &&
            G(88) == gateCanary && G(96) == gateCanary && G(104) == runtimeFlags && G(112) == runtimeCr3 && G(120) == runtimeCr4 && G(128) == gateCanary && G32(136) == 1 && gate[140] == 1 && gate[141] == 1 && BinaryPrimitives.ReadUInt16LittleEndian(gate.AsSpan(142)) == 0 &&
            G32(144) == 0x4b47534f && G32(148) == 0 && G(152) == 0, "observation_call_gate_structure_invalid");
        (string Key, ulong Value)[] associations = [("cr0Before", cr0), ("firmwareCr3Before", cr3), ("rawCr4Before", cr4), ("ia32EferBefore", efer), ("physicalAddressMask", R(240)), ("ownedCr3", ownedCr3), ("transitionCanaryValue", replacementCanary), ("rflagsBefore", rflags),
            ("firmwareCr3After", G(112)), ("rawCr4After", G(120)), ("rflagsAfter", G(104)), ("runtimeFirmwareCr3Readback", R(408)), ("runtimeRestoredCr4Readback", R(424)), ("runtimeRestoredRflagsIntent", R(432))];
        foreach (var a in associations) Need(values[a.Key] == "0x" + a.Value.ToString("X16", CultureInfo.InvariantCulture), "observation_pod_field_mismatch_" + a.Key);
        Need(values["cpuidMaximumExtendedLeaf"] == "0x" + maxLeaf.ToString("X8", CultureInfo.InvariantCulture) && values["cpuidAddressSizesEax"] == "0x" + addressSizes.ToString("X8", CultureInfo.InvariantCulture), "observation_cpuid_field_mismatch");
        Need(cr0 == R(208) && cr3 == runtimeCr3 && cr4 == runtimeCr4 && efer == R(232) && cr3 == G(112) && cr4 == G(120) && rflags == runtimeFlags && rflags == G(104), "observation_exact_restoration_mismatch");
        Need(File.Exists(PrivateSerialPath) && File.Exists(PublicSerialPath), "observation_serial_missing");
        string privateHash = Hash(PrivateSerialPath), publicHash = Hash(PublicSerialPath);
        Need(values["privateSerialSha256"] == privateHash && values["predictedPublicSerialSha256"] == publicHash && privateHash == publicHash, "observation_serial_hash_mismatch");
        Need(File.Exists(QmpTranscriptPath), "observation_qmp_transcript_missing");
        string[] qmp = File.ReadAllLines(QmpTranscriptPath);
        foreach (string required in new[] { "qmpMemoryShapeValidated=true", "qmpPaused=true", "M01_RAW_OBSERVATION_CAPTURED", "publicSerialSha256=" + publicHash, "privateSerialSha256=" + privateHash, "terminalReleaseAuthorized=true", "terminalReleased=true" })
            Need(qmp.Contains(required, StringComparer.Ordinal), "observation_qmp_transcript_missing_" + required);
        Need(!Directory.Exists(ScratchDirectory) || Directory.GetFileSystemEntries(ScratchDirectory).Length == 0, "observation_capture_data_retained");
    }

    public void Dispose()
    {
        string prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(SupportDirectory)) + Path.DirectorySeparatorChar;
        foreach (string path in new[] { PrivateSerialPath, ScratchDirectory })
            Need(Path.GetFullPath(path).StartsWith(prefix, StringComparison.OrdinalIgnoreCase), "capture_cleanup_outside_support_directory");
        if (File.Exists(PrivateSerialPath)) File.Delete(PrivateSerialPath);
        if (Directory.Exists(ScratchDirectory)) Directory.Delete(ScratchDirectory, recursive: true);
    }
}
