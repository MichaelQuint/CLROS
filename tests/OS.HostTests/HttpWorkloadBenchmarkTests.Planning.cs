using OS.Tooling;

internal static partial class HttpWorkloadBenchmarkTests
{
    [HostTest("HTTP workload planning preserves ordinary profiles and admits explicit healthy full diagnostics only")]
    private static void OwnerPlanning()
    {
        HostCheck.True(QemuSmokeOptions.Parse().HttpBenchmarkWarmSequences is null);
        foreach (string backend in new[] { "whpx", "tcg" })
        {
            var ordinary = FullOptions(backend);
            var baseline = QemuProfiles.Create(ordinary);
            foreach (int warm in new[] { 0, 10 })
            {
                var options = FullOptions(backend, "-HttpBenchmarkWarmSequences", warm.ToString());
                var plan = QemuProfiles.Create(options);
                HostCheck.True(options.HttpBenchmarkWarmSequences == warm && options.MemoryMiB is null &&
                    options.TimeoutSeconds == ordinary.TimeoutSeconds && options.Accelerator == backend);
                HostCheck.True(plan.Flags.OrderBy(pair => pair.Key).SequenceEqual(baseline.Flags.OrderBy(pair => pair.Key)) &&
                    plan.Required.SequenceEqual(baseline.Required) && plan.Forbidden.SequenceEqual(baseline.Forbidden));
            }
            foreach (string flag in new[] { "EnableM10018TimingProbe", "EnableM10018EvidenceTraceProbe", "EnableM10018EvidenceSummaryProbe" })
                RejectPlan(FullOptions(backend, "-HttpBenchmarkWarmSequences", "0", "-" + flag),
                    "http_benchmark_requires_healthy_full_profile");
            RejectPlan(FullOptions(backend, "-HttpBenchmarkWarmSequences", "0", "-WebRootFault", "Truncated"),
                "http_benchmark_requires_healthy_full_profile");
            RejectPlan(FullOptions(backend, "-HttpBenchmarkWarmSequences", "11"), "http_benchmark_warm_sequences");
        }
        RejectPlan(QemuSmokeOptions.Parse("-HttpBenchmarkWarmSequences", "0"), "http_benchmark_requires_healthy_full_profile");
        bool duplicate = false;
        try
        {
            _ = QemuSmokeOptions.Parse("-HttpBenchmarkWarmSequences", "0", "-HttpBenchmarkWarmSequences", "10");
        }
        catch (ArgumentException error)
        {
            duplicate = error.Message == "qemu_duplicate_option HttpBenchmarkWarmSequences";
        }
        HostCheck.True(duplicate);
    }

    [HostTest("HTTP benchmark owner rejects invalid controls before creating artifacts or starting a runner")]
    private static void OwnerControls() => WithAsset((root, inventory, body) =>
    {
        foreach (var item in new[] { (Options(11), 7200), (Options(0), 600) })
        {
            string serial = Fresh(root) + ".log";
            bool launched = false, rejected = false;
            try
            {
                _ = NativeAotHttpBenchmarkPeer.RunAsync(root, serial,
                    (_, _) =>
                    {
                        launched = true;
                        return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
                    }, inventory, item.Item1, item.Item2, (_, _, _) => Task.CompletedTask).GetAwaiter().GetResult();
            }
            catch (ToolFailure)
            {
                rejected = true;
            }
            HostCheck.True(rejected && !launched && !Path.Exists(serial + ".http-benchmark"));
        }
    });

    private static QemuSmokeOptions FullOptions(string backend, params string[] extra) =>
        QemuSmokeOptions.Parse(["-BuildEfi", "-EnableM10018FullProbe", "-Accelerator", backend,
            "-LinuxElfPackage", "artifacts/disk-static.pkg", "-LinuxThreadingDisassembly", "artifacts/disk-static.dump",
            "-WebRootFixture", "DiskA", "-WebRootAssets", "artifacts/webroot", .. extra]);

    private static void RejectPlan(QemuSmokeOptions options, string reason)
    {
        bool rejected = false;
        try
        {
            _ = QemuProfiles.Create(options);
        }
        catch (ToolFailure error)
        {
            rejected = error.Reason == reason;
        }
        HostCheck.True(rejected, reason);
    }
}
