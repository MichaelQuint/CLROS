using OS.Tooling;

internal static partial class M04LayoutContractEfiIntegrationTests
{
    private static string CompileLeaf(string root, bool includeRuntime)
    {
        var efi = HostSourceCatalog.Efi(EfiOptions.Parse(
            ["--" + EndianFlag, "--" + SpanFlag, "--" + ArithmeticFlag, "--" + Flag], true).Profile());
        var source = HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj");
        var selected = HostEfiLeafSources.Select(source, efi, Path.Combine(root, "src", "OS.Kernel", "Runtime"),
            "OS.Kernel.Runtime", "OS.Kernel.Runtime.ObjectArrayStringEETypeMethodTableLayoutContracts")
            .Select(document => document.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (includeRuntime)
            selected.UnionWith(HostNoStdlibSdkSources.Runtime(root, efi, "layout_metadata")
                .Select(document => document.Path));
        return string.Join(Environment.NewLine, efi.Documents.Where(document => selected.Contains(document.Path))
            .Select(document => Compile(root, Path.GetRelativePath(root, document.Path))));
    }
}
