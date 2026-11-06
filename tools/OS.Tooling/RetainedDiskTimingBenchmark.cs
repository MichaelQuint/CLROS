using System.Globalization;

namespace OS.Tooling;

/// <summary>
/// Runs the existing bounded timing peer against a retained, reviewed image.
/// The caller must hold the image's original EFI, package, source and native-review
/// authorities until this call returns. This host-only diagnostic neither rebuilds
/// the kernel nor grants source-matched qualification or normal shutdown acceptance.
/// Failures retain fresh observations; cancellation still joins the original runner.
/// </summary>
public static class RetainedDiskTimingBenchmark
{
    public static async Task<NativeAotDiskTimingPeer.Completion> RunAsync(string root,
        QemuSmokeOptions recipe, WebRootDiskInventory.Inspection inventory, TextWriter output,
        CancellationToken cancellation = default)
    {
        root = Path.GetFullPath(root);
        string serial = Path.GetFullPath(recipe.SerialLog, root);
        foreach (string path in new[] { serial, Path.GetFullPath(recipe.SummaryLog, root), serial + ".pcap",
            serial + ".disk-timing", serial + ".whpx-prelaunch" })
            if (Path.Exists(path))
                throw new ToolFailure("retained_timing_fresh_outputs_required", path);

        using var inputs = new AuthorityFiles();
        string imagePath = Path.GetFullPath(recipe.Image, root);
        var image = inputs.Open(root, imagePath, "retained timing image", "retained_timing_image",
            WebRootImageFixture.MaximumImageBytes, singleLink: true);
        if (!string.Equals(image.Sha256, inventory.ImageSha256, StringComparison.OrdinalIgnoreCase))
            throw new ToolFailure("retained_timing_inventory_mismatch");

        string[] arguments = await CreateRunnerArgumentsAsync(root, recipe);
        output.WriteLine($"RETAINED_TIMING_START imageSha256={image.Sha256} accelerator={recipe.Accelerator} freshKernelBuild=false fullAndCleanupQualified=false");
        var options = NativeAotDiskTimingPeer.BenchmarkOptions.ForProfile(QemuProfiles.Create(recipe).Flags,
            () => image.ReadBytes(WebRootImageFixture.MaximumImageBytes, "retained_timing_image"));
        var result = await NativeAotDiskTimingPeer.RunAsync(root, serial, RunRunner, inventory, options,
            recipe.TimeoutSeconds, cancellation);
        inputs.VerifyUnchanged();
        output.WriteLine("TEST_PASS benchmark.retained-disk-timing full_and_cleanup_qualified=false");
        return result;

        async Task<ProcessResult> RunRunner(int port, CancellationToken token)
        {
            using var startup = WhpxLaunchSession.Create(root, serial, recipe.Qemu, recipe.Accelerator);
            if (recipe.Accelerator == "whpx" && startup is null)
                throw new ToolFailure("retained_timing_whpx_owner_required");

            var request = new ProcessRequest("dotnet", [HostTools.ManagedTool(root, "OS.QemuRunner"),
                .. arguments, "--socket-forward-port", port.ToString(CultureInfo.InvariantCulture)], root)
            {
                Timeout = TimeSpan.FromSeconds(recipe.TimeoutSeconds + 60),
                OutputCharacterLimit = 16 * 1024 * 1024,
                RetainOwnershipUntilJoined = OperatingSystem.IsWindows()
            };
            if (startup is not null)
                request = startup.Prepare(request);

            var joined = await ProcessRunner.RunAsync(request, token);
            if (startup is not null)
                joined = startup.VerifyJoined(joined, request.OutputCharacterLimit);

            output.Write(joined.StandardOutput);
            output.Write(joined.StandardError);
            return joined;
        }
    }

    internal static async Task<string[]> CreateRunnerArgumentsAsync(string root, QemuSmokeOptions recipe)
    {
        if (!recipe.DryRun || !recipe.BuildEfi || recipe.StartupTimeoutSeconds != 30
            || !NativeAotDiskSelection.TimingSelected(QemuProfiles.Create(recipe).Flags))
            throw new ToolFailure("retained_timing_recipe_required");

        _ = NativeAotDiskTimingPeer.PeerTimeoutSeconds(recipe.TimeoutSeconds);
        string[]? captured = null;
        int result = await QemuSmoke.RunAsync(root, recipe, TextWriter.Null, (tool, arguments) =>
        {
            // The dry run is only a plan producer. No child executable runs here.
            if (tool != "OS.QemuRunner")
                throw new ToolFailure("retained_timing_unexpected_child", tool);
            if (captured is not null)
                throw new ToolFailure("retained_timing_duplicate_runner");

            captured = arguments.ToArray();
            return Task.FromResult(new ProcessResult(0, "", "", false, false, false, TimeSpan.Zero));
        });
        if (result != 0 || captured is null || captured.Count(value => value == "--dry-run") != 1)
            throw new ToolFailure("retained_timing_plan_failed");

        var actual = new List<string>();
        for (int index = 0; index < captured.Length; index++)
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
