using Microsoft.CodeAnalysis.CSharp;
using OS.Kernel.Runtime;
using OS.Tooling;

internal static unsafe partial class GuestTestStringAllocationCharacterStorageTests
{
    private static Type[] StringRuntimeOwners() =>
    [
        typeof(ManagedHeapPageRangeLiveAllocator), typeof(ManagedHeapPageRangeStatus),
        typeof(ManagedHeapPageRangeLifecycle), typeof(ManagedHeapPageRangeState),
        typeof(ManagedHeapPageRangeResult),
        typeof(ObjectArrayStringEETypeMethodTableLayoutContracts), typeof(NativeAotLayoutProfile),
        typeof(NativeAotLayoutShape), typeof(NativeAotLayoutStatus), typeof(NativeAotMethodTablePrefix), typeof(NativeAotLayoutResult),
        typeof(GuestTestRhpNewFast), typeof(RhpNewFastHeapAccess), typeof(RhpNewFastBumpState),
        typeof(RhpNewFastStatus), typeof(RhpNewFastOperation), typeof(RhpNewFastResult),
        typeof(GuestTestStringAllocationCharacterStorage)
    ];

    private static CSharpSourceDocument[] StringSdkSources(string root, CSharpSourceCatalog kernel, CSharpSourceCatalog efi) =>
        HostNoStdlibSdkSources.Select(root, kernel, efi, typeof(GuestTestStringAllocationCharacterStorage), StringRuntimeOwners(), "string_sdk");

    private static CSharpCompilation StringSdkCompilation(IReadOnlyList<CSharpSourceDocument> sources) =>
        HostNoStdlibSdkSources.Compile(sources, "CLROS.M04.String.NoStdLib", "string_sdk");

    private static string[] NoStdLibStringSources(string root)
    {
        var kernel = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var efi = HostSourceCatalog.Efi(EfiOptions.Parse([], false).Profile());
        var sources = StringSdkSources(root, kernel, efi);
        _ = StringSdkCompilation(sources);
        foreach (var source in sources)
            AuthorityFiles.RequirePlainPath(root, source.Path, "string_sdk_source_link");
        return sources.Select(document => Path.GetRelativePath(root, document.Path)).ToArray();
    }
}
