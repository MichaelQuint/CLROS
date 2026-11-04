using OS.Tooling;

internal static partial class M04ArithmeticConformanceEfiIntegrationTests
{
    private static string CompileLeaf(string root, bool includeRuntime)
    {
        var efi = HostSourceCatalog.Efi(EfiOptions.Parse(
            ["--" + EndianFlag, "--" + SpanFlag, "--" + Flag], true).Profile());
        var source = HostSourceCatalog.Project("src/OS.Std.NoRuntime.Shared/OS.Std.NoRuntime.Shared.csproj");
        var selected = HostEfiLeafSources.Select(source, efi, Path.Combine(root, "std", "no-runtime", "shared"),
            "OS.Std.Numerics", "OS.Std.Numerics.IndependentArithmeticShiftCastN64BitDivisionRemainder")
            .Select(document => document.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (includeRuntime)
            selected.UnionWith(HostNoStdlibSdkSources.Runtime(root, efi, "arithmetic_metadata")
                .Select(document => document.Path));
        return string.Join(Environment.NewLine, efi.Documents.Where(document => selected.Contains(document.Path))
            .Select(document => Compile(root, Path.GetRelativePath(root, document.Path))));
    }
}
