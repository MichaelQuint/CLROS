using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Checks the admitted boot authority surface by symbol identity. The
/// expected API remains an independent contract; filenames and partial wrappers
/// do not grant authority. This is source inspection, not emitted-code proof.</summary>
internal static class HostBootSourceGuard
{
    private static readonly SymbolDisplayFormat SurfaceFormat = new(
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters | SymbolDisplayGenericsOptions.IncludeTypeConstraints,
        memberOptions: SymbolDisplayMemberOptions.IncludeAccessibility | SymbolDisplayMemberOptions.IncludeModifiers
            | SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeParameters | SymbolDisplayMemberOptions.IncludeRef,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeName | SymbolDisplayParameterOptions.IncludeDefaultValue,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.UseSpecialTypes | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    internal static string[] Surface(INamedTypeSymbol type) => type.GetMembers()
        .Where(member => !member.IsImplicitlyDeclared)
        .Select(member => member.ToDisplayString(SurfaceFormat))
        .Order(StringComparer.Ordinal).ToArray();

    internal static bool Authority(CSharpSourceCatalog catalog, string name, string[] expected)
    {
        var type = catalog.Type(name);
        if (type.TypeKind != TypeKind.Class || !type.IsStatic || type.DeclaredAccessibility != Accessibility.Public
            || type.Arity != 0 || type.GetAttributes().Length != 0
            || !Surface(type).SequenceEqual(expected.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            return false;
        foreach (var member in type.GetMembers().Where(member => !member.IsImplicitlyDeclared))
        {
            if (member.GetAttributes().Length != 0)
                return false;
            if (member is IMethodSymbol method && (method.GetReturnTypeAttributes().Length != 0
                || method.Parameters.Any(parameter => parameter.GetAttributes().Length != 0)
                || method.TypeParameters.Any(parameter => parameter.GetAttributes().Length != 0)))
                return false;
            foreach (var reference in member.DeclaringSyntaxReferences)
            {
                var syntax = reference.GetSyntax();
                var model = catalog.Compilation.GetSemanticModel(syntax.SyntaxTree);
                if (model.GetDiagnostics(syntax.Span).Any(item => item.Severity == DiagnosticSeverity.Error))
                    return false;
                foreach (var node in syntax.DescendantNodesAndSelf().OfType<TypeSyntax>())
                {
                    var bound = model.GetTypeInfo(node).Type;
                    if (bound is { SpecialType: SpecialType.System_String }
                        || bound?.OriginalDefinition.ToDisplayString() is "System.Span<T>" or "System.ReadOnlySpan<T>"
                        || bound?.Name is "ISerialSink" or "BootEventRingBuffer" or "ProbeDefinition")
                        return false;
                }
            }
        }
        return true;
    }

    internal static bool CallsConfined(CSharpSourceCatalog catalog, string rootType, string callerType,
        string targetType, string[] required, string[] forbidden, bool enabled, string? callerMethod = null)
    {
        var owner = catalog.Type(callerType);
        var root = catalog.Type(rootType);
        var target = catalog.Type(targetType);
        var candidates = required.Concat(forbidden).ToHashSet(StringComparer.Ordinal);
        var counts = required.ToDictionary(name => name, _ => 0, StringComparer.Ordinal);
        foreach (var document in catalog.TypeSources(rootType))
        {
            var model = catalog.Compilation.GetSemanticModel(document.Syntax);
            foreach (var nameSyntax in document.Syntax.GetRoot().DescendantNodes().OfType<SimpleNameSyntax>())
            {
                string name = nameSyntax.Identifier.ValueText;
                if (!candidates.Contains(name))
                    continue;
                var enclosing = model.GetEnclosingSymbol(nameSyntax.SpanStart);
                var container = enclosing?.ContainingType;
                while (container is not null && !SymbolEqualityComparer.Default.Equals(container, root))
                    container = container.ContainingType;
                if (container is null)
                    continue;
                var binding = model.GetSymbolInfo(nameSyntax);
                if (binding.Symbol is not IMethodSymbol symbol)
                {
                    if (binding.CandidateSymbols.OfType<IMethodSymbol>().Any(candidate =>
                        SymbolEqualityComparer.Default.Equals(candidate.ContainingType, target)))
                        return false;
                    continue;
                }
                if (!SymbolEqualityComparer.Default.Equals(symbol.ContainingType, target))
                    continue;
                SyntaxNode expression = nameSyntax.Parent is MemberAccessExpressionSyntax member && member.Name == nameSyntax
                    ? member : nameSyntax;
                // A method group, function-pointer address or nameof cannot stand
                // in for an actual admitted call or bypass a forbidden target.
                if (!enabled || !counts.ContainsKey(name)
                    || expression.Parent is not InvocationExpressionSyntax call || call.Expression != expression
                    || !SymbolEqualityComparer.Default.Equals(enclosing?.ContainingType, owner)
                    || callerMethod is not null && enclosing?.Name != callerMethod
                    || call.Ancestors().Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
                    return false;
                _ = catalog.Invocation(call);
                counts[name]++;
            }
        }
        return counts.Values.All(count => count == (enabled ? 1 : 0));
    }

    internal static bool OrderedCalls(CSharpSourceCatalog catalog, string caller, string method,
        string target, params string[] expected)
    {
        var targetType = catalog.Type(target);
        var declaration = catalog.Method(caller, method);
        var names = expected.ToHashSet(StringComparer.Ordinal);
        var calls = catalog.MethodInvocations(declaration).Where(call =>
            SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, targetType)
            && names.Contains(call.TargetMethod.Name)).ToArray();
        // A dormant lambda/local function is not a phase call.
        return calls.All(call => !call.Syntax.Ancestors().TakeWhile(node => node != declaration)
                .Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax))
            && calls.Select(call => call.TargetMethod.Name).SequenceEqual(expected, StringComparer.Ordinal);
    }

    internal static bool Callback(CSharpSourceCatalog catalog, string owner, string prepare, string callback,
        string recordType, string fieldName, string stubType, string parameterName)
    {
        var method = catalog.Method(owner, prepare);
        var expected = catalog.Type(owner).GetMembers(callback).OfType<IMethodSymbol>().Single();
        var field = catalog.Type(recordType).GetMembers(fieldName).OfType<IFieldSymbol>().Single();
        var stub = catalog.Type(stubType);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var assignments = method.DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Select(syntax => model.GetOperation(syntax)).OfType<ISimpleAssignmentOperation>()
            .Where(assignment => assignment.Target is IFieldReferenceOperation target
                && SymbolEqualityComparer.Default.Equals(target.Field, field)).ToArray();
        var calls = catalog.MethodInvocations(method).Where(call => call.TargetMethod.Name == "Prepare"
            && SymbolEqualityComparer.Default.Equals(call.TargetMethod.ContainingType, stub)).ToArray();
        return catalog.Compilation.Options.Platform == Platform.X64
            && field.Type.SpecialType == SpecialType.System_UInt64
            && assignments.Length == 1 && calls.Length == 1
            && Active(assignments[0].Syntax) && Active(calls[0].Syntax)
            && Address(assignments[0].Value, integerField: true)
            && calls[0].Arguments.SingleOrDefault(argument => argument.Parameter?.Name == parameterName) is { } argument
            && Address(argument.Value, integerField: false);

        bool Address(IOperation operation, bool integerField)
        {
            operation = Identity(operation);
            if (integerField)
            {
                // Only the actual x64 function-pointer address -> ulong storage
                // conversion is admitted. An intervening uint/floating/integer
                // conversion can truncate or transform the callback address.
                if (operation is not IConversionOperation { Conversion.IsUserDefined: false,
                    Type.SpecialType: SpecialType.System_UInt64, Operand.Type: IFunctionPointerTypeSymbol } conversion)
                    return false;
                operation = Identity(conversion.Operand);
            }
            return operation is IAddressOfOperation { Reference: IMethodReferenceOperation reference }
                && SymbolEqualityComparer.Default.Equals(reference.Method, expected);
        }

        static IOperation Identity(IOperation operation)
        {
            while (operation is IConversionOperation { Conversion.IsIdentity: true } conversion
                && SymbolEqualityComparer.Default.Equals(conversion.Type, conversion.Operand.Type))
                operation = conversion.Operand;
            return operation;
        }

        bool Active(SyntaxNode syntax) => !syntax.Ancestors().TakeWhile(node => node != method)
            .Any(node => node is AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax);
    }
}
