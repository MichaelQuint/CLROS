using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using OS.Kernel.Diagnostics;
using OS.Tooling;

internal static partial class M02PageFaultCr2ErrorCodeEfiIntegrationTests
{
    private const string AdapterOwner = "OS.Boot.EfiEntry+M02PageFaultAdapter";

    private static CSharpSourceCatalog PageFaultSources() =>
        HostSourceCatalog.Efi(EfiOptions.Parse(["--" + Flag], true).Profile());

    private static Type[] PageTableSourceOwners() =>
    [
        typeof(SeparatePageFaultPageTableStatus), typeof(SeparatePageFaultMappingRange),
        typeof(SeparatePageFaultPageTableRequest), typeof(SeparatePageFaultPageTableResult),
        typeof(SeparateDisabledDefaultPageFaultPageTables)
    ];

    private static Type[] TransitionSourceOwners() =>
    [
        typeof(SeparatePageFaultTransitionOperation), typeof(SeparatePageFaultTransitionStatus),
        typeof(SeparatePageFaultTransitionRequest), typeof(SeparatePageFaultTransitionResult),
        typeof(SeparateDisabledDefaultPageFaultTransition)
    ];

    private static IReadOnlyDictionary<string, CSharpSourceDocument> PageFaultPinnedFamily(CSharpSourceCatalog source, params Type[] owners) => HostEfiFaultSources.PinnedFamily(Root(), source, owners);

    private static MethodDeclarationSyntax PageFaultRun(CSharpSourceCatalog source)
    {
        var method = source.Method(AdapterOwner, "Run", symbol => symbol.IsStatic && symbol.Arity == 0
            && symbol.ReturnsVoid && symbol.DeclaredAccessibility == Accessibility.Internal);
        _ = source.MethodInvocations(method);
        return method;
    }

    private static MethodDeclarationSyntax PageFaultCaller(CSharpSourceCatalog source) => HostEfiFaultSources.StageOneCaller(source, PageFaultRun(source));

    private static string PageFaultAuthority(CSharpSourceCatalog source) => HostEfiFaultSources.FalseAuthority(source, nameof(Probes.EnableNullPointerPanicProbe));

    private static string PageFaultHostAdapter() =>
        HostEfiAdapterSources.ForHost(PageFaultSources(), AdapterOwner, "FailBeforeEntry");
}
