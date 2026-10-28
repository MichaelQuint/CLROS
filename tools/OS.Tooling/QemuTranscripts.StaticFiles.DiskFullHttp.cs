using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    public sealed record DiskStaticFullHttpInspection(DiskStaticHttpInspection Http,
        IReadOnlyList<StaticFileHttpWireMessage> BrowserMessages,bool ConcurrentWireOverlap);
    private sealed record DiskFullMessage(StaticHttpMessage Message,int ResponseOffset);

    /// <summary>Inspect the explicitly selected full finite peer, every TCP
    /// exchange and all positive disk reads. Remaining byte-exact exchanges are
    /// returned for the separately owned original browser consumer; this value
    /// inspection cannot authenticate a browser, process or native publication.</summary>
    public static DiskStaticFullHttpInspection InspectNativeAotDiskStaticFullHttpValues(byte[] packets,uint anchor,
        QemuArpReceiveReceipt arp,IReadOnlyList<uint> ports,ReadOnlySpan<byte> receiptJson,
        IReadOnlyDictionary<string,byte[]> bodies,WebRootDiskInventory.Inspection inventory,DiskStaticReplayReport replay,
        WebRootDiskInventory.VerifiedTruncated? truncated=null)
    {
        Need(replay.FullProfile && packets is {Length:>24 and <=DiskStaticCapturePolicy.PacketBytes} &&
            ports is {Count:>0 and <=32} && receiptJson.Length is >0 and <=524288,"disk_full_http_input");
        Need(replay.Truncated==(truncated is not null) && (truncated is null || ReferenceEquals(inventory,truncated.Baseline)),
            "disk_full_http_fault_scope");
        var plan=truncated is null?DiskStaticHttpContract.CreateAssetPlan(inventory):DiskStaticHttpContract.CreateTruncatedAssetPlan(truncated);
        string scope=truncated is null?"disk-static-full-v1":"disk-static-truncated-v1";
        int count=4+plan.Requests.Count+2;
        Need(bodies.Count==count && bodies.Sum(b=>(long)b.Value.Length)<=DiskStaticHttpContract.MaximumExpectedBodyBytes,"disk_full_http_bodies");
        using var document=JsonDocument.Parse(receiptJson.ToArray(),new(){MaxDepth=8});var root=document.RootElement;
        StaticObject(root,"Schema","Scope","Port","Complete","ClockFrequency","ImageSha256","PublishedInventorySha256","Responses");
        int port=root.GetProperty("Port").GetInt32();long frequency=root.GetProperty("ClockFrequency").GetInt64();var rows=root.GetProperty("Responses");
        Need(root.GetProperty("Schema").GetString()==(truncated is null?"clros-disk-http-full-v1":"clros-disk-http-truncated-v1") && root.GetProperty("Scope").GetString()==scope &&
            root.GetProperty("Complete").GetBoolean() && root.GetProperty("ImageSha256").GetString()==plan.ImageSha256 &&
            root.GetProperty("PublishedInventorySha256").GetString()==plan.PublishedInventorySha256 && port is >0 and <=65535 &&
            frequency is >0 and <=1_000_000_000 && rows.ValueKind==JsonValueKind.Array && rows.GetArrayLength()==count,"disk_full_http_receipt");
        List<ServiceWireByteRange> ranges=[];
        var capture=ServiceWirePackets(packets,anchor,arp,ports,true,true,
            r=>{Need(ranges.Count<262144,"disk_full_http_ranges");ranges.Add(r);},quotaStreams:true,diskStaticFull:true);
        Need(capture.QuietSyns==0 && capture.Flows.Count==replay.Static.Connections.Count,"disk_full_http_flow_inventory");
        List<DiskFullMessage> all=[];Dictionary<uint,int> connections=[];
        for(int c=0;c<capture.Flows.Count;c++)
        {
            var flow=capture.Flows[c];var socket=replay.Static.Connections[c];connections.Add(flow.Port,c);
            Need(socket.Connection==c && socket.Sent==(ulong)flow.ResponseExtent && socket.Received==(ulong)flow.RequestExtent &&
                (socket.Reset>0)==(flow.ResetRecord!=0),"disk_full_http_socket_totals");
            if(flow.ResetRecord==0)ServiceWireClose(flow,flow.ServerFinFirst);
            else Need(flow.ResetRecord>flow.ResponseLastRecord,"disk_full_http_reset_after_entities");
            var writes=replay.SocketWrites.Where(w=>w.Connection==c).ToArray();ulong queued=0;int previous=-1;
            foreach(var write in writes)
            {Need(write.Before==queued && write.After>write.Before && write.CallRecordIndex>previous,"disk_full_http_queue");queued=write.After;previous=write.CallRecordIndex;}
            Need(queued==(ulong)flow.ResponseExtent,"disk_full_http_queue_extent");
            int requestAt=0,responseAt=0,index=0;
            while(requestAt<flow.RequestExtent)
            {
                Need(all.Count<512,"disk_full_http_message_bound");var request=ServiceHttpHead(flow.Request.AsSpan(requestAt,flow.RequestExtent-requestAt));
                string[] first=request.First.Split(' ');Need(first.Length==3 && first[0] is "GET" or "HEAD" or "POST" && first[2]=="HTTP/1.1" &&
                    request.Headers.GetValueOrDefault("Host")=="127.0.0.1:"+port && !request.Headers.ContainsKey("Transfer-Encoding") &&
                    (!request.Headers.TryGetValue("Content-Length",out var length) || length=="0"),"disk_full_http_request");
                var response=StaticResponse(flow.Response.AsSpan(responseAt,flow.ResponseExtent-responseAt),first[0],diskStaticFull:true);
                var message=new StaticHttpMessage(flow,index++,first[0],first[1],request.Headers,response.Headers,response.Status,
                    flow.Request.AsSpan(requestAt,request.End).ToArray(),flow.Response.AsSpan(responseAt,response.HeaderEnd).ToArray(),response.Body,
                    flow.Response.AsSpan(responseAt,response.End).ToArray(),
                    StaticRecords(ranges,flow.Port,false,requestAt,requestAt+request.End,false),StaticRecords(ranges,flow.Port,false,requestAt,requestAt+request.End,true),
                    StaticRecords(ranges,flow.Port,true,responseAt,responseAt+response.End,false),StaticRecords(ranges,flow.Port,true,responseAt,responseAt+response.End,true));
                Need(message.ResponseFirst>=message.RequestLast && response.Headers.GetValueOrDefault("Server")=="Kestrel" &&
                    StaticDate(response.Headers.GetValueOrDefault("Date","")),"disk_full_http_exchange");
                all.Add(new(message,responseAt));requestAt+=request.End;responseAt+=response.End;
            }
            Need(requestAt==flow.RequestExtent && responseAt==flow.ResponseExtent,"disk_full_http_complete_stream");
        }
        var remaining=all.ToList();List<DiskFullMessage> peer=[];long previousFinished=0,firstStarted=0;
        string[] api=["/healthz","/config","/api/status","/api/items"];
        var largest=DiskStaticHttpContract.ConcurrentAsset(plan);
        for(int i=0;i<count;i++)
        {
            var row=rows[i];StaticObject(row,"Ordinal","Method","Path","Status","RequestHeaders","ResponseHeaders","Bytes","Sha256","BodyFile","Started","Finished");
            string name=i.ToString("D3",CultureInfo.InvariantCulture)+".body";Need(bodies.TryGetValue(name,out var body),"disk_full_http_body_missing");
            ArgumentNullException.ThrowIfNull(body);
            long started=row.GetProperty("Started").GetInt64(),finished=row.GetProperty("Finished").GetInt64();
            var expected=i>=4 && i<4+plan.Requests.Count?plan.Requests[i-4]:null;
            string method=expected?.Method??"GET",path=expected?.Target??(i<4?api[i]:i==count-2?largest.Target+"?clros-concurrent=asset":"/api/status?clros-concurrent=json");
            Need(row.GetProperty("Ordinal").GetInt32()==i && row.GetProperty("Method").GetString()==method && row.GetProperty("Path").GetString()==path &&
                row.GetProperty("Status").GetInt32()==(expected?.Status??200) && row.GetProperty("BodyFile").GetString()==name &&
                body!.Length<=DiskStaticHttpContract.MaximumAssetBytes && row.GetProperty("Bytes").GetInt32()==body.Length &&
                row.GetProperty("Sha256").GetString()==Convert.ToHexString(SHA256.HashData(body)) && started>0 && finished>=started &&
                (Int128)finished-started<=(Int128)240*frequency && (i>=count-2 || started>=previousFinished),"disk_full_http_peer_row");
            if(i==0)firstStarted=started;previousFinished=Math.Max(previousFinished,finished);
            var requestHeaders=StaticHeaderList(row.GetProperty("RequestHeaders"));
            Need(requestHeaders.Count==(expected?.Headers.Count??0) && (expected is null || expected.Headers.All(h=>requestHeaders.GetValueOrDefault(h.Name)==h.Value)) &&
                (expected is null || body.Length==expected.BodyBytes && Convert.ToHexString(SHA256.HashData(body))==expected.BodySha256),"disk_full_http_peer_plan");
            var peerRow=new StaticPeerRow(i,method,path,expected?.Status??200,requestHeaders,StaticHeaderList(row.GetProperty("ResponseHeaders")),body,started,finished);
            var matches=remaining.Where(m=>StaticMatches(m.Message,peerRow,port)).ToArray();
            Need(matches.Length>0,"disk_full_http_peer_wire_match");var selected=matches.MinBy(m=>m.Message.RequestFirst)!;remaining.Remove(selected);peer.Add(selected);
            if(i<count-2)Need(selected.Message.Flow==capture.Flows[0] && selected.Message.Index==i &&
                (i==0 || selected.Message.RequestFirst>peer[i-1].Message.ResponseLast),"disk_full_http_sequential_matrix");
            if(expected?.ContentLength is long expectedLength)Need(selected.Message.ResponseHeaders.GetValueOrDefault("Content-Length")==expectedLength.ToString(CultureInfo.InvariantCulture),"disk_full_http_length");
            if(expected is not null)Need(selected.Message.ResponseHeaders.GetValueOrDefault("Content-Range")==expected.ContentRange,"disk_full_http_range");
        }
        var a=peer[^2].Message;var j=peer[^1].Message;
        bool concurrent=a.Flow!=j.Flow && a.RequestFirst<j.ResponseLast && j.RequestFirst<a.ResponseLast;
        Need(concurrent && a.RequestFirst>peer[^3].Message.ResponseLast && j.RequestFirst>peer[^3].Message.ResponseLast &&
            (Int128)previousFinished-firstStarted<=(Int128)1700*frequency,"disk_full_http_concurrent_progress");
        int lastPeer=Math.Max(a.ResponseLast,j.ResponseLast);
        Need(remaining.Count>0 && remaining.All(m=>m.Message.RequestFirst>lastPeer),"disk_full_http_browser_phase");
        // Packet indices establish the phase order above. Resolve the complete
        // responses through their authenticated queued stream extents to obtain
        // serial-domain boundaries; packet and syscall indices are unrelated.
        int LastSend(DiskFullMessage item)
        {
            ulong start=(ulong)item.ResponseOffset,end=start+(uint)item.Message.RawResponse.Length;
            int connection=connections[item.Message.Flow.Port];
            return replay.SocketWrites.Where(w=>w.Connection==connection && w.Before<end && w.After>start).Max(w=>w.CallRecordIndex);
        }
        int matrixSerialBoundary=LastSend(peer[^3]);
        int browserSerialBoundary=Math.Max(LastSend(peer[^2]),LastSend(peer[^1]));
        var browserMessages=remaining.ToHashSet();
        HashSet<NativeDiskStaticReadWindows.ReadWindow> used=[];List<DiskStaticHttpDataJoin> joins=[];
        HashSet<DiskStaticFault> usedFaults=[];int faultResponses=0;
        foreach(var flow in capture.Flows)
        {
            int previousSend=-1,c=connections[flow.Port];var writes=replay.SocketWrites.Where(w=>w.Connection==c).ToArray();
            foreach(var item in all.Where(m=>m.Message.Flow==flow).OrderBy(m=>m.Message.Index))
            {
                var m=item.Message;string target=m.Path.Split('?')[0];string decoded=Uri.UnescapeDataString(target);
                var file=inventory.Find(WebRootDiskInventory.Root+(decoded=="/"?"/index.html":decoded));
                if(truncated is not null && file?.Path==WebRootDiskInventory.VerifiedTruncated.Target)
                {
                    Need(peer.Contains(item) && m.Method is "GET" or "HEAD" && m.Status==404 && m.Body.Length==0 &&
                        m.ResponseHeaders.GetValueOrDefault("Content-Length")=="0" &&
                        new[]{"Content-Type","ETag","Last-Modified","Content-Range","Accept-Ranges","Cache-Control"}.All(h=>!m.ResponseHeaders.ContainsKey(h)),
                        "disk_full_http_fault_response");
                    ulong start=(ulong)item.ResponseOffset,end=start+(uint)m.RawResponse.Length;
                    int firstSend=writes.First(w=>w.Before<end && w.After>start).CallRecordIndex;
                    var faults=InspectDiskStaticFaultResponseValues(replay.Faults,replay.ReadinessRecordIndex,previousSend,firstSend);
                    foreach(var fault in faults)Need(usedFaults.Add(fault),"disk_full_http_fault_reuse");
                    faultResponses++;
                }
                else if(m.Status is 200 or 206 or 304 && file is {IsDirectory:false})
                {
                    var asset=plan.Assets.Single(f=>f.Path==file.Path);
                    if(m.Status is 200 or 206)Need(m.ResponseHeaders.GetValueOrDefault("Content-Type")==asset.MediaType &&
                        m.ResponseHeaders.GetValueOrDefault("Accept-Ranges")=="bytes","disk_full_http_media");
                    Need(m.ResponseHeaders.GetValueOrDefault("ETag")==asset.ETag && m.ResponseHeaders.GetValueOrDefault("Last-Modified")==asset.LastModified &&
                        m.ResponseHeaders.GetValueOrDefault("Cache-Control")=="no-store","disk_full_http_metadata");
                    if(m.Method=="GET" && m.Body.Length>0)
                    {
                        uint offset=0;
                        if(m.Status==206)
                        {
                            string range=m.ResponseHeaders.GetValueOrDefault("Content-Range","");int dash=range.IndexOf('-');
                            Need(range.StartsWith("bytes ",StringComparison.Ordinal) && dash>6 && uint.TryParse(range.AsSpan(6,dash-6),NumberStyles.None,CultureInfo.InvariantCulture,out offset),"disk_full_http_range_offset");
                        }
                        byte[] physical=file.CopyBytes();Need(offset<physical.Length && m.Body.Length<=physical.Length-offset &&
                            m.Body.AsSpan().SequenceEqual(physical.AsSpan((int)offset,m.Body.Length)),"disk_full_http_physical_bytes");
                        ulong bodyStart=(ulong)(item.ResponseOffset+m.Header.Length),bodyEnd=bodyStart+(uint)m.Body.Length;
                        int lowerBound=browserMessages.Contains(item)?Math.Max(previousSend,browserSerialBoundary):
                            item==peer[^2] || item==peer[^1]?Math.Max(previousSend,matrixSerialBoundary):previousSend;
                        var reads=InspectDiskStaticBodyReadValues(file.Inode,offset,(uint)m.Body.Length,replay.ReadinessRecordIndex,lowerBound,bodyStart,bodyEnd,replay.Reads,writes);
                        int firstSend=writes.First(w=>w.Before<bodyEnd && w.After>bodyStart).CallRecordIndex;
                        foreach(var read in reads)
                        {Need(used.Add(read),"disk_full_http_read_reuse");joins.Add(new(all.IndexOf(item),file.Path,file.Inode,read.DescriptorGeneration,read.RequestGeneration,
                            read.CompleteRecordIndex,firstSend,m.RequestFirst,m.ResponseLast));}
                    }
                }
                else if(m.Status==200)
                {
                    Need(m.Method=="GET" && m.ResponseHeaders.GetValueOrDefault("Content-Type")==(decoded=="/healthz"?"text/plain":"application/json; charset=utf-8"),"disk_full_http_api_media");
                    if(decoded=="/healthz")Need(m.Body.AsSpan().SequenceEqual("OS_MINIMAL_API_OK"u8),"disk_full_http_health");
                    else{Need(decoded is "/config" or "/api/status" or "/api/items","disk_full_http_api_path");StaticJsonBody(m.Body,decoded=="/config"?1:decoded=="/api/status"?2:3);}
                }
                else Need(m.Status is 404 or 416 && m.Body.Length==0,"disk_full_http_missing_or_range");
                int responseEnd=item.ResponseOffset+m.RawResponse.Length;
                previousSend=writes.Where(w=>w.Before<(ulong)responseEnd && w.After>(ulong)item.ResponseOffset).Max(w=>w.CallRecordIndex);
            }
        }
        Need(used.Count==replay.Reads.Count,"disk_full_http_read_inventory");
        Need(truncated is null?replay.Faults.Count==0:faultResponses==2 && usedFaults.Count==replay.Faults.Count,
            "disk_full_http_fault_inventory");
        var report=new DiskStaticHttpInspection(capture.Flows.Count,all.Count,capture.TcpRecords,capture.Flows.Sum(f=>f.RequestExtent),capture.Flows.Sum(f=>f.ResponseExtent),
            Array.AsReadOnly(joins.ToArray()),Array.AsReadOnly(capture.Flows.Select(f=>new StaticFileHttpFlow(f.Port,f.RequestExtent,f.ResponseExtent,f.ResetRecord!=0)).ToArray())){Scope=scope};
        return new(report,Array.AsReadOnly(remaining.Select(m=>m.Message.Copy()).ToArray()),concurrent);
    }

    /// <summary>Join copied failed stat intervals to one response's actual
    /// queued stream interval. Indices are all serial-domain positions; packet
    /// indices and clocks cannot substitute. Every selected failure must be a
    /// complete source4 EIO with an admitted stat output. Multiple lookups per
    /// request are allowed; missing, late, duplicate or wrong-path values throw.
    /// No HTTP, device, source or original execution authority is granted.</summary>
    public static IReadOnlyList<DiskStaticFault> InspectDiskStaticFaultResponseValues(IReadOnlyList<DiskStaticFault> faults,
        int readiness,int previousSend,int firstSend)
    {
        Need(faults is {Count:>0} && faults.Count<=DiskStaticExecutionPolicy.MaximumCalls && readiness>=0 && previousSend>=-1 &&
            firstSend>Math.Max(readiness,previousSend),"disk_full_fault_join_input");
        var selected=faults.Where(f=>f.Operation.BeginRecordIndex>previousSend && f.Operation.BeginRecordIndex<firstSend).ToArray();
        Need(selected.Length>0 && selected.Select(f=>f.Operation.Request).Distinct().Count()==selected.Length,"disk_full_fault_join_missing");
        foreach(var f in selected)
        {
            var op=f.Operation;
            Need(f.Path==WebRootDiskInventory.VerifiedTruncated.Target && op.Identity!=0 && op.Ordinal!=0 && op.Request!=0 &&
                op.Number is 4 or 6 && op.Value==-5 && op.OutputPrefix>=144 && op.BeginRecordIndex>readiness &&
                op.WakeRecordIndex>op.BeginRecordIndex && op.CompleteRecordIndex>op.WakeRecordIndex &&
                op.CompleteRecordIndex<firstSend,"disk_full_fault_join_operation");
        }
        return Array.AsReadOnly(selected);
    }

    /// <summary>Convert already reconstructed bounded browser wire exchanges
    /// into copied browser-matcher values. Exact headers/targets and decoded
    /// entities are preserved; no CDP id to NAT/port mapping is invented. This
    /// supplies no original packet, browser, process or source authority.</summary>
    public static IReadOnlyList<NativeAotDiskStaticBrowser.WireExchange> InspectDiskStaticBrowserWireValues(
        IReadOnlyList<StaticFileHttpWireMessage> messages)
    {
        Need(messages is {Count:>0 and <=NativeAotDiskStaticBrowser.MaximumRequests},"disk_full_browser_wire_input");
        List<NativeAotDiskStaticBrowser.WireExchange> result=[];
        foreach(var message in messages)
        {
            byte[] request=Convert.FromBase64String(message.RequestBase64),header=Convert.FromBase64String(message.ResponseHeaderBase64),body=Convert.FromBase64String(message.BodyBase64);
            Need(request.Length is >0 and <=4096 && header.Length is >0 and <=4096 && body.Length<=DiskStaticHttpContract.MaximumAssetBytes,"disk_full_browser_wire_extents");
            var sent=ServiceHttpHead(request);var response=ServiceHttpHead(header);string[] first=sent.First.Split(' ');
            Need(first.Length==3 && sent.End==request.Length && response.End==header.Length && first[2]=="HTTP/1.1" &&
                response.First.StartsWith("HTTP/1.1 ",StringComparison.Ordinal) && response.First.Length>=13,"disk_full_browser_wire_head");
            int status=int.Parse(response.First.AsSpan(9,3),NumberStyles.None,CultureInfo.InvariantCulture);
            result.Add(new(first[0],first[1],status,sent.Headers.Select(h=>new NativeAotDiskStaticBrowser.Header(h.Key,h.Value)),
                response.Headers.Select(h=>new NativeAotDiskStaticBrowser.Header(h.Key,h.Value)),[],body));
        }
        return Array.AsReadOnly(result.ToArray());
    }

    /// <summary>Join one nonempty uncompressed static response to one uniquely
    /// identified fd generation's consecutive positive reads. Each actual queued
    /// body prefix must follow the reads which supply it. Caller-owned supplied
    /// values are copied synchronously; original syscall/memory/device, content,
    /// connection/HTTP and process authority remain separate obligations. Missing,
    /// late, overlapping, ambiguous or exhausted inputs throw ToolFailure.</summary>
    public static IReadOnlyList<NativeDiskStaticReadWindows.ReadWindow> InspectDiskStaticBodyReadValues(
        ulong inode,ulong fileOffset,uint bytes,int readiness,int previousSend,ulong bodyStart,ulong bodyEnd,
        IReadOnlyList<NativeDiskStaticReadWindows.ReadWindow> reads,IReadOnlyList<DiskStaticSocketWrite> writes)
    {
        Need(inode!=0 && bytes is >0 and <=DiskStaticHttpContract.MaximumAssetBytes && fileOffset<=ulong.MaxValue-bytes &&
            bodyEnd>bodyStart && bodyEnd-bodyStart==bytes && readiness>=0 && previousSend>=-1 &&
            reads is {Count:>0 and <=NativeDiskStaticReadWindows.MaximumWindows} && writes is {Count:>0 and <=DiskStaticCapturePolicy.ProtocolRows},
            "disk_full_body_input");
        var sends=writes.Where(w=>w.Before<bodyEnd && w.After>bodyStart).ToArray();
        Need(sends.Length>0 && sends[0].Before<=bodyStart && sends[^1].After>=bodyEnd && sends.All(w=>w.Before<w.After) &&
            sends.Zip(sends.Skip(1)).All(p=>p.First.After==p.Second.Before && p.First.CallRecordIndex<p.Second.CallRecordIndex),
            "disk_full_body_sends");
        int last=sends[^1].CallRecordIndex;ulong needed=fileOffset+bytes;
        var groups=reads.Where(r=>r.Inode==inode && r.BeginRecordIndex>readiness && r.BeginRecordIndex>previousSend && r.CompleteRecordIndex<last)
            .GroupBy(r=>(r.Descriptor,r.DescriptorGeneration));
        List<NativeDiskStaticReadWindows.ReadWindow[]> fits=[];
        foreach(var group in groups)
        {
            var sequence=group.OrderBy(r=>r.BeginRecordIndex).ToArray();ulong covered=fileOffset;
            bool good=group.Key.DescriptorGeneration!=0 && group.Key.Descriptor is >=3 and <32;
            for(int i=0;i<sequence.Length && good;i++)
            {
                var r=sequence[i];good=r.Identity!=0 && r.Ordinal!=0 && r.RequestGeneration!=0 && r.Number is 0 or 17 &&
                    r.ReturnedBytes is >0 and <=8192 && r.ReturnedBytes<=r.Requested && r.ReturnedBytes<=r.OutputPrefix && r.OutputPrefix<=8192 &&
                    r.Offset<=ulong.MaxValue-r.ReturnedBytes && r.BeginRecordIndex<r.CompleteRecordIndex &&
                    (i==0?r.Offset<=fileOffset && r.Offset+r.ReturnedBytes>fileOffset:
                        r.Offset==sequence[i-1].Offset+sequence[i-1].ReturnedBytes && r.BeginRecordIndex>sequence[i-1].CompleteRecordIndex);
                if(good)covered=r.Offset+r.ReturnedBytes;
            }
            if(!good || covered<needed)continue;
            foreach(var send in sends)
            {
                ulong queuedEnd=fileOffset+Math.Min(send.After,bodyEnd)-bodyStart,available=fileOffset;
                foreach(var read in sequence)
                {
                    if(read.CompleteRecordIndex>=send.CallRecordIndex)break;
                    available=read.Offset+read.ReturnedBytes;
                }
                if(available<queuedEnd){good=false;break;}
            }
            if(good)fits.Add(sequence);
        }
        Need(fits.Count==1,"disk_full_body_unique_read_sequence");
        return Array.AsReadOnly(fits[0]);
    }
}
