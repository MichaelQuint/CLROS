using System.Xml.Linq;
using System.Text.Json.Nodes;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static class M04NestedFinallyCatchEfiIntegrationTests
{
    internal const string Flag = "EnableM04NestedFinallyCatchProbe";
    internal static readonly string[] Flags = [.. RuntimeProfilePortTests.RethrowFlags[..^1], "M04NestedFinallyCatch"];
    private static string Root => HostToolProcess.FindRepositoryRoot();
    private static string[] Arguments(IEnumerable<string> names) => names.Select(n => "--Enable"+n+"Probe").ToArray();

    [HostTest("m04.021.nested-finally.efi default off authority")]
    private static unsafe void Defaults()
    {
        var project = XDocument.Load(Path.Combine(Root,"src/OS/OS.csproj"));
        var defaults = project.Descendants(Flag).ToArray();
        HostCheck.True(defaults.Length == 1 && defaults[0].Value == "false" &&
            !Probes.EnableM04NestedFinallyCatchProbe && !BootSequence.IsNestedFinallyCatchEnabled());
        var probe = Probes.GetM04NestedFinallyCatchProbe();
        HostCheck.True(probe.Name == Flag && !probe.EnabledByDefault && probe.Risk == ProbeRisk.Destructive);
        HostCheck.True(Probes.ProbeCatalogCount == 43 && Probes.TryGetProbe(40,out var last) && last.Name == Flag);
        var result = BootSequence.RunNestedFinallyCatchUnwind(default,default,null,0,0,null,0,null,0,default);
        HostCheck.True(!result.Succeeded && result.Caller.Rip == 0 && result.Caller.Rsp == 0);
        var admission = EfiProjectAdmission.InspectStatic(Root);
        HostCheck.True(admission.Succeeded,admission.Reason+" "+admission.Detail);
        HostCheck.True(ContractAudit.Inspect(Root).Count == 0);
    }

    [HostTest("m04.021.nested-finally.efi shared profile dependencies")]
    private static void Profiles()
    {
        foreach(bool build in new[]{false,true})
        {
            var selected = EfiOptions.Parse(Arguments(Flags),build).Profile();
            HostCheck.True(Flags.All(selected.IsEnabled));
            foreach(string missing in Flags[..^1]) Reject([.. Arguments(Flags.Where(n=>n!=missing))],build);
            foreach(string collision in new[]{"M04SimpleThrowCatch","M04SingleFinallyCatch","M04Rethrow","M04UnhandledException","M04FrameDispatch",
                "M04SeededCollectorChurn","M04ClassConstructorExecution","M04InterfaceDispatch","M04X64UnwindMetadata"})
                Reject(Arguments([..Flags,collision]),build);
            Reject(["--boot-policy","VerifiedExit",..Arguments(Flags)],build);
        }
        static void Reject(string[] args,bool build)
        {
            bool rejected=false;
            try { _=EfiOptions.Parse(args,build).Profile(); }
            catch(ToolFailure) { rejected=true; }
            HostCheck.True(rejected,"invalid nested profile admitted");
        }
    }

    [HostTest("m04.021.nested-finally.efi retained exception allocation routing")]
    private static void AllocationRouting()
    {
        string source=File.ReadAllText(Path.Combine(Root,"src/OS/Runtime/GCStaticRootMaterializationProbe.cs"));
        foreach(string symbol in new[]{"M04_SIMPLE_THROW_CATCH_PROBE","M04_SINGLE_FINALLY_CATCH_PROBE","M04_RETHROW_PROBE","M04_NESTED_FINALLY_CATCH_PROBE"})
        {
            var tree=Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(source,
                new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(preprocessorSymbols:["NOSTDLIB_EFI","M04_GC_STATIC_ROOT_MATERIALIZATION_PROBE",symbol]));
            var method=tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>().Single(m=>m.Identifier.Text=="AllocateGcValue");
            var route=method.Body!.Statements[0] as Microsoft.CodeAnalysis.CSharp.Syntax.IfStatementSyntax;
            HostCheck.True(route is not null && route.Condition.ToString()=="s_gcPhase == 5" &&
                route.Statement is Microsoft.CodeAnalysis.CSharp.Syntax.ReturnStatementSyntax returned && returned.Expression?.ToString()=="AllocateCatchException(token)",symbol);
        }
    }

    [HostTest("m04.021.nested-finally.efi measured metadata negative controls")]
    private static void Metadata()
    {
        byte[] table=Convert.FromHexString("083632CA36D2090236E1027D02C087050036E102D10280870500");
        Bytes(table,b=>EfiVerify.TestNestedFinallyEhTable(b,0x587c0,0x58780));
        foreach(ulong bad in new ulong[]{0,0x587c1,0x100000000})
        {
            HostCheck.True(!EfiVerify.TestNestedFinallyEhTable(table,bad,0x58780));
            HostCheck.True(!EfiVerify.TestNestedFinallyEhTable(table,0x587c0,bad));
        }
        foreach(bool root in new[]{false,true})
        {
            byte[] unwind=Convert.FromHexString(root?"0106030006620230015004D0A70500":"01040100044201");
            Bytes(unwind,b=>EfiVerify.TestNestedFinallyUnwindBytes(b,root,0x5a7d0));
            if(root) foreach(ulong bad in new ulong[]{0,0x5a7d1,0x100000000})
                HostCheck.True(!EfiVerify.TestNestedFinallyUnwindBytes(unwind,true,bad));
        }
        static void Bytes(byte[] bytes,Func<byte[]?,bool> admits)
        {
            HostCheck.True(admits(bytes) && !admits(null) && !admits([..bytes,0]));
            for(int i=0;i<bytes.Length;i++)
            {
                HostCheck.True(!admits(bytes[..i]));
                byte[] changed=[..bytes];changed[i]^=1;HostCheck.True(!admits(changed));
            }
        }
    }

    [HostTest("m04.021.nested-finally.efi complete transcript associations")]
    private static void Transcript()
    {
        string[] ancestor=M04GCStaticRootMaterializationEfiIntegrationTests.Lines;
        int gc=Array.FindIndex(ancestor,line=>line.StartsWith("M04_GC_ROOT ",StringComparison.Ordinal)), insert=Array.IndexOf(ancestor,"TEST_START boot.sequence");
        ulong[] image=EfiTranscriptControls.Fields(ancestor[gc]),module=EfiTranscriptControls.Fields(ancestor[gc+1]),stack=EfiTranscriptControls.Fields(ancestor[gc+2]),canaries=EfiTranscriptControls.Fields(ancestor[gc+7]);
        ulong backing=EfiTranscriptControls.Fields(ancestor[gc+4])[0]-32;
        ulong seed=(module[3]^backing)&0xffff, method=image[0]+0x1000, throwRip=method+0x309, rsp=stack[2]-0x1000, caller=rsp+48, local=caller+40;
        ulong obj=backing+0x300,type=image[0]+0x5000;
        string R(int stage,params ulong[] v)=>EfiTranscriptControls.Row("M04_NESTED",stage,v);
        string[] child=["TEST_START m04.021.nested-finally",R(0,module[3],image[0]+0x10000,4000,300),R(1,obj,type,9,3),
            R(2,throwRip,throwRip^1,rsp,0),R(14,(ulong)OS.Kernel.Runtime.BoundedExceptionDispatchStatus.InvalidClause,0,0,0),
            R(3,throwRip,rsp,method+32,caller),R(4,1,0,1,1),R(5,method+101,method+130,method+180,local),
            R(6,obj,local,seed+2,1),R(7,method+101,method+130,local,seed+2),R(8,obj,local,seed+3,2),R(9,method+101,method+130,local,seed+3),
            R(10,obj,seed,seed+3,1),R(11,method+73,caller,caller+64,1),R(12,obj,seed,seed+1,1),R(13,10,rsp-5000,canaries[1],canaries[2]),
            R(15,obj,type,module[3],9),"TEST_PASS m04.021.nested-finally","CLROS_M04_NESTED_FINALLY_READY"];
        string[] lines=[..ancestor[..insert],..child,..ancestor[insert..]];
        string folder=Path.Combine(Root,"artifacts/nested-finally-transcripts",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        void Check(string name,string[] rows,bool pass)
        {
            string path=Path.Combine(folder,name+".log");File.WriteAllText(path,string.Join("\r\n",rows)+"\r\n");
            bool accepted=true;
            try { QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(path,includeGcStatics:true,includeNestedFinally:true); }
            catch(ToolFailure){accepted=false;}
            HostCheck.True(accepted==pass,name);
        }
        Check("valid",lines,true);
        for(int i=1;i<17;i++) for(int field=0;field<4;field++)
        {
            string[] changed=[..lines];int index=insert+i;string[] parts=changed[index].Split(' ');
            parts[field+2]="0x"+(Convert.ToUInt64(parts[field+2][2..],16)^1).ToString("X16");changed[index]=string.Join(' ',parts);
            // Snapshot sizes/count and minimum-stack witnesses admit ranges;
            // identity, order and scalar execution fields remain exact.
            if(i is 1 or 15) continue;
            Check("field-"+i+"-"+field,changed,false);
        }
        Check("truncated",lines[..^1],false);
        Check("duplicate",[..lines[..insert],child[0],..lines[insert..]],false);
        var plan=QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run","--build-efi",..Arguments(Flags)]));
        HostCheck.True(plan.Required.Contains("CLROS_M04_NESTED_FINALLY_READY"));
        QemuTranscripts.ValidateUnselectedRecords(string.Join('\n',lines),plan);
        bool unselected=false;
        try { QemuTranscripts.ValidateUnselectedRecords("CLROS_M04_NESTED_FINALLY_READY",QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run"]))); }
        catch(ToolFailure){unselected=true;}
        HostCheck.True(unselected);
    }

    [HostTest("m04.021.nested-finally.efi fresh native bridge and relocation controls",HostLane.Native,4)]
    private static void Native()
    {
        string name="nested-finally-controls-"+Guid.NewGuid().ToString("N"),artifact=Path.Combine(Root,"artifacts/efi",name+".EFI"),
            log=Path.Combine(Root,"artifacts/nested-finally-native",name+".log");
        using StringWriter output=new();
        int code=EfiBuild.Run(["--EfiOutputPath",artifact,"--LogPath",log,..Arguments(Flags)],output);
        string text=output.ToString();
        HostCheck.True(code==0,text.Length>4000?text[^4000..]:text);
        byte[] original=File.ReadAllBytes(artifact);string evidencePath=artifact+".link.json",originalEvidence=File.ReadAllText(evidencePath),map=File.ReadAllText(artifact+".link.map");
        EfiPeImage image=new(original);
        ulong Address(string symbol)
        {
            var symbols=EfiPeImage.Symbols(map,symbol);HostCheck.True(symbols.Count==1);
            return EfiPeImage.Hex(symbols[0].Groups[3].Value,"fixture address")-image.ImageBase;
        }
        int Offset(string symbol,int length)=>image.Resolve(Address(symbol),(ulong)length,"fixture backing","fixture ambiguity").Offset;
        void Verify(string? expected=null)
        {
            string? failure=null;
            try { EfiVerify.Verify(EfiOptions.Parse(["--EfiArtifact",artifact,..Arguments(Flags)],false)); }
            catch(ToolFailure e){failure=e.Reason;}
            HostCheck.True(failure==expected,"native expected="+expected+" actual="+failure);
        }
        Verify();
        int table=Offset("__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__NestedCatcher",26),method=Offset("OS_OS_Runtime_RhpNewFastAllocation__NestedCatcher",222),bridge=Offset("RhpThrowEx",1624);
        foreach(var (at,reason) in new[]{(table,"nested_finally_clause_table_mismatch"),(table+2,"nested_finally_clause_table_mismatch"),
            (table+13,"nested_finally_clause_table_mismatch"),(method+3,"nested_finally_method_bytes_mismatch"),
            (method+28,"nested_finally_method_call_mismatch"),(bridge+330,"nested_finally_rel32_mismatch")})
        {
            try
            {
                byte[] changed=[..original];changed[at]^=1;File.WriteAllBytes(artifact,changed);
                JsonObject evidence=JsonNode.Parse(originalEvidence)!.AsObject();evidence["artifactSha256"]=ContractAudit.Hash(changed);
                File.WriteAllText(evidencePath,evidence.ToJsonString());Verify(reason);
            }
            finally { File.WriteAllBytes(artifact,original);File.WriteAllText(evidencePath,originalEvidence); }
        }
        string sidecar=artifact+".nested-finally.obj";byte[] objectBytes=File.ReadAllBytes(sidecar);
        try { byte[] bad=[..objectBytes];bad[0]^=1;File.WriteAllBytes(sidecar,bad);Verify("nested_finally_object_identity_mismatch"); }
        finally { File.WriteAllBytes(sidecar,objectBytes); }
        Verify();
        File.WriteAllText(log+".controls","fresh publication; six rehashed PE mutations and one object mutation rejected; original inputs restored and reverified\n");
    }
}
