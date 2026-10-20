using System.Text;
using System.Text.Json;
using OS.Tooling;

internal static class NativeAotServiceQuotaOverlapTests
{
    private const ulong Identity = 4294967297;
    private const string Gc = "limit=16777216 baselineHeap=30920 baselineCommitted=151552 liveBytes=2097152 liveHeap=2200000 liveCommitted=2300000 pressureBytes=8388608 pressureHeap=8500000 pressureCommitted=9000000 pressureCollections=4 oom=1\n";
    private sealed record Vector(byte[] Raw, QemuTranscripts.ServiceQuotaMemoryInspection Memory,
        QemuTranscripts.ServiceQuotaHostHttpInspection Host, QemuTranscripts.ServiceQuotaTemporalInspection Temporal, byte[] Peer);
    private static string Lifecycle(int active,int read) => $"CLROS_LIFECYCLE_STATUS active={active} waiting={(active==0?0:1)} waits={(active==0?0:1)} canceled=0 echoes=0 streams=0 rejected=0 read={read} written=0";
    private static string Worker(int phase,int calls,int iterations) => JsonSerializer.Serialize(new { contract="clros.service.v1",phase,failure=0,
        managedWorkerId=phase==0?0:7,calls,randomRequests=phase==0?0:33,randomBytes=phase==0?0:4097,iterations,elapsedMilliseconds=iterations*100 });
    private static NativeAotServiceQuotaPeer.Observation Peer()
    {
        string zero="CLROS_QUOTA_STATUS phase=0 attempts=0 retainedBytes=0 gcCollections=0", quota="CLROS_QUOTA_STATUS phase=1 attempts=1 retainedBytes=1048576 gcCollections=1";
        string[] names=["health.startup","lifecycle.initial","quota.initial","worker.initial","cancel.headers","lifecycle.cancel","lifecycle.held",
            "worker.start","worker.running","worker.prepressure","quota.prepressure","quota.retained","worker.after_retained","health.overlap",
            "worker.progress","lifecycle.overlap","quota.overlap","quota.last"];
        string[] bodies=["CLROS_SERVICE_HEALTH_OK",Lifecycle(0,0),zero,Worker(0,0,0),"HTTP/1.1 200 OK\r\nConnection: close\r\nContent-Length: 0\r\nDate: Tue, 20 Oct 2026 00:00:00 GMT\r\n\r\n",
            Lifecycle(1,0),Lifecycle(2,64),"CLROS_SERVICE_STARTED",Worker(1,100,2),Worker(1,101,3),zero,quota,Worker(1,102,4),"CLROS_SERVICE_HEALTH_OK",
            Worker(1,103,5),Lifecycle(2,64),quota,quota];
        return new(8080,4,7,11,12,14,15,17,19,"ConnectionError",1048576,1,102,103,4,5,
            names.Select((name,i)=>new NativeAotServiceQuotaPeer.Sample(name,i+1,bodies[i])).ToArray());
    }

    private static Vector Fixture(bool segmented=false,bool responseBeforeMap=false,bool echoLate=false)
    {
        // Independent borrowed values isolate this new seam. No complete memory,
        // HostHttp, socket/scheduler, packet, owned process or guest is fabricated.
        var peer=Peer();var raw=new StringBuilder();int row=0;ulong ordinal=0;
        List<QemuTranscripts.ServiceQuotaHostHttpMessage> messages=[];
        List<QemuTranscripts.ServiceQuotaHostHttpSample> samples=[];
        List<QemuTranscripts.ServiceQuotaHttpSyscallInterval> intervals=[];
        Dictionary<int,(ulong Sent,ulong Received)> fronts=[];
        void Row(string text){raw.Append(text).Append("\r\n");row++;}
        int Call(int nr,int fd,long value,ulong bytes=0)
        {
            int at=row;ordinal++;ulong bits=unchecked((ulong)value);
            Row($"M16_008_CALL identity={Identity} nr={nr} a0={fd} a1={(nr==9?1048576UL:0x500000)} a2={(nr==9?3:bytes)} a3={(nr==9?0x22:0)} a4={(nr==9?uint.MaxValue:0)} a5=0 rip=4198402 rsp=1879048184 flags=514 fs=0 gs=0 clock={ordinal} valueBits={bits} action=1 status=1 ordinal={ordinal} waitSource=0 waitDeadline=0 waitSequence=0 signalDepth=0 signalFrame=0 blockedSignals=0 returnRip=4198402 returnRsp=1879048184 returnFlags=514 returnFs=0 returnGs=0 returnValueBits={bits}");
            return at;
        }
        void Output(string text)
        {
            foreach(string part in segmented?text.Chunk(7).Select(c=>new string(c)):new[]{text})
            {int bytes=Encoding.ASCII.GetByteCount(part);Row($"M16_005_WRITE fd=2 bytes={bytes} data={part}");Call(1,2,bytes,(ulong)bytes);}
        }
        QemuTranscripts.ServiceQuotaHttpSyscallInterval Io(int exchange,int connection,bool response,string kind,int bytes)
        {
            int fd=connection+2;var prior=fronts.GetValueOrDefault(fd);ulong offset=response?prior.Sent:prior.Received;
            ulong sent=prior.Sent+(response?(ulong)bytes:0),received=prior.Received+(response?0:(ulong)bytes);
            int at=Call(response?44:45,fd,bytes,(ulong)bytes);uint port=43000+(uint)connection;
            var identity=new QemuTranscripts.ServiceQuotaSocketIdentity(4096,1,1,(uint)connection-1,(uint)connection-1,0x0a00020f,8080,0x0a000202,port);
            var io=new QemuTranscripts.ServiceQuotaSocketIo(row,at,Identity,ordinal,(ulong)(response?44:45),fd,1,(uint)fd,100+(ulong)fd,
                identity,offset,(uint)bytes,false,sent,received);
            Row($"M16_016_SOCKET identity={Identity} ordinal={ordinal} nr={io.Number} fd={fd} fdGeneration=1 description={fd} inode={100+fd} slot={connection-1} owner=4096 epoch=1 generation=1 index={connection-1} local=167772687 localPort=8080 remote=167772674 remotePort={port} offset={offset} bytes={bytes} peek=0 sent={sent} received={received}");
            fronts[fd]=(sent,received);
            var result=new QemuTranscripts.ServiceQuotaHttpSyscallInterval(exchange,connection,identity,response,kind,(int)offset,(int)offset+bytes,
                [new(io,offset,(uint)bytes)]);intervals.Add(result);return result;
        }
        QemuTranscripts.ServiceQuotaHttpSyscallInterval Request(int index) => Io(index+1,index==4?2:1,false,"complete-request",1);
        void Response(int index,QemuTranscripts.ServiceQuotaHttpSyscallInterval request)
        {
            string body=peer.Samples[index].Body;bool headers=index==4;int bytes=headers?body.Length:body.Length+1;
            var response=Io(index+1,index==4?2:1,true,headers?"cancel-header":"complete-response",bytes);
            var req=new QemuTranscripts.ServiceQuotaHttpPrefix(request.Start,request.End,request.End,"GET /healthz HTTP/1.1",new Dictionary<string,string>(),"none",0,"complete","");
            var res=new QemuTranscripts.ServiceQuotaHttpPrefix(response.Start,headers?response.End:response.Start+1,response.End,"HTTP/1.1 200 OK",new Dictionary<string,string>(),"length",headers?0:body.Length,"complete",headers?"":Convert.ToBase64String(Encoding.ASCII.GetBytes(body)));
            samples.Add(new(peer.Samples[index].Name,messages.Count,headers));messages.Add(new(request.ConnectionId,index+1,req,res,[],[]));
        }
        Output("CLROS_APP_ENTRY minimal.api.service\nCLROS_APP_CCTOR minimal.api.service\nCLROS_APP_CTOR minimal.api.service\nCLROS_APP_BUILT minimal.api.service\nCLROS_SERVICE_GC_BEGIN "+Gc+"CLROS_SERVICE_LISTEN contract=v3-quota endpoint=0.0.0.0:8080\nCLROS_M10_011_READY\n");
        for(int i=0;i<11;i++)Response(i,Request(i));
        if(!echoLate)Io(100,3,false,"echo-prefix",10);
        Io(101,4,false,"pressure-request",5);
        Output("CLROS_SERVICE_QUOTA_BEGIN chunkBytes=1048576 maximumChunks=64\n");
        if(echoLate)Io(100,3,false,"echo-prefix",10);
        Output("CLROS_SERVICE_QUOTA_ATTEMPT index=0 retainedBytes=0 gcCommitted=9000000\n");
        var pending=Request(11);if(responseBeforeMap)Response(11,pending);
        int mapping=Call(9,0,0x200000000);ulong mapOrdinal=ordinal;
        if(!responseBeforeMap)Response(11,pending);
        // Valid publication race: the poll has already sent its response when
        // the independently segmented RETAINED marker begins publication.
        Output("CLROS_SERVICE_QUOTA_RETAINED index=0 retainedBytes=1048576 collectionsBefore=7 collectionsAfter=8 gcCommitted=9000000\n");
        for(int i=12;i<18;i++)Response(i,Request(i));
        Output("CLROS_SERVICE_QUOTA_ATTEMPT index=1 retainedBytes=1048576 gcCommitted=9000000\n");
        Row("M16_014_MEMORY value=copy");Row("M16_016_TOPOLOGY value=copy");int denial=Call(9,0,-12);
        var snapshot=new QemuTranscripts.ServiceQuotaMemorySnapshot(denial-2,Identity,ordinal,9,0,0,0,0,0,0,0,0,0,0,0);
        var memory=new QemuTranscripts.ServiceQuotaMemoryInspection(0,0,0,0,0,0,default,default,[],0,0,1,1048576,snapshot,0,0,0)
        {RetainedCalls=[new(0,0x200000000,1048576,mapping,Identity,mapOrdinal)]};
        var empty=new QemuTranscripts.ServiceQuotaHostDirection(0,"",[],null);
        var cancel=new QemuTranscripts.ServiceQuotaHostConnection(2,new(peer.Samples[4].Body.Length,Convert.ToBase64String(Encoding.ASCII.GetBytes(peer.Samples[4].Body)),[],null),empty);
        var host=new QemuTranscripts.ServiceQuotaHostHttpInspection(new(0,0,0,[cancel]),messages.ToArray(),samples.ToArray());
        return new(Encoding.UTF8.GetBytes(raw.ToString()),memory,host,new(0,0,0,intervals.ToArray()),Encoding.UTF8.GetBytes(NativeAotServiceQuotaPeer.SerializeReport(peer)));
    }
    private static QemuTranscripts.ServiceQuotaOverlapInspection Read(Vector v) => QemuTranscripts.InspectNativeAotServiceQuotaOverlapForTests(v.Raw,v.Memory,v.Host,v.Temporal,v.Peer);

    [HostTest("quota overlap joins segmented original writes HTTP progress and GC without ordering a retained poll request or response after publication")]
    private static void Values()
    {
        foreach(bool split in new[]{false,true})
        {
            var v=Fixture(split);byte[] before=v.Raw.ToArray();var value=Read(v);
            HostCheck.True(value.Samples.Count==7 && value.ForcedGcMarkers.Count==1 && value.FirstRetained.CallRecordIndex<value.DenialCall);
            HostCheck.True(value.Samples[0].RequestFirstCall<value.FirstRetained.CallRecordIndex && value.Samples[0].ResponseFirstCall>value.FirstRetained.CallRecordIndex);
            HostCheck.True(value.Samples[0].ResponseLastCall<value.ForcedGcMarkers[0].Writes[0].CallRecordIndex);
            HostCheck.True((split?value.ForcedGcMarkers[0].Writes.Count>1:value.ForcedGcMarkers[0].Writes.Count==1) && v.Raw.AsSpan().SequenceEqual(before));
            Array.Clear(v.Raw);Array.Clear(v.Peer);
            HostCheck.True(value.ForcedGcMarkers[0].Writes.Sum(w=>w.ByteCount)==value.ForcedGcMarkers[0].ByteEnd-value.ForcedGcMarkers[0].ByteStart);
            bool immutable=false;try{((IList<QemuTranscripts.ServiceQuotaProgressSlice>)value.Begin.Writes).Clear();}catch(NotSupportedException){immutable=true;}
            HostCheck.True(immutable);
        }
    }
    [HostTest("quota overlap rejects premature retained response and held input consumed after pressure begins")]
    private static void Windows()
    {Reject(Fixture(responseBeforeMap:true));Reject(Fixture(echoLate:true));}
    [HostTest("quota overlap rejects detached write calls altered retained projections and original socket row substitutions")]
    private static void Originals()
    {
        var v=Fixture();string raw=Encoding.UTF8.GetString(v.Raw);
        Reject(v with{Raw=Encoding.UTF8.GetBytes(raw.Replace("nr=1 a0=2 ","nr=1 a0=1 ",StringComparison.Ordinal))});
        var retained=v.Memory.RetainedCalls.ToArray();retained[0]=retained[0] with{Address=0x200100000};
        Reject(v with{Memory=v.Memory with{RetainedCalls=retained}});
        Reject(v with{Memory=v.Memory with{RetainedCalls=[]}});
        Reject(v with{Raw=Encoding.UTF8.GetBytes(raw.Replace("fdGeneration=1","fdGeneration=2",StringComparison.Ordinal))});
        var samples=v.Host.Samples.ToArray();samples[11]=samples[11] with{MessageIndex=samples[10].MessageIndex};
        Reject(v with{Host=v.Host with{Samples=samples}});
    }
    private static void Reject(Vector v)
    {try{_ = Read(v);}catch(Exception error)when(error is ToolFailure or InvalidDataException){return;}throw new InvalidOperationException("expected quota overlap rejection");}
}
