using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Memory;
using OS.Tooling;
using System.Text;

internal static unsafe partial class X64ProtectionProbeStubPlanTests
{
    private static string[] ProtectionSourceOwners() =>
    [
        typeof(X64ProtectionProbeStubPlan).FullName!, typeof(X64ProtectionProbeStubPhase).FullName!,
        typeof(X64ProtectionProbeTerminalKind).FullName!, typeof(X64ProtectionProbeEventKind).FullName!,
        typeof(X64ProtectionProbeStatus).FullName!, typeof(X64ProtectionProbeStubPlanResult).FullName!,
        typeof(X64ProtectionProbeEvent).FullName!, typeof(X64ProtectionProbeObservation).FullName!,
        typeof(X64ProtectionProbePlanResult).FullName!
    ];

    private static bool CheckSourceBoundary(CSharpSourceCatalog kernel, CSharpSourceCatalog host)
    {
        string production = HostSourceText.Documents(kernel, ProtectionSourceOwners());
        string testSource = HostSourceText.Documents(host, typeof(X64ProtectionProbeStubPlanTests).FullName!);
        return production.Contains("PerformsLiveProtectionChange = false", StringComparison.Ordinal)
            && production.Contains("PerformsLiveExecution = false", StringComparison.Ordinal)
            && production.Contains("HostTestsInvokeBytes = false", StringComparison.Ordinal)
            && production.Contains("IsLiveEvidence = false", StringComparison.Ordinal)
            && !production.Contains("delegate" + "*", StringComparison.Ordinal)
            && !production.Contains("VirtualProtect", StringComparison.Ordinal)
            && !production.Contains("GetDelegateForFunctionPointer", StringComparison.Ordinal)
            && !testSource.Contains("delegate" + "*", StringComparison.Ordinal)
            && !X64ProtectionProbeStubPlan.PerformsLiveProtectionChange
            && !X64ProtectionProbeStubPlan.PerformsLiveExecution
            && !X64ProtectionProbeStubPlan.HostTestsInvokeBytes
            && !X64ProtectionProbeStubPlan.IsLiveEvidence;
    }

    [HostTest("m01.014 protection raw source boundary follows extracted and inactive family declarations")]
    private static void ProtectionSourceControls()
    {
        var kernel = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var host = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        HostCheck.True(CheckSourceBoundary(kernel, host), "actual production and host raw families");
        var emit = kernel.Method(typeof(X64ProtectionProbeStubPlan).FullName!, nameof(X64ProtectionProbeStubPlan.Emit));
        var document = kernel.Document(emit);
        var parent = (ClassDeclarationSyntax)emit.Parent!;
        var partial = parent.Modifiers.Any(SyntaxKind.PartialKeyword) ? parent
            : parent.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        var unit = document.Syntax.GetRoot();
        string remaining = unit.ReplaceNode(parent, partial.WithMembers(parent.Members.Remove(emit)))
            .NormalizeWhitespace().ToFullString();
        string moved = unit.ReplaceNode(parent, partial.WithMembers(SyntaxFactory.SingletonList<MemberDeclarationSyntax>(emit)))
            .NormalizeWhitespace().ToFullString();
        HostCheck.True(CheckSourceBoundary(Change(kernel, document.Path, remaining, moved), host),
            "emitter moved to an arbitrary partial source");
        foreach (string forbidden in new[] { "delegate" + "*", "VirtualProtect", "GetDelegateForFunctionPointer" })
        {
            string hidden = "#if UNRELATED_DISABLED\nnamespace OS.Kernel.Memory; public static unsafe partial class X64ProtectionProbeStubPlan { // "
                + forbidden + "\n}\n#endif\n";
            HostCheck.True(!CheckSourceBoundary(Change(kernel, document.Path, document.Text, hidden), host),
                "inactive production partial retains raw exclusion " + forbidden);
        }
        var observationField = kernel.Type(typeof(X64ProtectionProbeObservation).FullName!)
            .GetMembers(nameof(X64ProtectionProbeObservation.StructureSize)).OfType<IFieldSymbol>().Single();
        var observation = kernel.Document(observationField.DeclaringSyntaxReferences.Single().GetSyntax());
        HostCheck.True(!CheckSourceBoundary(Change(kernel, observation.Path,
            observation.Text + "\n// VirtualProtect\n"), host), "extracted observation remains in production raw boundary");

        var testMethod = host.Method(typeof(X64ProtectionProbeStubPlanTests).FullName!, "TestSourceBoundary");
        var testDocument = host.Document(testMethod);
        string hiddenTest = "#if UNRELATED_DISABLED\ninternal static unsafe partial class X64ProtectionProbeStubPlanTests { // "
            + "delegate" + "*\n}\n#endif\n";
        HostCheck.True(!CheckSourceBoundary(kernel, Change(host, testDocument.Path, testDocument.Text, hiddenTest)),
            "inactive host partial retains the original function-pointer exclusion");
        var fixtureDocument = FixtureDocument(host);
        HostCheck.True(!CheckSourceBoundary(kernel, Change(host, fixtureDocument.Path,
            fixtureDocument.Text + "\n// " + "delegate" + "*\n")), "extracted host helper remains in raw boundary");

        var field = host.Type(typeof(X64ProtectionProbeStubPlanTests).FullName! + "+ProtectionFixture")
            .GetMembers("InvlpgEntryOffset").OfType<IFieldSymbol>().Single();
        var fixtureOwner = field.DeclaringSyntaxReferences.Single().GetSyntax().Ancestors()
            .OfType<ClassDeclarationSyntax>().First();
        var fixturePartial = fixtureOwner.Modifiers.Any(SyntaxKind.PartialKeyword) ? fixtureOwner
            : fixtureOwner.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword));
        string fixtureText = fixtureDocument.Syntax.GetRoot().ReplaceNode(fixtureOwner, fixturePartial)
            .NormalizeWhitespace().ToFullString();
        const string fixturePart = "internal static unsafe partial class X64ProtectionProbeStubPlanTests { private sealed unsafe partial class ProtectionFixture { } }";
        var splitHost = Change(host, fixtureDocument.Path, fixtureText, fixturePart);
        HostCheck.True(FixtureDocument(splitHost).Path == fixtureDocument.Path
            && CheckSourceBoundary(kernel, splitHost), "field-owned source selection accepts a multi-part nested helper");

        static CSharpSourceDocument FixtureDocument(CSharpSourceCatalog source)
        {
            var owner = source.Type(typeof(X64ProtectionProbeStubPlanTests).FullName! + "+ProtectionFixture");
            var offset = owner.GetMembers("InvlpgEntryOffset").OfType<IFieldSymbol>().Single();
            return source.Document(offset.DeclaringSyntaxReferences.Single().GetSyntax());
        }

        CSharpSourceCatalog Change(CSharpSourceCatalog source, string path, string replacement, string? added = null)
        {
            var original = source.Documents.Single(item => item.Path == path);
            var parse = (CSharpParseOptions)original.Syntax.Options;
            string addedPath = Path.Combine(Path.GetDirectoryName(path)!, "ArbitraryProtectionPart.cs");
            var documents = source.Documents.Select(item => item.Path == path
                ? CSharpSourceDocument.FromBytes(path, Encoding.UTF8.GetBytes(replacement), parse) : item).ToList();
            if (added is not null)
                documents.Add(CSharpSourceDocument.FromBytes(addedPath, Encoding.UTF8.GetBytes(added), parse));
            var result = new CSharpSourceCatalog(documents, source.Compilation.References,
                source.Compilation.AssemblyName!, source.Compilation.Options);
            foreach (var changed in result.Documents.Where(item => item.Path == path || item.Path == addedPath))
                HostCheck.True(!result.Compilation.GetSemanticModel(changed.Syntax).GetDiagnostics()
                    .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "protection mutation is valid code");
            return result;
        }
    }
}
