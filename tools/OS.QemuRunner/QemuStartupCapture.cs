using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace OS.Tools.QemuRunner;

/// <summary>
/// Captures an owned QEMU child's stopped startup state through its private stdio QMP channel.
/// The caller must redirect stdin/stdout, pass -qmp stdio, and terminate the child afterward.
/// Capture is best effort with a five-second total budget; it never changes a failed run to success.
/// </summary>
internal static class QemuStartupCapture
{
    public static void CaptureBestEffort(Process process, string serialPath)
    {
        string directory = Path.GetFullPath(serialPath) + ".startup-" + Guid.NewGuid().ToString("N");
        try
        {
            Directory.CreateDirectory(directory);
            using StreamWriter log = new(Path.Combine(directory, "qmp.log"), false, new UTF8Encoding(false)) { AutoFlush = true };
            using CancellationTokenSource deadline = new(TimeSpan.FromSeconds(5));
            Capture(process, directory, log, deadline.Token).GetAwaiter().GetResult();
            Console.WriteLine("QEMU_STARTUP_CAPTURE complete path=" + directory);
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or OperationCanceledException or JsonException or UnauthorizedAccessException or KeyNotFoundException or FormatException)
        {
            // The original startup failure and owned-child cleanup take precedence.
            Console.Error.WriteLine("QEMU_STARTUP_CAPTURE incomplete path=" + directory + " reason=" + error.GetType().Name);
        }
    }

    private static async Task Capture(Process process, string directory, StreamWriter log, CancellationToken cancellation)
    {
        if (process.HasExited) throw new IOException("qemu_already_exited");
        JsonElement greeting = await Read();
        if (!greeting.TryGetProperty("QMP", out _)) throw new IOException("qmp_greeting_missing");
        await Command("qmp_capabilities");
        await Command("stop");
        JsonElement status = await Command("query-status");
        if (status.GetProperty("running").GetBoolean()) throw new IOException("qemu_not_stopped");
        foreach (string query in new[] { "info registers", "x/24i $rip", "x/48gx $rsp" })
            await Command("human-monitor-command", new Dictionary<string, string> { ["command-line"] = query });
        JsonElement memory = await Command("query-memory-size-summary");
        ulong bytes = memory.GetProperty("base-memory").GetUInt64();
        if (bytes == 0 || bytes > 512UL * 1024 * 1024 ||
            memory.TryGetProperty("plugged-memory", out JsonElement plugged) && plugged.GetUInt64() != 0)
            throw new IOException("startup_ram_extent_unsupported");
        string ram = Path.Combine(directory, "physical.ram");
        await Command("pmemsave", new { val = 0, size = bytes, filename = ram });
        if ((ulong)new FileInfo(ram).Length != bytes) throw new IOException("startup_ram_incomplete");

        async Task<JsonElement> Read()
        {
            while (true)
            {
                string line = await process.StandardOutput.ReadLineAsync(cancellation) ?? throw new IOException("qmp_eof");
                log.WriteLine(line);
                using JsonDocument document = JsonDocument.Parse(line);
                JsonElement root = document.RootElement;
                if (!root.TryGetProperty("event", out _)) return root.Clone();
            }
        }

        async Task<JsonElement> Command(string name, object? arguments = null)
        {
            string request = JsonSerializer.Serialize(new Dictionary<string, object?> { ["execute"] = name, ["arguments"] = arguments ?? new { } });
            log.WriteLine("REQUEST " + request);
            await process.StandardInput.WriteLineAsync(request.AsMemory(), cancellation);
            await process.StandardInput.FlushAsync(cancellation);
            JsonElement response = await Read();
            if (!response.TryGetProperty("return", out JsonElement result)) throw new IOException("qmp_command_failed_" + name);
            return result;
        }
    }
}
