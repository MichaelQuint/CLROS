using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class SourceFirewallTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;

        Run("source firewall accepts current tree", TestCurrentTree, ref failures);
        Run("naming guard permits explicit SharpOS reference discussion", TestSharpOsReferenceDiscussion, ref failures);
        Run("naming guard rejects SharpOS as first-party identity", TestSharpOsFirstPartyIdentity, ref failures);
        Run("source firewall rejects forbidden first-party source", TestForbiddenFirstPartySource, ref failures);
        Run("source firewall ignores reference source files", TestReferenceSourceFilesAreIgnored, ref failures);
        Run("source firewall requires CLROS solution sentinel", TestMissingSolution, ref failures);
        Run("source firewall rejects legacy OS solution sentinel", TestLegacySolutionSentinel, ref failures);
        Run("source firewall rejects references in solution", TestReferenceProjectInSolution, ref failures);
        Run("source firewall rejects references in first-party project", TestReferenceProjectInFirstPartyProject, ref failures);
        Run("source firewall accepts clean props and targets", TestCleanBuildInputs, ref failures);
        Run("source firewall rejects reference imports in props", TestReferenceImportInProps, ref failures);
        Run("source firewall rejects reference imports in targets", TestReferenceImportInTargets, ref failures);
        Run("source firewall prunes excluded directories before enumeration", TestPrunedEnumeration, ref failures);
        Run("source firewall preserves excluded file names", TestExcludedFileNames, ref failures);
        Run("source firewall scans untracked files and similar directory names", TestUntrackedAndSimilarNames, ref failures);
        Run("source firewall preserves hidden and junction provider behavior", TestProviderVisibility, ref failures);

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
            Console.WriteLine("TEST_FAIL " + name + " assertion_failed");
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestCurrentTree()
    {
        ProcessResult result = RunGuard(HostToolProcess.FindRepositoryRoot());

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

    private static bool TestSharpOsReferenceDiscussion()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile(
            "docs/reference-note.md",
            "`SharpOS-main` is a SharpOS project snapshot retained as a reference.");

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 0
            && result.Output.Contains("TEST_PASS naming.guard", StringComparison.Ordinal);
    }

    private static bool TestSharpOsFirstPartyIdentity()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("README.md", "Sharp" + "OS project completion gates");

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL naming.guard README.md:1: Use CLROS for first-party project naming",
                StringComparison.Ordinal);
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
        repository.WriteFile("CLROS.slnx", "<Solution><Project Path=\"references/bad/Bad.csproj\" /></Solution>");

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL source.firewall CLROS.slnx:1: reference_project_in_solution", StringComparison.Ordinal);
    }

    private static bool TestMissingSolution()
    {
        using TemporaryRepository repository = TemporaryRepository.Create(includeSolution: false);

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL source.firewall CLROS.slnx: required_solution_missing",
                StringComparison.Ordinal);
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

    private static bool TestLegacySolutionSentinel()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("OS.slnx", "<Solution />");

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL source.firewall OS.slnx: legacy_solution_sentinel_present",
                StringComparison.Ordinal);
    }

    private static bool TestCleanBuildInputs()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile(
            "Directory.Build.props",
            "<Project><PropertyGroup><LangVersion>14.0</LangVersion></PropertyGroup></Project>");
        repository.WriteFile(
            "build/FirstParty.targets",
            "<Project><Target Name=\"Validate\" /></Project>");

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 0
            && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal);
    }

    private static bool TestReferenceImportInProps()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile(
            "Directory.Build.props",
            "<Project><Import Project=\"references\\bad\\Bad.props\" /></Project>");

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL source.firewall Directory.Build.props:1: reference_import_in_first_party_build_input",
                StringComparison.Ordinal);
    }

    private static bool TestReferenceImportInTargets()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile(
            "build/FirstParty.targets",
            "<Project><Import Project=\"..\\references\\bad\\Bad.targets\" /></Project>");

        ProcessResult result = RunGuard(repository.Root);

        return result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL source.firewall build\\FirstParty.targets:1: reference_import_in_first_party_build_input",
                StringComparison.Ordinal);
    }

    private static ProcessResult RunGuard(string root)
    {
        string scriptPath = Path.Combine(
            HostToolProcess.FindRepositoryRoot(),
            "scripts",
            "check-naming.ps1");
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

    private static bool TestPrunedEnumeration()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        string[] excluded = [".claude", ".git", ".vs", "artifacts", "bin", "obj", "references"];
        foreach (string name in excluded)
        {
            repository.WriteFile(name + "/never/Bad.c", "Sharp" + "OS project");
            repository.WriteFile("src/Kept/" + name.ToUpperInvariant() + "/never/Bad.c", "Sharp" + "OS project");
        }

        repository.WriteFile("src/Kept/Code.cs", "// admitted untracked source");
        repository.WriteFile("std/shared/Code.cs", "// admitted shared source");
        repository.WriteFile("apps/Workload.csproj", "<Project />");
        repository.WriteFile("build/FirstParty.targets", "<Project />");
        ProcessResult result = RunObservedGuard(repository.Root);
        string[] visits = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => line.StartsWith("ENUMERATED ", StringComparison.Ordinal))
            .Select(line => line[11..]).Order(StringComparer.Ordinal).ToArray();
        string[] expected = [".", "apps", "build", "src", "src\\Kept", "std", "std\\shared"];
        return result.ExitCode == 0
            && visits.SequenceEqual(expected, StringComparer.Ordinal)
            && result.Output.Contains("TEST_PASS naming.guard", StringComparison.Ordinal)
            && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal);
    }

    private static bool TestExcludedFileNames()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        string[] excluded = [".claude", ".git", ".vs", "artifacts", "bin", "obj", "references"];
        foreach (string name in excluded)
        {
            repository.WriteFile(name, "Sharp" + "OS project");
            repository.WriteFile("src/Kept/" + name.ToUpperInvariant(), "Sharp" + "OS project");
        }

        ProcessResult result = RunGuard(repository.Root);
        return result.ExitCode == 0
            && result.Output.Contains("TEST_PASS naming.guard", StringComparison.Ordinal)
            && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal);
    }

    private static bool TestUntrackedAndSimilarNames()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("src/artifacts-extra/Nested/Identity.md", "Sharp" + "OS project");
        repository.WriteFile("std/binary/Nested/Bad.CPP", "// forbidden untracked source");
        repository.WriteFile("apps/object/Nested/Bad.asm", "; forbidden untracked source");
        repository.WriteFile("references-extra/Nested/Bad.csproj", "<Project><Import Project=\"references/bad.props\" /></Project>");
        repository.WriteFile("build/obj-extra/Nested/Bad.props", "<Project><Import Project=\"references/bad.props\" /></Project>");
        ProcessResult result = RunGuard(repository.Root);
        return !Directory.Exists(Path.Combine(repository.Root, ".git"))
            && result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL naming.guard src\\artifacts-extra\\Nested\\Identity.md:1:", StringComparison.Ordinal)
            && result.Output.Contains("std\\binary\\Nested\\Bad.CPP: forbidden_source_language extension=.cpp", StringComparison.Ordinal)
            && result.Output.Contains("apps\\object\\Nested\\Bad.asm: forbidden_source_language extension=.asm", StringComparison.Ordinal)
            && result.Output.Contains("references-extra\\Nested\\Bad.csproj:1: reference_project_in_first_party_project", StringComparison.Ordinal)
            && result.Output.Contains("build\\obj-extra\\Nested\\Bad.props:1: reference_import_in_first_party_build_input", StringComparison.Ordinal);
    }

    private static bool TestProviderVisibility()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        using TemporaryRepository target = TemporaryRepository.Create(includeSolution: false);
        repository.WriteFile("src/Visible/Code.cs", "// admitted");
        repository.WriteFile("src/Hidden/Bad.c", "Sharp" + "OS project");
        repository.WriteFile("src/hidden.md", "Sharp" + "OS project");
        target.WriteFile("Bad.c", "Sharp" + "OS project");
        File.SetAttributes(Path.Combine(repository.Root, "src", "Hidden"), FileAttributes.Directory | FileAttributes.Hidden);
        File.SetAttributes(Path.Combine(repository.Root, "src", "hidden.md"), FileAttributes.Hidden);
        string link = Path.Combine(repository.Root, "src", "Linked");
        try
        {
            ProcessResult result = RunObservedGuard(repository.Root, target.Root);
            return result.ExitCode == 0
                && result.Output.Contains("PROVIDER_VISIBLE CLROS.slnx|src\\Visible\\Code.cs", StringComparison.Ordinal)
                && !result.Output.Contains("ENUMERATED src\\Hidden", StringComparison.Ordinal)
                && !result.Output.Contains("ENUMERATED src\\Linked", StringComparison.Ordinal)
                && result.Output.Contains("TEST_PASS naming.guard", StringComparison.Ordinal)
                && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal);
        }
        finally
        {
            // Remove only this fixture's junction before recursive fixture cleanup.
            if (Directory.Exists(link))
            {
                Directory.Delete(link);
            }
        }
    }

    private static ProcessResult RunObservedGuard(string root, string? junctionTarget = null)
    {
        // This child-scope proxy observes actual provider calls without a production hook.
        string scriptPath = Path.Combine(HostToolProcess.FindRepositoryRoot(), "scripts", "check-naming.ps1");
        string command = """
            $ErrorActionPreference = 'Stop'
            $fixtureRoot = $env:CLROS_FIREWALL_FIXTURE
            if ($env:CLROS_FIREWALL_JUNCTION) {
                New-Item -ItemType Junction -Path (Join-Path $fixtureRoot 'src/Linked') -Target $env:CLROS_FIREWALL_JUNCTION | Out-Null
                $visible = @(Microsoft.PowerShell.Management\Get-ChildItem -LiteralPath $fixtureRoot -Recurse -File |
                    ForEach-Object { $_.FullName.Substring($fixtureRoot.Length + 1) } | Sort-Object)
                Write-Host ('PROVIDER_VISIBLE ' + ($visible -join '|'))
            }
            function Get-ChildItem {
                param([string] $LiteralPath, [switch] $Recurse, [switch] $File)
                if ($Recurse) { throw 'recursive_enumeration_not_pruned' }
                $relative = if ($LiteralPath -eq $fixtureRoot) { '.' } else { $LiteralPath.Substring($fixtureRoot.Length + 1) }
                foreach ($segment in ($relative -split '[\\/]')) {
                    if (@('.claude', '.git', '.vs', 'artifacts', 'bin', 'obj', 'references') -contains $segment) {
                        throw ('excluded_directory_entered ' + $relative)
                    }
                }
                Write-Host ('ENUMERATED ' + $relative)
                Microsoft.PowerShell.Management\Get-ChildItem @PSBoundParameters
            }
            & $env:CLROS_FIREWALL_SCRIPT -Root $fixtureRoot
            """;
        ProcessStartInfo start = new ProcessStartInfo("powershell.exe")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        start.Environment["CLROS_FIREWALL_FIXTURE"] = root;
        start.Environment["CLROS_FIREWALL_SCRIPT"] = scriptPath;
        start.Environment["CLROS_FIREWALL_JUNCTION"] = junctionTarget ?? "";
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-ExecutionPolicy");
        start.ArgumentList.Add("Bypass");
        start.ArgumentList.Add("-EncodedCommand");
        start.ArgumentList.Add(Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(command)));
        using Process process = Process.Start(start) ?? throw new InvalidOperationException("Failed to start enumeration observer.");
        Task<string> output = process.StandardOutput.ReadToEndAsync();
        Task<string> error = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30_000))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new InvalidOperationException("Enumeration observer timed out.");
        }

        return new ProcessResult(process.ExitCode, output.GetAwaiter().GetResult() + error.GetAwaiter().GetResult());
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

        public static TemporaryRepository Create(bool includeSolution = true)
        {
            string root = Path.Combine(Path.GetTempPath(), "clros-source-firewall-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            TemporaryRepository repository = new TemporaryRepository(root);
            if (includeSolution)
            {
                repository.WriteFile("CLROS.slnx", "<Solution />");
            }

            return repository;
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
