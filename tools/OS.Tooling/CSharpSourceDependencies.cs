using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OS.Tooling;

/// <summary>Cached compiler-bound source edges for one immutable compilation.
/// This is a source dependency graph, not a runtime call graph. Callers deciding
/// whether execution evidence can be reused must separately handle dynamic
/// dispatch and external inputs.</summary>
internal sealed class CSharpSourceDependencies(CSharpSourceCatalog catalog)
{
    private readonly Dictionary<SyntaxTree, SyntaxTree[]> edges = [];

    internal IReadOnlyList<CSharpSourceDocument> Closure(IEnumerable<INamedTypeSymbol> roots)
    {
        HashSet<SyntaxTree> selected = [];
        Queue<SyntaxTree> pending = new();
        foreach (var type in roots)
            foreach (var declaration in type.OriginalDefinition.DeclaringSyntaxReferences)
                Add(declaration.SyntaxTree);
        foreach (var document in catalog.Documents)
            if (document.Syntax.GetRoot() is CompilationUnitSyntax unit
                && (unit.AttributeLists.Count != 0 || unit.Usings.Any(import => import.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword))))
                Add(document.Syntax);
        while (pending.TryDequeue(out var tree))
            foreach (var dependency in Direct(tree))
                Add(dependency);
        return catalog.Documents.Where(document => selected.Contains(document.Syntax)).ToArray();

        void Add(SyntaxTree tree)
        {
            if (selected.Add(tree))
                pending.Enqueue(tree);
        }
    }

    private SyntaxTree[] Direct(SyntaxTree tree)
    {
        if (edges.TryGetValue(tree, out var known))
            return known;
        var model = catalog.Compilation.GetSemanticModel(tree);
        var errors = model.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray();
        if (errors.Length != 0)
            throw new InvalidOperationException("source_catalog_dependency_binding " + tree.FilePath + " "
                + string.Join("; ", errors.Select(error => error.ToString())));
        HashSet<SyntaxTree> dependencies = [];
        HashSet<INamedTypeSymbol> visited = new(SymbolEqualityComparer.Default);
        foreach (var node in tree.GetRoot().DescendantNodes())
        {
            Symbol(model.GetDeclaredSymbol(node));
            if (node is ExpressionSyntax expression)
            {
                Symbol(model.GetSymbolInfo(expression).Symbol);
                Type(model.GetTypeInfo(expression).Type);
                Symbol(model.GetConversion(expression).MethodSymbol);
            }
            else if (node is AttributeSyntax attribute)
                Symbol(model.GetSymbolInfo(attribute).Symbol);
            switch (node)
            {
                case CommonForEachStatementSyntax loop:
                    var iteration = model.GetForEachStatementInfo(loop);
                    Symbol(iteration.GetEnumeratorMethod);
                    Symbol(iteration.MoveNextMethod);
                    Symbol(iteration.CurrentProperty);
                    Symbol(iteration.DisposeMethod);
                    Type(iteration.ElementType);
                    Symbol(iteration.ElementConversion.MethodSymbol);
                    Symbol(iteration.CurrentConversion.MethodSymbol);
                    if (loop is ForEachVariableStatementSyntax variableLoop)
                        Deconstruction(model.GetDeconstructionInfo(variableLoop));
                    break;
                case AwaitExpressionSyntax awaited:
                    var awaiter = model.GetAwaitExpressionInfo(awaited);
                    Symbol(awaiter.GetAwaiterMethod);
                    Symbol(awaiter.IsCompletedProperty);
                    Symbol(awaiter.GetResultMethod);
                    break;
                case AssignmentExpressionSyntax assignment when assignment.Left is TupleExpressionSyntax or DeclarationExpressionSyntax:
                    Deconstruction(model.GetDeconstructionInfo(assignment));
                    break;
                case QueryClauseSyntax clause:
                    var query = model.GetQueryClauseInfo(clause);
                    Symbol(query.CastInfo.Symbol);
                    Symbol(query.OperationInfo.Symbol);
                    break;
                case SelectOrGroupClauseSyntax select:
                    Symbol(model.GetSymbolInfo(select).Symbol);
                    break;
                case OrderingSyntax ordering:
                    Symbol(model.GetSymbolInfo(ordering).Symbol);
                    break;
            }
        }
        known = dependencies.ToArray();
        edges.Add(tree, known);
        return known;

        void Deconstruction(DeconstructionInfo value)
        {
            Symbol(value.Method);
            if (value.Conversion is { } conversion)
                Symbol(conversion.MethodSymbol);
            if (!value.Nested.IsDefaultOrEmpty)
                foreach (var nested in value.Nested)
                    Deconstruction(nested);
        }

        void Type(ITypeSymbol? type)
        {
            if (type is IArrayTypeSymbol array)
                Type(array.ElementType);
            else if (type is IPointerTypeSymbol pointer)
                Type(pointer.PointedAtType);
            else if (type is IFunctionPointerTypeSymbol function)
                Symbol(function.Signature);
            else if (type is ITypeParameterSymbol parameter)
            {
                foreach (var constraint in parameter.ConstraintTypes)
                    Type(constraint);
            }
            else if (type is INamedTypeSymbol named)
            {
                // Register before traversing arguments: recursive generic
                // constraints must not recurse indefinitely.
                if (!visited.Add(named))
                    return;
                foreach (var argument in named.TypeArguments)
                    Type(argument);
                foreach (var declaration in named.OriginalDefinition.DeclaringSyntaxReferences)
                    dependencies.Add(declaration.SyntaxTree);
                Type(named.ContainingType);
            }
        }

        void Symbol(ISymbol? symbol)
        {
            switch (symbol)
            {
                case IAliasSymbol alias:
                    Symbol(alias.Target);
                    break;
                case ITypeSymbol type:
                    Type(type);
                    break;
                case IMethodSymbol method:
                    Type(method.ReturnType);
                    foreach (var argument in method.TypeArguments)
                        Type(argument);
                    foreach (var parameter in method.Parameters)
                        Type(parameter.Type);
                    break;
                case IPropertySymbol property:
                    Type(property.Type);
                    break;
                case IFieldSymbol field:
                    Type(field.Type);
                    break;
                case IEventSymbol @event:
                    Type(@event.Type);
                    break;
                case ILocalSymbol local:
                    Type(local.Type);
                    break;
                case IParameterSymbol parameter:
                    Type(parameter.Type);
                    break;
            }
            Type(symbol?.ContainingType);
        }
    }
}
