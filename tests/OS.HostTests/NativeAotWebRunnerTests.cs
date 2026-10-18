using System.Security.Cryptography;
using System.Text;
using OS.Tooling;

internal static class NativeAotWebRunnerTests
{
    private const string Body="OK",Config="{}";
    private sealed record Fixture(QemuSmokeOptions Options,QemuSmokePlan Plan,ProcessResult Runner,byte[] Raw,byte[] Summary,QemuTranscripts.WebRunnerContext Context);
    // Independent small protocol fixture: three completed markers and one
    // unfinished unselected kernel row. No firmware/guest authority is claimed.
    private static Fixture Vector(bool quota=false,int loggingMilliseconds=300,int loggingTimeout=1)
    {
        string[] markers=["TEST_START m10.004","M10_004_LIVE","TEST_PASS m10.004"];
        string raw=string.Join("\r\n",markers)+"\r\nM16_008_CALL identity=4294967297 nr=202";
        string markerList=string.Join(';',markers),qemu=Path.GetFullPath("qemu.exe"),ovmf=Path.GetFullPath("ovmf.fd");
        string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.ASCII.GetBytes(value))).ToLowerInvariant();
        string healthHash=Hash(Body),configHash=Hash(Config);
        string loggingBody=quota?"":"CLROS_LOG_OK workers=8 records=64 writev=32 invalid_errno=9";
        List<string> extraOut=[],extraSummary=[];
        List<(string Prefix,string Path,string Method,int Status,string Body,int Time)> stages=[
            ("WEB_LOG_PROBE","/__clros/logexercise","POST",quota?500:200,loggingBody,loggingMilliseconds),
            ("WEB_LOG_REPEAT_PROBE","/__clros/logexercise","POST",409,"",100),
            ("WEB_HEALTH_AFTER_PROBE","/healthz","GET",200,Body,100)];
        if(!quota)stages.Insert(2,("WEB_VECTOR_PROBE","/__clros/vectors","GET",200,"",150));
        foreach(var stage in stages)
        {
            bool binary=stage.Prefix=="WEB_VECTOR_PROBE";
            int length=binary?49152:Encoding.ASCII.GetByteCount(stage.Body);string hash=binary?"664d1e34fe80e8713fefa2b9c30df7d7877bbffd850411ffda14f54bd1ce847c":Hash(stage.Body);
            extraOut.Add($"HTTP_PROBE_START endpoint=http://127.0.0.1:20080 expected_status={stage.Status} expected_body_bytes={length} timeout_seconds={(stage.Prefix=="WEB_LOG_PROBE"?loggingTimeout:1)}");
            extraOut.Add($"HTTP_PROBE_PASS endpoint=http://127.0.0.1:20080 status={stage.Status} body_bytes={length} body_sha256={hash} elapsed_ms={stage.Time}");
            extraSummary.AddRange([stage.Prefix+"_URL http://127.0.0.1:20080"+stage.Path,stage.Prefix+"_METHOD "+stage.Method,
                stage.Prefix+"_EXPECTED_STATUS "+stage.Status,stage.Prefix+"_EXPECTED_BODY_BYTES "+length,stage.Prefix+"_EXPECTED_BODY_SHA256 "+hash,
                stage.Prefix+"_RESULT pass",stage.Prefix+"_REASON none",stage.Prefix+"_STATUS "+stage.Status,stage.Prefix+"_BODY_BYTES "+length,
                stage.Prefix+"_BODY_SHA256 "+hash,stage.Prefix+"_ELAPSED_MS "+stage.Time]);
        }
        int lifetime=Math.Max(3000,loggingMilliseconds+2000);
        var first=new DateTimeOffset(2000,1,1,0,0,0,TimeSpan.Zero);
        List<string> utcRows=["WEB_HTTP_UTC_TOLERANCE_SECONDS 5","WEB_HTTP_UTC_WINDOWS "+(quota?5:6)];
        int elapsed=500,index=0;
        foreach(int duration in new[]{100,200}.Concat(stages.Select(s=>s.Time)))
        {
            string prefix="WEB_HTTP_UTC_"+index++;
            utcRows.Add(prefix+"_STARTED "+first.AddMilliseconds(elapsed).UtcDateTime.ToString("O"));
            elapsed+=duration;
            utcRows.Add(prefix+"_COMPLETED "+first.AddMilliseconds(elapsed).UtcDateTime.ToString("O"));
        }
        var options=new QemuSmokeOptions{Image="image.raw",SerialLog="serial.log",SummaryLog="summary.log",TimeoutSeconds=Math.Max(30,(lifetime+999)/1000),FirmwareRngSource="virtio"};
        var plan=new QemuSmokePlan("Diagnostic",new Dictionary<string,bool>{{"EnableM10004Probe",true}},markers,markers,markers,["TEST_FAIL","OS_SMOKE_FAIL"],[],false,true);
        string[] stdout=["QEMU_DISCOVERY result="+qemu+" checked="+qemu,"OVMF_DISCOVERY result="+ovmf+" checked="+ovmf,
            "TEST_START qemu.smoke","IMAGE_ARTIFACT image.raw","SERIAL_LOG serial.log","OVMF_CODE "+ovmf,
            "WORKLOAD_PROFILE SingleMinimalApi","FIRMWARE_RNG_SOURCE virtio","SOCKET_FORWARD_PORT 20080","MARKER_PRESETS none",
            "REQUIRED_MARKERS "+markerList,"ORDERED_MARKERS "+markerList,"UNIQUE_MARKERS "+markerList,"FORBIDDEN_MARKERS TEST_FAIL;OS_SMOKE_FAIL",
            "HTTP_PROBE_START endpoint=http://127.0.0.1:20080 expected_status=200 expected_body_bytes=2 timeout_seconds=2",
            "HTTP_PROBE_PASS endpoint=http://127.0.0.1:20080 status=200 body_bytes=2 body_sha256="+healthHash+" elapsed_ms=100",
            "HTTP_PROBE_START endpoint=http://127.0.0.1:20080 expected_status=200 expected_body_bytes=2 timeout_seconds=1",
            "HTTP_PROBE_PASS endpoint=http://127.0.0.1:20080 status=200 body_bytes=2 body_sha256="+configHash+" elapsed_ms=200",
            ..extraOut,
            $"SERIAL_MARKER_STREAM status=Passed terminal=Success bytes={raw.Length} lines=4 invalidBytes=0 invalidLines=0 orderedSeen=3 outOfOrder=0 uniqueSeen=3 duplicateUnique=0",
            "SERIAL_MARKER_DRAIN milliseconds=750","QEMU_PROCESS_RESULT disposition=runner-terminated exitCode=-1","SUMMARY_LOG summary.log","TEST_PASS qemu.smoke"];
        string[] summary=["QEMU_SMOKE_STATUS pass","IMAGE_ARTIFACT image.raw","SERIAL_LOG serial.log","WORKLOAD_PROFILE SingleMinimalApi",
            "FIRMWARE_RNG_SOURCE virtio","SOCKET_FORWARD_PORT 20080","MARKER_PRESETS none","REQUIRED_MARKERS "+markerList,
            "ORDERED_MARKERS "+markerList,"UNIQUE_MARKERS "+markerList,"FORBIDDEN_MARKERS TEST_FAIL;OS_SMOKE_FAIL",
            "HTTP_PROBE_ENDPOINT http://127.0.0.1:20080","HTTP_PROBE_EXPECTED_STATUS 200","HTTP_PROBE_EXPECTED_BODY_BYTES 2","HTTP_PROBE_TIMEOUT_SECONDS 20",
            "HTTP_PROBE_RESULT pass","HTTP_PROBE_REASON none","HTTP_PROBE_STATUS 200","HTTP_PROBE_BODY_BYTES 2","HTTP_PROBE_BODY_SHA256 "+healthHash,
            "HTTP_PROBE_ELAPSED_MS 100","CONTINUING_WEB true","WEB_LOG_CASE "+(quota?"records64":"normal"),"WEB_CONFIG_PROBE_URL http://127.0.0.1:20080/config","WEB_CONFIG_PROBE_EXPECTED_STATUS 200",
            "WEB_CONFIG_PROBE_EXPECTED_BODY_BYTES 2","WEB_CONFIG_PROBE_EXPECTED_BODY_SHA256 "+configHash,"WEB_CONFIG_PROBE_RESULT pass","WEB_CONFIG_PROBE_REASON none",
            "WEB_CONFIG_PROBE_STATUS 200","WEB_CONFIG_PROBE_BODY_BYTES 2","WEB_CONFIG_PROBE_BODY_SHA256 "+configHash,"WEB_CONFIG_PROBE_ELAPSED_MS 200",
            ..extraSummary,
            "WEB_ENDPOINT_PRELAUNCH_AVAILABLE true","WEB_ENDPOINT_STOPPED_REFUSED true","WEB_PROCESS_ID 123",
            "WEB_PROCESS_START_UTC 2000-01-01T00:00:00.0000000Z","WEB_PROCESS_END_UTC "+first.AddMilliseconds(lifetime).UtcDateTime.ToString("O"),"WEB_PROCESS_LIFETIME_MS "+lifetime,
            ..utcRows,
            "WEB_PROCESS_ALIVE_BEFORE_HEALTH true","WEB_PROCESS_ALIVE_AFTER_HEALTH true","WEB_PROCESS_ALIVE_AFTER_CONFIG true",
            "WEB_PROCESS_ALIVE_AFTER_LOG true","WEB_PROCESS_ALIVE_AFTER_LOG_REPEAT true",
            ..quota?Array.Empty<string>():new[]{"WEB_PROCESS_ALIVE_AFTER_VECTORS true"},"WEB_PROCESS_ALIVE_AFTER_FINAL_HEALTH true",
            "WEB_PROCESS_TERMINATION_REQUESTED true","WEB_PROCESS_DEATH_CONFIRMED true","WEB_PROCESS_SPONTANEOUS_EXIT false","WEB_PROCESS_EXIT_CODE -1","WEB_PROCESS_FAILURE none",
            "OBSERVED_MARKER TEST_START m10.004","OBSERVED_MARKER M10_004_LIVE","OBSERVED_MARKER TEST_PASS m10.004","OBSERVED_MARKER M16_008_CALL identity=4294967297 nr=202",
            "APP_EXIT_RESULT none","APP_EXIT_MALFORMED none","MISSING_MARKERS none","OUT_OF_ORDER_MARKERS none","DUPLICATE_UNIQUE_MARKERS none","FORBIDDEN_MARKER_HIT none","FAIL_REASON none",
            "SERIAL_TAIL_LIMIT lines=8 chars=240","SERIAL_TAIL_LINE TEST_START m10.004","SERIAL_TAIL_LINE M10_004_LIVE","SERIAL_TAIL_LINE TEST_PASS m10.004","SERIAL_TAIL_LINE M16_008_CALL identity=4294967297 nr=202"];
        return new(options,plan,new(0,string.Join("\r\n",stdout)+"\r\n","",false,false,false,TimeSpan.FromMilliseconds(lifetime+1000)),Encoding.UTF8.GetBytes(raw),
            [..Encoding.UTF8.GetPreamble(),..Encoding.UTF8.GetBytes(string.Join("\r\n",summary)+"\r\n")],new(20080,qemu,ovmf,first.AddSeconds(-1),first.AddMilliseconds(lifetime+1000)));
    }
    private static QemuTranscripts.WebRunnerReport Inspect(Fixture f,bool quota=false)=>QemuTranscripts.InspectNativeAotWebRunnerForTests(f.Options,f.Plan,f.Runner,f.Raw,f.Summary,f.Context,Body,Config,quota);
    private static Fixture Summary(Fixture f,string before,string after)
    {
        string text=Encoding.UTF8.GetString(f.Summary);HostCheck.True(text.Contains(before,StringComparison.Ordinal));
        return f with{Summary=Encoding.UTF8.GetBytes(text.Replace(before,after,StringComparison.Ordinal))};
    }
    private static void Reject(Action action)
    {bool rejected=false;try{action();}catch(ToolFailure){rejected=true;}HostCheck.True(rejected);}

    [HostTest("web runner joins actual selection bounded probe deadlines and exact raw summary")]
    private static void Positive()
    {
        var report=Inspect(Vector());HostCheck.True(report.ProcessId==123 && report.LifetimeMilliseconds==3000 &&
            report.HealthMilliseconds==100 && report.ConfigurationMilliseconds==200 && report.UtcObservations.Count==6);
        HostCheck.Equal(new DateTimeOffset(2000,1,1,0,0,0,TimeSpan.Zero).AddMilliseconds(500),report.UtcObservations[0].StartedUtc);
    }
    [HostTest("web runner permits bounded traced logging while preserving other request deadlines")]
    private static void LoggingDeadline()
    {
        _=Inspect(Vector(loggingMilliseconds:25000,loggingTimeout:60));
        Reject(()=>Inspect(Vector(loggingMilliseconds:60001,loggingTimeout:60)));
        Reject(()=>Inspect(Vector(loggingTimeout:61)));
        var f=Vector();string[] rows=f.Runner.StandardOutput.Split("\r\n");
        rows[20]=rows[20].Replace("timeout_seconds=1","timeout_seconds=21",StringComparison.Ordinal);
        Reject(()=>Inspect(f with{Runner=f.Runner with{StandardOutput=string.Join("\r\n",rows)}}));
    }
    [HostTest("web runner quota binds empty 500 actual case repeat 409 and later health 200")]
    private static void Quota()
    {
        var f=Vector(true);HostCheck.Equal(123,Inspect(f,true).ProcessId);
        Reject(()=>Inspect(f));Reject(()=>Inspect(Vector(),true));
        Reject(()=>Inspect(Summary(f,"WEB_LOG_CASE records64","WEB_LOG_CASE normal"),true));
        Reject(()=>Inspect(Summary(f,"WEB_LOG_PROBE_STATUS 500","WEB_LOG_PROBE_STATUS 200"),true));
        Reject(()=>Inspect(Summary(f,"WEB_HEALTH_AFTER_PROBE_STATUS 200","WEB_HEALTH_AFTER_PROBE_STATUS 500"),true));
        Reject(()=>Inspect(Summary(f,"WEB_PROCESS_ALIVE_AFTER_LOG true","WEB_PROCESS_ALIVE_AFTER_LOG false"),true));
    }
    [HostTest("web runner requires ordered UTC probe windows inside the owned child and matching elapsed bounds")]
    private static void UtcWindows()
    {
        var f=Vector();
        Reject(()=>Inspect(Summary(f,"WEB_HTTP_UTC_TOLERANCE_SECONDS 5","WEB_HTTP_UTC_TOLERANCE_SECONDS 6")));
        Reject(()=>Inspect(Summary(f,"WEB_HTTP_UTC_WINDOWS 6","WEB_HTTP_UTC_WINDOWS 5")));
        Reject(()=>Inspect(Summary(f,"WEB_HTTP_UTC_0_STARTED 2000-01-01T00:00:00.5000000Z","WEB_HTTP_UTC_0_STARTED 1999-12-31T23:59:59.5000000Z")));
        Reject(()=>Inspect(Summary(f,"WEB_HTTP_UTC_1_STARTED 2000-01-01T00:00:00.6000000Z","WEB_HTTP_UTC_1_STARTED 2000-01-01T00:00:00.5500000Z")));
        Reject(()=>Inspect(Summary(f,"WEB_HTTP_UTC_2_COMPLETED 2000-01-01T00:00:01.1000000Z","WEB_HTTP_UTC_2_COMPLETED 2000-01-01T00:00:00.7000000Z")));
        Reject(()=>Inspect(Summary(f,"WEB_HTTP_UTC_5_COMPLETED 2000-01-01T00:00:01.4500000Z","WEB_HTTP_UTC_5_COMPLETED 2000-01-01T00:00:04.0000000Z")));
        foreach(string invalid in new[]{"99999-01-01T00:00:00.0000000Z","2000-01-01T00:00:00.5000000+01:00"})
            Reject(()=>Inspect(Summary(f,"WEB_HTTP_UTC_0_STARTED 2000-01-01T00:00:00.5000000Z","WEB_HTTP_UTC_0_STARTED "+invalid)));
        Reject(()=>Inspect(Summary(f,"WEB_HTTP_UTC_0_STARTED 2000-01-01T00:00:00.5000000Z\r\n","")));
        Reject(()=>Inspect(Summary(f,"WEB_HTTP_UTC_WINDOWS 6","WEB_HTTP_UTC_WINDOWS 6\r\nWEB_HTTP_UTC_WINDOWS 6")));
        var slow=Vector(loggingMilliseconds:18000,loggingTimeout:30);
        Reject(()=>Inspect(Summary(slow,"WEB_HTTP_UTC_2_COMPLETED 2000-01-01T00:00:18.8000000Z","WEB_HTTP_UTC_2_COMPLETED 2000-01-01T00:00:00.9000000Z")));
    }

    [HostTest("web runner rejects missing liveness premature exit false death timestamps and stale invocation")]
    private static void Lifetime()
    {
        var f=Vector();
        foreach(string key in new[]{"WEB_PROCESS_ALIVE_BEFORE_HEALTH","WEB_PROCESS_ALIVE_AFTER_HEALTH","WEB_PROCESS_ALIVE_AFTER_CONFIG",
            "WEB_PROCESS_ALIVE_AFTER_LOG","WEB_PROCESS_ALIVE_AFTER_LOG_REPEAT","WEB_PROCESS_ALIVE_AFTER_VECTORS","WEB_PROCESS_ALIVE_AFTER_FINAL_HEALTH",
            "WEB_PROCESS_TERMINATION_REQUESTED","WEB_PROCESS_DEATH_CONFIRMED","WEB_ENDPOINT_PRELAUNCH_AVAILABLE","WEB_ENDPOINT_STOPPED_REFUSED"})
            Reject(()=>Inspect(Summary(f,key+" true",key+" false")));
        Reject(()=>Inspect(Summary(f,"WEB_PROCESS_SPONTANEOUS_EXIT false","WEB_PROCESS_SPONTANEOUS_EXIT true")));
        Reject(()=>Inspect(Summary(f,"WEB_PROCESS_LIFETIME_MS 3000","WEB_PROCESS_LIFETIME_MS 4000")));
        Reject(()=>Inspect(Summary(f,"WEB_PROCESS_ID 123","WEB_PROCESS_ID 0123")));
        Reject(()=>Inspect(f with{Context=f.Context with{StartedUtc=f.Context.StartedUtc.AddMinutes(1),CompletedUtc=f.Context.CompletedUtc.AddMinutes(1)}}));
        Reject(()=>Inspect(f with{Runner=f.Runner with{Duration=TimeSpan.Zero}}));
        Reject(()=>Inspect(f with{Runner=f.Runner with{TerminationConfirmed=false}}));
        Reject(()=>Inspect(f with{Runner=f.Runner with{TimedOut=true}}));
        Reject(()=>Inspect(f with{Runner=f.Runner with{StandardError="native error"}}));
    }
    [HostTest("web runner rejects changed HTTP observations markers stream metrics and selected executable")]
    private static void Observations()
    {
        var f=Vector();Reject(()=>Inspect(Summary(f,"HTTP_PROBE_STATUS 200","HTTP_PROBE_STATUS 201")));
        Reject(()=>Inspect(Summary(f,"WEB_CONFIG_PROBE_BODY_BYTES 2","WEB_CONFIG_PROBE_BODY_BYTES 1")));
        Reject(()=>Inspect(Summary(f,"OBSERVED_MARKER M10_004_LIVE\r\n","")));
        Reject(()=>Inspect(Summary(f,"SERIAL_TAIL_LINE M10_004_LIVE","SERIAL_TAIL_LINE changed")));
        Reject(()=>Inspect(f with{Runner=f.Runner with{StandardOutput=f.Runner.StandardOutput.Replace("lines=4 ","lines=3 ",StringComparison.Ordinal)}}));
        Reject(()=>Inspect(f with{Runner=f.Runner with{StandardOutput=f.Runner.StandardOutput.Replace("timeout_seconds=1\r\n","timeout_seconds=0\r\n",StringComparison.Ordinal)}}));
        Reject(()=>Inspect(f with{Context=f.Context with{ForwardPort=20081}}));
        Reject(()=>Inspect(f with{Context=f.Context with{Qemu=Path.GetFullPath("other.exe")}}));
    }

    [HostTest("web runner requires exact logging repeat and later health in the same live process")]
    private static void LoggingObservations()
    {
        var f=Vector();
        foreach(string prefix in new[]{"WEB_LOG_PROBE","WEB_LOG_REPEAT_PROBE","WEB_VECTOR_PROBE","WEB_HEALTH_AFTER_PROBE"})
        {
            Reject(()=>Inspect(Summary(f,prefix+"_RESULT pass",prefix+"_RESULT not_run")));
            Reject(()=>Inspect(Summary(f,prefix+"_REASON none",prefix+"_REASON failure")));
            string text=Encoding.UTF8.GetString(f.Summary);
            string line=text.Split("\r\n").Single(x=>x.StartsWith(prefix+"_BODY_SHA256 ",StringComparison.Ordinal));
            Reject(()=>Inspect(Summary(f,line,line[..(prefix.Length+13)]+new string('0',64))));
            Reject(()=>Inspect(Summary(f,line+"\r\n",line+"\r\n"+line+"\r\n")));
        }
        Reject(()=>Inspect(Summary(f,"WEB_LOG_PROBE_METHOD POST","WEB_LOG_PROBE_METHOD GET")));
        Reject(()=>Inspect(Summary(f,"WEB_LOG_REPEAT_PROBE_STATUS 409","WEB_LOG_REPEAT_PROBE_STATUS 200")));
        Reject(()=>Inspect(Summary(f,"WEB_VECTOR_PROBE_BODY_BYTES 49152","WEB_VECTOR_PROBE_BODY_BYTES 49151")));
        Reject(()=>Inspect(Summary(f,"WEB_VECTOR_PROBE_METHOD GET","WEB_VECTOR_PROBE_METHOD POST")));
        Reject(()=>Inspect(Summary(f,"WEB_VECTOR_PROBE_URL http://127.0.0.1:20080/__clros/vectors","WEB_VECTOR_PROBE_URL http://127.0.0.1:20080/healthz")));
        Reject(()=>Inspect(Summary(f,"WEB_HEALTH_AFTER_PROBE_STATUS 200","WEB_HEALTH_AFTER_PROBE_STATUS 500")));
        Reject(()=>Inspect(Summary(f,"WEB_LOG_PROBE_ELAPSED_MS 300","WEB_LOG_PROBE_ELAPSED_MS 3000")));
        string[] stdout=f.Runner.StandardOutput.Split("\r\n",StringSplitOptions.None);
        Reject(()=>Inspect(f with{Runner=f.Runner with{StandardOutput=string.Join("\r\n",stdout.Where((_,i)=>i is not (18 or 19)))}}));
    }
}
