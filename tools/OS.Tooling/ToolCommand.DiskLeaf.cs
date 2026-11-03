namespace OS.Tooling;

public static partial class ToolCommand
{
    private static int InspectDiskLeaf(string diskPackage, string diskSerial)
    {
        diskPackage = Path.GetFullPath(diskPackage);
        diskSerial = Path.GetFullPath(diskSerial);
        using AuthorityFiles files = new();
        var package = files.Open(Path.GetDirectoryName(diskPackage)!, diskPackage, "disk leaf package", "disk_leaf_package", 65536, singleLink: true);
        var serial = files.Open(Path.GetDirectoryName(diskSerial)!, diskSerial, "disk leaf serial", "disk_leaf_serial", 8 * 1024 * 1024, singleLink: true);
        AuthorityFiles.RequireDistinct(package, serial);
        string[] rows = QemuTranscripts.FirmwareProtocol(diskSerial, "TEST_PASS m16-012\r\n", true, false, "disk_leaf_capture", linuxThreads: true);
        var report = QemuTranscripts.InspectLinuxDiskCaptureLeafForTests(rows, package.ReadBytes(65536, "disk_leaf_package"));
        files.VerifyUnchanged();
        Console.WriteLine($"TEST_PASS qemu.disk.leaf report_only=true calls={report.Calls} transfers={report.Transfers} epochs={report.Epochs} completions={report.Completions.Count} auxiliary={report.Auxiliary.Count} auxiliary_joins=false original_process_and_native_required=true");
        return 0;
    }
}
