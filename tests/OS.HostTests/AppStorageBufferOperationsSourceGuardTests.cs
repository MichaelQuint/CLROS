using System.Runtime.CompilerServices;

internal static class AppStorageBufferOperationsSourceGuardTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("storage.package staging uses shared buffer operations", TestPackageStagingUsesSharedBufferOperations, ref failures);
        Run("storage.package parser path copy uses shared buffer operations", TestParserPathCopyUsesSharedBufferOperations, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("App/storage buffer operations source guard tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestPackageStagingUsesSharedBufferOperations()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Storage",
            "OsPackageFileStagingPlan.cs"));

        return source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(source, destination)", StringComparison.Ordinal)
            && source.Contains("if (!copy.Succeeded)", StringComparison.Ordinal)
            && source.Contains("OsPackageFileStagingStatus.DestinationTooSmall", StringComparison.Ordinal)
            && !source.Contains(".CopyTo(destination)", StringComparison.Ordinal);
    }

    private static bool TestParserPathCopyUsesSharedBufferOperations()
    {
        string repositoryRoot = HostToolProcess.FindRepositoryRoot();
        string source = File.ReadAllText(Path.Combine(
            repositoryRoot,
            "src",
            "OS.Kernel",
            "Storage",
            "OsPackageParser.cs"));

        return source.Contains("using OS.Std.Memory;", StringComparison.Ordinal)
            && source.Contains("BufferOperations.MemoryCopy(path, destination)", StringComparison.Ordinal)
            && source.Contains("if (!copy.Succeeded)", StringComparison.Ordinal)
            && source.Contains("OsPackageStatus.DestinationTooSmall", StringComparison.Ordinal)
            && !source.Contains("path.CopyTo(destination)", StringComparison.Ordinal);
    }
}
