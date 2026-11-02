using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using OS.Storage;
using OS.Tooling;

/// <summary>Checks that shared path equality drives lookup and duplicate-path
/// results. Behavioral package fixtures independently verify the actual entries.</summary>
internal static class StoragePackageEqualitySourceGuard
{
    internal static bool Check(CSharpSourceCatalog catalog, string owner, string methodName, bool duplicate)
    {
        var method = catalog.Method(owner, methodName);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        var calls = HostSourceOperations.Calls(catalog, method, typeof(OS.Std.MemoryExtensions), nameof(OS.Std.MemoryExtensions.SequenceEqual));
        if (calls.Length != 1 || catalog.Type(owner).GetMembers("PathsEqual").Length != 0
            || PathEntry(HostSourceOperations.Argument(calls[0], 0)) is not { } first)
            return false;
        ILocalSymbol? second = duplicate ? PathEntry(HostSourceOperations.Argument(calls[0], 1)) : null;
        if (duplicate ? second is null || SymbolEqualityComparer.Default.Equals(first, second)
            : !HostSourceOperations.IsParameter(HostSourceOperations.Argument(calls[0], 1), symbol.Parameters[1]))
            return false;
        foreach (var branch in method.DescendantNodes().OfType<IfStatementSyntax>())
        {
            if (model.GetOperation(branch.Condition) is not IInvocationOperation comparison || comparison.Syntax.Span != calls[0].Syntax.Span)
                continue;
            ReturnStatementSyntax? returned = branch.Statement switch
            {
                ReturnStatementSyntax direct => direct,
                BlockSyntax { Statements.Count: 1 } block => block.Statements[0] as ReturnStatementSyntax,
                _ => null
            };
            if (returned?.Expression is null)
                continue;
            var value = model.GetOperation(returned.Expression);
            if (duplicate)
                return value is IFieldReferenceOperation status && status.Field.Name == nameof(OsPackageStatus.DuplicatePath)
                    && SymbolEqualityComparer.Default.Equals(status.Field.ContainingType, symbol.ReturnType);
            return value is IInvocationOperation factory
                && HostSourceOperations.IsMethod(factory.TargetMethod, typeof(OsPackageFileLookupResult), nameof(OsPackageFileLookupResult.Success))
                && HostSourceOperations.Argument(factory, 1) is ILocalReferenceOperation entry
                && SymbolEqualityComparer.Default.Equals(first, entry.Local);
        }
        return false;

        ILocalSymbol? PathEntry(IOperation path)
        {
            if (HostSourceOperations.LocalInitializer(model, path) is not IInvocationOperation slice
                || slice.TargetMethod.Name != "SliceRange" || !SymbolEqualityComparer.Default.Equals(slice.TargetMethod.ContainingType, symbol.ContainingType)
                || !HostSourceOperations.IsParameter(HostSourceOperations.Argument(slice, 0), symbol.Parameters[0])
                || HostSourceOperations.Argument(slice, 1) is not IPropertyReferenceOperation { Property.Name: nameof(OsPackageFileEntry.PathOffset), Instance: ILocalReferenceOperation offset }
                || HostSourceOperations.Argument(slice, 2) is not IPropertyReferenceOperation { Property.Name: nameof(OsPackageFileEntry.PathLength), Instance: ILocalReferenceOperation length }
                || !SymbolEqualityComparer.Default.Equals(offset.Local, length.Local)
                || !SymbolEqualityComparer.Default.Equals(offset.Local.Type, catalog.Compilation.GetTypeByMetadataName(typeof(OsPackageFileEntry).FullName!))
                || !HostSourceOperations.IsUnmodifiedLocal(model, offset.Local, method))
                return null;
            return offset.Local;
        }
    }
}
