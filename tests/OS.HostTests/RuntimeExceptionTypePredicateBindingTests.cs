using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Tooling;

internal static class RuntimeExceptionTypePredicateBindingTests
{
    private static MethodDeclarationSyntax Export(params string[] symbols)
        => ExportMethod("IsInstanceOfException", symbols);

    private static MethodDeclarationSyntax ExportMethod(string name, params string[] symbols)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var unit = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, "src/OS/Runtime/TypeCast.cs")),
            new CSharpParseOptions(preprocessorSymbols: symbols)).GetCompilationUnitRoot();
        return unit.DescendantNodes().OfType<ClassDeclarationSyntax>().Single(c => c.Identifier.Text == "TypeCast")
            .Members.OfType<MethodDeclarationSyntax>().Single(m => m.Identifier.Text == name);
    }

    [HostTest("runtime.exception-predicate.profile-only-export")]
    private static void ProfileOnlyExport()
    {
        foreach (string symbol in new[] { "", "M04_SIMPLE_THROW_CATCH_PROBE", "M04_SINGLE_FINALLY_CATCH_PROBE",
            "M04_RETHROW_PROBE", "M04_NESTED_FINALLY_CATCH_PROBE", "M04_FILTERED_CATCH_PROBE" })
        {
            var method = symbol.Length == 0 ? Export("NOSTDLIB_EFI") : Export("NOSTDLIB_EFI", symbol);
            var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(c => c.Expression.ToString()).ToArray();
            HostCheck.True(calls.SequenceEqual(new[] { "RuntimeFailFast.Halt" }), "unselected export remains terminal " + symbol);
            HostCheck.True(method.DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression!.ToString() == "false");
        }
        foreach(string symbol in new[]{"M04_UNHANDLED_EXCEPTION_PROBE","M04_FRAME_DISPATCH_PROBE","M04_CONSTRUCTOR_FAILURE_PROBE"})
        {
            var withoutShell = Export(symbol);
            HostCheck.True(withoutShell.DescendantNodes().OfType<InvocationExpressionSyntax>().Single().Expression.ToString() == "RuntimeFailFast.Halt");
        }
    }

    [HostTest("runtime.exception-predicate.export-argument-contract")]
    private static void ExportArgumentContract()
    {
        foreach(string symbol in new[]{"M04_UNHANDLED_EXCEPTION_PROBE","M04_FRAME_DISPATCH_PROBE","M04_CONSTRUCTOR_FAILURE_PROBE"})
        {
        var method = Export("NOSTDLIB_EFI", symbol);
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

    [HostTest("runtime.class-predicate.filtered-profile-only-argument-contract")]
    private static void FilteredClassPredicate()
    {
        foreach (string symbol in new[] { "", "M04_SIMPLE_THROW_CATCH_PROBE", "M04_SINGLE_FINALLY_CATCH_PROBE",
            "M04_RETHROW_PROBE", "M04_NESTED_FINALLY_CATCH_PROBE", "M04_UNHANDLED_EXCEPTION_PROBE",
            "M04_FRAME_DISPATCH_PROBE", "M04_CONSTRUCTOR_FAILURE_PROBE" })
        {
            var disabled = symbol.Length == 0 ? ExportMethod("IsInstanceOfClass", "NOSTDLIB_EFI")
                : ExportMethod("IsInstanceOfClass", "NOSTDLIB_EFI", symbol);
            HostCheck.True(disabled.DescendantNodes().OfType<InvocationExpressionSyntax>().Single().Expression.ToString() == "RuntimeFailFast.Halt");
            HostCheck.True(disabled.DescendantNodes().OfType<ReturnStatementSyntax>().Single().Expression!.ToString() == "value");
        }
        var withoutShell = ExportMethod("IsInstanceOfClass", "M04_FILTERED_CATCH_PROBE");
        HostCheck.True(withoutShell.DescendantNodes().OfType<InvocationExpressionSyntax>().Single().Expression.ToString() == "RuntimeFailFast.Halt");
        var method = ExportMethod("IsInstanceOfClass", "NOSTDLIB_EFI", "M04_FILTERED_CATCH_PROBE");
        HostCheck.True(method.ReturnType.ToString() == "object" && method.ParameterList.Parameters.Count == 2 &&
            method.ParameterList.Parameters[0].Type!.ToString() == "Internal.Runtime.MethodTable*" &&
            method.ParameterList.Parameters[0].Identifier.Text == "targetType" &&
            method.ParameterList.Parameters[1].Type!.ToString() == "object" && method.ParameterList.Parameters[1].Identifier.Text == "value");
        var call = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Single();
        HostCheck.True(call.Expression.ToString() == "OS.Runtime.RhpNewFastAllocation.MatchRetainedExceptionType" &&
            call.ArgumentList.Arguments.Count == 2 && call.ArgumentList.Arguments[0].Expression is CastExpressionSyntax cast &&
            cast.Type.ToString() == "ulong" && cast.Expression.ToString() == "targetType" &&
            call.ArgumentList.Arguments[1].Expression.ToString() == "value", "class predicate target/object ABI");
        HostCheck.True(method.AttributeLists.ToFullString().Contains("RuntimeExport(\"RhTypeCast_IsInstanceOfClass\")", StringComparison.Ordinal));
        HostCheck.True(method.Body!.Statements.Count == 1 && method.Body.Statements[0] is ReturnStatementSyntax statement &&
            statement.Expression is ConditionalExpressionSyntax conditional && conditional.Condition.ToString() == call.ToString() &&
            conditional.WhenTrue.ToString() == "value" && conditional.WhenFalse.IsKind(SyntaxKind.NullLiteralExpression),
            "bounded class predicate returns the original object or null");
    }
}
