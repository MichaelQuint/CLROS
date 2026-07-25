using System.Runtime.CompilerServices;

internal static class CiWorkflowTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;
        string workflow = ReadWorkflow();

        Run("ci.workflow includes dry-run validation commands", ref failures, () => TestIncludesDryRunValidationCommands(workflow));
        Run("ci.workflow preserves dry-run metadata", ref failures, () => TestPreservesDryRunMetadata(workflow));
        Run("ci.workflow uploads smoke logs", ref failures, () => TestUploadsSmokeLogs(workflow));
        Run("ci.workflow excludes real smoke defaults", ref failures, () => TestExcludesRealSmokeDefaults(workflow));

        if (failures != 0)
        {
            throw new InvalidOperationException("CI workflow tests failed: " + failures);
        }
    }

    private static void Run(string name, ref int failures, Func<bool> test)
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
            Console.WriteLine("TEST_FAIL " + name + " exception=" + ex.GetType().Name);
        }
    }

    private static bool TestIncludesDryRunValidationCommands(string workflow)
    {
        return Contains(workflow, "runs-on: windows")
            && Contains(workflow, "uses: actions/checkout@v4")
            && Contains(workflow, @"dotnet build .\OS.slnx --nologo -m:1")
            && Contains(workflow, @".\scripts\build-efi-shell.ps1 -PreflightOnly")
            && Contains(workflow, @".\scripts\run-ci-smoke.ps1 -DryRun")
            && Contains(workflow, @".\scripts\run-baseline-evidence.ps1 -PlanOnly")
            && Contains(workflow, "git diff --check");
    }

    private static bool TestPreservesDryRunMetadata(string workflow)
    {
        return Contains(workflow, "  push:")
            && Contains(workflow, "  pull_request:")
            && Contains(workflow, "  workflow_dispatch:")
            && Contains(workflow, "timeout-minutes: 45")
            && Contains(workflow, "shell: powershell")
            && Contains(workflow, "if: always()");
    }

    private static bool TestUploadsSmokeLogs(string workflow)
    {
        return Contains(workflow, "uses: actions/upload-artifact@v4")
            && Contains(workflow, "name: os-ci-smoke-logs")
            && Contains(workflow, "path: artifacts/logs/**");
    }

    private static bool TestExcludesRealSmokeDefaults(string workflow)
    {
        return !Contains(workflow, "-RunQcow2RealSmoke")
            && !Contains(workflow, @"run-qemu-smoke.ps1 -Qemu")
            && !Contains(workflow, @"run-qemu-qcow2-smoke.ps1");
    }

    private static bool Contains(string text, string value)
    {
        return text.Contains(value, StringComparison.Ordinal);
    }

    private static string ReadWorkflow()
    {
        string root = FindRepositoryRoot();
        string path = Path.Combine(root, ".gitea", "workflows", "ci-smoke.yml");
        return File.ReadAllText(path);
    }

    private static string FindRepositoryRoot()
    {
        string? current = Directory.GetCurrentDirectory();
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "OS.slnx")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
