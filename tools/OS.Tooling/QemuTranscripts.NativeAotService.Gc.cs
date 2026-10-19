namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied GC values from one fd2 measurement. These describe
    /// supplied output and confer no allocation, runtime or guest authority.</summary>
    public readonly record struct ServiceGcMeasurement(ulong LimitBytes, ulong BaselineHeapBytes,
        ulong BaselineCommittedBytes, ulong LiveBytes, ulong LiveHeapBytes, ulong LiveCommittedBytes);

    /// <summary>Value-only joins of the supplied entitlement, kernel budget and
    /// two application GC measurements. Write/call, package, native ownership
    /// and guest execution must be independently verified by the parent replay.</summary>
    public readonly record struct ServiceGcInspection(ulong GcLimitBytes, ulong NativeReserveBytes,
        ulong StaticBytes, ulong PoolBytes, ulong EntitlementBytes, ServiceGcMeasurement Begin, ServiceGcMeasurement End,
        string TerminalStatus);

    /// <summary>Synchronously borrow bounded original framed service bytes and
    /// inspect only explicit v2 GC/budget values and ordering. V1 returns null
    /// and rejects every GC/budget observation. Invalid input throws; no I/O,
    /// execution or authority is created, and the caller retains its input.</summary>
    public static ServiceGcInspection? InspectNativeAotServiceGcForTests(ReadOnlySpan<byte> raw,
        ulong entitlementBytes, bool lifecycle = false) =>
        ReadServiceGc(NativeAotThreadingOutput.ReadService(raw), entitlementBytes, lifecycle, out _);

    internal static ServiceGcInspection? ReadServiceGc(NativeAotThreadingOutput.Capture framed,
        ulong entitlementBytes, bool lifecycle, out int? budgetPosition)
    {
        const string budgetMarker = "M16_016_BUDGET", gcMarker = "CLROS_SERVICE_GC_";
        budgetPosition = null;
        bool Has(string marker) => framed.NormalizedTail.Contains(marker, StringComparison.Ordinal) ||
            framed.Stdout.Contains(marker, StringComparison.Ordinal) || framed.Stderr.Contains(marker, StringComparison.Ordinal);
        if (!lifecycle)
        {
            Need(!Has(budgetMarker) && !Has(gcMarker), "service_gc_requires_explicit_v2"); return null;
        }
        Need(!framed.Stdout.Contains(gcMarker, StringComparison.Ordinal) && !framed.NormalizedTail.Contains(gcMarker, StringComparison.Ordinal) &&
            !framed.Stdout.Contains(budgetMarker, StringComparison.Ordinal) && !framed.Stderr.Contains(budgetMarker, StringComparison.Ordinal),
            "service_gc_channel");
        string text = framed.NormalizedTail;
        Need(text.EndsWith("\r\n", StringComparison.Ordinal), "service_gc_records");
        string[] rows = text[..^2].Split("\r\n", StringSplitOptions.None);
        Need(rows.Length is >0 and <=1_000_000 && rows.All(row => row.Length is >0 and <=4096 &&
            row.All(c => c is >= ' ' and <= '~')), "service_gc_records");
        int[] budgets = Enumerable.Range(0, rows.Length).Where(i => rows[i].Contains(budgetMarker, StringComparison.Ordinal)).ToArray();
        int[] starts = Enumerable.Range(0, rows.Length).Where(i => rows[i] == "TEST_START m16.014.startup").ToArray();
        int firstError = Array.FindIndex(rows, row => row.StartsWith("M16_005_WRITE fd=2 ", StringComparison.Ordinal));
        Need(budgets.Length == 1 && starts.Length == 1 && budgets[0] < starts[0] &&
            firstError > starts[0], "service_gc_budget_order");
        var budget = ReadRecord(rows[budgets[0]], budgetMarker, "gc:dec", "nativeReserve:dec", "static:dec", "pool:dec", "entitlement:dec");
        const ulong limit = 16 * 1024 * 1024, pool = 32 * 1024 * 1024;
        Need(entitlementBytes == NativeAotServicePackage.MemoryBytes && budget["gc"] == limit && budget["nativeReserve"] == limit && budget["pool"] == pool &&
            budget["static"] > 0 && budget["static"] % 4096 == 0 && entitlementBytes % 4096 == 0 &&
            budget["entitlement"] == entitlementBytes && pool <= entitlementBytes && budget["static"] <= entitlementBytes - pool,
            "service_gc_budget");

        Need(framed.Stderr.EndsWith('\n'), "service_gc_output");
        string[] error = framed.Stderr[..^1].Split('\n', StringSplitOptions.None);
        int One(string marker, bool prefix = false)
        {
            int[] found = Enumerable.Range(0, error.Length).Where(i => prefix ? error[i].StartsWith(marker, StringComparison.Ordinal) : error[i] == marker).ToArray();
            Need(found.Length == 1, "service_gc_marker"); return found[0];
        }
        int built = One("CLROS_APP_BUILT minimal.api.service"), begin = One("CLROS_SERVICE_GC_BEGIN ", true),
            listen = One("CLROS_SERVICE_LISTEN contract=v2 endpoint=0.0.0.0:8080"),
            drained = One("CLROS_SERVICE_LIFECYCLE_END ", true), end = One("CLROS_SERVICE_GC_END ", true),
            appEnd = One("CLROS_SERVICE_APP_END ", true);
        Need(built < begin && begin < listen && listen < drained && drained < end && end < appEnd, "service_gc_order");
        for (int i = 0; i < error.Length; i++)
            Need(!error[i].Contains(gcMarker, StringComparison.Ordinal) || i == begin || i == end, "service_gc_unknown_record");
        var terminal = ReadRecord(error[drained], "CLROS_SERVICE_LIFECYCLE_END CLROS_LIFECYCLE_STATUS",
            "active:dec", "waiting:dec", "waits:dec", "canceled:dec", "echoes:dec", "streams:dec", "rejected:dec", "read:dec", "written:dec");
        Need(terminal["active"] == 0 && terminal["waiting"] == 0 && terminal["waits"] == 2 && terminal["canceled"] == 2 &&
            terminal["echoes"] == 1 && terminal["streams"] == 1 && terminal["rejected"] == 1 &&
            terminal["read"] is >=32768 and <=65536 && terminal["written"] == 524288, "service_gc_lifecycle_drain");
        var first = ReadServiceGcMeasurement(error[begin], "CLROS_SERVICE_GC_BEGIN");
        var last = ReadServiceGcMeasurement(error[end], "CLROS_SERVICE_GC_END");
        budgetPosition = budgets[0];
        return new(budget["gc"], budget["nativeReserve"], budget["static"], budget["pool"], entitlementBytes, first, last,
            error[drained]["CLROS_SERVICE_LIFECYCLE_END ".Length..]);
    }

    private static ServiceGcMeasurement ReadServiceGcMeasurement(string line, string marker)
    {
        var row = ReadRecord(line, marker, "limit:dec", "baselineHeap:dec", "baselineCommitted:dec", "liveBytes:dec", "liveHeap:dec", "liveCommitted:dec");
        const ulong limit = 16 * 1024 * 1024, live = 2 * 1024 * 1024;
        Need(row["limit"] == limit && row["liveBytes"] == live && row["baselineHeap"] <= row["baselineCommitted"] &&
            row["baselineCommitted"] <= limit && row["liveHeap"] >= live && row["liveHeap"] <= row["liveCommitted"] &&
            row["liveCommitted"] <= limit, "service_gc_measurement");
        return new(row["limit"], row["baselineHeap"], row["baselineCommitted"], row["liveBytes"], row["liveHeap"], row["liveCommitted"]);
    }
}
