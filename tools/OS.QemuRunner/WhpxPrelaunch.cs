using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using OS.Tooling.Native;

namespace OS.Tools.QemuRunner;

// Host-only compatibility policy for one inspected QEMU binary. No guest result
// is inherited: written raw stderr is retained in a synchronous file, and any
// bytes after the authenticated pre-resume seal fail. The runner owns both QMP endpoints; its
// existing stdio endpoint is used only for failed-startup capture after resume.
internal sealed partial class WhpxPrelaunch : IDisposable
{
    public const string AffectedSha256 = OS.Tooling.QemuContracts.WhpxPrelaunchQemuSha256;
    public const string Machine = OS.Tooling.QemuContracts.WhpxPrelaunchMachine;
    private const int Limit = 65536;
    private readonly Process process;
    private readonly FileStream executable, raw = null!;
    private readonly SafeFileHandle writer = null!;
    private readonly string ownerHash;
    private readonly StreamWriter qmp = null!;
    private readonly string directory, executablePath, rawKey;
    private readonly TcpListener listener = null!;
    private QmpConnection? connection;
    private long qmpBytes;
    private FileStream? sealLease;
    private string? sealDigest;
    private readonly object sync = new();
    private bool started;
    private FileStream? resumeLease;
    private string? resumeDigest;
    private byte[]? sealedPrefix;
    private string? failure, receipt;
    private bool resumed, completed, contAcknowledged;
    public string? Failure
    {
        get
        {
            lock (sync)
            {
                if (raw.Length > Limit)
                    failure ??= "whpx_prelaunch_stderr_bound";
                if (sealedPrefix is not null && raw.Length != sealedPrefix.Length)
                    failure ??= "whpx_prelaunch_post_seal_stderr";
                return failure;
            }
        }
    }

    public static WhpxPrelaunch? Create(Process process, string accelerator, string serialPath)
    {
        if (!OperatingSystem.IsWindows() || accelerator != "whpx")
            return null;
        var executable = new FileStream(process.StartInfo.FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            if (Convert.ToHexString(SHA256.HashData(executable)) != AffectedSha256)
            {
                executable.Dispose();
                return null;
            }
            var args = process.StartInfo.ArgumentList;
            int machine = args.IndexOf("-machine");
            if (machine < 0 || args.Count(x => x == "-machine") != 1 || args[machine + 1] != Machine ||
                args.Contains("-S") || args.Contains("-accel"))
                throw new IOException("whpx_prelaunch_recipe");
            return new(process, serialPath, executable);
        }
        catch
        {
            executable.Dispose();
            throw;
        }
    }

    private WhpxPrelaunch(Process process, string serialPath, FileStream executable)
    {
        this.process = process;
        this.executable = executable;
        executablePath = process.StartInfo.FileName;
        directory = Path.GetFullPath(serialPath) + ".whpx-prelaunch";
        string rawPath = Path.Combine(directory, "stderr.raw");
        try
        {
            string? inheritedPath = Environment.GetEnvironmentVariable("CLROS_WHPX_STDERR_PATH");
            if (inheritedPath is null)
            {
                if (Path.Exists(directory))
                    throw new IOException("whpx_prelaunch_fresh_output_required");
                Directory.CreateDirectory(directory);
                writer = File.OpenHandle(rawPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                ownerHash = "self-owned-no-surviving-outer-owner";
            }
            else
            {
                if (!string.Equals(Path.GetFullPath(inheritedPath), rawPath, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("whpx_prelaunch_inherited_path");
                writer = Console.OpenStandardErrorHandle();
                ownerHash = Environment.GetEnvironmentVariable("CLROS_WHPX_OWNER_SHA256") ?? throw new IOException("whpx_prelaunch_owner_missing");
            }
            raw = new FileStream(rawPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            rawKey = NativeFile.GetIdentity(raw.SafeFileHandle).Key;
            if (NativeFile.GetIdentity(writer).Key != rawKey ||
                inheritedPath is not null && Environment.GetEnvironmentVariable("CLROS_WHPX_STDERR_ID") != rawKey)
                throw new IOException("whpx_prelaunch_inherited_identity");
            qmp = new(new FileStream(Path.Combine(directory, "qmp.log"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false))
            {
                AutoFlush = true
            };
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Server.ExclusiveAddressUse = true;
            listener.Start(1);
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            process.StartInfo.ArgumentList.Add("-S");
            process.StartInfo.ArgumentList.Add("-qmp");
            process.StartInfo.ArgumentList.Add("tcp:127.0.0.1:" + port);
            process.StartInfo.RedirectStandardError = false;
            process.StartInfo.StandardErrorHandle = writer;
        }
        catch
        {
            listener?.Stop();
            qmp?.Dispose();
            raw?.Dispose();
            writer?.Dispose();
            throw;
        }
    }

    public void ResumeAfterStart(TimeSpan remaining) => Resume(remaining).GetAwaiter().GetResult();

    private async Task Resume(TimeSpan remaining)
    {
        started = true;
        Console.WriteLine("QEMU_WHPX_CHILD " + JsonSerializer.Serialize(new
        {
            OwnerSha256 = ownerHash,
            ProcessId = process.Id,
            StartUtc = process.StartTime.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture)
        }));
        Console.Out.Flush();
        double milliseconds = Math.Min(5000, remaining.TotalMilliseconds);
        if (milliseconds <= 0)
            throw new IOException("whpx_prelaunch_budget_expired");
        using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(milliseconds));
        TcpClient accepted = await listener.AcceptTcpClientAsync(deadline.Token);
        try
        {
            connection = new QmpConnection(accepted);
        }
        catch
        {
            accepted.Dispose();
            throw;
        }
        listener.Stop();
        var stream = connection.Stream;
        using var writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true) { AutoFlush = true };
        int id = 0;
        async Task<JsonElement> Read()
        {
            while (true)
            {
                string line = await ReadBoundedLine(stream, deadline.Token);
                LogQmp(line);
                using var document = JsonDocument.Parse(line);
                if (document.RootElement.TryGetProperty("event", out var e))
                {
                    if (!resumed)
                        throw new IOException("whpx_prelaunch_early_event_" + e.GetString());
                    continue;
                }
                return document.RootElement.Clone();
            }
        }
        async Task<JsonElement> Command(string name, object? arguments = null)
        {
            int current = ++id;
            string request = JsonSerializer.Serialize(new
            {
                execute = name,
                arguments = arguments ?? new
                {
                },
                id = current
            });
            LogQmp("REQUEST " + request);
            await writer.WriteLineAsync(request.AsMemory(), deadline.Token);
            var response = await Read();
            if (response.GetProperty("id").GetInt32() != current || !response.TryGetProperty("return", out var value))
                throw new IOException("whpx_prelaunch_qmp_response_" + name);
            return value;
        }
        if (!(await Read()).TryGetProperty("QMP", out _))
            throw new IOException("whpx_prelaunch_qmp_greeting");
        await Command("qmp_capabilities");
        var before = await Command("query-status");
        var cpus = await Command("query-cpus-fast");
        if (cpus.ValueKind != JsonValueKind.Array || cpus.GetArrayLength() != 1 || cpus[0].GetProperty("cpu-index").GetInt32() != 0)
            throw new IOException("whpx_prelaunch_single_vcpu");
        var registers = await Command("human-monitor-command", new Dictionary<string, string> { ["command-line"] = "info registers" });
        var after = await Command("query-status");
        if (before.GetProperty("running").GetBoolean() || before.GetProperty("status").GetString() != "prelaunch" ||
            !ResetRegisters(registers.GetString() ?? ""))
            throw new IOException("whpx_prelaunch_reset_state");
        // No timing inference grants admission. A reader that delivers startup
        // bytes later than this boundary fails conservatively as post-seal bytes.
        await Task.Delay(100, deadline.Token);
        lock (sync)
        {
            byte[] prefix = ReadRaw();
            string? invalid = ValidatePrelaunch(AffectedSha256, Machine, executablePath,
                after.GetProperty("status").GetString() ?? "", after.GetProperty("running").GetBoolean(), resumed, prefix);
            if (failure is not null || invalid is not null)
                throw new IOException(failure ?? invalid);
            sealedPrefix = prefix;
            WriteNew("seal.json", new
            {
                Schema = "clros.whpx.prelaunch-seal.v2",
                Transport = "inherited-standard-error-file",
                OwnerSha256 = ownerHash,
                QemuSha256 = AffectedSha256,
                Executable = executablePath,
                Arguments = process.StartInfo.ArgumentList.ToArray(),
                ProcessId = process.Id,
                StartUtc = process.StartTime.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                Before = before,
                Cpus = cpus,
                Registers = registers,
                After = after,
                RawIdentity = rawKey,
                PrefixBytes = prefix.Length,
                PrefixSha256 = Hash(prefix),
                ContCommandsBeforeSeal = 0,
                Boundary = "Observed shared launcher/QEMU initialization bytes only; origin unknown. No guest execution or harmlessness claim."
            });
            resumed = true; // Seal and phase change precede the only cont write.
        }
        await Command("cont");
        contAcknowledged = true;
        WriteNew("resume.json", new
        {
            Schema = "clros.whpx.prelaunch-resume.v2",
            OwnerSha256 = ownerHash,
            ProcessId = process.Id,
            ContCommands = 1,
            ContAcknowledged = true,
            SealSha256 = sealDigest,
            PrefixBytes = sealedPrefix!.Length,
            PrefixSha256 = Hash(sealedPrefix),
            QmpSha256 = Hash(ReadShared(Path.Combine(directory, "qmp.log")))
        });
        Console.WriteLine("QEMU_WHPX_RESUME " + JsonSerializer.Serialize(new
        {
            OwnerSha256 = ownerHash,
            ProcessId = process.Id,
            StartUtc = process.StartTime.ToUniversalTime().ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            SealSha256 = sealDigest,
            ResumeSha256 = resumeDigest,
            QmpSha256 = Hash(ReadShared(Path.Combine(directory, "qmp.log")))
        }));
        Console.Out.Flush();
    }

    public string? Complete()
    {
        if (completed)
        {
            if (failure is not null)
                throw new IOException(failure);
            return receipt;
        }
        if (started && !process.HasExited)
            throw new IOException("whpx_prelaunch_child_unjoined");
        lock (sync)
        {
            byte[] actual = ReadRaw();
            if (!contAcknowledged)
                failure ??= "whpx_prelaunch_cont_not_acknowledged";
            failure ??= sealedPrefix is null ? "whpx_prelaunch_not_sealed" :
                ValidateSeal(sealedPrefix, actual, rawKey, NativeFile.GetIdentity(raw.SafeFileHandle).Key, resumed);
            if (sealLease is null || Hash(ReadStream(sealLease)) != sealDigest || resumeLease is null || Hash(ReadStream(resumeLease)) != resumeDigest)
                failure ??= "whpx_prelaunch_seal_file_changed";
            executable.Position = 0;
            if (Convert.ToHexString(SHA256.HashData(executable)) != AffectedSha256)
                failure ??= "whpx_prelaunch_binary_changed";
            string path = WriteNew("receipt.json", new
            {
                Schema = "clros.whpx.prelaunch-final.v2",
                Transport = "inherited-standard-error-file",
                OwnerSha256 = ownerHash,
                QemuSha256 = AffectedSha256,
                Machine,
                ProcessId = !started ? (int?)null : process.Id,
                ContCommands = resumed ? 1 : 0,
                ContAcknowledged = contAcknowledged,
                PrefixBytes = sealedPrefix?.Length,
                PrefixSha256 = sealedPrefix is null ? null : Hash(sealedPrefix),
                RawBytes = actual.Length,
                RawSha256 = Hash(actual),
                RawIdentity = rawKey,
                SealSha256 = sealDigest,
                QmpSha256 = Hash(ReadShared(Path.Combine(directory, "qmp.log"))),
                Failure = failure,
                Complete = started && failure is null,
                Policy = "Only exact observed prelaunch prefix for this binary/recipe; no post-seal bytes. Old failed runs remain failed."
            });
            receipt = path + " sha256=" + Hash(File.ReadAllBytes(path));
            completed = true;
            if (failure is not null)
                throw new IOException(failure);
            return receipt;
        }
    }

    public static bool ResetRegisters(string text) => text.Contains("EIP=0000fff0", StringComparison.Ordinal) &&
        text.Contains("CS =f000 ffff0000", StringComparison.Ordinal) && text.Contains("CR0=60000010", StringComparison.Ordinal);

    public static string? ValidatePrelaunch(string hash, string machine, string executable, string status, bool running, bool resumed, byte[] prefix)
    {
        if (hash != AffectedSha256 || machine != Machine)
            return "whpx_prelaunch_identity";
        if (status != "prelaunch" || running || resumed)
            return "whpx_prelaunch_phase";
        byte[] expected = Encoding.UTF8.GetBytes(executable + ": warning: Ignoring request for interrupt vector 0\r\n");
        return prefix.Length == 0 || prefix.AsSpan().SequenceEqual(expected) ? null : "whpx_prelaunch_stderr_prefix";
    }

    public static string? ValidateSeal(byte[] prefix, byte[] actual, string originalKey, string currentKey, bool resumed) =>
        !resumed || originalKey != currentKey || !prefix.AsSpan().SequenceEqual(actual) ? "whpx_prelaunch_seal_changed" : null;

    private byte[] ReadRaw()
    {
        long length = raw.Length;
        if (length > Limit)
            throw new IOException("whpx_prelaunch_stderr_bound");
        // This is an independently opened reader, never the inherited writer.
        raw.Position = 0;
        byte[] bytes = new byte[checked((int)length)];
        raw.ReadExactly(bytes);
        if (raw.Length != length)
            throw new IOException("whpx_prelaunch_stderr_changed_during_read");
        return bytes;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private async Task<string> ReadBoundedLine(Stream stream, CancellationToken cancellation)
    {
        byte[] bytes = new byte[Limit], one = new byte[1];
        int count = 0;
        while (true)
        {
            if (await stream.ReadAsync(one, cancellation) != 1)
                throw new IOException("whpx_prelaunch_qmp_eof");
            if (count == bytes.Length)
                throw new IOException("whpx_prelaunch_qmp_line_bound");
            bytes[count++] = one[0];
            if (one[0] == 10)
                return new UTF8Encoding(false, true).GetString(bytes, 0, count).TrimEnd('\r', '\n');
        }
    }
    private void LogQmp(string line)
    {
        qmpBytes = checked(qmpBytes + Encoding.UTF8.GetByteCount(line) + 2);
        if (qmpBytes > 4 * Limit)
            throw new IOException("whpx_prelaunch_qmp_total_bound");
        qmp.WriteLine(line);
    }
    private static byte[] ReadStream(FileStream input)
    {
        input.Position = 0;
        byte[] bytes = new byte[checked((int)input.Length)];
        input.ReadExactly(bytes);
        return bytes;
    }
    private static byte[] ReadShared(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return ReadStream(input);
    }
    private string WriteNew(string name, object value)
    {
        string path = Path.Combine(directory, name);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(value, new JsonSerializerOptions { WriteIndented = true });
        var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
        try
        {
            output.Write(bytes);
            output.Flush(true);
            if (name == "seal.json")
            {
                sealLease = output;
                sealDigest = Hash(bytes);
            }
            else if (name == "resume.json")
            {
                resumeLease = output;
                resumeDigest = Hash(bytes);
            }
            else
                output.Dispose();
            return path;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }
    public void Dispose()
    {
        if (started)
        {
            while (!process.HasExited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
                {
                }
                process.WaitForExit(1000);
            }
        }
        try
        {
            if (!completed && started)
                Complete();
        }
        finally
        {
            try
            {
                // Handshake completion does not end the monitor connection's
                // lifetime. Retain it through the existing child join.
                connection?.CloseAfterChildJoin(!started || process.HasExited);
            }
            finally
            {
                listener.Stop();
                resumeLease?.Dispose();
                sealLease?.Dispose();
                qmp.Dispose();
                raw.Dispose();
                writer.Dispose();
                executable.Dispose();
            }
        }
    }
}
