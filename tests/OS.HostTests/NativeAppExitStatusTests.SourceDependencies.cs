using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Kernel.Runtime;
using OS.Std.Text;
using OS.Tooling;

internal static partial class NativeAppExitStatusTests
{
    [HostTest("appmodel.exit status parser uses shared memory helpers", HostLane.Fast, 7)]
    private static bool TestParserUsesSharedMemoryHelpers()
        => HostCheck.Result(ParserDependencies(HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj")));

    [HostTest("appmodel.exit status formatter uses shared ascii text builder", HostLane.Fast, 8)]
    private static bool TestFormatterUsesSharedAsciiTextBuilder()
        => HostCheck.Result(FormatterDependencies(HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj")));

    private static bool ParserDependencies(CSharpSourceCatalog catalog)
    {
        string owner = typeof(NativeAppExitStatusFormatter).FullName!;
        var type = catalog.Type(owner);
        if (new[] { "StartsWith", "EqualsBytes" }.Any(name => type.GetMembers(name).Length != 0))
            return false;
        var parse = catalog.Method(owner, nameof(NativeAppExitStatusFormatter.TryParse));
        var model = catalog.Compilation.GetSemanticModel(parse.SyntaxTree);
        var line = ((IMethodSymbol)model.GetDeclaredSymbol(parse)!).Parameters.Single();
        var calls = HostSourceOperations.Calls(catalog, parse, typeof(OS.Std.MemoryExtensions), nameof(OS.Std.MemoryExtensions.StartsWith));
        HashSet<string> prefixes = [];
        foreach (var call in calls)
        {
            if (!InCondition(call, parse) || HostSourceOperations.Argument(call, 1) is not IPropertyReferenceOperation property
                || !SymbolEqualityComparer.Default.Equals(property.Property.ContainingType, type)
                || property.Property.DeclaringSyntaxReferences.Single().GetSyntax()
                    is not PropertyDeclarationSyntax { ExpressionBody.Expression: LiteralExpressionSyntax literal }
                || !literal.IsKind(SyntaxKind.Utf8StringLiteralExpression))
                return false;
            var input = HostSourceOperations.Argument(call, 0);
            if (literal.Token.ValueText == " status=")
            {
                var initializer = HostSourceOperations.LocalInitializer(model, input);
                if (initializer?.Syntax is not ElementAccessExpressionSyntax element
                    || element.ArgumentList.Arguments is not [{ Expression: RangeExpressionSyntax { RightOperand: null } }]
                    || model.GetOperation(element.Expression) is not { } receiver
                    || !HostSourceOperations.IsParameter(receiver, line))
                    return false;
            }
            else if (!HostSourceOperations.IsParameter(input, line))
                return false;
            if (!prefixes.Add(literal.Token.ValueText))
                return false;
        }
        if (!prefixes.SetEquals(["APP_EXIT", "APP_EXIT code=", " status="]))
            return false;
        var tokenMethod = catalog.Method(owner, "TryParseStatusToken");
        var token = ((IMethodSymbol)catalog.Compilation.GetSemanticModel(tokenMethod.SyntaxTree).GetDeclaredSymbol(tokenMethod)!).Parameters[0];
        var comparisons = HostSourceOperations.Calls(catalog, tokenMethod, typeof(OS.Std.MemoryExtensions), nameof(OS.Std.MemoryExtensions.SequenceEqual));
        HashSet<string> literals = [];
        foreach (var call in comparisons)
        {
            if (!InCondition(call, tokenMethod) || !HostSourceOperations.IsParameter(HostSourceOperations.Argument(call, 0), token)
                || HostSourceOperations.Argument(call, 1).Syntax is not LiteralExpressionSyntax literal
                || !literal.IsKind(SyntaxKind.Utf8StringLiteralExpression))
                return false;
            literals.Add(literal.Token.ValueText);
        }
        return new[] { "OSHOST_STATUS_OK", "OSHOST_STATUS_ADDR_IN_USE", "OSHOST_STATUS_ADDRESS_IN_USE",
            "OSHOST_STATUS_ADDR_NOT_AVAILABLE", "OSHOST_STATUS_ADDRESS_NOT_AVAILABLE" }.All(literals.Contains);
    }

    private static bool InCondition(IInvocationOperation call, MethodDeclarationSyntax method)
    {
        IOperation current = call;
        while (current.Parent is IUnaryOperation { OperatorKind: UnaryOperatorKind.Not }
            or IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalOr or BinaryOperatorKind.ConditionalAnd })
            current = current.Parent;
        return current.Parent is IConditionalOperation conditional && conditional.Condition == current
            && current.Syntax.Ancestors().TakeWhile(node => node != method)
                .All(node => node is not AnonymousFunctionExpressionSyntax and not LocalFunctionStatementSyntax);
    }

    private static bool FormatterDependencies(CSharpSourceCatalog catalog)
    {
        string owner = typeof(NativeAppExitStatusFormatter).FullName!;
        var type = catalog.Type(owner);
        if (new[] { "CountDecimalDigits", "WriteDecimal" }.Any(name => type.GetMembers(name).Length != 0))
            return false;
        var format = catalog.Method(owner, nameof(NativeAppExitStatusFormatter.TryFormat));
        var model = catalog.Compilation.GetSemanticModel(format.SyntaxTree);
        var record = ((IMethodSymbol)model.GetDeclaredSymbol(format)!).Parameters[0];
        var counts = HostSourceOperations.Calls(catalog, format, typeof(AsciiDecimalFormatter), nameof(AsciiDecimalFormatter.CountInt32Digits));
        if (counts is not [var count] || count.Arguments[0].Value is not IPropertyReferenceOperation exitCode
            || exitCode.Property.Name != nameof(NativeAppExitStatusRecord.ExitCode)
            || exitCode.Instance is null || !HostSourceOperations.IsParameter(exitCode.Instance, record))
            return false;
        return CountSizesBuffer(model, format, count)
            && HostFormatterSourceGuard.Append(catalog, owner, "TryAppendChars", nameof(AsciiTextBuilder.AppendChars), tryStyle: true)
            && HostFormatterSourceGuard.Append(catalog, owner, "TryAppendUInt64Decimal", nameof(AsciiTextBuilder.AppendUInt64Decimal), tryStyle: true);
    }

    private static bool CountSizesBuffer(SemanticModel model, MethodDeclarationSyntax method, IInvocationOperation count)
    {
        IOperation sum = count;
        while (sum.Parent is IBinaryOperation { OperatorKind: BinaryOperatorKind.Add, OperatorMethod: null } addition)
            sum = addition;
        if (sum.Parent is not IVariableInitializerOperation { Parent: IVariableDeclaratorOperation variable }
            || variable.Syntax.Ancestors().OfType<LocalDeclarationStatementSyntax>().First().Parent != method.Body
            || !HostSourceOperations.IsUnmodifiedLocal(model, variable.Symbol, method))
            return false;
        var destination = ((IMethodSymbol)model.GetDeclaredSymbol(method)!).Parameters[1];
        return method.Body!.Statements.OfType<IfStatementSyntax>().Any(branch =>
            model.GetOperation(branch.Condition) is IBinaryOperation
            {
                OperatorKind: BinaryOperatorKind.LessThan,
                LeftOperand: IPropertyReferenceOperation { Property.Name: "Length", Instance: IParameterReferenceOperation input },
                RightOperand: ILocalReferenceOperation required
            }
            && SymbolEqualityComparer.Default.Equals(input.Parameter, destination)
            && SymbolEqualityComparer.Default.Equals(required.Local, variable.Symbol)
            && ReturnsShortBuffer(branch.Statement));

        bool ReturnsShortBuffer(StatementSyntax statement)
        {
            if (statement is BlockSyntax { Statements.Count: 1 } block)
                statement = block.Statements[0];
            return statement is ReturnStatementSyntax { Expression: { } expression }
                && model.GetOperation(expression) is IObjectCreationOperation creation
                && SymbolEqualityComparer.Default.Equals(creation.Type,
                    model.Compilation.GetTypeByMetadataName(typeof(NativeAppExitMarkerResult).FullName!))
                && creation.Arguments.Length == 4
                && EnumField(creation.Arguments[0].Value, typeof(NativeAppExitMarkerStatus), nameof(NativeAppExitMarkerStatus.DestinationTooSmall))
                && creation.Arguments[1].Value.ConstantValue is { HasValue: true, Value: 0 }
                && creation.Arguments[2].Value is ILocalReferenceOperation size
                && SymbolEqualityComparer.Default.Equals(size.Local, variable.Symbol)
                && EnumField(creation.Arguments[3].Value, typeof(OS.Pal.OSHostStatus), nameof(OS.Pal.OSHostStatus.BufferTooSmall));
        }

        static bool EnumField(IOperation operation, Type owner, string name)
            => operation is IFieldReferenceOperation field && field.Field.Name == name
                && field.Field.ContainingType.ToDisplayString() == owner.FullName
                && field.Field.ContainingAssembly.Name == owner.Assembly.GetName().Name;
    }

    [HostTest("appmodel.exit dependency guards follow moved sources and reject wrong inputs or decoy calls")]
    private static void SourceDependencyControls()
    {
        var catalog = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        string owner = typeof(NativeAppExitStatusFormatter).FullName!;
        var parse = catalog.Method(owner, nameof(NativeAppExitStatusFormatter.TryParse));
        var parseDocument = catalog.Document(parse);
        var moved = Rebuild(parseDocument, parseDocument.Text, parseDocument.Path + ".moved.cs");
        HostCheck.True(ParserDependencies(moved) && FormatterDependencies(moved));
        foreach (var (member, before, after) in new[]
        {
            (nameof(NativeAppExitStatusFormatter.TryParse), "OS.Std.MemoryExtensions.StartsWith(line, AppExitMarker)", "OS.Std.MemoryExtensions.StartsWith(line, AppExitCodePrefix)"),
            (nameof(NativeAppExitStatusFormatter.TryParse), "OS.Std.MemoryExtensions.StartsWith(statusField, AppExitStatusPrefix)", "OS.Std.MemoryExtensions.StartsWith(line, AppExitStatusPrefix)"),
            ("TryParseStatusToken", "OS.Std.MemoryExtensions.SequenceEqual(token, \"OSHOST_STATUS_OK\"u8)", "OS.Std.MemoryExtensions.SequenceEqual(\"other\"u8, \"OSHOST_STATUS_OK\"u8)")
        })
        {
            var document = catalog.Document(catalog.Method(owner, member));
            HostCheck.True(!ParserDependencies(Rebuild(document, HostSourceMutation.ReplaceExpression(document.Text, before, after))));
        }
        foreach (var (member, before, after) in new[]
        {
            (nameof(NativeAppExitStatusFormatter.TryFormat), "AsciiDecimalFormatter.CountInt32Digits(record.ExitCode)", "AsciiDecimalFormatter.CountInt32Digits(0)"),
            (nameof(NativeAppExitStatusFormatter.TryFormat), "AsciiDecimalFormatter.CountInt32Digits(record.ExitCode)", "AsciiDecimalFormatter.CountInt32Digits((byte)record.ExitCode)"),
            ("TryAppendChars", "AsciiTextBuilder.AppendChars(destination, offset, value)", "AsciiTextBuilder.AppendChars(destination, 0, value)"),
            ("TryAppendChars", "AsciiTextBuilder.AppendChars(destination, offset, value)", "AsciiTextBuilder.AppendChars(destination, (byte)offset, value)")
        })
        {
            var document = catalog.Document(catalog.Method(owner, member));
            HostCheck.True(!FormatterDependencies(Rebuild(document, HostSourceMutation.ReplaceExpression(document.Text, before, after))));
        }
        var adapter = catalog.Method(owner, "TryAppendChars");
        var adapterDocument = catalog.Document(adapter);
        foreach (string body in new[]
        {
            "{ AsciiTextBuilder.AppendChars(destination, offset, value); return true; }",
            "{ var result = AsciiTextBuilder.AppendChars(destination, offset, value); return true; }"
        })
        {
            var discarded = adapter.WithBody((BlockSyntax)SyntaxFactory.ParseStatement(body));
            HostCheck.True(!FormatterDependencies(Rebuild(adapterDocument,
                adapterDocument.Syntax.GetRoot().ReplaceNode(adapter, discarded).ToFullString())));
        }
        var format = catalog.Method(owner, nameof(NativeAppExitStatusFormatter.TryFormat));
        var formatDocument = catalog.Document(format);
        var decoy = HostSourceMutation.ReplaceExpression(formatDocument.Text, "AsciiDecimalFormatter.CountInt32Digits(record.ExitCode)", "1");
        var decoyRoot = CSharpSyntaxTree.ParseText(decoy, (CSharpParseOptions)format.SyntaxTree.Options).GetRoot();
        var decoyFormat = decoyRoot.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Single(method => method.Identifier.ValueText == format.Identifier.ValueText);
        decoyRoot = decoyRoot.ReplaceNode(decoyFormat, decoyFormat.WithBody(decoyFormat.Body!.WithStatements(
            decoyFormat.Body.Statements.Insert(0, SyntaxFactory.ParseStatement("int unused = AsciiDecimalFormatter.CountInt32Digits(record.ExitCode);")))));
        HostCheck.True(!FormatterDependencies(Rebuild(formatDocument, decoyRoot.ToFullString())));
        var bufferBranch = format.Body!.Statements.OfType<IfStatementSyntax>().Single(branch =>
            catalog.Compilation.GetSemanticModel(format.SyntaxTree).GetOperation(branch.Condition)
                is IBinaryOperation
            {
                OperatorKind: BinaryOperatorKind.LessThan,
                LeftOperand: IPropertyReferenceOperation { Property.Name: "Length" }
            });
        foreach (string body in new[]
        {
            "{ if (false) { return new NativeAppExitMarkerResult(NativeAppExitMarkerStatus.DestinationTooSmall, 0, requiredBytes, OSHostStatus.BufferTooSmall); } return default; }",
            "{ return new NativeAppExitMarkerResult(NativeAppExitMarkerStatus.Success, 0, requiredBytes, OSHostStatus.BufferTooSmall); }"
        })
        {
            // Bind the existing required-size local when the fixture has been renamed.
            var comparison = (IBinaryOperation)catalog.Compilation.GetSemanticModel(format.SyntaxTree).GetOperation(bufferBranch.Condition)!;
            string localName = ((ILocalReferenceOperation)comparison.RightOperand).Local.Name;
            var replacement = bufferBranch.WithStatement(SyntaxFactory.ParseStatement(body.Replace("requiredBytes", localName, StringComparison.Ordinal)));
            HostCheck.True(!FormatterDependencies(Rebuild(formatDocument,
                formatDocument.Syntax.GetRoot().ReplaceNode(bufferBranch, replacement).ToFullString())));
        }

        // A real partial move preserves private access across different files.
        var movedRoot = adapterDocument.Syntax.GetRoot().RemoveNode(adapter, SyntaxRemoveOptions.KeepNoTrivia)!;
        foreach (var declaration in movedRoot.DescendantNodes().OfType<ClassDeclarationSyntax>()
            .Where(declaration => declaration.Identifier.ValueText == nameof(NativeAppExitStatusFormatter)
                && !declaration.Modifiers.Any(SyntaxKind.PartialKeyword)).ToArray())
            movedRoot = movedRoot.ReplaceNode(declaration, declaration.AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword).WithTrailingTrivia(SyntaxFactory.Space)));
        var partialCatalog = Rebuild(adapterDocument, movedRoot.ToFullString());
        string partialText = "using System; using OS.Std.Text; namespace OS.Kernel.Runtime; public static partial class NativeAppExitStatusFormatter { " + adapter.ToFullString() + " }";
        int suffix = 0;
        string partialPath;
        do
        {
            partialPath = adapterDocument.Path + ".helper" + suffix++ + ".cs";
        }
        while (catalog.Documents.Any(document => document.Path.Equals(partialPath, OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)));
        var partialDocument = CSharpSourceDocument.FromBytes(partialPath, System.Text.Encoding.UTF8.GetBytes(partialText),
            (CSharpParseOptions)adapter.SyntaxTree.Options);
        var split = new CSharpSourceCatalog([.. partialCatalog.Documents, partialDocument], catalog.Compilation.References,
            catalog.Compilation.AssemblyName!, catalog.Compilation.Options);
        HostCheck.True(ParserDependencies(split) && FormatterDependencies(split));

        CSharpSourceCatalog Rebuild(CSharpSourceDocument document, string text, string? path = null)
        {
            var replacement = CSharpSourceDocument.FromBytes(path ?? document.Path, System.Text.Encoding.UTF8.GetBytes(text),
                (CSharpParseOptions)document.Syntax.Options);
            return new(catalog.Documents.Select(item => item == document ? replacement : item), catalog.Compilation.References,
                catalog.Compilation.AssemblyName!, catalog.Compilation.Options);
        }
    }
}
