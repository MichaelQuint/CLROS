using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Linq;
using OS.Kernel.Diagnostics;
using OS.Kernel.Runtime;
using OS.Tools.QemuRunner;

internal static class M04SimpleThrowCatchEfiIntegrationTests
{
    private const string Flag="EnableM04SimpleThrowCatchProbe";
    private static string Root=>HostToolProcess.FindRepositoryRoot();
    private static void Need(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}

    [HostTest("m04.021.efi source", HostLane.Fast, 0)]
    private static void Source()
    {
        var project=XDocument.Load(Path.Combine(Root,"src/OS/OS.csproj"));
        var defaults=project.Descendants(Flag).ToArray();
        Need(defaults.Length==1&&defaults[0].Value=="false"&&!Probes.EnableM04SimpleThrowCatchProbe&&!OS.Boot.BootSequence.IsSimpleThrowCatchEnabled(),"default-off child boundary");
        var definition=Probes.GetM04SimpleThrowCatchProbe();Need(!definition.EnabledByDefault&&definition.Risk==ProbeRisk.Destructive,"explicit dangerous probe");
        string[] errors=project.Descendants("Error").Select(x=>(string)x.Attribute("Condition")!).ToArray();
        foreach(string condition in new[]{
            "'$(EnableM04SimpleThrowCatchProbe)' != '' and '$(EnableM04SimpleThrowCatchProbe)' != 'true' and '$(EnableM04SimpleThrowCatchProbe)' != 'false'",
            "'$(EnableM04SimpleThrowCatchProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'",
            "'$(EnableM04SimpleThrowCatchProbe)' == 'true' and ('$(EnableM04SeededCollectorChurnProbe)' == 'true' or '$(EnableM04ClassConstructorExecutionProbe)' == 'true' or '$(EnableM04InterfaceDispatchProbe)' == 'true' or '$(EnableM04X64UnwindMetadataProbe)' == 'true')"})
            Need(errors.Count(x=>x==condition)==1,"exact child relationship");
        string[] compile=project.Descendants("Compile").Select(x=>(string)x.Attribute("Include")!).ToArray();
        foreach(string file in new[]{"..\\OS.Kernel\\Runtime\\ExceptionTypeMatch.cs","..\\OS.Kernel\\Runtime\\X64FrameUnwind.cs","..\\OS.Kernel\\Runtime\\NativeAotEhClauseMetadata.cs","Runtime\\ExceptionCatchProbe.cs"})Need(compile.Count(x=>x==file)==1,"one explicit admitted source "+file);
        Need(Marshal.SizeOf<X64UnwindContext>()==240,"actual context size");
        foreach(var offset in new[]{("Rip",0),("Rsp",8),("Rbx",16),("Rbp",24),("Rsi",32),("Rdi",40),("R12",48),("R13",56),("R14",64),("R15",72),("Xmm6",80),("Xmm15",224)})
            Need(Marshal.OffsetOf<X64UnwindContext>(offset.Item1).ToInt32()==offset.Item2,"actual capture field "+offset.Item1);
        Need(Marshal.SizeOf<X64UnwindVector>()==16&&Marshal.OffsetOf<X64UnwindVector>("High").ToInt32()==8,"whole 128-bit nonvolatile vector");
        string probe=File.ReadAllText(Path.Combine(Root,"src/OS/Runtime/ExceptionCatchProbe.cs")).ReplaceLineEndings("\n");
        const string header="#if NOSTDLIB_EFI && (M04_SIMPLE_THROW_CATCH_PROBE || M04_SINGLE_FINALLY_CATCH_PROBE || M04_RETHROW_PROBE || M04_NESTED_FINALLY_CATCH_PROBE || M04_UNHANDLED_EXCEPTION_PROBE || M04_FRAME_DISPATCH_PROBE || M04_CONSTRUCTOR_FAILURE_PROBE || M04_FILTERED_CATCH_PROBE || M04_NORMAL_FINALLY_PROBE)";
        Need(probe.StartsWith(header,StringComparison.Ordinal)&&
            probe.Contains("throw value;",StringComparison.Ordinal)&&probe.Contains("catch (SimpleCatchBase caught)",StringComparison.Ordinal)&&
            probe.Contains("CollectGcGraph(&root, 1)",StringComparison.Ordinal)&&probe.Contains("post->Rbp != s_ehCaller.Rbp",StringComparison.Ordinal)&&
            probe.Contains("unwound.Caller.Rip - 1 - catcher",StringComparison.Ordinal),"actual throw/catch/root/body contract");
        foreach(string required in new[]{"M04_SIMPLE_THROW_CATCH_PROBE","M04_SINGLE_FINALLY_CATCH_PROBE","M04_NORMAL_FINALLY_PROBE"})
            Need(!probe.Replace(required,"M04_UNREVIEWED_PROBE",StringComparison.Ordinal).StartsWith(header,StringComparison.Ordinal),"exact header inventory control "+required);
        Need(probe.Contains("[MethodImpl(MethodImplOptions.NoInlining)]\n    private static bool CaptureAdmitted(",StringComparison.Ordinal)&&
            probe.Contains("rip == thrower + EhThrowReturnOffset && rsp == capturedAddress + 816 &&",StringComparison.Ordinal)&&
            probe.Contains("rip != 0 && rsp >= s_gcStack.Start + 16400 && rsp <= s_gcStack.InitialRsp;",StringComparison.Ordinal),"same bounded non-inlined capture predicate");
        int original=probe.IndexOf("if (!CaptureAdmitted((ulong)captured, originalRip, originalRsp, thrower)) return RejectEh(14);",StringComparison.Ordinal);
        int invalid=probe.IndexOf("bool invalidAdmitted = CaptureAdmitted((ulong)captured, invalidRip, originalRsp, thrower);",StringComparison.Ordinal);
        int rejection=probe.IndexOf("EhLine(2, originalRip, invalidRip, originalRsp, invalidAdmitted ? 1UL : 0UL);",StringComparison.Ordinal);
        Need(original>=0&&invalid>original&&rejection>invalid&&probe.IndexOf("BootSequence.RunSimpleThrowCatchUnwind(",StringComparison.Ordinal)>rejection&&
            probe.Contains("ulong invalidRip = originalRip ^ 1UL;",StringComparison.Ordinal)&&
            probe.Contains("invalidAdmitted || captured->Rip != originalRip || captured->Rsp != originalRsp",StringComparison.Ordinal)&&
            probe.Contains("s_ehPhase != 3 || s_ehSelected != 0 || s_ehTransferAddress != 0",StringComparison.Ordinal)&&
            probe.Contains("s_ehCaller.Rip != 0 || s_ehCaller.Rsp != 0 || s_ehCatchEntries != 0 || s_ehContinuations != 0",StringComparison.Ordinal)&&
            probe.Contains("s_ehReceipt.ObjectReference != exception || s_ehReceipt.MethodTableToken != originalType",StringComparison.Ordinal)&&
            probe.Contains("s_ehReceipt.AllocationCount != 9 || !EhObjectIntact()) return RejectEh(21);",StringComparison.Ordinal),"local rejection precedes selection and preserves original capture/root/phase");
    }
    private static string InvokeQemu(params string[] args)
{
    var result = HostToolProcess.RunTool(["qemu", "smoke", ..args]);
    Need(result.ExitCode == 0 && result.Error.Length == 0, result.Output + result.Error);
    return result.Output;
}
    [HostTest("m04.021.efi transcript", HostLane.Fast, 1)]
private static void Transcript() => EfiExceptionTranscriptControls.Run(finallyProfile: false);
    [HostTest("m04.021.efi profile", HostLane.Fast, 2)]
    private static void Profile()
    {
        string[] chain=["EnableM01LoadedImageStackProbe","EnableM01LiveAllocatorProbe","EnableM04IndependentEndianConformanceProbe","EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe","EnableM04ArithmeticConformanceProbe","EnableM04LayoutContractProbe","EnableM04ManagedHeapPageRangeProbe","EnableM04RhpNewFastProbe","EnableM04GcStaticRootMaterializationProbe",Flag];
        string output=InvokeQemu(["-DryRun","-BuildEfi",..chain.Select(x=>"-"+x)]);
        string[] Read(string key)=>output.Split('\n').Single(x=>x.StartsWith(key,StringComparison.Ordinal))[key.Length..].TrimEnd('\r').Split(';',StringSplitOptions.RemoveEmptyEntries);
        string[] required=Read("REQUIRED_MARKERS "),ordered=Read("ORDERED_MARKERS "),unique=Read("UNIQUE_MARKERS "),forbidden=Read("FORBIDDEN_MARKERS ");
        string[] expectedRequired=new[]{"OS_BOOT_OK","TEST_PASS boot.sequence","OS_SMOKE_PASS"}.Concat(ordered).Distinct(StringComparer.Ordinal).ToArray();
        Need(ordered.Length==OS.Tooling.QemuContracts.M04SimpleCatchRequiredMarkers.Count&&required.Length==ordered.Length&&required.SequenceEqual(expectedRequired,StringComparer.Ordinal)&&unique.SequenceEqual(ordered,StringComparer.Ordinal),"complete ancestor plus twelve child markers and original default-required order");
        int first=Array.IndexOf(ordered,"TEST_START m04.021.simple-catch");
        Need(first==51&&ordered[first-1]=="CLROS_M04_013_READY"&&ordered[first+12]=="TEST_START boot.sequence","after actual collector, before ordinary boot");
        bool Accept(string[] records){var state=new SerialMarkerStateMachine(required,forbidden,orderedMarkers:ordered,uniqueMarkers:unique);return state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n",records)+"\r\n")).Succeeded&&state.Complete().Passed;}
        Need(Accept(ordered),"complete marker stream");
        for(int i=first;i<first+12;i++)Need(!Accept(ordered.Where((_,index)=>index!=i).ToArray()),"missing child marker");
        Need(!Accept(ordered.Append("TEST_FAIL m04.021.simple-catch 1").ToArray()),"late failure");
    }

}
