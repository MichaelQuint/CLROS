using System.Diagnostics;

internal static class HostToolProcess
{
    public const string RepositorySolutionFileName = "CLROS.slnx";

    internal readonly record struct ToolResult(int ExitCode, string Output, string Error);

    // Build provenance follows the current ordered input inventory and exact raw
    // bytes. It must not pin the changing host-tool implementation to an old run.
    public static string SourceClosureSha256(string root) => OS.Tooling.EfiEvidence.Aggregate(
        OS.Tooling.EfiEvidence.SourceClosurePaths(root).Select(path => new OS.Tooling.EfiInput(path,
            OS.Tooling.ContractAudit.Hash(File.ReadAllBytes(Path.Combine(root, path))))));

    // Each fixture is isolated in its own process; pure argument/profile controls
    // can call the same production command without spawning another runtime.
    public static ToolResult RunTool(params string[] arguments)
    {
        TextWriter priorOutput = Console.Out, priorError = Console.Error;
        using StringWriter output = new(System.Globalization.CultureInfo.InvariantCulture);
        using StringWriter error = new(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            int exitCode = OS.Tooling.ToolCommand.Run(arguments);
            return new(exitCode, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(priorOutput);
            Console.SetError(priorError);
        }
    }

    public static ToolResult ValidateEfiProfile(bool build, params string[] arguments)
    {
        try
        {
            var options = OS.Tooling.EfiOptions.Parse(arguments, build);
            options.Profile();
            return new(0, "", "");
        }
        catch (OS.Tooling.ToolFailure error) { return new(1, error.Reason + " " + error.Message, ""); }
    }

    public static string FindLlvmObjdump()
    {
        const string variable = "CLROS_LLVM_OBJDUMP";
        string? configured = Environment.GetEnvironmentVariable(variable)
            ?? (OperatingSystem.IsWindows() ? Environment.GetEnvironmentVariable(variable, EnvironmentVariableTarget.User) : null);
        if (!string.IsNullOrEmpty(configured))
        {
            if (!Path.IsPathFullyQualified(configured) ||
                !string.Equals(Path.GetFileName(configured), "llvm-objdump.exe", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(configured))
            {
                throw new InvalidOperationException(variable + " must name an existing absolute llvm-objdump.exe path.");
            }

            return Path.GetFullPath(configured);
        }

        string visualStudio = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Visual Studio");
        string? discovered = Directory.Exists(visualStudio)
            ? Directory.EnumerateFiles(visualStudio, "llvm-objdump.exe", SearchOption.AllDirectories)
                .Where(path => path.Contains("\\x64\\", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal).FirstOrDefault()
            : null;
        return discovered ?? throw new InvalidOperationException(
            "Missing x64 llvm-objdump.exe. Install Visual Studio's C++ Clang Compiler for Windows component " +
            "or set " + variable + " to a verified user-local tool before running native inspection fixtures.");
    }

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
