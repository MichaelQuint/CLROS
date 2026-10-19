using OS.Tooling;

internal static class EfiServiceBindingTests
{
    private const string Prefix="OS_OS_Boot_EfiEntry_M03FirstExitAdapter__";
    private static readonly string[] Web=["LinuxWebObserveCall","LinuxWebObserveOutput","LinuxWebPublishLive"];
    private static readonly string[] Service=["CaptureLinuxServiceSeed","PrepareLinuxServiceRandom","ClearLinuxServiceRandom","LinuxServiceObserveCall","LinuxServiceObserveWorkers"];
    private static string[] Required(string mode)=>mode=="M16014"?[..Web,..Service]:mode=="M10004"?[..Web,"LinuxWebRetainFailure"]:[];
    private static string Row(string name,ulong address=0x180001040,string owner="OS.obj")=>$" 0001:00000040 {Prefix}{name} {address:x16} {owner}\n";
    private static void Verify(string mode,string map)
    {
        Dictionary<string,bool> flags=[];if(mode.Length!=0)flags["Enable"+mode+"Probe"]=true;
        EfiVerify.TestWebServiceBindings(EfiProfile.Create("Diagnostic",flags),map,EfiArtifactVerificationScriptTests.CreateValidPe());
    }
    [HostTest("EFI service requires lifetime and random adapters while original web alone retains its failure stub")]
    private static void Selection()
    {foreach(string mode in new[]{"","M10004","M16014"})Verify(mode,string.Concat(Required(mode).Select(name=>Row(name))));}

    [HostTest("EFI service bindings reject missing duplicate foreign superseded and nonexecutable adapters")]
    private static void Refusals()
    {
        foreach(string mode in new[]{"","M10004","M16014"})
        {
            string map=string.Concat(Required(mode).Select(name=>Row(name)));
            foreach(string name in Required(mode))
            {
                string row=Row(name);Reject(()=>Verify(mode,map.Replace(row,"",StringComparison.Ordinal)));
                Reject(()=>Verify(mode,map+row));Reject(()=>Verify(mode,map.Replace(row,Row(name,owner:"foreign.obj"),StringComparison.Ordinal)));
                foreach(ulong address in new[]{0x180000000UL,0x180004000UL,0x180001300UL,0x180002000UL})
                    Reject(()=>Verify(mode,map.Replace(row,Row(name,address),StringComparison.Ordinal)));
            }
            foreach(string name in Web.Concat(Service).Append("LinuxWebRetainFailure").Except(Required(mode)))Reject(()=>Verify(mode,map+Row(name)));
        }
    }
    private static void Reject(Action action)
    {bool refused=false;try{action();}catch(ToolFailure){refused=true;}HostCheck.True(refused);}
}
