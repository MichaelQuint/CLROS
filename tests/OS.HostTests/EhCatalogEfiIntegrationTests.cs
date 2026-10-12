using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static class EhCatalogEfiIntegrationTests
{
    internal static string[] Flags => [.. RuntimeProfilePortTests.RethrowFlags[..^1], "M04FrameDispatch", "M04EhCatalog"];
    internal static string[] Arguments => [.. Flags.Select(n => "--Enable" + n + "Probe")];

    [HostTest("m04.021.eh-catalog profile prerequisite and isolation", HostLane.Fast, 0)]
    private static void Profiles()
    {
        HostCheck.True(!Probes.EnableM04EhCatalogProbe && !BootSequence.IsEhCatalogEnabled(), "default-off host and boot modifier");
        foreach (bool build in new[] {false,true})
        {
            var profile = EfiOptions.Parse(Arguments,build).Profile();
            HostCheck.True(profile.BootPolicy == "Diagnostic" && Flags.All(profile.IsEnabled), "existing physical frame modifier");
            var normal = EfiOptions.Parse([],build).Profile();
            HostCheck.True(!normal.IsEnabled("M04EhCatalog") && normal.BootPolicy == "VerifiedExit", "ordinary exit unchanged");
            foreach (string missing in Flags)
            {
                string[] args = [.. Flags.Where(n => n != missing).Select(n => "--Enable"+n+"Probe")];
                bool failed=false;
                try { _=EfiOptions.Parse(args,build).Profile(); }
                catch (ToolFailure e) { failed=true; if(missing == "M04FrameDispatch") HostCheck.True(e.Reason == "m04_eh_catalog_requires_frame_dispatch",e.Reason); }
                HostCheck.True(failed == (missing != "M04EhCatalog"), "dependency " + missing);
            }
            foreach (string collision in new[] {"M04SimpleThrowCatch","M04SingleFinallyCatch","M04Rethrow","M04NestedFinallyCatch","M04UnhandledException","M04FilteredCatch","M04ConstructorFailure","M04SeededCollectorChurn"})
            {
                bool failed=false;
                try { _=EfiOptions.Parse([..Arguments,"--Enable"+collision+"Probe"],build).Profile(); }
                catch(ToolFailure) { failed=true; }
                HostCheck.True(failed,"collision "+collision);
            }
        }
    }

    [HostTest("m04.021.eh-catalog source and project admission", HostLane.Fast, 1)]
    private static void Admission()
    {
        string root=HostToolProcess.FindRepositoryRoot();
        var admitted=EfiProjectAdmission.InspectStatic(root);
        HostCheck.True(admitted.Succeeded,admitted.Reason+":"+admitted.Detail);
        foreach (string relative in new[]{@"..\OS.Kernel\Runtime\NativeAotEhClauseMetadata.Catalog.cs",@"..\OS.Kernel\Runtime\BoundedExceptionCallerWindow.cs",@"Runtime\ExceptionFrameDispatchProbe.cs"})
        {
            string path=Path.GetFullPath(Path.Combine(root,"src/OS",relative)), source=File.ReadAllText(path);
            HostCheck.True(EfiSourceAdmission.Inspect(source,relative).Succeeded,"exact reviewed source "+relative);
            HostCheck.True(!EfiSourceAdmission.Inspect(source+"\npublic class Unreviewed {}",relative).Succeeded,"source mutation "+relative);
        }
    }

    [HostTest("m04.021.eh-catalog original compiler and linked contribution review", HostLane.Native, 2)]
    private static void Native()
    {
        string root=HostToolProcess.FindRepositoryRoot(), name="eh-catalog-controls-"+Guid.NewGuid().ToString("N"),
            artifact=Path.Combine(root,"artifacts/efi",name+".EFI"), log=Path.Combine(root,"artifacts/eh-catalog-native",name+".log");
        using StringWriter output=new();
        int code=EfiBuild.Run(["--EfiOutputPath",artifact,"--LogPath",log,..Arguments],output);
        string text=output.ToString();
        HostCheck.True(code == 0,text.Length > 5000 ? text[^5000..] : text);
        HostCheck.True(text.Contains("EFI_ARTIFACT_EH_CATALOG methods=2 bytes=48",StringComparison.Ordinal),"linked original catalog review");
        string evidencePath=artifact+".link.json", evidenceText=File.ReadAllText(evidencePath), map=File.ReadAllText(artifact+".link.map");
        byte[] pe=File.ReadAllBytes(artifact); EfiPeImage image=new(pe);
        image.VerifyMapAndRuntime(map,"CLROS.X64.InstructionPublication.obj");
        void Verify(string? expected=null)
        {
            string? failure=null;
            try { EfiVerify.Verify(EfiOptions.Parse(["--EfiArtifact",artifact,..Arguments],false)); }
            catch(ToolFailure error){failure=error.Reason;}
            HostCheck.True(failure==expected,"expected="+expected+" actual="+failure);
        }
        int Offset(string name,int size)
        {
            var symbols=EfiPeImage.Symbols(map,name); HostCheck.True(symbols.Count==1,"unique original symbol");
            ulong address=EfiPeImage.Hex(symbols[0].Groups[3].Value,"control symbol");
            return image.Resolve(address-image.ImageBase,(ulong)size,"control backing","control ambiguity").Offset;
        }
        Verify();
        int data=Offset("CLROS_Eh_Contributions",48), getters=Offset("CLROS_Eh_CatalogStart",16);
        List<(int At,string Reason)> controls=[..new[]{0,4,8,12,16,20,24,28,32,36,40,44}.Select(at=>(data+at,"eh_catalog_linked_payload_mismatch")),
            (getters,"eh_catalog_getter_code_mismatch"),(getters+7,"eh_catalog_getter_code_mismatch"),
            (getters+3,"eh_catalog_start_rel32"),(getters+11,"eh_catalog_end_rel32")];
        foreach(var (at,reason) in controls)
        {
            try
            {
                byte[] changed=[..pe]; changed[at]^=1; File.WriteAllBytes(artifact,changed);
                var evidence=System.Text.Json.Nodes.JsonNode.Parse(evidenceText)!.AsObject(); evidence["artifactSha256"]=ContractAudit.Hash(changed);
                File.WriteAllText(evidencePath,evidence.ToJsonString()); Verify(reason);
            }
            finally {File.WriteAllBytes(artifact,pe); File.WriteAllText(evidencePath,evidenceText);}
        }
        var originalEvidence=System.Text.Json.Nodes.JsonNode.Parse(evidenceText)!;
        foreach(string prefix in new[]{"ehCatalog","ehCatalogIlc","ehCatalogMap"})
        {
            string sidecar=originalEvidence[prefix+"ObjectPath"]!.GetValue<string>(); byte[] bytes=File.ReadAllBytes(sidecar);
            try {byte[] changed=[..bytes]; changed[0]^=1; File.WriteAllBytes(sidecar,changed); Verify("eh_catalog_input_identity");}
            finally {File.WriteAllBytes(sidecar,bytes);}
        }
        foreach(string prefix in new[]{"ehCatalog","ehCatalogIlc"})
        {
            string sidecar=originalEvidence[prefix+"ObjectPath"]!.GetValue<string>(), linked=originalEvidence[prefix+"LinkedObjectPath"]!.GetValue<string>();
            byte[] bytes=File.ReadAllBytes(sidecar);
            // For original ILC, mutate the exact FrameCatcher code contribution while retaining valid COFF/map/pdata geometry.
            int at=prefix=="ehCatalog" ? 0 : OriginalCodeOffset(bytes,"OS_OS_Runtime_RhpNewFastAllocation__FrameCatcher");
            try
            {
                byte[] changed=[..bytes]; changed[at]^=1; File.WriteAllBytes(sidecar,changed); File.WriteAllBytes(linked,changed);
                var evidence=System.Text.Json.Nodes.JsonNode.Parse(evidenceText)!.AsObject(); evidence[prefix+"ObjectSha256"]=ContractAudit.Hash(changed);
                File.WriteAllText(evidencePath,evidence.ToJsonString());
                Verify(prefix=="ehCatalog" ? "eh_catalog_recomputed_object_mismatch" : "eh_catalog_original_code_join");
            }
            finally {File.WriteAllBytes(sidecar,bytes); File.WriteAllBytes(linked,bytes); File.WriteAllText(evidencePath,evidenceText);}
        }
        Verify(); File.WriteAllText(log+".controls","16 rehashed linked payload/getter controls, three sidecar mutations, paired/rehashed catalog and original-code controls rejected; originals restored and reverified. This does not prove guest execution.\n");
    }

    private static int OriginalCodeOffset(byte[] bytes,string name)
    {
        uint U32(int at)=>System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(at,4));
        int symbols=checked((int)U32(8)),count=checked((int)U32(12));
        for(int i=0;i<count;)
        {
            int at=symbols+i*18; string symbol;
            if(U32(at)!=0) symbol=System.Text.Encoding.ASCII.GetString(bytes,at,8).TrimEnd('\0');
            else {int start=checked(symbols+count*18+(int)U32(at+4)),end=start; while(bytes[end]!=0)end++; symbol=System.Text.Encoding.ASCII.GetString(bytes,start,end-start);}
            if(symbol==name)
            {
                short section=System.Buffers.Binary.BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(at+12,2));
                HostCheck.True(section>0,"defined original method"); return checked((int)(U32(20+(section-1)*40+20)+U32(at+8)));
            }
            i+=1+bytes[at+17];
        }
        throw new InvalidDataException("original code symbol");
    }
}
