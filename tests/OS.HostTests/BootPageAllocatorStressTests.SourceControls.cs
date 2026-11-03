using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text.Json.Nodes;

internal static partial class BootPageAllocatorStressTests
{
    [HostTest("m01.010 source discovery follows partials and rejects host leaks")]
    private static void StressSourceDiscoveryControls()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var host = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        var kernel = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var family = host.TypeSources(StressSourceOwner).Select(document => document.Path).ToArray();
        Need(StressHostBoundary(root, host, kernel), "actual host/kernel boundary");
        var evaluation = HostSourceCatalog.EfiEvaluation(EfiOptions.Parse([], true).Profile());
        Need(StressEfiBoundary(root, evaluation, family, "SingleMinimalApi"), "actual explicit no-reference EFI closure");
        foreach (string property in new[]
        {
            "NoStdLib",
            "DisableImplicitFrameworkReferences",
            "EnableDefaultCompileItems"
        })
        {
            var changed = JsonNode.Parse(evaluation.EvaluationJson)!;
            changed["Properties"]![property] = property == "EnableDefaultCompileItems" ? "true" : "false";
            Need(!StressEfiBoundary(root, new(changed.ToJsonString(), evaluation.Sources), family, "SingleMinimalApi"), property);
        }

        foreach (string kind in new[]
        {
            "Reference",
            "ProjectReference"
        })
        {
            var changed = JsonNode.Parse(evaluation.EvaluationJson)!;
            changed["Items"]![kind]!.AsArray().Add(new JsonObject { ["Identity"] = "Unexpected" });
            Need(!StressEfiBoundary(root, new(changed.ToJsonString(), evaluation.Sources), family, "SingleMinimalApi"), kind);
        }

        var options = new CSharpParseOptions(LanguageVersion.CSharp14);
        var hidden = StressDocument(Path.Combine(root, "src", "moved-stress.cs"), "internal static class " + StressSourceOwner + " { }", options);
        var leak = new CSharpSourceCatalog([hidden], host.Compilation.References);
        Need(!StressExcluded(root, leak, family), "stress type imported under unrelated filename");
        var foreign = StressDocument(Path.Combine(root, "tests", "OS.HostTests", "unrelated.cs"), "internal static class UnrelatedHostHelper { }", options);
        Need(!StressExcluded(root, new([foreign], host.Compilation.References), family), "any host-test source import");
        static void Need(bool value, string reason)
        {
            if (!value)
                throw new InvalidOperationException(reason);
        }
    }

    [HostTest("m01.010 marker discovery follows partials and retains global ordering")]
    private static void StressMarkerDiscoveryControls()
    {
        var actual = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        Need(StressMarkerShape(actual), "actual marker shape");
        var source = MarkerFixture(actual);
        Need(StressMarkerShape(source), "minimal marker fixture");
        var method = source.Method(StressSourceOwner, nameof(RunAll));
        var document = source.Document(method);
        var owner = (TypeDeclarationSyntax)method.Parent!;
        string movedPath = Path.Combine(Path.GetDirectoryName(document.Path)!, "another-marker-part.cs");
        var remaining = document.Syntax.GetRoot().ReplaceNode(owner, owner.RemoveNode(method, SyntaxRemoveOptions.KeepNoTrivia)!);
        var moved = SyntaxFactory.CompilationUnit().WithUsings(((CompilationUnitSyntax)document.Syntax.GetRoot()).Usings).WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(owner.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method))));
        var parse = (CSharpParseOptions)document.Syntax.Options;
        var split = new CSharpSourceCatalog([StressDocument(document.Path, remaining.NormalizeWhitespace().ToFullString(), parse), StressDocument(movedPath, moved.NormalizeWhitespace().ToFullString(), parse)], source.Compilation.References);
        Need(StressMarkerShape(split), "RunAll moved into an arbitrary partial file");
        string ready = string.Concat("Console.WriteLine(", "ReadyMarker);");
        string pass = string.Concat("Console.WriteLine(\"TEST_PASS \" + ", "IssueTestName);");
        Reject(document.Text.Replace(ready + " " + pass, pass + " " + ready, StringComparison.Ordinal), "ready/pass order");
        Reject(document.Text.Replace(ready, ready + " " + ready, StringComparison.Ordinal), "duplicate ready write");
        Reject(document.Text.Replace("if (failures != 0)", "if (failures == 0)", StringComparison.Ordinal), "failure gate");
        Reject(document.Text.Replace(ready, ready + "\n#if INACTIVE_MARKER_CONTROL\n" + ready + "\n#endif\n", StringComparison.Ordinal), "inactive duplicate ready write");
        Reject(document.Text.Replace("partial class", "partial class", StringComparison.Ordinal).Replace("private const string IssueTestName", "// " + string.Concat("OS_BOOT_", "OK") + "\nprivate const string IssueTestName", StringComparison.Ordinal), "raw guest marker exclusion");
        void Reject(string text, string reason)
        {
            Need(text != document.Text, "control applies: " + reason);
            var changed = new CSharpSourceCatalog([StressDocument(document.Path, text, parse)], source.Compilation.References);
            Need(!changed.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "valid control: " + reason);
            Need(!StressMarkerShape(changed), reason);
        }

        static void Need(bool value, string reason)
        {
            if (!value)
                throw new InvalidOperationException(reason);
        }
    }

    private static CSharpSourceCatalog MarkerFixture(CSharpSourceCatalog references)
    {
        string begin = string.Concat("Console.WriteLine(\"TEST_START \" + ", "IssueTestName);");
        string fail = string.Concat("Console.WriteLine(\"TEST_FAIL \" + ", "IssueTestName + \" failures=\" + failures);");
        string ready = string.Concat("Console.WriteLine(", "ReadyMarker);");
        string pass = string.Concat("Console.WriteLine(\"TEST_PASS \" + ", "IssueTestName);");
        string text = "using System; internal static partial class " + StressSourceOwner + " { private const string IssueTestName = \"control\"; private const string ReadyMarker = \"" + ReadyMarker + "\"; internal static void RunAll() { int failures = 0; " + begin + " if (failures != 0) { " + fail + " throw new InvalidOperationException(); } " + ready + " " + pass + " } }";
        return new([StressDocument(Path.Combine(Path.GetTempPath(), "stress-marker-control.cs"), text, new CSharpParseOptions(LanguageVersion.CSharp14))], references.Compilation.References);
    }

    private static CSharpSourceDocument StressDocument(string path, string text, CSharpParseOptions options) => CSharpSourceDocument.FromBytes(path, System.Text.Encoding.UTF8.GetBytes(text), options);
}
