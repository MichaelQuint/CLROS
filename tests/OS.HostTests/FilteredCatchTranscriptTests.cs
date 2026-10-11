using OS.Tooling;
using OS.Tools.QemuRunner;

internal static class FilteredCatchTranscriptTests
{
    internal static string[] Arguments => [..RuntimeProfilePortTests.RethrowFlags[..^1].Select(n=>"--Enable"+n+"Probe"),
        "--EnableM04FilteredCatchProbe"];

    [HostTest("m04.021.filters transcript ordered actual returns and shared parent local")]
    private static void Transcript()
    {
        string[] original=M04GCStaticRootMaterializationEfiIntegrationTests.Lines;
        int first=Array.FindIndex(original,l=>l.StartsWith("M04_GC_ROOT ",StringComparison.Ordinal)),insert=Array.IndexOf(original,"TEST_START boot.sequence");
        ulong[] image=EfiTranscriptControls.Fields(original[first]),module=EfiTranscriptControls.Fields(original[first+1]),
            stack=EfiTranscriptControls.Fields(original[first+2]),canaries=EfiTranscriptControls.Fields(original[first+7]);
        ulong backing=EfiTranscriptControls.Fields(original[first+4])[0]-32,seed=(module[3]^backing)&0xffff;
        ulong obj=backing+184,type=image[0]+0x5000,thrower=image[0]+0x1000,catcher=thrower+0x100,
            rsp=stack[2]-0x1000,parentRsp=rsp+48,parentRbp=parentRsp+64,local=parentRbp-8;
        string R(int stage,params ulong[] values)=>EfiTranscriptControls.Row("M04_FILTERS",stage,values);
        string[] child=["TEST_START m04.021.filters",R(0,module[3],image[0]+0x10000,4000,300),R(1,obj,type,9,3),
            R(2,thrower+9,(thrower+9)^1,rsp,0),R(3,thrower+9,rsp,catcher+35,parentRsp),
            R(4,catcher+60,catcher+178,catcher+127,catcher+245),R(5,obj,local,seed+1,0),R(6,catcher+60,0,1,parentRsp),
            R(7,obj,local,seed+2,1),R(8,catcher+178,1,2,parentRsp),R(9,catcher+245,1,parentRbp,local),R(10,obj,seed+2,2,1),
            R(11,catcher+36,parentRsp,parentRbp,1),R(12,obj,seed,seed+1,1),R(13,15,rsp-12000,canaries[1],canaries[2]),
            R(14,obj,type,2,1),"TEST_PASS m04.021.filters","CLROS_M04_021_FILTERS_READY"];
        string[] lines=[..original[..insert],..child,..original[insert..]];
        HostCheck.True(lines.Length==88,"whole inherited success transcript");
        string folder=Path.Combine(HostToolProcess.FindRepositoryRoot(),"artifacts/filter-transcripts",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string Text(string[] rows)=>string.Join("\r\n",rows)+"\r\n";
        void Check(string name,string text,bool pass)
        {
            string path=Path.Combine(folder,name+".log");File.WriteAllText(path,text);
            bool accepted=true;
            try{QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(path,includeGcStatics:true,includeFilteredCatch:true);}
            catch(ToolFailure){accepted=false;}
            HostCheck.True(accepted==pass,name);
        }
        Check("valid",Text(lines),true);
        var plan=QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run","--build-efi",..Arguments]));
        var state=new SerialMarkerStateMachine(plan.Required,plan.Forbidden,orderedMarkers:plan.Ordered,uniqueMarkers:plan.Unique);
        HostCheck.True(state.Feed(System.Text.Encoding.ASCII.GetBytes(Text(lines))).Succeeded&&state.Complete(false).Passed);
        for(int stage=1;stage<=14;stage++)for(int field=0;field<4;field++)
        {
            string[] changed=[..lines],tokens=changed[insert+stage+1].Split(' ');
            tokens[field+2]="0x"+(Convert.ToUInt64(tokens[field+2][2..],16)^1).ToString("X16");changed[insert+stage+1]=string.Join(' ',tokens);
            Check("field-"+stage+"-"+field,Text(changed),false);
        }
        foreach((int stage,int field,ulong value) in new[]{(0,0,0UL),(0,1,ulong.MaxValue),(0,2,0UL),(0,3,0UL),
            (2,2,0UL),(3,2,ulong.MaxValue),(9,2,ulong.MaxValue),(13,1,0UL)})
        {
            string[] changed=[..lines],tokens=changed[insert+stage+1].Split(' ');tokens[field+2]="0x"+value.ToString("X16");
            changed[insert+stage+1]=string.Join(' ',tokens);Check("range-"+stage+"-"+field,Text(changed),false);
        }
        for(int i=0;i<lines.Length;i++)
        {
            Check("missing-"+i,Text([..lines[..i],..lines[(i+1)..]]),false);
            Check("duplicate-"+i,Text([..lines[..i],lines[i],..lines[i..]]),false);
        }
        foreach(string ending in new[]{"\r\n","OS_SMOKE_PASS\r\n","TEST_START boot.sequence\r\n","x","\0"})Check("suffix-"+ending.Length,Text(lines)+ending,false);
        Check("truncated",Text(lines)[..^1],false);Check("lf",Text(lines).Replace("\r\n","\n",StringComparison.Ordinal),false);
        Check("control",Text(lines).Replace("M04_FILTERS 10","M04_FILTERS\0 10",StringComparison.Ordinal),false);
        string[] reversed=[..lines];(reversed[insert+6],reversed[insert+8])=(reversed[insert+8],reversed[insert+6]);Check("reversed-filters",Text(reversed),false);
        foreach(bool collision in new[]{false,true})
        {
            bool rejected=false;try{QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(Path.Combine(folder,"valid.log"),
                includeGcStatics:collision,includeConstructorFailure:collision,includeFilteredCatch:true);}catch(ToolFailure){rejected=true;}
            HostCheck.True(rejected,"prerequisite/collision");
        }
    }

    [HostTest("m04.021.filters profile original capture and record isolation")]
    private static void Profile()
    {
        var plan=QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run","--build-efi",..Arguments]));
        HostCheck.True(plan.ReplaceDefaultRequiredMarkers&&plan.Required.SequenceEqual(QemuContracts.M04FilteredCatchRequiredMarkers));
        HostCheck.True(plan.Required[^1]=="OS_SMOKE_PASS"&&plan.Forbidden.Contains("OS_SMOKE_FAIL"));
        QemuTranscripts.ValidateUnselectedRecords("TEST_START m04.021.filters",plan);
        bool rejected=false;try{QemuTranscripts.ValidateUnselectedRecords("M04_FILTERS 0",QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run"])));}
        catch(ToolFailure){rejected=true;}HostCheck.True(rejected);
        foreach(string[] args in new string[][]{["--dry-run",..Arguments],["--dry-run","--build-efi","--skip-image-build",..Arguments],
            ["--dry-run","--build-efi",..Arguments,"--require-marker","x"],["--dry-run","--build-efi",..Arguments,"--EnableM04ConstructorFailureProbe"],
            ["--dry-run","--build-efi","--boot-policy","VerifiedExit",..Arguments]})
        {
            rejected=false;try{_=QemuProfiles.Create(QemuSmokeOptions.Parse(args));}catch(ToolFailure){rejected=true;}
            HostCheck.True(rejected,"invalid filter selection");
        }
    }
}
