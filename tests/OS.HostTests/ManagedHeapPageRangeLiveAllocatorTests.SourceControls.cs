using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Runtime;
using OS.Tooling;
using System.Text;

internal static unsafe partial class ManagedHeapPageRangeLiveAllocatorTests
{
    [HostTest("m04.006 SDK discovery preserves shim, profile and allocator ownership")]
    private static void HeapSdkSourceControls()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var kernel = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var efi = HostSourceCatalog.Efi(EfiOptions.Parse([], false).Profile());
        Check(kernel, efi, true, "actual original runtime shim and kernel dependencies");

        var method = kernel.Method(typeof(ManagedHeapPageRangeLiveAllocator).FullName!, "ReadFixedPage");
        var document = kernel.Document(method);
        var owner = (ClassDeclarationSyntax)method.Parent!;
        var partial = owner.Modifiers.Any(SyntaxKind.PartialKeyword)
            ? owner : owner.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var unit = document.Syntax.GetRoot();
        string remaining = unit.ReplaceNode(owner, partial.WithMembers(owner.Members.Remove(method))).NormalizeWhitespace().ToFullString();
        string moved = unit.ReplaceNode(owner, partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(method))).NormalizeWhitespace().ToFullString();
        Check(ChangeHeapSdk(kernel, document.Path, remaining, moved), ChangeHeapSdk(efi, document.Path, remaining, moved),
            true, "heap readback may move into an arbitrary partial document");
        Check(ChangeHeapSdk(kernel, document.Path, document.Text + "\n// host-only bytes\n"), efi,
            false, "host and EFI dependency identities must match");

        string foreignCall = document.Text.Insert(method.Body!.OpenBraceToken.Span.End, "\n_ = global::Foreign.HeapDependency.Value;\n");
        const string foreign = "namespace Foreign; public static class HeapDependency { public static int Value => 1; }";
        Check(ChangeHeapSdk(kernel, document.Path, foreignCall, foreign), ChangeHeapSdk(efi, document.Path, foreignCall, foreign),
            false, "dependencies cannot admit a foreign namespace");
        string extraRuntimeCall = document.Text.Insert(method.Body.OpenBraceToken.Span.End, "\n_ = OtherRuntimeDependency.Value;\n");
        const string extraRuntime = "namespace OS.Kernel.Runtime; public static class OtherRuntimeDependency { public static int Value => 1; }";
        Check(ChangeHeapSdk(kernel, document.Path, extraRuntimeCall, extraRuntime), ChangeHeapSdk(efi, document.Path, extraRuntimeCall, extraRuntime),
            false, "the runtime namespace does not admit unrelated runtime services");
        string conditionalCall = document.Text.Insert(method.Body.OpenBraceToken.Span.End,
            "\n#if NOSTDLIB_EFI\n_ = HeapConditionalDependency.Value;\n#endif\n");
        const string conditional = "#if NOSTDLIB_EFI\nnamespace OS.Kernel.Runtime; public static class HeapConditionalDependency { public static int Value => 1; }\n#endif\n";
        Check(ChangeHeapSdk(kernel, document.Path, conditionalCall, conditional), ChangeHeapSdk(efi, document.Path, conditionalCall, conditional),
            false, "a host-only closure cannot omit a selected no-stdlib dependency");

        var omitted = efi.TypeSources("Internal.Runtime.CompilerHelpers.StartupCodeHelpers").Single();
        var missingRuntime = new CSharpSourceCatalog(efi.Documents.Where(value => value.Path != omitted.Path),
            efi.Compilation.References, efi.Compilation.AssemblyName!, efi.Compilation.Options);
        HostCheck.True(!missingRuntime.Compilation.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "omitted source-unreferenced export owner remains valid C#");
        Check(kernel, missingRuntime, false, "the complete original reviewed runtime family remains required");
        var attribute = efi.TypeSources(typeof(System.Runtime.CompilerServices.MethodImplAttribute).FullName!).Single();
        Check(kernel, ChangeHeapSdk(efi, attribute.Path, attribute.Text
            + "\nnamespace System.Runtime.CompilerServices { internal class ExtraAttributeLayer { } }\n"),
            false, "the explicit MethodImpl family cannot import an extra type");

        void Check(CSharpSourceCatalog candidateKernel, CSharpSourceCatalog candidateEfi, bool expected, string reason)
        {
            bool accepted;
            try
            {
                _ = HeapSdkCompilation(HeapSdkSources(root, candidateKernel, candidateEfi));
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

    private static CSharpSourceCatalog ChangeHeapSdk(CSharpSourceCatalog source, string path, string replacement, string? added = null)
    {
        var original = source.Documents.Single(document => document.Path == path);
        var parse = (CSharpParseOptions)original.Syntax.Options;
        string addedPath = Path.Combine(Path.GetDirectoryName(path)!, "ArbitraryHeapPart.cs");
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
