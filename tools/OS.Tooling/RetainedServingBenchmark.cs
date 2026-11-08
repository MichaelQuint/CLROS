using System.Globalization;
using System.Text.Json;

namespace OS.Tooling;

/// <summary>Fresh quiet HTTP observation of the fixed, previously qualified2e
/// Serving artifact. Historical source/native identities remain historical;
/// current host sources/producers and every consumed private copy are separately
/// held through join. This adapter never builds a kernel, restores old live
/// paths or grants current-source EFI, Full, browser or natural-cleanup acceptance.</summary>
public static partial class RetainedServingBenchmark
{
    internal const string GuestSource = "2e81ee3abd1efbcfe6fa92233cd1449a4729ce70";
    private const string CloseoutSha = "e44de92cc20bf176c8a0e42afc905c784876bb6688ce9cf41fcd15bc2ef87c86";
    private const string PopulationSha = "b1db6290333860ac02b5e0059eafe9cfaf28321a7aa3c06907434ecb7ae73903";
    private const string ManifestSha = "fc6c109b2ee1017592db36380d4f26e349b60d750bf58f0c94502009f317333c";
    internal const string EfiSha = "46ba847e1e584ec0bf2309f97bb62617e1d9c97b6fb8d0aa2ef0a5808a81b560";
    internal const string PackageSha = "63187af6afe822012b32b758748f08be895511bbf14658b1b064f1950f65cb6e";
    internal const string AssetsSha = "11f4e6ebd042fe6ed2be59dd8c4e34a3ade253a7860fa5c89d98496e935cfea9";
    internal const string AssetSha = "e6cdc4eda0f24ca3b131a5665c53762e64352f75c37ed0bf7626cec302dfa5c5";

    /// <summary>Prepare or run once in a fresh artifacts directory. historicalRoot
    /// is the physical retained refactor-r01 directory (no junction traversal),
    /// closeout is the Step1790 pinned readback, and raw
    /// is the original boot01 DiskA. Preparation copies/verifies the historical
    /// chain,12 native leaves, raw image, package and complete published assets;
    /// it launches no child. Execution additionally derives a typed runner recipe,
    /// owns WHPX launch/environment and joins its process and88 HTTP observations.
    /// Caller retains the phase's current Full/native authorization. Failure
    /// throws and retains private copies; existing outputs are never replaced.</summary>
    public static async Task RunAsync(string root, string historicalRoot, string closeout, string raw,
        string freshDirectory, bool prepareOnly, TextWriter output, CancellationToken cancellation = default)
    {
        root = Path.GetFullPath(root);
        historicalRoot = Path.GetFullPath(historicalRoot);
        Require(Path.GetFileName(historicalRoot) == "refactor-r01", "historical_root");
        string physicalArtifacts = Path.GetDirectoryName(historicalRoot)!;
        string directory = Path.GetFullPath(freshDirectory, root);
        Require(directory.StartsWith(Path.Combine(root, "artifacts") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !Path.Exists(directory), "fresh_outputs");
        AuthorityFiles.RequirePlainPath(root, Path.GetDirectoryName(directory)!, "retained_serving_output_parent");
        Require(ToolingBuildInputs.Validate(root).Count == 0, "current_host_sources");
        using AuthorityFiles held = new();
        List<FileBinding> bindings = [];
        Directory.CreateDirectory(directory);
        AuthorityFile Hold(string path, string expected, long maximum = 128 * 1024 * 1024)
        {
            path = HistoricalReadPath(path, physicalArtifacts);
            var file = held.Open(Path.GetPathRoot(path)!, path, "retained Serving input", "retained_serving_input", maximum, allowEmpty: true, singleLink: true);
            Require(Equal(file.Sha256, expected), "hash " + path);
            return file;
        }
        AuthorityFile Copy(string historicalPath, string readFrom, string expected, string relative, long maximum = 128 * 1024 * 1024)
        {
            cancellation.ThrowIfCancellationRequested();
            var original = Hold(readFrom, expected, maximum);
            string destination = Path.GetFullPath(relative, directory);
            Require(destination.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "copy_path");
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using (FileStream target = new(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                target.Write(original.ReadBytes(maximum, "retained_serving_copy"));
                target.Flush(true);
            }
            var copied = Hold(destination, expected, maximum);
            AuthorityFiles.RequireDistinct(original, copied);
            Require(copied.Length == original.Length, "copy_length");
            bindings.Add(new(historicalPath, original.Path, copied.Path, copied.Length, copied.Sha256));
            return copied;
        }
        JsonDocument Json(AuthorityFile file) => JsonDocument.Parse(file.ReadBytes(4 * 1024 * 1024, "retained_serving_json"), new JsonDocumentOptions { MaxDepth = 32 });
        string Historical(string relative) => Path.Combine(historicalRoot, relative);
        using var close = Json(Copy(closeout, closeout, CloseoutSha, "authority/closeout.json"));
        Require(close.RootElement.GetProperty("RuntimeSource").GetString() == GuestSource && close.RootElement.GetProperty("AllPinsMatched").GetBoolean(), "closeout");
        var populationPin = close.RootElement.GetProperty("ReceiptPins").EnumerateArray().Single(item => Equal(item.GetProperty("Sha256").GetString()!, PopulationSha));
        string populationPath = populationPin.GetProperty("Path").GetString()!;
        using var population = Json(Copy(populationPath, populationPath, PopulationSha, "authority/population.json"));
        Require(population.RootElement.GetProperty("Source").GetString() == GuestSource && Equal(population.RootElement.GetProperty("AssetSha256").GetString()!, AssetSha), "population");
        var boot = population.RootElement.GetProperty("Bindings").EnumerateArray().Single(item => item.GetProperty("System").GetString() == "CLROS" && item.GetProperty("Backend").GetString() == "whpx" && item.GetProperty("Boot").GetInt32() == 1);
        string bootPath = boot.GetProperty("Path").GetString()!;
        using var oldHttp = Json(Copy(bootPath, bootPath, boot.GetProperty("Sha256").GetString()!, "authority/old-http.json"));
        string manifestPath = Historical("matched-clros-owner-compat-r01/launch-manifest-current-r01.json");
        using var manifest = Json(Copy(manifestPath, manifestPath, ManifestSha, "authority/launch.json"));
        var m = manifest.RootElement;
        AuthorityFile Reference(string property, string relative)
        {
            var reference = m.GetProperty(property);
            string original = reference.GetProperty("path").GetString()!;
            var file = Copy(original, original, reference.GetProperty("sha256").GetString()!, relative);
            Require(file.Length == reference.GetProperty("bytes").GetInt64(), "reference_length");
            return file;
        }
        var sourceReceipt = Reference("SourceCheckpoint", "authority/historical-source.json");
        var snapshotFile = Reference("NativeSnapshot", "native/native-snapshot.json");
        var linkFile = Reference("NativeInputEvidence", "authority/historical-link.json");
        using var snapshot = Json(snapshotFile);
        using var link = Json(linkFile);
        ValidateBaselineFields(m, link.RootElement, oldHttp.RootElement);
        var leaves = snapshot.RootElement.GetProperty("Files").EnumerateArray().ToArray();
        Require(leaves.Length == 12, "native_leaf_count");
        string retainedNative = Path.GetDirectoryName(m.GetProperty("NativeSnapshot").GetProperty("path").GetString()!)!;
        foreach (var leaf in leaves)
        {
            string relative = leaf.GetProperty("RelativePath").GetString()!;
            string from = Path.GetFullPath(relative, retainedNative);
            Require(from.StartsWith(retainedNative + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), "native_path");
            var file = Copy(leaf.GetProperty("Source").GetString()!, from, leaf.GetProperty("Sha256").GetString()!, "native/" + relative);
            Require(file.Length == leaf.GetProperty("Length").GetInt64(), "native_length");
        }
        var efi = Reference("Image", "inputs/BOOTX64.EFI");
        var package = Reference("Package", "inputs/package.pkg");
        var elf = Reference("ElfDisassembly", "inputs/guest.txt");
        var firmware = Reference("Ovmf", "inputs/ovmf.fd");
        var qemuReference = m.GetProperty("Qemu");
        var qemu = Hold(qemuReference.GetProperty("path").GetString()!, qemuReference.GetProperty("sha256").GetString()!);
        var image = Copy(raw, raw, oldHttp.RootElement.GetProperty("ImageSha256").GetString()!, "inputs/disk.raw");
        string receiptPath = raw + ".webroot.json";
        string receiptReadPath = HistoricalReadPath(receiptPath, physicalArtifacts);
        var originalReceipt = held.Open(Path.GetPathRoot(receiptReadPath)!, receiptReadPath, "retained image receipt", "retained_serving_receipt", 4 * 1024 * 1024, singleLink: true);
        var receiptFile = Copy(receiptPath, receiptPath, originalReceipt.Sha256, "inputs/disk.webroot.json");
        var receipt = JsonSerializer.Deserialize<WebRootImageFixture.Receipt>(receiptFile.ReadBytes(4 * 1024 * 1024, "retained_serving_receipt")) ?? throw new ToolFailure("retained_serving_receipt");
        byte[] imageBytes = image.ReadBytes(WebRootImageFixture.MaximumImageBytes, "retained_serving_image");
        _ = LinuxElfImageFixture.Inspect(imageBytes, efi.ReadBytes(16 * 1024 * 1024, "retained_serving_efi"), package.ReadBytes(16 * 1024 * 1024, "retained_serving_package"));
        NativeAotStaticFileProgram.RequirePackage(package.ReadBytes(16 * 1024 * 1024, "retained_serving_package"), false);
        var inventory = WebRootDiskInventory.Inspect(image.Path, imageBytes, receipt);
        var asset = inventory.Find(WebRootDiskInventory.Root + NativeAotDiskTimingPeer.Target);
        Require(Equal(receipt.PublishedInventorySha256, AssetsSha) && asset is { Bytes: 2009369 } && Equal(asset.Sha256, AssetSha), "image_assets");
        string assets = m.GetProperty("PublishedAssets").GetString()!;
        var published = WebRootImageFixture.SnapshotPublishedRoot(HistoricalReadPath(assets, physicalArtifacts));
        Require(Equal(published.Sha256, AssetsSha), "published_assets");
        foreach (var file in published.Files)
            Copy(Path.Combine(assets, file.Path), Path.Combine(assets, file.Path), file.Sha256, "inputs/assets/" + file.Path);
        Require(Equal(WebRootImageFixture.SnapshotPublishedRoot(Path.Combine(directory, "inputs/assets")).Sha256, AssetsSha), "private_assets");
        string serial = Path.Combine(directory, "serial.log");
        var recipe = QemuSmokeOptions.Parse("-DryRun", "-BuildEfi", "-EnableM10018ServingBenchmarkProbe", "-HttpBenchmarkWarmSequences", "10",
            "-Accelerator", "whpx", "-MemoryMiB", "256", "-Image", image.Path, "-EfiArtifact", efi.Path,
            "-SerialLog", serial, "-SummaryLog", Path.Combine(directory, "summary.log"), "-LinuxElfPackage", package.Path,
            "-LinuxThreadingDisassembly", elf.Path, "-WebRootFixture", "DiskA", "-WebRootAssets", Path.Combine(directory, "inputs/assets"),
            "-Qemu", qemu.Path, "-OvmfCode", firmware.Path);
        var flags = QemuProfiles.Create(recipe).Flags;
        var properties = link.RootElement.GetProperty("postRestoreEvaluation").GetProperty("properties");
        Require(flags.All(flag => properties.TryGetProperty(flag.Key, out var value) && value.GetString() == (flag.Value ? "true" : "false")), "historical_profile");
        string[] arguments = await CreateRunnerArgumentsAsync(root, recipe);
        var hostBindings = HoldCurrentHost(root, held);
        Require(ToolingBuildInputs.Validate(root).Count == 0, "current_host_sources");
        held.VerifyUnchanged();
        Write("binding.json", new
        {
            Schema = "clros-retained-serving-binding-v1", GuestSource, CurrentHost = hostBindings,
            HistoricalArtifactsRoot = HistoricalArtifacts, PhysicalArtifactsRoot = physicalArtifacts,
            HistoricalManifestSha256 = ManifestSha, HistoricalSourceReceipt = sourceReceipt.Sha256,
            Files = bindings, RunnerArguments = arguments, image.Sha256,
            PreparedOnly = prepareOnly, FreshNativePublication = false, EfiVerificationPassed = false,
            FullAndCleanupQualified = false, BrowserQualified = false
        });
        if (prepareOnly)
        {
            output.WriteLine("TEST_PASS retained.serving.prepared guest_executed=false");
            return;
        }
        try
        {
            var result = await NativeAotHttpBenchmarkPeer.RunQuietAsync(root, serial, RunRunner, inventory,
                new() { System = "CLROS", Backend = "whpx", WarmSequences = 10 }, recipe.TimeoutSeconds, cancellation);
            held.VerifyUnchanged();
            Require(ToolingBuildInputs.Validate(root).Count == 0, "current_host_changed");
            Write("completion.json", new { GuestSource, Complete = true, result.Observations, RunnerJoined = result.Runner.TerminationConfirmed, FullAndCleanupQualified = false, BrowserQualified = false });
        }
        catch (Exception error)
        {
            Write("failure.json", new { GuestSource, Failure = error.ToString(), FullAndCleanupQualified = false });
            throw;
        }
        async Task<ProcessResult> RunRunner(int port, CancellationToken token)
        {
            held.VerifyUnchanged();
            Require(ToolingBuildInputs.Validate(root).Count == 0, "current_host_sources");
            using var startup = WhpxLaunchSession.Create(root, serial, qemu.Path, "whpx") ?? throw new ToolFailure("retained_serving_whpx_owner");
            var request = startup.Prepare(new ProcessRequest("dotnet", [HostTools.ManagedTool(root, "OS.QemuRunner"), .. arguments,
                "--socket-forward-port", port.ToString(CultureInfo.InvariantCulture)], root)
            {
                Timeout = TimeSpan.FromSeconds(recipe.TimeoutSeconds + 60), OutputCharacterLimit = 16 * 1024 * 1024,
                RetainOwnershipUntilJoined = OperatingSystem.IsWindows()
            });
            var joined = startup.VerifyJoined(await ProcessRunner.RunAsync(request, token), request.OutputCharacterLimit);
            held.VerifyUnchanged();
            output.Write(joined.StandardOutput);
            output.Write(joined.StandardError);
            return joined;
        }
        void Write(string name, object value)
        {
            using FileStream target = new(Path.Combine(directory, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(target, value, new JsonSerializerOptions { WriteIndented = true });
            target.Flush(true);
        }
    }

    private static bool Equal(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new ToolFailure("retained_serving_" + reason);
    }
}
