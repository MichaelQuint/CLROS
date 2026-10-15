using System.Globalization;
using OS.Tooling;

internal static class LinuxSignalTranscriptTests
{
    [HostTest("m16-009 complete pool envelope rejects old extents physical overflow and overlap in its last worker pages")]
    private static void PoolEnvelope()
    {
        var rows = Synthetic("AlternateStack"); var package = LinuxSignalProofFixture.CreatePackage("AlternateStack");
        QemuTranscripts.ValidateLinuxSignalTail(Change(rows, 4, "pool", 0xfff97000), package, 0x200000, 0x100000);
        Reject(Change(rows, 4, "pool", 0xfff98000), package);
        Reject(Change(rows, 4, "code", 16384), package); Reject(Change(rows, 4, "metadata", 45056), package);
        bool rejected = false;
        try { QemuTranscripts.ValidateLinuxSignalTail(Change(rows, 4, "pool", 0x9c000), package, 0x200000, 0x100000); }
        catch (ToolFailure failure) { rejected = failure.Reason == "m16_009_roots_pool_alias"; }
        HostCheck.True(rejected);
    }

    [HostTest("m16-009 synthetic tails require actual handler restorer and expected negative exit protocols")]
    private static void Positive()
    {
        foreach (string variant in LinuxSignalProofFixture.SyscallVariants)
        foreach (ulong pid in new[] { 1UL,103UL })
        {
            string[] rows = Synthetic(variant,pid);
            QemuTranscripts.ValidateLinuxSignalTail(rows,LinuxSignalProofFixture.CreatePackage(variant),0x200000,0x100000);
            HostCheck.Equal(variant == "BadRestorer" ? 21 : variant == "BadReturn" ? 27 : 32,rows.Length);
            HostCheck.True(rows.Count(r => r.StartsWith("M16_008_CALL ",StringComparison.Ordinal)) ==
                (variant == "BadRestorer" ? 9 : variant == "BadReturn" ? 14 : 18));
        }
    }

    [HostTest("m16-009 signal tails reject forged syscall handler stack context return and privilege fields")]
    private static void CallRejections()
    {
        foreach (string variant in LinuxSignalProofFixture.SyscallVariants)
        {
            string[] rows = Synthetic(variant); byte[] package = LinuxSignalProofFixture.CreatePackage(variant);
            foreach (int at in Enumerable.Range(0,rows.Length).Where(i => rows[i].StartsWith("M16_008_CALL ",StringComparison.Ordinal)))
            {
                foreach (string key in new[] {"identity","nr","a0","a1","a2","a3","a4","a5","rip","rsp","fs","gs","clock","valueBits","action","status"})
                    Reject(Change(rows,at,key,Field(rows[at],key) + 1),package);
                Reject(Change(rows,at,"flags",0),package); Reject(Change(rows,at,"flags",0x3002),package);
                Reject(Change(rows,at,"flags",Field(rows[at],"flags") ^ 0x400),package);
                string[] duplicate = (string[])rows.Clone(); duplicate[at] += " rip=4194304"; Reject(duplicate,package);
            }
        }
    }

    [HostTest("m16-009 signal readiness rejects missing extra reordered witness restoration and reap records")]
    private static void OrderAndLifetime()
    {
        foreach (string variant in LinuxSignalProofFixture.SyscallVariants)
        {
            string[] rows = Synthetic(variant); byte[] package = LinuxSignalProofFixture.CreatePackage(variant);
            for (int i = 0; i < rows.Length; i++)
            {
                Reject(rows.Where((_,at) => at != i).ToArray(),package);
                string[] altered = (string[])rows.Clone(); altered[i] += " false=true"; Reject(altered,package);
                if (i + 1 < rows.Length) { altered = (string[])rows.Clone(); (altered[i],altered[i + 1]) = (altered[i + 1],altered[i]); Reject(altered,package); }
            }
            Reject([.. rows,rows[^1]],package);
            int end = Array.FindIndex(rows,r => r.StartsWith("M16_008_OWNER_END ",StringComparison.Ordinal));
            foreach (string key in new[] {"reaped","transfers","irqs","exit","userReserved","userBacked"}) Reject(Change(rows,end,key,Field(rows[end],key) + 1),package);
            string[] liveStack = (string[])rows.Clone(); liveStack[end] = liveStack[end].Replace("initialStack=zero", "initialStack=dirty", StringComparison.Ordinal); Reject(liveStack,package);
            int reap = Array.FindIndex(rows,r => r.StartsWith("M16_008_REAP ",StringComparison.Ordinal));
            Reject(Change(rows,reap,"next",0x100000001),package);
            int confirm = Array.FindIndex(rows,r => r.StartsWith("M16_008_SUPERVISOR ",StringComparison.Ordinal));
            foreach (string key in new[] {"identity","action","transfers","ready","active"}) Reject(Change(rows,confirm,key,Field(rows[confirm],key) + 1),package);
        }
    }

    [HostTest("m16-009 canonical packages and original parent roots cannot be substituted by names or ancestor tails")]
    private static void PackageAndParents()
    {
        byte[] package = LinuxSignalProofFixture.CreatePackage("AlternateStack"); string[] rows = Synthetic("AlternateStack");
        Reject(rows,LinuxSignalProofFixture.CreatePackage("UserStack")); Reject(rows,LinuxThreadProofFixture.CreatePackage("Threads"));
        byte[] corrupt = (byte[])package.Clone(); corrupt[^1] ^= 1; Reject(rows,corrupt); Reject(rows,package[..^1]);
        Reject(Change(rows,4,"pool",0x200000),package); Reject(Change(rows,4,"root",0x400000),package);
        foreach (string key in new[] {"workers","code","metadata","userImage"}) Reject(Change(rows,4,key,Field(rows[4],key) + 1),package);
        bool rejected = false;
        try { QemuTranscripts.ValidateLinuxSignalTail(rows,package,0x400000,0x100000); } catch (ToolFailure) { rejected = true; }
        HostCheck.True(rejected);
        foreach (string old in new[] {"TEST_START m16-008","CLROS_M16_008_READY","TEST_PASS m16-008"})
        {
            string[] changed = (string[])rows.Clone();
            int at = old.StartsWith("TEST_START",StringComparison.Ordinal) ? 0 : old.StartsWith("CLROS",StringComparison.Ordinal) ? rows.Length - 2 : rows.Length - 1;
            changed[at] = old; Reject(changed,package);
        }
    }

    internal static string[] Synthetic(string variant,ulong pid = 17)
    {
        List<string> rows = ["TEST_START m16-009",
            "M16_006_POLICY physical=268435456 available=134217728 poolPages=4096 vaEnd=68719476736 reservationQuota=17179869184 entropyBytes=32 cpus=1 root=inactive-kernel",
            "M16_007_POLICY descriptors=16 pipes=2 capacity=4096 scratch=8192 packageBytes=28672 package=declared-immutable proc=owned-snapshot stdin=eof stdoutStderr=serial sigpipe=ignored-mechanical blocking=scheduler",
            "M16_005_ARMED root=2097152 cs=59 ss=51 kernelStack=9437184 guard=absent syscall=sce-star-lstar-fmask return=iretq fp=x87-sse kernelFsGs=owned-null",
            "M16_008_BOUND root=1048576 pool=3145728 workers=2 code=28672 metadata=102400 userImage=728 timer=idle-only clock=hpet-nanoseconds reuse=off-stack"];
        (ulong Nr,ulong A0,ulong A1,ulong A2,ulong A3)[] calls = [
            (39,0,0,0,0),(186,0,0,0,0),(158,0x1002,0x402000,0,0),(13,10,0x401100,0,8),(131,0x401120,0,0,0),
            (14,2,0x401140,0,8),(234,pid,pid,10,0),(14,2,0,0x401150,8),(14,2,0x401148,0,8),
            (186,0,0,0,0),(14,2,0,0x401158,8),(131,0,0x4011a0,0,0),(1,1,0x4001a0,15,0),(15,1,0x4001a0,15,0),
            (14,2,0,0x401150,8),(131,0,0x401180,0,0),(1,1,0x400180,15,0),(231,42,0,0,0)];
        var instructions = LinuxSignalProofFixture.ExpectedInvocations(variant);
        ulong stack = variant == "UserStack" ? 0x404bb8UL : 0x406c38UL;
        for (int i = 0; i < instructions.Count; i++)
        {
            if (i is 12 or 16) rows.Add("M16_005_WRITE fd=1 bytes=15 data=" + (i == 12 ? "M16_HANDLER_OK!" : "M16_SIGNALS_OK!"));
            var c = calls[i]; ulong action = i == 17 || i == 13 && variant == "BadReturn" ? 2UL : i == 13 ? 5UL : 1;
            ulong value = i is 0 or 1 or 9 ? pid : i is 12 or 16 ? 15UL : i == 13 && variant != "BadReturn" ? 0x12345678UL : 0;
            ulong rsp = i == 13 ? stack + 8 : i is >= 9 and <= 12 ? stack : 0x405000;
            rows.Add(FormattableString.Invariant($"M16_008_CALL identity=4294967297 nr={c.Nr} a0={c.A0} a1={c.A1} a2={c.A2} a3={c.A3} a4=0 a5=0 rip={instructions[i].ReturnRip} rsp={rsp} flags={(i == 8 ? 0x446UL : 0x46UL)} fs={(i < 2 ? 0UL : 0x402000UL)} gs=0 clock=0 valueBits={value} action={action} status={(action == 2 ? 2 : 1)}"));
        }
        rows.AddRange(["M16_008_EXIT identity=4294967297 clearTidResult=0",
            "M16_008_SUPERVISOR identity=4294967297 action=2 transfers=2 ready=0 active=1",
            "M16_008_REAP old=4294967297 next=8589934593 context=zero stack=zero slot=zero",
            "M16_008_OWNER_END reaped=1 transfers=2 irqs=0 exit=" + (variant.StartsWith("Bad",StringComparison.Ordinal) ? "139" : "42") + " userReserved=0 userBacked=0 process=drained timer=restored descriptors=closed guard=detached pool=scrubbed initialStack=zero reservation=retained",
            "M16_009_RESTORE root=true descriptors=true ss=true msrs=true fsGs=true fp=true stack=true", "CLROS_M16_009_READY", "TEST_PASS m16-009"]);
        return LinuxCallOrdinalTests.Annotate(rows);
    }

    internal static ulong Field(string row,string key) => ulong.Parse(row.Split(' ').Single(x => x.StartsWith(key + "=",StringComparison.Ordinal))[(key.Length + 1)..],CultureInfo.InvariantCulture);
    internal static string[] Change(string[] rows,int index,string key,ulong value)
    {
        string[] copy = (string[])rows.Clone();
        copy[index] = string.Join(' ',copy[index].Split(' ').Select(x => x.StartsWith(key + "=",StringComparison.Ordinal) ? key + "=" + value.ToString(CultureInfo.InvariantCulture) : x));
        return copy;
    }
    internal static void Reject(string[] rows,byte[] package)
    { bool rejected = false; try { QemuTranscripts.ValidateLinuxSignalTail(rows,package,0x200000,0x100000); } catch (ToolFailure) { rejected = true; } HostCheck.True(rejected); }
}
