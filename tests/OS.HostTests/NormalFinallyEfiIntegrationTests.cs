using OS.Tooling;
using OS.Boot;
using OS.Kernel.Diagnostics;
using System.Buffers.Binary;

internal static class NormalFinallyEfiIntegrationTests
{
    internal static string[] Flags => [.. RuntimeProfilePortTests.RethrowFlags[..^1], "M04NormalFinally"];
    internal static string[] Arguments => [.. Flags.Select(name => "--Enable" + name + "Probe")];

    [HostTest("m04.021.normal-finally guarded compiler fixture admission", HostLane.Fast, 0)]
    private static void CompilerFixtureAdmission()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        const string relative=@"Runtime\NormalFinallyProbe.cs";
        string source=File.ReadAllText(Path.Combine(root,"src/OS",relative));
        var project=EfiProjectAdmission.InspectStatic(root);
        HostCheck.True(project.Succeeded,project.Reason+":"+project.Detail);
        HostCheck.True(EfiSourceAdmission.Inspect(source,relative).Succeeded,"exact reviewed fixture");
        HostCheck.True(source.StartsWith("#if NOSTDLIB_EFI && M04_NORMAL_FINALLY_PROBE",StringComparison.Ordinal),"dedicated opt-in fixture, no ancestor anchoring");
        foreach(var (from,to) in new[]{("(MethodImplOptions)0x40","(MethodImplOptions)0"),
            ("NormalThrower(value);","NormalBody(value);"),
            ("stamp = value.Payload + 2;","stamp = value.Payload + 3;"),
            ("CLROS_NormalFinally_CompilerAddress","CLROS_Unreviewed_CompilerAddress"),
            ("NormalTransferAdmitted = false;","NormalTransferAdmitted = true;"),
            ("recovered.ExitedClauseIndex != 0","recovered.ExitedClauseIndex != 1"),
            ("actual->GetInteger(reg) != s_ehCaller.GetInteger(reg)","false"),
            ("a.Low != b.Low || a.High != b.High","false")})
        {
            string changed=source.Replace(from,to,StringComparison.Ordinal);
            HostCheck.True(changed!=source,"control changes source");
            var rejected=EfiSourceAdmission.Inspect(changed,relative);
            HostCheck.True(!rejected.Succeeded&&rejected.Reason=="forbidden_active_source"&&
                rejected.Detail.EndsWith(":reviewed_normal_finally_source_identity",StringComparison.Ordinal),"fixture control "+from+":"+rejected.Reason);
        }
    }

    [HostTest("m04.021.normal-finally profile dependencies and closed guest gate", HostLane.Fast, 1)]
    private static void Profile()
    {
        HostCheck.True(!Probes.EnableM04NormalFinallyProbe && !BootSequence.IsNormalFinallyEnabled(),"ordinary profile defaults off");
        foreach (bool build in new[] { false, true })
        {
            var selected = EfiOptions.Parse(Arguments,build).Profile();
            HostCheck.True(selected.BootPolicy == "Diagnostic" && Flags.All(selected.IsEnabled),"dedicated GC-dependent profile");
            foreach (string missing in Flags)
            {
                bool failed=false;
                try { _=EfiOptions.Parse([.. Flags.Where(name=>name!=missing).Select(name=>"--Enable"+name+"Probe")],build).Profile(); }
                catch(ToolFailure){failed=true;}
                HostCheck.True(failed == (missing != "M04NormalFinally"),"exact prerequisite "+missing);
            }
            foreach (string conflict in new[]{"M04SimpleThrowCatch","M04SingleFinallyCatch","M04Rethrow","M04NestedFinallyCatch","M04UnhandledException","M04FrameDispatch","M04EhCatalog","M04ConstructorFailure","M04FilteredCatch","M04SeededCollectorChurn"})
            {
                bool failed=false;
                try { _=EfiOptions.Parse([..Arguments,"--Enable"+conflict+"Probe"],build).Profile(); }
                catch(ToolFailure){failed=true;}
                HostCheck.True(failed,"profile collision "+conflict);
            }
        }
        var options=new QemuSmokeOptions { DryRun=true, BuildEfi=true };
        foreach(string flag in Flags)options.Flags["Enable"+flag+"Probe"]=true;
        bool rejected=false;
        try { _=QemuProfiles.Create(options); }
        catch(ToolFailure e){rejected=e.Reason=="qemu_normal_finally_transfer_not_admitted";}
        HostCheck.True(rejected,"even dry-run guest planning denies unreviewed transfer");
    }

    [HostTest("m04.021.normal-finally exact native clause and unwind controls", HostLane.Fast, 2)]
    private static void NativeVectors()
    {
        // Independent wire vectors for two original compiler rows: [43,56)
        // finally handler89; [43,62) typed handler121, typeRVA0x5000.
        byte[] table=[4,86,106,178,86,152,242,0,80,0,0];
        HostCheck.True(EfiVerify.TestNormalFinallyEhTable(table,0x5000),"original two-row vector");
        foreach(int length in new[]{0,1,7,10,12})HostCheck.True(!EfiVerify.TestNormalFinallyEhTable(new byte[length],0x5000),"exact EH extent");
        for(int i=0;i<table.Length;i++)
        {
            byte[] bad=[..table];bad[i]^=1;HostCheck.True(!EfiVerify.TestNormalFinallyEhTable(bad,0x5000),"EH byte control "+i);
        }
        HostCheck.True(!EfiVerify.TestNormalFinallyEhTable(table,0)&&!EfiVerify.TestNormalFinallyEhTable(table,0x100000000),"type range");
        byte[][] unwind=[[1,5,2,0,5,114,1,80,4,0,80,0,0],[1,4,1,0,4,66,1],[1,4,1,0,4,66,0]];
        for(int part=0;part<unwind.Length;part++)
        {
            HostCheck.True(EfiVerify.TestNormalFinallyUnwind(unwind[part],part,0x5000),"original own-frame unwind "+part);
            for(int i=0;i<unwind[part].Length;i++)
            {
                byte[] bad=[..unwind[part]];bad[i]^=1;
                HostCheck.True(!EfiVerify.TestNormalFinallyUnwind(bad,part,0x5000),"flags/frame/GP/handler/trailer control");
            }
            HostCheck.True(!EfiVerify.TestNormalFinallyUnwind(unwind[part][..^1],part,0x5000),"unwind truncated");
        }
        HostCheck.True(!EfiVerify.TestNormalFinallyUnwind(unwind[0],0,0x5001),"stale original EH RVA");
        byte[] thrower=[0x48,0x83,0xec,0x28,0xe8,0,0,0,0,0xcc];
        HostCheck.True(EfiVerify.TestNormalFinallyCode(thrower,false),"original thrower code");
        foreach(int offset in new[]{0,1,2,3,4,9})
        { byte[] bad=[..thrower];bad[offset]^=1;HostCheck.True(!EfiVerify.TestNormalFinallyCode(bad,false),"thrower opcode/frame control"); }
        BinaryPrimitives.WriteUInt32LittleEndian(thrower.AsSpan(5),0x1234);
        HostCheck.True(EfiVerify.TestNormalFinallyCode(thrower,false),"rel32 is joined separately to genuine bridge");
    }
}
