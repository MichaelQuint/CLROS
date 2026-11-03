using System.Globalization;
using System.Text;

namespace OS.Tooling;

/// <summary>Strict host-only observations for the separate compiler-generated
/// runtime matrix. Borrowed stdout provides no producer, native root/scheduler,
/// guest process lifetime or CLROS execution authority.</summary>
public static class NativeAotRuntimeMatrixGuest
{
    /// <summary>Require all three complete rounds of EH, cached constructor
    /// failure, delegates, GC/finalization/thread roots and async teardown.
    /// Missing, failed, extra, stale or inconsistent records throw ToolFailure.
    /// Native input/trace and complete guest lifecycle admission remain separate.</summary>
    public static void ValidateOutput(string text)
    {
        Need(text.Length is > 0 and <= 4096, "size");
        text = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (text.EndsWith('\n'))
            text = text[..^1];
        string[] rows = text.Split('\n');
        Need(rows.Length == 17 && rows[0] == "TEST_START nativeaot.runtime-matrix" && rows[^1] == "TEST_PASS nativeaot.runtime-matrix", "inventory");
        long lastCollection = 0, lastAfter = 0, lastFrequency = 0;
        for (int round = 1; round <= 3; round++)
        {
            int at = (round - 1) * 5 + 1;
            Need(rows[at] == FormattableString.Invariant($"NATIVEAOT_MATRIX_EH round={round} filters=1 catches=2 cleanups=2 order=12 identity=true"), "eh");
            Need(rows[at + 1] == FormattableString.Invariant($"NATIVEAOT_MATRIX_CCTOR round={round} attempts=2 total={round * 2} runs=1 typed=true innerIdentity=true"), "constructor");
            Need(rows[at + 2] == FormattableString.Invariant($"NATIVEAOT_MATRIX_DELEGATES round={round} closed={round + 50} closure={round + 47} calls=2 actions=3 order=122 sum=19"), "delegates");
            string[] gc = rows[at + 3].Split(' ');
            Need(gc.Length == 16 && gc[0] == "NATIVEAOT_MATRIX_GC" && Number(gc[1], "round", 3) == round && gc[2] == "collections=2" &&
                gc[5] == "workers=2" && gc[6] == "joined=2" && gc[7] == "freshTls=2" && gc[8] == "roots=2" && gc[9] == "interior=2" && gc[10] == "tls=2" &&
                Number(gc[14], "finalizedMask", 7) == (1 << round) - 1 && gc[15] == "duplicateFinalizers=0", "gc_record");
            long before = Number(gc[3], "before", int.MaxValue), after = Number(gc[4], "after", int.MaxValue), progress = Number(gc[11], "progressBefore", long.MaxValue),
                returned = Number(gc[12], "progressReturned", long.MaxValue), progressAfter = Number(gc[13], "progressAfter", long.MaxValue);
            Need(before >= lastCollection && after - before >= 2 && progress > 0 && returned >= progress && progressAfter > returned, "gc_progress");
            lastCollection = after;
            string[] async = rows[at + 4].Split(' ');
            Need(async.Length == 19 && async[0] == "NATIVEAOT_MATRIX_ASYNC" && Number(async[1], "round", 3) == round && async[2] == "tasks=4" &&
                async[3] == "completed=4" && async[4] == "checksum=30" && Number(async[5], "workers", 4) > 0 && async[6] == "canceledBefore=1" &&
                async[7] == "canceledDuring=1" && async[8] == "workCallbacks=0" && async[16] == "callbacks=1" && async[17] == "stale=0" && async[18] == "disposed=true", "async_record");
            long delayBefore = Number(async[9], "delayBefore", long.MaxValue), delayAfter = Number(async[10], "delayAfter", long.MaxValue), timerBefore = Number(async[11], "timerBefore", long.MaxValue),
                fired = Number(async[12], "fired", long.MaxValue), drained = Number(async[13], "drained", long.MaxValue), end = Number(async[14], "after", long.MaxValue), frequency = Number(async[15], "frequency", long.MaxValue);
            Need(frequency > 0 && delayBefore >= lastAfter && delayAfter > delayBefore && timerBefore >= delayAfter && fired > timerBefore && drained >= fired && end > drained &&
                Interval(delayBefore, delayAfter, frequency) && Interval(timerBefore, fired, frequency) && Interval(drained, end, frequency) &&
                (decimal)(end - delayBefore) <= (decimal)frequency * 20 && (lastFrequency == 0 || frequency == lastFrequency), "clock");
            lastAfter = end;
            lastFrequency = frequency;
        }
    }
    private static bool Interval(long before, long after, long frequency) => (decimal)(after - before) * 1000 >= (decimal)frequency * 25;

    /// <summary>Read a bounded strict UTF8 capture with read-only sharing.
    /// Caller retains file ownership. I/O/encoding/invalid observations throw;
    /// nothing executes, writes or gains native/guest provenance.</summary>
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
            throw new ToolFailure("nativeaot_runtime_matrix_" + reason);
    }
}
