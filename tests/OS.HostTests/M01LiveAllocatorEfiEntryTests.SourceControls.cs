using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static partial class M01LiveAllocatorEfiEntryTests
{
    [HostTest("allocator source contracts follow moved partials and reject extra bound callers")]
    private static void SourceDiscoveryControls()
    {
        var source = Enabled();
        var disabled = Selected(false, false);
        HostCheck.True(ValidateContract(source, disabled, out _), "baseline");
        const string gate = "'$(EnableM01LiveAllocatorProbe)' == 'true' and '$(EnableM01LoadedImageStackProbe)' != 'true'";
        System.Xml.Linq.XDocument Project(string condition) => new(new System.Xml.Linq.XElement("Project",
            new System.Xml.Linq.XElement("Target", new System.Xml.Linq.XElement("Error",
                new System.Xml.Linq.XAttribute("Condition", condition)))));
        HostCheck.True(StageTwoProjectGate(Project(gate)), "independent MSBuild prerequisite gate");
        HostCheck.True(!StageTwoProjectGate(new System.Xml.Linq.XDocument(new System.Xml.Linq.XElement("Project"))), "missing MSBuild gate");
        HostCheck.True(!StageTwoProjectGate(Project(gate.Replace("!=", "==", StringComparison.Ordinal))), "inverted MSBuild gate");
        HostCheck.True(!StageTwoProjectGate(Project(gate.Replace("LoadedImageStack", "OwnedPaging", StringComparison.Ordinal))), "foreign MSBuild prerequisite");
        var renamed = new CSharpSourceCatalog(source.Documents.Select(document => CSharpSourceDocument.FromBytes(Path.Combine(Path.GetDirectoryName(document.Path)!, "moved-" + Path.GetFileName(document.Path)), System.Text.Encoding.UTF8.GetBytes(document.Text), (CSharpParseOptions)document.Syntax.Options)), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        HostCheck.True(ValidateContract(renamed, disabled, out _), "arbitrary filenames");
        foreach (var (owner, method) in new[]
        {
            (StageTwoOwner, "RunAfterStageOne"),
            (StageTwoOwner, "ValidateCompletedResult"),
            (StageOneOwner, "ExecuteStageTwoCoordinator")
        }

        )
        {
            var moved = MoveMethod(source, owner, method);
            RequireBound(moved);
            HostCheck.True(ValidateContract(moved, disabled, out _), "partial method move: " + method);
        }

        var parse = (CSharpParseOptions)source.Documents[0].Syntax.Options;
        const string extra = """
            namespace OS.Boot;
            public static unsafe partial class EfiEntry
            {
                private static class AdditionalCaller
                {
                    internal static bool Call() => M01StageTwoAdapter.RunAfterStageOne(
                        default, default, default, default, default, default, default, default);
                }
            }
            """;
        var injected = new CSharpSourceCatalog(source.Documents.Append(CSharpSourceDocument.FromBytes(Path.Combine(Path.GetDirectoryName(source.Documents[0].Path)!, "AdditionalCaller.cs"), System.Text.Encoding.UTF8.GetBytes(extra), parse)), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        RequireBound(injected);
        HostCheck.True(!ValidateContract(injected, disabled, out _), "extra nested bound caller");
    }

    private static CSharpSourceCatalog MoveMethod(CSharpSourceCatalog source, string owner, string name)
    {
        var method = source.Method(owner, name);
        var document = source.Document(method);
        string remaining = document.Text.Remove(method.SpanStart, method.Span.Length);
        string imports = string.Join("\n", document.Syntax.GetCompilationUnitRoot().Usings.Select(HostSourceText.Active));
        string body = imports + "\nnamespace OS.Boot;\npublic static unsafe partial class EfiEntry\n{\n" + "private static partial class " + source.Type(owner).Name + "\n{\n" + HostSourceText.Active(method) + "\n}\n}\n";
        var parse = (CSharpParseOptions)document.Syntax.Options;
        var replaced = CSharpSourceDocument.FromBytes(document.Path, System.Text.Encoding.UTF8.GetBytes(remaining), parse);
        var added = CSharpSourceDocument.FromBytes(Path.Combine(Path.GetDirectoryName(document.Path)!, "Moved-" + name + ".cs"), System.Text.Encoding.UTF8.GetBytes(body), parse);
        return new(source.Documents.Select(item => item == document ? replaced : item).Append(added), source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
    }

    private static void RequireBound(CSharpSourceCatalog source)
    {
        var errors = source.Compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        HostCheck.True(!errors.Any(), "mutation must bind: " + string.Join("; ", errors));
    }
}
