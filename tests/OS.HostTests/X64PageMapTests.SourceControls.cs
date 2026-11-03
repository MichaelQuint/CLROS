using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Kernel.Memory;
using OS.Tooling;
using System.Text;

internal static unsafe partial class X64PageMapTests
{
    [HostTest("m01.014 page-map source discovery retains raw policies and shim-only dependency boundaries")]
    private static void PageMapSourceControls()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var host = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var efi = PageMapEfi();
        string shim = PageMapControlShim(HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj"));
        Check(host, efi, true, "actual raw families and no-reference closure");
        string owner = typeof(X64PageMap).FullName!;
        var method = host.Method(owner, nameof(X64PageMap.Apply));
        var document = host.Document(method);
        var parent = (ClassDeclarationSyntax)method.Parent!;
        var partial = parent.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var unit = document.Syntax.GetRoot();
        string remaining = unit.ReplaceNode(parent, partial.WithMembers(parent.Members.Remove(method)))
            .NormalizeWhitespace().ToFullString();
        string moved = unit.ReplaceNode(parent, partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method)))
            .NormalizeWhitespace().ToFullString();
        string movedPath = Path.Combine(Path.GetDirectoryName(document.Path)!, "ArbitraryMapOperation.cs");
        Check(Change(host, document.Path, remaining, movedPath, moved),
            Change(efi, document.Path, remaining, movedPath, moved), true, "method moved into a separate partial");

        RenameResult(host, efi);
        var resultType = host.Compilation.GetTypeByMetadataName(typeof(X64PageMapResult).FullName!)!;
        var sizeField = resultType.GetMembers(nameof(X64PageMapResult.StructureSize)).OfType<IFieldSymbol>().Single();
        var sizeSyntax = sizeField.DeclaringSyntaxReferences.Single().GetSyntax();
        var resultDocument = host.Document(sizeSyntax);
        var resultParent = sizeSyntax.Ancestors().OfType<StructDeclarationSyntax>().First();
        var resultPartial = resultParent.Modifiers.Any(SyntaxKind.PartialKeyword) ? resultParent
            : resultParent.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        string resultText = resultDocument.Syntax.GetRoot().ReplaceNode(resultParent, resultPartial)
            .NormalizeWhitespace().ToFullString();
        string resultPartPath = Path.Combine(Path.GetDirectoryName(resultDocument.Path)!, "ArbitraryResultPart.cs");
        const string resultPart = "namespace OS.Kernel.Memory; public readonly partial struct X64PageMapResult { }";
        RenameResult(Change(host, resultDocument.Path, resultText, resultPartPath, resultPart),
            Change(efi, resultDocument.Path, resultText, resultPartPath, resultPart));

        string basePartial = unit.ReplaceNode(parent, partial).NormalizeWhitespace().ToFullString();
        foreach (string forbidden in new[] { "System.", "Span<", "throw ", "delegate ", "INVLPG" })
        {
            string hidden = "#if UNRELATED_DISABLED\nnamespace OS.Kernel.Memory; public static unsafe partial class X64PageMap { // "
                + forbidden + "\n}\n#endif\n";
            Check(Change(host, document.Path, basePartial, movedPath, hidden),
                Change(efi, document.Path, basePartial, movedPath, hidden), false, "raw inactive partial policy " + forbidden);
        }
        Check(Change(host, document.Path, document.Text + "\n// host-only snapshot change\n"), efi,
            false, "host and admitted EFI bytes must agree");

        var body = method.Body ?? throw new InvalidOperationException("page_map_control_body");
        string foreign = unit.ReplaceNode(body, body.WithStatements(body.Statements.Insert(0,
            SyntaxFactory.ParseStatement("_ = global::Foreign.Helper.Value;")))).NormalizeWhitespace().ToFullString();
        const string foreignType = "namespace Foreign; public static class Helper { public static int Value => 1; }";
        Check(Change(host, document.Path, foreign, movedPath, foreignType),
            Change(efi, document.Path, foreign, movedPath, foreignType), false, "foreign dependency layer rejected");

        string conditional = document.Text.Insert(body.OpenBraceToken.Span.End,
            "\n#if NOSTDLIB_EFI\n_ = PageMapConditionalDependency.Value;\n#endif\n");
        const string conditionalType = "#if NOSTDLIB_EFI\nnamespace OS.Kernel.Memory; public static class PageMapConditionalDependency { public static int Value => 1; }\n#endif\n";
        Check(Change(host, document.Path, conditional, movedPath, conditionalType),
            Change(efi, document.Path, conditional, movedPath, conditionalType), false,
            "EFI-only dependency absent from host closure is not silently omitted");

        void RenameResult(CSharpSourceCatalog candidateHost, CSharpSourceCatalog candidateEfi)
        {
            var type = candidateHost.Compilation.GetTypeByMetadataName(typeof(X64PageMapResult).FullName!)!;
            var field = type.GetMembers(nameof(X64PageMapResult.StructureSize)).OfType<IFieldSymbol>().Single();
            var fieldDocument = candidateHost.Document(field.DeclaringSyntaxReferences.Single().GetSyntax());
            string renamedPath = Path.Combine(Path.GetDirectoryName(fieldDocument.Path)!, "ArbitraryResult.cs");
            Check(Change(candidateHost, fieldDocument.Path, null, renamedPath, fieldDocument.Text),
                Change(candidateEfi, fieldDocument.Path, null, renamedPath, fieldDocument.Text), true,
                "bound field document renamed independently of result partial count");
        }

        void Check(CSharpSourceCatalog candidateHost, CSharpSourceCatalog candidateEfi, bool expected, string reason)
        {
            bool accepted;
            try
            {
                accepted = PageMapRawSourcePolicy(candidateHost, candidateEfi);
                if (accepted)
                {
                    var sources = PageMapSourceClosure(root, candidateHost, candidateEfi);
                    ValidatePageMapShimClosure(sources, shim);
                }
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }
            HostCheck.True(accepted == expected, reason);
        }
    }

    private static CSharpSourceCatalog Change(CSharpSourceCatalog source, string path, string? replacement,
        string? addedPath = null, string? addedText = null)
    {
        var original = source.Documents.Single(item => item.Path == path);
        var parse = (CSharpParseOptions)original.Syntax.Options;
        var documents = source.Documents.Where(item => item.Path != path).ToList();
        if (replacement is not null)
            documents.Insert(source.Documents.ToList().IndexOf(original),
                CSharpSourceDocument.FromBytes(path, Encoding.UTF8.GetBytes(replacement), parse));
        if (addedPath is not null)
            documents.Add(CSharpSourceDocument.FromBytes(addedPath, Encoding.UTF8.GetBytes(addedText!), parse));
        var result = new CSharpSourceCatalog(documents, source.Compilation.References,
            source.Compilation.AssemblyName!, source.Compilation.Options);
        foreach (var document in result.Documents.Where(item => item.Path == path || item.Path == addedPath))
            HostCheck.True(!result.Compilation.GetSemanticModel(document.Syntax).GetDiagnostics()
                .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "page-map mutation is valid code");
        return result;
    }

    private static string PageMapControlShim(CSharpSourceCatalog source)
    {
        var method = source.Method(nameof(X64PageMapTests), "BuildNoStdLibClosure");
        var file = source.Compilation.GetTypeByMetadataName("System.IO.File");
        var path = source.Compilation.GetTypeByMetadataName("System.IO.Path");
        var writes = source.MethodInvocations(method).Where(call => call.TargetMethod.Name == nameof(File.WriteAllText)
            && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, file)
            && HostSourceOperations.Argument(call, 0) is IInvocationOperation destination
            && destination.TargetMethod.Name == nameof(Path.Combine)
            && SymbolEqualityComparer.Default.Equals(destination.TargetMethod.ContainingType, path)
            && destination.TargetMethod.Parameters.Length == 2
            && destination.TargetMethod.Parameters.All(parameter => parameter.Type.SpecialType == SpecialType.System_String)
            && HostSourceOperations.Argument(destination, 1).ConstantValue is { HasValue: true, Value: "NoStdLibShim.cs" })
            .ToArray();
        if (writes.Length != 1 || HostSourceOperations.Argument(writes[0], 1).ConstantValue.Value is not string shim)
            throw new InvalidOperationException("page_map_control_shim_missing_or_ambiguous");
        return shim;
    }

    [HostTest("m01.014 shim discovery follows the bound destination and rejects missing or duplicate writes")]
    private static void PageMapShimSelectorControls()
    {
        var references = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj").Compilation.References;
        const string prefix = "using System.IO; internal static partial class X64PageMapTests { ";
        const string write = "File.WriteAllText(Path.Combine(root, \"NoStdLibShim.cs\"), \"expected shim\");";
        const string other = "File.WriteAllText(Path.Combine(root, \"Other.cs\"), \"\"\"unrelated raw literal\"\"\");";
        foreach (var (body, expected) in new[]
        {
            (write, true), (other + write, true), (other, false), (write + write, false)
        })
        {
            var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
            var source = new CSharpSourceCatalog([
                CSharpSourceDocument.FromBytes("arbitrary-owner.cs", Encoding.UTF8.GetBytes(prefix + "private const string Decoy = \"\"\"first raw text\"\"\"; }"), parse),
                CSharpSourceDocument.FromBytes("moved-builder.cs", Encoding.UTF8.GetBytes(prefix
                    + "private static bool BuildNoStdLibClosure(string root) { " + body + "return true; } }"), parse)
            ], references);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
                "shim selector control is valid code");
            bool accepted;
            try
            {
                accepted = PageMapControlShim(source) == "expected shim";
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }
            HostCheck.True(accepted == expected, "bound shim destination is unique and independent of literal order");
        }
    }
}
