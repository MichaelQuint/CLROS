using Microsoft.CodeAnalysis.CSharp;
using OS.Kernel.Runtime;
using OS.Tooling;

internal static unsafe partial class ManagedHeapPageRangeLiveAllocatorTests
{
    private static Type[] HeapRuntimeOwners() =>
    [
        typeof(ManagedHeapPageRangeLiveAllocator), typeof(ManagedHeapPageRangeStatus),
        typeof(ManagedHeapPageRangeLifecycle), typeof(ManagedHeapPageRangeState),
        typeof(ManagedHeapPageRangeResult)
    ];

    private static CSharpSourceDocument[] HeapSdkSources(string root, CSharpSourceCatalog kernel, CSharpSourceCatalog efi) =>
        HostNoStdlibSdkSources.Select(root, kernel, efi, typeof(ManagedHeapPageRangeLiveAllocator), HeapRuntimeOwners(), "heap_sdk");

    private static CSharpCompilation HeapSdkCompilation(IReadOnlyList<CSharpSourceDocument> sources) =>
        HostNoStdlibSdkSources.Compile(sources, "CLROS.M04.Heap.NoStdLib", "heap_sdk");

    private static string[] NoStdLibHeapSources(string root)
    {
        var kernel = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var efi = HostSourceCatalog.Efi(EfiOptions.Parse([], false).Profile());
        var sources = HeapSdkSources(root, kernel, efi);
        _ = HeapSdkCompilation(sources);
        foreach (var source in sources)
            AuthorityFiles.RequirePlainPath(root, source.Path, "heap_sdk_source_link");
        return sources.Select(document => Path.GetRelativePath(root, document.Path)).ToArray();
    }
}
