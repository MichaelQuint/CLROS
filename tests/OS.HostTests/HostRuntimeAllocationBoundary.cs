using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Tooling;

/// <summary>Bound source contracts for the restricted allocation wrappers.
/// Used by shim and ledger fixtures; does not establish native execution.</summary>
internal static class HostRuntimeAllocationBoundary
{
    internal static CSharpSourceCatalog AllocationProfile(params string[] additions)
    {
        string[] heap = ["EnableM01LoadedImageStackProbe", "EnableM01LiveAllocatorProbe", "EnableM04IndependentEndianConformanceProbe",
            "EnableM04SpanOverlapMemoryCastUnsafeWidthConformanceProbe", "EnableM04ArithmeticConformanceProbe",
            "EnableM04LayoutContractProbe", "EnableM04ManagedHeapPageRangeProbe", "EnableM04RhpNewFastProbe"];
        return HostSourceCatalog.Efi(EfiProfile.Create("Diagnostic", heap.Concat(additions).ToDictionary(flag => flag, _ => true)));
    }

    internal static bool StringAllocator(CSharpSourceCatalog catalog)
    {
        var imported = catalog.Type("System.Runtime.RuntimeImports").GetMembers("RhNewString").OfType<IMethodSymbol>().Single();
        if (!imported.IsExtern || !imported.IsStatic || imported.Arity != 0 || imported.RefKind != RefKind.None
            || imported.ReturnType.SpecialType != SpecialType.System_String
            || imported.Parameters is not [{ RefKind: RefKind.None, Type: IPointerTypeSymbol pointer },
                { RefKind: RefKind.None, Type.SpecialType: SpecialType.System_IntPtr }]
            || !SymbolEqualityComparer.Default.Equals(pointer.PointedAtType, catalog.Type("Internal.Runtime.MethodTable"))
            || !imported.GetAttributes().Any(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, catalog.Type("System.Runtime.RuntimeImportAttribute"))
                && attribute.ConstructorArguments is [{ Value: "*" }, { Value: "RhNewString" }]))
            return false;
        var fastAllocate = catalog.Method("System.String", "FastAllocateString");
        var model = catalog.Compilation.GetSemanticModel(fastAllocate.SyntaxTree);
        var signature = (IMethodSymbol)model.GetDeclaredSymbol(fastAllocate)!;
        return signature.IsStatic && signature.Arity == 0 && signature.RefKind == RefKind.None
            && signature.ReturnType.SpecialType == SpecialType.System_String
            && signature.Parameters is [{ RefKind: RefKind.None, Type.SpecialType: SpecialType.System_IntPtr }]
            && fastAllocate.Body?.Statements is [ReturnStatementSyntax { Expression: { } returned }]
            && model.GetOperation(returned) is IInvocationOperation import && SymbolEqualityComparer.Default.Equals(import.TargetMethod, imported)
            && import.Arguments[0].Value is IInvocationOperation table && table.TargetMethod.Name == "Of"
            && SymbolEqualityComparer.Default.Equals(table.TargetMethod.ContainingType, catalog.Type("Internal.Runtime.MethodTable"))
            && table.TargetMethod.TypeArguments is [{ SpecialType: SpecialType.System_String }]
            && import.Arguments[1].Value is IParameterReferenceOperation lengthArgument
            && SymbolEqualityComparer.Default.Equals(lengthArgument.Parameter, signature.Parameters[0]);
    }

    internal static bool Halts(CSharpSourceCatalog catalog, MethodDeclarationSyntax method)
    {
        if (method.Body?.Statements is not [WhileStatementSyntax loop])
            return false;
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        StatementSyntax body = loop.Statement is BlockSyntax { Statements.Count: 1 } block ? block.Statements[0] : loop.Statement;
        return model.GetConstantValue(loop.Condition) is { HasValue: true, Value: true }
            && body is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call }
            && catalog.Invocation(call) is { Name: "Halt", Parameters.Length: 0, IsStatic: true } halt
            && SymbolEqualityComparer.Default.Equals(halt.ContainingType, catalog.Type("System.RuntimeFailFast"));
    }

    internal static bool Allocates(CSharpSourceCatalog catalog, string name, string allocatorName, bool? pointerArray = null)
    {
        var method = (MethodDeclarationSyntax)HostRuntimeExports.Read(catalog)[name].DeclaringSyntaxReferences.Single().GetSyntax();
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var declaration = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        var calls = method.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(call =>
            (Syntax: call, Symbol: catalog.Invocation(call), Operation: (IInvocationOperation)model.GetOperation(call)!)).ToArray();
        var allocation = calls.Single(call => call.Symbol.Name == allocatorName);
        var reference = calls.Single(call => call.Symbol.Name == "As");
        return calls.Length == 2 && WrapperSignature(catalog, declaration, name) && Reinterprets(catalog)
            && catalog.Compilation.Options.Platform == Platform.X64
            && method.Body?.Statements is [LocalDeclarationStatementSyntax { Declaration.Variables: [var local] }, ReturnStatementSyntax]
            && local.Initializer is { } initializer
            && SymbolEqualityComparer.Default.Equals(allocation.Symbol.ContainingType, catalog.Type("OS.Runtime.RhpNewFastAllocation"))
            && ExactConversion(allocation.Operation.Arguments[0].Value, declaration.Parameters[0], SpecialType.System_UInt64)
            && (allocatorName == "Allocate" || ExactConversion(allocation.Operation.Arguments[1].Value, declaration.Parameters[1], SpecialType.System_Int64))
            && (pointerArray is null || HostSourceOperations.Argument(allocation.Operation, 2).ConstantValue is { HasValue: true, Value: bool actual } && actual == pointerArray)
            && SymbolEqualityComparer.Default.Equals(reference.Symbol.ContainingType, catalog.Type("System.Runtime.CompilerServices.Unsafe"))
            && reference.Symbol.TypeArguments[0].SpecialType == SpecialType.System_UIntPtr
            && SymbolEqualityComparer.Default.Equals(reference.Symbol.TypeArguments[1], declaration.ReturnType)
            && reference.Operation.Arguments[0].Value is ILocalReferenceOperation returnedLocal
            && SymbolEqualityComparer.Default.Equals(returnedLocal.Local, model.GetDeclaredSymbol(local))
            && model.GetOperation(initializer.Value) is IConversionOperation
                { Type.SpecialType: SpecialType.System_UIntPtr, Conversion.IsUserDefined: false, Operand: IInvocationOperation produced }
            && produced.Type?.SpecialType == SpecialType.System_UInt64
            && produced.Syntax == allocation.Syntax
            && reference.Operation.Parent is IReturnOperation;
    }

    internal static bool ExactConversion(IOperation operation, IParameterSymbol source, SpecialType target)
        => operation is IConversionOperation { Conversion.IsUserDefined: false, Operand: IParameterReferenceOperation parameter } conversion
            && conversion.Type?.SpecialType == target && SymbolEqualityComparer.Default.Equals(parameter.Parameter, source);

    internal static bool WrapperSignature(CSharpSourceCatalog catalog, IMethodSymbol method, string exportName)
        => method.IsStatic && method.Arity == 0 && method.RefKind == RefKind.None
            && SymbolEqualityComparer.Default.Equals(method.ContainingType, catalog.Type("System.Runtime.RhpNewFastRuntime"))
            && method.DeclaredAccessibility == Accessibility.Internal
            && method.ReturnType.SpecialType == (exportName == "RhNewString" ? SpecialType.System_String : SpecialType.System_Object)
            && method.Parameters.Length == (exportName == "RhpNewFast" ? 1 : 2)
            && method.Parameters.All(parameter => parameter.RefKind == RefKind.None)
            && method.Parameters[0].Type is IPointerTypeSymbol pointer
            && SymbolEqualityComparer.Default.Equals(pointer.PointedAtType, catalog.Type("Internal.Runtime.MethodTable"))
            && (method.Parameters.Length == 1 || method.Parameters[1].Type.SpecialType == SpecialType.System_IntPtr)
            && method.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == typeof(MethodImplAttribute).FullName
                && attribute.ConstructorArguments is [{ Value: int value }] && (value & (int)MethodImplOptions.NoInlining) != 0);

    internal static bool Reinterprets(CSharpSourceCatalog catalog)
    {
        var method = catalog.Type("System.Runtime.CompilerServices.Unsafe").GetMembers("As").OfType<IMethodSymbol>().Single();
        return method.IsExtern && method.IsStatic && method.RefKind == RefKind.Ref && method.TypeParameters.Length == 2
            && method.Parameters is [{ RefKind: RefKind.Ref }]
            && SymbolEqualityComparer.Default.Equals(method.Parameters[0].Type, method.TypeParameters[0])
            && SymbolEqualityComparer.Default.Equals(method.ReturnType, method.TypeParameters[1]);
    }

}
