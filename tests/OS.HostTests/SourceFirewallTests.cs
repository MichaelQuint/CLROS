using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class SourceFirewallTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;

        Run("source firewall accepts current tree", TestCurrentTree, ref failures);
        Run("source firewall rejects forbidden first-party source", TestForbiddenFirstPartySource, ref failures);
        Run("source firewall ignores reference source files", TestReferenceSourceFilesAreIgnored, ref failures);
        Run("source firewall rejects references in solution", TestReferenceProjectInSolution, ref failures);
        Run("source firewall rejects references in first-party project", TestReferenceProjectInFirstPartyProject, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Source firewall tests failed: " + failures);
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

    private static bool TestCurrentTree()
    {
        ProcessResult result = RunGuard(FindRepositoryRoot());

        return result.ExitCode == 0
            && result.Output.Contains("TEST_PASS naming.guard", StringComparison.Ordinal)
            && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal);
    }

    private static bool TestForbiddenFirstPartySource()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("src/Kernel/Bad.c", "int main(void) { return 0; }");

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL source.firewall src\\Kernel\\Bad.c: forbidden_source_language extension=.c", StringComparison.Ordinal);
    }

    private static bool TestReferenceSourceFilesAreIgnored()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("references/some-os/Bad.c", "int main(void) { return 0; }");

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 0
            && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal);
    }

    private static bool TestReferenceProjectInSolution()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("OS.slnx", "<Solution><Project Path=\"references/bad/Bad.csproj\" /></Solution>");

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL source.firewall OS.slnx:1: reference_project_in_solution", StringComparison.Ordinal);
    }

    private static bool TestReferenceProjectInFirstPartyProject()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile(
            "src/Kernel/Kernel.csproj",
            "<Project><ItemGroup><ProjectReference Include=\"..\\..\\references\\bad\\Bad.csproj\" /></ItemGroup></Project>");

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL source.firewall src\\Kernel\\Kernel.csproj:1: reference_project_in_first_party_project",
                StringComparison.Ordinal);
    }

    private static ProcessResult RunGuard(string root)
    {
        string scriptPath = Path.Combine(FindRepositoryRoot(), "scripts", "check-naming.ps1");
        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-File");
        start.ArgumentList.Add(scriptPath);
        start.ArgumentList.Add("-Root");
        start.ArgumentList.Add(root);

        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start PowerShell.");
        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, standardOutput + standardError);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "OS.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find repository root containing OS.slnx.");
    }

    private readonly struct ProcessResult(int exitCode, string output)
    {
        public int ExitCode { get; } = exitCode;

        public string Output { get; } = output;
    }

    private sealed class TemporaryRepository : IDisposable
    {
        private TemporaryRepository(string root)
        {
            Root = root;
        }

        public string Root { get; }

        public static TemporaryRepository Create()
        {
            string root = Path.Combine(Path.GetTempPath(), "os-source-firewall-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            return new TemporaryRepository(root);
        }

        public void WriteFile(string relativePath, string content)
        {
            string path = Path.Combine(Root, relativePath);
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
