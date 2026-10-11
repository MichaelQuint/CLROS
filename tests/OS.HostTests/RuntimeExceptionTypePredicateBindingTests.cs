using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static class RuntimeExceptionTypePredicateBindingTests
{
    private static MethodDeclarationSyntax Export(params string[] symbols)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var unit = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "src/OS/Runtime/MinimalRuntime.cs")),
            new CSharpParseOptions(preprocessorSymbols: symbols)).GetCompilationUnitRoot();
        return unit.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "TypeCast")
            .Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == "IsInstanceOfException");
    }

    [HostTest("runtime.exception-predicate.profile-only-export")]
    private static void ProfileOnlyExport()
    {
        foreach (string symbol in new[] { "", "M04_SIMPLE_THROW_CATCH_PROBE", "M04_SINGLE_FINALLY_CATCH_PROBE",
            "M04_RETHROW_PROBE", "M04_NESTED_FINALLY_CATCH_PROBE" })
        {
            var method = symbol.Length == 0 ? Export("NOSTDLIB_EFI") : Export("NOSTDLIB_EFI", symbol);
            var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(c => c.Expression.ToString()).ToArray();
            HostCheck.True(calls.SequenceEqual(new[] { "RuntimeFailFast.Halt" }), "unselected export remains terminal " + symbol);
            HostCheck.True(method.DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression!.ToString() == "false");
        }
        var withoutShell = Export("M04_UNHANDLED_EXCEPTION_PROBE");
        HostCheck.True(withoutShell.DescendantNodes().OfType<InvocationExpressionSyntax>().Single().Expression.ToString() == "RuntimeFailFast.Halt");
    }

    [HostTest("runtime.exception-predicate.export-argument-contract")]
    private static void ExportArgumentContract()
    {
        var method = Export("NOSTDLIB_EFI", "M04_UNHANDLED_EXCEPTION_PROBE");
        HostCheck.True(method.ReturnType.ToString() == "bool" && method.ParameterList.Parameters.Count == 2);
        HostCheck.True(method.ParameterList.Parameters[0].Type!.ToString() == "Internal.Runtime.MethodTable*" &&
            method.ParameterList.Parameters[0].Identifier.Text == "targetType" &&
            method.ParameterList.Parameters[1].Type!.ToString() == "object" && method.ParameterList.Parameters[1].Identifier.Text == "value");
        var call = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        HostCheck.True(call.Expression.ToString() == "OS.Runtime.RhpNewFastAllocation.MatchRetainedExceptionType" &&
            call.ArgumentList.Arguments.Count == 2 && call.ArgumentList.Arguments[0].Expression is CastExpressionSyntax cast &&
            cast.Type.ToString() == "ulong" && cast.Expression.ToString() == "targetType" &&
            call.ArgumentList.Arguments[1].Expression.ToString() == "value", "target/object order must match the native helper ABI");
        HostCheck.True(method.AttributeLists.ToFullString().Contains("RuntimeExport(\"RhTypeCast_IsInstanceOfException\")", StringComparison.Ordinal));
        HostCheck.True(method.Body!.Statements.Count == 1 && method.Body.Statements[0] is ReturnStatementSyntax);
    }
}
