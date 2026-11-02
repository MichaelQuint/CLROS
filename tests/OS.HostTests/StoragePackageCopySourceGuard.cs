using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using OS.Storage;
using OS.Tooling;

/// <summary>Checks that package copies consume the selected file's content and,
/// for range reads, the caller's offset and count. Existing behavior tests check
/// actual bytes, invalid ranges and destination bounds.</summary>
internal static class StoragePackageCopySourceGuard
{
    internal static bool Check(CSharpSourceCatalog catalog, string owner, string methodName, bool range)
    {
        if (!StorageBufferCopySourceGuard.Check(catalog, owner, methodName, typeof(OsPackageFileSystemStatus).FullName!))
            return false;
        var method = catalog.Method(owner, methodName);
        var model = catalog.Compilation.GetSemanticModel(method.SyntaxTree);
        var declaration = (IMethodSymbol)model.GetDeclaredSymbol(method)!;
        var copy = StorageSourceGuard.Calls(catalog, method, typeof(OS.Std.Memory.BufferOperations),
            nameof(OS.Std.Memory.BufferOperations.MemoryCopy)).Single();
        IOperation source = StorageSourceGuard.Argument(copy, 0);
        if (range)
        {
            if (StorageSourceGuard.LocalInitializer(model, source) is not IInvocationOperation slice
                || slice.TargetMethod.Name != nameof(ReadOnlySpan<byte>.Slice)
                || !SymbolEqualityComparer.Default.Equals(slice.TargetMethod.ContainingType.OriginalDefinition,
                    catalog.Compilation.GetTypeByMetadataName("System.ReadOnlySpan`1"))
                || slice.Arguments.Length != 2 || slice.Instance is null
                || !StorageSourceGuard.IsParameter(StorageSourceGuard.Argument(slice, 0), declaration.Parameters[2])
                || !StorageSourceGuard.IsParameter(StorageSourceGuard.Argument(slice, 1), declaration.Parameters[3]))
                return false;
            source = StorageSourceGuard.Value(slice.Instance);
        }
        if (source is not IPropertyReferenceOperation content || content.Instance is null
            || content.Property.Name != nameof(OsPackageFileContentResult.Content)
            || content.Property.ContainingType.ToDisplayString() != typeof(OsPackageFileContentResult).FullName
            || StorageSourceGuard.LocalInitializer(model, content.Instance) is not IInvocationOperation lookup
            || !StorageSourceGuard.IsMethod(lookup.TargetMethod, typeof(OsPackageParser), nameof(OsPackageParser.GetFileContent))
            || !SymbolEqualityComparer.Default.Equals(content.Property.ContainingType, lookup.TargetMethod.ReturnType))
            return false;
        return StorageSourceGuard.IsParameter(StorageSourceGuard.Argument(lookup, 0), declaration.Parameters[0])
            && StorageSourceGuard.IsParameter(StorageSourceGuard.Argument(lookup, 1), declaration.Parameters[1]);
    }
}
