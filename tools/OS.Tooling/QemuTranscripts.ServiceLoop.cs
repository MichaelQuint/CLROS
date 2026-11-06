namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public static IReadOnlyList<string> M16ServiceLoopMarkers
    {
        get;
    } = Array.AsReadOnly<string>(
        ["TEST_START m16-003", "M16_003_CLOCK",
         .. Enumerable.Range(1, 4).Select(i => "M16_003_ITERATION index=" + i + " "),
         .. Enumerable.Range(0, 8).Select(i => "M16_003_FIRMWARE_DENIED index=" + i + " "),
         "M16_003_STOP", "CLROS_M16_003_READY", "TEST_PASS m16-003"]);

    // The caller validates the exact package/firmware/owned parent afterward.
    public static string[] ValidateServiceLoopTail(string[] lines, bool linuxStartup = false)
    {
        Need(lines.Length == QemuContracts.M03OwnedSubsystemMarkers.Count + 7 + (linuxStartup ? 7 : 0) + 17, "m16_003_record_count");
        string[] tail = lines[^17..];
        Need(tail[0] == "TEST_START m16-003" && tail[15] == "CLROS_M16_003_READY" &&
            tail[16] == "TEST_PASS m16-003", "m16_003_tail_identity");
        const string clockSuffix = " source=owned-hpet";
        Need(tail[1].EndsWith(clockSuffix, StringComparison.Ordinal), "m16_003_clock_source");
        var clock = ReadRecord(tail[1][..^clockSuffix.Length], "M16_003_CLOCK", "frequency:dec", "period:dec", "recoveries:dec");
        ulong frequency = clock["frequency"], period = clock["period"];
        Need(frequency is >= 1000000 and <= 10000000000 && period != 0 &&
            1000000000000000UL % period == 0 && frequency == 1000000000000000UL / period &&
            clock["recoveries"] <= 1, "m16_003_clock_geometry");
        const string stopSuffix = " firmware=revoked providers=1 shutdown=owned-controller";
        Need(tail[14].EndsWith(stopSuffix, StringComparison.Ordinal), "m16_003_stop_authority");
        var stop = ReadRecord(tail[14][..^stopSuffix.Length], "M16_003_STOP",
            "iterations:dec", "reads:dec", "first:dec", "last:dec", "interval:dec");
        ulong interval = (frequency + 999) / 1000, previous = stop["first"];
        Need(stop["iterations"] == 4 && stop["reads"] is >= 5 and <= 16385 &&
            stop["interval"] == interval, "m16_003_stop_counters");
        for (int i = 0; i < 4; i++)
        {
            var row = ReadRecord(tail[2 + i], "M16_003_ITERATION", "index:dec", "ticks:dec");
            Need(row["index"] == (ulong)i + 1 && previous <= ulong.MaxValue - interval &&
                row["ticks"] >= previous + interval, "m16_003_timed_iteration");
            previous = row["ticks"];
        }
        Need(stop["last"] == previous, "m16_003_stop_last_tick");
        for (int i = 0; i < 8; i++)
            Need(tail[6 + i] == "M16_003_FIRMWARE_DENIED index=" + i +
                " status=ServicesUnavailable providers=1 outputs=unchanged", "m16_003_firmware_denial");
        return lines[..^17];
    }
}
