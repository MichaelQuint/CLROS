namespace OS.Tooling;

public static class Repository
{
    public const string SolutionName = "CLROS.slnx";

    public static string FindRoot(string? start = null)
    {
        for (DirectoryInfo? directory = new(start ?? AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, SolutionName)))
                return directory.FullName;
        }
        throw new DirectoryNotFoundException("repository_root_not_found sentinel=" + SolutionName);
    }
}
