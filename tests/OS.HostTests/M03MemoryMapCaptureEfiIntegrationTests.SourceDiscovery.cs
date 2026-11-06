#if !MEMORY_MAP_CAPTURE_HOST
using Microsoft.CodeAnalysis;
using OS.Tooling;

internal static partial class M03MemoryMapCaptureEfiIntegrationTests
{
    private static CSharpSourceCatalog CaptureSources() =>
        HostSourceCatalog.Efi(EfiOptions.Parse(["--" + Flag], true).Profile());

    private static IFieldSymbol CaptureAuthority(CSharpSourceCatalog source) =>
        source.Type("OS.Kernel.Diagnostics.Probes").GetMembers("EnableMemoryMapCaptureProbe")
            .OfType<IFieldSymbol>().Single();
}
#endif
