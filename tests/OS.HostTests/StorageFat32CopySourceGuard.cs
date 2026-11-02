using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Storage;
using OS.Tooling;

/// <summary>Checks the shared final-block copy and its exact destination slice.
/// Existing FAT read fixtures verify device failures, byte counts and contents.</summary>
internal static class StorageFat32CopySourceGuard
{
    internal static bool Check(CSharpSourceCatalog catalog, string owner, string name)
    {
        var method = catalog.Method(owner, name);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        if (!StorageBufferCopySourceGuard.Check(catalog, owner, name, typeof(Fat32Status).FullName!,
            nameof(Fat32Status.FileDestinationTooSmall), Destination))
            return false;
        var copy = StorageSourceGuard.Calls(catalog, method, typeof(OS.Std.Memory.BufferOperations), nameof(OS.Std.Memory.BufferOperations.MemoryCopy)).Single();
        var destination = (IInvocationOperation)StorageSourceGuard.Argument(copy, 1);
        if (StorageSourceGuard.Argument(copy, 0).Syntax is not ElementAccessExpressionSyntax { ArgumentList.Arguments.Count: 1 } element
            || model.GetOperation(element.Expression) is not ILocalReferenceOperation buffer
            || StorageSourceGuard.LocalInitializer(model, buffer)?.Syntax is not StackAllocArrayCreationExpressionSyntax
            || element.ArgumentList.Arguments[0].Expression is not RangeExpressionSyntax { LeftOperand: null, RightOperand: { } end }
            || model.GetOperation(end) is not ILocalReferenceOperation remaining
            || StorageSourceGuard.Argument(destination, 1) is not ILocalReferenceOperation count
            || !SymbolEqualityComparer.Default.Equals(remaining.Local, count.Local))
            return false;
        return StorageSourceGuard.Calls(catalog, method, typeof(IReadOnlyBlockDevice), nameof(IReadOnlyBlockDevice.ReadBlocks))
            .Any(read => StorageSourceGuard.Argument(read, 2) is ILocalReferenceOperation target
                && SymbolEqualityComparer.Default.Equals(target.Local, buffer.Local)
                && read.Instance is not null && StorageSourceGuard.IsParameter(read.Instance, symbol.Parameters[0])
                && read.Arguments.Single(argument => argument.Parameter?.Ordinal == 1).Value.ConstantValue is { HasValue: true, Value: 1u });

        bool Destination(IOperation operation, IParameterSymbol parameter) => operation is IInvocationOperation slice
            && slice.TargetMethod.Name == nameof(Span<byte>.Slice)
            && SymbolEqualityComparer.Default.Equals(slice.TargetMethod.ContainingType, parameter.Type)
            && slice.Instance is not null && StorageSourceGuard.IsParameter(slice.Instance, parameter)
            && slice.Arguments.Length == 2 && symbol.Parameters.Length == 8
            && StorageSourceGuard.IsParameter(StorageSourceGuard.Argument(slice, 0), symbol.Parameters[6]);
    }
}
