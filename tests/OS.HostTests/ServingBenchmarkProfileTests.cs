using OS.Tooling;

internal static class ServingBenchmarkProfileTests
{
    private const string Benchmark = "EnableM10018ServingBenchmarkProbe";
    private const string Full = "EnableM10018FullProbe";

    [HostTest("serving benchmark stays disabled for default EFI build and verification selections", HostLane.Fast, 0)]
    private static void Defaults()
    {
        HostCheck.Equal(1, EfiContracts.DiagnosticProbes.Count(name => name == Benchmark));
        foreach (bool build in new[] { true, false })
        {
            var options = EfiOptions.Parse([], build);
            var profile = options.Profile();
            HostCheck.True(!options.Probes.ContainsKey(Benchmark) && !options.Flag(Benchmark));
            HostCheck.True(profile.Flags.ContainsKey(Benchmark) && !profile.IsEnabled(Benchmark));
            HostCheck.True(profile.Flags.Values.All(enabled => !enabled));
            HostCheck.Equal("VerifiedExit", profile.BootPolicy);
            HostCheck.True(!profile.CommandArguments().Contains("--" + Benchmark));

            var disabled = EfiOptions.Parse(["-" + Benchmark + ":false"], build).Profile();
            HostCheck.Equal("VerifiedExit", disabled.BootPolicy);
            HostCheck.True(disabled.Flags.Values.All(enabled => !enabled));
        }
    }

    [HostTest("serving benchmark selects exactly the existing full serving prerequisites", HostLane.Fast, 1)]
    private static void FullPrerequisites()
    {
        var full = EfiProfile.Create("", new Dictionary<string, bool> { [Full] = true });
        var selected = new Dictionary<string, bool> { [Benchmark] = true };
        var benchmark = EfiProfile.Create("", selected);
        HostCheck.Equal("Diagnostic", benchmark.BootPolicy);
        HostCheck.True(benchmark.IsEnabled(Full) && benchmark.IsEnabled("EnableM10018Probe") &&
            benchmark.IsEnabled("EnableM16025Probe") && benchmark.IsEnabled("EnableM10004Probe"));
        HostCheck.True(benchmark.Flags.All(pair => pair.Value == (pair.Key == Benchmark || full.IsEnabled(pair.Key))));
        HostCheck.Equal(1, selected.Count);
        HostCheck.True(selected[Benchmark]);
        HostCheck.True(benchmark.CommandArguments().Contains("--" + Benchmark) &&
            benchmark.CommandArguments().Contains("--" + Full));
        HostCheck.True(!full.IsEnabled(Benchmark) && !full.CommandArguments().Contains("--" + Benchmark));
    }

    [HostTest("serving benchmark CLI supports explicit selection and full profile opt out", HostLane.Fast, 2)]
    private static void CliSelection()
    {
        foreach (bool build in new[] { true, false })
        {
            foreach (string[] arguments in new[] { new[] { "-" + Benchmark }, new[] { "--" + Benchmark + "=true" } })
            {
                var options = EfiOptions.Parse(arguments, build);
                HostCheck.True(options.Probes[Benchmark] && options.Flag(Benchmark));
                HostCheck.True(options.Profile().IsEnabled(Full));
            }

            var disabled = EfiOptions.Parse(["-" + Full, "-" + Benchmark, "false"], build);
            var ordinary = EfiProfile.Create("", new Dictionary<string, bool> { [Full] = true });
            HostCheck.True(disabled.Probes.TryGetValue(Benchmark, out bool enabled) && !enabled);
            HostCheck.True(!disabled.Flag(Benchmark) && !disabled.Profile().IsEnabled(Benchmark));
            HostCheck.True(disabled.Profile().Flags.All(pair => pair.Value == ordinary.IsEnabled(pair.Key)));
        }
    }

    [HostTest("serving benchmark respects explicit false full disk filesystem and socket prerequisites", HostLane.Fast, 3)]
    private static void ExplicitFalsePrerequisites()
    {
        foreach (var (parent, reason) in new[]
        {
            (Full, "serving_benchmark_explicit_false_prerequisite"),
            ("EnableM10018Probe", "disk_static_full_explicit_false_prerequisite"),
            ("EnableM16025Probe", "disk_static_explicit_false_prerequisite"),
            ("EnableM10004Probe", "disk_static_explicit_false_prerequisite"),
            ("EnableM16023Probe", "disk_read_explicit_false_prerequisite"),
            ("EnableM16024Probe", "disk_read_explicit_false_prerequisite"),
            ("EnableM16012Probe", "disk_read_explicit_false_prerequisite"),
            ("EnableM16011Probe", "m16_012_explicit_false_prerequisite")
        })
        {
            Reject(() => EfiProfile.Create("", new Dictionary<string, bool> { [Benchmark] = true, [parent] = false }), reason);
            foreach (bool build in new[] { true, false })
                Reject(() => EfiOptions.Parse(["-" + Benchmark, "-" + parent + ":false"], build).Profile(), reason);
        }
    }

    [HostTest("serving benchmark rejects timing trace and summary modes with the exact conflict reason", HostLane.Fast, 4)]
    private static void TraceConflicts()
    {
        foreach (string conflict in new[] { "EnableM10018TimingProbe", "EnableM10018EvidenceTraceProbe", "EnableM10018EvidenceSummaryProbe" })
        {
            Reject(() => EfiProfile.Create("", new Dictionary<string, bool> { [Benchmark] = true, [conflict] = true }),
                "serving_benchmark_trace_conflict");
            foreach (bool build in new[] { true, false })
                Reject(() => EfiOptions.Parse(["-" + Benchmark, "-" + conflict], build).Profile(), "serving_benchmark_trace_conflict");
            var disabled = EfiProfile.Create("", new Dictionary<string, bool> { [Benchmark] = true, [conflict] = false });
            HostCheck.True(disabled.IsEnabled(Benchmark) && !disabled.IsEnabled(conflict));
        }
        Reject(() => EfiProfile.Create("", new Dictionary<string, bool>
        {
            [Benchmark] = true, ["EnableM10018EvidenceTraceProbe"] = true, ["EnableM10018EvidenceSummaryProbe"] = true
        }), "serving_benchmark_trace_conflict");
    }

    [HostTest("serving benchmark preserves writable conflict rejection and ordinary evidence profiles", HostLane.Fast, 5)]
    private static void ExistingProfileContracts()
    {
        foreach (string writable in new[] { "EnableM16027Probe", "EnableM16027TimingProbe" })
        {
            var ordinary = EfiProfile.Create("", new Dictionary<string, bool> { [writable] = true });
            HostCheck.True(ordinary.IsEnabled(writable) && !ordinary.IsEnabled(Benchmark));
            var original = Reject(() => EfiProfile.Create("", new Dictionary<string, bool> { [Full] = true, [writable] = true }),
                "writable_disk_mechanical_profile_conflict");
            var benchmark = Reject(() => EfiProfile.Create("", new Dictionary<string, bool> { [Benchmark] = true, [writable] = true }),
                "writable_disk_mechanical_profile_conflict");
            HostCheck.Equal(original.Detail, benchmark.Detail);
        }
        foreach (string evidence in new[] { "EnableM10018EvidenceTraceProbe", "EnableM10018EvidenceSummaryProbe" })
        {
            var ordinary = EfiProfile.Create("", new Dictionary<string, bool> { [evidence] = true });
            HostCheck.True(ordinary.IsEnabled(evidence) && ordinary.IsEnabled("EnableM10018TimingProbe") &&
                ordinary.IsEnabled(Full) && !ordinary.IsEnabled(Benchmark));
        }
        Reject(() => EfiProfile.Create("", new Dictionary<string, bool>
        {
            [Benchmark] = false, ["EnableM10018EvidenceTraceProbe"] = true, ["EnableM10018EvidenceSummaryProbe"] = true
        }), "disk_evidence_mode_conflict");
    }

    [HostTest("serving benchmark metadata adds only its symbol to the existing full source inventory", HostLane.Fast, 6)]
    private static void AdmissionMetadata()
    {
        const string symbol = "M10_018_SERVING_BENCHMARK";
        HostCheck.Equal(symbol, EfiContracts.ProbeSymbols[Benchmark]);
        var full = EfiProfile.Create("", new Dictionary<string, bool> { [Full] = true });
        var benchmark = EfiProfile.Create("", new Dictionary<string, bool> { [Benchmark] = true });
        var ordinary = EfiProfile.Create("", new Dictionary<string, bool>());
        HostCheck.True(EfiProjectAdmission.ExpectedCompileItems(full).SequenceEqual(EfiProjectAdmission.ExpectedCompileItems(benchmark)));
        foreach (string workload in new[] { "SingleMinimalApi", "MultiMinimalApi" })
        {
            var expected = EfiProjectAdmission.ExpectedSymbols("Release", workload, full);
            HostCheck.True(!expected.Contains(symbol));
            expected.Add(symbol);
            HostCheck.True(expected.SetEquals(EfiProjectAdmission.ExpectedSymbols("Release", workload, benchmark)));
            HostCheck.True(!EfiProjectAdmission.ExpectedSymbols("Release", workload, ordinary).Contains(symbol));
        }
    }

    [HostTest("serving benchmark rejects invalid duplicate CLI flags and verified exit policy", HostLane.Fast, 7)]
    private static void InvalidArgumentsAndBootPolicy()
    {
        foreach (bool build in new[] { true, false })
        {
            Reject(() => EfiOptions.Parse(["-" + Benchmark + ":yes"], build), "argument_boolean_invalid");
            Reject(() => EfiOptions.Parse(["-" + Benchmark, "--" + Benchmark + "=false"], build), "argument_duplicate");
            Reject(() => EfiOptions.Parse(["-BootPolicy", "VerifiedExit", "-" + Benchmark], build).Profile(),
                "clros_boot_policy_probe_conflict");
        }
        Reject(() => EfiProfile.Create("VerifiedExit", new Dictionary<string, bool> { [Benchmark] = true }),
            "clros_boot_policy_probe_conflict");
    }

    private static ToolFailure Reject(Action action, string reason)
    {
        try
        {
            action();
        }
        catch (ToolFailure failure)
        {
            HostCheck.Equal(reason, failure.Reason);
            return failure;
        }
        throw new InvalidOperationException("expected rejection: " + reason);
    }
}
