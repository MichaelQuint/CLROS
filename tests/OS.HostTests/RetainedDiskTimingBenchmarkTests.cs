using OS.Tooling;

internal static class RetainedDiskTimingBenchmarkTests
{
    [HostTest("retained timing preserves the current dry-run marker contract without launching")]
    private static async Task Planning()
    {
        var recipe = QemuSmokeOptions.Parse("-DryRun", "-BuildEfi", "-EnableM10018TimingProbe",
            "-Accelerator", "tcg", "-LinuxElfPackage", "retained.pkg",
            "-LinuxThreadingDisassembly", "retained.txt", "-WebRootFixture", "DiskA",
            "-WebRootAssets", "assets", "-Image", "retained.raw", "-EfiArtifact", "retained.EFI",
            "-SerialLog", "artifacts/retained-test/serial.log", "-SummaryLog", "artifacts/retained-test/summary.log",
            "-TimeoutSeconds", "600");
        var arguments = await RetainedDiskTimingBenchmark.CreateRunnerArgumentsAsync(Repository.FindRoot(), recipe);
        HostCheck.True(arguments.Contains("--disk-read-timing"));
        HostCheck.True(arguments.Contains("--readonly-webroot-disk"));
        HostCheck.True(arguments.Contains("--require-ordered-marker"));
        HostCheck.True(arguments.Contains("--forbid-marker"));
        HostCheck.True(!arguments.Contains("--dry-run") && !arguments.Contains("--socket-forward-port"));
        HostCheck.Equal("30", arguments[Array.IndexOf(arguments, "--startup-timeout") + 1]);
    }

    [HostTest("retained timing rejects recipes that could run the ordinary builder")]
    private static async Task RejectNonDiagnostic()
    {
        foreach (string[] arguments in new[] { Array.Empty<string>(), new[] { "-DryRun" }, new[] { "-DryRun", "-BuildEfi" } })
        {
            bool rejected = false;
            try
            {
                _ = await RetainedDiskTimingBenchmark.CreateRunnerArgumentsAsync(Repository.FindRoot(), QemuSmokeOptions.Parse(arguments));
            }
            catch (ToolFailure)
            {
                rejected = true;
            }
            HostCheck.True(rejected);
        }
    }
}
