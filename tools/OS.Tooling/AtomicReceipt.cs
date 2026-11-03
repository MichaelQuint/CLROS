using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace OS.Tooling;

/// <summary>Complete receipts become visible only through an atomic rename.</summary>
public static class AtomicReceipt
{
    public static TimeSpan SharingRetryTimeout { get; } = TimeSpan.FromMilliseconds(250);
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static void WriteJson<T>(string path, T value)
    {
        string content = JsonSerializer.Serialize(value, JsonOptions) + "\n";
        string temporary = path + ".tmp";
        Stopwatch deadline = Stopwatch.StartNew();
        Retry(() => File.WriteAllText(temporary, content, new UTF8Encoding(false)));
        Retry(() => File.Move(temporary, path, overwrite: true));

        void Retry(Action operation)
        {
            while (true)
            {
                try
                {
                    operation();
                    return;
                }
                catch (Exception error) when (OperatingSystem.IsWindows() &&
                    error is IOException or UnauthorizedAccessException &&
                    (error.HResult & 0xffff) is 5 or 32 or 33 && deadline.Elapsed < SharingRetryTimeout)
                {
                    // A local reader/indexer may deny replacement briefly. The
                    // previous complete receipt stays intact; persistent denial
                    // escapes with its original exception after this small bound.
                    TimeSpan remaining = SharingRetryTimeout - deadline.Elapsed;
                    if (remaining > TimeSpan.Zero)
                        Thread.Sleep(TimeSpan.FromMilliseconds(Math.Min(25, remaining.TotalMilliseconds)));
                }
            }
        }
    }
}
