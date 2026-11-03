using System.Collections.ObjectModel;

namespace OS.Tooling;

/// <summary>Immutable copied diagnostics from one exact M16_027_TIMING row.
/// This host parser checks syntax and, only for valid reports, accounting.
/// It authenticates no clock, counter, original execution, owner or mapping.</summary>
public sealed class WritableStorageTimingReport
{
    public const int MaximumRowCharacters = 4096;
    private static readonly string[] Fields = ("width frequency valid outer auditCalls auditTicks outsideDeviceCalls " +
        "pairCalls pairTicks pairAuditTicks pumpCalls pumpTicks pumpAuditTicks engineCalls engineOwner " +
        "beforePhase beforeStep beforeWork beforeIo afterPhase afterStep afterWork afterIo rawReads " +
        "adjacentReadTicks auditOutside unattributed").Split(' ').Select(field => field + ":dec").ToArray();
    private readonly ReadOnlyDictionary<string, ulong> values;

    private WritableStorageTimingReport(string row, Dictionary<string, ulong> parsed)
    {
        RawRow = row;
        values = new(parsed);
    }

    public string RawRow
    {
        get;
    }
    public IReadOnlyDictionary<string, ulong> Values => values;
    public uint Width => (uint)values["width"];
    public ulong Frequency => values["frequency"];
    public bool Valid => values["valid"] == 1;
    public ulong OuterTicks => values["outer"];
    public ulong AuditCalls => values["auditCalls"];
    public ulong AuditTicks => values["auditTicks"];
    public ulong OutsideDeviceCalls => values["outsideDeviceCalls"];
    public ulong PairCalls => values["pairCalls"];
    public ulong PairTicks => values["pairTicks"];
    public ulong PairAuditTicks => values["pairAuditTicks"];
    public ulong PumpCalls => values["pumpCalls"];
    public ulong PumpTicks => values["pumpTicks"];
    public ulong PumpAuditTicks => values["pumpAuditTicks"];
    public ulong EngineCalls => values["engineCalls"];
    /// <summary>Copied number only; neither an authenticated pointer nor an owner token.</summary>
    public ulong EngineOwner => values["engineOwner"];
    public uint BeforePhase => (uint)values["beforePhase"];
    public uint BeforeStep => (uint)values["beforeStep"];
    public uint BeforeWork => (uint)values["beforeWork"];
    public bool BeforeIo => values["beforeIo"] == 1;
    public uint AfterPhase => (uint)values["afterPhase"];
    public uint AfterStep => (uint)values["afterStep"];
    public uint AfterWork => (uint)values["afterWork"];
    public bool AfterIo => values["afterIo"] == 1;
    public ulong RawReads => values["rawReads"];
    public ulong AdjacentReadTicks => values["adjacentReadTicks"];
    public ulong AuditOutsideTicks => values["auditOutside"];
    public ulong UnattributedTicks => values["unattributed"];
    public bool GrantsTimingAuthority => false;
    public bool GrantsExecutionOrProcessAuthority => false;
    public bool GrantsMappingOrDeviceAuthority => false;
    public bool GrantsSourceSchedulerGuestOrPersistenceAuthority => false;

    /// <summary>Synchronously read one stable logical row of at most4096 chars.
    /// Exact field order, canonical unsigned decimal, native scalar widths and
    /// boolean shapes apply to every row. valid=0 retains partial diagnostics
    /// without demanding complete accounting. valid=1 requires checked complete
    /// identities; raw totals include overhead, which is never deducted.
    /// Reject malformed, overflowing or inconsistent input with ToolFailure.
    /// No input files/processes are opened and no execution authority is granted.</summary>
    public static WritableStorageTimingReport Read(string row)
    {
        if (row is null || row.Length is 0 or > MaximumRowCharacters)
            throw new ToolFailure("writable_timing_row_size");
        var parsed = QemuTranscripts.ReadRecord(row, "M16_027_TIMING", Fields);
        Need(parsed["width"] <= uint.MaxValue && parsed["width"] is 32 or 64 && parsed["frequency"] != 0,
            "geometry");
        foreach (string name in new[] { "valid", "beforeIo", "afterIo" })
            Need(parsed[name] <= 1, "boolean");
        foreach (string name in new[] { "beforePhase", "beforeStep", "beforeWork", "afterPhase", "afterStep", "afterWork" })
            Need(parsed[name] <= uint.MaxValue, "progress_width");
        var report = new WritableStorageTimingReport(row, parsed);
        if (report.Valid)
            report.CheckAccounting();
        return report;
    }

    private void CheckAccounting()
    {
        try
        {
            Need(PairAuditTicks <= PairTicks && PumpAuditTicks <= PumpTicks, "inclusive_audit");
            ulong nested = checked(PairAuditTicks + PumpAuditTicks);
            Need(nested <= AuditTicks, "audit_partition");
            Need(AuditOutsideTicks == AuditTicks - nested, "audit_outside");
            Need(OuterTicks == checked(PairTicks + PumpTicks + AuditOutsideTicks + UnattributedTicks), "outer_partition");
            Need(RawReads == checked(4UL + 2UL * checked(AuditCalls + PairCalls + PumpCalls)), "raw_reads");
            Need(OutsideDeviceCalls <= checked(14UL * AuditCalls), "outside_device_calls");
        }
        catch (OverflowException) { throw new ToolFailure("writable_timing_accounting_overflow"); }
        if (EngineCalls == 0)
            Need(EngineOwner == 0 && BeforePhase == 0 && BeforeStep == 0 && BeforeWork == 0 && !BeforeIo &&
                AfterPhase == 0 && AfterStep == 0 && AfterWork == 0 && !AfterIo, "engine_absent");
        else
            Need(EngineOwner != 0, "engine_owner");
    }

    private static void Need(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("writable_timing_" + reason);
    }
}
