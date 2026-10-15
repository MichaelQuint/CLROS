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
    [HostTest("linux call current wait observations preserve position generation deadline sequence and immutable values")]
    private static void WaitObservations()
    {
        string[] rows=["header",
            "M16_008_CALL identity=4294967297 nr=202 ordinal=1 waitSource=1 waitDeadline=1050 waitSequence=7",
            "M16_008_CALL identity=4294967298 nr=0 ordinal=1 waitSource=2 waitDeadline=0 waitSequence=8",
            "M16_008_CALL identity=4294967297 nr=202 ordinal=2 waitSource=1 waitDeadline=0 waitSequence=9",
            "M16_008_CALL identity=8589934594 nr=39 ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0"];
        string[] before=(string[])rows.Clone();
        var values=QemuTranscripts.ReadLinuxCallWaits(rows);
        HostCheck.Equal(4,values.Count);
        HostCheck.Equal(new QemuTranscripts.LinuxCallWaitObservation(1,4294967297,1,1,1050,7),values[0]);
        HostCheck.Equal(new QemuTranscripts.LinuxCallWaitObservation(2,4294967298,1,2,0,8),values[1]);
        HostCheck.True(values[2].Ordinal==2 && values[2].Deadline==0 && values[2].Sequence==9 && values[3].Identity==8589934594 && values[3].Source==0);
        var stripped=QemuTranscripts.ValidateLinuxCallOrdinals(rows);
        HostCheck.Equal("M16_008_CALL identity=4294967297 nr=202",stripped[1]);
        HostCheck.True(rows.SequenceEqual(before)); Array.Fill(rows,"changed");
        HostCheck.True(values[0].Deadline==1050 && values is not QemuTranscripts.LinuxCallWaitObservation[]);
        if(values is IList<QemuTranscripts.LinuxCallWaitObservation> list)
        {
            bool denied=false; try{list[0]=default;}catch(NotSupportedException){denied=true;} HostCheck.True(denied);
        }
        // Old consumer compatibility never supplies a missing witness to the new reader.
        RejectWait(["M16_008_CALL identity=4294967297 nr=39 ordinal=1"]);
        RejectWait([before[1],"M16_008_CALL identity=4294967298 nr=39 ordinal=1"]);
    }
    [HostTest("linux call wait observations reject partial duplicated inconsistent noncanonical and overflowing witnesses")]
    private static void WaitFailures()
    {
        const string row="M16_008_CALL identity=4294967297 nr=202 ordinal=1";
        foreach(string suffix in new[]{
            " waitSource=0", " waitSource=1 waitDeadline=1050", " waitSource=1 waitDeadline=1050 waitSequence=0",
            " waitSource=0 waitDeadline=1 waitSequence=0", " waitSource=0 waitDeadline=0 waitSequence=1",
            " waitSource=2 waitDeadline=1050 waitSequence=1", " waitSource=3 waitDeadline=0 waitSequence=1",
            " waitSource=01 waitDeadline=0 waitSequence=1", " waitSource=1 waitDeadline=01050 waitSequence=1",
            " waitSource=1 waitDeadline=0 waitSequence=01", " waitSource=1 waitDeadline=-1 waitSequence=1",
            " waitSource=1 waitDeadline=9223372036854775808 waitSequence=1",
            " waitSource=1 waitDeadline=18446744073709551616 waitSequence=1",
            " waitSource=1 waitDeadline=0 waitSequence=18446744073709551616",
            " waitSource=0 waitDeadline=0 waitSequence=0 extra",
            " waitSource=0 waitDeadline=0 waitSequence=0 waitSource=0",
            " ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0"})
        { Reject([row+suffix]); RejectWait([row+suffix]); }
        RejectWait(["M16_008_CALL identity=4294967297 nr=202 waitSource=1 ordinal=1 waitSource=0 waitDeadline=0 waitSequence=0"]);
        RejectWait([row+" waitSource=0 waitDeadline=0 waitSequence=0",row+" waitSource=0 waitDeadline=0 waitSequence=0"]);
    }
    private static void RejectWait(string[] rows)
    { bool rejected=false; try{QemuTranscripts.ReadLinuxCallWaits(rows);}catch(ToolFailure){rejected=true;} HostCheck.True(rejected); }
    private static void Reject(string[] rows)
    { bool rejected=false; try{QemuTranscripts.ValidateLinuxCallOrdinals(rows);}catch(ToolFailure){rejected=true;} HostCheck.True(rejected); }
}
