using OS.Kernel.Compatibility.LinuxX64;

namespace OS.Tooling;

public static partial class QemuTranscripts
{
    private readonly record struct SignalWaitCall(ulong Nr,long Value,ulong A0=0,ulong A1=0,ulong A2=0,ulong A3=0,ulong A4=0,ulong Action=1,ulong Status=1);

    private static void ValidateLinuxSignalWaitBody(string[] rows,string variant)
    {
        bool futex=variant.StartsWith("SignalFutex",StringComparison.Ordinal), partial=variant=="SignalPipePartial";
        bool restart=variant.EndsWith("Restart",StringComparison.Ordinal);
        var instructions=LinuxSignalProofFixture.ExpectedWaitInvocations(variant);
        List<SignalWaitCall> pre=[new(39,1),new(186,1),new(158,0,0x1002,0x402000),new(13,0,10,0x401100,0,8),new(131,0,0x401120)];
        if(!futex)pre.Add(new(293,0,0x4011c0));
        pre.Add(new(56,2,0x7d0f00,0x406000,0x4011b8,0x4011bc,0x403000));
        SignalWaitCall blocked=futex?new(202,0,0x4011d0,128,0,0,Action:3,Status:9):
            new(partial?1UL:0UL,0,partial?4UL:3UL,partial?0x407000UL:0x409000UL,partial?4100UL:1UL,Action:3,Status:9);
        pre.Add(blocked);
        List<SignalWaitCall> child=[new(39,1),new(186,2),new(234,0,1,1,10)];
        if(futex)child.Add(new(202,0,0x4011d0,129,1));
        else if(!partial)child.Add(new(1,1,4,0x407000,1));
        child.Add(new(60,0,7,Action:4,Status:2));
        SignalWaitCall[] handler=[new(186,1),new(14,0,2,0,0x401158,8),new(131,0,0,0x4011a0),new(1,15,1,0x4001a0,15),
            new(15,partial?4096:restart?futex?202:0:-4,1,0x4001a0,15,Action:5)];
        List<SignalWaitCall> post=[];
        if(restart)post.Add(blocked with {Value=futex?-11:1,Action=1,Status=1});
        else if(!futex && !partial)post.Add(new(0,1,3,0x409000,1));
        if(partial){post.Add(new(0,4096,3,0x409000,4096));post.Add(new(1,4,4,0x408000,4));post.Add(new(0,4,3,0x409000,4));}
        post.Add(new(14,0,2,0,0x401150,8));post.Add(new(131,0,0,0x401180));
        if(!futex){post.Add(new(3,0,3));post.Add(new(3,0,4));}
        post.Add(new(1,15,1,0x400180,15));post.Add(new(231,0,42,Action:2,Status:2));
        Need(instructions.Count==pre.Count+child.Count+handler.Length+post.Count,"m16_009_wait_inventory");
        int at=0,call=0;ulong clock=0;
        string Take(){Need(at<rows.Length,"m16_009_wait_truncated");return rows[at++];}
        void Exact(string text)=>Need(Take()==text,"m16_009_wait_order");
        Dictionary<string,ulong> Row(string prefix,params string[] keys)=>ReadRecord(Take(),prefix,keys);
        void Check(SignalWaitCall expected,bool isChild=false,bool isHandler=false)
        {
            if(expected.Nr==1 && expected.A0==1)
                Exact("M16_005_WRITE fd=1 bytes=15 data="+(isHandler?"M16_HANDLER_OK!":"M16_SIGNALS_OK!"));
            var actual=Row("M16_008_CALL","identity:dec","nr:dec","a0:dec","a1:dec","a2:dec","a3:dec","a4:dec","a5:dec",
                "rip:dec","rsp:dec","flags:dec","fs:dec","gs:dec","clock:dec","valueBits:dec","action:dec","status:dec");
            var instruction=instructions[call++];
            Need(instruction.Child==isChild && instruction.Handler==isHandler && instruction.Number==expected.Nr &&
                actual["identity"]==(isChild?0x100000002UL:0x100000001UL) && actual["nr"]==expected.Nr && actual["rip"]==instruction.ReturnRip &&
                actual["a0"]==expected.A0 && actual["a1"]==expected.A1 && actual["a2"]==expected.A2 && actual["a3"]==expected.A3 && actual["a4"]==expected.A4 && actual["a5"]==0,
                "m16_009_wait_call_binding");
            ulong rsp=isChild?0x406000UL:isHandler?expected.Nr==15?0x406c40UL:0x406c38UL:0x405000UL;
            ulong fs=isChild?0x403000UL:call<=2?0UL:0x402000UL;
            // Only initial admission and signal interruption advance the table.
            // The changed-word EAGAIN replay reads HPET but publishes no time.
            bool advance=futex&&(expected.Nr==202&&expected.A1==128&&expected.Action==3||isChild&&expected.Nr==234);
            Need(actual["clock"]>=clock && actual["clock"]<=long.MaxValue && (advance||actual["clock"]==clock) &&
                actual["rsp"]==rsp && actual["fs"]==fs && actual["gs"]==0 && LinuxSyscallGateway.ValidateUserFlags(actual["flags"],false) &&
                (actual["flags"]&0x400)==0,"m16_009_wait_envelope");
            clock=actual["clock"];
            Need(actual["valueBits"]==unchecked((ulong)expected.Value)&&actual["action"]==expected.Action&&actual["status"]==expected.Status,"m16_009_wait_outcome");
        }
        void Exit(ulong identity){var e=Row("M16_008_EXIT","identity:dec","clearTidResult:dec");Need(e["identity"]==identity&&e["clearTidResult"]==0,"m16_009_wait_clear_tid");}
        void Supervisor(ulong identity,ulong action,ulong transfers,ulong ready,ulong active)
        {
            var s=Row("M16_008_SUPERVISOR","identity:dec","action:dec","transfers:dec","ready:dec","active:dec");
            Need(s["identity"]==identity&&s["action"]==action&&s["transfers"]==transfers&&s["ready"]==ready&&s["active"]==active,"m16_009_wait_supervisor");
        }
        void Reap(ulong identity)
        {
            const string suffix=" context=zero stack=zero slot=zero";string line=Take();Need(line.EndsWith(suffix,StringComparison.Ordinal),"m16_009_wait_reap_scrub");
            var r=ReadRecord(line[..^suffix.Length],"M16_008_REAP","old:dec","next:dec");
            Need(r["old"]==identity&&r["next"]==identity+(1UL<<32),"m16_009_wait_reap_generation");
        }
        foreach(var e in pre)Check(e);Supervisor(0x100000001,3,2,1,2);
        foreach(var e in child)Check(e,true);Exit(0x100000002);Supervisor(0x100000002,4,4,1,2);Reap(0x100000002);
        var resumed=Row("M16_008_RESUME","identity:dec","valueBits:dec","status:dec");
        Need(resumed["identity"]==0x100000001&&resumed["valueBits"]==unchecked((ulong)(partial?4096L:-4L))&&resumed["status"]==1,"m16_009_wait_resume_once");
        foreach(var e in handler)Check(e,false,true);foreach(var e in post)Check(e);
        Exit(0x100000001);Supervisor(0x100000001,2,6,0,1);Reap(0x100000001);
        Exact("M16_008_OWNER_END reaped=2 transfers=6 irqs=0 exit=42 userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained");
        Exact("M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true");Exact("CLROS_M16_009_READY");Exact("TEST_PASS m16-009");
        Need(call==instructions.Count&&at==rows.Length,"m16_009_wait_extra_record");
    }
}
