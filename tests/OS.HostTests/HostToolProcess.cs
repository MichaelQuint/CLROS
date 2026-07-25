using System.Diagnostics;

internal static class HostToolProcess
{
    public const string RepositorySolutionFileName = "CLROS.slnx";

    public static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositorySolutionFileName)))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not find repository root containing " + RepositorySolutionFileName + ".");
    }

    public static string GetBuiltToolPath(string repositoryRoot, string toolProjectName)
    {
        string configuration = GetCurrentBuildConfiguration();
        string targetFramework = GetCurrentTargetFramework();
        return Path.Combine(
            repositoryRoot,
            "tools",
            toolProjectName,
            "bin",
            configuration,
            targetFramework,
            toolProjectName + ".dll");
    }

    public static ProcessStartInfo CreateDotnetToolStartInfo(string repositoryRoot, string toolProjectName)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = repositoryRoot
        };

        startInfo.ArgumentList.Add(GetBuiltToolPath(repositoryRoot, toolProjectName));
        return startInfo;
    }

    private static string GetCurrentBuildConfiguration()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (directory.Parent?.Parent?.Name == "bin")
            {
                return directory.Parent.Name;
            }

            directory = directory.Parent;
        }

        return "Debug";
    }

    private static string GetCurrentTargetFramework()
    {
        DirectoryInfo directory = new DirectoryInfo(AppContext.BaseDirectory);
        return string.IsNullOrWhiteSpace(directory.Name) ? "net10.0" : directory.Name;
    }
}
