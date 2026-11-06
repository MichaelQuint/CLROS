using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text;

internal static class HostRhpAllocationSourceTests
{
    [HostTest("allocation source families follow real partial moves and reject SDK-only overloads")]
    private static void CoreMovesAndOverloads()
    {
        var source = HostSourceCatalog.Efi(EfiOptions.Parse(
        [
            "--EnableM01LoadedImageStackProbe", "--EnableM01LiveAllocatorProbe",
            "--EnableM04IndependentEndianConformanceProbe", "--EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe",
            "--EnableM04ArithmeticConformanceProbe", "--EnableM04LayoutContractProbe",
            "--EnableM04ManagedHeapPageRangeProbe", "--EnableM04RhpNewFastProbe"
        ], true).Profile());
        var original = HostRhpAllocationSources.RuntimeFamily(source);
        var method = source.Method(HostRhpAllocationSources.CoreOwner, "Read64");
        var document = source.Document(method);
        var owner = (TypeDeclarationSyntax)method.Parent!;
        var references = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj").Compilation.References;
        CSharpSourceCatalog Sdk(IEnumerable<CSharpSourceDocument> documents) => new(documents.Select(item =>
            Document(item.Path, item.Text, "RHP_CORE_TEST")), references);

        string retained = document.Text.Remove(method.SpanStart, method.Span.Length);
        string path = Path.Combine(Path.GetDirectoryName(document.Path)!, "ArbitraryAllocationMember.cs");
        var part = Document(path, "using OS.Kernel.Runtime;\nnamespace OS.Runtime;\ninternal static unsafe partial class RhpNewFastAllocationCore\n{\n"
            + method + "\n}\n", ((CSharpParseOptions)document.Syntax.Options).PreprocessorSymbolNames.ToArray());
        var moved = Change(source, document, retained, part);
        var family = HostRhpAllocationSources.RuntimeFamily(moved);
        HostCheck.True(family.Contains(part) && family.Count == original.Count + 1);
        Valid(Sdk(family));

        // Each mutant is valid in the SDK symbol context before the independent
        // family policy rejects its unreviewed overload in either selected part.
        string overload = "#if RHP_CORE_TEST\nprivate static ulong Read64(ulong value) => value;\n#endif\n";
        var field = source.Type("OS.Runtime.RhpAllocationControl").GetMembers("CanaryBefore").OfType<IFieldSymbol>().Single();
        var other = source.Document(field.DeclaringSyntaxReferences.Single().GetSyntax());
        foreach (var (target, replacement) in new[]
        {
            (document, document.Text.Insert(owner.CloseBraceToken.SpanStart, overload)),
            (other, other.Text + "\ninternal static unsafe partial class RhpNewFastAllocationCore\n{\n" + overload + "}\n")
        })
        {
            var changed = Change(source, target, replacement);
            var sdk = Sdk(original.Select(item => item == target ? changed.Documents.Single(candidate => candidate.Path == target.Path) : item));
            Valid(sdk);
            HostCheck.Equal(2, sdk.Type(HostRhpAllocationSources.CoreOwner).GetMembers("Read64").Length);
            Reject(() => HostRhpAllocationSources.RuntimeFamily(changed));
        }
    }

    [HostTest("allocation Program discovery follows conditional partials and rejects foreign context")]
    private static void ProgramContexts()
    {
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        const string symbol = "RHP_CORE_TEST", program = "RhpCoreTestProgram";
        const string original = "#if RHP_CORE_TEST\ninternal static partial class RhpCoreTestProgram { internal static int Value => 1; }\n#endif\n";
        const string moved = "#if RHP_CORE_TEST && NET11_0\ninternal static partial class RhpCoreTestProgram { internal static int Other => 2; }\n#endif\n";
        CSharpSourceCatalog Context(string first, string? second, params string[] symbols) => new(
            second is null ? [Document("ProgramContext.cs", first, symbols)] :
            [Document("ProgramContext.cs", first, symbols), Document("ArbitraryProgramPart.cs", second, symbols)], references);
        var host = Context(original, moved);
        Valid(Context(original, moved, symbol, "NET11_0"));
        HostCheck.Equal(2, HostRhpAllocationSources.ProgramFamily(host, program, symbol).Count);
        foreach (string extra in new[]
        {
            "#if NET11_0\ninternal class Foreign {}\n#endif\n",
            "#if X && Y && Z\nnamespace Foreign { }\n#endif\n",
            "#if RHP_CORE_TEST\n[assembly: System.CLSCompliant(false)]\n#endif\n",
            "#if RHP_CORE_TEST\nglobal using System;\n#endif\n"
        })
        {
            Valid(Context(extra + original, null, symbol, "NET11_0", "X", "Y", "Z"));
            Reject(() => HostRhpAllocationSources.ProgramFamily(Context(extra + original, null), program, symbol));
        }
        foreach (string declaration in new[] { "public static class", "file static class", "internal struct", "internal record" })
        {
            string text = "#if RHP_CORE_TEST\n" + declaration + " RhpCoreTestProgram {}\n#endif\n";
            Valid(Context(text, null, symbol));
            Reject(() => HostRhpAllocationSources.ProgramFamily(Context(text, null), program, symbol));
        }
        Reject(() => HostRhpAllocationSources.ProgramFamily(Context(original, null), program, "WRONG_TEST_SYMBOL"));
    }

    [HostTest("allocation Array ABI requires the original by-value field getter")]
    private static void ArrayShape()
    {
        foreach (string body in new[] { "int Length => _numComponents", "int Length => 0",
            "ref int Length => ref _numComponents", "ref readonly int Length => ref _numComponents" })
        {
            var source = new CSharpSourceCatalog([Document("ArrayShape.cs",
                "namespace System { public class Array { private int _numComponents; public " + body + "; } }")],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
            Valid(source);
            if (body == "int Length => _numComponents")
                _ = HostRhpAllocationChecks.ArrayShape(source);
            else
                Reject(() => HostRhpAllocationChecks.ArrayShape(source));
        }
    }

    private static CSharpSourceDocument Document(string path, string text, params string[] symbols) =>
        CSharpSourceDocument.FromBytes(Path.GetFullPath(path), Encoding.UTF8.GetBytes(text),
            new CSharpParseOptions(LanguageVersion.CSharp14, preprocessorSymbols: symbols));

    private static CSharpSourceCatalog Change(CSharpSourceCatalog source, CSharpSourceDocument original,
        string text, CSharpSourceDocument? added = null)
    {
        var documents = source.Documents.Select(item => item == original ? CSharpSourceDocument.FromBytes(item.Path,
            Encoding.UTF8.GetBytes(text), (CSharpParseOptions)item.Syntax.Options) : item);
        return new CSharpSourceCatalog(added is null ? documents : documents.Append(added), source.Compilation.References,
            source.Compilation.AssemblyName!, source.Compilation.Options);
    }

    private static void Valid(CSharpSourceCatalog source) => HostCheck.True(!source.Compilation.GetDiagnostics()
        .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "mutation must bind before selection");

    private static void Reject<T>(Func<T> select)
    {
        bool rejected = false;
        try
        {
            _ = select();
        }
        catch (InvalidOperationException error) when (error.Message.StartsWith("rhp_source_", StringComparison.Ordinal))
        {
            rejected = true;
        }
        HostCheck.True(rejected, "independent allocation contract rejects changed scope or ABI");
    }
}
