using Microsoft.CodeAnalysis.CSharp;
using OS.Kernel.Runtime;
using OS.Tooling;

internal static unsafe partial class GuestTestArrayAllocationLengthOverflowChecksTests
{
    private static Type[] ArrayRuntimeOwners() =>
    [
        typeof(ManagedHeapPageRangeLiveAllocator), typeof(ManagedHeapPageRangeStatus),
        typeof(ManagedHeapPageRangeLifecycle), typeof(ManagedHeapPageRangeState),
        typeof(ManagedHeapPageRangeResult),
        typeof(ObjectArrayStringEETypeMethodTableLayoutContracts), typeof(NativeAotLayoutProfile),
        typeof(NativeAotLayoutShape), typeof(NativeAotLayoutStatus), typeof(NativeAotMethodTablePrefix), typeof(NativeAotLayoutResult),
        typeof(GuestTestRhpNewFast), typeof(RhpNewFastHeapAccess), typeof(RhpNewFastBumpState),
        typeof(RhpNewFastStatus), typeof(RhpNewFastOperation), typeof(RhpNewFastResult),
        typeof(GuestTestArrayAllocationLengthOverflowChecks)
    ];

    private static CSharpSourceDocument[] ArraySdkSources(string root, CSharpSourceCatalog kernel, CSharpSourceCatalog efi) =>
        HostNoStdlibSdkSources.Select(root, kernel, efi, typeof(GuestTestArrayAllocationLengthOverflowChecks), ArrayRuntimeOwners(), "array_sdk");

    private static CSharpCompilation ArraySdkCompilation(IReadOnlyList<CSharpSourceDocument> sources) =>
        HostNoStdlibSdkSources.Compile(sources, "CLROS.M04.Array.NoStdLib", "array_sdk");

    private static string[] NoStdLibArraySources(string root)
    {
        var kernel = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var efi = HostSourceCatalog.Efi(EfiOptions.Parse([], false).Profile());
        var sources = ArraySdkSources(root, kernel, efi);
        _ = ArraySdkCompilation(sources);
        foreach (var source in sources)
            AuthorityFiles.RequirePlainPath(root, source.Path, "array_sdk_source_link");
        return sources.Select(document => Path.GetRelativePath(root, document.Path)).ToArray();
    }
}
