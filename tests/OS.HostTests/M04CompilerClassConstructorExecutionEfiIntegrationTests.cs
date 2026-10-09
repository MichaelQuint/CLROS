using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

internal static class M04CompilerClassConstructorExecutionEfiIntegrationTests
{
    private const string Flag = "EnableM04ClassConstructorExecutionProbe";
    private static string Root => HostToolProcess.FindRepositoryRoot();

    private static void Need(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }

    [HostTest("m04.024.efi compiler helper ABI and retained bootstrap order", HostLane.Fast, 0)]
    private static void Source()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/OS/OS.csproj"));
        Need(project.Descendants(Flag).Single().Value == "false", "disabled default");
        foreach (string edge in new[] {
            "'$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '$(EnableM04GcStaticRootMaterializationProbe)' != 'true'",
            "'$(EnableM04ClassConstructorExecutionProbe)' == 'true' and '$(EnableM04SeededCollectorChurnProbe)' == 'true'" })
            Need(project.Descendants("Error").Count(x => (string?)x.Attribute("Condition") == edge) == 1, "exact profile edge");
        string helper = File.ReadAllText(Path.Combine(Root, "src/OS/Runtime/ClassConstructorRunner.cs"));
        Need(helper.StartsWith("#if NOSTDLIB_EFI && M04_CLASS_CONSTRUCTOR_EXECUTION_PROBE", StringComparison.Ordinal) &&
            helper.Contains("public volatile nint cctorMethodAddress;", StringComparison.Ordinal), "profile-only exact pointer context");
        foreach (string name in new[] { "CheckStaticClassConstructionReturnNonGCStaticBase", "CheckStaticClassConstructionReturnGCStaticBase", "EnsureClassConstructorRun" })
            Need(helper.Contains(name + "(", StringComparison.Ordinal), "pinned managed helper " + name);
        Need(!helper.Contains("ThreadStatic", StringComparison.Ordinal) && !helper.Contains("RuntimeExport", StringComparison.Ordinal) &&
            !helper.Contains("UnmanagedCallersOnly", StringComparison.Ordinal), "no unsupported ABI placeholder");
        string gc = File.ReadAllText(Path.Combine(Root, "src/OS/Runtime/GCStaticRootMaterializationProbe.cs"));
        int bind = gc.IndexOf("BindAndRunCompilerClassConstructors(materialized.ObjectReference);", StringComparison.Ordinal);
        Need(bind > gc.IndexOf("s_gcHandle = registered.Handle;", StringComparison.Ordinal) &&
            bind < gc.IndexOf("GcStaticFixture.Get()", bind, StringComparison.Ordinal), "registration and materialization before first constructor-bearing accessor");
        string probe = File.ReadAllText(Path.Combine(Root, "src/OS/Runtime/CompilerClassConstructorExecutionProbe.cs"));
        Need(probe.Contains("BootSequence.RunCompilerClassConstructors(", StringComparison.Ordinal) &&
            probe.Contains("CctorDirectRead() => CctorDirectFixture.Read();", StringComparison.Ordinal) &&
            probe.Contains("s_cctorDepthOneReentries != 5 || s_cctorDepthTwoReentries != 3", StringComparison.Ordinal) &&
            probe.Contains("if (*(ulong*)context == 0) RejectCctor(20);", StringComparison.Ordinal), "actual active-context reentry and callable preservation");
    }

    private static string InvokeQemu(params string[] arguments)
{
    var result = HostToolProcess.RunTool(["qemu", "smoke", ..arguments]);
    Need(result.ExitCode == 0 && result.Error.Length == 0, result.Output + result.Error);
    return result.Output;
}

    [HostTest("m04.024.efi strict constructor records reject false once and reentry proof", HostLane.Fast, 1)]
private static void Transcript() => EfiConstructorTranscriptControls.Run();

    [HostTest("m04.024.efi explicit constructor profile marker isolation", HostLane.Fast, 2)]
    private static void Profile()
    {
        string[] chain = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe",
            "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe",
            "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe", "EnableM04GcStaticRootMaterializationProbe", Flag];
        string output = InvokeQemu(["-DryRun", "-BuildEfi", ..chain.Select(x => "-" + x)]);
        string[] Read(string key) => output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))[key.Length..].TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] required=Read("REQUIRED_MARKERS "), ordered=Read("ORDERED_MARKERS "), unique=Read("UNIQUE_MARKERS "), forbidden=Read("FORBIDDEN_MARKERS ");
        Need(ordered.Length == OS.Tooling.QemuContracts.M04CctorRequiredMarkers.Count && unique.SequenceEqual(ordered, StringComparer.Ordinal), "complete parent plus 12 constructor markers");
        int start = Array.IndexOf(ordered, "TEST_START m04.024");
        Need(start > 0 && ordered[start-1] == "M04_GC_ROOT 7" && ordered[start+12] == "M04_GC_ROOT 3", "constructor bootstrap insertion");
        bool Accept(string[] records)
        {
            var state = new SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            return state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n", records) + "\r\n")).Succeeded && state.Complete().Passed;
        }
        Need(Accept(ordered), "controlled complete profile stream");
        for (int i=start; i < start+12; i++) Need(!Accept(ordered.Where((_, index) => index != i).ToArray()), "missing constructor record " + i);
        Need(!Accept(ordered.Append("M04_CCTOR 255 0 0 0 0").ToArray()), "failure after readiness is rejected");
    }


}
