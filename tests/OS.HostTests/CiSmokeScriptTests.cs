using OS.Tooling;
using System.Text.Json.Nodes;

internal static class CiSmokeScriptTests
{
    [HostTest("ci smoke compiled tool lanes continue after controlled build failure", HostLane.Native)]
    internal static void CompiledLanes()
    {
        string root = HostToolProcess.FindRepositoryRoot();
        string directory = Path.Combine(root, "artifacts", "orchestration-integration", Guid.NewGuid().ToString("N"));
        var options = new EvidenceOptions { LogRoot = directory };
        Task<OS.Tooling.ProcessResult> Execute(ProcessRequest request, CancellationToken cancellation)
        {
            if (request.Arguments.FirstOrDefault() == "build" || request.Arguments.Contains("--lane"))
                return Task.FromResult(new OS.Tooling.ProcessResult(42, "controlled_failure\n", "", false, false, false, TimeSpan.Zero));
            return ProcessRunner.RunAsync(request, cancellation);
        }
        using StringWriter output = new();
        int exit = EvidenceOrchestration.RunAsync(root, options, false, output, execute: Execute).GetAwaiter().GetResult();
        HostCheck.Equal(1, exit);
        var summary = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "ci-smoke.summary.json")))!;
        var steps = summary["steps"]!.AsArray();
        HostCheck.Equal(EvidenceOrchestration.Plan(root, directory, options, false).Count, steps.Count);
        foreach (var node in steps)
        {
            string name = (string)node!["name"]!;
            HostCheck.Equal(name is "ci.smoke.build" or "ci.smoke.host.tests" ? 42 : 0, (int)node["exitCode"]!, name + " log=" + (string?)node["log"]);
            if (node["markerPresets"] is JsonArray presets)
            {
                string text = File.ReadAllText(Path.GetFullPath((string)node["log"]!, root));
                foreach (var preset in presets) HostCheck.True(text.Contains("QEMU_MARKER_PRESETS " + (string)preset!, StringComparison.Ordinal));
            }
        }
    }

    [HostFixture]
    internal static void Run()
    {
        int failures = 0;
        string root = HostToolProcess.FindRepositoryRoot();
        string temporary = Path.Combine(root, "artifacts", "orchestration-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporary);
        string directory = Path.Combine(temporary, "ci-plan");
        var options = new EvidenceOptions { PlanOnly = true, LogRoot = directory };
        using StringWriter output = new();
        int result = EvidenceOrchestration.RunAsync(root, options, false, output).GetAwaiter().GetResult();
        var plan = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "ci-smoke.artifacts.json")))!.AsObject();
        var steps = EvidenceOrchestration.Plan(root, directory, options, false);
        void Check(string name, Func<bool> test) => HostCheck.Run(name, test, ref failures);
        Check("ci.smoke.dryrun writes artifact plan", () => result == 0 && (string?)plan["schema"] == "os.ci.smoke.artifacts.v0" &&
            (string?)plan["mode"] == "dry-run" && (string?)plan["status"] == "planned" && plan["steps"]!.AsArray().Count == steps.Count &&
            steps.Count(x => x.Arguments.FirstOrDefault() == "build") == 1 && steps.Single(x => x.IsHostGate).Arguments.TakeLast(2).SequenceEqual(new[] { "--lane", "full" }));
        Check("ci.smoke.dryrun includes serial and summary logs", () => steps.Where(x => x.SerialLog.Length != 0).All(x =>
            x.SummaryLog.Length != 0 && x.Arguments.Contains("--serial-log") && x.Arguments.Contains("--summary-log")));
        foreach (string preset in EvidenceOrchestration.CiMarkerPresets)
        {
            string marker = preset == "http-static-tcp-response" ? "http static tcp response" : preset;
            Check("ci.smoke.dryrun includes " + marker + " marker preset", () =>
            {
                var step = steps.Single(x => x.MarkerPresets.Contains(preset));
                return Option(step.Arguments, "--marker-preset") == preset && Option(step.Arguments, "--boot-policy") == "Diagnostic" && step.Arguments.Contains("--dry-run") &&
                    Path.GetFileName(step.SerialLog) == "qemu-" + preset + "-serial.log" && Path.GetFileName(step.SummaryLog) == "qemu-" + preset + ".summary.log";
            });
        }
        Check("ci.smoke.dryrun includes qcow2 smoke intent", () => ValidateQcow(steps, false));
        Check("ci.smoke.dryrun records missing qemu-img discovery", () => Discovery(root, temporary, false, false));
        Check("ci.smoke.dryrun records found qemu-img discovery", () => Discovery(root, temporary, true, false));
        Check("ci.smoke.dryrun includes libvirt smoke intent", () => ValidateLibvirt(steps));
        Check("ci.smoke.dryrun includes git diff check", () => steps.Any(x => x.Executable == "git" && x.Arguments.SequenceEqual(new[] { "diff", "--check" })));
        Check("ci.smoke.dryrun records default workload profile", () => (string?)plan["workloadProfile"] == "SingleMinimalApi" &&
            steps.Single(x => x.Name.EndsWith(".qemu.dryrun", StringComparison.Ordinal)).Arguments.Contains("--boot-policy") == false);
        Check("ci.smoke.dryrun propagates multi workload profile", () => Profile(root, temporary));
        Check("ci.smoke rejects invalid workload profile", () => Rejects(() => EvidenceOptions.Parse(["--workload-profile", "invalid"])));
        Check("ci.smoke returns nonzero on failed command", () => ExecuteControls(root, temporary, false));
        Check("baseline evidence script wires qcow2 and libvirt intent", () =>
        {
            var baseline = EvidenceOrchestration.Plan(root, temporary, options with { WorkloadProfile = "MultiMinimalApi" }, true);
            return ValidateQcow(baseline, false) && ValidateLibvirt(baseline) && baseline.Where(x => x.SerialLog.Length != 0).All(x => Option(x.Arguments, "--workload-profile") == "MultiMinimalApi");
        });
        Check("baseline evidence script enforces complete host gate", () => ExecuteControls(root, temporary, true));
        Check("baseline evidence self-test rejects malformed terminals and summary", () =>
        {
            using StringWriter log = new();
            return EvidenceOrchestration.RunAsync(root, options with { PlanOnly = false, SelfTest = true, LogRoot = Path.Combine(temporary, "selftest") }, true, log).GetAwaiter().GetResult() == 0 &&
                log.ToString().Contains("TEST_PASS baseline.evidence.selftest malformed_summary", StringComparison.Ordinal);
        });
        Check("baseline evidence script records artifact summary lanes", () => BaselinePlan(root, temporary));
        Check("baseline evidence script rejects nonempty log root", () =>
        {
            string used = Path.Combine(temporary, "used"); Directory.CreateDirectory(used); File.WriteAllText(Path.Combine(used, "stale.log"), "stale");
            using StringWriter log = new();
            return EvidenceOrchestration.RunAsync(root, options with { LogRoot = used }, true, log).GetAwaiter().GetResult() == 1 && log.ToString().Contains("must be new or empty", StringComparison.Ordinal);
        });
        Check("baseline evidence plan records missing qemu-img discovery", () => Discovery(root, temporary, false, true));
        Check("baseline evidence plan records found qemu-img discovery", () => Discovery(root, temporary, true, true));
        Check("orchestration rejects partial host success markers", () => new[] { "", "TEST_PASS host.tests.fast\n", "TEST_PASS host.tests.focused\n",
            "TEST_PASS host.tests\nTEST_PASS host.tests\n", "TEST_PASS host.tests\nTEST_FAIL host.tests failures=1\n" }.All(x => !EvidenceOrchestration.ValidateHostTerminal(x).Valid) &&
            EvidenceOrchestration.ValidateHostTerminal("TEST_PASS host.tests\r\n").Valid);
        Check("orchestration cancellation preserves incomplete evidence", () => Cancellation(root, temporary));
        if (failures != 0) throw new InvalidOperationException("Orchestration checks failed: " + failures);
    }

    private static bool Profile(string root, string temporary)
    {
        var options = new EvidenceOptions { WorkloadProfile = "MultiMinimalApi" };
        var steps = EvidenceOrchestration.Plan(root, temporary, options, false);
        return steps.Single(x => x.Arguments.FirstOrDefault() == "build").Arguments.Contains("/p:OSWorkloadProfile=MultiMinimalApi") &&
            steps.Where(x => x.SerialLog.Length != 0).All(x => Option(x.Arguments, "--workload-profile") == options.WorkloadProfile);
    }
    private static bool ValidateQcow(IReadOnlyList<EvidenceStep> steps, bool real)
    {
        var step = steps.Single(x => x.Name.Contains(".qcow2.", StringComparison.Ordinal));
        return step.DiskFormat == "qcow2" && step.Arguments.Contains("qcow2") && Option(step.Arguments, "--qcow2-image") == step.Image &&
            step.Arguments.Contains("--dry-run") != real && step.SerialLog.Length != 0 && step.SummaryLog.Length != 0;
    }
    private static bool ValidateLibvirt(IReadOnlyList<EvidenceStep> steps)
    {
        var dry = steps.Single(x => x.Name.EndsWith("libvirt.dryrun", StringComparison.Ordinal));
        var missing = steps.Single(x => x.Name.EndsWith("libvirt.missingtool", StringComparison.Ordinal));
        return dry.Arguments.Contains("--dry-run") && !missing.Arguments.Contains("--dry-run") && Option(missing.Arguments, "--virsh").EndsWith("missing-virsh", StringComparison.Ordinal) &&
            dry.DomainXml.Length != 0 && missing.DomainXml.Length != 0 && dry.Image == missing.Image && dry.DiskFormat == "qcow2";
    }
    private static bool Discovery(string root, string temporary, bool found, bool baseline)
    {
        string directory = Path.Combine(temporary, "discovery-" + baseline + "-" + found);
        string tool = Path.Combine(temporary, "qemu-img-" + baseline + "-" + found + ".exe");
        if (found) File.WriteAllText(tool, "controlled tool discovery fixture, never executed");
        var options = new EvidenceOptions { PlanOnly = true, LogRoot = directory, RunQcow2RealSmoke = true, QemuImg = tool };
        using StringWriter log = new();
        if (EvidenceOrchestration.RunAsync(root, options, baseline, log).GetAwaiter().GetResult() != 0) return false;
        var summary = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, baseline ? "summary.json" : "ci-smoke.summary.json")))!;
        return (bool?)summary["qemuImgDiscovery"]?["available"] == found && (string?)summary["qemuImgDiscovery"]?["source"] == "configured" &&
            (string?)summary["qcow2ConversionIntent"]?["status"] == (found ? "planned" : "blocked_by_missing_qemu_img") &&
            (bool?)summary["qcow2ConversionIntent"]?["qemuImgRequired"] == true && ValidateQcow(EvidenceOrchestration.Plan(root, directory, options, baseline), true);
    }
    private static bool BaselinePlan(string root, string temporary)
    {
        string directory = Path.Combine(temporary, "baseline-plan");
        var options = new EvidenceOptions { PlanOnly = true, LogRoot = directory };
        using StringWriter log = new();
        if (EvidenceOrchestration.RunAsync(root, options, true, log).GetAwaiter().GetResult() != 0) return false;
        var steps = EvidenceOrchestration.Plan(root, directory, options, true);
        return EvidenceOrchestration.SummaryMatches(Path.Combine(directory, "summary.json"), "os.baseline.evidence.summary.v0", "planned", "plan-only", steps.Count) &&
            steps.Count(x => x.Arguments.FirstOrDefault() == "build") == 1 && steps.Any(x => x.Name == "baseline.naming.guard") && steps.Any(x => x.Name == "baseline.git.status") &&
            !Directory.EnumerateFiles(directory).Any(x => x.EndsWith(".raw", StringComparison.Ordinal));
    }
    private static bool ExecuteControls(string root, string temporary, bool baseline)
    {
        foreach (string kind in new[] { "build-failure", "host-missing", "host-duplicate", "host-fast", "host-fail", "timeout", "overflow", "exception", "valid" })
        {
            string directory = Path.Combine(temporary, "execute-" + baseline + "-" + kind);
            var options = new EvidenceOptions { LogRoot = directory };
            var steps = EvidenceOrchestration.Plan(root, directory, options, baseline);
            List<ProcessRequest> calls = [];
            Task<OS.Tooling.ProcessResult> Execute(ProcessRequest request, CancellationToken _)
            {
                calls.Add(request); bool host = request.Arguments.Contains("--lane"); bool build = request.Arguments.FirstOrDefault() == "build";
                if (kind == "exception" && build) throw new IOException("controlled failure");
                string text = !host ? "CONTROL_OK\n" : kind switch
                { "host-missing" => "", "host-fast" => "TEST_PASS host.tests.fast\n", "host-duplicate" => "TEST_PASS host.tests\nTEST_PASS host.tests\n",
                    "host-fail" => "TEST_PASS host.tests\nTEST_FAIL host.tests failures=1\n", _ => "TEST_PASS host.tests\n" };
                request.OutputReceived?.Invoke(text, false);
                return Task.FromResult(new OS.Tooling.ProcessResult(kind == "build-failure" && build ? 42 : 0, text, "", kind == "timeout" && host, kind == "overflow" && host, false, TimeSpan.Zero));
            }
            using StringWriter log = new();
            int exit = EvidenceOrchestration.RunAsync(root, options, baseline, log, execute: Execute).GetAwaiter().GetResult();
            var summary = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, baseline ? "summary.json" : "ci-smoke.summary.json")))!;
            if (calls.Count != steps.Count || exit != (kind == "valid" ? 0 : 1) || (string?)summary["status"] != (kind == "valid" ? "passed" : "failed") ||
                summary["steps"]!.AsArray().Count != steps.Count || calls.Count(x => x.Arguments.FirstOrDefault() == "build") != 1) { Console.WriteLine("ORCHESTRATION_CONTROL " + kind + " " + log); return false; }
            if (calls.Single(x => x.Arguments.Contains("--lane")).Timeout != EvidenceOrchestration.FullHostTimeout) return false;
            if (kind == "build-failure" && (int?)summary["steps"]!.AsArray().Single(x => ((string?)x?["name"])?.EndsWith(".build", StringComparison.Ordinal) == true)!["exitCode"] != 42) return false;
        }
        return true;
    }
    private static bool Cancellation(string root, string temporary)
    {
        string directory = Path.Combine(temporary, "cancelled");
        using CancellationTokenSource cancellation = new(); int calls = 0;
        Task<OS.Tooling.ProcessResult> Execute(ProcessRequest request, CancellationToken token)
        {
            if (++calls == 2) { cancellation.Cancel(); throw new OperationCanceledException(token); }
            return Task.FromResult(new OS.Tooling.ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
        }
        using StringWriter log = new();
        int exit = EvidenceOrchestration.RunAsync(root, new() { LogRoot = directory }, false, log, cancellation.Token, Execute).GetAwaiter().GetResult();
        var summary = JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "ci-smoke.summary.json")))!;
        return exit == 1 && calls == 2 && (string?)summary["status"] == "incomplete" && summary["steps"]!.AsArray().Count == 1;
    }
    private static string Option(IReadOnlyList<string> args, string key)
    { for (int i = 0; i + 1 < args.Count; i++) if (args[i] == key) return args[i + 1]; return ""; }
    private static bool Rejects(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } }
}
