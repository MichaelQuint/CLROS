using Microsoft.CodeAnalysis.CSharp;
using OS.Kernel.Runtime;
using OS.Tooling;

internal static unsafe partial class GuestTestRhpNewFastTests
{
    private static Type[] NewFastRuntimeOwners() =>
    [
        typeof(ManagedHeapPageRangeLiveAllocator), typeof(ManagedHeapPageRangeStatus),
        typeof(ManagedHeapPageRangeLifecycle), typeof(ManagedHeapPageRangeState),
        typeof(ManagedHeapPageRangeResult),
        typeof(ObjectArrayStringEETypeMethodTableLayoutContracts), typeof(NativeAotLayoutProfile),
        typeof(NativeAotLayoutShape), typeof(NativeAotLayoutStatus), typeof(NativeAotMethodTablePrefix), typeof(NativeAotLayoutResult),
        typeof(GuestTestRhpNewFast), typeof(RhpNewFastHeapAccess), typeof(RhpNewFastBumpState),
        typeof(RhpNewFastStatus), typeof(RhpNewFastOperation), typeof(RhpNewFastResult)
    ];

    private static CSharpSourceDocument[] NewFastSdkSources(string root, CSharpSourceCatalog kernel, CSharpSourceCatalog efi) =>
        HostNoStdlibSdkSources.Select(root, kernel, efi, typeof(GuestTestRhpNewFast), NewFastRuntimeOwners(), "newfast_sdk");

    private static CSharpCompilation NewFastSdkCompilation(IReadOnlyList<CSharpSourceDocument> sources) =>
        HostNoStdlibSdkSources.Compile(sources, "CLROS.M04.NewFast.NoStdLib", "newfast_sdk");

    private static string[] NoStdLibNewFastSources(string root)
    {
        var kernel = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var efi = HostSourceCatalog.Efi(EfiOptions.Parse([], false).Profile());
        var sources = NewFastSdkSources(root, kernel, efi);
        _ = NewFastSdkCompilation(sources);
        foreach (var source in sources)
            AuthorityFiles.RequirePlainPath(root, source.Path, "newfast_sdk_source_link");
        return sources.Select(document => Path.GetRelativePath(root, document.Path)).ToArray();
    }
}
