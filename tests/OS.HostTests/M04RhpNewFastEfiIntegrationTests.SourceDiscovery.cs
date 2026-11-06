using OS.Tooling;

internal static partial class M04RhpNewFastEfiIntegrationTests
{
    private static CSharpSourceCatalog AllocationSources() => HostSourceCatalog.Efi(
        EfiOptions.Parse(Chain.Append(Flag).Select(flag => "--" + flag).ToArray(), true).Profile());

    private static string[] AllocationSdkSourcePaths()
    {
        var runtime = HostRhpAllocationSources.RuntimeFamily(AllocationSources());
        var host = HostSourceCatalog.Project("tests/OS.HostTests/OS.HostTests.csproj");
        var program = HostRhpAllocationSources.ProgramFamily(host, "RhpCoreTestProgram", "RHP_CORE_TEST");
        return runtime.Concat(program).Select(document => document.Path).ToArray();
    }
}
