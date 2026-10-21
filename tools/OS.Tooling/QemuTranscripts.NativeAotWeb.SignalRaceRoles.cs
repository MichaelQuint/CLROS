using System.Globalization;
using System.Text.Json;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>A selected HTTP role retains original request/response CALL
    /// coverage. HostCancellation has request-only coverage and Response=null;
    /// it is event metadata, never a native cancellation, EOF or HTTP reply.</summary>
    public sealed record WebSignalRaceRoleSample(int Sample,string Kind,int ExchangeId,bool HostCancellation,
        WebSignalRaceHttpSyscallInterval Request,WebSignalRaceHttpSyscallInterval? Response);

    /// <summary>One strict original-row order relationship. Both indices come
    /// from complete replay or its actual HTTP byte-covering socket CALLs.</summary>
    public readonly record struct WebSignalRaceOrderJoin(string Relation,int BeforeRecordIndex,int AfterRecordIndex);

    /// <summary>Copied complete HTTP/actor role consistency, preserving all
    /// strict byte assignments and CALL coverage. Phase/progress bodies still
    /// confer no independent GC, callback-code, process or guest authority.</summary>
    public sealed record WebSignalRaceRoleInspection(WebSignalRaceStreamJoinInspection Streams,
        WebSignalRaceTemporalInspection Temporal,WebSignalRaceSelection Selection,
        IReadOnlyList<WebSignalRaceRoleSample> Samples,IReadOnlyList<WebSignalRaceOrderJoin> Order);

    /// <summary>Synchronously borrow complete raw wire/attempt/selected reports,
    /// guest streams and an actual complete replay report. Recompute strict HTTP,
    /// unique byte assignment and temporal coverage from replay.SocketCalls;
    /// never accept caller-supplied replacement temporal rows or scalar steps.
    /// Bind canonical selected body roles to replay identities. Require the
    /// consumed race request before the first wait, actor/read/EINTR-resume/
    /// callback observations before the armed reply, actual recovery wait before
    /// overlap GET consumption, recovery request consumption before its writer,
    /// and recovery read/three explicit closes before the race reply. Preserve
    /// concurrent recovery replies and long-request completion order. The one
    /// cancelled churn request needs unique actual full guest/native coverage;
    /// bytes may extend a separately unknown write attempt, without inventing a
    /// response or cancellation syscall. Existing bounded inspectors apply;
    /// extra request-only slices are<=2000000. Return immutable copied joins or
    /// throw. Source/native/owned file/process/UTC/PCAP and GC/ThreadPool/cleanup
    /// acceptance remain separate; phase4 alone supplies no blocked-wait proof.
    /// Only selected v1/v2 are supported here; event-owned v3 is refused until
    /// its separate native role contract is integrated.</summary>
    public static WebSignalRaceRoleInspection InspectNativeAotWebSignalRaceRolesForTests(ReadOnlySpan<byte> wireJson,
        string attemptsJson,string selectedJson,string endpoint,string expectedHealthBody,ServiceQuotaPacketStreams guest,
        WebSignalRaceReport replay)
    {
        if(replay is null)throw new ToolFailure("web_race_roles_input");
        var selection=NativeAotWebPayload.ValidateSignalRaceEvidence(selectedJson,endpoint,expectedHealthBody);
        using var document=JsonDocument.Parse(selectedJson,new JsonDocumentOptions{MaxDepth=8});
        string schema=document.RootElement.GetProperty("schema").GetString()!;
        // A v3 copied capture does not select the still-timed native/guest role
        // contract. Explicit event-wait role admission is a separate integration.
        Need(schema is "clros.web.signalrace.v1" or "clros.web.signalrace.v2","web_race_roles_event_contract_pending");
        bool startGate=schema=="clros.web.signalrace.v2";
        var trace=NativeAotWebPayload.InspectSignalRaceTransport(attemptsJson,selectedJson,wireJson,endpoint,expectedHealthBody);
        Need(trace.Purpose=="signalrace-run" && selection==replay.Selection,"web_race_roles_selection");
        var joined=InspectNativeAotWebSignalRaceStreamJoinForTests(wireJson,attemptsJson,selectedJson,endpoint,expectedHealthBody,guest);
        var temporal=InspectNativeAotWebSignalRaceTemporalForTests(joined,replay.SocketCalls);
        var mapped=new Dictionary<(int Exchange,bool Response),WebSignalRaceHttpSyscallInterval>();
        foreach(var interval in temporal.Intervals)
            Need(mapped.TryAdd((interval.Packet.ExchangeId,interval.Packet.Response),interval),"web_race_roles_interval");
        var selected=document.RootElement.GetProperty("samples");
        Need(joined.Host.Samples.Count==selected.GetArrayLength(),"web_race_roles_samples");
        List<WebSignalRaceRoleSample> samples=[];
        foreach(var sample in joined.Host.Samples)
        {
            Need(sample.Sample==samples.Count,"web_race_roles_samples");
            string kind=selected[sample.Sample].GetProperty("kind").GetString()!;
            if(sample.HostCancellation)
            {
                Need(kind=="churn-cancel" && sample.MessageIndex==-1,"web_race_roles_cancellation");
                samples.Add(new(sample.Sample,kind,sample.ExchangeId,true,
                    WebRaceCancelledRequest(joined,trace,replay.SocketCalls,sample.ExchangeId),null));
                continue;
            }
            Need(sample.MessageIndex>=0 && sample.MessageIndex<joined.Host.Messages.Count,"web_race_roles_samples");
            var message=joined.Host.Messages[sample.MessageIndex];
            Need(message.ExchangeId==sample.ExchangeId && mapped.ContainsKey((sample.ExchangeId,false)) &&
                mapped.ContainsKey((sample.ExchangeId,true)),"web_race_roles_interval");
            var request=mapped[(sample.ExchangeId,false)];var response=mapped[(sample.ExchangeId,true)];
            Need(request.Packet.ConnectionId==message.ConnectionId && response.Packet.ConnectionId==message.ConnectionId &&
                request.Packet.Start==message.Request.Start && request.Packet.End==message.Request.End &&
                message.Response is not null && response.Packet.Start==message.Response.Start && response.Packet.End==message.Response.End,
                "web_race_roles_interval");
            samples.Add(new(sample.Sample,kind,sample.ExchangeId,false,request,response));
        }
        var byKind=samples.ToDictionary(s=>s.Kind,StringComparer.Ordinal);
        var steps=replay.Steps.ToDictionary(s=>s.Kind,StringComparer.Ordinal);List<WebSignalRaceOrderJoin> order=[];
        void Before(string relation,int first,int last)
        {Need(first>=0 && first<last && last<100000,"web_race_roles_order_"+relation);order.Add(new(relation,first,last));}
        int Step(string kind)
        {Need(steps.TryGetValue(kind,out _),"web_race_roles_step");return steps[kind].RecordIndex;}
        WebSignalRaceRoleSample Role(string kind)
        {Need(byKind.TryGetValue(kind,out _),"web_race_roles_samples");return byKind[kind];}
        int RequestFirst(string kind)=>Role(kind).Request.Slices[0].Original.CallRecordIndex;
        int RequestLast(string kind)=>Role(kind).Request.Slices[^1].Original.CallRecordIndex;
        int ResponseFirst(string kind)=>Role(kind).Response!.Slices[0].Original.CallRecordIndex;
        int ResponseLast(string kind)=>Role(kind).Response!.Slices[^1].Original.CallRecordIndex;
        void Serial(string first,string last)=>Before(first+"_before_"+last,ResponseLast(first),RequestFirst(last));
        Serial("baseline-churn","baseline-race");Serial("baseline-race","health-before");
        if(startGate)
        {
            Before("health_before_race_request",ResponseLast("health-before"),RequestFirst("race-complete"));
            Before("race_request_before_initial_reply",RequestLast("race-complete"),ResponseFirst("initial-armed"));
            Before("initial_reply_before_churn_request",ResponseLast("initial-armed"),RequestFirst("churn-cancel"));
            Serial("churn-pending","start-request");
            Before("initial_wait_before_start_request",Step("initial-wait"),RequestFirst("start-request"));
            Before("start_request_before_actor_write",RequestLast("start-request"),Step("actor-write"));
            Serial("start-request","recovery-armed");
        }
        else
        {
            Before("health_before_churn_request",ResponseLast("health-before"),RequestFirst("churn-cancel"));
            Before("pending_before_race_request",ResponseLast("churn-pending"),RequestFirst("race-complete"));
        }
        Before("churn_request_before_pending",RequestLast("churn-cancel"),ResponseFirst("churn-pending"));
        Before("race_request_before_initial_wait",RequestLast("race-complete"),Step("initial-wait"));
        Before("actor_read_before_armed",Step("actor-read"),ResponseFirst("recovery-armed"));
        Before("eintr_resume_before_armed",Step("interrupt-resume"),ResponseFirst("recovery-armed"));
        Before("callback_before_armed",replay.Callback.GetTidRecordIndex,ResponseFirst("recovery-armed"));
        Serial("recovery-armed","churn-overlap");
        Before("recovery_wait_before_overlap",Step("recovery-wait"),RequestFirst("churn-overlap"));
        Serial("churn-overlap","overlap-health");
        Before("recovery_wait_before_health",Step("recovery-wait"),RequestFirst("overlap-health"));
        Serial("overlap-health","churn-drained");Serial("churn-drained","recovery-request");
        Before("recovery_wait_before_request",Step("recovery-wait"),RequestFirst("recovery-request"));
        Before("recovery_request_before_write",RequestLast("recovery-request"),Step("recovery-write"));
        Serial("recovery-request","recovery-repeat");
        foreach(string kind in new[]{"recovery-read","epoll-close","write-close","read-close"})
            Before(kind+"_before_race_reply",Step(kind),ResponseFirst("race-complete"));
        // Either recovery reply may be sent before or after the native writer;
        // the pending race response may complete before recovery-repeat is read.
        Serial("recovery-repeat","race-final");Serial("race-complete","race-final");
        Serial("race-final","health-after");Serial("health-after","churn-complete");
        Serial("churn-complete","final-churn");Serial("final-churn","health-final");
        Serial("health-final","repeat-race");Serial("repeat-race","repeat-probe");
        return new(joined,temporal,selection with{},Array.AsReadOnly(samples.ToArray()),Array.AsReadOnly(order.ToArray()));
    }

    private static WebSignalRaceHttpSyscallInterval WebRaceCancelledRequest(WebSignalRaceStreamJoinInspection joined,
        NativeAotServiceQuotaPeer.WireTrace trace,IReadOnlyList<WebSignalRaceSocketCall> calls,int exchangeId)
    {
        Need(exchangeId>0 && exchangeId<=trace.Exchanges.Count,"web_race_roles_cancellation");
        var exchange=trace.Exchanges[exchangeId-1];
        Need(exchange.Role=="churn" && exchange.Method=="POST" && exchange.Path=="/__clros/churn" &&
            exchange.Outcome=="cancelled","web_race_roles_cancellation");
        var (guest,_,indexes)=CopyWebRaceBoundPackets(joined);
        var streams=guest.Streams.ToDictionary(s=>s.RemotePort);
        var ports=joined.Bindings.ToDictionary(b=>b.ConnectionId,b=>b.RemotePort);
        var reads=calls.Where(c=>!c.Accept && !c.Response && !c.Peek).GroupBy(c=>c.Connection.RemotePort)
            .ToDictionary(g=>g.Key,g=>g.ToArray());
        WebSignalRaceHttpSyscallInterval? found=null;int slices=0;
        foreach(int connection in exchange.ConnectionIds)
        {
            if(!ports.TryGetValue(connection,out uint port) || !reads.TryGetValue(port,out var consuming))continue;
            var stream=streams[port];var host=joined.Host.Prefixes.Connections[connection-1];
            var messages=ReadServiceQuotaHttpPrefixes(Convert.FromBase64String(stream.Requests.KnownPrefixBase64),false,webDiagnostics:true).Messages;
            var known=joined.Host.Messages.Where(m=>m.ConnectionId==connection && m.ExchangeId==exchangeId)
                .ToDictionary(m=>m.Request.Start);
            var unknown=host.Writes.UnknownWrite;
            bool ownUnknown=unknown is not null && trace.Operations[unknown.OperationId-1].ExchangeId==exchangeId;
            foreach(var message in messages)
            {
                if(message.State!="complete" || message.First!="POST /__clros/churn HTTP/1.1" || message.Framing!="length" ||
                    message.DeclaredLength!=0 || message.Headers.GetValueOrDefault("Host")!="127.0.0.1:"+trace.Port.ToString(CultureInfo.InvariantCulture) ||
                    message.Headers.GetValueOrDefault("X-CLROS-Churn")!="v1" || message.Headers.ContainsKey("Connection"))continue;
                bool owns=known.TryGetValue(message.Start,out var prefix)?
                    message.End<=host.Writes.KnownBytes?prefix.Request.End==message.End:ownUnknown:
                    ownUnknown && message.Start==unknown!.Offset;
                if(!owns || consuming.Length==0 || consuming[^1].Offset+consuming[^1].Bytes<(ulong)message.End)continue;
                int first=0,last=consuming.Length;
                while(first<last)
                {int mid=first+(last-first)/2;if(consuming[mid].Offset+consuming[mid].Bytes<=(ulong)message.Start)first=mid+1;else last=mid;}
                ulong cursor=(ulong)message.Start;List<WebSignalRaceHttpSyscallSlice> clipped=[];
                for(int i=first;i<consuming.Length && consuming[i].Offset<(ulong)message.End;i++)
                {
                    var call=consuming[i];ulong start=Math.Max((ulong)message.Start,call.Offset),end=Math.Min((ulong)message.End,call.Offset+call.Bytes);
                    Need(start==cursor && end>start && ++slices<=2000000,"web_race_roles_cancel_coverage");
                    clipped.Add(new(call,start,(uint)(end-start)));cursor=end;
                }
                Need(cursor==(ulong)message.End,"web_race_roles_cancel_coverage");
                Need(found is null,"web_race_roles_cancel_ambiguous");
                var packet=new ServiceQuotaHttpPacketInterval(exchangeId,connection,port,false,"cancel-request",message.Start,message.End,
                    WebRaceHttpRanges(stream,false,message.Start,message.End,indexes[(port,false)]));
                found=new(packet,consuming[first].Connection,Array.AsReadOnly(clipped.ToArray()));
            }
        }
        return found??throw new ToolFailure("web_race_roles_cancel_request");
    }
}
