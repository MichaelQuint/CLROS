using OS.Tooling;

// Selection/command construction only. The invoke seam below starts no child,
// supplies no private Completion, and grants no original/native/guest evidence.
internal static class NativeAotWebRaceSelectionTests
{
    private const string Placeholder = "clros-web-race-00000000000000000000000000000000";
    private static QemuSmokeOptions Options(bool race = false, bool eventOwned = false)
    {
        var options = new QemuSmokeOptions
        {
            BootPolicy = "Diagnostic",
            BuildEfi = true,
            WebSignalRace = race,
            WebEventOwnedSignalRace = eventOwned,
            FirmwareRngSource = "virtio",
            LinuxElfPackage = "absent-web.pkg",
            LinuxThreadingDisassembly = "absent-web.dump"
        };
        options.Flags["EnableM10004Probe"] = true;
        return options;
    }
    private static void Reject(Action action, string expected)
    {
        string? reason = null;
        try
        {
            action();
        }
        catch (ToolFailure error) { reason = error.Reason; }
        HostCheck.Equal(expected, reason);
    }

    [HostTest("web signal race option stays default off and accepts only explicit boolean syntax")]
    private static void OptionGrammar()
    {
        HostCheck.True(!new QemuSmokeOptions().WebSignalRace && !QemuSmokeOptions.Parse().WebSignalRace);
        foreach (string argument in new[] { "--web-signal-race", "-WebSignalRace:true", "--WebSignalRace=$true" })
        {
            var options = QemuSmokeOptions.Parse(argument);
            HostCheck.True(options.WebSignalRace && options.Explicit.Contains("WebSignalRace") && !options.SignalEpollRace && !options.ServiceQuotaFailure);
        }
        var off = QemuSmokeOptions.Parse("--web-signal-race=false");
        HostCheck.True(!off.WebSignalRace && off.Explicit.Contains("WebSignalRace"));
        foreach (string value in new[] { "", "1", "0", "invalid", "true:false" })
        {
            string? reason = null;
            try
            {
                _ = QemuSmokeOptions.Parse("--web-signal-race=" + value);
            }
            catch (ArgumentException error) { reason = error.Message; }
            HostCheck.Equal("qemu_invalid_boolean WebSignalRace", reason);
        }
    }

    [HostTest("web signal race selection requires the actual web profile while explicit false preserves ordinary markers")]
    private static void ProfileAdmission()
    {
        foreach (string profile in new[] { "", "M16011", "M16012", "M16014" })
        {
            var options = Options(true);
            options.Flags.Clear();
            if (profile.Length != 0)
                options.Flags["Enable" + profile + "Probe"] = true;
            Reject(() => QemuProfiles.Create(options), "web_race_requires_web");
        }
        var ordinary = Options();
        var disabled = Options();
        disabled.Explicit.Add("WebSignalRace");
        var first = QemuProfiles.Create(ordinary);
        var second = QemuProfiles.Create(disabled);
        HostCheck.True(!ordinary.WebSignalRace && !disabled.WebSignalRace && first.Required.SequenceEqual(second.Required) &&
            first.Ordered.SequenceEqual(second.Ordered) && first.Unique.SequenceEqual(second.Unique) && first.Forbidden.SequenceEqual(second.Forbidden));
        var enabled = Options(true);
        var selected = QemuProfiles.Create(enabled);
        HostCheck.True(enabled.WebSignalRace && enabled.LinuxThreadingApplication == "MinimalApiSmoke" && selected.IsEnabled("EnableM10004Probe"));
    }

    [HostTest("web signal race dry run constructs one deterministic rendezvous without opening owner logs or inputs")]
    private static async Task DryCommands()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        foreach (bool race in new[] { false, true })
        {
            string folder = Path.Combine(root, "artifacts", "host-tests", "web-race-selection-" + Guid.NewGuid().ToString("N"));
            HostCheck.True(!Directory.Exists(folder));
            var options = Options(race);
            options.DryRun = true;
            options.SerialLog = Path.Combine(folder, "serial.log");
            options.SummaryLog = Path.Combine(folder, "summary.log");
            options.LinuxElfPackage = Path.Combine(folder, "missing.pkg");
            options.LinuxThreadingDisassembly = Path.Combine(folder, "missing.dump");
            options.Image = Path.Combine(folder, "image.raw");
            options.EfiArtifact = Path.Combine(folder, "BOOTX64.EFI");
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
            HostCheck.True(args.Contains("--dry-run") && args.Contains("--continuing-web"));
            HostCheck.Equal(1, args.Count(value => value == "--socket-forward-port"));
            int forward = args.ToList().IndexOf("--socket-forward-port");
            HostCheck.Equal("20080", args[forward + 1]);
            HostCheck.Equal(race ? 1 : 0, args.Count(value => value == "--web-race-pipe"));
            if (race)
            {
                int pipe = args.ToList().IndexOf("--web-race-pipe");
                HostCheck.Equal(Placeholder, args[pipe + 1]);
            }
            else
                HostCheck.True(!args.Any(value => value.StartsWith("clros-web-race-", StringComparison.Ordinal)));
        }
    }

    [HostTest("web signal race live path refuses absent original package before runner owner or evidence creation")]
    private static async Task MissingAdmission()
    {
        string root = HostToolProcess.FindRepositoryRoot(), folder = Path.Combine(root, "artifacts", "host-tests", "web-race-admission-" + Guid.NewGuid().ToString("N"));
        var options = Options(true);
        options.LinuxElfPackage = Path.Combine(folder, "missing.pkg");
        options.LinuxThreadingDisassembly = Path.Combine(folder, "missing.dump");
        options.SerialLog = Path.Combine(folder, "serial.log");
        options.SummaryLog = Path.Combine(folder, "summary.log");
        options.Image = Path.Combine(folder, "image.raw");
        options.EfiArtifact = Path.Combine(folder, "BOOTX64.EFI");
        int children = 0;
        using var output = new StringWriter();
        int code = await QemuSmoke.RunAsync(root, options, output, (_, _) =>
        {
            children++;
            return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
        });
        HostCheck.True(code == 1 && children == 0 && !Directory.Exists(folder) && output.ToString().Contains("TEST_FAIL qemu.smoke ", StringComparison.Ordinal) &&
            !output.ToString().Contains("TEST_PASS qemu.smoke.web", StringComparison.Ordinal));
    }

    [HostTest("web signal race preflight rejects aliased or stale report outputs before opening missing inputs")]
    private static async Task OutputPreflight()
    {
        string root = HostToolProcess.FindRepositoryRoot(), folder = Path.Combine(root, "artifacts", "host-tests", "web-race-output-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        int number = 0;
        try
        {
            async Task Check(Action<QemuSmokeOptions, string>? mutate = null, string? staleSuffix = null)
            {
                string prefix = Path.Combine(folder, (number++).ToString(System.Globalization.CultureInfo.InvariantCulture));
                var options = Options(true);
                options.SerialLog = prefix + ".serial";
                options.SummaryLog = prefix + ".summary";
                options.LinuxElfPackage = prefix + ".missing.pkg";
                options.LinuxThreadingDisassembly = prefix + ".missing.dump";
                options.Image = prefix + ".image.raw";
                options.EfiArtifact = prefix + ".EFI";
                string report = options.SerialLog + ".race.json";
                mutate?.Invoke(options, report);
                byte[] retained = [0, 90, 255, 13, 10, 107];
                string? stale = staleSuffix is null ? null : report + staleSuffix;
                if (stale is not null)
                    File.WriteAllBytes(stale, retained);
                try
                {
                    int children = 0;
                    using var output = new StringWriter();
                    int code = await QemuSmoke.RunAsync(root, options, output, (_, _) =>
                    {
                        children++;
                        return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
                    });
                    HostCheck.True(code == 1 && children == 0);
                    string[] failures = output.ToString().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.TrimEnd('\r'))
                        .Where(line => line.StartsWith("TEST_FAIL qemu.smoke ", StringComparison.Ordinal)).ToArray();
                    HostCheck.Equal(1, failures.Length);
                    HostCheck.Equal("TEST_FAIL qemu.smoke web_race_output_not_fresh_or_aliased", failures.Single());
                    HostCheck.Equal(stale is null ? 0 : 1, Directory.GetFileSystemEntries(folder).Length);
                    if (stale is not null)
                        HostCheck.True(File.ReadAllBytes(stale).SequenceEqual(retained));
                }
                finally { if (stale is not null) File.Delete(stale); }
            }
            await Check((options, report) => options.SummaryLog = report);
            // Even a missing input must not alias a future report pathname.
            await Check((options, report) => options.LinuxElfPackage = report);
            await Check((options, report) => options.LinuxThreadingDisassembly = report);
            await Check((options, report) => options.Image = report);
            await Check((options, report) => options.EfiArtifact = report);
            foreach (string suffix in new[] { "", ".attempts.json", ".wire.json", ".runner.log", ".runner.stderr.log" })
                await Check(staleSuffix: suffix);
        }
        finally { Directory.Delete(folder); }
    }

    [HostTest("web event-owned option has independent strict grammar and cannot conflict with legacy race profile")]
    private static void EventOptionAndProfile()
    {
        HostCheck.True(!new QemuSmokeOptions().WebEventOwnedSignalRace && !QemuSmokeOptions.Parse().WebEventOwnedSignalRace);
        foreach (string argument in new[] { "--web-event-owned-signal-race", "-WebEventOwnedSignalRace:true", "--WebEventOwnedSignalRace=$true" })
        {
            var o = QemuSmokeOptions.Parse(argument);
            HostCheck.True(o.WebEventOwnedSignalRace && !o.WebSignalRace && o.Explicit.Contains("WebEventOwnedSignalRace"));
        }
        foreach (string value in new[] { "", "1", "invalid" })
        {
            string? reason = null;
            try
            {
                _ = QemuSmokeOptions.Parse("--web-event-owned-signal-race=" + value);
            }
            catch (ArgumentException error) { reason = error.Message; }
            HostCheck.Equal("qemu_invalid_boolean WebEventOwnedSignalRace", reason);
        }
        string? duplicate = null;
        try
        {
            _ = QemuSmokeOptions.Parse("--web-event-owned-signal-race", "--web-event-owned-signal-race=false");
        }
        catch (ArgumentException error) { duplicate = error.Message; }
        HostCheck.Equal("qemu_duplicate_option WebEventOwnedSignalRace", duplicate);
        Reject(() => QemuProfiles.Create(Options(race: true, eventOwned: true)), "web_race_profile_conflict");
        var wrong = Options(eventOwned: true);
        wrong.Flags.Clear();
        Reject(() => QemuProfiles.Create(wrong), "web_race_requires_web");
        var enabled = Options(eventOwned: true);
        HostCheck.True(QemuProfiles.Create(enabled).IsEnabled("EnableM10004Probe") && !enabled.WebSignalRace);
        var disabled = QemuSmokeOptions.Parse("--web-event-owned-signal-race=false");
        HostCheck.True(!disabled.WebEventOwnedSignalRace && !disabled.WebSignalRace);
        var ordinary = Options();
        var off = Options();
        off.Explicit.Add("WebEventOwnedSignalRace");
        HostCheck.True(QemuProfiles.Create(ordinary).Required.SequenceEqual(QemuProfiles.Create(off).Required));
    }

    [HostTest("web event-owned dry run selects one v3 pipe while live preflight retains stale bytes before child admission")]
    private static async Task EventDryAndPreflight()
    {
        string root = HostToolProcess.FindRepositoryRoot(), folder = Path.Combine(root, "artifacts", "host-tests", "web-event-selection-" + Guid.NewGuid().ToString("N"));
        var options = Options(eventOwned: true);
        options.DryRun = true;
        options.SerialLog = Path.Combine(folder, "serial.log");
        options.SummaryLog = Path.Combine(folder, "summary.log");
        options.LinuxElfPackage = Path.Combine(folder, "missing.pkg");
        options.LinuxThreadingDisassembly = Path.Combine(folder, "missing.dump");
        options.Image = Path.Combine(folder, "image.raw");
        options.EfiArtifact = Path.Combine(folder, "BOOTX64.EFI");
        int starts = 0;
        IReadOnlyList<string>? command = null;
        int code = await QemuSmoke.RunAsync(root, options, TextWriter.Null, (_, args) =>
        {
            starts++;
            command = args.ToArray();
            return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
        });
        HostCheck.True(code == 0 && starts == 1 && command is not null && !Directory.Exists(folder));
        var arguments = command!;
        HostCheck.True(arguments.Count(a => a == "--web-event-owned-signal-race") == 1 && arguments.Count(a => a == "--web-race-pipe") == 1 && arguments.Count(a => a == "--socket-forward-port") == 1);
        HostCheck.Equal("clros-web-race-v3-00000000000000000000000000000000", arguments[arguments.ToList().IndexOf("--web-race-pipe") + 1]);
        options.DryRun = false;
        starts = 0;
        using (var output = new StringWriter())
        {
            code = await QemuSmoke.RunAsync(root, options, output, (_, _) => { starts++; throw new InvalidOperationException("must not start"); });
            HostCheck.True(code == 1 && starts == 0 && !Directory.Exists(folder) && !output.ToString().Contains("TEST_PASS qemu.smoke.web", StringComparison.Ordinal));
        }
        Directory.CreateDirectory(folder);
        string wire = options.SerialLog + ".race.json.wire.json";
        byte[] retained = [0, 90, 255, 13, 10, 107];
        File.WriteAllBytes(wire, retained);
        try
        {
            using var output = new StringWriter();
            code = await QemuSmoke.RunAsync(root, options, output, (_, _) => { starts++; throw new InvalidOperationException("must not start"); });
            HostCheck.True(code == 1 && starts == 0 && File.ReadAllBytes(wire).SequenceEqual(retained) && Directory.GetFiles(folder).Length == 1);
            HostCheck.True(output.ToString().Contains("TEST_FAIL qemu.smoke web_race_output_not_fresh_or_aliased", StringComparison.Ordinal));
        }
        finally { File.Delete(wire); Directory.Delete(folder); }
    }
}
