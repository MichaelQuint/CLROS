using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Finds the admitted leaf-walk implementation by compiler ownership.
/// The behavioral fixture supplies independent decoder, read and failure oracles.</summary>
internal sealed class EfiLeafReuseSources(CSharpSourceCatalog catalog)
{
    internal const string Owner = "OS.Boot.EfiEntry+M03FirstExitAdapter";
    private const string Decoder = "OS.Kernel.Memory.X64PageTableEntry";
    internal CSharpSourceCatalog Catalog => catalog;
    internal MethodDeclarationSyntax Method(string name) => catalog.Method(Owner, name);
    internal IMethodSymbol Symbol(MethodDeclarationSyntax method) =>
        (IMethodSymbol)catalog.Compilation.GetSemanticModel(method.SyntaxTree).GetDeclaredSymbol(method)!;

    internal IInvocationOperation[] Calls(MethodDeclarationSyntax method, string owner, string name) =>
        catalog.MethodInvocations(method).Where(call => call.TargetMethod.Name == name &&
            SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, catalog.Type(owner))).ToArray();
    internal IInvocationOperation[] Calls(MethodDeclarationSyntax method, string name) => Calls(method, Owner, name);
    internal static InvocationExpressionSyntax Syntax(IInvocationOperation call) => (InvocationExpressionSyntax)call.Syntax;

    internal FieldDeclarationSyntax Constant(string name)
    {
        var field = catalog.Type(Owner).GetMembers(name).OfType<IFieldSymbol>().Single();
        if (!field.IsConst || field.DeclaringSyntaxReferences.Single().GetSyntax() is not VariableDeclaratorSyntax variable
            || variable.Parent?.Parent is not FieldDeclarationSyntax declaration)
            throw new InvalidOperationException("leaf_fixture_constant " + name);
        // Each required constant remains available if a formatter separates or
        // recombines a declaration, or moves it into another partial file.
        return declaration.WithDeclaration(declaration.Declaration.WithVariables(SyntaxFactory.SingletonSeparatedList(variable)));
    }

    internal EfiLeafReuseSources Replace(MethodDeclarationSyntax method, MethodDeclarationSyntax replacement)
    {
        var document = catalog.Document(method);
        var root = document.Syntax.GetRoot().ReplaceNode(method, replacement);
        var updated = CSharpSourceDocument.FromBytes(document.Path, System.Text.Encoding.UTF8.GetBytes(root.ToFullString()),
            (CSharpParseOptions)document.Syntax.Options);
        return new(new CSharpSourceCatalog(catalog.Documents.Select(item => item == document ? updated : item),
            catalog.Compilation.References, catalog.Compilation.AssemblyName!, catalog.Compilation.Options));
    }

    internal bool PoolBoundary()
    {
        try
        {
            var walk = Method("CooperativeWalkPoolLeaf");
            var walkSymbol = Symbol(walk);
            int references = 0;
            foreach (var reference in catalog.Type(Owner).DeclaringSyntaxReferences)
            {
                var declaration = (TypeDeclarationSyntax)reference.GetSyntax();
                var model = catalog.Compilation.GetSemanticModel(declaration.SyntaxTree);
                // Nested helpers can access their containing type's private walk.
                // Count all bound references beneath every owner declaration.
                foreach (var name in declaration.DescendantNodes().OfType<SimpleNameSyntax>())
                {
                    if (!SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(name).Symbol, walkSymbol))
                        continue;
                    var invocation = name.Ancestors().OfType<InvocationExpressionSyntax>()
                        .FirstOrDefault(call => call.Expression.Span.Contains(name.Span));
                    if (invocation is null || !SymbolEqualityComparer.Default.Equals(catalog.Invocation(invocation), walkSymbol))
                        return false;
                    references++;
                }
            }
            if (references != 2)
                return false;

            foreach (string name in new[] { "CooperativeFindLeaf", "CooperativeAuditPool" })
            {
                var method = Method(name);
                var admission = Calls(method, "CooperativeAdmitPool");
                var walks = Calls(method, "CooperativeWalkPoolLeaf");
                if (admission.Length != 1 || walks.Length != 1)
                    return false;
                var admitted = Syntax(admission[0]);
                var walked = Syntax(walks[0]);
                if (admitted.Parent is not PrefixUnaryExpressionSyntax unary || !unary.IsKind(SyntaxKind.LogicalNotExpression)
                    || unary.Parent is not IfStatementSyntax gate || gate.Condition != unary || gate.Parent != method.Body
                    || !FalseReturn(gate.Statement) || gate.Span.End > walked.SpanStart)
                    return false;
                if (name == "CooperativeFindLeaf")
                {
                    if (walked.Parent is not ReturnStatementSyntax)
                        return false;
                }
                else
                {
                    var loops = method.DescendantNodes().OfType<ForStatementSyntax>().ToArray();
                    if (loops.Length != 1 || loops[0].Parent != method.Body || !walked.Ancestors().Contains(loops[0])
                        || !Calls(method, "CooperativeGuardPage").Any(call => call.Syntax.Ancestors().Contains(loops[0]))
                        || !Calls(method, Decoder, "TryDecode").Any(call => call.Syntax.Ancestors().Contains(loops[0])))
                        return false;
                }
            }
            return catalog.MethodInvocations(walk).All(call =>
                IsMethod(call.TargetMethod, Decoder, "TryDecode")
                || IsMethod(call.TargetMethod, "OS.Boot.PhysicalAddress", "TryGetRawValueForHardwareRegisterBoundary"));
        }
        catch (InvalidOperationException) { return false; }
    }

    internal bool ReadOrder()
    {
        try
        {
            var walk = Method("CooperativeWalkPoolLeaf");
            var audit = Method("CooperativeAuditPool");
            var find = Method("CooperativeFindLeaf");
            var parameters = Symbol(walk).Parameters;
            if (!PoolBoundary() || parameters.Length != 7
                || !walk.ParameterList.Parameters.Skip(5).All(parameter => parameter.Default?.Value.IsKind(SyntaxKind.NullLiteralExpression) == true)
                || Syntax(Calls(find, "CooperativeWalkPoolLeaf").Single()).ArgumentList.Arguments.Count != 5)
                return false;

            var firstDecode = Calls(walk, Decoder, "TryDecode").Single();
            var firstRaw = Local(firstDecode, 1);
            var firstContext = Local(firstDecode, 2);
            var walkModel = catalog.Compilation.GetSemanticModel(walk.SyntaxTree);
            if (firstRaw.Initializer?.Value is not PrefixUnaryExpressionSyntax firstRead
                || !firstRead.IsKind(SyntaxKind.PointerIndirectionExpression)
                || !Context(walkModel, firstContext.Initializer!.Value, parameters[0])
                || !(firstRaw.SpanStart < firstContext.SpanStart && firstContext.SpanStart < firstDecode.Syntax.SpanStart))
                return false;
            var slot = walkModel.GetSymbolInfo(firstRead.Operand).Symbol;
            if (slot is not ILocalSymbol || walk.DescendantNodes().OfType<AssignmentExpressionSyntax>().Count(assignment =>
                SymbolEqualityComparer.Default.Equals(walkModel.GetSymbolInfo(assignment.Left).Symbol, parameters[4])
                && SymbolEqualityComparer.Default.Equals(walkModel.GetSymbolInfo(assignment.Right).Symbol, slot)) != 1)
                return false;
            var levelExpression = Syntax(firstDecode).ArgumentList.Arguments[0].Expression;
            if (levelExpression is not CastExpressionSyntax cast)
                return false;
            var level = walkModel.GetSymbolInfo(cast.Expression).Symbol;
            if (level is not ILocalSymbol)
                return false;
            foreach (var parameter in parameters.Skip(5))
            {
                var stores = walk.DescendantNodes().OfType<AssignmentExpressionSyntax>().Where(assignment =>
                    assignment.Left is PrefixUnaryExpressionSyntax pointer && pointer.IsKind(SyntaxKind.PointerIndirectionExpression)
                    && SymbolEqualityComparer.Default.Equals(walkModel.GetSymbolInfo(pointer.Operand).Symbol, parameter)).ToArray();
                if (stores.Length != 1 || !stores[0].Ancestors().OfType<IfStatementSyntax>().Any(gate =>
                    gate.Condition is BinaryExpressionSyntax equal && equal.IsKind(SyntaxKind.EqualsExpression)
                    && SymbolEqualityComparer.Default.Equals(walkModel.GetSymbolInfo(equal.Left).Symbol, level)
                    && walkModel.GetConstantValue(equal.Right) is { HasValue: true, Value: int value } && value == 1))
                    return false;
            }

            var call = Calls(audit, "CooperativeWalkPoolLeaf").Single();
            var auditModel = catalog.Compilation.GetSemanticModel(audit.SyntaxTree);
            var leafExpression = Syntax(call).ArgumentList.Arguments[4].Expression;
            ISymbol? leaf = leafExpression is DeclarationExpressionSyntax declaration
                ? auditModel.GetDeclaredSymbol(declaration.Designation) : auditModel.GetSymbolInfo(leafExpression).Symbol;
            var secondReads = audit.DescendantNodes().OfType<VariableDeclaratorSyntax>().Where(variable =>
                variable.Initializer?.Value is PrefixUnaryExpressionSyntax pointer && pointer.IsKind(SyntaxKind.PointerIndirectionExpression)
                && SymbolEqualityComparer.Default.Equals(auditModel.GetSymbolInfo(pointer.Operand).Symbol, leaf)).ToArray();
            if (leaf is null || secondReads.Length != 1)
                return false;
            var guard = Calls(audit, "CooperativeGuardPage").Single();
            var fallback = Calls(audit, Decoder, "TryDecode").Single();
            var secondContext = Local(fallback, 2);
            if (!SymbolEqualityComparer.Default.Equals(auditModel.GetDeclaredSymbol(secondReads[0]),
                auditModel.GetDeclaredSymbol(Local(fallback, 1))))
                return false;
            var admission = Syntax(Calls(audit, "CooperativeAdmitPool").Single());
            var memoryExpression = admission.ArgumentList.Arguments[2].Expression;
            ISymbol? memory = memoryExpression is DeclarationExpressionSyntax memoryDeclaration
                ? auditModel.GetDeclaredSymbol(memoryDeclaration.Designation) : auditModel.GetSymbolInfo(memoryExpression).Symbol;
            return memory is not null && Context(auditModel, secondContext.Initializer!.Value, memory)
                && call.Syntax.SpanStart < secondReads[0].SpanStart && secondReads[0].SpanStart < guard.Syntax.SpanStart
                && guard.Syntax.SpanStart < secondContext.SpanStart && secondContext.SpanStart < fallback.Syntax.SpanStart;
        }
        catch (InvalidOperationException) { return false; }
    }

    private bool IsMethod(IMethodSymbol method, string owner, string name) => method.Name == name
        && SymbolEqualityComparer.Default.Equals(method.ContainingType, catalog.Type(owner));
    private static bool FalseReturn(StatementSyntax statement) => statement is ReturnStatementSyntax returned
        ? returned.Expression?.IsKind(SyntaxKind.FalseLiteralExpression) == true
        : statement is BlockSyntax { Statements.Count: 1 } block && FalseReturn(block.Statements[0]);

    private VariableDeclaratorSyntax Local(IInvocationOperation invocation, int ordinal)
    {
        IOperation value = invocation.Arguments.Single(argument => argument.Parameter!.Ordinal == ordinal).Value;
        while (value is IConversionOperation { Conversion.IsIdentity: true } conversion)
            value = conversion.Operand;
        if (value is not ILocalReferenceOperation local || local.Local.DeclaringSyntaxReferences.Single().GetSyntax() is not VariableDeclaratorSyntax { Initializer: not null } variable)
            throw new InvalidOperationException("leaf_source_local");
        return variable;
    }

    private bool Context(SemanticModel model, ExpressionSyntax expression, ISymbol memory) =>
        expression is MemberAccessExpressionSyntax preconditions && preconditions.Expression is MemberAccessExpressionSyntax tables
        && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(preconditions).Symbol,
            catalog.Type("OS.Kernel.Diagnostics.SeparatePageFaultPageTableRequest").GetMembers("Preconditions").Single())
        && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(tables).Symbol,
            catalog.Type("OS.Boot.FirstExitDiagnosticMemory").GetMembers("Tables").Single())
        && SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(tables.Expression).Symbol, memory);
}
