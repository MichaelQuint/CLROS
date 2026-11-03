using System.Diagnostics;
using System.Text.Json;
using OS.Tooling;

internal static class AtomicReceiptTests
{
    private static string Receipt()
    {
        string directory = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "atomic-receipt-controls", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "results.json");
        AtomicReceipt.WriteJson(path, new
        {
            phase = "previous",
            complete = false
        });
        return path;
    }

    private static string Phase(string path)
    {
        using JsonDocument value = JsonDocument.Parse(File.ReadAllText(path));
        return value.RootElement.GetProperty("phase").GetString()!;
    }

    [HostTest("atomic receipt waits for a transient Windows reader and preserves the previous complete file")]
    private static void TransientReader() => TransientReaderAsync().GetAwaiter().GetResult();

    private static async Task TransientReaderAsync()
    {
        if (!OperatingSystem.IsWindows())
            return;
        string path = Receipt();
        using FileStream reader = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        Task replacement = Task.Run(() => AtomicReceipt.WriteJson(path, new { phase = "next", complete = true }));
        try
        {
            Stopwatch wait = Stopwatch.StartNew();
            while (!File.Exists(path + ".tmp") && !replacement.IsCompleted && wait.Elapsed < TimeSpan.FromSeconds(5))
                await Task.Delay(1);
            await Task.Delay(40); // Keep a real deny-delete handle across at least one retry.
            HostCheck.True(!replacement.IsCompleted, "replacement must wait for the held reader");
            HostCheck.Equal("previous", Phase(path));
            // The temporary file remains writer-owned until publication. Reading
            // it here races the write on Windows; only the target is public.
        }
        finally
        {
            reader.Dispose();
        }
        await replacement.WaitAsync(TimeSpan.FromSeconds(5));
        HostCheck.Equal("next", Phase(path));
        HostCheck.True(!File.Exists(path + ".tmp"));
    }

    [HostTest("atomic receipt keeps the previous file and fails after persistent sharing denial")]
    private static void PersistentReader()
    {
        if (!OperatingSystem.IsWindows())
            return;
        foreach (bool temporary in new[] { false, true })
        {
            string path = Receipt();
            string locked = temporary ? path + ".tmp" : path;
            if (temporary)
                File.WriteAllText(locked, "held temporary file");
            using FileStream reader = File.Open(locked, FileMode.Open, FileAccess.Read, FileShare.Read);
            Stopwatch elapsed = Stopwatch.StartNew();
            bool rejected = false;
            try
            {
                AtomicReceipt.WriteJson(path, new
                {
                    phase = "uncommitted",
                    complete = true
                });
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException && (error.HResult & 0xffff) is 5 or 32 or 33)
            {
                rejected = true;
            }
            HostCheck.True(rejected, "persistent receipt denial must remain a failure");
            HostCheck.True(elapsed.Elapsed >= AtomicReceipt.SharingRetryTimeout && elapsed.Elapsed < TimeSpan.FromSeconds(5));
            HostCheck.Equal("previous", Phase(path));
            if (!temporary)
                HostCheck.Equal("uncommitted", Phase(path + ".tmp"));
        }
    }
}
