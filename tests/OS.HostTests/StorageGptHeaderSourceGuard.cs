using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Std.Binary;
using OS.Std.Hash;
using OS.Std.Memory;
using OS.Tooling;

/// <summary>Checks the GPT header CRC copy and its failure route by bound values.
/// Parser fixtures independently validate header sizes, CRC bytes and statuses.</summary>
internal static class StorageGptHeaderSourceGuard
{
    internal static bool Check(CSharpSourceCatalog catalog, string owner)
    {
        var method = catalog.Method(owner, "TryComputeHeaderCrc32");
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        if (symbol.ReturnType.SpecialType != SpecialType.System_Boolean || symbol.Parameters.Length != 3
            || !SymbolEqualityComparer.Default.Equals(symbol.Parameters[0].Type,
                catalog.Compilation.GetTypeByMetadataName("System.ReadOnlySpan`1")!.Construct(catalog.Compilation.GetSpecialType(SpecialType.System_Byte)))
            || symbol.Parameters[1].Type.SpecialType != SpecialType.System_UInt32
            || symbol.Parameters[2] is not { RefKind: RefKind.Out, Type.SpecialType: SpecialType.System_UInt32 }
            || method.Body?.Statements is not [ExpressionStatementSyntax initialize,
                LocalDeclarationStatementSyntax storage, LocalDeclarationStatementSyntax copied,
                IfStatementSyntax failure, ExpressionStatementSyntax clear,
                ExpressionStatementSyntax checksum, ReturnStatementSyntax success])
            return false;

        if (model.GetOperation(initialize.Expression) is not ISimpleAssignmentOperation initial
            || !HostSourceOperations.IsParameter(initial.Target, symbol.Parameters[2])
            || !UnsignedZero(initial.Value)
            || storage.Declaration.Variables is not [var storageVariable]
            || model.GetDeclaredSymbol(storageVariable) is not ILocalSymbol buffer
            || !SymbolEqualityComparer.Default.Equals(buffer.Type,
                catalog.Compilation.GetTypeByMetadataName("System.Span`1")!.Construct(catalog.Compilation.GetSpecialType(SpecialType.System_Byte)))
            || storageVariable.Initializer?.Value is not StackAllocArrayCreationExpressionSyntax
            { Type: ArrayTypeSyntax { RankSpecifiers.Count: 1 } allocated }
            || allocated.RankSpecifiers[0].Sizes is not [var size]
            || model.GetTypeInfo(allocated.ElementType).Type?.SpecialType != SpecialType.System_Byte
            || ExpressionOperation(model, size) is not IConversionOperation
            { IsChecked: true, Type.SpecialType: SpecialType.System_Int32, Operand: IParameterReferenceOperation length }
            || !SymbolEqualityComparer.Default.Equals(length.Parameter, symbol.Parameters[1])
            || !HostSourceOperations.IsUnmodifiedLocal(model, buffer, method)
            || copied.Declaration.Variables is not [var copyVariable]
            || model.GetDeclaredSymbol(copyVariable) is not ILocalSymbol result
            || copyVariable.Initializer is not { Value: InvocationExpressionSyntax copySyntax }
            || !HostSourceOperations.IsMethod(catalog.Invocation(copySyntax), typeof(BufferOperations), nameof(BufferOperations.MemoryCopy))
            || model.GetOperation(copySyntax) is not IInvocationOperation copy
            || !Local(HostSourceOperations.Argument(copy, 1), buffer))
            return false;

        if (HostSourceOperations.Argument(copy, 0) is not IInvocationOperation source
            || source.TargetMethod.Name != nameof(ReadOnlySpan<byte>.Slice)
            || !SymbolEqualityComparer.Default.Equals(source.TargetMethod.ContainingType, symbol.Parameters[0].Type)
            || source.Instance is null || !HostSourceOperations.IsParameter(source.Instance, symbol.Parameters[0])
            || source.Arguments.Length != 2 || !Integer(HostSourceOperations.Argument(source, 0), 0)
            || HostSourceOperations.Argument(source, 1) is not IPropertyReferenceOperation
            { Property.Name: nameof(Span<byte>.Length), Instance: { } sized } count
            || !Local(sized, buffer) || !SymbolEqualityComparer.Default.Equals(count.Property.ContainingType, buffer.Type)
            || failure.Else is not null
            || model.GetOperation(failure.Condition) is not IUnaryOperation
            {
                OperatorKind: UnaryOperatorKind.Not, Operand: IPropertyReferenceOperation
                { Property.Name: nameof(BufferOperationResult.Succeeded), Instance: { } instance } succeeded
            }
            || !Local(instance, result) || !SymbolEqualityComparer.Default.Equals(succeeded.Property.ContainingType, result.Type)
            || !Returns(failure.Statement, false) || !Returns(success, true))
            return false;

        if (clear.Expression is not InvocationExpressionSyntax clearSyntax
            || !HostSourceOperations.IsMethod(catalog.Invocation(clearSyntax), typeof(EndianPrimitives), nameof(EndianPrimitives.WriteUInt32LittleEndian))
            || model.GetOperation(clearSyntax) is not IInvocationOperation write
            || !BufferSlice(HostSourceOperations.Argument(write, 0), buffer, 16, 4)
            || !UnsignedZero(HostSourceOperations.Argument(write, 1))
            || model.GetOperation(checksum.Expression) is not ISimpleAssignmentOperation assignment
            || !HostSourceOperations.IsParameter(assignment.Target, symbol.Parameters[2])
            || assignment.Value is not IInvocationOperation hash
            || !HostSourceOperations.IsMethod(hash.TargetMethod, typeof(Crc32), nameof(Crc32.Compute))
            || !Local(HostSourceOperations.Argument(hash, 0), buffer))
            return false;

        var parser = catalog.Method(owner, "Parse");
        var parserModel = catalog.Compilation.GetSemanticModel(parser.SyntaxTree);
        var edges = parser.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(call => SymbolEqualityComparer.Default.Equals(catalog.Invocation(call), symbol)).ToArray();
        return edges is [var edge] && edge.Parent is PrefixUnaryExpressionSyntax condition
            && condition.IsKind(SyntaxKind.LogicalNotExpression) && condition.Parent is IfStatementSyntax { Else: null } branch
            && branch.Condition == condition && branch.Parent == parser.Body
            && DirectReturn(branch.Statement)?.Expression is InvocationExpressionSyntax failed
            && parserModel.GetOperation(failed) is IInvocationOperation factory
            && HostSourceOperations.IsMethod(factory.TargetMethod, typeof(OS.Storage.GptPartitionTableParseResult), "Fail")
            && factory.Arguments.Any(argument => argument.Value is IFieldReferenceOperation status
                && status.Field.Name == nameof(OS.Storage.GptPartitionTableStatus.HeaderSectorTooSmall)
                && SymbolEqualityComparer.Default.Equals(status.Field.ContainingType,
                    catalog.Compilation.GetTypeByMetadataName(typeof(OS.Storage.GptPartitionTableStatus).FullName!)));

        bool Returns(StatementSyntax statement, bool expected) => DirectReturn(statement)?.Expression is { } expression
            && model.GetConstantValue(expression) is { HasValue: true, Value: bool actual } && actual == expected;
    }

    private static ReturnStatementSyntax? DirectReturn(StatementSyntax statement) => statement switch
    {
        ReturnStatementSyntax returned => returned,
        BlockSyntax { Statements: [ReturnStatementSyntax returned] } => returned,
        _ => null
    };

    private static IOperation? ExpressionOperation(SemanticModel model, ExpressionSyntax expression)
    {
        while (expression is ParenthesizedExpressionSyntax or CheckedExpressionSyntax)
            expression = expression is ParenthesizedExpressionSyntax grouped ? grouped.Expression : ((CheckedExpressionSyntax)expression).Expression;
        return model.GetOperation(expression);
    }

    private static bool Local(IOperation operation, ILocalSymbol local) => HostSourceOperations.Value(operation) is ILocalReferenceOperation reference
        && SymbolEqualityComparer.Default.Equals(reference.Local, local);

    private static bool Integer(IOperation operation, int expected) => operation.ConstantValue is { HasValue: true, Value: int actual } && actual == expected;

    private static bool UnsignedZero(IOperation operation) => operation.ConstantValue is { HasValue: true, Value: 0u or 0 };

    private static bool BufferSlice(IOperation operation, ILocalSymbol buffer, int offset, int count) =>
        operation is IInvocationOperation { Instance: { } instance, Arguments.Length: 2 } slice
        && slice.TargetMethod.Name == nameof(Span<byte>.Slice)
        && SymbolEqualityComparer.Default.Equals(slice.TargetMethod.ContainingType, buffer.Type)
        && Local(instance, buffer) && Integer(HostSourceOperations.Argument(slice, 0), offset)
        && Integer(HostSourceOperations.Argument(slice, 1), count);
}
