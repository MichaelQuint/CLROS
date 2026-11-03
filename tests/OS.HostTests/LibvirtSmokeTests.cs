using OS.Tooling;

internal static class LibvirtSmokeTests
{
    private static string Scratch()
    {
        string path = Path.Combine(HostToolProcess.FindRepositoryRoot(), "artifacts", "libvirt-smoke-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    [HostTest("libvirt smoke CSharp dry-run writes valid XML without host tools")]
    private static void DryRun() => HostCheck.True(ValidateDefaultDryRun());

    internal static bool ValidateDefaultDryRun()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch();
        using StringWriter output = new();
        var options = new LibvirtSmokeOptions { DryRun = true, DomainXml = Path.Combine(directory, "domain.xml"), Image = Path.Combine(directory, "absent.raw") };
        int exit = LibvirtSmoke.RunAsync(root, options, output).GetAwaiter().GetResult();
        HostCheck.Equal(0, exit);
        var xml = System.Xml.Linq.XDocument.Load(options.DomainXml);
        HostCheck.Equal("raw", xml.Descendants("disk").Single().Element("driver")!.Attribute("type")!.Value);
        HostCheck.True(output.ToString().Contains("TEST_PASS libvirt.smoke.dryrun", StringComparison.Ordinal));
        HostCheck.True(output.ToString().Contains("REQUIRED_MARKERS " + string.Join(';', SmokeContracts.RequiredMarkers), StringComparison.Ordinal));
        return true;
    }

    [HostTest("libvirt smoke CSharp lifecycle plan retains opt-in and timeout")]
    private static void Plan()
    {
        string directory = Scratch();
        using StringWriter output = new();
        var options = new LibvirtSmokeOptions
        {
            PlanLifecycle = true,
            AllowRealLibvirtLifecycle = true,
            TimeoutSeconds = 45,
            Network = "default",
            DiskFormat = "qcow2",
            DomainXml = Path.Combine(directory, "domain.xml"),
            LifecyclePlan = Path.Combine(directory, "plan.log"),
            RequireMarkers = ["CUSTOM_PASS"],
            ForbidMarkers = ["CUSTOM_FAIL"]
        };
        HostCheck.Equal(0, LibvirtSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, output).GetAwaiter().GetResult());
        string plan = File.ReadAllText(options.LifecyclePlan);
        HostCheck.True(plan.Contains("LIBVIRT_LIFECYCLE_REAL_RUN_IMPLEMENTED false", StringComparison.Ordinal));
        HostCheck.True(plan.Contains("LIBVIRT_LIFECYCLE_TIMEOUT_SECONDS 45", StringComparison.Ordinal));
        HostCheck.True(plan.Contains("CUSTOM_PASS", StringComparison.Ordinal) && plan.Contains("CUSTOM_FAIL", StringComparison.Ordinal));
    }

    [HostTest("libvirt smoke CSharp validates captured serial and rejects forbidden markers")]
    private static void Serial()
    {
        string directory = Scratch();
        var options = new LibvirtSmokeOptions
        {
            CheckSerialOnly = true,
            SerialLog = Path.Combine(directory, "serial.log"),
            SummaryLog = Path.Combine(directory, "summary.log"),
            DomainXml = Path.Combine(directory, "absent.xml"),
            RequireMarkers = ["OS_SMOKE_PASS"],
            ForbidMarkers = ["OS_SMOKE_FAIL"]
        };
        File.WriteAllText(options.SerialLog, "OS_SMOKE_PASS\r\n");
        using StringWriter output = new();
        HostCheck.Equal(0, LibvirtSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, output).GetAwaiter().GetResult());
        HostCheck.True(File.Exists(options.SummaryLog) && !File.Exists(options.DomainXml));
        File.AppendAllText(options.SerialLog, "OS_SMOKE_FAIL\r\n");
        HostCheck.True(LibvirtSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, output).GetAwaiter().GetResult() != 0);
    }

    [HostTest("libvirt smoke CSharp missing tool and real lifecycle guards remain distinct")]
    private static void Guards()
    {
        string root = HostToolProcess.FindRepositoryRoot(), directory = Scratch();
        var options = new LibvirtSmokeOptions
        {
            Image = Path.Combine(directory, "image.raw"),
            DomainXml = Path.Combine(directory, "domain.xml"),
            Virsh = Path.Combine(directory, "missing.exe")
        };
        File.WriteAllBytes(options.Image, [1, 2, 3, 4]);
        using StringWriter missing = new();
        HostCheck.Equal(0, LibvirtSmoke.RunAsync(root, options, missing).GetAwaiter().GetResult());
        HostCheck.True(missing.ToString().Contains("TEST_PASS libvirt.smoke.missing_tool_reported", StringComparison.Ordinal));
        // Existing file is discovery evidence only. The wrapper must never execute it.
        File.WriteAllText(options.Virsh, "not executable");
        using StringWriter guarded = new();
        HostCheck.Equal(0, LibvirtSmoke.RunAsync(root, options, guarded).GetAwaiter().GetResult());
        HostCheck.True(guarded.ToString().Contains("real_lifecycle_requires_opt_in", StringComparison.Ordinal));
        using StringWriter optedIn = new();
        HostCheck.Equal(2, LibvirtSmoke.RunAsync(root, options with
        {
            AllowRealLibvirtLifecycle = true
        }, optedIn).GetAwaiter().GetResult());
        HostCheck.True(optedIn.ToString().Contains("real_run_not_implemented", StringComparison.Ordinal));
    }

    [HostTest("libvirt smoke CSharp rejects invalid configuration before invocation")]
    private static void Invalid()
    {
        foreach (var options in new[] { new LibvirtSmokeOptions { DiskFormat = "vhd" }, new() { Network = "all" },
            new() { RequireMarkers = [] }, new() { TimeoutSeconds = 0 }, new() { WorkloadProfile = "unknown" } })
        {
            using StringWriter output = new();
            bool called = false;
            int exit = LibvirtSmoke.RunAsync(HostToolProcess.FindRepositoryRoot(), options, output,
                _ => { called = true; throw new InvalidOperationException("must not invoke"); }).GetAwaiter().GetResult();
            HostCheck.True(exit != 0 && !called);
        }
    }

    [HostTest("libvirt smoke CSharp preserves process failure and never writes failed XML")]
    private static void ProcessFailure()
    {
        string directory = Scratch();
        foreach (var result in new[] { new ProcessResult(23, "partial", "failed", false, false, false, TimeSpan.Zero),
            new(0, "partial", "", true, false, false, TimeSpan.Zero), new(0, "partial", "", false, true, false, TimeSpan.Zero) })
        {
            using StringWriter output = new();
            var options = new LibvirtSmokeOptions { DryRun = true, DomainXml = Path.Combine(directory, "failed.xml") };
            HostCheck.True(LibvirtSmoke.RunAsync(directory, options, output, _ => Task.FromResult(result)).GetAwaiter().GetResult() != 0);
            HostCheck.True(!File.Exists(options.DomainXml));
        }
    }

    [HostTest("libvirt smoke CSharp parser supports repeated comma-separated markers")]
    private static void Parsing()
    {
        var options = LibvirtSmokeOptions.Parse(["--dry-run", "--require-marker", "ONE, TWO", "--require-marker", "THREE", "--forbid-marker", "FAIL", "--disk-format", "QCOW2"]);
        HostCheck.True(options.RequireMarkers.SequenceEqual(["ONE", "TWO", "THREE"]));
        HostCheck.True(options.ForbidMarkers.SequenceEqual(["FAIL"]));
        HostCheck.Equal("qcow2", options.DiskFormat);
        foreach (var args in new[] { new[] { "--image" }, new[] { "--unknown" } })
        {
            bool rejected = false;
            try
            {
                LibvirtSmokeOptions.Parse(args);
            }
            catch (ArgumentException) { rejected = true; }
            HostCheck.True(rejected);
        }
    }
}
