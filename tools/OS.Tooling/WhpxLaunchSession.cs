using Microsoft.Win32.SafeHandles;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Tooling.Native;

namespace OS.Tooling;

// Survives the original Runner/job await. The original synchronous write handle
// denies new writers/deletion for the entire join and final verification.
// No managed stream ever writes or seeks through this handle.
internal sealed class WhpxLaunchSession : IDisposable
{
    private readonly FileStream executable, ownership;
    private readonly SafeFileHandle rawWriter;
    private readonly string root, serial, actualQemu, rawKey, ownerSha;
    private readonly IReadOnlyDictionary<string, string?> environment;

    private WhpxLaunchSession(string root, string serial, string qemu, FileStream executable)
    {
        this.root = root;
        this.serial = serial;
        actualQemu = qemu;
        this.executable = executable;
        string directory = serial + ".whpx-prelaunch";
        if (Path.Exists(directory))
            throw new ToolFailure("whpx_prelaunch_fresh_output_required");
        Directory.CreateDirectory(directory);
        string raw = Path.Combine(directory, "stderr.raw");
        rawWriter = File.OpenHandle(raw, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
        try
        {
            rawKey = NativeFile.GetIdentity(rawWriter).Key;
            byte[] body = JsonSerializer.SerializeToUtf8Bytes(new
            {
                Schema = "clros.whpx.outer-file-owner.v2",
                Mode = "surviving-original-owner",
                Nonce = Guid.NewGuid().ToString("N"),
                Qemu = qemu,
                QemuSha256 = QemuContracts.WhpxPrelaunchQemuSha256,
                Machine = QemuContracts.WhpxPrelaunchMachine,
                Serial = serial,
                Raw = raw,
                RawIdentity = rawKey,
                OwnerProcessId = Environment.ProcessId,
                Protection = "synchronous-writer-FileShare.Read",
                Boundary = "Shared launcher/QEMU written stderr only; no userspace-buffer flush theorem or hard disk-write quota."
            }, new JsonSerializerOptions { WriteIndented = true });
            ownerSha = Convert.ToHexString(SHA256.HashData(body));
            ownership = new FileStream(Path.Combine(directory, "ownership.json"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            ownership.Write(body);
            ownership.Flush(true);
            environment = new Dictionary<string, string?>
            {
                ["CLROS_WHPX_STDERR_PATH"] = raw,
                ["CLROS_WHPX_STDERR_ID"] = rawKey,
                ["CLROS_WHPX_OWNER_SHA256"] = ownerSha
            };
        }
        catch { ownership?.Dispose(); rawWriter.Dispose(); throw; }
    }

    public static WhpxLaunchSession? Create(string root, string serial, string qemu, string accelerator)
    {
        if (!OperatingSystem.IsWindows() || accelerator != "whpx")
            return null;
        qemu = Path.GetFullPath(qemu, root);
        if (!File.Exists(qemu))
            return null;
        var input = new FileStream(qemu, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (Convert.ToHexString(SHA256.HashData(input)) != QemuContracts.WhpxPrelaunchQemuSha256)
            {
                input.Dispose();
                return null;
            }
            return new(root, Path.GetFullPath(serial, root), qemu, input);
        }
        catch { input.Dispose(); throw; }
    }

    public ProcessRequest Prepare(ProcessRequest request)
    {
        if (request.StandardErrorHandle is not null)
            throw new ToolFailure("whpx_prelaunch_existing_stderr_owner");
        var variables = new Dictionary<string, string?>(request.EnvironmentOverrides);
        foreach (var pair in environment)
            if (!variables.TryAdd(pair.Key, pair.Value))
                throw new ToolFailure("whpx_prelaunch_duplicate_environment");
        return request with
        {
            StandardErrorHandle = rawWriter,
            RetainOwnershipUntilJoined = true,
            EnvironmentOverrides = variables
        };
    }

    public ProcessResult VerifyJoined(ProcessResult original, int outputCharacterLimit)
    {
        // Rejections also retain their exact original output and process facts.
        // Preservation grants no acceptance and precedes every verification.
        using (var output = new FileStream(serial + ".whpx-prelaunch/stdout.original.log", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            output.Write(System.Text.Encoding.UTF8.GetBytes(original.StandardOutput));
            output.Flush(true);
        }
        using (var output = new FileStream(serial + ".whpx-prelaunch/process.original.json", FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        {
            JsonSerializer.Serialize(output, original);
            output.Flush(true);
        }
        executable.Position = 0;
        if (Convert.ToHexString(SHA256.HashData(executable)) != QemuContracts.WhpxPrelaunchQemuSha256 ||
            NativeFile.GetIdentity(rawWriter).Key != rawKey)
            throw new ToolFailure("whpx_prelaunch_owner_changed");
        ownership.Position = 0;
        if (ownership.Length > 65536)
            throw new ToolFailure("whpx_prelaunch_owner_extent");
        byte[] body = new byte[checked((int)ownership.Length)];
        ownership.ReadExactly(body);
        if (Convert.ToHexString(SHA256.HashData(body)) != ownerSha)
            throw new ToolFailure("whpx_prelaunch_owner_changed");
        QemuTranscripts.AssertWhpxJoinedFile(root, serial, actualQemu, rawKey, ownerSha, original, outputCharacterLimit);
        // Preserve the complete original before returning the exact protocol
        // projection to existing guest consumers. Only the two verified host
        // anchors are removed; their digests remain in outer-final.json.
        return original with
        {
            StandardOutput = string.Join('\n', original.StandardOutput.Split('\n')
            .Where(row => !row.StartsWith("QEMU_WHPX_CHILD ", StringComparison.Ordinal) &&
                !row.StartsWith("QEMU_WHPX_RESUME ", StringComparison.Ordinal)))
        };
    }

    public void Dispose()
    {
        ownership.Dispose();
        rawWriter.Dispose();
        executable.Dispose();
    }
}
