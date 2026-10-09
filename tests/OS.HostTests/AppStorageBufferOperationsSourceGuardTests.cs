using System.Runtime.CompilerServices;

internal static class AppStorageBufferOperationsSourceGuardTests
{

    [HostTest("storage.package staging uses shared buffer operations", HostLane.Fast, 0)]
    private static bool TestPackageStagingUsesSharedBufferOperations()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Storage",
            "OsPackageFileStagingPlan.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(source, destination)", StringComparison.Ordinal)
            && source.Contains("if (!copy.Succeeded)", StringComparison.Ordinal)
            && source.Contains("OsPackageFileStagingStatus.DestinationTooSmall", StringComparison.Ordinal)
            && !source.Contains(".CopyTo(destination)", StringComparison.Ordinal));
    }

    [HostTest("storage.package parser path copy uses shared buffer operations", HostLane.Fast, 1)]
    private static bool TestParserPathCopyUsesSharedBufferOperations()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Storage",
            "OsPackageParser.cs"));

        return HostCheck.Result(source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(path, destination)", StringComparison.Ordinal)
            && source.Contains("if (!copy.Succeeded)", StringComparison.Ordinal)
            && source.Contains("OsPackageStatus.DestinationTooSmall", StringComparison.Ordinal)
            && !source.Contains("path.CopyTo(destination)", StringComparison.Ordinal));
    }
}
