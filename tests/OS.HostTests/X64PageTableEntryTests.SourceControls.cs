using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Kernel.Memory;
using OS.Tooling;
using System.Text;

internal static partial class X64PageTableEntryTests
{
    private static string PageEntryWrittenSource(CSharpSourceCatalog host, string fileName)
    {
        var method = host.Method(typeof(X64PageTableEntryTests).FullName!, "BuildNoStdLibClosure");
        var file = host.Compilation.GetTypeByMetadataName("System.IO.File")
            ?? throw new InvalidOperationException("page_entry_file_type_missing");
        var path = host.Compilation.GetTypeByMetadataName("System.IO.Path")
            ?? throw new InvalidOperationException("page_entry_path_type_missing");
        var write = host.MethodInvocations(method).Single(call => call.TargetMethod.Name == nameof(File.WriteAllText)
            && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, file)
            && HostSourceOperations.Argument(call, 0) is IInvocationOperation destination
            && destination.TargetMethod.Name == nameof(Path.Combine)
            && SymbolEqualityComparer.Default.Equals(destination.TargetMethod.ContainingType, path)
            && destination.TargetMethod.Parameters.Length == 2
            && HostSourceOperations.Argument(destination, 1).ConstantValue.Value is string value && value == fileName);
        return HostSourceOperations.Argument(write, 1).ConstantValue.Value as string
            ?? throw new InvalidOperationException("page_entry_original_source_not_constant " + fileName);
    }

    [HostTest("m01.011 source discovery preserves raw policy and no-reference dependency ownership")]
    private static void PageEntrySourceControls()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var host = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var efi = PageEntryEfi("SingleMinimalApi").Sources;
        string shim = PageEntryWrittenSource(HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj"), "NoStdLibShim.cs");
        Check(host, efi, true, "actual original families and shim closure");
        var method = host.Method(typeof(X64PageTableEntry).FullName!, nameof(X64PageTableEntry.TryEncodeMany));
        var document = host.Document(method);
        var parent = (StructDeclarationSyntax)method.Parent!;
        var partial = parent.Modifiers.Any(SyntaxKind.PartialKeyword) ? parent
            : parent.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var unit = document.Syntax.GetRoot();
        string remaining = unit.ReplaceNode(parent, partial.WithMembers(parent.Members.Remove(method)))
            .NormalizeWhitespace().ToFullString();
        string moved = unit.ReplaceNode(parent, partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method)))
            .NormalizeWhitespace().ToFullString();
        Check(ChangePageEntry(host, document.Path, remaining, moved), ChangePageEntry(efi, document.Path, remaining, moved),
            true, "batch method moved to arbitrary partial source");
        string hidden = "#if UNRELATED_DISABLED\nnamespace OS.Kernel.Memory; public readonly partial struct X64PageTableEntry { private string forbidden; }\n#endif\n";
        Check(ChangePageEntry(host, document.Path, document.Text, hidden), ChangePageEntry(efi, document.Path, document.Text, hidden),
            false, "original raw non-host branch policy remains visible");
        Check(ChangePageEntry(host, document.Path, document.Text + "\n// changed host snapshot\n"), efi,
            false, "host and EFI source bytes must match");
        string addedCall = document.Text.Insert(method.Body!.OpenBraceToken.Span.End, "\n_ = global::Foreign.Dependency.Value;\n");
        const string foreign = "namespace Foreign; public static class Dependency { public static int Value => 1; }";
        Check(ChangePageEntry(host, document.Path, addedCall, foreign), ChangePageEntry(efi, document.Path, addedCall, foreign),
            false, "foreign dependency does not enter the shim closure");
        string conditional = document.Text.Insert(method.Body.OpenBraceToken.Span.End,
            "\n#if NOSTDLIB_EFI\n_ = PageEntryConditionalDependency.Value;\n#endif\n");
        const string conditionalType = "#if NOSTDLIB_EFI\nnamespace OS.Kernel.Memory; public static class PageEntryConditionalDependency { public static int Value => 1; }\n#endif\n";
        Check(ChangePageEntry(host, document.Path, conditional, conditionalType), ChangePageEntry(efi, document.Path, conditional, conditionalType),
            false, "EFI-only dependency missing from host closure fails original shim binding");

        void Check(CSharpSourceCatalog candidateHost, CSharpSourceCatalog candidateEfi, bool expected, string reason)
        {
            bool accepted;
            try
            {
                accepted = PageEntryRawPolicy(candidateHost);
                if (accepted)
                    HostBootPageAllocatorSources.ValidateShimClosure(PageEntrySources(root, candidateHost, candidateEfi), shim);
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }
            HostCheck.True(accepted == expected, reason);
        }
    }

    [HostTest("m01.011 marker discovery follows the aggregate method and retains raw ordering")]
    private static void PageEntryMarkerControls()
    {
        var host = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        HostCheck.True(PageEntryMarkerShape(host), "actual original marker source");
        var method = host.Method(typeof(X64PageTableEntryTests).FullName!, nameof(RunAll));
        var document = host.Document(method);
        var parent = (ClassDeclarationSyntax)method.Parent!;
        var partial = parent.Modifiers.Any(SyntaxKind.PartialKeyword) ? parent
            : parent.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var unit = document.Syntax.GetRoot();
        string remaining = unit.ReplaceNode(parent, partial.WithMembers(parent.Members.Remove(method)))
            .NormalizeWhitespace().ToFullString();
        string moved = unit.ReplaceNode(parent, partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method)))
            .NormalizeWhitespace().ToFullString();
        HostCheck.True(PageEntryMarkerShape(ChangePageEntry(host, document.Path, remaining, moved)),
            "aggregate method can move independently of its marker constant");
        string hidden = "#if UNRELATED_DISABLED\ninternal static partial class X64PageTableEntryTests { private const string Bad = \""
            + "OS_" + "BOOT_OK\"; }\n#endif\n";
        HostCheck.True(!PageEntryMarkerShape(ChangePageEntry(host, document.Path, document.Text, hidden)),
            "inactive owner partial retains raw boot-marker exclusion");
        var ready = method.Body!.Statements.Single(statement => statement.ToString() == "Console.WriteLine(ReadyMarker);");
        var reordered = method.WithBody(method.Body.WithStatements(method.Body.Statements.Remove(ready).Insert(0, ready)));
        HostCheck.True(!PageEntryMarkerShape(ChangePageEntry(host, document.Path,
            unit.ReplaceNode(method, reordered).ToFullString())), "ready marker cannot precede the failure gate");
    }

    private static CSharpSourceCatalog ChangePageEntry(CSharpSourceCatalog source, string path, string replacement, string? added = null)
    {
        var original = source.Documents.Single(document => document.Path == path);
        var parse = (CSharpParseOptions)original.Syntax.Options;
        string addedPath = Path.Combine(Path.GetDirectoryName(path)!, "ArbitraryPageEntryPart.cs");
        var documents = source.Documents.Select(document => document.Path == path
            ? CSharpSourceDocument.FromBytes(path, Encoding.UTF8.GetBytes(replacement), parse) : document).ToList();
        if (added is not null)
            documents.Add(CSharpSourceDocument.FromBytes(addedPath, Encoding.UTF8.GetBytes(added), parse));
        var result = new CSharpSourceCatalog(documents, source.Compilation.References,
            source.Compilation.AssemblyName!, source.Compilation.Options);
        foreach (var document in result.Documents.Where(document => document.Path == path || document.Path == addedPath))
            HostCheck.True(!result.Compilation.GetSemanticModel(document.Syntax).GetDiagnostics()
                .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "page-table mutation is valid selected-profile code");
        return result;
    }

    [HostTest("m01.011 original shim discovery uses the unique bound output destination")]
    private static void PageEntryWrittenSourceControls()
    {
        var references = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj").Compilation.References;
        const string owner = "using System.IO; internal static partial class X64PageTableEntryTests { ";
        const string write = "File.WriteAllText(Path.Combine(root, \"NoStdLibShim.cs\"), \"expected shim\");";
        const string other = "File.WriteAllText(Path.Combine(root, \"Other.cs\"), \"\"\"unrelated raw literal\"\"\");";
        foreach (var (body, expected) in new[] { (write, true), (other + write, true), (other, false), (write + write, false) })
        {
            var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
            var source = new CSharpSourceCatalog([
                CSharpSourceDocument.FromBytes("arbitrary-constant.cs", Encoding.UTF8.GetBytes(owner + "private const string Decoy = \"\"\"first raw literal\"\"\"; }"), parse),
                CSharpSourceDocument.FromBytes("moved-builder.cs", Encoding.UTF8.GetBytes(owner
                    + "private static void BuildNoStdLibClosure(string root) { " + body + " } }"), parse)
            ], references);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                "output selector control is valid code");
            bool accepted;
            try
            {
                accepted = PageEntryWrittenSource(source, "NoStdLibShim.cs") == "expected shim";
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }
            HostCheck.True(accepted == expected, "missing or duplicate destinations reject; unrelated preceding literals do not matter");
        }
    }

    [HostTest("m01.011 global raw enum and marker scans retain uncompiled duplicate controls")]
    private static void PageEntryGlobalRawControls()
    {
        string root = Path.Combine(Path.GetTempPath(), "clros-page-entry-source-controls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "src"));
        Directory.CreateDirectory(Path.Combine(root, "tests"));
        Directory.CreateDirectory(Path.Combine(root, "tools"));
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        var references = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj").Compilation.References;
        string enumPath = Path.Combine(root, "src", "ArbitraryLevel.cs");
        const string enumText = "namespace OS.Kernel.Memory; public enum X64PageTableLevel : byte { Uninitialized, Pte }";
        File.WriteAllText(enumPath, enumText, new UTF8Encoding(false));
        var enumSource = new CSharpSourceCatalog([CSharpSourceDocument.Read(enumPath, parse)], references);
        HostCheck.True(PageEntryEnumBoundary(root, enumSource), "enum follows its bound declaration document");
        File.WriteAllText(Path.Combine(root, "src", "UncompiledLevel.cs"),
            "#if UNRELATED_DISABLED\nnamespace Foreign; public enum X64PageTableLevel { Other }\n#endif\n", new UTF8Encoding(false));
        HostCheck.True(!PageEntryEnumBoundary(root, enumSource), "uncompiled inactive enum duplicate remains forbidden");

        string constants = "internal static partial class X64PageTableEntryTests { private const string IssueTestName = \"m01.011\"; private const string ReadyMarker = \""
            + ReadyMarker + "\"; }";
        string body = "using System; internal static partial class X64PageTableEntryTests { internal static void RunAll() { int failures = 0; "
            + "Console.WriteLine(\"TEST_START \" + IssueTestName); if (failures != 0) { Console.WriteLine(\"TEST_FAIL \" + IssueTestName); "
            + "throw new InvalidOperationException(\"M01-011 x64 page-table entry tests failed:\"); } Console.WriteLine(ReadyMarker); Console.WriteLine(\"TEST_PASS \" + IssueTestName); } }";
        string constantPath = Path.Combine(root, "tests", "ArbitraryConstants.cs");
        string bodyPath = Path.Combine(root, "tests", "MovedAggregate.cs");
        File.WriteAllText(constantPath, constants, new UTF8Encoding(false));
        File.WriteAllText(bodyPath, body, new UTF8Encoding(false));
        var markerSource = new CSharpSourceCatalog([
            CSharpSourceDocument.Read(constantPath, parse), CSharpSourceDocument.Read(bodyPath, parse)
        ], references);
        HostCheck.True(!markerSource.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "split marker fixture binds");
        HostCheck.True(PageEntryMarkerBoundary(root, markerSource), "global scan permits exact moved owner documents");
        File.WriteAllText(Path.Combine(root, "tools", "UncompiledMarker.ps1"), "# " + ReadyMarker, new UTF8Encoding(false));
        HostCheck.True(!PageEntryMarkerBoundary(root, markerSource), "uncompiled executable-source duplicate remains forbidden");
    }
}
