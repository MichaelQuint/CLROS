using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Runtime;
using OS.Tooling;
using System.Text;

internal static unsafe partial class GuestTestArrayAllocationLengthOverflowChecksTests
{
    [HostTest("m04.008 SDK discovery retains its wrapper and rejects unrelated runtime inputs")]
    private static void ArraySdkSourceControls()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var kernel = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var efi = HostSourceCatalog.Efi(EfiOptions.Parse([], false).Profile());
        Check(kernel, efi, true, "actual wrapper and original shim");
        var method = kernel.Method(typeof(GuestTestArrayAllocationLengthOverflowChecks).FullName!, "TryAllocate");
        var document = kernel.Document(method);
        var owner = (ClassDeclarationSyntax)method.Parent!;
        var partial = owner.Modifiers.Any(SyntaxKind.PartialKeyword)
            ? owner : owner.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var unit = document.Syntax.GetRoot();
        string remaining = unit.ReplaceNode(owner, partial.WithMembers(owner.Members.Remove(method))).NormalizeWhitespace().ToFullString();
        string moved = unit.ReplaceNode(owner, partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method))).NormalizeWhitespace().ToFullString();
        Check(ChangeArraySdk(kernel, document.Path, remaining, moved), ChangeArraySdk(efi, document.Path, remaining, moved),
            true, "wrapper operation may move to an arbitrary partial document");
        Check(ChangeArraySdk(kernel, document.Path, document.Text + "\n// host-only bytes\n"), efi,
            false, "host and EFI raw dependency identities must match");
        string sibling = Inject("_ = typeof(GuestTestStringAllocationCharacterStorage);");
        Check(ChangeArraySdk(kernel, document.Path, sibling), ChangeArraySdk(efi, document.Path, sibling),
            false, "the other wrapper is outside this original SDK family");
        string unrelated = Inject("_ = UnrelatedRuntimeService.Value;");
        const string service = "namespace OS.Kernel.Runtime; public static class UnrelatedRuntimeService { public static int Value => 1; }";
        Check(ChangeArraySdk(kernel, document.Path, unrelated, service), ChangeArraySdk(efi, document.Path, unrelated, service),
            false, "the runtime namespace does not authorize extra services");
        string foreign = Inject("_ = global::Foreign.WrapperDependency.Value;");
        const string foreignType = "namespace Foreign; public static class WrapperDependency { public static int Value => 1; }";
        Check(ChangeArraySdk(kernel, document.Path, foreign, foreignType), ChangeArraySdk(efi, document.Path, foreign, foreignType),
            false, "foreign dependency is outside the bounded layer");

        var omitted = efi.TypeSources("Internal.Runtime.CompilerHelpers.StartupCodeHelpers").Single();
        var missingRuntime = new CSharpSourceCatalog(efi.Documents.Where(value => value.Path != omitted.Path),
            efi.Compilation.References, efi.Compilation.AssemblyName!, efi.Compilation.Options);
        HostCheck.True(!missingRuntime.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error),
            "omitting a source-unreferenced shim owner remains valid C#");
        Check(kernel, missingRuntime, false, "complete original runtime shim remains required");

        string Inject(string statement)
        {
            var body = method.Body ?? SyntaxFactory.Block(SyntaxFactory.ReturnStatement(method.ExpressionBody!.Expression));
            var changed = method.WithExpressionBody(null).WithSemicolonToken(default)
                .WithBody(body.WithStatements(body.Statements.Insert(0, SyntaxFactory.ParseStatement(statement))));
            return unit.ReplaceNode(method, changed).NormalizeWhitespace().ToFullString();
        }

        void Check(CSharpSourceCatalog candidateKernel, CSharpSourceCatalog candidateEfi, bool expected, string reason)
        {
            bool accepted;
            try
            {
                _ = ArraySdkCompilation(ArraySdkSources(root, candidateKernel, candidateEfi));
                accepted = true;
            }
            catch (InvalidOperationException error)
            {
                if (expected)
                    throw new InvalidOperationException(reason, error);
                accepted = false;
            }
            HostCheck.True(accepted == expected, reason);
        }
    }

    private static CSharpSourceCatalog ChangeArraySdk(CSharpSourceCatalog source, string path, string replacement, string? added = null)
    {
        var original = source.Documents.Single(document => document.Path == path);
        var parse = (CSharpParseOptions)original.Syntax.Options;
        string addedPath = Path.Combine(Path.GetDirectoryName(path)!, "ArbitraryArrayPart.cs");
        var documents = source.Documents.Select(document => document.Path == path
            ? CSharpSourceDocument.FromBytes(path, Encoding.UTF8.GetBytes(replacement), parse) : document).ToList();
        if (added is not null)
            documents.Add(CSharpSourceDocument.FromBytes(addedPath, Encoding.UTF8.GetBytes(added), parse));
        var result = new CSharpSourceCatalog(documents, source.Compilation.References, source.Compilation.AssemblyName!, source.Compilation.Options);
        foreach (var document in result.Documents.Where(document => document.Path == path || document.Path == addedPath))
            HostCheck.True(!result.Compilation.GetSemanticModel(document.Syntax).GetDiagnostics()
                .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "source control binds before selection");
        return result;
    }
}
