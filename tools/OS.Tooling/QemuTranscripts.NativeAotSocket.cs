using System.Text;
using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Validate the original socket leaf against its canonical package,
    /// sealed instruction index and raw stdout. Scheduler, waits, signals and
    /// descriptor cleanup must agree. Inputs remain caller owned; unsupported or
    /// inconsistent observations throw. Parent firmware/NIC/packet/peer and runner
    /// provenance remain mandatory; this leaf grants no QEMU acceptance alone.</summary>
    public static void ValidateNativeAotSocketTail(string root, byte[] package, NativeAotSocketProgram program,
        ReadOnlySpan<byte> rawTail, ulong userRoot, ulong kernelRoot)
    {
        Need(program is not null && string.Equals(Path.GetFullPath(root),program.SourceRoot,
            OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal),"socket_program_owner");
        NativeAotSocketPackage.RequireCurrentSources(root);
        NativeAotSocketPackage.RequirePackage(package,program!.Asynchronous);
        var capture=NativeAotSocketGuest.Read(program.Asynchronous,rawTail);
        int offset=64+Encoding.ASCII.GetByteCount(NativeAotSocketPackage.Manifest(program.Asynchronous));
        _=new ThreadingProtocol(program.Asynchronous?"NativeAotSocketSmokeAsync":"NativeAotSocketSmoke",Rows(capture.Output),
            package[offset..],program.IsInstruction,program.IsSyscallReturn,capture.Output.WriteLengths,userRoot,kernelRoot,
            socketCapture:capture).Validate();
        NativeAotSocketPackage.RequireCurrentSources(root);
    }

    /// <summary>Value-only synthetic control seam. Raw stdout and all lifecycle
    /// rows are checked, but caller ELF/predicates grant no sealed program,
    /// package, firmware, packet or guest execution authority.</summary>
    public static ThreadingProtocolReport InspectNativeAotSocketProtocolForTests(bool asynchronous,byte[] elf,
        ReadOnlySpan<byte> rawTail,Func<ulong,bool> instruction,Func<ulong,bool> syscallReturn,ulong userRoot,ulong kernelRoot)
    {
        var capture=NativeAotSocketGuest.Read(asynchronous,rawTail);
        return new ThreadingProtocol(asynchronous?"NativeAotSocketSmokeAsync":"NativeAotSocketSmoke",Rows(capture.Output),elf,
            instruction,syscallReturn,capture.Output.WriteLengths,userRoot,kernelRoot,socketCapture:capture).Validate();
    }

    // Object identity follows the original open description, never a reused fd.
    // This bounded shadow validates observed calls; it does not invent packet
    // readiness or inspect opaque copied user event data.
    private sealed class SocketDescriptor(bool port,uint flags)
    {
        internal readonly bool Port=port;
        internal uint Flags=flags;
        internal int Phase; // 0 created, 1 bound, 2 listening, 3 accepted.
        internal int Connection=-1,Eof,Reset,TimedOut,ShutdownErrors;
        internal ulong Sent,Received,PeekBytes;
        internal bool WriteClosed,ReadClosed,StatusUnknown,SendBlocked;
        internal readonly Dictionary<int,SocketDescriptor> Watches=[];
        internal readonly Dictionary<int,WebProbePipe> ProbeReadWatches=[];
    }

    private sealed partial class ThreadingProtocol
    {
        private readonly bool socketPolicy;
        private readonly Dictionary<int,NativeAotSocketGuest.Readiness> socketObservations=[];
        private readonly Dictionary<int,SocketDescriptor> socketDescriptors=[];
        private readonly Dictionary<int,NativeAotSocketMessages.Message> socketMessageInputs=[];
        private readonly Dictionary<int,NativeAotSocketMessages.Message> socketMessageCalls=[];
        private readonly HashSet<int> preparedSocketMessages=[];
        private readonly List<SocketDescriptor> socketDescriptions=[];
        private int socketIdlePolls,socketAccepts,socketBlocks,socketResumes;
        private RuntimeThread? socketSelected;
        private bool socketIdleAllowed;
        private ulong socketSent,socketReceived;
        private bool containmentTimeoutObserved,containmentDropObserved;
        private readonly WebControlInspection? webControls;
        private readonly List<SocketLifetime>? webLifetimes;
        private int webControlEvents;
        private bool webPrivateRetired;

        private void SocketMessageInput(RuntimeThread thread)
        {
            if(!socket || !Starts("M16_013_MESSAGE "))return;
            Need(socketMessageInputs.TryGetValue(position,out var value),"socket_message_input_record");
            var message=value!;
            Need(message.Identity==thread.Identity && socketDescriptors.TryGetValue((int)message.Descriptor,out var descriptor) &&
                !descriptor.Port && descriptor.Phase==3 && preparedSocketMessages.Add(message.CallRecordIndex),"socket_message_input_owner");
            Need(memory.Covered(message.Header,56,1) &&
                (message.Vectors.Count==0 || memory.Covered(message.Table,(ulong)message.Vectors.Count*16,1)),"socket_message_metadata_mapping");
            if(message.Number==47)
                Need(memory.Covered(message.Header+8,4,3) && memory.Covered(message.Header+40,8,3) &&
                    memory.Covered(message.Header+48,4,3),"socket_message_header_output");
            foreach(var vector in message.Vectors)
                Need(vector.Bytes==0 || memory.Covered(vector.Address,vector.Bytes,message.Number==46?1U:3U),"socket_message_vector_mapping");
            int fd=(int)message.Descriptor;
            Need(message.Generation==descriptorGenerations[fd],"socket_message_descriptor_generation");
            position+=1+message.Vectors.Count;
        }

        private void SocketObservations(bool supervisor=false)
        {
            if(!socket)return;
            while(true)
            {
                SocketTransportObservations();
                if(!Starts("M16_012_POLL ") && !Starts("M16_012_WAKE "))break;
                Need(socketObservations.TryGetValue(position,out var observed),"socket_observation_position");
                if(observed.Kind==NativeAotSocketGuest.ObservationKind.Poll && observed.Identity!=(socketSelected?.Identity??0))break;
                position++;
                if(observed.Kind==NativeAotSocketGuest.ObservationKind.Poll)
                {
                    if(observed.Idle){Need(supervisor && socketIdleAllowed,"socket_idle_context");socketIdlePolls++;socketIdleAllowed=false;}
                    Clock(observed.Clock);
                    Need(observed.Ready==(uint)ready.Count,"socket_poll_ready");
                    if(supervisor && !observed.Idle)socketIdleAllowed=ready.Count==0;
                    continue;
                }
                var t=history.SingleOrDefault(t=>t.Identity==observed.Identity && !t.Reaped);
                Need(t is not null && t.WaitSource==3 && t.LastSequence==observed.Sequence && t.Epoll is not null &&
                    !t.EpollObserved && observed.State==(t.Parking?5U:1U),"socket_wake_lifetime");
                // Prior syscall wakes are serialized before a later native clock
                // advance. A known deadline/signal winner cannot be replaced.
                Clock(observed.Clock);
                if(observed.Completion==1)
                {
                    Need(t!.Completion is null && t.EpollEligible,"socket_wake_eligibility");
                    t.EpollWinner=1;Complete(t,long.MinValue);
                }
                else Need(t!.EpollWinner==observed.Completion && t.Completion.HasValue,"socket_wake_winner");
                t.EpollObserved=true;
            }
        }

        private void SocketTransportObservations()
        {
            if(!socket)return;
            if(containment)
            {
                while(position<rows.Length && (rows[position].StartsWith("M16_011_SLOT ",StringComparison.Ordinal) ||
                    rows[position].StartsWith("M07_027_TIMEOUT ",StringComparison.Ordinal) || rows[position].StartsWith("M07_028_DROP ",StringComparison.Ordinal)))
                {
                    // The mandatory containment capture already validates all
                    // exact fields, identities, clocks and ordering. Consume
                    // only those rows, at their original scheduler positions.
                    string row=Take();
                    if(row.StartsWith("M07_027_TIMEOUT ",StringComparison.Ordinal))containmentTimeoutObserved=true;
                    if(row.StartsWith("M07_028_DROP ",StringComparison.Ordinal))containmentDropObserved=true;
                }
                return;
            }
            while(position<rows.Length && (IsSocketTransport(rows[position]) || webControls is not null && IsWebControlEvent(rows[position])))
            {
                if(IsWebControlEvent(rows[position]))
                {
                    Need(webControlEvents<2 && rows[position]==(webControlEvents==0?webControls!.Sockets.Observations.DropRow:
                        webControls!.Sockets.Observations.TimeoutRow),"web_replay_control_event_order");
                    WebControlIo(before:true,requirePrivate:webControlEvents==1);
                    _=Take();webControlEvents++;continue;
                }
                var r=SocketTransport(Take());Need(r["clock"]<=long.MaxValue,"socket_transport_clock");
                if(webControls is not null && r.TryGetValue("event",out ulong slotEvent) && slotEvent==1 &&
                    r["remotePort"]==(ulong)webControls.Sockets.Deadline.SourcePort)
                {
                    var bad=webLifetimes!.Single(l=>l.RemotePort==r["remotePort"]);
                    Need(webControlEvents==2 && !webPrivateRetired && r["clock"]==bad.Retired,"web_replay_control_retire");
                    WebBadIo(requirePrivate:true);webPrivateRetired=true;
                }
                // Transport clocks do not advance the released thread table.
                // The mandatory parent/packet consumer owns full handle/tuple,
                // TIME-WAIT, retire, NIC and wire accounting for these rows.
            }
        }

        private SocketDescriptor WebControlDescription(int connection)
        {
            var descriptor=socketDescriptions.SingleOrDefault(d=>!d.Port && d.Phase==3 && d.Connection==connection);
            Need(descriptor is not null,"web_replay_control_descriptor");return descriptor!;
        }

        private void WebBadIo(bool requirePrivate)
        {
            var controls=webControls!.Sockets;int ordinary=webLogQuota?5:6;
            var bad=WebControlDescription(ordinary);
            Need(socketAccepts==ordinary+2 && bad.Sent==(ulong)controls.Deadline.ResponseHex.Length/2 &&
                bad.Received==(ulong)controls.Deadline.RequestHex.Length/2 && bad.Reset==0 &&
                (!requirePrivate || !socketDescriptors.ContainsValue(bad)),"web_replay_control_bad_io");
        }

        private void WebControlIo(bool before,bool requirePrivate)
        {
            WebBadIo(requirePrivate);var controls=webControls!.Sockets;var healthy=WebControlDescription((webLogQuota?5:6)+1);
            ulong received=(ulong)controls.Before.RequestHex.Length/2,sent=(ulong)controls.Before.ResponseHex.Length/2;
            if(!before){received+=(ulong)controls.After.RequestHex.Length/2;sent+=(ulong)controls.After.ResponseHex.Length/2;}
            Need(healthy.Received==received && healthy.Sent==sent && healthy.Reset==0 &&
                (!before || socketDescriptors.ContainsValue(healthy) && !healthy.WriteClosed && !healthy.ReadClosed),"web_replay_control_healthy_io");
        }

        // Return true only when Prepare itself stages another real wait. Such
        // a transfer never entered CPL3 or armed an APIC timer: no DRAIN exists.
        private bool SocketPrepare(RuntimeThread t)
        {
            Need(socket && t.Epoll is not null && t.Completion.HasValue && t.EpollObserved &&
                socketObservations.TryGetValue(position,out var observed) && observed.Identity==t.Identity,"socket_prepare_owner");
            var value=socketObservations[position++];Clock(value.Clock);
            Need(value.PreviousSequence==t.LastSequence || value.Sequence==t.LastSequence,"socket_prepare_sequence");
            if(value.Kind==NativeAotSocketGuest.ObservationKind.Repark)
            {
                Need(t.EpollWinner==1 && value.PreviousSequence==t.LastSequence && value.Sequence>t.LastSequence &&
                    value.Deadline==t.Deadline && (t.Deadline==0 || clock<t.Deadline),"socket_repark_owner");
                Need(t.Epoll!.Watches.Count!=0 || SocketReadyUpperBound(t.Epoll)==0,"web_probe_repark_ready");
                t.LastSequence=value.Sequence;t.WaitOrder=++waitOrder;t.AddressOrder=++addressOrder;
                t.Completion=null;t.Parking=true;t.EpollWinner=0;t.EpollObserved=false;t.EpollEligible=SocketReadyUpperBound(t.Epoll)!=0;
                InterruptPendingWait(t);
                SocketObservations();return true;
            }
            long result=unchecked((long)value.ValueBits);
            Need(value.Kind==NativeAotSocketGuest.ObservationKind.Complete && value.Sequence==t.LastSequence &&
                value.Completion==t.EpollWinner && (t.EpollWinner==5?result==-4:
                    result>=0 && result<=t.EpollMaximum && result<=SocketReadyUpperBound(t.Epoll!) &&
                    (result!=0 || t.Deadline!=0 && clock>=t.Deadline)),"socket_complete_result");
            t.Completion=result;socketResumes++;SocketObservations();return false;
        }

        private void SocketInstall(ulong fd,bool port,uint flags,int phase=0)
        {
            Need(fd==(ulong)LowestFd() && fd is >=3 and <16 &&
                (!port || socketDescriptions.Count(d=>d.Port && (socketDescriptors.ContainsValue(d) || history.Any(t=>!t.Reaped && t.Epoll==d)))<4),"socket_new_descriptor");
            var value=new SocketDescriptor(port,flags){Phase=phase};files.Add((int)fd);InstallDescriptorGeneration((int)fd);
            socketDescriptors.Add((int)fd,value);socketDescriptions.Add(value);
        }
        private void SocketClose(int fd)
        {
            Need(socketDescriptors.Remove(fd,out var value) && files.Remove(fd),"socket_close_owner");
            if(value!.Phase==3 && !web)
            {
                if(containment)ContainmentClose(value);
                else if(!vectors)
                {
                    int i=value.Connection;
                    Need(value.Received==(i==1?1UL:20UL) && value.Sent==(i==0?65536UL:i==1?0UL:18UL) &&
                        value.Eof==(i>=2?1:0) && value.Reset==(i==1?1:0) && value.WriteClosed==(i>=2),"socket_connection_io");
                }
            }
            foreach(var port in socketDescriptions.Where(d=>d.Port))
                foreach(int target in port.Watches.Where(pair=>pair.Value==value).Select(pair=>pair.Key).ToArray())port.Watches.Remove(target);
            SocketEligibility();
            if(value.Port && !history.Any(t=>!t.Reaped && t.Epoll==value))
            {value.Watches.Clear();value.ProbeReadWatches.Clear();}
        }

        private void SocketEligibility()
        {
            foreach(var t in history.Where(t=>!t.Reaped && t.Epoll is not null && t.Completion is null))
                t.EpollEligible=SocketReadyUpperBound(t.Epoll!)!=0;
        }

        private bool SocketCall(RuntimeThread t,Dictionary<string,ulong> r,LinuxCallWaitObservation witness,ulong beforeClock,out ulong action,out ulong status)
        {
            ulong nr=r["nr"],a=r["a0"],b=r["a1"],c=r["a2"],d=r["a3"],e=r["a4"],f=r["a5"],v=r["valueBits"];
            long result=unchecked((long)v);action=status=1;
            socketDescriptors.TryGetValue(a<=int.MaxValue?(int)a:-1,out var descriptor);
            if(nr==41)
            {
                Need((b&~0x8080fUL)==0,"socket_create_flags");
                if(a!=2)Need(result==-97,"socket_family_probe");
                else if((b&15)!=1)Need(result==-94,"socket_type_probe");
                else
                {
                    Need(c is 0 or 6,"socket_protocol");
                    if(result==-24)Need(socketPolicy && LowestFd()==-1,"socket_descriptor_capacity");
                    else SocketInstall(v,false,(uint)b&0x80800);
                }
                return true;
            }
            if(nr is 213 or 291)
            {
                Need(nr==213?unchecked((int)a)>0:(a&~0x80000UL)==0,"socket_epoll_create_flags");
                SocketInstall(v,true,nr==291?(uint)a:0);return true;
            }
            if(nr is 3 or 72 or 16)
            {
                if(descriptor is null)return false;
                if(nr==3){Need(v==0,"socket_close_result");SocketClose((int)a);return true;}
                if(nr==16)
                {
                    Need(!descriptor.Port && b==0x5421 && memory.Covered(c,4,1) && v==0,"socket_nonblocking_ioctl");
                    // The opaque pointed value is checked by the native service;
                    // the later F_GETFL observation supplies exact flag state.
                    descriptor.StatusUnknown=true;
                    return true;
                }
                switch(b)
                {
                    case 1: Need(v==((descriptor.Flags&0x80000)!=0?1UL:0UL),"socket_get_fdflags");break;
                    case 2: Need(c<=1 && v==0,"socket_set_fdflags");descriptor.Flags=(descriptor.Flags&~0x80000U)|(c==1?0x80000U:0);break;
                    case 3:
                        Need((v&~0x802UL)==0 && (v&2)==2 &&
                            (descriptor.StatusUnknown || (v&0x800)==(descriptor.Flags&0x800)),"socket_get_statusflags");
                        descriptor.Flags=(descriptor.Flags&~0x800U)|((uint)v&0x800);descriptor.StatusUnknown=false;break;
                    case 4:
                        // The unchanged musl path also supplies O_LARGEFILE;
                        // the descriptor service ignores that access flag.
                        Need((c&~0x8802UL)==0 && v==0,"socket_set_statusflags");
                        descriptor.Flags=(descriptor.Flags&~0x800U)|((uint)c&0x800);descriptor.StatusUnknown=false;break;
                    default:throw new ToolFailure("socket_unmeasured_fcntl");
                }
                return true;
            }
            if(nr is not (41 or 43 or 44 or 45 or 46 or 47 or 48 or 49 or 50 or 54 or 55 or 51 or 52 or 232 or 233 or 281 or 288))
            {
                Need(descriptor is null || nr is not (0 or 1 or 8),"socket_unmeasured_fd_operation");return false;
            }
            Need(descriptor is not null,"socket_call_descriptor");
            if(nr is 232 or 233 or 281)
            {
                Need(descriptor!.Port,"socket_epoll_descriptor");
                if(nr==233)
                {
                    var pipe=ProbeReadTarget(c);
                    bool stream=c<=int.MaxValue && socketDescriptors.TryGetValue((int)c,out var target) && !target.Port;
                    Need(b is 1 or 2 or 3 && c<=int.MaxValue && (stream || pipe is not null) &&
                        (b==2 || memory.Covered(d,12,1)),"socket_epoll_target");
                    bool found=descriptor.Watches.ContainsKey((int)c) || descriptor.ProbeReadWatches.ContainsKey((int)c);
                    if(b==1 && found)Need(result==-17,"socket_epoll_duplicate");
                    else if(b!=1 && !found)Need(result==-2,"socket_epoll_missing");
                    else
                    {
                        Need(v==0,"socket_epoll_control_result");
                        if(b==2){descriptor.Watches.Remove((int)c);descriptor.ProbeReadWatches.Remove((int)c);}
                        else if(pipe is not null)descriptor.ProbeReadWatches[(int)c]=pipe;
                        else descriptor.Watches[(int)c]=socketDescriptors[(int)c];
                        SocketEligibility();
                    }
                }
                else
                {
                    int timeout=unchecked((int)d);
                    Need(c is >0 and <=1024 && memory.Covered(b,c*12,3) && (nr!=281 || e==0 && f==8),"socket_epoll_buffer");
                    if(r["action"]==3)
                    {
                        Need(v==0 && timeout!=0 && (timeout<0?witness.Deadline==0:witness.Deadline>clock &&
                            witness.Deadline-clock<=(ulong)timeout*1_000_000),"socket_epoll_deadline");
                        t.Epoll=descriptor;t.EpollMaximum=(uint)c;
                        t.EpollWinner=0;t.EpollObserved=false;t.EpollEligible=SocketReadyUpperBound(descriptor)!=0;
                        Stage(t,witness,3,0,0,0);
                        action=3;status=9;socketBlocks++;
                    }
                    else
                    {
                        Need(result>=0 && result<=(long)c && result<=SocketReadyUpperBound(descriptor) &&
                            (result!=0 || timeout>=0),"socket_epoll_immediate");
                        if(result==0 && timeout>0)
                        {
                            ulong duration=(ulong)timeout*1_000_000;
                            Need(socketObservations.TryGetValue(position,out var expired) && expired.Kind==NativeAotSocketGuest.ObservationKind.Expire &&
                                expired.Identity==t.Identity && expired.Ordinal==witness.Ordinal && expired.Clock==clock &&
                                expired.Deadline>=Math.Min((ulong)long.MaxValue,beforeClock+duration),"socket_immediate_expiry");
                            position++;
                        }
                    }
                }
                return true;
            }
            Need(!descriptor!.Port,"socket_stream_descriptor");
            switch(nr)
            {
                case 49:
                    Need(descriptor.Phase==0 && c==16 && memory.Covered(b,16,1),"socket_bind");
                    if(result<0)
                        Need(socketPolicy && (result==-13 || result==-98 && socketDescriptors.Values.Any(other=>
                            !other.Port && other.Phase==2)),"socket_policy_bind_error");
                    else {Need(v==0,"socket_bind_result");descriptor.Phase=1;}
                    break;
                case 50:Need(descriptor.Phase is 1 or 2 && (socketPolicy?b is 1 or 4:b==(containment?3UL:web?512UL:4UL)) &&
                    v==0,"socket_listen");descriptor.Phase=2;break;
                case 43:case 288:
                    Need(descriptor.Phase==2 && (b==0 || memory.Covered(b,16,3) && memory.Covered(c,4,3)) &&
                        (nr==43 || (d&~0x80800UL)==0),"socket_accept_arguments");
                    if(result==-11)break;
                    Need(!containment || socketAccepts<3,"socket_containment_accept_bound");
                    SocketInstall(v,false,nr==288?(uint)d:0,3);socketDescriptors[(int)v].Connection=socketAccepts++;break;
                case 44:case 45:case 46:case 47:
                    bool messageCall=nr is 46 or 47,send=nr is 44 or 46;
                    if(messageCall)
                    {
                        Need(socketMessageCalls.TryGetValue(witness.RecordIndex,out var observed) &&
                            preparedSocketMessages.Remove(witness.RecordIndex),"socket_message_call_snapshot");
                        c=observed!.Total;d=observed.Flags;
                    }
                    Need(descriptor.Phase==3 && c<=(messageCall?LinuxSocketServices.MaximumMessageBytes:65536UL) &&
                        (messageCall || c>0 && e==0 && f==0 && memory.Covered(b,send?Math.Min(c,8192):c,send?1U:3U)) &&
                        (d&~(send?0x4040UL:web || messageCall?0x42UL:0x40UL))==0,"socket_io_arguments");
                    if(containment){Need(!messageCall,"socket_containment_unmeasured_message");ContainmentIo(descriptor,nr,c,result);break;}
                    Need(result is -11 or -104 or -32 || result>=0 && result<=(long)Math.Min(c,8192),"socket_io_result");
                    if(web)
                    {
                        if(send)
                            Need((c==0 || result!=0) && (!descriptor.WriteClosed || result is -32 or -104) &&
                                (descriptor.Reset==0 || result==-32),"web_socket_send_state");
                        else
                        {
                            Need(result!=-32 && (!descriptor.ReadClosed || result is 0 or -104) &&
                                (descriptor.Reset==0 || result>=0),"web_socket_receive_state");
                            if(c!=0 && !descriptor.ReadClosed && descriptor.PeekBytes!=0)
                                Need(result>0 && (ulong)result>=Math.Min(c,Math.Min(8192UL,descriptor.PeekBytes)),"web_socket_unread_peek");
                            if(result>0)
                                descriptor.PeekBytes=(d&2)!=0?Math.Max(descriptor.PeekBytes,(ulong)result):
                                    descriptor.PeekBytes>=(ulong)result?descriptor.PeekBytes-(ulong)result:0;
                        }
                        if(result==-104)Need(descriptor.Reset==0,"web_socket_reset_consumed");
                    }
                    if(result>0)
                    {
                        if(send){Need(!descriptor.WriteClosed,"socket_send_after_shutdown");socketSent+=(ulong)result;descriptor.Sent+=(ulong)result;}
                        else
                        {
                            Need(!descriptor.ReadClosed,"socket_receive_after_shutdown");
                            if((d&2)==0){socketReceived+=(ulong)result;descriptor.Received+=(ulong)result;}
                        }
                    }
                    if(!send && c!=0 && result==0)descriptor.Eof++;
                    if(result==-104)descriptor.Reset++;
                    break;
                case 48:Need(!containment && descriptor.Phase==3 && (web?b is 0 or 1 or 2:b==1) && v==0,"socket_shutdown");
                    if(b is 1 or 2)descriptor.WriteClosed=true;
                    if(b is 0 or 2){descriptor.ReadClosed=true;descriptor.PeekBytes=0;}break;
                case 51:
                    Need(web && descriptor.Phase is >=1 and <=3 && memory.Covered(b,16,3) && memory.Covered(c,4,3) && v==0,"web_socket_name");break;
                case 54:
                    Need(memory.Covered(d,e,1) && (e==4 && (b==1 && c is 2 or 7 || b==6 && c==1) && v==0 ||
                        b==1 && c==13 && e==8 && result==-22),"socket_option");break;
                default:throw new ToolFailure("socket_unmeasured_syscall_"+nr);
            }
            return true;
        }

        private void SocketFinish()
        {
            if(service)
                Need(socketAccepts>=10 && socketSent>0 && socketReceived>0 && clones>=2,"service_socket_workload_lifetimes");
            else if(containment)
                Need(socketAccepts==3 && socketSent==containmentCapture!.Application.Queued+30 && socketReceived==33 && clones>=2 &&
                    containmentTimeoutObserved && containmentDropObserved,"socket_containment_workload_lifetimes");
            else if(vectors)Need(socketAccepts==4 && socketSent==524352 && socketReceived==256 && clones>=2 &&
                socketBlocks>0 && socketResumes>0,"socket_vectors_workload_lifetimes");
            else Need(socketAccepts==4 && socketSent==65572 && socketReceived==61 && clones>=2 &&
                (app!="NativeAotSocketSmokeAsync" || socketBlocks>0 && socketResumes>0),"socket_workload_lifetimes");
            foreach(int fd in Enumerable.Range(0,16).Where(fd=>fd<3 || files.Contains(fd) || fd==pipeRead || fd==pipeWrite).ToArray())
            {
                SocketTransportObservations();
                var close=Row("M16_011_CLOSE","fd:dec","valueBits:dec");
                Need(close["fd"]==(ulong)fd && close["valueBits"]==0,"socket_final_close");
                if(socketDescriptors.ContainsKey(fd))SocketClose(fd);else files.Remove(fd);
                if(fd==pipeRead)pipeRead=0;if(fd==pipeWrite)pipeWrite=0;
            }
            SocketTransportObservations();
            Need(socketDescriptors.Count==0 && pipeRead==0 && pipeWrite==0,"socket_final_descriptors");
        }
    }
}
