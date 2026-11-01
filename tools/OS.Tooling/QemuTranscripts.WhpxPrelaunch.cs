using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OS.Tooling.Native;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // File facts are consumed only inside the original surviving launch owner,
    // after its retained Windows job await. Copies alone grant no execution.
    internal static void AssertWhpxJoinedFile(string root, string serial, string qemu, string rawKey,
        string ownerHash, ProcessResult original, int outputCharacterLimit)
    {
        Need(original.TerminationConfirmed && original.StandardErrorExternallyOwned &&
            original.FailureReason.Length == 0 && !original.TimedOut && !original.OutputLimitExceeded &&
            original.StandardError.Length == 0, "whpx_joined_original_required");
        string directory = Path.GetFullPath(serial, root) + ".whpx-prelaunch";
        var inputs = new List<(string Path, string Key, byte[] Bytes)>();
        var leases = new List<FileStream>();
        try
        {
        byte[] Read(string name, int bound)
        {
            string path = Path.Combine(directory, name);
            var input = new FileStream(path, FileMode.Open, FileAccess.Read,
                name is "ownership.json" or "stderr.raw" ? FileShare.ReadWrite : FileShare.Read);
            leases.Add(input);
            var before = NativeFile.GetIdentity(input.SafeFileHandle);
            long length = input.Length;
            Need(before.LinkCount == 1 && before.Length == (ulong)length && length <= bound, "whpx_file_extent");
            byte[] bytes = new byte[checked((int)length)]; input.ReadExactly(bytes);
            var after = NativeFile.GetIdentity(input.SafeFileHandle);
            Need(before.Key == after.Key && before.Length == after.Length && input.Length == length, "whpx_file_changed");
            inputs.Add((path, before.Key, bytes));
            return bytes;
        }
        static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
        static void Unique(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Object)
            {
                var fields = value.EnumerateObject().ToArray();
                Need(fields.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() == fields.Length, "whpx_json_duplicate");
                foreach (var field in fields) Unique(field.Value);
            }
            else if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) Unique(item);
        }
        JsonDocument Document(byte[] bytes)
        {
            var document = JsonDocument.Parse(bytes);
            try { Unique(document.RootElement); return document; } catch { document.Dispose(); throw; }
        }
        static string Text(JsonElement value, string name) => value.GetProperty(name).GetString() ?? "";
        static bool Same(JsonElement a, JsonElement b) => JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);

        byte[] ownerBytes = Read("ownership.json", 65536), sealBytes = Read("seal.json", 65536),
            resumeBytes = Read("resume.json", 65536), qmpBytes = Read("qmp.log", 262144), raw = Read("stderr.raw", 65536);
        using var ownerDocument = Document(ownerBytes);
        using var sealDocument = Document(sealBytes);
        using var resumeDocument = Document(resumeBytes);
        var owner = ownerDocument.RootElement; var seal = sealDocument.RootElement; var resume = resumeDocument.RootElement;
        string[] stdout = original.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        int childAt = Array.FindIndex(stdout, x => x.StartsWith("QEMU_WHPX_CHILD ", StringComparison.Ordinal));
        int resumeAt = Array.FindIndex(stdout, x => x.StartsWith("QEMU_WHPX_RESUME ", StringComparison.Ordinal));
        Need(childAt >= 0 && resumeAt > childAt &&
            stdout.Count(x => x.StartsWith("QEMU_WHPX_CHILD", StringComparison.Ordinal)) == 1 &&
            stdout.Count(x => x.StartsWith("QEMU_WHPX_RESUME", StringComparison.Ordinal)) == 1, "whpx_original_phase_anchors");
        using var childAnchorDocument = Document(Encoding.UTF8.GetBytes(stdout[childAt][16..]));
        using var resumeAnchorDocument = Document(Encoding.UTF8.GetBytes(stdout[resumeAt][17..]));
        var childAnchor = childAnchorDocument.RootElement; var resumeAnchor = resumeAnchorDocument.RootElement;
        Need(childAnchor.EnumerateObject().Count() == 3 && resumeAnchor.EnumerateObject().Count() == 6 &&
            Text(childAnchor, "OwnerSha256") == ownerHash && Text(resumeAnchor, "OwnerSha256") == ownerHash &&
            childAnchor.GetProperty("ProcessId").GetInt32() == seal.GetProperty("ProcessId").GetInt32() &&
            childAnchor.GetProperty("ProcessId").GetInt32() == resumeAnchor.GetProperty("ProcessId").GetInt32() &&
            Text(childAnchor, "StartUtc") == Text(seal, "StartUtc") &&
            Text(childAnchor, "StartUtc") == Text(resumeAnchor, "StartUtc") &&
            Text(resumeAnchor, "SealSha256") == Hash(sealBytes) && Text(resumeAnchor, "ResumeSha256") == Hash(resumeBytes) &&
            Text(resumeAnchor, "QmpSha256") == Hash(qmpBytes), "whpx_original_phase_anchor_binding");
        Need(Hash(ownerBytes) == ownerHash && Text(owner, "Schema") == "clros.whpx.outer-file-owner.v2" &&
            Text(owner, "Mode") == "surviving-original-owner" && Text(owner, "RawIdentity") == rawKey &&
            Text(owner, "Qemu") == qemu && Text(owner, "Serial") == Path.GetFullPath(serial, root) &&
            Text(owner, "Raw") == Path.Combine(directory, "stderr.raw") &&
            Text(owner, "QemuSha256") == QemuContracts.WhpxPrelaunchQemuSha256 &&
            Text(owner, "Machine") == QemuContracts.WhpxPrelaunchMachine, "whpx_outer_owner_identity");
        Need(Text(seal, "Schema") == "clros.whpx.prelaunch-seal.v2" &&
            Text(seal, "Transport") == "inherited-standard-error-file" && Text(seal, "OwnerSha256") == ownerHash &&
            Text(seal, "QemuSha256") == QemuContracts.WhpxPrelaunchQemuSha256 &&
            Text(seal, "Executable") == qemu && Text(seal, "RawIdentity") == rawKey &&
            seal.GetProperty("ContCommandsBeforeSeal").GetInt32() == 0, "whpx_seal_identity");
        Need(inputs[^1].Key == rawKey, "whpx_raw_identity");
        int prefix = seal.GetProperty("PrefixBytes").GetInt32();
        string rawHash = Hash(raw);
        byte[] warning = Encoding.UTF8.GetBytes(qemu + ": warning: Ignoring request for interrupt vector 0\r\n");
        Need(prefix == raw.Length && (raw.Length == 0 || raw.AsSpan().SequenceEqual(warning)) &&
            Text(seal, "PrefixSha256") == rawHash, "whpx_raw_after_seal");
        Need((long)original.StandardOutput.Length + raw.Length <= outputCharacterLimit, "whpx_combined_output_limit");
        Need(Text(resume, "Schema") == "clros.whpx.prelaunch-resume.v2" && Text(resume, "OwnerSha256") == ownerHash &&
            resume.GetProperty("ContCommands").GetInt32() == 1 && resume.GetProperty("ContAcknowledged").GetBoolean() &&
            resume.GetProperty("ProcessId").GetInt32() == seal.GetProperty("ProcessId").GetInt32() &&
            Text(resume, "SealSha256") == Hash(sealBytes) && Text(resume, "PrefixSha256") == rawHash &&
            resume.GetProperty("PrefixBytes").GetInt32() == raw.Length && Text(resume, "QmpSha256") == Hash(qmpBytes),
            "whpx_resume_identity");
        foreach (string phase in new[] { "Before", "After" })
            Need(Text(seal.GetProperty(phase), "status") == "prelaunch" &&
                !seal.GetProperty(phase).GetProperty("running").GetBoolean(), "whpx_prelaunch_status");
        var cpus = seal.GetProperty("Cpus");
        string registers = seal.GetProperty("Registers").GetString() ?? "";
        Need(cpus.GetArrayLength() == 1 && cpus[0].GetProperty("cpu-index").GetInt32() == 0 &&
            registers.Contains("EIP=0000fff0", StringComparison.Ordinal) &&
            registers.Contains("CS =f000 ffff0000", StringComparison.Ordinal) &&
            registers.Contains("CR0=60000010", StringComparison.Ordinal), "whpx_prelaunch_reset");
        string[] arguments = seal.GetProperty("Arguments").EnumerateArray().Select(x => x.GetString() ?? "").ToArray();
        int machine = Array.IndexOf(arguments, "-machine"), cpu = Array.IndexOf(arguments, "-cpu");
        Need(arguments.Count(x => x == "-machine") == 1 && machine >= 0 && machine + 1 < arguments.Length &&
            arguments[machine + 1] == QemuContracts.WhpxPrelaunchMachine && arguments.Count(x => x == "-S") == 1 &&
            !arguments.Contains("-accel") && arguments.Count(x => x == "-cpu") == 1 && cpu + 1 < arguments.Length &&
            arguments[cpu + 1] is "Nehalem" or "Nehalem,-rdrand,-rdseed", "whpx_prelaunch_recipe");

        string[] expected = ["qmp_capabilities", "query-status", "query-cpus-fast", "human-monitor-command", "query-status", "cont"];
        int requests = 0, responses = 0; bool greeting = false;
        var returns = new Dictionary<int, JsonElement>();
        foreach (string line in new UTF8Encoding(false, true).GetString(qmpBytes).Split('\n').Select(x => x.TrimEnd('\r')).Where(x => x.Length != 0))
        {
            bool request = line.StartsWith("REQUEST ", StringComparison.Ordinal);
            using var message = Document(Encoding.UTF8.GetBytes(request ? line[8..] : line));
            var value = message.RootElement;
            if (request)
            {
                Need(requests < expected.Length && value.GetProperty("id").GetInt32() == requests + 1 &&
                    Text(value, "execute") == expected[requests], "whpx_qmp_command_order");
                var args = value.GetProperty("arguments");
                if (requests == 3) Need(Text(args, "command-line") == "info registers" && args.EnumerateObject().Count() == 1, "whpx_qmp_register_query");
                else Need(args.ValueKind == JsonValueKind.Object && !args.EnumerateObject().Any(), "whpx_qmp_arguments");
                requests++;
            }
            else if (value.TryGetProperty("QMP", out _)) { Need(!greeting && requests == 0, "whpx_qmp_greeting"); greeting = true; }
            else if (value.TryGetProperty("event", out _)) Need(requests == 6, "whpx_qmp_early_event");
            else
            {
                int id = value.GetProperty("id").GetInt32();
                Need(greeting && id == responses + 1 && requests == id && value.TryGetProperty("return", out _), "whpx_qmp_response_order");
                returns.Add(id, value.GetProperty("return").Clone()); responses++;
            }
        }
        Need(greeting && requests == 6 && responses == 6 &&
            Same(returns[2], seal.GetProperty("Before")) && Same(returns[3], cpus) &&
            Same(returns[4], seal.GetProperty("Registers")) && Same(returns[5], seal.GetProperty("After")) &&
            returns[6].ValueKind == JsonValueKind.Object && !returns[6].EnumerateObject().Any(), "whpx_qmp_original_join");

        // Sidecar read leases deny replacement after the anchored first read;
        // the surviving writer separately protects the raw file through join.
        foreach (var input in inputs.ToArray())
        {
            using var file = new FileStream(input.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Need(NativeFile.GetIdentity(file.SafeFileHandle).Key == input.Key && file.Length == input.Bytes.Length, "whpx_final_input_changed");
            byte[] bytes = new byte[input.Bytes.Length]; file.ReadExactly(bytes);
            Need(bytes.AsSpan().SequenceEqual(input.Bytes), "whpx_final_input_changed");
        }
        string final = Path.Combine(directory, "outer-final.json");
        using var output = new FileStream(final, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        JsonSerializer.Serialize(output, new
        {
            Schema = "clros.whpx.outer-joined-file.v2", Complete = true, OriginalJobJoined = true,
            OwnerSha256 = ownerHash, SealSha256 = Hash(sealBytes), ResumeSha256 = Hash(resumeBytes),
            QmpSha256 = Hash(qmpBytes), RawIdentity = rawKey, RawBytes = raw.Length, RawSha256 = rawHash,
            OriginalExit = original.ExitCode, OriginalCancelled = original.Cancelled,
            OriginalStdoutSha256 = Hash(Encoding.UTF8.GetBytes(original.StandardOutput)),
            Boundary = "Original retained job joined and written file bytes verified; cancellation is not natural guest completion. No userspace-buffer flush theorem."
        }, new JsonSerializerOptions { WriteIndented = true });
        output.Flush(true);
        }
        finally { foreach (var lease in leases) lease.Dispose(); }
    }
}
