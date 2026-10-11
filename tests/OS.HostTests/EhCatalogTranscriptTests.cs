using OS.Tooling;
using OS.Tools.QemuRunner;
using OS.Kernel.Runtime;

internal static class EhCatalogTranscriptTests
{
    [HostTest("m04.021.eh-catalog transcript original frame association and hostile records")]
    private static void Transcript()
    {
        string[] original=M04GCStaticRootMaterializationEfiIntegrationTests.Lines;
        int first=Array.FindIndex(original,l=>l.StartsWith("M04_GC_ROOT ",StringComparison.Ordinal)), insert=Array.IndexOf(original,"TEST_START boot.sequence");
        ulong[] image=EfiTranscriptControls.Fields(original[first]), module=EfiTranscriptControls.Fields(original[first+1]),
            stack=EfiTranscriptControls.Fields(original[first+2]), canaries=EfiTranscriptControls.Fields(original[first+7]);
        ulong backing=EfiTranscriptControls.Fields(original[first+4])[0]-32, seed=(module[3]^backing)&0xffff;
        ulong obj=backing+184,type=image[0]+0x5000,method=image[0]+0x1000,middle=method+0x100,catcher=method+0x200;
        ulong rsp=stack[2]-0x1000,middleRsp=rsp+48,catchRsp=rsp+112;
        string R(int stage,params ulong[] values)=>EfiTranscriptControls.Row("M04_FRAMES",stage,values);
        string C(int stage,params ulong[] values)=>EfiTranscriptControls.Row("M04_EH_CATALOG",stage,values);
        string[] child=["TEST_START m04.021.frames",R(0,module[3],image[0]+0x10000,4000,300),R(1,obj,type,9,3),R(2,1,1,0,0),
            R(3,method+9,rsp,middle+25,middleRsp),R(4,middle+25,middleRsp,catcher+46,catchRsp),
            C(0,image[0]+0x6000,image[0]+0x6030,2,module[3]),C(1,middle,72,image[0]+0x6100,4),C(2,catcher,138,image[0]+0x6200,15),
            R(5,(ulong)BoundedExceptionFrameSearchStatus.InvalidFrame,(ulong)BoundedExceptionFrameSearchStatus.DispatchRejected,
                (ulong)BoundedExceptionDispatchStatus.InvalidClause,0),R(6,1,0,1,1),R(7,middle+49,catcher+96,middleRsp+40,catchRsp+40),
            R(8,obj,middleRsp+40,seed+4,1),R(9,middle+49,middleRsp,catchRsp,obj),R(10,obj,seed,seed+3,1),
            R(11,catcher+47,catchRsp,catchRsp+64,1),R(12,obj,seed+1,seed+3,1),R(13,10,rsp-15456,canaries[1],canaries[2]),
            R(14,obj,type,module[3],9),"TEST_PASS m04.021.frames","CLROS_M04_021_FRAMES_READY","CLROS_M04_EH_CATALOG_READY"];
        string[] lines=[..original[..insert],..child,..original[insert..]];
        string folder=Path.Combine(HostToolProcess.FindRepositoryRoot(),"artifacts/eh-catalog-transcripts",Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        string Text(string[] rows)=>string.Join("\r\n",rows)+"\r\n";
        void Check(string name,string text,bool pass,bool catalog=true)
        {
            string path=Path.Combine(folder,name+".log"); File.WriteAllText(path,text);
            bool accepted=true;
            try {QemuTranscripts.AssertM04ManagedHeapPageRangeTranscript(path,includeGcStatics:true,includeFrameDispatch:true,includeEhCatalog:catalog);}
            catch(ToolFailure){accepted=false;}
            HostCheck.True(accepted==pass,name);
        }
        Check("valid",Text(lines),true); Check("legacy-rejects-catalog",Text(lines),false,false);
        var plan=QemuProfiles.Create(QemuSmokeOptions.Parse(["--dry-run","--build-efi",..EhCatalogEfiIntegrationTests.Arguments]));
        var state=new SerialMarkerStateMachine(plan.Required,plan.Forbidden,orderedMarkers:plan.Ordered,uniqueMarkers:plan.Unique);
        HostCheck.True(state.Feed(System.Text.Encoding.ASCII.GetBytes(Text(lines))).Succeeded && state.Complete(false).Passed,"ordered full profile");
        for(int at=0;at<lines.Length;at++)
        {
            Check("missing-"+at,Text([..lines[..at],..lines[(at+1)..]]),false);
            Check("duplicate-"+at,Text([..lines[..at],lines[at],..lines[at..]]),false);
        }
        for(int stage=0;stage<3;stage++) for(int field=0;field<4;field++)
        {
            // Table RVAs can vary within admitted ranges; use an out-of-image hostile value for that field.
            string[] changed=[..lines]; string[] tokens=changed[insert+6+stage].Split(' ');
            ulong old=Convert.ToUInt64(tokens[field+2][2..],16), value=stage>0 && field==2 ? ulong.MaxValue : old^1;
            tokens[field+2]="0x"+value.ToString("X16"); changed[insert+6+stage]=string.Join(' ',tokens);
            Check("field-"+stage+"-"+field,Text(changed),false);
        }
        string[] moved=[..lines]; (moved[insert+6],moved[insert+9])=(moved[insert+9],moved[insert+6]); Check("reordered",Text(moved),false);
        foreach(string ending in new[]{"\r\n","OS_SMOKE_PASS\r\n","TEST_FAIL catalog\r\n","x","\0"}) Check("suffix-"+ending.Length,Text(lines)+ending,false);
        Check("truncated",Text(lines)[..^1],false); Check("lf",Text(lines).Replace("\r\n","\n",StringComparison.Ordinal),false);
    }
}
