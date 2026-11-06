using System.Diagnostics;
using System.Runtime.CompilerServices;

internal static partial class DoneStepAllocatorScriptTests
{
    [HostFixture(HostLane.Fast)]
    internal static void Run()
    {
        int failures = 0;
        string tempRoot = Path.Combine(Path.GetTempPath(), "os-done-step-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tempRoot);
            Run("done step allocator reports next step without mutating done", () => TestDryRunComputesNextStep(tempRoot), ref failures);
            Run("done step allocator reserves template", () => TestReserveCreatesTemplate(tempRoot), ref failures);
            Run("done step allocator refuses existing target", () => TestReserveRefusesExistingTarget(tempRoot), ref failures);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch
            {
                // Cleanup failure should not hide allocator contract results.
            }
        }

        if (failures != 0)
        {
            throw new InvalidOperationException("Done step allocator script tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        HostCheck.Run("" + name, test, ref failures);
    }

    private static bool TestDryRunComputesNextStep(string tempRoot)
    {
        string root = CreateFixtureRoot(tempRoot, "dryrun");
        string done = Path.Combine(root, "done");
        WriteStep(done, 1);
        WriteStep(done, 9);
        WriteStep(done, 10);
        File.WriteAllText(Path.Combine(done, "stepABC.md"), "# Ignored");
        ProcessResult result = RunAllocator(root);
        return result.ExitCode == 0 && result.Output.Contains("TEST_START done.step.allocate", StringComparison.Ordinal) && result.Output.Contains("NEXT_DONE_STEP done/step011.md", StringComparison.Ordinal) && result.Output.Contains("TEST_PASS done.step.allocate", StringComparison.Ordinal) && !File.Exists(Path.Combine(done, "step011.md"));
    }

    private static bool TestReserveCreatesTemplate(string tempRoot)
    {
        string root = CreateFixtureRoot(tempRoot, "reserve");
        string done = Path.Combine(root, "done");
        WriteStep(done, 1);
        ProcessResult result = RunAllocator(root, "-Reserve");
        string reservedPath = Path.Combine(done, "step002.md");
        string content = File.Exists(reservedPath) ? File.ReadAllText(reservedPath) : string.Empty;
        return result.ExitCode == 0 && result.Output.Contains("NEXT_DONE_STEP done/step002.md", StringComparison.Ordinal) && result.Output.Contains("TEST_PASS done.step.allocate", StringComparison.Ordinal) && content.Contains("## Summary", StringComparison.Ordinal) && content.Contains("## Why", StringComparison.Ordinal) && content.Contains("## Files changed", StringComparison.Ordinal) && content.Contains("## Tests run", StringComparison.Ordinal) && content.Contains("## Evidence", StringComparison.Ordinal) && content.Contains("## Failures", StringComparison.Ordinal) && content.Contains("## Deferred work", StringComparison.Ordinal);
    }

    private static bool TestReserveRefusesExistingTarget(string tempRoot)
    {
        string root = CreateFixtureRoot(tempRoot, "overwrite");
        string done = Path.Combine(root, "done");
        WriteStep(done, 1);
        Directory.CreateDirectory(Path.Combine(done, "step002.md"));
        ProcessResult result = RunAllocator(root, "-Reserve");
        return result.ExitCode == 1 && result.Output.Contains("TEST_START done.step.allocate", StringComparison.Ordinal) && result.Output.Contains("TEST_FAIL done.step.allocate target_exists path=done/step002.md", StringComparison.Ordinal);
    }

    private static string CreateFixtureRoot(string tempRoot, string name)
    {
        string root = Path.Combine(tempRoot, name);
        string done = Path.Combine(root, "done");
        Directory.CreateDirectory(done);
        return root;
    }

    private static void WriteStep(string done, int step)
    {
        string path = Path.Combine(done, "step" + step.ToString("000") + ".md");
        File.WriteAllText(path, "# Step " + step.ToString("000"));
    }

    private static ProcessResult RunAllocator(string root, params string[] extraArguments)
    {
        using StringWriter output = new();
        output.WriteLine("TEST_START done.step.allocate");
        try
        {
            string path = OS.Tooling.DoneSteps.Allocate(root, extraArguments.Contains("-Reserve"));
            output.WriteLine("NEXT_DONE_STEP " + path);
            output.WriteLine("TEST_PASS done.step.allocate");
            return new ProcessResult(0, output.ToString());
        }
        catch (Exception error)
        {
            output.WriteLine("TEST_FAIL done.step.allocate " + error.Message);
            return new ProcessResult(1, output.ToString());
        }
    }
}
