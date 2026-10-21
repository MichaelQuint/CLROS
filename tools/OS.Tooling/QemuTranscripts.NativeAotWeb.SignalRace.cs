namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied HTTP selectors for one literal Smoke race. Tids only
    /// select identities from actual replay; they authenticate no callback code.</summary>
    public sealed record WebSignalRaceSelection(int Pid,int WaiterTid,int ActorTid,int CallbackTid,int RecoveryWriterTid);

    /// <summary>One validated native accept or positive socket CALL in an added
    /// lifetime after the ordinary eight controls. CallRecordIndex is the actual
    /// original normalized CALL row, not an invented post-CALL diagnostic row.
    /// DescriptorGeneration/OpenDescription are replay IDs; tuple/generation
    /// comes from the checked native transport lifetime. Counters are replayed
    /// accepted/consumed bytes, not transmission, pointed bytes or allocation.</summary>
    public readonly record struct WebSignalRaceSocketCall(int CallRecordIndex,ulong Identity,ulong Ordinal,ulong Number,
        int Descriptor,uint DescriptorGeneration,int OpenDescription,ServiceQuotaSocketIdentity Connection,
        ulong Offset,uint Bytes,bool Peek,ulong Sent,ulong Received)
    {
        public bool Accept=>Number is 43 or 288;
        public bool Response=>Number is 44 or 46;
    }

    /// <summary>Immutable original-order projection after complete web replay.
    /// Indices address complete normalized original rows before metadata removal.
    /// Pointer contents, callback code roles, native joins, packets and owned
    /// process/guest acceptance remain separate. Legacy profiles permit missing
    /// native exits; the event-owned profile requires both dedicated probe
    /// threads' generation-bound native exits and reaps after their last CALL.</summary>
    public sealed class WebSignalRaceReport
    {
        /// <summary>Actual replay identity and FS at the selected gettid CALL;
        /// native lifecycle rows are optional only for legacy profiles and
        /// framework-owned callback/recovery-writer threads.</summary>
        public readonly record struct Thread(ulong Identity,int Tid,ulong Fs,int GetTidRecordIndex,
            int? CloneRecordIndex,int? ExitRecordIndex,int? ReapRecordIndex);
        /// <summary>Generation-bound original creation and explicit close rows.</summary>
        public readonly record struct DescriptorObservation(int Descriptor,uint Generation,int CreatedRecordIndex,int ClosedRecordIndex);
        /// <summary>Original row order; zero ordinal/sequence means inapplicable.</summary>
        public readonly record struct Step(string Kind,int RecordIndex,ulong Identity,ulong Ordinal,ulong Sequence);
        public WebSignalRaceSelection Selection { get; }
        /// <summary>True only when complete replay checked the explicit
        /// event-owned named waits and repark. This is copied profile identity,
        /// not original native, source, process or guest authority.</summary>
        public bool EventOwned { get; }
        public Thread Waiter { get; }
        public Thread Actor { get; }
        public Thread Callback { get; }
        public Thread RecoveryWriter { get; }
        public DescriptorObservation ReadDescriptor { get; }
        public DescriptorObservation WriteDescriptor { get; }
        public DescriptorObservation EpollDescriptor { get; }
        public IReadOnlyList<Step> Steps { get; }
        /// <summary>Copied added-lifetime socket projections, emitted only after
        /// the entire existing replay succeeds. Ordinary sockets and probe-pipe
        /// calls are excluded; no quota-only M16_016_SOCKET row is fabricated.</summary>
        public IReadOnlyList<WebSignalRaceSocketCall> SocketCalls { get; }
        internal WebSignalRaceReport(WebSignalRaceSelection selection,Thread waiter,Thread actor,Thread callback,Thread writer,
            DescriptorObservation read,DescriptorObservation write,DescriptorObservation epoll,IEnumerable<Step> steps,
            IEnumerable<WebSignalRaceSocketCall> socketCalls,bool eventOwned=false)
        {
            Selection=selection with{};EventOwned=eventOwned;Waiter=waiter;Actor=actor;Callback=callback;RecoveryWriter=writer;
            ReadDescriptor=read;WriteDescriptor=write;EpollDescriptor=epoll;Steps=Array.AsReadOnly(steps.ToArray());
            SocketCalls=Array.AsReadOnly(socketCalls.ToArray());
        }
    }

    private sealed record WebRacePipe(WebProbePipe Pipe,int Read,int Write,uint ReadGeneration,uint WriteGeneration,int Index);
    private sealed record WebRacePort(SocketDescriptor Port,int Descriptor,uint Generation,int Index);
    private sealed record WebRaceWatch(WebProbePipe Pipe,SocketDescriptor Port,int Index);
    private readonly record struct WebRaceFrame(ulong Identity,uint Signal,ulong Frame,int Index,bool Returned);
    private sealed class WebRaceTrace(WebSignalRaceSelection selection,int[] originalRows,bool eventOwned)
    {
        internal readonly WebSignalRaceSelection Selection=selection;
        internal readonly int[] OriginalRows=originalRows;
        internal readonly bool EventOwned=eventOwned;
        internal readonly List<WebRacePipe> Pipes=[];
        internal readonly List<WebRacePort> Ports=[];
        internal readonly List<WebRaceWatch> Watches=[];
        internal readonly List<WebRaceFrame> Frames=[];
        internal readonly List<WebSignalRaceSocketCall> SocketCalls=[];
    }

    private sealed partial class ThreadingProtocol
    {
        private WebRaceTrace? webRaceTrace;

        // Called only by the explicit race mode; ordinary replay collects nothing.
        internal void EnableWebSignalRace(WebSignalRaceSelection selection,IReadOnlyList<int> originalRows,int originalCount,bool eventOwned=false)
        {
            Need(web && !service && webRaceTrace is null && selection is not null && selection.Pid==1 &&
                selection.WaiterTid>0 && selection.ActorTid>0 && selection.CallbackTid>0 && selection.RecoveryWriterTid>0 &&
                selection.WaiterTid!=selection.ActorTid && selection.WaiterTid!=selection.CallbackTid,"web_race_selection");
            Need(originalRows is not null && originalRows.Count==rows.Length && originalCount>=rows.Length && originalCount<=100000,
                "web_race_original_rows");
            int[] map=originalRows!.ToArray();
            Need(map.All(i=>i>=0 && i<originalCount) && map.Zip(map.Skip(1)).All(p=>p.First<p.Second),"web_race_original_rows");
            webRaceTrace=new(selection! with{},map,eventOwned);
        }

        private void ObserveWebSignalRaceCall(RuntimeThread t,Dictionary<string,ulong> r,LinuxCallWaitObservation call)
        {
            if(webRaceTrace is not { } trace)return;
            Need(call.Identity==t.Identity && call.RecordIndex>=0 && call.RecordIndex<rows.Length,"web_race_call_owner");
            ulong nr=r["nr"],value=r["valueBits"];
            if(nr==293 && r["a1"]==0x80800 && value==0)
            {
                Need(webProbePipe is not null,"web_race_pipe");var pipe=webProbePipe!;
                trace.Pipes.Add(new(pipe,pipe.Read,pipe.Write,pipe.ReadGeneration,pipe.WriteGeneration,call.RecordIndex));
            }
            else if(nr is 213 or 291 && value is >=3 and <16 && socketDescriptors.TryGetValue((int)value,out var port) && port.Port)
                trace.Ports.Add(new(port,(int)value,descriptorGenerations[(int)value],call.RecordIndex));
            else if(nr==233 && value==0 && r["a1"] is 1 or 3 && r["a0"]<16 && r["a2"]<16 &&
                socketDescriptors.TryGetValue((int)r["a0"],out var owner) &&
                owner.ProbeReadWatches.TryGetValue((int)r["a2"],out var watched))
                trace.Watches.Add(new(watched,owner,call.RecordIndex));
            ObserveWebSignalRaceSocketCall(trace,r,call);
        }

        private void ObserveWebSignalRaceSocketCall(WebRaceTrace trace,Dictionary<string,ulong> r,LinuxCallWaitObservation call)
        {
            ulong nr=r["nr"],value=r["valueBits"];bool accept=nr is 43 or 288;
            if((!accept && nr is not (44 or 45 or 46 or 47)) || r["action"]!=1 || r["status"]!=1 ||
                unchecked((long)value)<0 || (!accept && value==0))return;
            ulong fd=accept?value:r["a0"];
            Need(fd<16 && socketDescriptors.TryGetValue((int)fd,out _),"web_race_socket_descriptor");
            var descriptor=socketDescriptors[(int)fd];
            if(descriptor.Connection<8)return;
            Need(!descriptor.Port && descriptor.Phase==3 && webLifetimes is not null &&
                descriptor.Connection<webLifetimes.Count && trace.SocketCalls.Count<100000,"web_race_socket_lifetime");
            var first=webLifetimes![descriptor.Connection].First;
            int description=socketDescriptions.IndexOf(descriptor);
            Need(description>=0 && descriptorGenerations[(int)fd]>0 && (accept || value<=8192),"web_race_socket_projection");
            bool response=nr is 44 or 46,peek=!accept && !response && ((nr==45?r["a3"]:r["a2"])&2)!=0;
            uint bytes=accept?0:(uint)value;
            ulong front=response?descriptor.Sent:descriptor.Received;
            Need(accept?descriptor.Sent==0 && descriptor.Received==0:peek || front>=bytes,"web_race_socket_projection");
            var connection=new ServiceQuotaSocketIdentity(first["owner"],first["epoch"],first["generation"],
                checked((uint)first["index"]),checked((uint)first["slot"]),first["local"],checked((uint)first["localPort"]),
                first["remote"],checked((uint)first["remotePort"]));
            trace.SocketCalls.Add(new(trace.OriginalRows[call.RecordIndex],call.Identity,call.Ordinal,nr,(int)fd,
                descriptorGenerations[(int)fd],description,connection,accept?0:peek?front:front-bytes,bytes,peek,
                descriptor.Sent,descriptor.Received));
        }

        private void ObserveWebSignalRaceFrame(RuntimeThread t,uint signal,ulong frame,int replayRow,bool returned)
        {
            if(webRaceTrace is not { } trace)return;
            Need(replayRow>=0 && replayRow<rows.Length,"web_race_frame_row");
            trace.Frames.Add(new(t.Identity,signal,frame,replayRow,returned));
        }

        // No scheduler or descriptor semantics are rerun here. Every selected
        // row, lifetime and binding has passed the complete preceding replay.
        internal WebSignalRaceReport ReadWebSignalRace()
        {
            Need(webRaceTrace is not null && position==rows.Length && active>0 && !stopping,"web_race_complete_prefix");
            var trace=webRaceTrace!;var s=trace.Selection;
            T One<T>(IEnumerable<T> values,string reason)
            {T[] found=values.Take(2).ToArray();Need(found.Length==1,reason);return found[0];}
            int Original(int i)=>trace.OriginalRows[i];
            RuntimeThread Role(int tid)=>One(history.Where(t=>t.Tid==tid),"web_race_thread");
            var waiter=Role(s.WaiterTid);var actor=Role(s.ActorTid);var callback=Role(s.CallbackTid);var writer=Role(s.RecoveryWriterTid);
            var pipe=One(trace.Pipes,"web_race_pipe_generation");
            var watch=One(trace.Watches.Where(w=>ReferenceEquals(w.Pipe,pipe.Pipe)),"web_race_epoll_binding");
            var port=One(trace.Ports.Where(p=>ReferenceEquals(p.Port,watch.Port)),"web_race_epoll_generation");
            var calls=waits.Values.OrderBy(c=>c.RecordIndex).Select(c=>(Wait:c,R:ReadRecord(rows[c.RecordIndex],"M16_008_CALL",Fields))).ToArray();
            int Close(int fd,int created)=>One(calls.Where(c=>c.Wait.RecordIndex>created && c.R["nr"]==3 &&
                c.R["a0"]==(ulong)fd && c.R["valueBits"]==0).Select(c=>c.Wait.RecordIndex).Take(1),"web_race_descriptor_close");
            int readClose=Close(pipe.Read,pipe.Index),writeClose=Close(pipe.Write,pipe.Index),portClose=Close(port.Descriptor,port.Index);
            var io=calls.Where(c=>c.Wait.RecordIndex>pipe.Index && c.R["nr"] is 0 or 1 &&
                (c.R["a0"]==(ulong)pipe.Read && c.Wait.RecordIndex<readClose ||
                 c.R["a0"]==(ulong)pipe.Write && c.Wait.RecordIndex<writeClose)).ToArray();
            Need(io.Length==4 && io.All(c=>c.R["a2"]==1 && c.R["valueBits"]==1),"web_race_pipe_io");
            var writes=io.Where(c=>c.R["nr"]==1 && c.R["a0"]==(ulong)pipe.Write).ToArray();
            var reads=io.Where(c=>c.R["nr"]==0 && c.R["a0"]==(ulong)pipe.Read).ToArray();
            Need(writes.Length==2 && reads.Length==2 && writes[0].Wait.Identity==actor.Identity && reads[0].Wait.Identity==actor.Identity &&
                writes[1].Wait.Identity==writer.Identity && reads[1].Wait.Identity==waiter.Identity,"web_race_pipe_roles");
            var epollWaits=calls.Where(c=>c.Wait.Identity==waiter.Identity && c.R["nr"]==232 &&
                c.R["a0"]==(ulong)port.Descriptor && c.Wait.RecordIndex>watch.Index && c.Wait.RecordIndex<portClose).ToArray();
            Need(epollWaits.Length==2 && epollWaits.All(c=>c.Wait.Source==3 && c.R["action"]==3 && c.R["valueBits"]==0),"web_race_epoll_waits");
            var initial=epollWaits[0];var recovery=epollWaits[1];
            if(trace.EventOwned)Need(epollWaits.All(c=>c.R["a2"]==1 && c.R["a3"]==ulong.MaxValue &&
                c.R["status"]==9 && c.Wait.Deadline==0 && c.Wait.Sequence>0),"web_race_event_waits");
            var signal=One(calls.Where(c=>c.Wait.RecordIndex>pipe.Index && c.Wait.RecordIndex<readClose &&
                c.R["nr"] is 200 or 234 && (c.R["nr"]==200?c.R["a1"]:c.R["a2"])==12 &&
                (c.R["nr"]==200?c.R["a0"]:c.R["a1"])==(ulong)waiter.Tid),"web_race_directed_signal");
            Need(signal.R["nr"]==234 && signal.R["a0"]==1 && signal.R["valueBits"]==0 && signal.Wait.Identity==actor.Identity,
                "web_race_directed_signal");
            var observations=socketObservations.Values.Where(o=>o.Identity==waiter.Identity).ToArray();
            NativeAotSocketGuest.Readiness Event(NativeAotSocketGuest.ObservationKind kind,ulong sequence)=>
                One(observations.Where(o=>o.Kind==kind && o.Sequence==sequence),"web_race_readiness");
            var ready=Event(NativeAotSocketGuest.ObservationKind.Wake,initial.Wait.Sequence);
            var repark=One(observations.Where(o=>o.Kind==NativeAotSocketGuest.ObservationKind.Repark &&
                o.PreviousSequence==initial.Wait.Sequence),"web_race_repark");
            if(trace.EventOwned)Need(repark.Deadline==0 && repark.Sequence>initial.Wait.Sequence,"web_race_event_repark");
            var interrupted=Event(NativeAotSocketGuest.ObservationKind.Wake,repark.Sequence);
            var completed=Event(NativeAotSocketGuest.ObservationKind.Complete,repark.Sequence);
            var recovered=Event(NativeAotSocketGuest.ObservationKind.Wake,recovery.Wait.Sequence);
            var recoveredComplete=Event(NativeAotSocketGuest.ObservationKind.Complete,recovery.Wait.Sequence);
            Need(ready.Completion==1 && repark.Deadline==initial.Wait.Deadline && interrupted.Completion==5 && interrupted.State==5 &&
                completed.Completion==5 && completed.ValueBits==Bits(-4) && recovery.Wait.Sequence>repark.Sequence &&
                recovered.Completion==1 && recoveredComplete.Completion==1 && recoveredComplete.ValueBits==1,"web_race_winners");
            var frames=trace.Frames.Where(f=>f.Identity==waiter.Identity && f.Signal==12).ToArray();
            Need(frames.Length==2 && !frames[0].Returned && frames[1].Returned && frames[0].Frame==frames[1].Frame,"web_race_signal_frame");
            int entry=frames[0].Index,returned=frames[1].Index;
            Need(calls.Any(c=>c.Wait.RecordIndex==returned && c.R["nr"]==15 && c.R["valueBits"]==Bits(-4)),"web_race_sigreturn");
            int Resume(int after,long value)=>One(Enumerable.Range(after+1,rows.Length-after-1)
                .Where(i=>rows[i].StartsWith("M16_008_RESUME identity="+waiter.Identity+" ",StringComparison.Ordinal))
                .Take(1).Where(i=>ReadRecord(rows[i],"M16_008_RESUME","identity:dec","valueBits:dec","status:dec")["valueBits"]==Bits(value)),
                "web_race_resume");
            int resumed=Resume(completed.RecordIndex,-4),recoveryResume=Resume(recoveredComplete.RecordIndex,1);
            int[] order=[initial.Wait.RecordIndex,writes[0].Wait.RecordIndex,ready.RecordIndex,signal.Wait.RecordIndex,reads[0].Wait.RecordIndex,
                repark.RecordIndex,interrupted.RecordIndex,completed.RecordIndex,resumed,entry,returned,recovery.Wait.RecordIndex,
                writes[1].Wait.RecordIndex,recovered.RecordIndex,recoveredComplete.RecordIndex,recoveryResume,reads[1].Wait.RecordIndex];
            Need(order.Zip(order.Skip(1)).All(p=>p.First<p.Second) && order[^1]<Math.Min(readClose,Math.Min(writeClose,portClose)),"web_race_order");
            int GetTid(RuntimeThread t,int before,int after,bool last=false)
            {
                var matches=calls.Where(c=>c.Wait.Identity==t.Identity && c.R["nr"]==186 && c.R["valueBits"]==(ulong)t.Tid &&
                    c.Wait.RecordIndex>after && c.Wait.RecordIndex<before).Select(c=>c.Wait.RecordIndex).ToArray();
                Need(matches.Length>0,"web_race_gettid");return last?matches[^1]:matches[0];
            }
            WebSignalRaceReport.Thread Thread(RuntimeThread t,int get)
            {
                int? clone=t.Tid==1?null:One(calls.Where(c=>c.R["nr"]==56 && c.R["valueBits"]==(ulong)t.Tid)
                    .Select(c=>(int?)c.Wait.RecordIndex),"web_race_clone");
                int? Locate(string prefix)=>Enumerable.Range(0,rows.Length).Where(i=>rows[i].StartsWith(prefix,StringComparison.Ordinal))
                    .Select(i=>(int?)Original(i)).FirstOrDefault();
                return new(t.Identity,t.Tid,ReadRecord(rows[get],"M16_008_CALL",Fields)["fs"],Original(get),clone is int cloneIndex?Original(cloneIndex):null,
                    Locate("M16_008_EXIT identity="+t.Identity+" "),Locate("M16_008_REAP old="+t.Identity+" "));
            }
            var waiterValue=Thread(waiter,GetTid(waiter,initial.Wait.RecordIndex,pipe.Index));
            var actorValue=Thread(actor,GetTid(actor,writes[0].Wait.RecordIndex,pipe.Index));
            var callbackValue=Thread(callback,GetTid(callback,writes[1].Wait.RecordIndex,entry));
            var writerValue=Thread(writer,GetTid(writer,writes[1].Wait.RecordIndex,recovery.Wait.RecordIndex,last:true));
            if(trace.EventOwned)
            {
                RequireRetired(waiterValue);RequireRetired(actorValue);
            }
            void RequireRetired(WebSignalRaceReport.Thread t)
            {
                int last=calls.Where(c=>c.Wait.Identity==t.Identity).Max(c=>Original(c.Wait.RecordIndex));
                Need(t.CloneRecordIndex is int clone && clone<t.GetTidRecordIndex && t.GetTidRecordIndex<=last &&
                    t.ExitRecordIndex is int exit && last<exit && t.ReapRecordIndex is int reap && exit<reap &&
                    rows.Count(r=>r.StartsWith("M16_008_EXIT identity="+t.Identity+" ",StringComparison.Ordinal))==1 &&
                    rows.Count(r=>r.StartsWith("M16_008_REAP old="+t.Identity+" ",StringComparison.Ordinal))==1,
                    "web_race_thread_lifetime");
            }
            string[] kinds=["initial-wait","actor-write","ready","signal","actor-read","repark","interrupted","interrupt-complete",
                "interrupt-resume","handler","sigreturn","recovery-wait","recovery-write","recovery-ready","recovery-complete","recovery-resume","recovery-read"];
            var steps=order.Select((i,n)=>
            {
                bool call=waits.TryGetValue(i,out var w);return new WebSignalRaceReport.Step(kinds[n],Original(i),
                    call?w.Identity:waiter.Identity,call?w.Ordinal:0,call?w.Sequence:socketObservations.GetValueOrDefault(i).Sequence);
            }).ToList();
            foreach(var close in new[]{(portClose,"epoll-close"),(writeClose,"write-close"),(readClose,"read-close")}.OrderBy(x=>x.Item1))
            {var c=waits[close.Item1];steps.Add(new(close.Item2,Original(close.Item1),c.Identity,c.Ordinal,0));}
            return new(s,waiterValue,actorValue,callbackValue,writerValue,
                new(pipe.Read,pipe.ReadGeneration,Original(pipe.Index),Original(readClose)),
                new(pipe.Write,pipe.WriteGeneration,Original(pipe.Index),Original(writeClose)),
                new(port.Descriptor,port.Generation,Original(port.Index),Original(portClose)),steps,trace.SocketCalls,trace.EventOwned);
        }
    }
}
