using System.Text.Json;
using OS.Tooling;

internal static class ToolCommandInspectionTests
{
    [HostTest("inspection CLI retains full-mode rejection before opening any input")]
    private static void InvalidMode()
    {
        foreach (string[] suffix in new[] { new[] { "--FULL" }, new[] { "--full", "extra" } })
        {
            var observed = Capture(["qemu", "inspect-disk-static-values", "missing-package", "missing-image",
                "missing-index", "missing-serial", .. suffix]);
            HostCheck.True(observed.Exit == 1 && observed.Output.Length == 0 &&
                observed.Error.Trim() == "TEST_FAIL tooling disk_static_values_mode");
        }
    }

    [HostTest("inspection CLI reads actual webroot bytes and retains report-only authority")]
    private static void Inventory() => WebRootImageFixtureTests.WithImage((root, directory, image, source) =>
    {
        string imported = Path.Combine(directory, "inspection.raw");
        var receipt = WebRootImageFixture.Apply(root, image, source, WebRootImageFixture.SnapshotPublishedRoot(source), imported);
        File.WriteAllText(imported + ".webroot.json", JsonSerializer.Serialize(receipt));
        string beforeImage = HostRunCheckpoint.HashFile(imported);
        string beforeReceipt = HostRunCheckpoint.HashFile(imported + ".webroot.json");
        var observed = Capture(["qemu", "inspect-webroot-inventory", imported]);
        HostCheck.True(observed.Exit == 0 && observed.Error.Length == 0);
        string[] lines = observed.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        using var json = JsonDocument.Parse(lines.Single(line => line.StartsWith("WEBROOT_INVENTORY_VALUES ", StringComparison.Ordinal))
            ["WEBROOT_INVENTORY_VALUES ".Length..]);
        HostCheck.Equal(beforeImage, json.RootElement.GetProperty("ImageSha256").GetString());
        var entries = json.RootElement.GetProperty("Entries").EnumerateArray().ToDictionary(
            entry => entry.GetProperty("Path").GetString()!, StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(source, file).Replace('\\', '/');
            var entry = entries[WebRootDiskInventory.Root + "/" + relative];
            HostCheck.True(!entry.GetProperty("IsDirectory").GetBoolean() &&
                entry.GetProperty("Bytes").GetInt64() == new FileInfo(file).Length);
            HostCheck.Equal(HostRunCheckpoint.HashFile(file), entry.GetProperty("Sha256").GetString());
        }
        HostCheck.True(lines.Last().StartsWith("TEST_PASS qemu.webroot.inventory report_only=true entries=", StringComparison.Ordinal) &&
            lines.Last().EndsWith("original_native_process_device_and_http_authority=false", StringComparison.Ordinal));
        HostCheck.Equal(beforeImage, HostRunCheckpoint.HashFile(imported));
        HostCheck.Equal(beforeReceipt, HostRunCheckpoint.HashFile(imported + ".webroot.json"));
    });

    [HostTest("inspection CLI rejects a missing receipt and does not modify the image")]
    private static void MissingReceipt() => WebRootImageFixtureTests.WithImage((root, directory, image, source) =>
    {
        string before = HostRunCheckpoint.HashFile(image);
        var observed = Capture(["qemu", "inspect-webroot-inventory", image]);
        HostCheck.True(observed.Exit == 1 && observed.Output.Length == 0 && observed.Error.StartsWith("TEST_FAIL tooling ", StringComparison.Ordinal));
        HostCheck.Equal(before, HostRunCheckpoint.HashFile(image));
    });

    private static (int Exit, string Output, string Error) Capture(string[] arguments)
    {
        TextWriter originalOutput = Console.Out, originalError = Console.Error;
        using StringWriter output = new(), error = new();
        try
        {
            Console.SetOut(output);
            Console.SetError(error);
            int result = ToolCommand.Run(arguments);
            return (result, output.ToString(), error.ToString());
        }
        finally
        {
            Console.SetOut(originalOutput);
            Console.SetError(originalError);
        }
    }
}
