using OS.Tooling;
using OS.Kernel.Runtime;
using OS.Tools.QemuRunner;

internal static class ConstructorFailureTranscriptTests
{
    internal static string[] Arguments => [..RuntimeProfilePortTests.RethrowFlags[..^1].Select(n=>"--Enable"+n+"Probe"),
        "--EnableM04ConstructorFailureProbe"];

    [HostTest("m04.021.ctor-failure transcript physical frames and retained wrapper")]
    private static void Transcript()
    {
        string[] original=M04GCStaticRootMaterializationEfiIntegrationTests.Lines;
        int first=Array.FindIndex(original,l=>l.StartsWith("M04_GC_ROOT ",StringComparison.Ordinal)),insert=Array.IndexOf(original,"TEST_START boot.sequence");
        ulong[] image=EfiTranscriptControls.Fields(original[first]),module=EfiTranscriptControls.Fields(original[first+1]),
            stack=EfiTranscriptControls.Fields(original[first+2]),canaries=EfiTranscriptControls.Fields(original[first+7]);
        ulong backing=EfiTranscriptControls.Fields(original[first+4])[0]-32,seed=(module[3]^backing)&0xffff;
        ulong obj=backing+184,wrapper=obj+24,type=image[0]+0x5000,baseType=type+0x100,wrapperType=type+0x200,
            context=image[0]+0x8000,constructor=image[0]+0x1000,invoke=constructor+0x100,outer=constructor+0x200;
        ulong rsp=stack[2]-0x1000,outerRsp=rsp+416;
        string R(int stage,params ulong[] values)=>EfiTranscriptControls.Row("M04_CTOR_FAILURE",stage,values);
        string[] child=["TEST_START m04.021.ctor-failure",R(0,module[3],image[0]+0x10000,4000,300),R(1,obj,type,9,3),R(2,1,1,0,0),
            R(3,context,constructor,0,0xffffffff),R(4,context,constructor,1,seed),R(5,constructor+54,rsp,invoke+28,rsp+64),
            R(6,outer+16,outerRsp,outerRsp+48,context),R(7,invoke+35,outer+40,baseType,wrapperType),R(8,obj,context,1,1),
            R(9,0,context,constructor,1),R(10,wrapper,obj,context,4),R(11,wrapper,1,1,1),R(12,invoke+52,rsp-896,outer+40,outerRsp),
            R(13,wrapper,obj,context,1),R(14,outer+20,outerRsp,outerRsp+48,1),R(15,wrapper,obj,seed+1,1),
            R(16,(ulong)CompilerClassConstructorStatus.InitializationFailed,constructor,1,0),R(17,7,rsp-12000,canaries[1],canaries[2]),
            R(18,obj,wrapper,9,10),"TEST_PASS m04.021.ctor-failure","CLROS_M04_021_CTOR_FAILURE_READY"];
        string[] lines=[..original[..insert],..child,..original[insert..]];
        HostCheck.True(lines.Length==original.Length+22,"whole inherited success transcript");
        string folder=Path.Combine(HostToolProcess.FindRepositoryRoot(),"artifacts/ctor-failure-transcripts",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string Text(string[] rows)=>string.Join("\r\n",rows)+"\r\n";
        void Check(string name,string text,bool pass)
        {
            string path=Path.Combine(folder,name+".log");File.WriteAllText(path,text);
            bool accepted=true;
            try{QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(path,includeGcStatics:true,includeConstructorFailure:true);}
            catch(ToolFailure){accepted=false;}
            HostCheck.True(accepted==pass,name);
        }
        Check("valid",Text(lines),true);
        var plan=QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run","--build-efi",..Arguments]));
        var state=new SerialMarkerStateMachine(plan.Required,plan.Forbidden,orderedMarkers:plan.Ordered,uniqueMarkers:plan.Unique);
        HostCheck.True(state.Feed(System.Text.Encoding.ASCII.GetBytes(Text(lines))).Succeeded&&state.Complete(false).Passed);
        for(int stage=1;stage<=18;stage++)for(int field=0;field<4;field++)
        {
            string[] changed=[..lines],tokens=changed[insert+stage+1].Split(' ');
            tokens[field+2]="0x"+(Convert.ToUInt64(tokens[field+2][2..],16)^1).ToString("X16");changed[insert+stage+1]=string.Join(' ',tokens);
            Check("field-"+stage+"-"+field,Text(changed),false);
        }
        foreach((int stage,int field,ulong value) in new[]{(0,0,0UL),(0,1,ulong.MaxValue),(0,2,0UL),(0,3,0UL),
            (3,0,ulong.MaxValue),(5,1,0UL),(6,1,ulong.MaxValue),(12,1,ulong.MaxValue),(17,1,0UL)})
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
        Check("control",Text(lines).Replace("M04_CTOR_FAILURE 10","M04_CTOR_FAILURE\0 10",StringComparison.Ordinal),false);
    }

    [HostTest("m04.021.ctor-failure profile original capture and record isolation")]
    private static void Profile()
    {
        var plan=QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run","--build-efi",..Arguments]));
        HostCheck.True(plan.ReplaceDefaultRequiredMarkers&&plan.Required.SequenceEqual(QemuContracts.M04ConstructorFailureRequiredMarkers));
        HostCheck.True(plan.Required[^1]=="OS_SMOKE_PASS"&&plan.Forbidden.Contains("OS_SMOKE_FAIL"));
        QemuTranscripts.ValidateUnselectedRecords("TEST_START m04.021.ctor-failure",plan);
        bool rejected=false;try{QemuTranscripts.ValidateUnselectedRecords("M04_CTOR_FAILURE 0",QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run"])));}
        catch(ToolFailure){rejected=true;}HostCheck.True(rejected);
        foreach(string[] args in new string[][]{["--dry-run",..Arguments],["--dry-run","--build-efi","--skip-image-build",..Arguments],
            ["--dry-run","--build-efi",..Arguments,"--require-marker","x"],["--dry-run","--build-efi",..Arguments,"--EnableM04UnhandledExceptionProbe"],
            ["--dry-run","--build-efi","--boot-policy","VerifiedExit",..Arguments]})
        {
            rejected=false;try{_=QemuProfiles.Create(QemuSmokeOptions.Parse(args));}catch(ToolFailure){rejected=true;}
            HostCheck.True(rejected,"invalid constructor-failure selection");
        }
    }
}
