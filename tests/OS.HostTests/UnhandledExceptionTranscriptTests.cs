using OS.Tooling;

internal static class UnhandledExceptionTranscriptTests
{
    internal static readonly string[] Flags=[..RuntimeProfilePortTests.RethrowFlags[..^1],"M04UnhandledException"];
    internal static string[] Arguments=>Flags.Select(n=>"--Enable"+n+"Probe").ToArray();
    private static string Root=>HostToolProcess.FindRepositoryRoot();

    [HostTest("m04.021.unhandled transcript complete prerequisites and associations")]
    private static void Transcript()
    {
        string[] original=M04GCStaticRootMaterializationEfiIntegrationTests.Lines;
        int first=Array.FindIndex(original,l=>l.StartsWith("M04_GC_ROOT ",StringComparison.Ordinal)),insert=Array.IndexOf(original,"TEST_START boot.sequence");
        ulong[] image=EfiTranscriptControls.Fields(original[first]),module=EfiTranscriptControls.Fields(original[first+1]),
            stack=EfiTranscriptControls.Fields(original[first+2]),canaries=EfiTranscriptControls.Fields(original[first+7]);
        ulong backing=EfiTranscriptControls.Fields(original[first+4])[0]-32;
        ulong method=image[0]+0x1000,throwRip=method+9,rsp=stack[2]-0x1000,middle=method+0x100+35,catcher=method+0x200+15;
        string R(int stage,params ulong[] values)=>EfiTranscriptControls.Row("M04_UNHANDLED",stage,values);
        string[] child=["TEST_START m04.021.unhandled",R(0,module[3],image[0]+0x10000,4000,300),R(1,backing+184,image[0]+0x5000,9,3),
            R(2,1,1,0,0),R(3,throwRip,rsp,middle,rsp+48),R(4,middle,rsp+48,catcher,rsp+112),
            R(5,image[0]+0x5100,catcher+14,rsp+104,0),R(6,3,10,0,0),R(7,4,rsp-5000,canaries[1],canaries[2]),
            QemuTranscripts.UnhandledFailure,QemuTranscripts.UnhandledTerminal];
        string[] lines=[..original[..insert],..child];
        HostCheck.True(lines.Length==QemuTranscripts.UnhandledRecordCount);
        string folder=Path.Combine(Root,"artifacts/unhandled-transcripts",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        void Check(string name,string text,bool pass)
        {
            string path=Path.Combine(folder,name+".log");File.WriteAllText(path,text);
            bool accepted=true;
            try{QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(path,includeGcStatics:true,includeUnhandled:true);}
            catch(ToolFailure){accepted=false;}
            HostCheck.True(accepted==pass,name);
            int result=QemuTranscripts.EvaluateM04UnhandledCapture(text,out string prerequisites);
            HostCheck.True((result == 1)==pass && (pass || prerequisites.Length==0),"capture "+name);
        }
        string Text(string[] rows)=>string.Join("\r\n",rows)+"\r\n";
        Check("valid",Text(lines),true);
        for(int stage=2;stage<8;stage++) for(int field=0;field<4;field++)
        {
            if(stage==7 && field==1) continue; // minimum witness admits a bounded interval
            string[] changed=[..lines],tokens=changed[insert+stage+1].Split(' ');
            tokens[field+2]="0x"+(Convert.ToUInt64(tokens[field+2][2..],16)^1).ToString("X16");changed[insert+stage+1]=string.Join(' ',tokens);
            Check("field-"+stage+"-"+field,Text(changed),false);
        }
        for(int i=0;i<lines.Length;i++)
        {
            Check("missing-"+i,Text([..lines[..i],..lines[(i+1)..]]),false);
            Check("duplicate-"+i,Text([..lines[..i],lines[i],..lines[i..]]),false);
        }
        foreach(string ending in new[]{"\r\n","OS_SMOKE_PASS\r\n","TEST_START boot.sequence\r\n","x","\0"}) Check("suffix-"+ending.Length,Text(lines)+ending,false);
        Check("truncated",Text(lines)[..^1],false);
        Check("lf",Text(lines).Replace("\r\n","\n",StringComparison.Ordinal),false);
        string[] alternate=[..lines];alternate[^1]="OS_SMOKE_FAIL subsystem=runtime reason=unhandled-policy-rejected";Check("wrong-failure",Text(alternate),false);
        alternate=[..lines];alternate[^2]="TEST_FAIL m04.021.unhandled 21";Check("numeric-reject",Text(alternate),false);
    }

    [HostTest("m04.021.unhandled profile isolation and original capture")]
    private static void Profile()
    {
        var selected=QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run","--build-efi",..Arguments]));
        HostCheck.True(selected.ReplaceDefaultRequiredMarkers && selected.Required.SequenceEqual(QemuContracts.M04UnhandledRequiredMarkers));
        HostCheck.True(!selected.Required.Any(l=>l.Contains("OS_SMOKE_PASS",StringComparison.Ordinal)) && selected.Forbidden.Contains("OS_SMOKE_FAIL"));
        QemuTranscripts.ValidateUnselectedRecords("TEST_START m04.021.unhandled",selected);
        bool rejected=false;
        try{QemuTranscripts.ValidateUnselectedRecords("M04_UNHANDLED 0",QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run"])));}
        catch(ToolFailure){rejected=true;}
        HostCheck.True(rejected);
        foreach(string[] invalid in new string[][]{["--dry-run",..Arguments],["--dry-run","--build-efi",..Arguments,"--require-marker","x"],
            ["--dry-run","--build-efi",..Arguments,"--EnableM04NestedFinallyCatchProbe"],["--dry-run","--build-efi","--boot-policy","VerifiedExit",..Arguments]})
        {
            rejected=false;try{_=QemuProfiles.Create(QemuSmokeOptions.Parse(invalid));}catch(ToolFailure){rejected=true;}
            HostCheck.True(rejected,"invalid unhandled selection");
        }
    }
}
