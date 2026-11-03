using OS.Tooling;

// Option/command construction only. Copied runner results supply no guest or
// actual QEMU RAM authority; no child, owner, package or capture is started.
internal static class QemuMemoryOptionsTests
{
    [HostTest("qemu RAM option preserves null default and parses positive MiB values")]
    private static void Grammar()
    {
        HostCheck.True(new QemuSmokeOptions().MemoryMiB is null && QemuSmokeOptions.Parse().MemoryMiB is null);
        foreach (string argument in new[] { "-MemoryMiB=256", "--memory-mib:256", "--MemoryMiB=256" })
        {
            var options = QemuSmokeOptions.Parse(argument);
            HostCheck.True(options.MemoryMiB == 256 && options.Explicit.Contains("MemoryMiB"));
        }
        HostCheck.True(QemuSmokeOptions.Parse("-MemoryMiB", "512").MemoryMiB == 512);
        foreach (string value in new[] { "", "-1", "+1", "256M", "1.5", "2147483648" })
        {
            bool refused = false;
            try
            {
                _ = QemuSmokeOptions.Parse("-MemoryMiB=" + value);
            }
            catch (Exception error) when (error is FormatException or OverflowException) { refused = true; }
            HostCheck.True(refused, "invalid integer RAM value " + value);
        }
        foreach (var options in new[] { QemuSmokeOptions.Parse("-MemoryMiB=0"), new QemuSmokeOptions { MemoryMiB = -1 } })
        {
            string? reason = null;
            try
            {
                _ = QemuProfiles.Create(options);
            }
            catch (ToolFailure error) { reason = error.Reason; }
            HostCheck.Equal("qemu_memory_mib_out_of_range", reason);
        }
    }

    [HostTest("qemu invalid parsed and manual RAM values refuse before children or artifact creation")]
    private static async Task BeforeChildren()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (var options in new[]{QemuSmokeOptions.Parse("-MemoryMiB=0"),
            new QemuSmokeOptions{MemoryMiB=0},new QemuSmokeOptions{MemoryMiB=-1},new QemuSmokeOptions{MemoryMiB=int.MinValue}})
        {
            string folder = Path.Combine(root, "artifacts", "host-tests", "qemu-memory-invalid-" + Guid.NewGuid().ToString("N"));
            options.BootPolicy = "Diagnostic";
            options.BuildEfi = true;
            options.Image = Path.Combine(folder, "disk.raw");
            options.EfiArtifact = Path.Combine(folder, "BOOTX64.EFI");
            options.SerialLog = Path.Combine(folder, "serial.log");
            options.SummaryLog = Path.Combine(folder, "summary.log");
            int children = 0;
            using var output = new StringWriter();
            int code = await QemuSmoke.RunAsync(root, options, output, (_, _) =>
            {
                children++;
                throw new InvalidOperationException("invalid RAM must refuse before any child");
            });
            HostCheck.True(code == 1 && children == 0 && !Directory.Exists(folder));
            HostCheck.True(output.ToString().Split('\n').Select(line => line.TrimEnd('\r'))
                .Contains("TEST_FAIL qemu.smoke qemu_memory_mib_out_of_range"));
        }
    }

    [HostTest("qemu common dry arguments forward optional RAM for ordinary web and quota service plans")]
    private static async Task Forwarding()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (string mode in new[] { "ordinary", "web", "quota" })
            foreach (int? memory in new int?[] { null, 256, 512 })
            {
                string folder = Path.Combine(root, "artifacts", "host-tests", "qemu-memory-dry-" + Guid.NewGuid().ToString("N"));
                var options = new QemuSmokeOptions
                {
                    BootPolicy = "Diagnostic",
                    BuildEfi = true,
                    DryRun = true,
                    MemoryMiB = memory,
                    FirmwareRngSource = "virtio",
                    Image = Path.Combine(folder, "disk.raw"),
                    EfiArtifact = Path.Combine(folder, "BOOTX64.EFI"),
                    SerialLog = Path.Combine(folder, "serial.log"),
                    SummaryLog = Path.Combine(folder, "summary.log"),
                    LinuxElfPackage = Path.Combine(folder, "missing.elfpkg"),
                    LinuxThreadingDisassembly = mode == "ordinary" ? "" : Path.Combine(folder, "missing.asm")
                };
                if (mode == "web")
                    options.Flags["EnableM10004Probe"] = true;
                if (mode == "quota")
                {
                    options.Flags["EnableM16014Probe"] = true;
                    options.ServiceQuotaFailure = true;
                }
                int children = 0;
                IReadOnlyList<string>? constructed = null;
                int code = await QemuSmoke.RunAsync(root, options, TextWriter.Null, (name, arguments) =>
                {
                    children++;
                    HostCheck.Equal("OS.QemuRunner", name);
                    constructed = arguments.ToArray();
                    return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
                });
                HostCheck.True(code == 0 && children == 1 && constructed is not null && !Directory.Exists(folder));
                var args = constructed!;
                HostCheck.True(args.Contains("--dry-run"));
                HostCheck.Equal(memory.HasValue ? 1 : 0, args.Count(value => value == "--memory"));
                if (memory is int mib)
                {
                    int at = args.ToList().IndexOf("--memory");
                    HostCheck.Equal(mib.ToString(System.Globalization.CultureInfo.InvariantCulture) + "M", args[at + 1]);
                }
                HostCheck.Equal(mode == "quota", args.Contains("--continuing-service") && args.Contains("--service-quota-failure"));
                HostCheck.Equal(mode == "web", args.Contains("--continuing-web"));
            }
    }
}
