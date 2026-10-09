namespace OS.Tooling;

public static class HostTools
{
    public static string ManagedTool(string root, string name)
    {
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        string path = Path.Combine(root, "tools", name, "bin", configuration, "net10.0", name + ".dll");
        if (!File.Exists(path)) throw new FileNotFoundException("host_tool_not_built build CLROS.slnx first", path);
        return path;
    }

    public static string? FindExecutable(string? requested, string environmentName, string name, string? fallback = null)
    {
        string? configured = string.IsNullOrWhiteSpace(requested) ? Environment.GetEnvironmentVariable(environmentName) : requested;
        if (!string.IsNullOrWhiteSpace(configured)) return File.Exists(configured) ? Path.GetFullPath(configured) : null;
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            foreach (string filename in OperatingSystem.IsWindows() ? new[] { name + ".exe", name } : new[] { name })
            {
                string path = Path.Combine(directory.Trim('"'), filename);
                if (File.Exists(path)) return Path.GetFullPath(path);
            }
        }
        return fallback is not null && File.Exists(fallback) ? Path.GetFullPath(fallback) : null;
    }

    public static Task<ProcessResult> RunAsync(string root, string name, IReadOnlyList<string> args, CancellationToken cancellation = default) =>
        ProcessRunner.RunAsync(new("dotnet", [ManagedTool(root, name), .. args], root), cancellation);
}
