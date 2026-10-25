using System.Net;
using System.Text;
using System.Text.Json;
using OS.Tooling;

internal static class NativeAotDiskTimingPeerTests
{
    private const string Metrics="M10_018_TIMING bytes=65536 calls=8 steps=32 pumpNs=3200 scanNs=3200 flushNs=3200 preNetworkNs=800 postNetworkNs=800 postReadinessNs=300 postWaitRecordNs=500 networkCalls=8 diskReady=1 otherReady=0 noAdvance=0 elapsed=0 stepLimit=7 failures=0 flushedPastBudget=0\r\n";

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
                .Replace("postReadinessNs=300","postReadinessNs="+parts.Ready).Replace("postWaitRecordNs=500","postWaitRecordNs="+parts.Wait);
            var values=NativeAotDiskTimingPeer.InspectMetrics(row);
            HostCheck.Equal(values["postNetworkNs"],values["postReadinessNs"]+values["postWaitRecordNs"]);
        }
        string oldV2=Metrics.Replace("postReadinessNs=300 postWaitRecordNs=500 ","").Replace("flushedPastBudget=0","flushedPastBudget=0 replenished=0 replenishBudgetSkips=0");
        bool rejected=false;try{_=NativeAotDiskTimingPeer.InspectMetrics(oldV2);}catch(ToolFailure){rejected=true;}
        HostCheck.True(rejected,"original v2 is a distinct retained producer, not a v3 partition");
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
