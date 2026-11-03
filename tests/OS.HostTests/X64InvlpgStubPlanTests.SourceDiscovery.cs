using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Memory;
using OS.Tooling;
using System.Text;

internal static unsafe partial class X64InvlpgStubPlanTests
{
    // Preserve the complete original raw-source policy after its POD types moved
    // into separate files. These identities define the policy, not a file layout.
    private static string[] InvlpgSourceOwners() =>
    [
        typeof(X64InvlpgStubPlan).FullName!, typeof(X64InvlpgStubPhase).FullName!,
        typeof(X64InvlpgStubPlanStatus).FullName!, typeof(X64InvlpgStubPlanResult).FullName!,
        typeof(X64InvlpgInvocationRequest).FullName!, typeof(X64InvlpgInvocationPlanResult).FullName!
    ];

    private static bool CheckNoLiveInvocationBoundary(CSharpSourceCatalog kernel)
    {
        string source = HostSourceText.Documents(kernel, InvlpgSourceOwners());
        return X64InvlpgStubPlan.UsesWindowsX64Abi
            && X64InvlpgStubPlan.TargetPassedInRcx
            && !X64InvlpgStubPlan.TouchesNonvolatileRegisters
            && !X64InvlpgStubPlan.TouchesXmm6ThroughXmm15
            && !X64InvlpgStubPlan.HasExternalRelocations
            && !X64InvlpgStubPlan.PerformsLiveInvalidation
            && !X64InvlpgStubPlan.ReloadsCr3
            && !X64InvlpgStubPlan.InvokesStub
            && !X64InvlpgStubPlan.HostTestsInvokeBytes
            && !source.Contains("delegate*", StringComparison.Ordinal)
            && !source.Contains("GetDelegateForFunctionPointer", StringComparison.Ordinal)
            && !source.Contains("DynamicInvoke(", StringComparison.Ordinal)
            && !source.Contains(".Invoke(", StringComparison.Ordinal)
            && !source.Contains("OpCodes.Calli", StringComparison.Ordinal);
    }

    [HostTest("m01.014 invlpg raw source boundary follows moved and inactive family declarations")]
    private static void InvlpgSourceControls()
    {
        var source = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        HostCheck.True(CheckNoLiveInvocationBoundary(source), "actual original invlpg raw family");
        var emit = source.Method(typeof(X64InvlpgStubPlan).FullName!, nameof(X64InvlpgStubPlan.Emit));
        var document = source.Document(emit);
        var parent = (ClassDeclarationSyntax)emit.Parent!;
        var partial = parent.Modifiers.Any(SyntaxKind.PartialKeyword) ? parent
            : parent.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var unit = document.Syntax.GetRoot();
        string remaining = unit.ReplaceNode(parent, partial.WithMembers(parent.Members.Remove(emit)))
            .NormalizeWhitespace().ToFullString();
        string moved = unit.ReplaceNode(parent, partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(emit)))
            .NormalizeWhitespace().ToFullString();
        HostCheck.True(CheckNoLiveInvocationBoundary(Change(document.Path, remaining, moved)),
            "emitter moved to an arbitrary partial source");
        foreach (string forbidden in new[] { "delegate*", "GetDelegateForFunctionPointer", "DynamicInvoke(", ".Invoke(", "OpCodes.Calli" })
        {
            string hidden = "#if UNRELATED_DISABLED\nnamespace OS.Kernel.Memory; public static unsafe partial class X64InvlpgStubPlan { // "
                + forbidden + "\n}\n#endif\n";
            HostCheck.True(!CheckNoLiveInvocationBoundary(Change(document.Path, document.Text, hidden)),
                "inactive partial retains raw exclusion " + forbidden);
        }
        var resultField = source.Type(typeof(X64InvlpgStubPlanResult).FullName!)
            .GetMembers(nameof(X64InvlpgStubPlanResult.StructureSize)).OfType<IFieldSymbol>().Single();
        var resultDocument = source.Document(resultField.DeclaringSyntaxReferences.Single().GetSyntax());
        HostCheck.True(!CheckNoLiveInvocationBoundary(Change(resultDocument.Path,
            resultDocument.Text + "\n// DynamicInvoke(\n")), "extracted result remains in original raw boundary");

        CSharpSourceCatalog Change(string path, string replacement, string? added = null)
        {
            var original = source.Documents.Single(item => item.Path == path);
            var parse = (CSharpParseOptions)original.Syntax.Options;
            string addedPath = Path.Combine(Path.GetDirectoryName(path)!, "ArbitraryInvlpgPart.cs");
            var documents = source.Documents.Select(item => item.Path == path
                ? CSharpSourceDocument.FromBytes(path, Encoding.UTF8.GetBytes(replacement), parse) : item).ToList();
            if (added is not null)
                documents.Add(CSharpSourceDocument.FromBytes(addedPath, Encoding.UTF8.GetBytes(added), parse));
            var result = new CSharpSourceCatalog(documents, source.Compilation.References,
                source.Compilation.AssemblyName!, source.Compilation.Options);
            foreach (var changed in result.Documents.Where(item => item.Path == path || item.Path == addedPath))
                HostCheck.True(!result.Compilation.GetSemanticModel(changed.Syntax).GetDiagnostics()
                    .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "invlpg mutation is valid code");
            return result;
        }
    }
}
