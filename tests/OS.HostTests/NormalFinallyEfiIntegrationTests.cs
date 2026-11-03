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
        string root = HostToolProcess.FindRepositoryRoot();
        var project = EfiProjectAdmission.InspectStatic(root);
        HostCheck.True(project.Succeeded, project.Reason + ":" + project.Detail);
        var catalog = HostSourceCatalog.Efi(EfiOptions.Parse(Arguments, true).Profile());
        const string owner = "OS.Runtime.RhpNewFastAllocation";
        const string reason = "reviewed_normal_finally_source_identity";
        string Relative(CSharpSourceDocument document) =>
            Path.GetRelativePath(Path.Combine(root, "src", "OS"), document.Path).Replace('/', '\\');
        var compiled = catalog.Documents.ToDictionary(Relative, StringComparer.Ordinal);
        var family = EfiContracts.SourceAdmissionReasons.Where(pair => pair.Value == reason).ToArray();
        HostCheck.True(family.Length != 0, "reviewed normal-finally source family exists");
        foreach (var member in family)
        {
            HostCheck.True(compiled.TryGetValue(member.Key, out var document), "reviewed family member is compiled");
            HostCheck.True(EfiSourceAdmission.Inspect(document!.Text, member.Key).Succeeded, "exact reviewed fixture member");
            HostCheck.True(document.Text.StartsWith("#if NOSTDLIB_EFI && M04_NORMAL_FINALLY_PROBE", StringComparison.Ordinal),
                "dedicated opt-in fixture, no ancestor anchoring");
        }

        Microsoft.CodeAnalysis.SyntaxNode Method(string name) => catalog.Method(owner, name);
        var authority = catalog.Type(owner).GetMembers("NormalTransferAdmitted").Single();
        var field = authority.DeclaringSyntaxReferences.Single().GetSyntax();
        foreach (var (member, from, to) in new (Microsoft.CodeAnalysis.SyntaxNode, string, string)[]
        {
            (Method("NormalCatcher"), "(MethodImplOptions)0x40", "(MethodImplOptions)0"),
            (Method("NormalCatcher"), "NormalThrower(value)", "NormalBody(value)"),
            (Method("NormalFinallyEntered"), "stamp = value.Payload + 2", "stamp = value.Payload + 3"),
            (Method("NormalCompilerAddress"), "\"CLROS_NormalFinally_CompilerAddress\"", "\"CLROS_Unreviewed_CompilerAddress\""),
            (field, "true", "false"),
            (Method("NormalSelect"), "recovered.ExitedClauseIndex != 0", "recovered.ExitedClauseIndex != 1"),
            (Method("NormalContextIntact"), "actual->GetInteger(reg) != s_ehCaller.GetInteger(reg)", "false"),
            (Method("NormalContextIntact"), "a.Low != b.Low || a.High != b.High", "false")
        })
        {
            var document = catalog.Document(member);
            string relative = Relative(document);
            HostCheck.True(family.Any(pair => pair.Key == relative), "independent semantic anchor belongs to reviewed family");
            var expected = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseExpression(from);
            var matches = member.DescendantNodesAndSelf().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ExpressionSyntax>()
                .Where(expression => Microsoft.CodeAnalysis.CSharp.SyntaxFactory.AreEquivalent(expression, expected)).ToArray();
            HostCheck.True(matches.Length == 1, "control has one active syntax anchor: " + from);
            var span = matches[0].Span;
            string changed = document.Text.Remove(span.Start, span.Length).Insert(span.Start, to);
            HostCheck.True(changed != document.Text, "control changes source");
            var rejected = EfiSourceAdmission.Inspect(changed, relative);
            HostCheck.True(!rejected.Succeeded && rejected.Reason == "forbidden_active_source"
                && rejected.Detail.EndsWith(":" + reason, StringComparison.Ordinal), "fixture control " + from + ":" + rejected.Reason);
        }
    }

    [HostTest("m04.021.normal-finally profile dependencies and reviewed guest plan", HostLane.Fast, 1)]
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
        var plan=QemuProfiles.Create(options);
        HostCheck.True(plan.Required.SequenceEqual(QemuContracts.M04NormalFinallyRequiredMarkers)&&
            plan.Required[^1]=="OS_SMOKE_PASS"&&plan.Forbidden.Contains("OS_SMOKE_FAIL"),"exact reviewed guest marker envelope; no execution proof");
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

    [HostTest("m04.021.normal-finally original native identity and ABI mutations", HostLane.Native, 3)]
    private static void NativeControls()
    {
        string root=HostToolProcess.FindRepositoryRoot(),name="normal-finally-controls-"+Guid.NewGuid().ToString("N"),
            artifact=Path.Combine(root,"artifacts/efi",name+".EFI"),log=Path.Combine(root,"artifacts/normal-finally-native",name+".log");
        using StringWriter output=new();
        int exit=EfiBuild.Run(["--EfiOutputPath",artifact,"--LogPath",log,..Arguments],output);
        string text=output.ToString();
        HostCheck.True(exit==0&&text.Contains("EFI_ARTIFACT_NORMAL_FINALLY_THUNK",StringComparison.Ordinal)&&text.Contains("TEST_PASS efi.artifact.verify",StringComparison.Ordinal),
            text.Length>3000?text[^3000..]:text);
        string evidencePath=artifact+".link.json",evidenceText=File.ReadAllText(evidencePath),map=File.ReadAllText(artifact+".link.map");
        byte[] pe=File.ReadAllBytes(artifact);var image=new EfiPeImage(pe);image.VerifyMapAndRuntime(map,"CLROS.X64.InstructionPublication.obj");
        var originalEvidence=System.Text.Json.Nodes.JsonNode.Parse(evidenceText)!;
        int controls=0;
        void Verify(string? expected)
        {
            string? actual=null;
            try {EfiVerify.Verify(EfiOptions.Parse(["--EfiArtifact",artifact,..Arguments],false));}
            catch(ToolFailure failure){actual=failure.Reason;}
            HostCheck.True(actual==expected,"expected="+expected+" actual="+actual);controls++;
        }
        ulong Address(string symbol)
        {var found=EfiPeImage.Symbols(map,symbol);HostCheck.True(found.Count==1,"unique original symbol");return EfiPeImage.Hex(found[0].Groups[3].Value,"original address");}
        int Offset(string symbol,int size)=>image.Resolve(Address(symbol)-image.ImageBase,(uint)size,"native control backing","native control alias").Offset;
        void PeMutation(int at,string reason)
        {
            try
            {
                byte[] bad=[..pe];bad[at]^=1;File.WriteAllBytes(artifact,bad);
                var evidence=System.Text.Json.Nodes.JsonNode.Parse(evidenceText)!.AsObject();evidence["artifactSha256"]=ContractAudit.Hash(bad);
                File.WriteAllText(evidencePath,evidence.ToJsonString());Verify(reason);
            }
            finally {File.WriteAllBytes(artifact,pe);File.WriteAllText(evidencePath,evidenceText);}
        }
        Verify(null);
        int catalog=Offset("CLROS_Eh_Contributions",32),getters=Offset("CLROS_Eh_CatalogStart",16),bridge=Offset("RhpThrowEx",1032),
            catcher=Offset("OS_OS_Runtime_RhpNewFastAllocation__NormalCatcher",173),thrower=Offset("OS_OS_Runtime_RhpNewFastAllocation__NormalThrower",10),
            table=Offset("__ehinfo_OS_OS_Runtime_RhpNewFastAllocation__NormalCatcher",11),predicate=Offset("RhTypeCast_IsInstanceOfException",5);
        foreach(int at in new[]{0,4,8,12,16,20,24,28})PeMutation(catalog+at,"eh_catalog_linked_payload_mismatch");
        PeMutation(getters,"eh_catalog_getter_code_mismatch");PeMutation(getters+3,"eh_catalog_start_rel32");PeMutation(getters+11,"eh_catalog_end_rel32");
        foreach(int at in new[]{0,3,47,56,57,62,89,110,121,161,164,172})PeMutation(catcher+at,"eh_catalog_original_code_join");
        foreach(int at in new[]{0,3,4,9})PeMutation(thrower+at,"normal_finally_method_bytes_mismatch");
        PeMutation(thrower+5,"normal_finally_method_call_mismatch");
        foreach(int at in new[]{0,2,3,6,7,10})PeMutation(table+at,"eh_catalog_original_eh_join");
        foreach(int at in new[]{0,3,32,528,768,776,808,1016})PeMutation(bridge+at,"normal_finally_text_mismatch");
        PeMutation(bridge+330,"normal_finally_selector_relocation");PeMutation(bridge+811,"normal_finally_continuation_relocation");PeMutation(bridge+1027,"normal_finally_table_relocation");
        PeMutation(predicate,"normal_finally_predicate_export_shape");PeMutation(predicate+1,"normal_finally_predicate_export_call");
        ulong rootRva=Address("OS_OS_Runtime_RhpNewFastAllocation__NormalCatcher")-image.ImageBase;
        foreach(var row in image.RuntimeFunctions.Where(f=>f.Begin>=rootRva&&f.Begin<rootRva+173))
            PeMutation(row.UnwindOffset+5,"normal_finally_unwind_mismatch");
        foreach(string prefix in new[]{"normalFinally","ehCatalog","ehCatalogIlc","ehCatalogMap"})
        {
            string path=originalEvidence[prefix+"ObjectPath"]!.GetValue<string>();byte[] saved=File.ReadAllBytes(path);
            try {byte[] bad=[..saved];bad[0]^=1;File.WriteAllBytes(path,bad);Verify(prefix=="normalFinally"?"normal_finally_object_identity_mismatch":"eh_catalog_input_identity");}
            finally {File.WriteAllBytes(path,saved);}
        }
        // Alter both the original code and PE consistently while preserving
        // source and valid method extents. The ABI fingerprint must still reject.
        string original=originalEvidence["ehCatalogIlcObjectPath"]!.GetValue<string>(),linked=originalEvidence["ehCatalogIlcLinkedObjectPath"]!.GetValue<string>();
        byte[] coff=File.ReadAllBytes(original);int originalAt=OriginalCodeOffset(coff,"OS_OS_Runtime_RhpNewFastAllocation__NormalCatcher");
        foreach(int at in new[]{3,57,62,89,164})
        {
            try
            {
                byte[] badCoff=[..coff],badPe=[..pe];badCoff[originalAt+at]^=1;badPe[catcher+at]^=1;
                File.WriteAllBytes(original,badCoff);File.WriteAllBytes(linked,badCoff);File.WriteAllBytes(artifact,badPe);
                var evidence=System.Text.Json.Nodes.JsonNode.Parse(evidenceText)!.AsObject();evidence["ehCatalogIlcObjectSha256"]=ContractAudit.Hash(badCoff);evidence["artifactSha256"]=ContractAudit.Hash(badPe);
                File.WriteAllText(evidencePath,evidence.ToJsonString());Verify("normal_finally_method_bytes_mismatch");
            }
            finally {File.WriteAllBytes(original,coff);File.WriteAllBytes(linked,coff);File.WriteAllBytes(artifact,pe);File.WriteAllText(evidencePath,evidenceText);}
        }
        Verify(null);
        File.WriteAllText(log+".controls","native rejection checks="+controls+"; exact original bytes restored and reverified; no guest proof.\n");
        Console.WriteLine("NORMAL_FINALLY_NATIVE_CONTROLS checks="+controls+" guestAccepted=false");
    }

    private static int OriginalCodeOffset(byte[] bytes,string name)
    {
        uint U32(int at)=>BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at,4));
        int symbols=checked((int)U32(8)),count=checked((int)U32(12));
        for(int i=0;i<count;)
        {
            int at=symbols+i*18;string symbol;
            if(U32(at)!=0)symbol=System.Text.Encoding.ASCII.GetString(bytes,at,8).TrimEnd('\0');
            else {int start=checked(symbols+count*18+(int)U32(at+4)),end=start;while(bytes[end]!=0)end++;symbol=System.Text.Encoding.ASCII.GetString(bytes,start,end-start);}
            if(symbol==name)
            {
                short section=BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(at+12,2));HostCheck.True(section>0,"defined original method");
                return checked((int)(U32(20+(section-1)*40+20)+U32(at+8)));
            }
            i+=1+bytes[at+17];
        }
        throw new InvalidDataException("original code symbol");
    }
}
