using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace OS.Tooling;

public sealed record EvidenceOptions
{
    public string WorkloadProfile { get; init; } = "SingleMinimalApi";
    public string LogRoot { get; init; } = "";
    public string DotNet { get; init; } = "dotnet";
    public string Qemu { get; init; } = "";
    public string QemuImg { get; init; } = "";
    public string OvmfCode { get; init; } = "";
    public bool PlanOnly { get; init; }
    public bool SelfTest { get; init; }
    public bool RunQcow2RealSmoke { get; init; }

    public static EvidenceOptions Parse(string[] args)
    {
        var value = new EvidenceOptions();
        for (int i = 0; i < args.Length; i++)
        {
            string Next() => ++i < args.Length ? args[i] : throw new ArgumentException("missing_option_value");
            value = CommandOptions.Key(args[i]) switch
            {
                "dryrun" or "planonly" => value with { PlanOnly = true },
                "selftest" => value with { SelfTest = true },
                "runqcow2realsmoke" => value with { RunQcow2RealSmoke = true },
                "logroot" => value with { LogRoot = Next() },
                "workloadprofile" => value with { WorkloadProfile = Next() },
                "dotnet" => value with { DotNet = Next() },
                "qemu" => value with { Qemu = Next() },
                "qemuimg" => value with { QemuImg = Next() },
                "ovmfcode" => value with { OvmfCode = Next() },
                _ => throw new ArgumentException("unknown_option " + args[i])
            };
        }
        SmokeContracts.ValidateWorkload(value.WorkloadProfile);
        if (value.PlanOnly && value.SelfTest) throw new ArgumentException("self-test cannot be combined with plan-only");
        return value;
    }
}

public static class CommandOptions
{
    public static string Key(string text) => text.TrimStart('-').Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
}

public sealed record EvidenceStep(string Name, string Executable, IReadOnlyList<string> Arguments, string LogPath)
{
    public string Image { get; init; } = "";
    public string DiskFormat { get; init; } = "";
    public string SerialLog { get; init; } = "";
    public string SummaryLog { get; init; } = "";
    public string DomainXml { get; init; } = "";
    public IReadOnlyList<string> MarkerPresets { get; init; } = [];
    public TimeSpan Timeout { get; init; } = TimeSpan.FromMinutes(5);
    public bool IsHostGate { get; init; }
}

public sealed record HostTerminalValidation(bool Valid, string Reason, int PassCount, int FailCount);

public static class EvidenceOrchestration
{
    // One inventory drives both CI plans and their tests. Native execution remains a separate gate.
    public static IReadOnlyList<string> CiMarkerPresets { get; } = Array.AsReadOnly<string>(
    ["diagnostics", "app-args", "app-exit", "cctor-preflight", "cctor-work-items", "fetch-http",
     "fetch-http-endpoint", "fetch-http-tcp-exchange", "http-static-response", "http-static-tcp-response",
     "virtio-rx-tcp-dispatch", "manifest-env", "nativeaot-heap", "pal-export"]);

    // Historical full native host acceptance took 94 minutes. A bounded two-hour gate allows
    // that measured workload while the new direct C# fixtures remove process/build overhead.
    public static TimeSpan FullHostTimeout { get; } = TimeSpan.FromHours(2);
    public const int WorkflowTimeoutMinutes = 150;

    public static IReadOnlyList<EvidenceStep> Plan(string root, string logRoot, EvidenceOptions options, bool baseline)
    {
        SmokeContracts.ValidateWorkload(options.WorkloadProfile);
        string prefix = baseline ? "baseline" : "ci.smoke";
        string configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent?.Name ?? "Debug";
        string tool = Path.Combine(root, "tools", "OS.Tooling", "bin", configuration, "net11.0", "OS.Tooling.dll");
        string host = Path.Combine(root, "tests", "OS.HostTests", "bin", configuration, "net11.0", "OS.HostTests.dll");
        string Log(string file) => Path.Combine(logRoot, file);
        List<EvidenceStep> steps = [];
        EvidenceStep Tool(string name, string log, params string[] args) => new(prefix + "." + name, "dotnet", [tool, .. args], Log(log));
        if (baseline) steps.Add(Tool("naming.guard", "naming-guard.log", "repo", "check"));
        steps.Add(new(prefix + ".build", options.DotNet,
            ["build", "CLROS.slnx", "--nologo", "-m:1", "--configuration", configuration, "/p:OSWorkloadProfile=" + options.WorkloadProfile], Log("dotnet-build.log")) { Timeout = TimeSpan.FromMinutes(20) });
        steps.Add(new(prefix + ".host.tests", options.DotNet, [host, "--lane", "full"], Log("host-tests.log"))
            { Timeout = FullHostTimeout, IsHostGate = true });
        void Qemu(string name, string stem, string? preset = null, bool nativeApp = false)
        {
            string serial = Log(stem + "-serial.log"), summary = Log(stem + ".summary.log");
            List<string> args = ["qemu", "smoke", "--dry-run", "--workload-profile", options.WorkloadProfile,
                "--serial-log", serial, "--summary-log", summary];
            if (preset is not null || nativeApp) args.AddRange(["--boot-policy", "Diagnostic"]);
            if (preset is not null) args.AddRange(["--marker-preset", preset]);
            if (nativeApp) args.Add("--require-native-app-markers");
            steps.Add(Tool(name, stem + "-dry-run.log", [.. args]) with
                { SerialLog = serial, SummaryLog = summary, MarkerPresets = preset is null ? [] : [preset] });
        }
        Qemu("qemu.dryrun", "qemu-smoke");
        if (!baseline) Qemu("nativeapp.dryrun", "qemu-native-app", nativeApp: true);
        foreach (string preset in baseline ? new[] { "diagnostics" } : CiMarkerPresets)
            Qemu((baseline ? "qemu." : "") + preset.Replace('-', '_') + ".dryrun", "qemu-" + preset, preset);
        string raw = options.RunQcow2RealSmoke ? Path.Combine(root, "artifacts", "images", "os.raw") : Log("qemu-qcow2-intent.raw");
        string qcow = options.RunQcow2RealSmoke ? Path.Combine(root, "artifacts", "images", "os.qcow2") : Log("qemu-qcow2-intent.qcow2");
        List<string> qcowArgs = ["qemu", "qcow2", "--raw-image", raw, "--qcow2-image", qcow,
            "--workload-profile", options.WorkloadProfile, "--serial-log", Log("qemu-qcow2-serial.log"), "--summary-log", Log("qemu-qcow2.summary.log")];
        if (!options.RunQcow2RealSmoke) qcowArgs.Add("--dry-run");
        foreach (var (key, value) in new[] { ("--qemu", options.Qemu), ("--qemu-img", options.QemuImg), ("--ovmf-code", options.OvmfCode) })
            if (value.Length != 0) qcowArgs.AddRange([key, value]);
        steps.Add(Tool("qemu.qcow2." + (options.RunQcow2RealSmoke ? "real" : "dryrun"), "qemu-qcow2-smoke.log", [.. qcowArgs]) with
            { Image = qcow, DiskFormat = "qcow2", SerialLog = Log("qemu-qcow2-serial.log"), SummaryLog = Log("qemu-qcow2.summary.log") });
        foreach (bool missing in new[] { false, true })
        {
            string stem = missing ? "libvirt-missing-tool" : "libvirt-dryrun";
            List<string> args = ["libvirt", "smoke", "--image", qcow, "--disk-format", "qcow2", "--workload-profile", options.WorkloadProfile,
                "--serial-log", Log(stem + "-serial.log"), "--summary-log", Log(stem + ".summary.log"), "--domain-xml", Log(stem + ".xml")];
            if (missing) args.AddRange(["--virsh", Log("missing-virsh")]); else args.Add("--dry-run");
            steps.Add(Tool("libvirt." + (missing ? "missingtool" : "dryrun"), stem + ".log", [.. args]) with
                { Image = qcow, DiskFormat = "qcow2", SerialLog = Log(stem + "-serial.log"), SummaryLog = Log(stem + ".summary.log"), DomainXml = Log(stem + ".xml") });
        }
        steps.Add(new(prefix + ".git.diffcheck", "git", ["diff", "--check"], Log("git-diff-check.log")));
        if (baseline) steps.Add(new(prefix + ".git.status", "git", ["status", "--short"], Log("git-status-short.log")));
        return steps;
    }

    public static HostTerminalValidation ValidateHostTerminal(string? text)
    {
        if (text is null) return new(false, "missing_log", 0, 0);
        int pass = Regex.Matches(text, @"(?m)^TEST_PASS host\.tests\r?$").Count;
        int fail = Regex.Matches(text, @"(?m)^TEST_FAIL host\.tests(?:\s.*)?\r?$").Count;
        return new(pass == 1 && fail == 0, pass == 1 && fail == 0 ? "valid" : "terminal_marker_cardinality", pass, fail);
    }

    public static bool SummaryMatches(string path, string schema, string status, string mode, int steps)
    {
        try
        {
            var json = JsonNode.Parse(File.ReadAllText(path))!;
            return (string?)json["schema"] == schema && (string?)json["status"] == status &&
                (string?)json["mode"] == mode && json["steps"]!.AsArray().Count == steps;
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidOperationException) { return false; }
    }

    public static async Task<int> RunAsync(string root, EvidenceOptions options, bool baseline, TextWriter output,
        CancellationToken cancellation = default, Func<ProcessRequest, CancellationToken, Task<ProcessResult>>? execute = null)
    {
        string prefix = baseline ? "baseline" : "ci.smoke";
        try
        {
            SmokeContracts.ValidateWorkload(options.WorkloadProfile);
            if (!File.Exists(Path.Combine(root, "CLROS.slnx"))) throw new IOException("Required solution not found");
            string logRoot = Path.GetFullPath(options.LogRoot.Length == 0 ? Path.Combine(root, "artifacts", "logs",
                (baseline ? "baseline-" : "ci-smoke-") + DateTime.UtcNow.ToString("yyyyMMddTHHmmss") + "-" + Guid.NewGuid().ToString("N")) : options.LogRoot, root);
            if (Directory.Exists(logRoot) && Directory.EnumerateFileSystemEntries(logRoot).Any())
                throw new IOException("Evidence log directory must be new or empty: " + logRoot);
            Directory.CreateDirectory(logRoot);
            string Rel(string path) => Path.GetRelativePath(root, path);
            string summaryPath = Path.Combine(logRoot, baseline ? "summary.json" : "ci-smoke.summary.json");
            string planPath = Path.Combine(logRoot, "ci-smoke.artifacts.json");
            string mode = options.PlanOnly ? baseline ? "plan-only" : "dry-run" : "execute";
            output.WriteLine("WORKLOAD_PROFILE " + options.WorkloadProfile);
            output.WriteLine("TEST_PASS workload.profile.tooling " + (options.WorkloadProfile == "SingleMinimalApi" ? "default single" : "propagates multi"));
            if (options.SelfTest) return SelfTest(logRoot, output);
            var discovery = DiscoverQemuImg(root, options.QemuImg);
            bool available = (bool)discovery["available"]!;
            JsonObject intent = new() { ["mode"] = options.RunQcow2RealSmoke ? "real" : "dry-run",
                ["status"] = options.RunQcow2RealSmoke && !available ? "blocked_by_missing_qemu_img" : "planned",
                ["qemuImgRequired"] = options.RunQcow2RealSmoke, ["qemuImgAvailable"] = available };
            string upper = baseline ? "BASELINE" : "CI_SMOKE";
            output.WriteLine("TEST_START " + prefix + ".qemu_img.discovery");
            output.WriteLine(upper + "_QEMU_IMG_DISCOVERY " + Fields(discovery));
            output.WriteLine(upper + "_QCOW2_CONVERSION_INTENT " + Fields(intent));
            output.WriteLine("TEST_PASS " + prefix + ".qemu_img.discovery");
            var steps = Plan(root, logRoot, options, baseline);
            JsonObject Artifact(EvidenceStep step)
            {
                JsonObject node = new() { ["name"] = step.Name, ["workloadProfile"] = options.WorkloadProfile,
                    ["command"] = CommandText(step.Executable, step.Arguments), ["executable"] = step.Executable,
                    ["arguments"] = new JsonArray(step.Arguments.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray()), ["log"] = Rel(step.LogPath) };
                foreach (var (name, path) in new[] { ("image", step.Image), ("serialLog", step.SerialLog), ("summaryLog", step.SummaryLog), ("domainXml", step.DomainXml) })
                    if (path.Length != 0) node[name] = Rel(path);
                if (step.DiskFormat.Length != 0) node["diskFormat"] = step.DiskFormat;
                if (step.MarkerPresets.Count != 0) node["markerPresets"] = new JsonArray(step.MarkerPresets.Select(x => (JsonNode?)JsonValue.Create(x)).ToArray());
                if (step.Name.Contains(".qcow2.", StringComparison.Ordinal))
                { node["qemuImgDiscovery"] = discovery.DeepClone(); node["qcow2ConversionIntent"] = intent.DeepClone(); }
                return node;
            }
            JsonObject Summary(string status, JsonArray results) => new() { ["schema"] = baseline ? "os.baseline.evidence.summary.v0" : "os.ci.smoke.summary.v0",
                ["status"] = status, ["mode"] = mode, ["timestamp"] = DateTime.UtcNow.ToString("O"), ["workloadProfile"] = options.WorkloadProfile,
                ["repositoryRoot"] = root, ["logDirectory"] = Rel(logRoot), ["summary"] = Rel(summaryPath), ["artifactPlan"] = baseline ? null : Rel(planPath),
                ["qemuImgDiscovery"] = discovery.DeepClone(), ["qcow2ConversionIntent"] = intent.DeepClone(), ["steps"] = results };
            var planned = new JsonArray(steps.Select(x => (JsonNode?)Artifact(x)).ToArray());
            if (!baseline)
            {
                var plan = Summary("planned", (JsonArray)planned.DeepClone()); plan["schema"] = "os.ci.smoke.artifacts.v0"; WriteJson(planPath, plan);
                output.WriteLine("CI_SMOKE_ARTIFACT_PLAN " + Rel(planPath));
            }
            if (options.PlanOnly)
            {
                WriteJson(summaryPath, Summary("planned", planned));
                output.WriteLine("SUMMARY " + Rel(summaryPath));
                output.WriteLine("TEST_PASS " + (baseline ? "baseline.artifact_summary" : "ci.smoke.dryrun"));
                return 0;
            }
            if (!options.RunQcow2RealSmoke)
                foreach (string extension in new[] { "raw", "qcow2" })
                    File.WriteAllBytes(Path.Combine(logRoot, "qemu-qcow2-intent." + extension), "OSINTENT"u8.ToArray());
            JsonArray results = [];
            WriteJson(summaryPath, Summary("incomplete", new JsonArray()));
            execute ??= ProcessRunner.RunAsync;
            foreach (var step in steps)
            {
                cancellation.ThrowIfCancellationRequested();
                DateTime started = DateTime.UtcNow;
                var watch = Stopwatch.StartNew();
                using StreamWriter log = new(step.LogPath, false, new UTF8Encoding(false)) { AutoFlush = true };
                object sync = new();
                void Write(string text) { lock (sync) { output.Write(text); log.Write(text); } }
                Write("TEST_START " + step.Name + "\nCOMMAND " + CommandText(step.Executable, step.Arguments) + "\n");
                int exit = 1;
                string failure = "";
                try
                {
                    var result = await execute(new(step.Executable, step.Arguments, root) { Timeout = step.Timeout,
                        OutputCharacterLimit = 32 * 1024 * 1024, OutputReceived = (text, _) => Write(text) }, cancellation);
                    exit = result.Succeeded ? 0 : result.ExitCode == 0 ? 1 : result.ExitCode;
                    if (step.IsHostGate)
                    {
                        var terminal = ValidateHostTerminal(result.StandardOutput + "\n" + result.StandardError);
                        if (!terminal.Valid) { exit = exit == 0 ? 90 : exit; failure = terminal.Reason; }
                    }
                    if (result.Cancelled) throw new OperationCanceledException(cancellation);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e) { failure = e.GetType().Name + ": " + e.Message.ReplaceLineEndings(" "); }
                Write((exit == 0 ? "TEST_PASS " : "TEST_FAIL ") + step.Name + (exit == 0 ? "\n" : " exit_code=" + exit + " " + failure + "\n"));
                var node = Artifact(step); node["exitCode"] = exit; node["status"] = exit == 0 ? "passed" : "failed";
                node["startedAt"] = started.ToString("O"); node["endedAt"] = DateTime.UtcNow.ToString("O"); node["durationSeconds"] = watch.Elapsed.TotalSeconds;
                results.Add(node);
                WriteJson(summaryPath, Summary("incomplete", (JsonArray)results.DeepClone()));
            }
            int failed = results.Count(x => (int?)x?["exitCode"] != 0);
            string status = failed == 0 ? "passed" : "failed";
            var summary = Summary(status, results); WriteJson(summaryPath, summary);
            if (!SummaryMatches(summaryPath, (string)summary["schema"]!, status, mode, steps.Count)) throw new IOException("summary_roundtrip_mismatch");
            output.WriteLine("SUMMARY " + Rel(summaryPath));
            output.WriteLine((failed == 0 ? "TEST_PASS " : "TEST_FAIL ") + (baseline ? "baseline.evidence" : "ci.smoke") + (failed == 0 ? "" : " failures=" + failed));
            return failed == 0 ? 0 : 1;
        }
        catch (Exception e)
        {
            output.WriteLine("TEST_FAIL " + (baseline ? "baseline.evidence" : "ci.smoke") + " " + e.Message.ReplaceLineEndings(" "));
            return 1;
        }
    }

    public static JsonObject DiscoverQemuImg(string root, string requested)
    {
        string? configured = string.IsNullOrWhiteSpace(requested) ? Environment.GetEnvironmentVariable("QEMU_IMG") : requested;
        string source = !string.IsNullOrWhiteSpace(requested) ? "configured" : !string.IsNullOrWhiteSpace(configured) ? "environment" : "auto";
        string? path = null;
        if (!string.IsNullOrWhiteSpace(configured))
        {
            string absolute = Path.GetFullPath(configured, root);
            path = File.Exists(absolute) ? absolute : null;
            if (path is null && !configured.Contains('/') && !configured.Contains('\\')) path = SearchPath(configured);
        }
        else
        {
            path = SearchPath("qemu-img");
            if (path is not null) source = "PATH";
            else foreach (string candidate in new[] { @"C:\Program Files\qemu\qemu-img.exe", @"C:\Program Files (x86)\qemu\qemu-img.exe",
                @"C:\ProgramData\chocolatey\bin\qemu-img.exe", @"C:\msys64\mingw64\bin\qemu-img.exe", "/usr/bin/qemu-img", "/usr/local/bin/qemu-img" })
                if (File.Exists(candidate)) { path = candidate; source = "well-known"; break; }
        }
        return new() { ["requested"] = configured ?? "auto", ["source"] = source, ["resolved"] = path is null ? "not_found" : Path.GetRelativePath(root, path), ["available"] = path is not null };
    }

    private static string? SearchPath(string name)
    {
        foreach (string directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory)) continue;
            foreach (string suffix in OperatingSystem.IsWindows() ? new[] { ".exe", "" } : new[] { "" })
            { string path = Path.Combine(directory.Trim('"'), name + suffix); if (File.Exists(path)) return Path.GetFullPath(path); }
        }
        return null;
    }

    public static string CommandText(string executable, IEnumerable<string> arguments) => string.Join(' ', new[] { executable }.Concat(arguments).Select(x =>
        x.Length == 0 || x.Any(c => char.IsWhiteSpace(c) || c == '"') ? "\"" + x.Replace("\"", "\\\"", StringComparison.Ordinal) + "\"" : x));
    private static string Fields(JsonObject node) => string.Join(' ', node.Select(x => x.Key + "=" + (x.Value is JsonValue v && v.TryGetValue<bool>(out bool b) ? b.ToString().ToLowerInvariant() : x.Value?.ToString())));
    private static void WriteJson(string path, JsonNode value)
        => AtomicReceipt.WriteJson(path, value);
    private static int SelfTest(string logRoot, TextWriter output)
    {
        foreach (var (name, text, expected) in new (string, string?, bool)[] { ("missing_host_log", null, false),
            ("single_host_pass", "TEST_PASS host.tests\r\n", true), ("duplicate_host_pass", "TEST_PASS host.tests\nTEST_PASS host.tests\n", false),
            ("conflicting_host_terminal", "TEST_PASS host.tests\nTEST_FAIL host.tests failures=1\n", false) })
        {
            if (ValidateHostTerminal(text).Valid != expected) throw new InvalidOperationException(name);
            output.WriteLine("TEST_PASS baseline.evidence.selftest " + name);
        }
        string path = Path.Combine(logRoot, "selftest-summary.json");
        WriteJson(path, new JsonObject { ["schema"] = "selftest", ["status"] = "passed", ["mode"] = "execute", ["steps"] = new JsonArray(new JsonObject()) });
        if (!SummaryMatches(path, "selftest", "passed", "execute", 1) || SummaryMatches(path, "selftest", "failed", "execute", 1)) throw new InvalidOperationException("summary_mismatch");
        output.WriteLine("TEST_PASS baseline.evidence.selftest summary_mismatch");
        File.WriteAllText(path, "{not-json");
        if (SummaryMatches(path, "selftest", "passed", "execute", 1)) throw new InvalidOperationException("malformed_summary");
        output.WriteLine("TEST_PASS baseline.evidence.selftest malformed_summary");
        output.WriteLine("TEST_PASS baseline.evidence.selftest"); return 0;
    }
}

public static class CiSmoke
{
    public static Task<int> RunAsync(string root, string[] args, TextWriter output, CancellationToken cancellation = default) =>
        EvidenceOrchestration.RunAsync(root, EvidenceOptions.Parse(args), false, output, cancellation);
}

public static class BaselineEvidence
{
    public static Task<int> RunAsync(string root, string[] args, TextWriter output, CancellationToken cancellation = default) =>
        EvidenceOrchestration.RunAsync(root, EvidenceOptions.Parse(args), true, output, cancellation);
}
