using OS.Tooling;
using F = OS.Tooling.LinuxSignalProofFixture;
using T = LinuxSignalTranscriptTests;

internal static class LinuxSignalNestedTranscriptTests
{
    [HostTest("m16-009 nested tails require two real restorers or fatal fourth alternate stack delivery")]
    private static void Positive()
    {
        foreach(string variant in F.NestedVariants)
        {
            string[] rows=Synthetic(variant);
            QemuTranscripts.ValidateLinuxSignalTail(rows,F.CreatePackage(variant),0x200000,0x100000);
            HostCheck.Equal(variant=="AlternateStackExhaustion"?34:43,rows.Length);
            HostCheck.Equal(variant=="AlternateStackExhaustion"?0:2,rows.Count(r=>r.StartsWith("M16_008_CALL ",StringComparison.Ordinal)&&T.Field(r,"nr")==15));
        }
    }

    [HostTest("m16-009 nested tails reject forged depth stack mask pointer restorer and exhaustion continuations")]
    private static void CallBinding()
    {
        foreach(string variant in F.NestedVariants)
        {
            string[] rows=Synthetic(variant);byte[] package=F.CreatePackage(variant);
            foreach(int at in Enumerable.Range(0,rows.Length).Where(i=>rows[i].StartsWith("M16_008_CALL ",StringComparison.Ordinal)))
            {
                foreach(string key in new[]{"identity","nr","a0","a1","a2","a3","a4","a5","rip","rsp","fs","gs","clock","valueBits","action","status","ordinal"})
                    T.Reject(T.Change(rows,at,key,T.Field(rows[at],key)+1),package);
                foreach(ulong flags in new[]{0UL,0x246UL,0x446UL,0x3046UL})T.Reject(T.Change(rows,at,"flags",flags),package);
            }
            int end=Array.FindIndex(rows,r=>r.StartsWith("M16_008_OWNER_END ",StringComparison.Ordinal));
            T.Reject(T.Change(rows,end,"exit",variant=="AlternateStackExhaustion"?42UL:139UL),package);
            T.Reject(rows,F.CreatePackage(variant=="AlternateStackExhaustion"?"NestedAlternateStack":"AlternateStackExhaustion"));
        }
    }

    [HostTest("m16-009 nested protocols reject lost repeated handler calls and unconfirmed termination or restoration")]
    private static void OrderAndCleanup()
    {
        foreach(string variant in F.NestedVariants)
        {
            string[] rows=Synthetic(variant);byte[] package=F.CreatePackage(variant);
            for(int i=0;i<rows.Length;i++)
            {
                T.Reject(rows.Where((_,at)=>at!=i).ToArray(),package);
                string[] changed=(string[])rows.Clone();changed[i]+=" extra=1";T.Reject(changed,package);
                if(i+1<rows.Length){changed=(string[])rows.Clone();(changed[i],changed[i+1])=(changed[i+1],changed[i]);T.Reject(changed,package);}
            }
            T.Reject([..rows,rows[^1]],package);
        }
    }

    private static string[] Synthetic(string variant)
    {
        bool exhaustion=variant=="AlternateStackExhaustion";
        List<string> rows=T.Synthetic("AlternateStack").Take(5).ToList();
        var instructions=F.ExpectedNestedInvocations(variant);int index=0;
        void Emit(ulong nr,uint depth=0,ulong a=0,ulong b=0,ulong c=0,ulong d=0,ulong value=0)
        {
            if(nr==1)rows.Add("M16_005_WRITE fd=1 bytes=15 data="+(depth==0?"M16_SIGNALS_OK!":"M16_HANDLER_OK!"));
            ulong rsp=depth switch{0=>0x405000,1=>0x406c38,2=>0x4067b8,3=>0x406338,_=>throw new InvalidOperationException()};
            if(nr==15)rsp+=8;
            rows.Add(FormattableString.Invariant($"M16_008_CALL identity=4294967297 nr={nr} a0={a} a1={b} a2={c} a3={d} a4=0 a5=0 rip={instructions[index].ReturnRip} rsp={rsp} flags=70 fs={(index<2?0UL:0x402000UL)} gs=0 clock=0 valueBits={value} action={(nr==15?5:nr==231?2:1)} status={(nr==231?2:1)}"));index++;
        }
        Emit(39,value:1);Emit(186,value:1);Emit(158,a:0x1002,b:0x402000);Emit(13,a:10,b:0x401100,d:8);
        Emit(131,a:0x401120);Emit(14,a:2,b:0x401140,d:8);Emit(234,a:1,b:1,c:10);
        for(uint depth=1;depth<=(exhaustion?3:2);depth++)
        {
            Emit(186,depth,value:1);Emit(14,depth,2,0,0x401158,8);Emit(131,depth,b:0x4011a0);
            Emit(14,depth,2,0x401260+(depth-1)*8,0,8);if(exhaustion||depth==1)Emit(234,depth,1,1,10);
        }
        if(!exhaustion)
        {
            for(uint depth=2;depth>0;depth--)
            {
                Emit(14,depth,2,0,0x401158,8);Emit(131,depth,b:0x4011a0);Emit(1,depth,1,0x4001a0,15,value:15);
                Emit(15,depth,1,0x4001a0,15,value:depth==2?0x23456789UL:0x12345678UL);
            }
            Emit(14,a:2,c:0x401150,d:8);Emit(131,b:0x401180);Emit(1,a:1,b:0x400180,c:15,value:15);Emit(231,a:42);
        }
        rows.AddRange(["M16_008_EXIT identity=4294967297 clearTidResult=0",
            "M16_008_SUPERVISOR identity=4294967297 action=2 transfers=2 ready=0 active=1",
            "M16_008_REAP old=4294967297 next=8589934593 context=zero stack=zero slot=zero",
            "M16_008_OWNER_END reaped=1 transfers=2 irqs=0 exit="+(exhaustion?"139":"42")+" userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained",
            "M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true","CLROS_M16_009_READY","TEST_PASS m16-009"]);
        HostCheck.Equal(instructions.Count,index);return LinuxCallOrdinalTests.Annotate(rows);
    }
}
