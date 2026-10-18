namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        private void SignalObservation(RuntimeThread t,Dictionary<string,ulong> r,LinuxCallReturnObservation value)
        {
            Need(value.Identity==t.Identity && value.ValueBits==r["valueBits"],"threading_return_binding");
            Need((value.BlockedSignals&((1UL<<8)|(1UL<<18)))==0,"threading_unblockable_mask");
            int incoming=checked((int)value.SignalDepth)+(r["nr"]==15?1:0);
            bool entered=incoming!=t.Frames.Count;
            if(incoming!=t.Frames.Count)
            {
                Need(incoming==t.Frames.Count+1 && t.Requests>0,"threading_unrequested_handler");
                ulong frame=r["nr"]==15?r["rsp"]-8:value.SignalFrame;
                ulong interrupted=t.DeliveryRsp??t.SavedRsp;
                Need(interrupted>=1100,"threading_signal_redzone");
                ulong fp=(interrupted-128-512)&~63UL, expected=((fp-440)&~15UL)-8;
                Need(frame==expected && memory.Covered(frame-8,448,3) && memory.Covered(fp,512,3),"threading_signal_frame_geometry");
                Need(phases is null || t.SignalProofs.Count!=0,"runtime_signal_request_evidence");
                CollectionSignal? proof=phases is null?null:t.SignalProofs.Dequeue();
                Need(proof is null || proof.Identity==t.Identity,"runtime_signal_generation");
                t.Requests--; t.Delivered++; t.Frames.Push((frame,interrupted,proof));t.DeliveryRsp=null;
            }
            if(r["nr"]==15)
            {
                Need(t.Frames.Count>0 && r["action"]==5 && r["status"]==1,"threading_sigreturn_without_frame");
                var frame=t.Frames.Pop(); Need(r["rsp"]==frame.Frame+8 && value.Rsp==frame.Rsp && instruction(value.Rip),"threading_sigreturn_context");
                Envelope(t,value.Rip,value.Rsp,value.Flags,value.Fs,value.Gs,15); t.Returned++; signalReturns++;
                if(frame.Proof is not null)phases!.Collected(frame.Proof);
                t.TimedRestart=null; // rt_sigreturn invalidates the restart callback.
            }
            else
                Need(value.Rip==r["rip"] && value.Rsp==r["rsp"] && value.Flags==r["flags"] && value.Fs==r["fs"] && value.Gs==r["gs"],"threading_unexpected_context_rewrite");
            Need(value.SignalDepth==(ulong)t.Frames.Count && value.SignalFrame==(t.Frames.Count==0?0:t.Frames.Peek().Frame),"threading_signal_owner");
            if(!entered && r["nr"] is not (14 or 15))Need(value.BlockedSignals==t.Mask,"threading_unexplained_mask_change");
            t.Mask=value.BlockedSignals;
        }

        private bool Borrowed(ulong start,ulong bytes)=>history.Any(t=>!t.Reaped && t.WaitSource==1 && HelloMemory.Overlap(start,bytes,t.WaitAddress,4));
        private void Stage(RuntimeThread t,LinuxCallWaitObservation witness,uint source,ulong address,uint mode,uint bits)
        {
            Need(witness.Source==source && witness.Sequence>t.LastSequence && (witness.Deadline==0 || witness.Deadline>clock),"threading_wait_owner");
            t.WaitSource=source; t.WaitAddress=address; t.WaitMode=mode; t.WaitBits=bits;
            t.Deadline=witness.Deadline; t.LastSequence=witness.Sequence;
            t.WaitOrder=++waitOrder;t.AddressOrder=++addressOrder;t.Parking=true;
        }
        private int LowestFd()=>Enumerable.Range(3,13).FirstOrDefault(fd=>!files.Contains(fd) && fd!=pipeRead && fd!=pipeWrite,-1);
        private readonly uint[] descriptorGenerations=new uint[16];
        private void InstallDescriptorGeneration(int fd)
        {
            Need(fd is >=3 and <16 && descriptorGenerations[fd]!=uint.MaxValue,"threading_descriptor_generation");
            descriptorGenerations[fd]++;
        }
        private void Call(RuntimeThread t,Dictionary<string,ulong> r,LinuxCallWaitObservation witness,bool output)
        {
            ulong nr=r["nr"],a=r["a0"],b=r["a1"],c=r["a2"],d=r["a3"],e=r["a4"],f=r["a5"],v=r["valueBits"];
            Need(witness.Identity==t.Identity && witness.Ordinal<=4096,"threading_wait_generation");
            if(t.Tid==1 && witness.Ordinal<=3)Need(nr==(witness.Ordinal==1?9UL:witness.Ordinal==2?158UL:218UL),"threading_main_startup_order");
            RuntimeThread? targetClock=nr is 200 or 234?slots.SingleOrDefault(x=>x?.Tid==unchecked((int)(nr==200?a:b))):null;
            bool readsClock=nr==202 && (((uint)b&~384U) is 0 or 9) || nr==219 && t.TimedRestart.HasValue ||
                targetClock is not null && !targetClock.Exiting && targetClock.WaitSource==1 &&
                unchecked((int)(nr==200?b:c))==35 && (targetClock.Mask&(1UL<<34))==0;
            ulong beforeClock=clock;
            Need(r["clock"]==clock || readsClock || socket,"threading_unowned_clock_change"); Clock(r["clock"]);
            ulong action=1,status=1;
            if(web && WebFileCall(r,witness)) { }
            else if(socket && SocketCall(t,r,witness,beforeClock,out action,out status)) { }
            else switch(nr)
            {
                case 9:
                    Need(b>0 && b<=0x400000000 && c is 0 or 1 or 3 && d is 0x22 or 0x32 && (uint)e==uint.MaxValue && f==0,"threading_mmap");
                    if(v==Bits(-12)) break;
                    Need(v>=0x100000000 && v<0x1000000000 && (d!=0x32 || v==a),"threading_mmap_result");
                    if(d==0x32) Need(!Borrowed(a,HelloMemory.Round(b)),"threading_fixed_borrow");
                    memory.Map(v,b,(uint)c,d==0x32);
                    if(d==0x32 && HelloMemory.Overlap(a,HelloMemory.Round(b),0x400000000,0x200000)) heapIntact=false;
                    break;
                case 10: case 11: case 28:
                    Need(a>=4096 && a%4096==0 && b>0 && b<=0x1000000000,"threading_mapping_extent");
                    if(a<0x100000000)
                    {
                        if(nr!=10){Need(v==Bits(-14),"threading_static_mutation_rejection");break;}
                        bool admitted=memory.CanProtectImage(a,b,(uint)c,out long imageError);
                        bool held=admitted && (c==3)!=memory.Covered(a,HelloMemory.Round(b),3) && Borrowed(a,HelloMemory.Round(b));
                        Need(v==Bits(held?-12:imageError),"threading_image_protection");
                        if(admitted && !held)memory.Protect(a,b,(uint)c);
                        break;
                    }
                    bool borrowed=Borrowed(a,HelloMemory.Round(b)) && (nr!=28 || c is 4 or 8);
                    if(borrowed){Need(v==Bits(-12),"threading_mapping_borrow");break;}
                    Need(v==0,"threading_mapping_result");
                    if(nr==10){Need(c is 0 or 1 or 3,"threading_protection");memory.Protect(a,b,(uint)c);}
                    else if(nr==11)
                    {
                        if(HelloMemory.Overlap(a,HelloMemory.Round(b),r["rsp"]-8,8) || r["fs"]!=0 && HelloMemory.Overlap(a,HelloMemory.Round(b),r["fs"],1))
                            ArmExit(t,r);
                        memory.Unmap(a,b);if(HelloMemory.Overlap(a,HelloMemory.Round(b),0x400000000,0x200000))heapIntact=false;
                    }
                    else Need(c is 4 or 8 or 16 or 17 && memory.Covered(a,HelloMemory.Round(b),0),"threading_advice");
                    break;
                case 12:
                    if(a==0)Need(v==currentBreak,"threading_brk_query");
                    else if(heapIntact && a>=0x400000000 && a<=0x400200000 && !Borrowed(Math.Min(a,currentBreak),a>currentBreak?a-currentBreak:currentBreak-a))
                    {Need(v==a,"threading_brk_change");memory.Protect(0x400000000,0x200000,0);memory.Protect(0x400000000,HelloMemory.Round(a-0x400000000),3,true);currentBreak=a;}
                    else Need(v==currentBreak,"threading_brk_rejection"); break;
                case 25:
                    Need(a%4096==0 && b>0 && c>0 && d==0,"threading_remap");
                    if(a<0x100000000)
                    {Need(b==4096 && c==8192 && a>=t.Bottom-4096 && a<t.Top && v==Bits(memory.Covered(a,4096,0)?-12:-14),"threading_stack_probe");if(!memory.Covered(a,4096,0))stackProbe=true;break;}
                    if(v==Bits(-12)||v==Bits(-14))break;
                    Need(v==a && memory.Covered(a,HelloMemory.Round(b),0),"threading_remap_result");
                    ulong old=HelloMemory.Round(b),size=HelloMemory.Round(c);
                    if(size<old){Need(!Borrowed(a+size,old-size),"threading_remap_borrow");memory.Unmap(a+size,old-size);}
                    else if(size>old) memory.Map(a+old,size-old,memory.DynamicWritable(a).Protection,false);
                    break;
                case 158:
                    Need(a==0x1002 && memory.Covered(b,8,1) && v==0,"threading_set_fs"); t.Fs=b; break;
                case 218: Need(v==(ulong)t.Tid,"threading_set_tid"); t.ClearTid=a;break;
                case 39: Need(v==1,"threading_pid");break;
                case 186: Need(v==(ulong)t.Tid,"threading_tid");break;
                case 13:
                    Need(a is >=1 and <=64 && d==8 && v==0 && (b==0 || memory.Covered(b,32,1)) && (c==0 || memory.Covered(c,32,3)),"threading_sigaction");
                    if(b!=0)actions.Add(a);break;
                case 14:
                    Need(a<=2 && d==8 && v==0 && (b==0 || memory.Covered(b,8,1)) && (c==0 || memory.Covered(c,8,3)),"threading_sigmask");break;
                case 15: action=5;break;
                case 56:
                    Need(a==0x7d0f00 && memory.Covered(c,4,3) && memory.Covered(d,4,3),"threading_clone_arguments");
                    int slot=Array.FindIndex(slots,x=>x is null);
                    if(slot<0){Need(v==Bits(-11),"threading_clone_full");break;}
                    Need(v==(ulong)nextTid && b%8==0,"threading_clone_tid");
                    var stack=memory.DynamicWritable(b);
                    Need(b>=stack.Start+128 && b<stack.End && e>b && memory.Covered(e,512,3) &&
                        slots.All(x=>x is null || x.Fs!=e),"threading_clone_stack_tls");
                    ulong identity=(generations[slot]<<32)|(uint)(slot+1);
                    var child=new RuntimeThread(identity,nextTid++,stack.Start,b,e,r["rip"]){ClearTid=d,Gs=t.Gs,Mask=t.Mask};
                    slots[slot]=child; history.Add(child); Enqueue(child); active++;peak=Math.Max(peak,active);clones++;break;
                case 202:
                    uint op=(uint)b, mode=op&128,cmd=op&~384U,bits=cmd is 9 or 10?(uint)f:uint.MaxValue;
                    Need((a&3)==0 && (b&~(128UL|15))==0 && cmd is 0 or 1 or 3 or 9 or 10 && bits!=0 &&
                        memory.Covered(a,4,mode==0?3U:1U),"threading_futex_arguments");
                    if(cmd==3)
                    {
                        int wake=unchecked((int)c),move=unchecked((int)d);
                        Need(wake>=0 && move>=0 && (e&3)==0 && memory.Covered(e,4,mode==0?3U:1U),"threading_requeue_arguments");
                        Need(v==(ulong)Requeue(a,e,mode,(uint)wake,(uint)move),"threading_requeue_count");
                    }
                    else if(cmd is 1 or 10)
                    {int n=unchecked((int)c);Need(v==(ulong)Wake(a,mode,n<=0?1U:(uint)n,bits,explicitWake:true),"threading_wake_count");}
                    else if(r["action"]==3)
                    {
                        Need(v==0 && (d==0?witness.Deadline==0:memory.Covered(d,16,1)&&witness.Deadline>clock),"threading_futex_deadline");
                        Stage(t,witness,1,a,mode,bits);
                        t.WaitCall=new(a,op,unchecked((uint)c),d,bits,r["rip"],witness.Deadline);action=3;status=9;
                    }
                    else Need(v==Bits(-11) || d!=0 && v==Bits(-110),"threading_no_invented_wait_success");
                    break;
                case 219:
                    if(t.TimedRestart is not { } saved){Need(v==Bits(-4),"threading_unarmed_restart");break;}
                    t.TimedRestart=null;
                    if(!memory.Covered(saved.Address,4,(saved.Op&128)==0?3U:1U))
                    {Need(v==Bits(-14),"threading_restart_backing");break;}
                    if(r["action"]==3)
                    {
                        Need(v==0 && saved.Deadline>clock && witness.Deadline==saved.Deadline,"threading_restart_deadline");
                        Stage(t,witness,1,saved.Address,saved.Op&128,saved.Bits);t.WaitCall=saved;action=3;status=9;
                    }
                    else Need(v==Bits(-11) || saved.Deadline<=clock && v==Bits(-110),"threading_restart_result");
                    break;
                case 200: case 234:
                    int target=unchecked((int)(nr==200?a:b)),signal=unchecked((int)(nr==200?b:c));
                    Need(nr==200 || a==1,"threading_signal_process");
                    var recipient=slots.SingleOrDefault(x=>x?.Tid==target);
                    if(recipient is null){Need(v==Bits(-3),"threading_signal_stale_target");break;}
                    Need(v==0 && signal is 0 or 35,"threading_signal_request");
                    if(signal==35 && !recipient.Exiting)
                    {
                        Need(actions.Contains(35) && recipient.Requests<4,"threading_signal_action");recipient.Requests++;
                        if(phases is not null)recipient.SignalProofs.Enqueue(new(recipient.Identity,
                            history.Count(x=>!x.Reaped && x.WaitSource!=0 && x.Completion is null),
                            recipient!=t && ready.Contains(recipient) && recipient.WaitSource==0 && recipient.LastUserIrq!=0));
                        // NativeAOT unblocks its activation signal. A real staged
                        // wait is interrupted only when its completion has not won.
                        if(recipient.WaitSource!=0 && recipient.Completion is null && (recipient.Mask&(1UL<<34))==0)
                        {if(recipient.WaitSource==3)recipient.EpollWinner=5;Complete(recipient,-4);}
                    }
                    break;
                case 2: case 257:
                    Need(memory.Covered(nr==2?a:b,1,1) && (nr!=257 || a==Bits(-100)) && ((nr==2?b:c)&~(web?0x98100UL:0x98000UL))==0,"threading_open");
                    if(v!=Bits(-2))
                    {
                        Need(v==(ulong)LowestFd() && v is >=3 and <16,"threading_open_fd");files.Add((int)v);InstallDescriptorGeneration((int)v);
                        if(web)webFileGenerations.Add((int)v,++webFileGeneration);
                    }break;
                case 3:
                    Need(v==0 && a<=int.MaxValue,"threading_close_result");
                    if((int)a==pipeWrite){pipeWrite=0;foreach(var waiter in history.Where(x=>!x.Reaped&&x.WaitSource==2&&x.Completion is null))Complete(waiter,0);}
                    else if((int)a==pipeRead)pipeRead=0;
                    else Need(files.Remove((int)a),"threading_close_fd");
                    if(web)webFileGenerations.Remove((int)a);break;
                case 0:
                    Need(c>0 && c<=8192 && memory.Covered(b,c,3),"threading_read_buffer");
                    if(a==(ulong)pipeRead && pipeRead!=0)
                    {
                        Need(c==1,"threading_signal_pipe_read");
                        if(pipeBytes!=0){Need(v==1,"threading_pipe_read_result");pipeBytes--;}
                        else if(pipeWrite==0)Need(v==0,"threading_pipe_eof");
                        else {Need(v==0,"threading_pipe_block_value");Stage(t,witness,2,0,0,0);action=3;status=9;}
                    }
                    else Need(v<=c && (a==0?v==0:a<=int.MaxValue && files.Contains((int)a)),"threading_file_read");break;
                case 1:
                    Need(c>0 && c<=(web?65536UL:4096UL) && memory.Covered(b,c,1),"threading_write_buffer");
                    if(a==1 || web && a==2)
                    {
                        // Web startup/logging admission separately joins every
                        // quota denial to its actual EIO with no copied bytes.
                        if(web && v==Bits(-5)){Need(webLogQuota && !output,"threading_write_error_output");break;}
                        Need(output && writes<writeLengths.Count && a==writeChannels[writes] &&
                            (web?v==(ulong)writeLengths[writes] && v==Math.Min(c,8192UL):c==(ulong)writeLengths[writes] && v==c),"threading_stdout_pair");
                        if(a==1)stdoutBytes+=writeLengths[writes];writes++;phases?.Write(stdoutBytes);
                    }
                    else
                    {
                        Need(a==(ulong)pipeWrite && pipeWrite!=0 && pipeRead!=0 && c==1 && v==1 && pipeBytes<4096,"threading_pipe_write");pipeBytes++;
                        var reader=history.Where(x=>!x.Reaped&&x.WaitSource==2&&x.Completion is null).ToArray();
                        Need(reader.Length<=1,"threading_unobserved_pipe_retry");if(reader.Length==1)Complete(reader[0],1);
                    }break;
                case 20:
                    // Pointer-table access is replayed here. The preceding web
                    // logging validator joins actual copied bytes/results;
                    // table contents are not reconstructed from scalar rows.
                    Need(web && c is >0 and <=OS.Kernel.Compatibility.LinuxX64.LinuxDescriptorServices.MaximumVectors &&
                        memory.Covered(b,c*16,1),"web_writev_table");
                    if(a is 1 or 2)
                    {
                        if(v==Bits(-5)){Need(webLogQuota && !output,"web_writev_error_output");break;}
                        Need(output && writes<writeLengths.Count && a==writeChannels[writes] &&
                            v==(ulong)writeLengths[writes] && v is >0 and <=8192,"web_writev_output");
                        if(a==1)stdoutBytes+=writeLengths[writes];writes++;phases?.Write(stdoutBytes);
                    }
                    else Need(a==1234 && c==1 && v==Bits(-9) && !output,"web_writev_invalid_descriptor");
                    break;
                case 8: Need(a<=int.MaxValue && files.Contains((int)a) && c<=2 && v<=8192,"threading_seek");break;
                case 72: Need(a<=int.MaxValue && files.Contains((int)a) && b==2 && c==1 && v==0,"threading_fcntl");break;
                case 293:
                    Need(pipeRead==0 && pipeWrite==0 && memory.Covered(a,8,3) && b==0x80000 && v==0,"threading_pipe_create");
                    pipeRead=LowestFd();pipeWrite=LowestFd();Need(pipeRead>=3 && pipeWrite>=3,"threading_pipe_capacity");
                    InstallDescriptorGeneration(pipeRead);InstallDescriptorGeneration(pipeWrite);signalPipeCreated=true;break;
                case 60:
                    Need(a==0 && v==0,"threading_thread_exit");t.Exiting=t.NormalExit=true;normalExits++;action=4;status=2;break;
                case 231:
                    Need(!web,"web_replay_unexpected_group_exit");
                    Need(a==0 && v==0 && writes==writeLengths.Count,"threading_group_exit");stopping=true;action=2;status=2;
                    foreach(var other in slots.Where(x=>x is not null).Select(x=>x!))
                    {other.Exiting=other.GroupRetired=true;if(other.WaitSource!=0 && other.Completion is null)other.Completion=-125;if(other!=t && !ready.Contains(other))Enqueue(other);}
                    break;
                case 16: Need(a is 0 or 1 && b is 0x5401 or 0x5413 && memory.Covered(c,b==0x5401?36UL:8UL,3) && v==Bits(-25),"threading_not_tty");break;
                case 97: Need(a is 3 or 9 && memory.Covered(b,16,3) && v==0,"threading_rlimit");break;
                case 99: Need(memory.Covered(a,112,3) && v==0,"threading_sysinfo");break;
                case 204: Need(a is 0 or 1 && b is >=8 and <=4096 && memory.Covered(c,8,3) && v==8,"threading_affinity");break;
                case 228:
                    // Only the declared realtime-dependent web fixture owns UTC.
                    // Other profiles retain their unsupported ID0 contract.
                    Need(a is 1 or 6 || web && a==0?memory.Covered(b,16,3) && v==0:v==Bits(-22),"threading_monotonic_clock");
                    if(a==6 && v==0)phases?.MonotonicRead(clock);
                    break;
                case 302: Need(a is 0 or 1 && b is 3 or 9 && c==0 && memory.Covered(d,16,3) && v==0,"threading_limit");break;
                case 318:
                    Need(b is >0 and <=32 && c<=3 && memory.Covered(a,b,3),"threading_entropy");
                    ulong remainingEntropy=entropyLimit-entropyBytes;
                    long entropyResult=remainingEntropy==0?((c&1)!=0?-11:-38):
                        b>remainingEntropy && (c&1)==0?-38:(long)Math.Min(b,remainingEntropy);
                    Need(v==Bits(entropyResult),"threading_entropy_result");
                    if(entropyResult>0)entropyBytes+=(ulong)entropyResult;
                    break;
                case 324:
                    Need(b==0 && c==0 && a is 0 or 8 or 16 && v==(a==0?0x18UL:0UL) && (a!=8||barrierRegistered),"threading_barrier");if(a==16)barrierRegistered=true;break;
                case 24: Need(v==0,"threading_yield_result");action=6;break;
                case 35: case 98: case 137: case 157: case 217: case 239: case 309:
                    Need(v==Bits(-38),"threading_unsupported_result");break;
                default: throw new ToolFailure("threading_uninventoried_syscall_"+nr);
            }
            Need(r["action"]==action && r["status"]==status,"threading_call_outcome");
            if(action!=3)Need(witness.Source==0 && witness.Deadline==0 && witness.Sequence==0,"threading_spurious_wait");
        }
    }
}
