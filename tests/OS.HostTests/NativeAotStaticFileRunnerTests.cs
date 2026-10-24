using System.Globalization;
using OS.Tooling;
using OS.Tools.QemuRunner;

internal static class NativeAotStaticFileRunnerTests
{
    private static readonly DateTimeOffset Start=new(2026,10,23,12,0,0,TimeSpan.Zero);
    private static QemuTranscripts.WebRunnerContext Context=>new(40000,"qemu","ovmf",Start.AddSeconds(-1),Start.AddSeconds(151));
    private static string[] Rows()=>[
        "STATIC_CHILD_ID 42","STATIC_CHILD_START_UTC "+Start.UtcDateTime.ToString("O",CultureInfo.InvariantCulture),
        "STATIC_CHILD_END_UTC "+Start.AddSeconds(150).UtcDateTime.ToString("O",CultureInfo.InvariantCulture),
        "STATIC_CHILD_LIFETIME_MS 150000","STATIC_ENDPOINT_PRELAUNCH_AVAILABLE true","STATIC_ENDPOINT_STOPPED_REFUSED true",
        "STATIC_CHILD_TERMINATION_REQUESTED true","STATIC_CHILD_DEATH_CONFIRMED true","STATIC_CHILD_SPONTANEOUS false",
        "STATIC_CHILD_CLEANUP_FAILED false","STATIC_CHILD_EXIT_CODE -1","STATIC_CHILD_FAILURE none"];
    private static void Reject(Action action,string reason)
    {
        try{action();throw new InvalidOperationException("accepted invalid "+reason);}
        catch(ToolFailure error){HostCheck.True(error.Message.Contains(reason,StringComparison.Ordinal),error.Message);}
    }
    [HostTest("static child receipt requires original invocation lifetime and explicit owned termination")]
    private static void Child()
    {
        var report=QemuTranscripts.InspectNativeAotStaticFileChildForTests(Rows(),Context,600);
        HostCheck.True(report.ProcessId==42 && report.LifetimeMilliseconds==150000);
        HostCheck.Equal(StaticFileProcessEvidence.SummaryCount,new StaticFileProcessEvidence().Summary().Count);
        foreach(int index in new[]{4,5,6,7,8,9,11})
        {
            string[] rows=Rows();rows[index]=rows[index].Replace("true","false",StringComparison.Ordinal).Replace("none","failure",StringComparison.Ordinal);
            if(index is 8 or 9)rows[index]=rows[index].Replace("false","true",StringComparison.Ordinal);
            Reject(()=>QemuTranscripts.InspectNativeAotStaticFileChildForTests(rows,Context,600),"static_child_completion");
        }
    }
    [HostTest("static child receipt rejects missing malformed and unrelated process windows")]
    private static void ChildBounds()
    {
        foreach((int index,string value,string reason) in new[]{(0,"STATIC_CHILD_ID 042","static_child_number"),
            (3,"STATIC_CHILD_LIFETIME_MS 149999","static_child_lifetime"),(10,"STATIC_CHILD_EXIT_CODE none","static_child_exit"),
            (1,"STATIC_CHILD_START_UTC 2026-10-23T12:00:00.0000000+00:00","static_child_utc")})
        {string[] rows=Rows();rows[index]=value;Reject(()=>QemuTranscripts.InspectNativeAotStaticFileChildForTests(rows,Context,600),reason);}
        Reject(()=>QemuTranscripts.InspectNativeAotStaticFileChildForTests(Rows()[..^1],Context,600),"static_child_rows");
        Reject(()=>QemuTranscripts.InspectNativeAotStaticFileChildForTests(Rows(),Context with{StartedUtc=Start.AddSeconds(1)},600),"static_child_lifetime");
    }
    private static QemuTranscripts.StaticFileSocketIo[] Calls()=>[
        new(0,100,20,0,1,true,true),new(1,200,30,0,1,true,true),new(2,33000,50,1,0,true,true)];
    private static QemuTranscripts.StaticFileHttpFlow[] Flows()=>[
        new(40001,20,100,false),new(40002,30,200,false),new(40003,50,232,true)];
    [HostTest("static syscall wire join permits only the reset flow bounded queued unsent suffix")]
    private static void Io()
    {
        QemuTranscripts.InspectNativeAotStaticFileIoJoinForTests(Calls(),Flows());
        foreach(int index in new[]{0,1,2})
        {
            var calls=Calls();calls[index]=calls[index] with{Received=calls[index].Received+1};
            Reject(()=>QemuTranscripts.InspectNativeAotStaticFileIoJoinForTests(calls,Flows()),"static_io_stream_join");
        }
        var suffix=Calls();suffix[2]=suffix[2] with{Sent=33001};
        Reject(()=>QemuTranscripts.InspectNativeAotStaticFileIoJoinForTests(suffix,Flows()),"static_io_stream_join");
        var normal=Calls();normal[0]=normal[0] with{Sent=101};
        Reject(()=>QemuTranscripts.InspectNativeAotStaticFileIoJoinForTests(normal,Flows()),"static_io_stream_join");
        var wrong=Calls();(wrong[0],wrong[1])=(wrong[1],wrong[0]);
        Reject(()=>QemuTranscripts.InspectNativeAotStaticFileIoJoinForTests(wrong,Flows()),"static_io_stream_join");
    }
}
