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

    [HostTest("qemu capture argument admission binds exactly one selected backend and keeps memory and serial guards")]
    private static void CaptureArguments()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        var assembly = System.Reflection.Assembly.LoadFrom(HostTools.ManagedTool(root, "OS.QemuCapture"));
        var method = assembly.GetType("Program", throwOnError: true)!.GetMethod("ReplaceAndValidateArguments",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var validate = method.CreateDelegate<Func<List<string>, string, string, string>>();
        string publicSerial = Path.GetFullPath("capture-public.log"), privateSerial = Path.GetFullPath("capture-private.log");
        List<string> Arguments(string machine, string memory = "256M") =>
            ["-machine", machine, "-m", memory, "-display", "none", "-no-reboot", "-serial", "file:" + publicSerial];
        foreach (string backend in new[] { "whpx", "tcg" })
        {
            var arguments = Arguments("q35,accel=" + backend);
            HostCheck.Equal(publicSerial, validate(arguments, privateSerial, backend));
            HostCheck.Equal("file:" + privateSerial, arguments[^1]);
            HostCheck.Equal("q35,accel=" + backend, arguments[1]);
        }
        foreach (var input in new[] {
            (Selected: "whpx", Machine: "q35,accel=tcg"),
            (Selected: "tcg", Machine: "q35,accel=whpx"),
            (Selected: "whpx", Machine: "q35,accel=whpx:tcg") })
            Reject(() => validate(Arguments(input.Machine), privateSerial, input.Selected), "machine_must_be_q35_selected_accelerator");
        Reject(() => validate(Arguments("q35,accel=whpx", "128M"), privateSerial, "whpx"), "guest_memory_must_be_256M");
        Reject(() => validate(Arguments("q35,accel=tcg"), privateSerial, "auto"), "capture_accelerator_invalid");
        foreach (string alternative in new[] { "-accel", "-enable-kvm", "--enable-kvm" })
        {
            var arguments = Arguments("q35,accel=whpx"); arguments.AddRange([alternative, "tcg"]);
            Reject(() => validate(arguments, privateSerial, "whpx"), "accelerator_must_match_machine_selection");
        }
    }

    [HostTest("qemu capture preparation carries explicit backend without executing a child")]
    private static void CapturePreparation()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string temporary = Path.Combine(Path.GetTempPath(), "clros-accelerator-capture-" + Guid.NewGuid().ToString("N"));
        try
        {
            foreach (string backend in new[] { "whpx", "tcg" })
            {
                string folder = Path.Combine(temporary, backend);
                // The file is a synthetic hash input only; preparation starts no QEMU.
                using var capture = QemuCaptureSession.Create(root, HostTools.ManagedTool(root, "OS.QemuRunner"),
                    Path.Combine(folder, "raw.log"), Path.Combine(folder, "serial.log"), Path.Combine(folder, "summary.log"), 15, false, backend);
                HostCheck.Equal(backend, capture.Accelerator);
                HostCheck.Equal(backend, capture.Environment["CLROS_M01_ACCELERATOR"]);
                HostCheck.Equal(90, capture.RunnerTimeoutSeconds);
                HostCheck.True(!File.Exists(capture.ObservationPath) && !File.Exists(capture.PublicSerialPath));
            }
            Reject(() => QemuCaptureSession.Create(root, "missing", "raw", "serial", "summary", 15, false, "whpx:tcg"),
                "capture_accelerator_invalid");
        }
        finally { if (Directory.Exists(temporary)) Directory.Delete(temporary, recursive: true); }
    }

    private static void Reject(Action action, string reason)
    {
        string? actual = null;
        try { action(); }
        catch (InvalidOperationException error) { actual = error.Message; }
        catch (ToolFailure error) { actual = error.Reason; }
        HostCheck.Equal(reason, actual);
    }

    private static Task<ProcessResult> Runner(params string[] arguments)
    {
        string root = HostToolProcess.FindRepositoryRoot();
        return ProcessRunner.RunAsync(new("dotnet", [HostTools.ManagedTool(root, "OS.QemuRunner"), .. arguments], root)
        { Timeout = TimeSpan.FromSeconds(20), OutputCharacterLimit = 1024 * 1024 });
    }
}
