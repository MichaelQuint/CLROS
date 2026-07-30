using System.Runtime.CompilerServices;
using System.Text.Json;

internal static class RepositoryRecoveryTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        int failures = 0;
        string root = HostToolProcess.FindRepositoryRoot();

        RunTest("m00.001", () => ValidateBacklog(root), ref failures);
        RunTest("m00.002", () => ValidateSolutionSentinel(root), ref failures);
        RunTest("m00.003", () => ValidateSharedLocatorGroup(root,
        [
            "CiSmokeScriptTests.cs",
            "CiWorkflowTests.cs",
            "CompletionGateTests.cs"
        ]), ref failures);
        RunTest("m00.004", () => ValidateSharedLocatorGroup(root,
        [
            "FetchAppHttpCompletionPlanTests.cs",
            "FetchAppHttpTcpExchangePlanTests.cs",
            "HttpResponseParserTests.cs"
        ]), ref failures);
        RunTest("m00.005", () => ValidateSharedLocatorGroup(root,
        [
            "ImageBuilderTests.cs",
            "NativeAotHelperLedgerTests.cs",
            "NoStdLibShimBoundaryTests.cs"
        ]), ref failures);
        RunTest("m00.006", () => ValidateSharedLocatorGroup(root,
        [
            "SourceFirewallTests.cs",
            "WorkloadProfileTests.cs"
        ]), ref failures);
        RunTest("m00.007", () => ValidateRootScripts(root), ref failures);
        RunTest("m00.008", () => ValidateAgentGuides(root), ref failures);
        RunTest("m00.009", () => ValidateReadmeIdentity(root), ref failures);
        RunTest("m00.010", () => ValidateReferenceOverride(root), ref failures);
        RunTest("m00.011", () => ValidateReferenceInventory(root), ref failures);
        RunTest("m00.012", () => ValidateSharpOsEvidenceClasses(root), ref failures);
        RunTest("m00.013", () => ValidateLicenseBoundaries(root), ref failures);
        RunTest("m00.014", () => ValidateSourceFirewallContract(root), ref failures);
        RunTest("m00.015", () => ValidateCiWorkflow(root), ref failures);
        RunTest("m00.016", () => ValidateGlobalJson(root), ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("M00 repository recovery tests failed: " + failures);
        }
    }

    private static void RunTest(string name, Func<bool> test, ref int failures)
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

    private static bool ValidateBacklog(string root)
    {
        string manifestPath = Path.Combine(root, ".gitea", "backlog", "clros-issues.json");
        string scriptPath = Path.Combine(root, "scripts", "sync-gitea-backlog.ps1");
        if (!File.Exists(manifestPath) || !File.Exists(scriptPath))
        {
            return false;
        }

        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement target = document.RootElement.GetProperty("target");
        return target.GetProperty("baseUrl").GetString() == "http://localhost:3000"
            && target.GetProperty("owner").GetString() == "admin"
            && target.GetProperty("repository").GetString() == "CLROS"
            && document.RootElement.GetProperty("milestones").GetArrayLength() == 16
            && document.RootElement.GetProperty("labels").GetArrayLength() == 68
            && document.RootElement.GetProperty("issues").GetArrayLength() == 343
            && File.ReadAllText(scriptPath).Contains(
                "Refusing non-canonical target",
                StringComparison.Ordinal);
    }

    private static bool ValidateSolutionSentinel(string root)
    {
        return File.Exists(Path.Combine(root, "CLROS.slnx"))
            && !File.Exists(Path.Combine(root, "OS.slnx"))
            && HostToolProcess.FindRepositoryRoot() == root;
    }

    private static bool ValidateSharedLocatorGroup(string root, string[] names)
    {
        string testRoot = Path.Combine(root, "tests", "OS.HostTests");
        foreach (string name in names)
        {
            string text = File.ReadAllText(Path.Combine(testRoot, name));
            if (!text.Contains("HostToolProcess.FindRepositoryRoot()", StringComparison.Ordinal))
            {
                return false;
            }

            // SourceFirewallTests deliberately creates an OS.slnx fixture to
            // prove that the legacy sentinel is rejected. Other locator tests
            // must not retain the old sentinel.
            if (name != "SourceFirewallTests.cs"
                && text.Contains("\"OS.slnx\"", StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateRootScripts(string root)
    {
        foreach (string relativePath in new[]
        {
            "scripts/check-naming.ps1",
            "scripts/run-baseline-evidence.ps1",
            "scripts/run-ci-smoke.ps1"
        })
        {
            string text = File.ReadAllText(Path.Combine(root, relativePath));
            if (!text.Contains("CLROS.slnx", StringComparison.Ordinal))
            {
                return false;
            }
        }

        string naming = File.ReadAllText(Path.Combine(root, "scripts", "check-naming.ps1"));
        return naming.Contains("legacy_solution_sentinel_present", StringComparison.Ordinal)
            && naming.Contains("reference_import_in_first_party_build_input", StringComparison.Ordinal);
    }

    private static bool ValidateAgentGuides(string root)
    {
        string[] paths =
        [
            "AGENTS.md",
            "CLAUDE.md",
            "src/OS/AGENTS.override.md",
            "src/OS.Kernel/AGENTS.override.md",
            "std/no-runtime/shared/AGENTS.override.md",
            "tools/AGENTS.override.md",
            "tests/AGENTS.override.md"
        ];

        foreach (string path in paths)
        {
            string text = File.ReadAllText(Path.Combine(root, path));
            if (text.Contains(@"C:\Projects\OS", StringComparison.Ordinal)
                || text.Contains(@".\OS.slnx", StringComparison.Ordinal))
            {
                return false;
            }
        }

        string rootGuide = File.ReadAllText(Path.Combine(root, "AGENTS.md"));
        return rootGuide.Contains("The project name is `CLROS`.", StringComparison.Ordinal)
            && rootGuide.Contains("http://localhost:3000/admin/CLROS", StringComparison.Ordinal)
            && rootGuide.Contains("`OS` remains the stable assembly", StringComparison.Ordinal);
    }

    private static bool ValidateReadmeIdentity(string root)
    {
        string readme = File.ReadAllText(Path.Combine(root, "README.md"));
        return readme.Contains("alt=\"CLROS logo\"", StringComparison.Ordinal)
            && readme.Contains("included in `CLROS.slnx` under stricter build rules", StringComparison.Ordinal)
            && readme.Contains("only Gate 2 passes", StringComparison.Ordinal)
            && readme.Contains("not cloud-ready", StringComparison.Ordinal);
    }

    private static bool ValidateReferenceOverride(string root)
    {
        string path = Path.Combine(root, "references", "AGENTS.override.md");
        if (!File.Exists(path))
        {
            return false;
        }

        string text = File.ReadAllText(path);
        return text.Contains("read-mostly", StringComparison.OrdinalIgnoreCase)
            && text.Contains("build", StringComparison.OrdinalIgnoreCase)
            && text.Contains("first-party", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ValidateReferenceInventory(string root)
    {
        string map = File.ReadAllText(Path.Combine(root, "docs", "reference-map.md"));
        string[] snapshots =
        [
            "abanu-master", "AtomOS-master", "Aura-Operating-System-master",
            "Brovan-main", "CosmosELF-master", "Cosmos-gen2",
            "CSharpLLVM-master", "dotnet-aot-xc-main", "EfiSharp-main",
            "elfsharp-master", "expressos-master", "FlingOS-develop",
            "GlieseOS-main", "Kernel-master", "KernelSharp-master",
            "libcs-master", "libvirt-csharp-main", "MOOS-master",
            "MOSA-Project-master", "nativeaot-patcher-main", "Nitrocid-main",
            "PatienceOS-main", "RoseOS-master", "Sharpen-master",
            "SharpOS-main", "SharpOS-master", "smolsharp-main",
            "xOS-main", "zerosharp-master"
        ];

        return snapshots.All(
            name => map.Contains("`references/" + name + "`", StringComparison.Ordinal));
    }

    private static bool ValidateSharpOsEvidenceClasses(string root)
    {
        string diff = File.ReadAllText(Path.Combine(root, "docs", "fork-diff.md"));
        return diff.Contains("### SharpOS evidence classes", StringComparison.Ordinal)
            && diff.Contains("media/doom.gif", StringComparison.Ordinal)
            && diff.Contains("media/doom_small.gif", StringComparison.Ordinal)
            && diff.Contains("media/pwsh.png", StringComparison.Ordinal)
            && diff.Contains("media/screenshot.png", StringComparison.Ordinal)
            && diff.Contains("screenshots/*.png", StringComparison.Ordinal)
            && diff.Contains("missing WAD inputs", StringComparison.Ordinal);
    }

    private static bool ValidateLicenseBoundaries(string root)
    {
        string audit = File.ReadAllText(Path.Combine(root, "docs", "license-audit.md"));
        return audit.Contains("SharpOS-main", StringComparison.Ordinal)
            && audit.Contains("nativeaot-patcher-main", StringComparison.Ordinal)
            && audit.Contains("upstream", StringComparison.OrdinalIgnoreCase)
            && audit.Contains("revision", StringComparison.OrdinalIgnoreCase)
            && audit.Contains("license", StringComparison.OrdinalIgnoreCase)
            && audit.Contains("Direct source reuse", StringComparison.Ordinal);
    }

    private static bool ValidateSourceFirewallContract(string root)
    {
        string script = File.ReadAllText(Path.Combine(root, "scripts", "check-naming.ps1"));
        string tests = File.ReadAllText(Path.Combine(root, "tests", "OS.HostTests", "SourceFirewallTests.cs"));
        return script.Contains("$solutionFileName = \"CLROS.slnx\"", StringComparison.Ordinal)
            && script.Contains("\".csproj\"", StringComparison.Ordinal)
            && script.Contains("\".props\"", StringComparison.Ordinal)
            && script.Contains("\".targets\"", StringComparison.Ordinal)
            && tests.Contains("rejects references in solution", StringComparison.Ordinal)
            && tests.Contains("rejects reference imports in props", StringComparison.Ordinal)
            && tests.Contains("rejects reference imports in targets", StringComparison.Ordinal);
    }

    private static bool ValidateCiWorkflow(string root)
    {
        string workflow = File.ReadAllText(Path.Combine(root, ".gitea", "workflows", "ci-smoke.yml"));
        return workflow.Contains("name: CLROS CI smoke", StringComparison.Ordinal)
            && workflow.Contains(@"dotnet build .\CLROS.slnx", StringComparison.Ordinal)
            && workflow.Contains("name: clros-ci-smoke-logs", StringComparison.Ordinal)
            && !workflow.Contains("name: os-ci-smoke-logs", StringComparison.Ordinal);
    }

    private static bool ValidateGlobalJson(string root)
    {
        using JsonDocument document = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(root, "global.json")));
        JsonElement sdk = document.RootElement.GetProperty("sdk");
        return sdk.GetProperty("version").GetString() == "10.0.300"
            && sdk.GetProperty("rollForward").GetString() == "latestPatch"
            && sdk.GetProperty("allowPrerelease").ValueKind == JsonValueKind.False;
    }
}
