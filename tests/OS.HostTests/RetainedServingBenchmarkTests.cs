using System.Text.Json;
using System.Text.Json.Nodes;
using OS.Tooling;

internal static class RetainedServingBenchmarkTests
{
    [HostTest("retained Serving maps only historical artifact reads and preserves original source path identities")]
    private static void ArtifactReadMapping()
    {
        string physical = Path.Combine(Path.GetTempPath(), "retained-artifacts-control");
        string leaf = Path.Combine(RetainedServingBenchmark.HistoricalArtifacts, "refactor-r01", "native", "OS.obj");
        HostCheck.Equal(Path.Combine(physical, "refactor-r01", "native", "OS.obj"), RetainedServingBenchmark.HistoricalReadPath(leaf, physical));
        string source = Path.GetFullPath(Path.Combine(RetainedServingBenchmark.HistoricalArtifacts, "..", "src", "OS", "obj", "OS.obj"));
        HostCheck.Equal(source, RetainedServingBenchmark.HistoricalReadPath(source, physical));
        string foreign = Path.Combine(Path.GetTempPath(), "other-original", "OS.obj");
        HostCheck.Equal(foreign, RetainedServingBenchmark.HistoricalReadPath(foreign, physical));
    }

    [HostTest("retained Serving derives one typed WHPX256MiB runner without builder or process entry")]
    private static async Task Planning()
    {
        var recipe = Recipe();
        var arguments = await RetainedServingBenchmark.CreateRunnerArgumentsAsync(Repository.FindRoot(), recipe);
        HostCheck.True(arguments.Contains("--full-disk-static-files") && arguments.Contains("--readonly-webroot-disk") &&
            arguments.Contains("--require-ordered-marker") && arguments.Contains("--forbid-marker") &&
            !arguments.Contains("--dry-run") && !arguments.Contains("--disk-read-timing") && !arguments.Contains("--socket-forward-port"));
        HostCheck.Equal("256M", arguments[Array.IndexOf(arguments, "--memory") + 1]);
        HostCheck.Equal("30", arguments[Array.IndexOf(arguments, "--startup-timeout") + 1]);
    }

    [HostTest("retained Serving rejects wrong backend memory timing trace summary and nonplanning modes")]
    private static async Task RecipeRefusals()
    {
        foreach (var recipe in new[] { Recipe("-Accelerator", "tcg"), Recipe("-MemoryMiB", "128"),
            Recipe("-StartupTimeoutSeconds", "31"), Recipe("-HttpBenchmarkWarmSequences", "0"),
            Recipe("-EnableM10018TimingProbe"), Recipe("-EnableM10018EvidenceTraceProbe"), Recipe("-EnableM10018EvidenceSummaryProbe"),
            QemuSmokeOptions.Parse("-BuildEfi", "-EnableM10018ServingBenchmarkProbe") })
        {
            bool refused = false;
            try
            {
                _ = await RetainedServingBenchmark.CreateRunnerArgumentsAsync(Repository.FindRoot(), recipe);
            }
            catch (ToolFailure)
            {
                refused = true;
            }
            HostCheck.True(refused);
        }
    }

    [HostTest("retained Serving binds source EFI package image complete asset identity and exact quiet profile")]
    private static void FieldRefusals()
    {
        var manifest = new JsonObject
        {
            ["Source"] = RetainedServingBenchmark.GuestSource, ["WarmSequences"] = 10,
            ["Image"] = new JsonObject { ["sha256"] = RetainedServingBenchmark.EfiSha },
            ["Package"] = new JsonObject { ["sha256"] = RetainedServingBenchmark.PackageSha },
            ["PublishedInventorySha256"] = RetainedServingBenchmark.AssetsSha
        };
        var http = new JsonObject
        {
            ["Complete"] = true, ["WarmSequences"] = 10, ["RequestTimeoutTicks"] = TimeSpan.FromSeconds(180).Ticks,
            ["ImageSha256"] = RetainedServingBenchmark.DiskSha, ["AssetSha256"] = RetainedServingBenchmark.AssetSha, ["AssetBytes"] = 2009369
        };
        var properties = new JsonObject
        {
            ["EnableM10018FullProbe"] = "true", ["EnableM10018ServingBenchmarkProbe"] = "true", ["EnableM10018TimingProbe"] = "false",
            ["EnableM10018EvidenceTraceProbe"] = "false", ["EnableM10018EvidenceSummaryProbe"] = "false"
        };
        var link = new JsonObject { ["postRestoreEvaluation"] = new JsonObject { ["properties"] = properties } };
        Check(manifest, link, http);
        foreach (string key in new[] { "Source", "PublishedInventorySha256" })
        {
            var changed = manifest.DeepClone().AsObject();
            changed[key] = "wrong";
            Reject(() => Check(changed, link, http));
        }
        foreach (string key in new[] { "Image", "Package" })
        {
            var changed = manifest.DeepClone().AsObject();
            changed[key]!["sha256"] = "wrong";
            Reject(() => Check(changed, link, http));
        }
        foreach (string key in new[] { "ImageSha256", "AssetSha256" })
        {
            var changed = http.DeepClone().AsObject();
            changed[key] = "wrong";
            Reject(() => Check(manifest, link, changed));
        }
        var incomplete = http.DeepClone().AsObject();
        incomplete["Complete"] = false;
        Reject(() => Check(manifest, link, incomplete));
        foreach (var item in properties)
        {
            var changed = link.DeepClone().AsObject();
            changed["postRestoreEvaluation"]!["properties"]![item.Key] = item.Value!.GetValue<string>() == "true" ? "false" : "true";
            Reject(() => Check(manifest, changed, http));
        }
        // These fields are synthetic parser controls; public admission also
        // requires the immutable Step1790/manifest/native-leaf hash chain.
    }

    private static void Check(JsonObject manifest, JsonObject link, JsonObject http) => RetainedServingBenchmark.ValidateBaselineFields(
        JsonSerializer.SerializeToElement(manifest), JsonSerializer.SerializeToElement(link), JsonSerializer.SerializeToElement(http));
    private static void Reject(Action action)
    {
        bool refused = false;
        try
        {
            action();
        }
        catch (ToolFailure)
        {
            refused = true;
        }
        HostCheck.True(refused);
    }
    private static QemuSmokeOptions Recipe(params string[] extra)
    {
        List<string> arguments = ["-DryRun", "-BuildEfi", "-EnableM10018ServingBenchmarkProbe", "-HttpBenchmarkWarmSequences", "10",
            "-Accelerator", "whpx", "-MemoryMiB", "256", "-LinuxElfPackage", "retained.pkg", "-LinuxThreadingDisassembly", "retained.txt",
            "-WebRootFixture", "DiskA", "-WebRootAssets", "assets", "-Image", "retained.raw", "-EfiArtifact", "retained.EFI",
            "-SerialLog", "artifacts/retained-serving-test/serial.log", "-SummaryLog", "artifacts/retained-serving-test/summary.log"];
        for (int i = 0; i < extra.Length; i++)
        {
            int at = arguments.IndexOf(extra[i]);
            if (at >= 0)
                arguments[at + 1] = extra[++i];
            else
                arguments.Add(extra[i]);
        }
        return QemuSmokeOptions.Parse(arguments.ToArray());
    }
}
