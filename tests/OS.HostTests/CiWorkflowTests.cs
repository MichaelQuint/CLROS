using OS.Tooling;
using System.Runtime.CompilerServices;

internal static class CiWorkflowTests
{
    [HostFixture]
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

        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestIncludesDryRunValidationCommands(string workflow)
    {
        return Contains(workflow, "runs-on: windows")
            && Contains(workflow, "uses: actions/checkout@v4")
            && Contains(workflow, @"dotnet build .\CLROS.slnx --nologo -m:1")
            && Contains(workflow, @"dotnet run --project .\tests\OS.HostTests\OS.HostTests.csproj --no-build -- --lane full")
            && Contains(workflow, @"dotnet run --project .\tools\OS.Tooling --no-build -- efi build --preflight-only")
            && Contains(workflow, @"dotnet run --project .\tools\OS.Tooling --no-build -- ci smoke --dry-run")
            && Contains(workflow, @"dotnet run --project .\tools\OS.Tooling --no-build -- baseline evidence --plan-only")
            && Contains(workflow, "git diff --check");
    }

    private static bool TestPreservesDryRunMetadata(string workflow)
    {
        return Contains(workflow, "  push:")
            && Contains(workflow, "  pull_request:")
            && Contains(workflow, "  workflow_dispatch:")
            && Contains(workflow, $"timeout-minutes: {EvidenceOrchestration.WorkflowTimeoutMinutes}")
            && Contains(workflow, "shell: powershell")
            && Contains(workflow, "if: always()");
    }

    private static bool TestUploadsSmokeLogs(string workflow)
    {
        return Contains(workflow, "uses: actions/upload-artifact@v4")
            && Contains(workflow, "name: clros-ci-smoke-logs")
            && Contains(workflow, "path: artifacts/logs/**");
    }

    private static bool TestExcludesRealSmokeDefaults(string workflow)
    {
        return !Contains(workflow, "--run-qcow2-real-smoke")
            && !Contains(workflow, "qemu smoke --qemu")
            && !Contains(workflow, "qemu qcow2");
    }

    private static bool Contains(string text, string value)
    {
        return text.Contains(value, StringComparison.Ordinal);
    }

    private static string ReadWorkflow()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string path = Path.Combine(root, ".gitea", "workflows", "ci-smoke.yml");
        return File.ReadAllText(path);
    }
}
