using System.Globalization;
using System.Text;

namespace OS.Tooling;

/// <summary>
/// Strict observation checks for the separate upstream threading consumers.
/// Captured stdout alone grants no NativeAOT producer, signal, suspension or
/// CLROS guest authority. Those require separately bound native/guest receipts.
/// </summary>
public static class NativeAotThreadingGuest
{
    /// <summary>
    /// Checks the focused workload's entire stdout: two ordered explicit full
    /// collections, independently advancing worker progress and joined root/TLS
    /// witnesses. Counts may include unrelated runtime collections. Rejects
    /// missing, extra, failed or inconsistent rows; does not execute input.
    /// </summary>
    public static void ValidateSuspensionOutput(string text)
    {
        Need(text.Length is > 0 and <= 4096, "output_size");
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (text.EndsWith('\n'))
            text = text[..^1];
        string[] lines = text.Split('\n');
        Need(lines.Length == 5, "output_inventory");
        Need(lines[0] == "TEST_START nativeaot.suspension" && lines[4] == "TEST_PASS nativeaot.suspension", "output_markers");
        long lastCollection = 0, lastProgress = 0;
        for (int epoch = 1; epoch <= 2; epoch++)
        {
            string[] row = lines[epoch].Split(' ');
            Need(row.Length == 7 && row[0] == "NATIVEAOT_SUSPENSION_GC", "collection_record");
            long ordinal = Number(row[1], "epoch", int.MaxValue);
            long before = Number(row[2], "before", int.MaxValue), after = Number(row[3], "after", int.MaxValue);
            long progressBefore = Number(row[4], "progressBefore", long.MaxValue);
            long progressReturned = Number(row[5], "progressReturned", long.MaxValue), progressAfter = Number(row[6], "progressAfter", long.MaxValue);
            Need(ordinal == epoch && before >= lastCollection && after > before &&
                progressBefore > 0 && progressBefore >= lastProgress && progressReturned >= progressBefore &&
                progressAfter > progressReturned, "collection_progress");
            lastCollection = after;
            lastProgress = progressAfter;
        }
        Need(lines[3] == "NATIVEAOT_SUSPENSION_JOINED workers=1 collections=2 roots=true interior=true tls=true", "joined_witness");
    }

    /// <summary>Reads one bounded strict UTF8 capture and validates it. The file
    /// is read-only; unknown encodings, invalid observations and I/O failures throw.</summary>
    public static void ValidateSuspensionFile(string path)
        => ValidateSuspensionOutput(ReadCapture(path, "suspension"));

    /// <summary>Checks all eight bounded ThreadPool observation rows: actual
    /// work, continuation publication, cancellation, monotonic delay/timer
    /// intervals and callback drain. Output alone establishes neither native
    /// runtime provenance nor CLROS execution or ThreadPool worker shutdown.</summary>
    public static void ValidateThreadPoolOutput(string text)
    {
        const string profile = "threadpool";
        void Check(bool condition, string reason) => Need(condition, reason, profile);
        Check(text.Length is > 0 and <= 4096, "output_size");
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (text.EndsWith('\n'))
            text = text[..^1];
        string[] lines = text.Split('\n');
        Check(lines.Length == 8, "output_inventory");
        Check(lines[0] == "TEST_START nativeaot.threadpool" && lines[7] == "TEST_PASS nativeaot.threadpool", "output_markers");
        string[] work = lines[1].Split(' ');
        Check(work.Length == 6 && work[0] == "NATIVEAOT_THREADPOOL_WORK" && work[1] == "tasks=4" && work[2] == "completed=4" &&
            work[3] == "checksum=30" && work[5] == "threadPool=true", "work_record");
        Check(Number(work[4], "workers", 4, profile) > 0, "work_progress");
        Check(lines[2] == "NATIVEAOT_THREADPOOL_CONTINUATIONS before=1 race=1 after=1 count=3 value=43 publicationInline=0", "continuation_record");
        Check(lines[3] == "NATIVEAOT_THREADPOOL_CANCELLATION before=2 during=1 callbacks=0", "cancellation_record");
        long[] Clock(string line, bool timer)
        {
            string[] row = line.Split(' ');
            Check(row.Length == (timer ? 8 : 6) && row[0] == (timer ? "NATIVEAOT_THREADPOOL_TIMER" : "NATIVEAOT_THREADPOOL_DELAY") &&
                row[1] == (timer ? "dueMs=50" : "requestedMs=50"), "clock_record");
            long before = Number(row[2], "before", long.MaxValue, profile), after = Number(row[3], timer ? "fired" : "after", long.MaxValue, profile);
            long frequency = Number(row[4], "frequency", long.MaxValue, profile);
            Check(frequency > 0 && after > before && (decimal)(after - before) * 1000 >= (decimal)frequency * 25 &&
                (decimal)(after - before) <= (decimal)frequency * 20, "monotonic_interval");
            Check(timer ? row[5] == "callbacks=1" && row[6] == "stale=0" && row[7] == "disposed=true" : row[5] == "minimumMs=25", "timer_drain");
            return [before, after, frequency];
        }
        long[] delay = Clock(lines[4], false), timer = Clock(lines[5], true);
        Check(timer[0] >= delay[1] && timer[2] == delay[2], "clock_order");
        Check(lines[6] == "NATIVEAOT_THREADPOOL_DRAINED tasks=4 continuations=3 callbacks=1 stale=0", "drained_record");
    }

    /// <summary>Read one strict UTF8 ThreadPool capture under the same4096-byte
    /// limit. This does not execute or qualify a payload.</summary>
    public static void ValidateThreadPoolFile(string path) => ValidateThreadPoolOutput(ReadCapture(path, "threadpool"));

    private static string ReadCapture(string path, string profile)
    {
        using FileStream stream = new(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        Need(stream.Length is > 0 and <= 4096, "output_size", profile);
        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        Need(stream.ReadByte() == -1, "output_changed", profile);
        return new UTF8Encoding(false, true).GetString(bytes);
    }

    private static long Number(string field, string name, long maximum, string profile = "suspension")
    {
        string prefix = name + "=";
        Need(field.StartsWith(prefix, StringComparison.Ordinal), "collection_field", profile);
        string value = field[prefix.Length..];
        Need(long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) &&
            number <= maximum && number.ToString(CultureInfo.InvariantCulture) == value, "collection_number", profile);
        return number;
    }

    private static void Need(bool condition, string reason, string profile = "suspension")
    {
        if (!condition)
            throw new ToolFailure("nativeaot_" + profile + "_" + reason);
    }
}
