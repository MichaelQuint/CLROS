using OS.Tooling;

internal static class LinuxCallOrdinalTests
{
    [HostTest("linux call ordinals bind independent generations and leave original records immutable")]
    private static void Generations()
    {
        string[] rows=["header","M16_008_CALL identity=4294967297 nr=39 ordinal=1","M16_008_CALL identity=4294967298 nr=39 ordinal=1",
            "M16_008_CALL identity=4294967297 nr=186 ordinal=2","M16_008_CALL identity=8589934594 nr=39 ordinal=1"];
        var parsed=QemuTranscripts.ValidateLinuxCallOrdinals(rows);
        HostCheck.True(parsed[0]=="header" && parsed[3]=="M16_008_CALL identity=4294967297 nr=186" && rows[3].EndsWith(" ordinal=2",StringComparison.Ordinal));
    }
    [HostTest("linux call ordinals reject missing lost duplicate reordered overflow and noncanonical counters")]
    private static void Rejections()
    {
        foreach(string value in new[]{""," ordinal=0"," ordinal=01"," ordinal=2"," ordinal=4097"," ordinal=18446744073709551616"," ordinal=1 extra"})
            Reject(["M16_008_CALL identity=4294967297 nr=39"+value]);
        Reject(["M16_008_CALL identity=4294967297 nr=39 ordinal=1","M16_008_CALL identity=4294967297 nr=186 ordinal=1"]);
        Reject(["M16_008_CALL identity=4294967297 nr=39 ordinal=1","M16_008_CALL identity=4294967297 nr=186 ordinal=3"]);
    }
    internal static string[] Annotate(IEnumerable<string> rows)
    {
        Dictionary<string,int> counts=[];
        return rows.Select(row=>
        {
            if(!row.StartsWith("M16_008_CALL ",StringComparison.Ordinal)) return row;
            string identity=row.Split(' ')[1]; counts.TryGetValue(identity,out int count); counts[identity]=++count;
            return row+" ordinal="+count;
        }).ToArray();
    }
    private static void Reject(string[] rows)
    { bool rejected=false; try{QemuTranscripts.ValidateLinuxCallOrdinals(rows);}catch(ToolFailure){rejected=true;} HostCheck.True(rejected); }
}
