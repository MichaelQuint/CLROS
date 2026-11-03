using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;
using System.Text;

internal static partial class M02Ud2VectorStateEfiIntegrationTests
{
    private static CSharpSourceCatalog Ud2Sources() => HostSourceCatalog.Efi(
        EfiOptions.Parse(["--EnableM02Ud2VectorStateProbe"], true).Profile());

    private static MethodDeclarationSyntax Ud2Method(CSharpSourceCatalog source, string name,
        string owner = "OS.Boot.EfiEntry", Accessibility access = Accessibility.Private)
    {
        SpecialType returns = name switch
        {
            "EfiMain" => SpecialType.System_Int64,
            "GetM02Ud2VectorStateCleanupPlan" => SpecialType.System_Byte,
            "EnterM02Ud2VectorStateProbe" or "EmitSuccessTranscript" or "EmitFailureTranscript" => SpecialType.System_Void,
            _ => SpecialType.System_Boolean
        };
        var method = source.Method(owner, name, symbol => symbol.IsStatic && symbol.Arity == 0
            && symbol.DeclaredAccessibility == access && symbol.ReturnType.SpecialType == returns
            && !symbol.ReturnsByRef && !symbol.ReturnsByRefReadonly);
        _ = source.MethodInvocations(method);
        return method;
    }

    private static MethodDeclarationSyntax Ud2Caller(CSharpSourceCatalog source)
    {
        _ = Ud2Method(source, "EnterM02Ud2VectorStateProbe");
        var call = HostSourceOperations.CallsTo(source, "OS.Boot.EfiEntry", "EnterM02Ud2VectorStateProbe").Single();
        var method = call.Ancestors().OfType<MethodDeclarationSyntax>().First();
        var symbol = source.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;
        HostCheck.True(SymbolEqualityComparer.Default.Equals(symbol.ContainingType,
                source.Type("OS.Boot.EfiEntry+M01StageOneAdapter"))
            && symbol.IsStatic && symbol.Arity == 0 && symbol.ReturnsVoid
            && symbol.DeclaredAccessibility == Accessibility.Private, "UD2 stage-one caller ownership");
        _ = source.MethodInvocations(method);
        return method;
    }

    private static string Ud2Constants(CSharpSourceCatalog source)
    {
        string[] names =
        [
            "M02Ud2VectorStateEfiLoaderCode", "M02Ud2VectorStateEfiLoaderData",
            "M02Ud2VectorStatePageSize", "M02Ud2VectorStateCodePageCount", "M02Ud2VectorStateDataPageCount",
            "M02Ud2VectorStateCodeByteCount", "M02Ud2VectorStateDataByteCount",
            "M02Ud2VectorStatePreparedState", "M02Ud2VectorStateEnteredState"
        ];
        var owner = source.Type("OS.Boot.EfiEntry");
        return string.Join("\n", names.Select(name =>
        {
            var field = owner.GetMembers(name).OfType<IFieldSymbol>().Single();
            HostCheck.True(field.IsConst && field.DeclaredAccessibility == Accessibility.Private, "UD2 private constant " + name);
            return field.DeclaringSyntaxReferences.Single().GetSyntax().AncestorsAndSelf()
                .OfType<FieldDeclarationSyntax>().First().ToString();
        }));
    }

    private static IfStatementSyntax Ud2ExistingState(MethodDeclarationSyntax preparation) =>
        preparation.DescendantNodes().OfType<IfStatementSyntax>().Single(statement =>
            HostSourceText.Contains(statement.Condition.ToString(), "serviceEvaluation.Status =="));

    private static string Ud2PostInvocation(MethodDeclarationSyntax entry)
    {
        var invocation = entry.DescendantNodes().OfType<InvocationExpressionSyntax>().Single(node =>
            node.Expression is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == "terminalEntry");
        // A token position is not a character offset. Keep the original raw tail
        // so inactive text and comments still participate in the exclusions.
        return entry.ToString()[(invocation.SpanStart - entry.SpanStart)..];
    }

    private static string Ud2AllBranchCode(MethodDeclarationSyntax method) => method.ReplaceTrivia(
        method.DescendantTrivia(descendIntoTrivia: true).Where(trivia => trivia.IsDirective),
        (trivia, _) => SyntaxFactory.Whitespace(new string(trivia.ToFullString()
            .Select(character => character is '\r' or '\n' ? character : ' ').ToArray()))).ToString();

    private static int LastUd2Token(string source, string fragment)
    {
        int last = -1;
        int cursor = 0;
        while ((cursor = HostSourceText.IndexOf(source, fragment, cursor)) >= 0)
        {
            last = cursor;
            cursor++;
        }
        return last;
    }

    [HostTest("m02.013 raw occurrence guards retain comments and inactive code")]
    private static void Ud2RawCountControls()
    {
        foreach (string fragment in new[] { "PrepareM02Ud2VectorStateProbe(",
            "FailM02Ud2VectorStateBeforeEntry(", "AllocatePages(", "bootServices->FreePages(",
            "X64InstructionPublication.Serialize()", ".EvaluatePublication(", "terminalEntry(context);" })
        {
            string raw = "private static void M() {\n// " + fragment
                + "\n#if UNRELATED_DISABLED\n// " + fragment + "\n#endif\n}";
            var method = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration(raw,
                options: new CSharpParseOptions(LanguageVersion.CSharp14))!;
            HostCheck.True(CountOccurrences(method.ToString(), fragment) == 2,
                "comments and disabled text cannot escape raw occurrence limits " + fragment);
            HostCheck.True(CountOccurrences(Ud2AllBranchCode(method), fragment) == 2,
                "all-branch caller view retains raw occurrence limits " + fragment);
        }
    }

    [HostTest("m02.013 source discovery follows partial files and retains ownership and raw tails")]
    private static void Ud2DiscoveryControls()
    {
        const string parent = "namespace OS.Boot; public static partial class EfiEntry { ";
        const string target = "private static void EnterM02Ud2VectorStateProbe() { }";
        const string call = "EnterM02Ud2VectorStateProbe();";
        var parse = new CSharpParseOptions(LanguageVersion.CSharp14);
        var references = new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
        foreach (var (caller, expected) in new[]
        {
            ("private partial class M01StageOneAdapter { private static void RenamedCaller() { " + call + " } }", true),
            ("private partial class M01StageOneAdapter { private static void RenamedCaller() { " + call + call + " } }", false),
            ("private partial class M01StageOneAdapter { private class Foreign { private static void Call() { " + call + " } } }", false),
            ("private partial class M01StageOneAdapter { public static void Call() { " + call + " } }", false),
            ("private partial class M01StageOneAdapter { private static void Call<T>() { " + call + " } }", false)
        })
        {
            var source = new CSharpSourceCatalog([
                CSharpSourceDocument.FromBytes("arbitrary-target.cs", Encoding.UTF8.GetBytes(parent + target + " }"), parse),
                CSharpSourceDocument.FromBytes("moved-caller.cs", Encoding.UTF8.GetBytes(parent + caller + " }"), parse)
            ], references);
            HostCheck.True(!source.Compilation.GetDiagnostics().Any(item => item.Severity == DiagnosticSeverity.Error), "UD2 caller control binds");
            bool accepted;
            try
            {
                _ = Ud2Caller(source);
                accepted = true;
            }
            catch (InvalidOperationException)
            {
                accepted = false;
            }
            HostCheck.True(accepted == expected, "UD2 caller uniqueness and owner");
        }

        var entry = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("""
            private static void EnterM02Ud2VectorStateProbe() {
                // return before invocation does not belong to the tail.
                terminalEntry ( context );
            #if UNRELATED_DISABLED
                // bootServices->
            #endif
                SpinM02Ud2VectorStateProbe();
            }
            """, options: parse)!;
        string tail = Ud2PostInvocation(entry);
        HostCheck.True(!tail.Contains("return", StringComparison.Ordinal)
            && tail.Contains("bootServices->", StringComparison.Ordinal), "original raw tail excludes prior text and retains inactive policy");
        HostCheck.True(HostSourceText.IndexOf(entry.ToString(), "terminalEntry(context);")
            < LastUd2Token(entry.ToString(), "SpinM02Ud2VectorStateProbe();"), "spacing does not alter operation order");

        var branches = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("""
            private static void Branches() {
                First();
            #if FIRST_DISABLED
                Second();
            #elif SECOND_DISABLED
                Third();
            #else
                Fourth();
            #endif
                Fifth();
            }
            """, options: parse)!;
        string allBranches = Ud2AllBranchCode(branches);
        string[] operations = ["First();", "Second();", "Third();", "Fourth();", "Fifth();"];
        var positions = operations.Select(operation => HostSourceText.IndexOf(allBranches, operation)).ToArray();
        HostCheck.True(positions.All(position => position >= 0) && positions.SequenceEqual(positions.Order())
            && operations.All(operation => HostSourceText.Count(allBranches, operation) == 1),
            "original cross-profile raw order retains inactive and alternate branches");

        var preparation = (MethodDeclarationSyntax)SyntaxFactory.ParseMemberDeclaration("""
            private static bool PrepareM02Ud2VectorStateProbe() {
                if (serviceEvaluation.Status == ExistingState) { return false; }
                if (!serviceEvaluation.Succeeded) { return false; }
                return true;
            }
            """, options: parse)!;
        HostCheck.True(Ud2ExistingState(preparation).ToString().Contains("ExistingState", StringComparison.Ordinal),
            "existing-state branch discovered independently of following declaration");
    }
}
