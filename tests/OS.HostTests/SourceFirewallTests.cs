using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static class SourceFirewallTests
{

    [HostTest("source firewall accepts current tree", HostLane.Fast, 0)]
    private static bool TestCurrentTree()
    {
        ProcessResult result = RunGuard(HostToolProcess.FindRepositoryRoot());

        return HostCheck.Result(result.ExitCode == 0
            && result.Output.Contains("TEST_PASS naming.guard", StringComparison.Ordinal)
            && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal));
    }

    [HostTest("source firewall rejects forbidden first-party source", HostLane.Fast, 3)]
    private static bool TestForbiddenFirstPartySource()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("src/Kernel/Bad.c", "int main(void) { return 0; }");

        ProcessResult result = RunGuard(repository.Root);

        return HostCheck.Result(result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL source.firewall src\\Kernel\\Bad.c: forbidden_source_language extension=.c", StringComparison.Ordinal));
    }

    [HostTest("naming guard permits explicit SharpOS reference discussion", HostLane.Fast, 1)]
    private static bool TestSharpOsReferenceDiscussion()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile(
            "docs/reference-note.md",
            "`SharpOS-main` is a SharpOS project snapshot retained as a reference.");

        ProcessResult result = RunGuard(repository.Root);

        return HostCheck.Result(result.ExitCode == 0
            && result.Output.Contains("TEST_PASS naming.guard", StringComparison.Ordinal));
    }

    [HostTest("naming guard rejects SharpOS as first-party identity", HostLane.Fast, 2)]
    private static bool TestSharpOsFirstPartyIdentity()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("README.md", "Sharp" + "OS project completion gates");

        ProcessResult result = RunGuard(repository.Root);

        return HostCheck.Result(result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL naming.guard README.md:1: Use CLROS for first-party project naming",
                StringComparison.Ordinal));
    }

    [HostTest("source firewall ignores reference source files", HostLane.Fast, 4)]
    private static bool TestReferenceSourceFilesAreIgnored()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("references/some-os/Bad.c", "int main(void) { return 0; }");

        ProcessResult result = RunGuard(repository.Root);

        return HostCheck.Result(result.ExitCode == 0
            && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal));
    }

    [HostTest("source firewall rejects references in solution", HostLane.Fast, 7)]
    private static bool TestReferenceProjectInSolution()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("CLROS.slnx", "<Solution><Project Path=\"references/bad/Bad.csproj\" /></Solution>");

        ProcessResult result = RunGuard(repository.Root);

        return HostCheck.Result(result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL source.firewall CLROS.slnx:1: reference_project_in_solution", StringComparison.Ordinal));
    }

    [HostTest("source firewall requires CLROS solution sentinel", HostLane.Fast, 5)]
    private static bool TestMissingSolution()
    {
        using TemporaryRepository repository = TemporaryRepository.Create(includeSolution: false);

        ProcessResult result = RunGuard(repository.Root);

        return HostCheck.Result(result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL source.firewall CLROS.slnx: required_solution_missing",
                StringComparison.Ordinal));
    }

    [HostTest("source firewall rejects references in first-party project", HostLane.Fast, 8)]
    private static bool TestReferenceProjectInFirstPartyProject()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile(
            "src/Kernel/Kernel.csproj",
            "<Project><ItemGroup><ProjectReference Include=\"..\\..\\references\\bad\\Bad.csproj\" /></ItemGroup></Project>");

        ProcessResult result = RunGuard(repository.Root);

        return HostCheck.Result(result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL source.firewall src\\Kernel\\Kernel.csproj:1: reference_project_in_first_party_project",
                StringComparison.Ordinal));
    }

    [HostTest("source firewall rejects legacy OS solution sentinel", HostLane.Fast, 6)]
    private static bool TestLegacySolutionSentinel()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("OS.slnx", "<Solution />");

        ProcessResult result = RunGuard(repository.Root);

        return HostCheck.Result(result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL source.firewall OS.slnx: legacy_solution_sentinel_present",
                StringComparison.Ordinal));
    }

    [HostTest("source firewall accepts clean props and targets", HostLane.Fast, 9)]
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

        return HostCheck.Result(result.ExitCode == 0
            && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal));
    }

    [HostTest("source firewall rejects reference imports in props", HostLane.Fast, 10)]
    private static bool TestReferenceImportInProps()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile(
            "Directory.Build.props",
            "<Project><Import Project=\"references\\bad\\Bad.props\" /></Project>");

        ProcessResult result = RunGuard(repository.Root);

        return HostCheck.Result(result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL source.firewall Directory.Build.props:1: reference_import_in_first_party_build_input",
                StringComparison.Ordinal));
    }

    [HostTest("source firewall rejects reference imports in targets", HostLane.Fast, 11)]
    private static bool TestReferenceImportInTargets()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile(
            "build/FirstParty.targets",
            "<Project><Import Project=\"..\\references\\bad\\Bad.targets\" /></Project>");

        ProcessResult result = RunGuard(repository.Root);

        return HostCheck.Result(result.ExitCode == 1
            && result.Output.Contains(
                "TEST_FAIL source.firewall build\\FirstParty.targets:1: reference_import_in_first_party_build_input",
                StringComparison.Ordinal));
    }

    private static ProcessResult RunGuard(string root)
    {
    using StringWriter output = new();
    var result = OS.Tooling.RepositoryChecks.Check(root);
    result.WriteTo(output);
    return new ProcessResult(result.Succeeded ? 0 : 1, output.ToString());
}

    [HostTest("source firewall prunes excluded directories before enumeration", HostLane.Fast, 12)]
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
        return HostCheck.Result(result.ExitCode == 0
            && visits.SequenceEqual(expected, StringComparer.Ordinal)
            && result.Output.Contains("TEST_PASS naming.guard", StringComparison.Ordinal)
            && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal));
    }

    [HostTest("source firewall preserves excluded file names", HostLane.Fast, 13)]
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
        return HostCheck.Result(result.ExitCode == 0
            && result.Output.Contains("TEST_PASS naming.guard", StringComparison.Ordinal)
            && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal));
    }

    [HostTest("source firewall scans untracked files and similar directory names", HostLane.Fast, 14)]
    private static bool TestUntrackedAndSimilarNames()
    {
        using TemporaryRepository repository = TemporaryRepository.Create();
        repository.WriteFile("src/artifacts-extra/Nested/Identity.md", "Sharp" + "OS project");
        repository.WriteFile("std/binary/Nested/Bad.CPP", "// forbidden untracked source");
        repository.WriteFile("apps/object/Nested/Bad.asm", "; forbidden untracked source");
        repository.WriteFile("references-extra/Nested/Bad.csproj", "<Project><Import Project=\"references/bad.props\" /></Project>");
        repository.WriteFile("build/obj-extra/Nested/Bad.props", "<Project><Import Project=\"references/bad.props\" /></Project>");
        ProcessResult result = RunGuard(repository.Root);
        return HostCheck.Result(!Directory.Exists(Path.Combine(repository.Root, ".git"))
            && result.ExitCode == 1
            && result.Output.Contains("TEST_FAIL naming.guard src\\artifacts-extra\\Nested\\Identity.md:1:", StringComparison.Ordinal)
            && result.Output.Contains("std\\binary\\Nested\\Bad.CPP: forbidden_source_language extension=.cpp", StringComparison.Ordinal)
            && result.Output.Contains("apps\\object\\Nested\\Bad.asm: forbidden_source_language extension=.asm", StringComparison.Ordinal)
            && result.Output.Contains("references-extra\\Nested\\Bad.csproj:1: reference_project_in_first_party_project", StringComparison.Ordinal)
            && result.Output.Contains("build\\obj-extra\\Nested\\Bad.props:1: reference_import_in_first_party_build_input", StringComparison.Ordinal));
    }

    [HostTest("source firewall preserves hidden and junction provider behavior", HostLane.Fast, 15)]
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
            return HostCheck.Result(result.ExitCode == 0
                && result.Output.Contains("PROVIDER_VISIBLE CLROS.slnx|src\\Visible\\Code.cs", StringComparison.Ordinal)
                && !result.Output.Contains("ENUMERATED src\\Hidden", StringComparison.Ordinal)
                && !result.Output.Contains("ENUMERATED src\\Linked", StringComparison.Ordinal)
                && result.Output.Contains("TEST_PASS naming.guard", StringComparison.Ordinal)
                && result.Output.Contains("TEST_PASS source.firewall", StringComparison.Ordinal));
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
    using StringWriter output = new();
    if (junctionTarget is not null)
    {
        TestJunction.Create(Path.Combine(root, "src", "Linked"), junctionTarget);
        output.WriteLine("PROVIDER_VISIBLE " + string.Join("|", OS.Tooling.RepositoryChecks.Enumerate(root)
            .Select(path => Path.GetRelativePath(root, path)).Order(StringComparer.Ordinal)));
    }
    var result = OS.Tooling.RepositoryChecks.Check(root, path =>
        output.WriteLine("ENUMERATED " + Path.GetRelativePath(root, path)));
    result.WriteTo(output);
    return new ProcessResult(result.Succeeded ? 0 : 1, output.ToString());
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
