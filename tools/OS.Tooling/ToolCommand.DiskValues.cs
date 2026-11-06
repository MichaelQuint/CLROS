namespace OS.Tooling;

public static partial class ToolCommand
{
    private static int InspectDiskValues(string capturePackage, string captureImage, string captureSerial)
    {
        capturePackage = Path.GetFullPath(capturePackage);
        captureImage = Path.GetFullPath(captureImage);
        captureSerial = Path.GetFullPath(captureSerial);
        using AuthorityFiles files = new();
        AuthorityFile Hold(string path, int maximum) => files.Open(Path.GetDirectoryName(path)!, path, "disk value inspection", "disk_values", maximum, singleLink: true);
        var package = Hold(capturePackage, 65536);
        var image = Hold(captureImage, WebRootImageFixture.MaximumImageBytes);
        var serial = Hold(captureSerial, 8 * 1024 * 1024);
        var receiptFile = Hold(captureImage + ".webroot.json", 16 * 1024 * 1024);
        AuthorityFiles.RequireDistinct(package, image, serial, receiptFile);
        var receipt = System.Text.Json.JsonSerializer.Deserialize<WebRootImageFixture.Receipt>(receiptFile.ReadUtf8(16 * 1024 * 1024, "disk_values_receipt"))
            ?? throw new ToolFailure("disk_values_receipt_missing");
        string[] rows = QemuTranscripts.FirmwareProtocol(captureSerial, "TEST_PASS m16-012\r\n", true, false, "disk_values_capture", linuxThreads: true);
        var report = QemuTranscripts.InspectLinuxDiskCaptureValues(rows, package.ReadBytes(65536, "disk_values_package"),
            image.ReadBytes(WebRootImageFixture.MaximumImageBytes, "disk_values_image"), receipt, captureImage);
        files.VerifyUnchanged();
        Console.WriteLine($"TEST_PASS qemu.disk.values report_only=true calls={report.Leaf.Calls} sectors={report.Device.Counters.Copied - report.Device.Counters.BootstrapCopied} readWindows={report.ReadWindows.Count} instructionIrqs={report.InstructionIrqs} original_process_native_and_packets_required=true");
        return 0;
    }
}
