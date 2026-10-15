using OS.Tooling;
using F = OS.Tooling.LinuxSignalProofFixture;
using Shared = LinuxSignalTranscriptTests;

internal static class LinuxSignalFaultTranscriptTests
{
    [HostTest("m16-009 synthetic hardware-fault tails require exact recovery fatal and invalid-return protocols")]
    private static void Positive()
    {
        foreach (string variant in F.FaultVariants)
        foreach (ulong pid in new[] {1UL,107UL})
        {
            string[] rows=Synthetic(variant,pid);
            QemuTranscripts.ValidateLinuxSignalTail(rows,F.CreatePackage(variant),0x200000,0x100000);
            HostCheck.Equal(variant=="FaultFatal" ? 19 : variant=="FaultReturn" ? 25 : 30,rows.Length);
            HostCheck.Equal(1,rows.Count(r=>r.StartsWith("M16_009_FAULT ",StringComparison.Ordinal)));
        }
    }

    [HostTest("m16-009 hardware-fault records reject forged identity vector error RIP RSP address code delivery and duplicate capture")]
    private static void FaultRejections()
    {
        foreach (string variant in F.FaultVariants)
        {
            string[] rows=Synthetic(variant); byte[] package=F.CreatePackage(variant);
            int at=Array.FindIndex(rows,r=>r.StartsWith("M16_009_FAULT ",StringComparison.Ordinal));
            foreach (string key in new[] {"identity","vector","error","rip","rsp","address","code","delivery"})
                Shared.Reject(Shared.Change(rows,at,key,Shared.Field(rows[at],key)+1),package);
            Shared.Reject([..rows[..at],rows[at],..rows[at..]],package);
            foreach (string other in F.FaultVariants.Where(v=>v!=variant)) Shared.Reject(rows,F.CreatePackage(other));
        }
    }

    [HostTest("m16-009 hardware-fault tails reject altered invocation bases handler stack results and privilege")]
    private static void CallRejections()
    {
        foreach (string variant in F.FaultVariants)
        {
            string[] rows=Synthetic(variant); byte[] package=F.CreatePackage(variant);
            foreach (int at in Enumerable.Range(0,rows.Length).Where(i=>rows[i].StartsWith("M16_008_CALL ",StringComparison.Ordinal)))
            {
                foreach (string key in new[] {"identity","nr","a0","a1","a2","a3","a4","a5","rip","rsp","fs","gs","clock","valueBits","action","status"})
                    Shared.Reject(Shared.Change(rows,at,key,Shared.Field(rows[at],key)+1),package);
                Shared.Reject(Shared.Change(rows,at,"flags",0),package);
                Shared.Reject(Shared.Change(rows,at,"flags",0x10446),package);
            }
        }
    }

    [HostTest("m16-009 hardware-fault readiness rejects missing extra reordered witness exit reap and restoration rows")]
    private static void CompleteLifetime()
    {
        foreach (string variant in F.FaultVariants)
        {
            string[] rows=Synthetic(variant); byte[] package=F.CreatePackage(variant);
            for (int i=0;i<rows.Length;i++)
            {
                Shared.Reject(rows.Where((_,at)=>at!=i).ToArray(),package);
                string[] altered=(string[])rows.Clone(); altered[i]+=" false=true"; Shared.Reject(altered,package);
                if (i+1<rows.Length) { altered=(string[])rows.Clone(); (altered[i],altered[i+1])=(altered[i+1],altered[i]); Shared.Reject(altered,package); }
            }
            Shared.Reject([..rows,rows[^1]],package);
            int end=Array.FindIndex(rows,r=>r.StartsWith("M16_008_OWNER_END ",StringComparison.Ordinal));
            foreach (string key in new[] {"reaped","transfers","irqs","exit"}) Shared.Reject(Shared.Change(rows,end,key,Shared.Field(rows[end],key)+1),package);
        }
    }

    private static string[] Synthetic(string variant,ulong pid=19)
    {
        string[] original=Shared.Synthetic("AlternateStack",pid);
        List<string> rows=[..original.Take(5)];
        (ulong Nr,ulong A0,ulong A1,ulong A2,ulong A3)[] calls=[
            (39,0,0,0,0),(186,0,0,0,0),(158,0x1002,0x402000,0,0),(158,0x1001,0x402100,0,0),
            (13,11,0x401100,0,8),(131,0x401120,0,0,0),(186,0,0,0,0),(14,2,0,0x401158,8),(131,0,0x4011a0,0,0),
            (1,1,0x4001a0,15,0),(15,1,0x4001a0,15,0),(14,2,0,0x401150,8),(131,0,0x401180,0,0),(1,1,0x400180,15,0),(231,42,0,0,0)];
        (ulong Vector,ulong Error,ulong Address,ulong Code,ulong Rsp) expected=variant switch
        {
            "FaultGp"=>(13,0,0,128,0x405000), "FaultWrite"=>(14,7,0x400180,2,0x405000),
            "FaultExecute" or "FaultReturn"=>(14,21,0x403000,2,0x405000),"FaultStack"=>(14,4,0x150000000,1,0),
            _=>(14,4,0x150000000,1,0x405000)
        };
        var fault=F.ExpectedFault(variant); var instructions=F.ExpectedInvocations(variant);
        void AddFault()=>rows.Add(FormattableString.Invariant($"M16_009_FAULT identity=4294967297 vector={expected.Vector} error={expected.Error} rip={fault.SavedRip} rsp={expected.Rsp} address={expected.Address} code={expected.Code} delivery={(variant=="FaultFatal" ? 4 : 3)}"));
        for (int i=0;i<instructions.Count;i++)
        {
            if (i==6) AddFault();
            if (i is 9 or 13) rows.Add("M16_005_WRITE fd=1 bytes=15 data="+(i==9 ? "M16_HANDLER_OK!" : "M16_SIGNALS_OK!"));
            var c=calls[i]; ulong action=i==14 || i==10 && variant=="FaultReturn" ? 2UL : i==10 ? 5UL : 1UL;
            ulong value=i is 0 or 1 or 6 ? pid : i is 9 or 13 ? 15UL : i==10 && variant!="FaultReturn" ? 0x12345678UL : 0;
            ulong rsp=i==10 ? 0x406c40UL : i is >=6 and <=9 ? 0x406c38UL : 0x405000UL;
            rows.Add(FormattableString.Invariant($"M16_008_CALL identity=4294967297 nr={c.Nr} a0={c.A0} a1={c.A1} a2={c.A2} a3={c.A3} a4=0 a5=0 rip={instructions[i].ReturnRip} rsp={rsp} flags=70 fs={(i<2 ? 0UL : 0x402000UL)} gs={(i<3 ? 0UL : 0x402100UL)} clock=0 valueBits={value} action={action} status={(action==2 ? 2 : 1)}"));
        }
        if (variant=="FaultFatal") AddFault();
        rows.AddRange(original.TakeLast(7).Select(r=>r.StartsWith("M16_008_OWNER_END ",StringComparison.Ordinal) ?
            r.Replace("exit=42 ","exit="+F.ExpectedExitCode(variant)+" ",StringComparison.Ordinal) : r));
        return LinuxCallOrdinalTests.Annotate(rows);
    }
}
