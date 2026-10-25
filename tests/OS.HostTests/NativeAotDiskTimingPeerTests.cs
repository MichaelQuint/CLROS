using System.Net;
using System.Text;
using System.Text.Json;
using OS.Tooling;

internal static class NativeAotDiskTimingPeerTests
{
    private const string MetricsV4="M10_018_TIMING bytes=65536 calls=8 steps=32 pumpNs=3200 scanNs=3200 flushNs=3200 preNetworkNs=800 postNetworkNs=800 postReadinessNs=300 readinessRefreshNs=100 readinessWaitScanNs=200 postWaitRecordNs=500 networkCalls=8 diskReady=1 otherReady=0 noAdvance=0 elapsed=0 stepLimit=7 failures=0 flushedPastBudget=0";
    private const string Metrics=MetricsV4+" pollNs=11200 diskRoundNs=9600 clockCalls=8 clockNs=11200 clockPreNs=800 clockDiskNs=9600 clockPostNs=800"+
        " syscallCalls=0 syscallNs=0 syscallPreNs=0 syscallDiskNs=0 syscallPostNs=0"+
        " supervisorCalls=0 supervisorNs=0 supervisorPreNs=0 supervisorDiskNs=0 supervisorPostNs=0"+
        " idleCalls=0 idleNs=0 idlePreNs=0 idleDiskNs=0 idlePostNs=0\r\n";

    [HostTest("timing aggregate rejects duplicates overflow missing and nonexclusive yield reasons")]
    private static void Aggregate()
    {
        HostCheck.Equal(210,NativeAotDiskTimingPeer.PeerTimeoutSeconds(240));
        HostCheck.Equal(450,NativeAotDiskTimingPeer.PeerTimeoutSeconds(480));
        HostCheck.Equal(480,NativeAotDiskTimingPeer.PeerTimeoutSeconds(600));
        foreach(int timeout in new[]{239,601})
        {
            bool denied=false;try{_=NativeAotDiskTimingPeer.PeerTimeoutSeconds(timeout);}catch(ToolFailure){denied=true;}
            HostCheck.True(denied);
        }
        HostCheck.Equal(32UL,NativeAotDiskTimingPeer.InspectMetrics("unrelated\n"+Metrics)["steps"]);
        foreach(string bad in new[]{"",Metrics+Metrics,Metrics.Replace("calls=8","calls=18446744073709551616"),
            Metrics.Replace("calls=8","calls=08"),Metrics.Replace("stepLimit=7","stepLimit=8"),
            Metrics.Replace("failures=0","failures=1"),Metrics.Replace("steps=32","steps=33"),
            Metrics.Replace("networkCalls=8","networkCalls=7"),Metrics.Replace("bytes=65536","bytes=65535"),
            Metrics.Replace(" flushedPastBudget=0",""),Metrics.Replace("postReadinessNs=300","postReadinessNs=299"),
            Metrics.Replace("postWaitRecordNs=500","postWaitRecordNs=501"),Metrics.Replace(" postReadinessNs=300",""),
            Metrics.Replace("postWaitRecordNs=500","postWaitRecordNs=-1"),Metrics.Replace("postReadinessNs=300","postReadinessNs=300 postReadinessNs=300"),
            Metrics.Replace("postReadinessNs=300","postReadinessNs=18446744073709551615").Replace("postWaitRecordNs=500","postWaitRecordNs=1")})
        {
            bool rejected=false;try{_=NativeAotDiskTimingPeer.InspectMetrics(bad);}catch(ToolFailure){rejected=true;}
            HostCheck.True(rejected,bad);
        }
    }

    [HostTest("timing post-network partition conserves parent duration at zero and maximum unsigned boundaries")]
    private static void PostNetworkPartition()
    {
        foreach(var parts in new (string Parent,string Ready,string Wait)[]{("0","0","0"),("800","0","800"),("800","800","0"),
            ("18446744073709551615","18446744073709551614","1"),("18446744073709551615","0","18446744073709551615")})
        {
            string row=Metrics.Replace("postNetworkNs=800","postNetworkNs="+parts.Parent)
                .Replace("postReadinessNs=300","postReadinessNs="+parts.Ready).Replace("postWaitRecordNs=500","postWaitRecordNs="+parts.Wait)
                .Replace("readinessRefreshNs=100","readinessRefreshNs="+parts.Ready).Replace("readinessWaitScanNs=200","readinessWaitScanNs=0")
                .Replace("preNetworkNs=800","preNetworkNs=0").Replace("diskRoundNs=9600","diskRoundNs=0")
                .Replace("pollNs=11200","pollNs="+parts.Parent).Replace("clockNs=11200","clockNs="+parts.Parent)
                .Replace("clockPreNs=800","clockPreNs=0").Replace("clockDiskNs=9600","clockDiskNs=0")
                .Replace("clockPostNs=800","clockPostNs="+parts.Parent);
            var values=NativeAotDiskTimingPeer.InspectMetrics(row);
            HostCheck.Equal(values["postNetworkNs"],values["postReadinessNs"]+values["postWaitRecordNs"]);
        }
        string oldV3=MetricsV4.Replace("readinessRefreshNs=100 readinessWaitScanNs=200 ","");
        string oldV2=oldV3.Replace("postReadinessNs=300 postWaitRecordNs=500 ","")
            .Replace("flushedPastBudget=0","flushedPastBudget=0 replenished=0 replenishBudgetSkips=0");
        foreach(string original in new[]{MetricsV4,oldV3,oldV2})
        {
            bool rejected=false;try{_=NativeAotDiskTimingPeer.InspectMetrics(original);}catch(ToolFailure){rejected=true;}
            HostCheck.True(rejected,"original v2/v3/v4 producers remain distinct from the v5 cause partition");
        }
    }

    [HostTest("timing poll causes conserve counts whole elapsed and three stages without borrowing empty or overflowing buckets")]
    private static void PollCauses()
    {
        // Redistribute the same measured intervals across all four causes.
        // No independent pump/scan/flush sum is required to equal the WHOLE
        // disk round: those are subintervals of an overlapping parent.
        foreach(string cause in new[]{"clock","syscall","supervisor","idle"})
        {
            string row=Metrics;
            if(cause!="clock")
                row=row.Replace("clockCalls=8","clockCalls=0").Replace("clockNs=11200","clockNs=0")
                    .Replace("clockPreNs=800","clockPreNs=0").Replace("clockDiskNs=9600","clockDiskNs=0").Replace("clockPostNs=800","clockPostNs=0")
                    .Replace(cause+"Calls=0",cause+"Calls=8").Replace(cause+"Ns=0",cause+"Ns=11200")
                    .Replace(cause+"PreNs=0",cause+"PreNs=800").Replace(cause+"DiskNs=0",cause+"DiskNs=9600").Replace(cause+"PostNs=0",cause+"PostNs=800");
            var values=NativeAotDiskTimingPeer.InspectMetrics(row);
            HostCheck.Equal(values["networkCalls"],values[cause+"Calls"]);
            HostCheck.Equal(values["pollNs"],values[cause+"Ns"]);
        }
        string mixed=Metrics.Replace("clockCalls=8","clockCalls=2").Replace("clockNs=11200","clockNs=2800")
            .Replace("clockPreNs=800","clockPreNs=200").Replace("clockDiskNs=9600","clockDiskNs=2400").Replace("clockPostNs=800","clockPostNs=200");
        foreach(string cause in new[]{"syscall","supervisor","idle"})
            mixed=mixed.Replace(cause+"Calls=0",cause+"Calls=2").Replace(cause+"Ns=0",cause+"Ns=2800")
                .Replace(cause+"PreNs=0",cause+"PreNs=200").Replace(cause+"DiskNs=0",cause+"DiskNs=2400").Replace(cause+"PostNs=0",cause+"PostNs=200");
        HostCheck.Equal(11200UL,NativeAotDiskTimingPeer.InspectMetrics(mixed)["pollNs"]);
        foreach(string bad in new[]{Metrics.Replace("clockCalls=8","clockCalls=7"),Metrics.Replace("clockCalls=8","clockCalls=0"),
            Metrics.Replace("clockNs=11200","clockNs=11199"),Metrics.Replace("diskRoundNs=9600","diskRoundNs=9599"),
            Metrics.Replace("pollNs=11200","pollNs=11201"),Metrics.Replace(" clockDiskNs=9600",""),
            Metrics.Replace("idleCalls=0","idleCalls=0 idleCalls=0"),Metrics.Replace("idleCalls=0","idleCalls=1"),
            Metrics.Replace("clockDiskNs=9600","clockDiskNs=18446744073709551615"),
            Metrics.Replace("idleNs=0","idleNs=18446744073709551615").Replace("idlePreNs=0","idlePreNs=18446744073709551615")
                .Replace("idleCalls=0","idleCalls=1").Replace("clockCalls=8","clockCalls=7")})
        { bool rejected=false;try{_=NativeAotDiskTimingPeer.InspectMetrics(bad);}catch(ToolFailure){rejected=true;}HostCheck.True(rejected,bad); }
    }

    [HostTest("timing readiness children conserve parent and reject missing duplicated regressed and overflowing durations")]
    private static void ReadinessPartition()
    {
        foreach(var parts in new (string Refresh,string Scan)[]{("0","300"),("300","0"),("100","200")})
        {
            string row=Metrics.Replace("readinessRefreshNs=100","readinessRefreshNs="+parts.Refresh)
                .Replace("readinessWaitScanNs=200","readinessWaitScanNs="+parts.Scan);
            var values=NativeAotDiskTimingPeer.InspectMetrics(row);
            HostCheck.Equal(values["postReadinessNs"],values["readinessRefreshNs"]+values["readinessWaitScanNs"]);
        }
        foreach(string bad in new[]{Metrics.Replace("readinessRefreshNs=100","readinessRefreshNs=99"),
            Metrics.Replace("readinessWaitScanNs=200","readinessWaitScanNs=201"),Metrics.Replace(" readinessRefreshNs=100",""),
            Metrics.Replace("readinessWaitScanNs=200","readinessWaitScanNs=-1"),
            Metrics.Replace("readinessRefreshNs=100","readinessRefreshNs=100 readinessRefreshNs=100"),
            Metrics.Replace("readinessRefreshNs=100","readinessRefreshNs=18446744073709551615").Replace("readinessWaitScanNs=200","readinessWaitScanNs=1")})
        {
            bool rejected=false;try{_=NativeAotDiskTimingPeer.InspectMetrics(bad);}catch(ToolFailure){rejected=true;}
            HostCheck.True(rejected);
        }
    }

    // Real loopback HTTP with synthetic runner/serial and independent image
    // extraction tests ownership and byte validation; it is not guest evidence.
    [HostTest("short timing peer checks actual bytes metadata metrics and joins deliberate cancellation")]
    private static void Lifecycle()=>WebRootImageFixtureTests.WithImage((root,dir,image,source)=>
    {
        byte[] core=Enumerable.Range(0,70001).Select(i=>(byte)(i*37^(i>>9))).ToArray();
        File.WriteAllBytes(Path.Combine(source,"_framework","System.Private.CoreLib.wasm"),core);
        string applied=Path.Combine(dir,"timing.raw");
        var receipt=WebRootImageFixture.Apply(root,image,source,WebRootImageFixture.SnapshotPublishedRoot(source),applied);
        var inventory=WebRootDiskInventory.Inspect(applied,File.ReadAllBytes(applied),receipt);
        foreach(string mode in new[]{"good","body","etag","metrics","unconfirmed"})
            Check(mode).GetAwaiter().GetResult();

        async Task Check(string mode)
        {
            string serial=Path.Combine(root,"artifacts","tests","disk-timing-"+Guid.NewGuid().ToString("N")+".serial.log");
            Directory.CreateDirectory(Path.GetDirectoryName(serial)!);bool exited=false;
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(20));ToolFailure? failure=null;
            try
            {
                var result=await NativeAotDiskTimingPeer.RunAsync(root,serial,Stub,inventory,600,deadline.Token);
                HostCheck.True(mode=="good" && result.Runner.Cancelled && result.Runner.TerminationConfirmed && exited);
                int starts=0;bool denied=false;
                try{_=await NativeAotDiskTimingPeer.RunAsync(root,serial,(_,_)=>{starts++;throw new InvalidOperationException();},inventory,600,deadline.Token);}
                catch(ToolFailure error){denied=error.Reason=="disk_timing_fresh_artifacts";}
                HostCheck.True(denied && starts==0);
            }
            catch(ToolFailure error){failure=error;}
            HostCheck.True(exited && (mode=="good"?failure is null:failure is not null),mode+" "+failure?.Message);
            using var actual=JsonDocument.Parse(File.ReadAllBytes(serial+".disk-timing/diagnostic.json"));
            HostCheck.Equal(mode=="good",actual.RootElement.GetProperty("Complete").GetBoolean());
            HostCheck.True(!actual.RootElement.GetProperty("FullAndCleanupQualified").GetBoolean() &&
                actual.RootElement.GetProperty("RunnerJoined").GetBoolean());

            async Task<ProcessResult> Stub(int port,CancellationToken cancellation)
            {
                using HttpListener listener=new();listener.Prefixes.Add("http://127.0.0.1:"+port+"/");listener.Start();
                File.WriteAllText(serial,"M10_018_LISTENING main=1\r\n");
                var entry=inventory.Find(WebRootDiskInventory.Root+NativeAotDiskTimingPeer.Target)!;
                long modified=DateTimeOffset.FromUnixTimeSeconds(entry.ModifiedSeconds).ToFileTime();
                string etag="\""+(modified^entry.Bytes).ToString("x")+"\"";
                try
                {
                    for(int i=0;;i++)
                    {
                        var context=await listener.GetContextAsync().WaitAsync(cancellation);
                        bool head=context.Request.HttpMethod=="HEAD",range=context.Request.Headers["Range"] is not null;
                        bool asset=context.Request.RawUrl==NativeAotDiskTimingPeer.Target;
                        HostCheck.True(i switch{0 or 3=>context.Request.RawUrl=="/healthz",1=>asset&&head,2=>asset&&range,_=>false});
                        byte[] entity=asset?range?core[..65536]:[]:"OS_MINIMAL_API_OK"u8.ToArray();
                        if(mode=="body" && range)entity[1]^=1;
                        context.Response.StatusCode=range?206:200;context.Response.ContentLength64=head?core.Length:entity.Length;
                        context.Response.ContentType=asset?"application/wasm":"text/plain";
                        if(asset)context.Response.Headers["ETag"]=mode=="etag"?"\"wrong\"":etag;
                        if(range)context.Response.Headers["Content-Range"]="bytes 0-65535/"+core.Length;
                        if(!head)await context.Response.OutputStream.WriteAsync(entity,cancellation);
                        context.Response.Close();
                        if(i==3)File.AppendAllText(serial,mode=="metrics"?Metrics+Metrics:Metrics);
                    }
                }
                catch(OperationCanceledException)when(cancellation.IsCancellationRequested)
                {return new(-1,"synthetic-runner-cancelled","",false,false,true,TimeSpan.Zero){TerminationConfirmed=mode!="unconfirmed"};}
                finally{exited=true;listener.Stop();}
            }
        }
    });
}
