using System.Runtime.CompilerServices;

internal static class AppStorageBufferOperationsSourceGuardTests
{

    [HostTest("storage.package staging uses shared buffer operations", HostLane.Fast, 0)]
    private static bool TestPackageStagingUsesSharedBufferOperations()
    {
        return HostCheck.Result(StorageBufferCopySourceGuard.Check(
            HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"),
            "OS.Storage.OsPackageFileStagingPlanner", "StageOpenedFile",
            "OS.Storage.OsPackageFileStagingStatus"));
    }

    [HostTest("storage.package parser path copy uses shared buffer operations", HostLane.Fast, 1)]
    private static bool TestParserPathCopyUsesSharedBufferOperations()
    {
        return HostCheck.Result(StorageBufferCopySourceGuard.Check(
            HostSourceCatalog.Project("src/OS.Kernel/OS.Kernel.csproj"),
            "OS.Storage.OsPackageParser", "CopyFilePathByIndex", "OS.Storage.OsPackageStatus"));
    }
}
