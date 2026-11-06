using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static partial class M02DoubleFaultIstEfiIntegrationTests
{
    private const string AdapterOwner = "OS.Boot.EfiEntry+M02DoubleFaultIstAdapter";

    private static CSharpSourceCatalog DoubleFaultSources() =>
        HostSourceCatalog.Efi(EfiOptions.Parse(["--" + Flag], true).Profile());

    private static Type[] DoubleFaultOwners() =>
    [
        typeof(DoubleFaultIstOperation), typeof(DoubleFaultIstStatus), typeof(DoubleFaultIstRequest),
        typeof(DoubleFaultIstResult), typeof(DoubleFaultIstObservation), typeof(DoubleFaultIstBuffers),
        typeof(DoubleFaultIstWalk), typeof(DoubleFaultISTPathSilentRebootOrTriple)
    ];

    private static Type[] PageTableOwners() =>
    [
        typeof(SeparatePageFaultPageTableStatus), typeof(SeparatePageFaultMappingRange),
        typeof(SeparatePageFaultPageTableRequest), typeof(SeparatePageFaultPageTableResult),
        typeof(SeparateDisabledDefaultPageFaultPageTables)
    ];

    private static Type[] TransitionOwners() =>
    [
        typeof(SeparatePageFaultTransitionOperation), typeof(SeparatePageFaultTransitionStatus),
        typeof(SeparatePageFaultTransitionRequest), typeof(SeparatePageFaultTransitionResult),
        typeof(SeparateDisabledDefaultPageFaultTransition)
    ];

    private static MethodDeclarationSyntax DoubleFaultMethod(CSharpSourceCatalog source, string name, SpecialType result)
    {
        var method = source.Method(AdapterOwner, name, symbol => symbol.IsStatic && symbol.Arity == 0
            && !symbol.ReturnsByRef && !symbol.ReturnsByRefReadonly
            && symbol.ReturnType.SpecialType == result && symbol.DeclaredAccessibility == Accessibility.Internal);
        _ = source.MethodInvocations(method);
        return method;
    }

    private static string DoubleFaultHostAdapter() =>
        HostEfiAdapterSources.ForHost(DoubleFaultSources(), AdapterOwner, "FailBeforeEntry", "SamePrepared");

    private static bool DoubleFaultFlow(MethodDeclarationSyntax method)
    {
        string raw = method.ToString();
        string run = HostSourceText.Active(method);
        var attempts = method.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.IsKind(SyntaxKind.SimpleAssignmentExpression)
                && assignment.Left is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == "s_state"
                && assignment.Right is LiteralExpressionSyntax literal && literal.Token.Value is int value && value == 2)
            .ToArray();
        if (attempts.Length != 1)
            return false;
        // Retain the original conservative raw tail, including comments and
        // inactive text; token positions are not character offsets.
        int attempt = raw.IndexOf("s_state = 2;", StringComparison.Ordinal);
        if (attempt < 0 || attempt != attempts[0].SpanStart - method.SpanStart)
            return false;
        return HostSourceText.Contains(run, "if (s_state != 0) { Failure(console, serial); Spin(); }")
            && HostSourceText.Contains(run, "X64InstructionPublication.Serialize() != X64InstructionPublication.ExpectedSuccessCookie")
            && HostSourceText.Contains(run, "(c + Page + OS.Kernel.Diagnostics.DoubleFaultISTPathSilentRebootOrTriple.EntryOffset)")
            && HostSourceText.Contains(run, "enter((byte*)(d + Page));")
            && !raw[attempt..].Contains("FreePages", StringComparison.Ordinal)
            && !raw[attempt..].Contains("FailBeforeEntry", StringComparison.Ordinal)
            && method.DescendantNodes().OfType<WhileStatementSyntax>().Any(loop => loop.SpanStart > attempts[0].SpanStart
                && loop.Condition.IsKind(SyntaxKind.TrueLiteralExpression) && loop.Statement is BlockSyntax { Statements.Count: 0 });
    }
}
