using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using OS.Tooling;

internal static partial class BootPageAllocatorStateMaterializerTests
{
    private static bool CheckMaterializerMarker(CSharpSourceCatalog source,
        IReadOnlyList<KeyValuePair<string, string>> markerSources)
    {
        var runAll = source.Method(nameof(BootPageAllocatorStateMaterializerTests), nameof(RunAll),
            method => method.IsStatic && method.Arity == 0 && method.ReturnsVoid
                && method.Parameters.Length == 0 && method.DeclaredAccessibility == Accessibility.Internal);
        _ = source.MethodInvocations(runAll);
        var document = source.Document(runAll);
        string expectedSource = Path.GetFullPath(document.Path);
        if (markerSources.Count != 1
            || !string.Equals(Path.GetFullPath(markerSources[0].Key), expectedSource, StringComparison.OrdinalIgnoreCase)
            || markerSources[0].Value != document.Text)
            return false;
        // The literal inventory and emission count retain the original raw
        // scope, including inactive text. Order belongs to the actual emitter.
        int emissionCount = System.Text.RegularExpressions.Regex.Matches(document.Text,
            "Console\\.WriteLine\\(\"CLROS_" + "M01_006_READY\"\\)",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant).Count;
        string runAllSource = runAll.ToString();
        int umbrellaStart = runAllSource.IndexOf("Console.WriteLine(\"TEST_START \" + IssueTestName)", StringComparison.Ordinal);
        int failureBlock = runAllSource.IndexOf("\"TEST_FAIL \" + IssueTestName + \" failures=\"", StringComparison.Ordinal);
        int ready = runAllSource.IndexOf("Console.WriteLine(\"CLROS_" + "M01_006_READY\")", StringComparison.Ordinal);
        int pass = runAllSource.IndexOf("Console.WriteLine(\"TEST_PASS \" + IssueTestName)", StringComparison.Ordinal);
        return emissionCount == 1 && umbrellaStart >= 0 && failureBlock > umbrellaStart
            && ready > failureBlock && pass > ready;
    }

    [HostTest("m01.006 marker discovery follows moved partial emitters and retains raw exclusions")]
    private static void MaterializerMarkerControls()
    {
        var references = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj").Compilation.References;
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        string ready = "CLROS_" + "M01_006_READY";
        const string header = "using System; internal static partial class BootPageAllocatorStateMaterializerTests { ";
        const string issue = "private const string IssueTestName = \"m01.006\";";
        const string start = "Console.WriteLine(\"TEST_START \" + IssueTestName);";
        const string failure = "Console.WriteLine(\"TEST_FAIL \" + IssueTestName + \" failures=\");";
        string emit = "Console.WriteLine(\"" + ready + "\");";
        const string pass = "Console.WriteLine(\"TEST_PASS \" + IssueTestName);";
        foreach (var (body, extra, accepted) in new[]
        {
            (start + failure + emit + pass, "", true),
            (start + failure + emit + pass, "\n#if HIDDEN_MARKER\n" + emit + "\n#endif\n", false),
            (start + emit + failure + pass, "", false),
            (start + failure + pass, "", false)
        })
        {
            var owner = CSharpSourceDocument.FromBytes(Path.Combine(Path.GetTempPath(), "MaterializerOwner.cs"),
                System.Text.Encoding.UTF8.GetBytes(header + issue + " }"), parse);
            var moved = CSharpSourceDocument.FromBytes(Path.Combine(Path.GetTempPath(), "ArbitraryEmitter.cs"),
                System.Text.Encoding.UTF8.GetBytes(header + "internal static void RunAll() { " + body + extra + " } }"), parse);
            var source = new CSharpSourceCatalog([owner, moved], references);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), "marker control binds");
            var inventory = new[] { new KeyValuePair<string, string>(moved.Path, moved.Text) }
                .Where(item => item.Value.Contains(ready, StringComparison.Ordinal)).ToArray();
            HostCheck.True(CheckMaterializerMarker(source, inventory) == accepted, "marker order and raw duplicate policy");
            if (accepted)
            {
                var foreign = new KeyValuePair<string, string>(Path.Combine(Path.GetTempPath(), "ForeignMarker.cs"), "// " + ready);
                HostCheck.True(!CheckMaterializerMarker(source, [..inventory, foreign]), "global raw foreign marker rejected");
                HostCheck.True(!CheckMaterializerMarker(source, [new(moved.Path, moved.Text + "\n// drift")]), "observed emitter matches compiler snapshot");
            }
        }
    }
}
