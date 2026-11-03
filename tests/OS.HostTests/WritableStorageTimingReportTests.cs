using System.Globalization;
using OS.Tooling;

internal static class WritableStorageTimingReportTests
{
    // Independent complete row: 150 audit ticks outside the two inclusive
    // buckets; their 600 ticks plus150 outside plus250 unattributed equal1000.
    private const string Complete = "M16_027_TIMING width=32 frequency=10000000 valid=1 outer=1000 " +
        "auditCalls=2 auditTicks=300 outsideDeviceCalls=28 pairCalls=1 pairTicks=400 pairAuditTicks=100 " +
        "pumpCalls=1 pumpTicks=200 pumpAuditTicks=50 engineCalls=1 engineOwner=4096 " +
        "beforePhase=2 beforeStep=1 beforeWork=5 beforeIo=1 afterPhase=3 afterStep=2 afterWork=6 afterIo=0 " +
        "rawReads=12 adjacentReadTicks=3 auditOutside=150 unattributed=250";
    private const string Empty = "M16_027_TIMING width=64 frequency=1 valid=1 outer=0 " +
        "auditCalls=0 auditTicks=0 outsideDeviceCalls=0 pairCalls=0 pairTicks=0 pairAuditTicks=0 " +
        "pumpCalls=0 pumpTicks=0 pumpAuditTicks=0 engineCalls=0 engineOwner=0 " +
        "beforePhase=0 beforeStep=0 beforeWork=0 beforeIo=0 afterPhase=0 afterStep=0 afterWork=0 afterIo=0 " +
        "rawReads=4 adjacentReadTicks=0 auditOutside=0 unattributed=0";

    private static string Set(string row, string name, string value)
    {
        string marker = " " + name + "=";
        int start = row.IndexOf(marker, StringComparison.Ordinal);
        HostCheck.True(start >= 0, "test vector field missing " + name);
        start += marker.Length;
        int end = row.IndexOf(' ', start);
        if (end < 0)
            end = row.Length;
        return row[..start] + value + row[end..];
    }
    private static string Set(string row, string name, ulong value) => Set(row, name, value.ToString(CultureInfo.InvariantCulture));
    private static void Reject(string row, string? reason = null)
    {
        string? failure = null;
        try
        {
            _ = WritableStorageTimingReport.Read(row);
        }
        catch (ToolFailure error)
        {
            failure = error.Message;
        }
        HostCheck.True(failure is not null && (reason is null || failure.Contains(reason, StringComparison.Ordinal)),
            "expected=" + reason + " actual=" + failure);
    }

    [HostTest("writable timing copied reports preserve exact fields and inclusive accounting for both counter widths")]
    private static void CompleteReports()
    {
        foreach (uint width in new uint[] { 32, 64 })
        {
            string row = Set(Complete, "width", width);
            var report = WritableStorageTimingReport.Read(row);
            HostCheck.Equal(row, report.RawRow);
            HostCheck.Equal(width, report.Width);
            HostCheck.True(report.Valid);
            HostCheck.Equal(10000000UL, report.Frequency);
            HostCheck.Equal(1000UL, report.OuterTicks);
            HostCheck.Equal(2UL, report.AuditCalls);
            HostCheck.Equal(300UL, report.AuditTicks);
            HostCheck.Equal(28UL, report.OutsideDeviceCalls);
            HostCheck.Equal(1UL, report.PairCalls);
            HostCheck.Equal(400UL, report.PairTicks);
            HostCheck.Equal(100UL, report.PairAuditTicks);
            HostCheck.Equal(1UL, report.PumpCalls);
            HostCheck.Equal(200UL, report.PumpTicks);
            HostCheck.Equal(50UL, report.PumpAuditTicks);
            HostCheck.Equal(1UL, report.EngineCalls);
            HostCheck.Equal(4096UL, report.EngineOwner);
            HostCheck.Equal(2U, report.BeforePhase);
            HostCheck.Equal(1U, report.BeforeStep);
            HostCheck.Equal(5U, report.BeforeWork);
            HostCheck.True(report.BeforeIo);
            HostCheck.Equal(3U, report.AfterPhase);
            HostCheck.Equal(2U, report.AfterStep);
            HostCheck.Equal(6U, report.AfterWork);
            HostCheck.True(!report.AfterIo);
            HostCheck.Equal(12UL, report.RawReads);
            HostCheck.Equal(3UL, report.AdjacentReadTicks);
            HostCheck.Equal(150UL, report.AuditOutsideTicks);
            HostCheck.Equal(250UL, report.UnattributedTicks);
        }
    }

    [HostTest("writable timing accepts zero work and retains overhead without deduction or inferred duration")]
    private static void EmptyAndOverhead()
    {
        foreach (string row in new[] { Empty, Set(Empty, "adjacentReadTicks", ulong.MaxValue) })
        {
            var report = WritableStorageTimingReport.Read(row);
            HostCheck.True(report.Valid);
            HostCheck.Equal(0UL, report.OuterTicks);
            HostCheck.Equal(4UL, report.RawReads);
            HostCheck.Equal(0UL, report.EngineCalls);
            HostCheck.Equal(0UL, report.EngineOwner);
            HostCheck.Equal(row.EndsWith("adjacentReadTicks=0 auditOutside=0 unattributed=0", StringComparison.Ordinal) ? 0UL : ulong.MaxValue,
                report.AdjacentReadTicks);
        }
        string outside = Set(Set(Set(Empty, "outer", 75), "auditCalls", 1), "auditTicks", 50);
        outside = Set(Set(Set(outside, "rawReads", 6), "auditOutside", 50), "unattributed", 25);
        var copied = WritableStorageTimingReport.Read(outside);
        HostCheck.Equal(50UL, copied.AuditOutsideTicks);
        HostCheck.Equal(25UL, copied.UnattributedTicks);
    }

    [HostTest("writable timing invalid diagnostics retain partial overflowing accounting and progress without upgrading validity")]
    private static void PartialDiagnostics()
    {
        string row = Set(Complete, "valid", 0);
        foreach (string field in new[] { "outer", "auditCalls", "auditTicks", "outsideDeviceCalls", "pairCalls", "pairTicks",
            "pairAuditTicks", "pumpCalls", "pumpTicks", "pumpAuditTicks", "rawReads", "adjacentReadTicks", "auditOutside", "unattributed" })
            row = Set(row, field, ulong.MaxValue);
        row = Set(Set(row, "engineCalls", 0), "engineOwner", ulong.MaxValue);
        var partial = WritableStorageTimingReport.Read(row);
        HostCheck.True(!partial.Valid);
        HostCheck.Equal(row, partial.RawRow);
        HostCheck.Equal(ulong.MaxValue, partial.AuditCalls);
        HostCheck.Equal(ulong.MaxValue, partial.RawReads);
        HostCheck.Equal(ulong.MaxValue, partial.EngineOwner);
        HostCheck.Equal(0UL, partial.EngineCalls);
        HostCheck.True(partial.BeforeIo);
        HostCheck.Equal(6U, partial.AfterWork);
        Reject(Set(row, "valid", 1));
        HostCheck.True(!WritableStorageTimingReport.Read(Set(Set(Empty, "valid", 0), "engineCalls", 1)).Valid);
    }

    [HostTest("writable timing rejects malformed ordered rows and noncanonical decimals without normalizing input")]
    private static void Syntax()
    {
        foreach (string malformed in new[] { "", " " + Complete, Complete + " ", Complete + "\r\n", Complete + "\n",
            Complete.Replace("M16_027_TIMING", "M16_027_TIMINGX", StringComparison.Ordinal), Complete + " extra=1",
            Complete.Replace(" frequency=10000000", "", StringComparison.Ordinal), Complete + " width=32",
            Complete.Replace("width=32 frequency=10000000", "frequency=10000000 width=32", StringComparison.Ordinal),
            Complete.Replace(" width=", "  width=", StringComparison.Ordinal), Complete.Replace(" width=", "\twidth=", StringComparison.Ordinal),
            Complete.Replace("beforeIo=1", "beforeIo=true", StringComparison.Ordinal), Complete + "\0" })
            Reject(malformed);
        foreach (string malformed in new[] { "00", "01", "+1", "-1", "1 ", " 1", "1\t", "1.0", "0x1", "١", "１", "1_000", "18446744073709551616" })
            Reject(Set(Complete, "rawReads", malformed));
        Reject(null!, "row_size");
        Reject(new string('x', WritableStorageTimingReport.MaximumRowCharacters + 1), "row_size");
    }

    [HostTest("writable timing enforces native scalar widths and booleans even for failed diagnostics")]
    private static void ScalarWidths()
    {
        foreach (string row in new[] { Complete, Set(Complete, "valid", 0) })
        {
            foreach (ulong width in new ulong[] { 0, 31, 33, 63, 65, uint.MaxValue, (ulong)uint.MaxValue + 1 })
                Reject(Set(row, "width", width), "geometry");
            Reject(Set(row, "frequency", 0), "geometry");
            foreach (string boolean in new[] { "valid", "beforeIo", "afterIo" })
            {
                Reject(Set(row, boolean, 2), "boolean");
                Reject(Set(row, boolean, ulong.MaxValue), "boolean");
            }
            foreach (string progress in new[] { "beforePhase", "beforeStep", "beforeWork", "afterPhase", "afterStep", "afterWork" })
            {
                HostCheck.Equal((ulong)uint.MaxValue, WritableStorageTimingReport.Read(Set(row, progress, uint.MaxValue)).Values[progress]);
                Reject(Set(row, progress, (ulong)uint.MaxValue + 1), "progress_width");
            }
        }
        HostCheck.Equal(ulong.MaxValue, WritableStorageTimingReport.Read(Set(Complete, "frequency", ulong.MaxValue)).Frequency);
    }

    [HostTest("writable timing rejects inconsistent nested inclusive remainder and outer tick partitions")]
    private static void TickAccounting()
    {
        Reject(Set(Complete, "pairAuditTicks", 401), "inclusive_audit");
        Reject(Set(Complete, "pumpAuditTicks", 201), "inclusive_audit");
        Reject(Set(Complete, "auditTicks", 149), "audit_partition");
        foreach (string field in new[] { "auditOutside", "unattributed", "outer", "pairTicks", "pumpTicks" })
            Reject(Set(Complete, field, WritableStorageTimingReport.Read(Complete).Values[field] + 1));
        string allAudit = Set(Set(Set(Complete, "auditTicks", 150), "auditOutside", 0), "unattributed", 400);
        HostCheck.Equal(0UL, WritableStorageTimingReport.Read(allAudit).AuditOutsideTicks);
    }

    [HostTest("writable timing validates sample and OutsideDevice counts without inferring engine execution")]
    private static void Counts()
    {
        foreach (ulong reads in new ulong[] { 0, 4, 11, 13 })
            Reject(Set(Complete, "rawReads", reads), "raw_reads");
        Reject(Set(Complete, "outsideDeviceCalls", 29), "outside_device_calls");
        HostCheck.Equal(0UL, WritableStorageTimingReport.Read(Set(Complete, "outsideDeviceCalls", 0)).OutsideDeviceCalls);
        foreach (string field in new[] { "auditCalls", "pairCalls", "pumpCalls" })
            Reject(Set(Complete, field, WritableStorageTimingReport.Read(Complete).Values[field] + 1));
        // A positive observed engine owner is a number, not an aligned pointer or token.
        HostCheck.Equal(1UL, WritableStorageTimingReport.Read(Set(Complete, "engineOwner", 1)).EngineOwner);
    }

    [HostTest("writable timing rejects checked arithmetic wrap rather than accepting wrapped equalities")]
    private static void Overflow()
    {
        string nested = Set(Set(Set(Set(Empty, "pairAuditTicks", ulong.MaxValue), "pairTicks", ulong.MaxValue), "pumpAuditTicks", 1), "pumpTicks", 1);
        Reject(nested, "accounting_overflow");
        string outer = Set(Set(Empty, "pairTicks", ulong.MaxValue), "pumpTicks", 1);
        Reject(outer, "accounting_overflow");
        string calls = Set(Set(Empty, "auditCalls", ulong.MaxValue), "pairCalls", 1);
        Reject(calls, "accounting_overflow");
        Reject(Set(Empty, "auditCalls", ulong.MaxValue / 2), "accounting_overflow");
        ulong largeAudit = ulong.MaxValue / 14 + 1;
        string device = Set(Set(Empty, "auditCalls", largeAudit), "rawReads", checked(4 + 2 * largeAudit));
        Reject(device, "accounting_overflow");
        string outside = Set(Set(Set(Empty, "auditTicks", 1), "auditOutside", 1), "unattributed", ulong.MaxValue);
        Reject(outside, "accounting_overflow");
    }

    [HostTest("writable timing ties complete engine presence to copied owner and all progress fields")]
    private static void EnginePresence()
    {
        Reject(Set(Complete, "engineOwner", 0), "engine_owner");
        foreach (string field in new[] { "engineOwner", "beforePhase", "beforeStep", "beforeWork", "beforeIo", "afterPhase", "afterStep", "afterWork", "afterIo" })
            Reject(Set(Empty, field, 1), "engine_absent");
        HostCheck.Equal(ulong.MaxValue, WritableStorageTimingReport.Read(Set(Complete, "engineCalls", ulong.MaxValue)).EngineCalls);
        string positive = Set(Set(Empty, "engineCalls", 1), "engineOwner", ulong.MaxValue);
        HostCheck.Equal(ulong.MaxValue, WritableStorageTimingReport.Read(positive).EngineOwner);
    }

    [HostTest("writable timing values are immutable copies and grant no timing execution or device authority")]
    private static void CopiedValues()
    {
        char[] input = Complete.ToCharArray();
        string row = new(input);
        var report = WritableStorageTimingReport.Read(row);
        Array.Fill(input, 'x');
        row = "changed";
        HostCheck.Equal(Complete, report.RawRow);
        HostCheck.Equal(27, report.Values.Count);
        bool rejected = false;
        try
        {
            ((IDictionary<string, ulong>)report.Values)["valid"] = 0;
        }
        catch (NotSupportedException)
        {
            rejected = true;
        }
        HostCheck.True(rejected && report.Valid);
        HostCheck.True(!report.GrantsTimingAuthority && !report.GrantsExecutionOrProcessAuthority &&
            !report.GrantsMappingOrDeviceAuthority && !report.GrantsSourceSchedulerGuestOrPersistenceAuthority);
        HostCheck.True(!WritableStorageTimingReport.Read(Set(Complete, "valid", 0)).GrantsTimingAuthority);
    }
}
