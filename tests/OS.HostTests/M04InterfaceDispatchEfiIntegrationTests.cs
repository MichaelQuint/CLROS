using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml.Linq;
using OS.Tools.QemuRunner;

internal static class M04InterfaceDispatchEfiIntegrationTests
{
    private const string Flag = "EnableM04InterfaceDispatchProbe";
    private static string Root => HostToolProcess.FindRepositoryRoot();

    private static void Need(bool value, string reason)
    {
        if (!value)
            throw new InvalidOperationException(reason);
    }

    [HostTest("m04.016.efi compiler callsites and explicit child profile", HostLane.Fast, 0)]
    private static void Source()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/OS/OS.csproj"));
        Need(project.Descendants(Flag).Single().Value == "false", "disabled default");
        Need(OS.Kernel.Runtime.TypeManagerModuleMetadataLiveNativeAOTImage.StateBytes == 272, "raw original-owner handle offset matches current POD");
        const string edge = "'$(EnableM04InterfaceDispatchProbe)' == 'true' and '$(EnableM04ClassConstructorExecutionProbe)' != 'true'";
        Need(project.Descendants("Error").Count(x => (string?)x.Attribute("Condition") == edge) == 1, "exact constructor prerequisite");
        var source = HostSourceCatalog.Efi(OS.Tooling.EfiOptions.Parse(
            [.. RuntimeProfilePortTests.RethrowFlags[..^1].Select(name => "--Enable" + name + "Probe"),
                "--EnableM04ClassConstructorExecutionProbe", "--" + Flag], true).Profile());
        const string owner = "OS.Runtime.RhpNewFastAllocation";
        foreach (string consumer in new[] { "DispatchPlain", "DispatchVirtual", "DispatchProducer", "DispatchConsumer", "DispatchNoise" })
        {
            var method = source.Method(owner, consumer, OriginalConsumerSignature);
            _ = source.MethodInvocations(method);
        }
        var callback = source.Method(owner, "DispatchCallback");
        var calls = source.MethodInvocations(callback);
        string callbackText = HostSourceText.Active(callback);
        Need(calls.Any(call => call.TargetMethod.ContainingType.ToDisplayString() == "OS.Boot.BootSequence"
                && call.TargetMethod.Name == "RunInterfaceDispatch")
            && callbackText.Contains("CLROS_InterfaceDispatch_Resolve", StringComparison.Ordinal)
            && HostSourceText.Contains(callbackText, "resolved.CacheHit != (index % 3 == 2)"),
            "real callback and original cache receipts");
        var run = source.Method(owner, "RunGcStatics");
        _ = source.MethodInvocations(run);
        string gc = HostSourceText.Active(run);
        int collector = HostSourceText.IndexOf(gc, "RunCollectorGraph();");
        Need(collector >= 0 && HostSourceText.IndexOf(gc, "RunCompilerInterfaceDispatch();") > collector,
            "dispatch follows retained original collector");
    }

    private static bool OriginalConsumerSignature(Microsoft.CodeAnalysis.IMethodSymbol method) =>
        method.IsStatic && method.Arity == 0 && !method.ReturnsByRef && !method.ReturnsByRefReadonly
        && method.DeclaredAccessibility == Microsoft.CodeAnalysis.Accessibility.Private
        && method.ReturnType.SpecialType == Microsoft.CodeAnalysis.SpecialType.System_UInt64;

    [HostTest("m04.016.efi consumer signature excludes generic and reference-return variants")]
    private static void ConsumerSignatureControls()
    {
        const string text = """
            internal class Consumers
            {
                private static ulong value;
                private static ulong Original() => value;
                private static ulong Generic<T>() => value;
                private static ref ulong Reference() => ref value;
                private static ref readonly ulong ReadOnlyReference() => ref value;
            }
            """;
        var source = new OS.Tooling.CSharpSourceCatalog(
            [OS.Tooling.CSharpSourceDocument.FromBytes("arbitrary-consumers.cs", Encoding.UTF8.GetBytes(text),
                new Microsoft.CodeAnalysis.CSharp.CSharpParseOptions(Microsoft.CodeAnalysis.CSharp.LanguageVersion.CSharp14))],
            [Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic =>
            diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error), "all signature controls compile");
        foreach (string name in new[] { "Original", "Generic", "Reference", "ReadOnlyReference" })
        {
            var method = source.Type("Consumers").GetMembers(name).OfType<Microsoft.CodeAnalysis.IMethodSymbol>().Single();
            HostCheck.True(OriginalConsumerSignature(method) == (name == "Original"), name);
        }
    }

    private static string InvokeQemu(params string[] arguments)
    {
        var result = HostToolProcess.RunTool(["qemu", "smoke", .. arguments]);
        Need(result.ExitCode == 0 && result.Error.Length == 0, result.Output + result.Error);
        return result.Output;
    }

    [HostTest("m04.016.efi complete raw dispatch oracle rejects malformed evidence", HostLane.Fast, 1)]
    private static void Transcript() => EfiDispatchTranscriptControls.Run();

    [HostTest("m04.016.efi bounded envelope markers retain final boot EOF", HostLane.Fast, 2)]
    private static void Profile()
    {
        string[] chain = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe",
            "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe", "EnableM04LayoutContractProbe",
            "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe", "EnableM04GcStaticRootMaterializationProbe",
            "EnableM04ClassConstructorExecutionProbe", Flag];
        string output = InvokeQemu(["-DryRun", "-BuildEfi", .. chain.Select(x => "-" + x)]);
        string[] Read(string key) => output.Split('\n').Single(x => x.StartsWith(key, StringComparison.Ordinal))[key.Length..].TrimEnd('\r').Split(';', StringSplitOptions.RemoveEmptyEntries);
        string[] required = Read("REQUIRED_MARKERS "), ordered = Read("ORDERED_MARKERS "), unique = Read("UNIQUE_MARKERS "), forbidden = Read("FORBIDDEN_MARKERS ");
        Need(ordered.Length == OS.Tooling.QemuContracts.M04DispatchRequiredMarkers.Count && ordered[^1] == "OS_SMOKE_PASS" && unique.SequenceEqual(ordered, StringComparer.Ordinal), "71 parent plus 3 envelope markers; raw parser owns 73 numeric stages");
        int index = Array.IndexOf(ordered, "TEST_START m04.016");
        Need(index == 63 && ordered[index - 1] == "CLROS_M04_013_READY" && ordered[index + 3] == "TEST_START boot.sequence", "dispatch precedes final eight boot records");
        bool Accept(string[] records)
        {
            var state = new SerialMarkerStateMachine(required, forbidden, orderedMarkers: ordered, uniqueMarkers: unique);
            return state.Feed(Encoding.ASCII.GetBytes(string.Join("\r\n", records) + "\r\n")).Succeeded && state.Complete().Passed;
        }
        Need(Accept(ordered), "controlled envelope only; raw proof tested separately");
        for (int i = index; i < index + 3; i++)
            Need(!Accept(ordered.Where((_, n) => n != i).ToArray()), "missing dispatch envelope " + i);
        Need(!Accept(ordered.Append("M04_DISPATCH 255 0 0 0 0").ToArray()), "failure after readiness");
    }

}
