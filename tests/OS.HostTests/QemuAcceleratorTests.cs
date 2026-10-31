using OS.Tooling;

internal static class QemuAcceleratorTests
{
    [HostTest("qemu accelerator defaults follow host and explicit selections reject invalid or fallback lists")]
    private static void Grammar()
    {
        HostCheck.Equal(OperatingSystem.IsWindows() ? "whpx" : "tcg", QemuSmokeOptions.Parse().Accelerator);
        foreach (string value in new[] { "whpx", "tcg" })
            HostCheck.Equal(value, QemuSmokeOptions.Parse("-Accelerator", value).Accelerator);
        foreach (string value in new[] { "", "auto", "kvm", "whpx:tcg", "tcg,thread=multi", "WHPX" })
        {
            bool refused = false;
            try { _ = QemuSmokeOptions.Parse("-Accelerator=" + value); }
            catch (ArgumentException error) { refused = error.Message == "qemu_accelerator_invalid"; }
            HostCheck.True(refused, "invalid accelerator " + value);
        }
        bool duplicate = false;
        try { _ = QemuSmokeOptions.Parse("-Accelerator=whpx", "-Accelerator=tcg"); }
        catch (ArgumentException error) { duplicate = error.Message == "qemu_duplicate_option Accelerator"; }
        HostCheck.True(duplicate);
    }

    [HostTest("qemu unavailable WHPX refuses explicitly while TCG needs no hypervisor")]
    private static void AvailabilityPolicy()
    {
        foreach (var host in new[] { (Windows: false, Hypervisor: false), (Windows: false, Hypervisor: true), (Windows: true, Hypervisor: false) })
            HostCheck.Equal("qemu_accelerator_unavailable accelerator=whpx", QemuContracts.AcceleratorFailure("whpx", host.Windows, host.Hypervisor));
        HostCheck.True(QemuContracts.AcceleratorFailure("whpx", true, true) is null);
        HostCheck.True(QemuContracts.AcceleratorFailure("tcg", false, false) is null);
        HostCheck.Equal("qemu_accelerator_invalid", QemuContracts.AcceleratorFailure("whpx:tcg", true, true));
        if (!OperatingSystem.IsWindows()) HostCheck.True(!QemuContracts.IsWhpxAvailable());
    }

    [HostTest("qemu tooling propagates one explicit backend without creating artifacts or a VM")]
    private static async Task ToolingPropagation()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (string accelerator in new[] { "whpx", "tcg" })
        {
            var options = QemuSmokeOptions.Parse("-BootPolicy", "Diagnostic", "-DryRun", "-Accelerator", accelerator);
            IReadOnlyList<string>? observed = null;
            int code = await QemuSmoke.RunAsync(root, options, TextWriter.Null, (name, arguments) =>
            {
                HostCheck.Equal("OS.QemuRunner", name);
                observed = arguments.ToArray();
                return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
            });
            HostCheck.True(code == 0 && observed is not null);
            var arguments = observed ?? throw new InvalidOperationException("runner arguments were not observed");
            HostCheck.Equal(1, arguments.Count(a => a == "--accelerator"));
            HostCheck.Equal(accelerator, arguments[arguments.ToList().IndexOf("--accelerator") + 1]);
        }
    }

    [HostTest("qemu invalid manually constructed backend refuses before invoking a child")]
    private static async Task InvalidBeforeChildren()
    {
        int children = 0;
        using StringWriter output = new();
        var options = new QemuSmokeOptions { Accelerator = "whpx:tcg", DryRun = true };
        int code = await QemuSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, output, (_, _) =>
        {
            children++;
            return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
        });
        HostCheck.True(code != 0 && children == 0 && output.ToString().Contains("qemu_accelerator_invalid", StringComparison.Ordinal));
    }

    [HostTest("qemu real runner dry-run records each backend and preserves the explicit CPU")]
    private static async Task RunnerArguments()
    {
        foreach (string accelerator in new[] { "whpx", "tcg" })
        {
            var result = await Runner("--dry-run", "--qemu", "qemu-test", "--accelerator", accelerator);
            HostCheck.True(result.Succeeded && result.StandardOutput.Contains("QEMU_ACCELERATOR " + accelerator, StringComparison.Ordinal));
            HostCheck.True(result.StandardOutput.Contains("q35,accel=" + accelerator, StringComparison.Ordinal));
            HostCheck.True(result.StandardOutput.Contains("-cpu Nehalem", StringComparison.Ordinal));
            HostCheck.True(!result.StandardOutput.Contains("whpx:tcg", StringComparison.Ordinal) && !result.StandardOutput.Contains("-cpu host", StringComparison.Ordinal));
        }
    }

    [HostTest("qemu runner rejects unsupported and duplicate accelerator arguments before startup")]
    private static async Task RunnerRejections()
    {
        foreach (string[] arguments in new[] { new[] { "--accelerator", "whpx:tcg" }, new[] { "--accelerator", "auto" }, new[] { "--accelerator", "whpx", "--accelerator", "tcg" } })
        {
            var result = await Runner(["--dry-run", .. arguments]);
            HostCheck.True(!result.Succeeded && !result.StandardOutput.Contains("TEST_PASS", StringComparison.Ordinal));
            HostCheck.True((result.StandardOutput + result.StandardError).Contains("accelerator", StringComparison.OrdinalIgnoreCase));
        }
    }

    private static Task<ProcessResult> Runner(params string[] arguments)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        return ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(root, "OS.QemuRunner"), .. arguments], root)
        { Timeout = TimeSpan.FromSeconds(20), OutputCharacterLimit = 1024 * 1024 });
    }
}
