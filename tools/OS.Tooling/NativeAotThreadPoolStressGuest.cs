using System.Globalization;
using System.Text;

namespace OS.Tooling;

/// <summary>Host-only observations for the separate repeated ThreadPool stress
/// app. Borrowed stdout grants no producer, kernel wait/scheduler, runtime worker
/// shutdown or NativeAOT/CLROS execution authority.</summary>
public static class NativeAotThreadPoolStressGuest
{
    /// <summary>Require all three complete rounds of bounded work, cancellation,
    /// exclusive publication, continuations and measured timer drain. Missing,
    /// failed, extra or inconsistent rows throw. Only app-owned operations are
    /// described; native input/trace and complete guest cleanup remain separate.</summary>
    public static void ValidateOutput(string text)
    {
        Need(text.Length is > 0 and <= 4096, "size");
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (text.EndsWith('\n'))
            text = text[..^1];
        string[] rows = text.Split('\n');
        Need(rows.Length == 14 && rows[0] == "TEST_START nativeaot.tp-stress" && rows[^1] == "TEST_PASS nativeaot.tp-stress", "inventory");
        long lastAfter = 0, lastFrequency = 0;
        for (int lifetime = 1; lifetime <= 3; lifetime++)
        {
            int at = (lifetime - 1) * 4 + 1;
            string[] work = rows[at].Split(' ');
            Need(work.Length == 10 && work[0] == "NATIVEAOT_TP_STRESS_WORK" && Number(work[1], "lifetime", 3) == lifetime &&
                work[2] == "tasks=8" && work[3] == "completed=8" && work[4] == "checksum=204" && work[5] == "activeMax=2" &&
                work[6] == "inFlight=0" && work[7] == "mask=255" && work[9] == "threadPool=true", "work");
            Need(Number(work[8], "workers", 8) > 0, "worker_progress");
            Need(rows[at + 1] == FormattableString.Invariant($"NATIVEAOT_TP_STRESS_CANCEL lifetime={lifetime} before=2 during=2 workCallbacks=0 recovered=1"), "cancellation");
            string[] race = rows[at + 2].Split(' ');
            Need(race.Length == 8 && race[0] == "NATIVEAOT_TP_STRESS_RACE" && Number(race[1], "lifetime", 3) == lifetime &&
                race[3] == "winners=1" && race[4] == "duplicates=2" && race[5] == "continuations=3" && race[6] == "inline=0" &&
                (race[2] == "outcome=result" && race[7] == "value=43" || race[2] == "outcome=cancel" && race[7] == "value=0"), "publication");
            string[] timer = rows[at + 3].Split(' ');
            Need(timer.Length == 12 && timer[0] == "NATIVEAOT_TP_STRESS_TIMER" && Number(timer[1], "lifetime", 3) == lifetime &&
                timer[7] == "callbacks=1" && timer[8] == "completed=1" && timer[9] == "stale=0" && timer[10] == "never=0" && timer[11] == "pendingDrain=true", "timer");
            long before = Number(timer[2], "before", long.MaxValue), fired = Number(timer[3], "fired", long.MaxValue),
                drained = Number(timer[4], "drained", long.MaxValue), after = Number(timer[5], "after", long.MaxValue), frequency = Number(timer[6], "frequency", long.MaxValue);
            Need(frequency > 0 && before >= lastAfter && fired > before && drained >= fired && after > drained &&
                (decimal)(fired - before) * 1000 >= (decimal)frequency * 25 && (decimal)(after - drained) * 1000 >= (decimal)frequency * 25 &&
                (decimal)(after - before) <= (decimal)frequency * 20 && (lastFrequency == 0 || frequency == lastFrequency), "clock");
            lastAfter = after;
            lastFrequency = frequency;
        }
    }

    /// <summary>Read a bounded strict UTF8 capture under read-only sharing.
    /// Caller retains file ownership. I/O/encoding/invalid observations throw;
    /// nothing executes or writes the file and no provenance is inferred.</summary>
    public static void ValidateFile(string path)
    {
        using FileStream stream = new(Path.GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read);
        Need(stream.Length is > 0 and <= 4096, "size");
        byte[] bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        Need(stream.ReadByte() == -1, "changed");
        ValidateOutput(new UTF8Encoding(false, true).GetString(bytes));
    }
    private static long Number(string field, string name, long maximum)
    {
        string prefix = name + "=";
        Need(field.StartsWith(prefix, StringComparison.Ordinal), "field");
        string value = field[prefix.Length..];
        Need(long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out long number) && number <= maximum &&
            number.ToString(CultureInfo.InvariantCulture) == value, "number");
        return number;
    }
    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new ToolFailure("nativeaot_tp_stress_" + reason);
    }
}
