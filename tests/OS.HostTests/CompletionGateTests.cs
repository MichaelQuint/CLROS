using System.Runtime.CompilerServices;

internal static class CompletionGateTests
{

    [HostTest("completion.gates ci remains failed without runner evidence", HostLane.Fast, 0)]
    private static bool TestCiGateRemainsFailedWithoutRunnerEvidence()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string completionGates = File.ReadAllText(
            Path.Combine(root, "docs", "completion-gates.md"));

        return HostCheck.Result(ContainsAll(
            completionGates,
            "| 12. CI | Fail |",
            "no executed Gitea CI evidence has been captured yet",
            "Run the workflow on a compatible Windows agent",
            "preserve logs/artifacts from a clean checkout",
            "then add real QEMU smoke in CI-compatible hosts"));
    }

    private static bool ContainsAll(string text, params string[] required)
    {
        foreach (string value in required)
        {
            if (!text.Contains(value, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }
}
