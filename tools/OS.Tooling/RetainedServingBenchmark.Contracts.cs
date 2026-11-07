using System.Text.Json;

namespace OS.Tooling;

public static partial class RetainedServingBenchmark
{
    internal static readonly string HistoricalArtifacts = Path.GetFullPath("C:/Users/AI/.codex/worktrees/refactor-combined-r01/CLROS/artifacts");
    internal const string DiskSha = "9df7f9f8bee2aa7995e1bca6735f04125a74bd85fd8ad32ce1ae46a22cf12fc7";

    // Only the declared archived artifacts prefix is mapped. Original source/
    // intermediate paths remain metadata; they are never restored or rewritten.
    internal static string HistoricalReadPath(string original, string physicalArtifacts)
    {
        string path = Path.GetFullPath(original);
        return path.StartsWith(HistoricalArtifacts + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(Path.GetRelativePath(HistoricalArtifacts, path), physicalArtifacts) : path;
    }

    internal static void ValidateBaselineFields(JsonElement manifest, JsonElement link, JsonElement http)
    {
        Require(manifest.GetProperty("Source").GetString() == GuestSource && manifest.GetProperty("WarmSequences").GetInt32() == 10, "guest_source");
        Require(Equal(manifest.GetProperty("Image").GetProperty("sha256").GetString()!, EfiSha), "efi");
        Require(Equal(manifest.GetProperty("Package").GetProperty("sha256").GetString()!, PackageSha), "package");
        Require(Equal(manifest.GetProperty("PublishedInventorySha256").GetString()!, AssetsSha), "assets");
        Require(http.GetProperty("Complete").GetBoolean() && http.GetProperty("WarmSequences").GetInt32() == 10 &&
            http.GetProperty("RequestTimeoutTicks").GetInt64() == TimeSpan.FromSeconds(180).Ticks &&
            Equal(http.GetProperty("ImageSha256").GetString()!, DiskSha) && Equal(http.GetProperty("AssetSha256").GetString()!, AssetSha) &&
            http.GetProperty("AssetBytes").GetInt64() == 2009369, "http_binding");
        var properties = link.GetProperty("postRestoreEvaluation").GetProperty("properties");
        foreach (string flag in new[] { "EnableM10018FullProbe", "EnableM10018ServingBenchmarkProbe", "EnableM10018TimingProbe", "EnableM10018EvidenceTraceProbe", "EnableM10018EvidenceSummaryProbe" })
            Require(properties.GetProperty(flag).GetString() == (flag is "EnableM10018FullProbe" or "EnableM10018ServingBenchmarkProbe" ? "true" : "false"), "profile");
    }

    private static object HoldCurrentHost(string root, AuthorityFiles held)
    {
        string tooling = HostTools.ManagedTool(root, "OS.Tooling"), runner = HostTools.ManagedTool(root, "OS.QemuRunner");
        string executing = typeof(RetainedServingBenchmark).Assembly.Location;
        string[] producers = new[] { tooling, executing }.Concat(Directory.GetFiles(Path.GetDirectoryName(runner)!)
            .Where(path => Path.GetExtension(path) is ".dll" or ".json")).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var binaries = producers.Select(path => held.Open(root, path, "current host producer", "retained_serving_host", 128 * 1024 * 1024, singleLink: true)).ToArray();
        Require(Equal(binaries.Single(file => file.Path == tooling).Sha256, binaries.Single(file => file.Path == executing).Sha256), "host_tooling_copy");
        var sources = ToolingBuildInputs.SourceInputs(root).Select(path => held.Open(root, path, "current host source", "retained_serving_host_source", 128 * 1024 * 1024, allowEmpty: true, singleLink: true)).ToArray();
        return new
        {
            Kind = "current-host-file-bindings", Root = root,
            Producers = binaries.Select(file => new { file.Path, file.Length, file.Sha256 }).ToArray(),
            Sources = sources.Select(file => new { file.Path, file.Length, file.Sha256 }).ToArray()
        };
    }

    internal static async Task<string[]> CreateRunnerArgumentsAsync(string root, QemuSmokeOptions recipe)
    {
        var flags = QemuProfiles.Create(recipe).Flags;
        Require(recipe.DryRun && recipe.BuildEfi && recipe.StartupTimeoutSeconds == 30 && recipe.Accelerator == "whpx" &&
            recipe.MemoryMiB == 256 && recipe.HttpBenchmarkWarmSequences == 10 && recipe.WebRootFixture == "DiskA" && recipe.WebRootFault == "None" &&
            NativeAotDiskSelection.FullStaticSelected(flags) && flags["EnableM10018ServingBenchmarkProbe"] &&
            !flags["EnableM10018TimingProbe"] && !flags["EnableM10018EvidenceTraceProbe"] && !flags["EnableM10018EvidenceSummaryProbe"], "recipe");
        string[]? captured = null;
        int result = await QemuSmoke.RunAsync(root, recipe, TextWriter.Null, (tool, arguments) =>
        {
            Require(tool == "OS.QemuRunner" && captured is null, "single_planned_runner");
            captured = arguments.ToArray();
            return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
        });
        Require(result == 0 && captured is not null && captured.Count(value => value == "--dry-run") == 1, "plan");
        List<string> actual = [];
        for (int index = 0; index < captured!.Length; index++)
        {
            if (captured[index] == "--socket-forward-port")
            {
                index++;
                continue;
            }
            if (captured[index] != "--dry-run")
                actual.Add(captured[index]);
        }
        return actual.ToArray();
    }
}
