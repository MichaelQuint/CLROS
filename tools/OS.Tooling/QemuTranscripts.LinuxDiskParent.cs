namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private static readonly string[] DiskFileEndMarkers =
    [ "CLROS_M16_023_READY", "TEST_PASS m16.023", "CLROS_M16_024_READY", "TEST_PASS m16.024",
      "CLROS_M16_025_READY", "TEST_PASS m16.025" ];

    private static Dictionary<string, ulong> ReadDiskFileEnd(string row)
    {
        const string suffix = " device=retired fs=retired dma=zero descriptors=closed reservation=retained";
        Need(row.EndsWith(suffix, StringComparison.Ordinal), "disk_parent_cleanup");
        var end = ReadRecord(row[..^suffix.Length], "M16_025_END", "submitted:dec", "completed:dec", "copied:dec",
            "bootstrapCompleted:dec", "bootstrapCopied:dec", "fsReads:dec", "cacheHits:dec", "cacheMisses:dec");
        Need(end["bootstrapCompleted"] >= 35 && end["bootstrapCopied"] == end["bootstrapCompleted"] &&
            end["submitted"] > end["bootstrapCompleted"] && end["submitted"] == end["completed"] &&
            end["copied"] == end["completed"] && end["fsReads"] <= ulong.MaxValue - 34 &&
            end["fsReads"] + 34 == end["submitted"] && end["cacheHits"] <= long.MaxValue &&
            end["cacheMisses"] <= end["fsReads"], "disk_parent_counters");
        return end;
    }

    /// <summary>Check only the supplied mechanical disk/NIC envelope values,
    /// including exact final disk counters and retired owners. The canonical
    /// leaf, firmware/startup, sector bytes, native code, original runner and
    /// independent packets remain mandatory separate joins. Inputs are borrowed;
    /// malformed, excessive, unknown or incomplete rows throw.</summary>
    public static void InspectLinuxDiskNetworkForTests(string[] rows)
    {
        Need(rows is { Length: > 0 and <= 131072 } && rows.All(row => row is { Length: > 0 and <= 1536 } &&
            row.All(value => value is >= ' ' and <= '~')), "disk_parent_input");
        _ = SocketNetworkEnvelope(rows, true, mechanical: true, diskFiles: true);
    }
}
