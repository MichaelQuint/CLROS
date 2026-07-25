using System.Runtime.CompilerServices;

internal static class CompletionGateTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run(
            "completion.gates ci remains failed without runner evidence",
            TestCiGateRemainsFailedWithoutRunnerEvidence,
            ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Completion gate host tests failed: " + failures);
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

    private static bool TestCiGateRemainsFailedWithoutRunnerEvidence()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string completionGates = File.ReadAllText(
            Path.Combine(root, "docs", "completion-gates.md"));

        return ContainsAll(
            completionGates,
            "| 12. CI | Fail |",
            "no executed Gitea CI evidence has been captured yet",
            "Run the workflow on a compatible Windows agent",
            "preserve logs/artifacts from a clean checkout",
            "then add real QEMU smoke in CI-compatible hosts");
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
