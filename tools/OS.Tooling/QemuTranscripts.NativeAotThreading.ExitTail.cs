using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private sealed partial class ThreadingProtocol
    {
        private static unsafe HashSet<ulong> FindExitTails(byte[] elf,Func<ulong,bool> instruction,Func<ulong,bool> syscall)
        {
            HashSet<ulong> found=[];
            ReadOnlySpan<byte> pattern=[0xb8,11,0,0,0,0x0f,5,0x48,0x31,0xff,0xb8,60,0,0,0,0x0f,5];
            fixed(byte* image=elf)
            {
                Need(LinuxElfLoader.Validate(image,(uint)elf.Length,64*1024*1024,1572864,out var plan)==LinuxElfLoadStatus.Ready,"threading_exit_elf");
                for(uint i=0;i<plan.LoadCount;i++)
                {
                    uint at=i*5;
                    if(plan.Loads[at+4]!=5)continue;
                    var code=elf.AsSpan(checked((int)plan.Loads[at+1]),checked((int)plan.Loads[at+2]));
                    for(int offset=0;offset<=code.Length-pattern.Length;offset++)
                    {
                        if(!code.Slice(offset,pattern.Length).SequenceEqual(pattern))continue;
                        ulong rip=plan.Loads[at]+(uint)offset+7;
                        if(rip<0x100000000 && instruction(rip-7) && syscall(rip) && instruction(rip) &&
                            instruction(rip+3) && instruction(rip+8) && syscall(rip+10) && instruction(rip+10))found.Add(rip);
                    }
                }
            }
            return found;
        }
        private void ArmExit(RuntimeThread t,Dictionary<string,ulong> row)
        {
            // Bind the first tail observation to the existing live thread
            // before its stack/TLS mapping is removed and the baseline saved.
            Envelope(t,row["rip"],row["rsp"],row["flags"],row["fs"],row["gs"],row["nr"]);
            ulong start=row["a0"],bytes=row["a1"],rsp=row["rsp"],fs=row["fs"];
            Need(t.TailRip==0 && exitTails.Contains(row["rip"]) && bytes is >0 and <=16*1024*1024 && bytes%4096==0 &&
                rsp>=start+8 && rsp<=start+bytes && fs>=start && fs<start+bytes && row["gs"]==0 && t.Frames.Count==0 &&
                t.Mask==~LinuxSignalMask.Unblockable,"threading_exit_tail_admission");
            t.TailRip=row["rip"];t.TailRsp=rsp;t.TailFs=fs;t.TailFlags=row["flags"];
        }
        private void ExitEnvelope(RuntimeThread t,ulong rip,ulong rsp,ulong flags,ulong fs,ulong gs,ulong number)
        {
            Need(rip>=t.TailRip && rsp==t.TailRsp && fs==t.TailFs && gs==0 && t.Mask==~LinuxSignalMask.Unblockable &&
                t.Frames.Count==0 && !memory.Covered(rsp-8,8,0) && !memory.Covered(fs,1,0) &&
                LinuxSyscallGateway.ValidateUserFlags(flags,true),"threading_exit_tail_context");
            ulong offset=rip-t.TailRip;
            bool position=number==11?offset==0:number==60?offset==10:number==0 && (offset==0 || offset==3 || offset==8);
            Need(position && (offset==0?flags==t.TailFlags:(flags&~0x10UL)==((t.TailFlags&~0x8d5UL)|0x44)),"threading_exit_tail_continuation");
        }
    }
}
