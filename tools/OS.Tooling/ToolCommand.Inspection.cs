namespace OS.Tooling;

public static partial class ToolCommand
{
    private static int InspectDiskStaticValues(string[] args, string staticDiskPackage, string staticDiskImage, string staticDiskIndex, string staticDiskSerial)
    {
        bool full = args.Length == 7 && args[6] == "--full";
        if (args.Length != 6 && !full)
            throw new ToolFailure("disk_static_values_mode");
        int serialMaximum = full ? DiskStaticCapturePolicy.SerialBytes : 8 * 1024 * 1024;
        string root = Repository.FindRoot();
        staticDiskPackage = Path.GetFullPath(staticDiskPackage);
        staticDiskImage = Path.GetFullPath(staticDiskImage);
        staticDiskIndex = Path.GetFullPath(staticDiskIndex);
        staticDiskSerial = Path.GetFullPath(staticDiskSerial);
        using AuthorityFiles files = new();
        AuthorityFile Hold(string path, int maximum) => files.Open(Path.GetDirectoryName(path)!, path,
            "disk static value inspection", "disk_static_values", maximum, singleLink: true);
        var package = Hold(staticDiskPackage, checked((int)OS.Manifest.LinuxElfPackage.MaximumBytes));
        var image = Hold(staticDiskImage, WebRootImageFixture.MaximumImageBytes);
        var receiptFile = Hold(staticDiskImage + ".webroot.json", 16 * 1024 * 1024);
        var index = Hold(staticDiskIndex, NativeAotStaticFileProgram.MaximumDisassemblyBytes);
        var serial = Hold(staticDiskSerial, serialMaximum);
        AuthorityFiles.RequireDistinct(package, image, receiptFile, index, serial);
        byte[] packageBytes = package.ReadBytes(OS.Manifest.LinuxElfPackage.MaximumBytes, "disk_static_values_package");
        var program = NativeAotStaticFileProgram.Create(root, packageBytes, index.ReadUtf8(NativeAotStaticFileProgram.MaximumDisassemblyBytes, "disk_static_values_index"), false);
        var packageView = NativeAotStaticFileProgram.RequirePackage(packageBytes, false);
        var receipt = System.Text.Json.JsonSerializer.Deserialize<WebRootImageFixture.Receipt>(receiptFile.ReadUtf8(16 * 1024 * 1024, "disk_static_values_receipt"))
            ?? throw new ToolFailure("disk_static_values_receipt_missing");
        byte[] imageBytes = image.ReadBytes(WebRootImageFixture.MaximumImageBytes, "disk_static_values_image");
        var inventory = WebRootDiskInventory.Inspect(staticDiskImage, imageBytes, receipt);
        byte[] raw = serial.ReadBytes(serialMaximum, "disk_static_values_serial");
        int parentEnd = raw.AsSpan().IndexOf("TEST_START m16-011\r\n"u8);
        if (parentEnd < 0)
            throw new ToolFailure("disk_static_values_parent_missing");
        var parent = QemuTranscripts.InspectNativeAotWebParentForTests(packageBytes, raw.AsSpan(0, parentEnd), diskStatic: true);
        byte[] begin = "TEST_START m16-009\r\n"u8.ToArray(), end = "TEST_PASS m16-009\r\n"u8.ToArray();
        int at = raw.AsSpan().IndexOf(begin), last = at < 0 ? -1 : raw.AsSpan(at).IndexOf(end);
        if (at < 0 || last < 0)
            throw new ToolFailure("disk_static_values_leaf_missing");
        byte[] leaf = raw.AsSpan(at, last + end.Length).ToArray();
        string text = System.Text.Encoding.UTF8.GetString(leaf);
        ulong Root(string marker)
        {
            var match = System.Text.RegularExpressions.Regex.Match(text, "(?m)^" + marker + " root=([1-9][0-9]*) ");
            return match.Success ? ulong.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : throw new ToolFailure("disk_static_values_root");
        }
        var report = QemuTranscripts.InspectNativeAotDiskStaticProtocolForTests(packageBytes.AsSpan((int)packageView.PayloadOffset, (int)packageView.PayloadBytes).ToArray(),
            leaf, program.IsInstruction, program.IsSyscallReturn, Root("M16_005_ARMED"), Root("M16_008_BOUND"), inventory, diskStaticFull: full);
        string whole = System.Text.Encoding.ASCII.GetString(raw);
        var counts = System.Text.RegularExpressions.Regex.Match(whole, "(?m)^M16_025_END submitted=([0-9]+) completed=([0-9]+) copied=([0-9]+) ");
        if (!counts.Success)
            throw new ToolFailure("disk_static_values_end_missing");
        ulong Counter(int group) => ulong.Parse(counts.Groups[group].Value, System.Globalization.CultureInfo.InvariantCulture);
        var sectors = QemuTranscripts.InspectNativeAotDiskStaticSectorValues(report, inventory, imageBytes,
            new(report.Owner, report.Epoch, report.BootstrapCompleted, report.BootstrapCopied, Counter(1), Counter(2), Counter(3)));
        if (full)
            QemuTranscripts.InspectNativeAotDiskStaticParentValues(raw, report, sectors);
        files.VerifyUnchanged();
        if (full)
            Console.WriteLine("DISK_STATIC_FULL_PROTOCOL_DIAGNOSTIC " + System.Text.Json.JsonSerializer.Serialize(new
            {
                OriginalAccepted = false,
                ParentEnvelopeAndEndCountersChecked = true,
                report.Owner,
                report.Epoch,
                Calls = report.Static.Protocol.Calls,
                ReadWindows = report.Reads.Count,
                IoRows = report.DeviceRows.Count,
                Connections = report.Static.Connections.Count,
                sectors.Device.Counters,
                MetadataCopies = sectors.Metadata.Count,
                sectors.ReconstructsFileSystemCache,
                sectors.GrantsOriginalNativeProcessOrHttpAuthority
            }));
        else
            Console.WriteLine("DISK_STATIC_REPLAY_VALUES " + System.Text.Json.JsonSerializer.Serialize(new
            {
                Parent = parent,
                report.Static,
                report.Reads,
                report.Operations,
                report.SocketWrites,
                report.Owner,
                report.Epoch,
                report.BootstrapCompleted,
                report.BootstrapCopied,
                report.ReadinessRecordIndex,
                sectors.Device.Counters,
                Assigned = sectors.Data.Assigned.Select(c => new { c.Path, c.Sector, c.Copy.CopyRecordIndex, c.Window.RequestGeneration }),
                sectors.Metadata,
                sectors.ReconstructsFileSystemCache,
                sectors.GrantsOriginalNativeProcessOrHttpAuthority
            }));
        Console.WriteLine($"TEST_PASS qemu.disk.static.values report_only=true calls={report.Static.Protocol.Calls} readWindows={report.Reads.Count} ioRows={report.DeviceRows.Count} metadataCopies={sectors.Metadata.Count} original_native_process_device_and_http_authority=false");
        return 0;
    }
    private static int InspectWebRootInventory(string inventoryImage)
    {
        inventoryImage = Path.GetFullPath(inventoryImage);
        using AuthorityFiles files = new();
        var image = files.Open(Path.GetDirectoryName(inventoryImage)!, inventoryImage, "webroot inventory image",
            "webroot_inventory", WebRootImageFixture.MaximumImageBytes, singleLink: true);
        var receiptFile = files.Open(Path.GetDirectoryName(inventoryImage)!, inventoryImage + ".webroot.json",
            "webroot inventory receipt", "webroot_inventory", 16 * 1024 * 1024, singleLink: true);
        AuthorityFiles.RequireDistinct(image, receiptFile);
        var receipt = System.Text.Json.JsonSerializer.Deserialize<WebRootImageFixture.Receipt>(
            receiptFile.ReadUtf8(16 * 1024 * 1024, "webroot_inventory_receipt")) ?? throw new ToolFailure("webroot_inventory_receipt_missing");
        var report = WebRootDiskInventory.Inspect(inventoryImage,
            image.ReadBytes(WebRootImageFixture.MaximumImageBytes, "webroot_inventory_image"), receipt);
        files.VerifyUnchanged();
        Console.WriteLine("WEBROOT_INVENTORY_VALUES " + System.Text.Json.JsonSerializer.Serialize(new
        {
            report.ImageSha256,
            report.PartitionFirstLba,
            report.PartitionLastLba,
            Entries = report.Entries.Select(e => new
            {
                e.Path,
                e.Inode,
                e.Bytes,
                e.IsDirectory,
                e.Attributes,
                e.ModifiedSeconds,
                e.Sha256,
                e.ShortEntryLba,
                e.ShortEntrySlot,
                e.AllocatedSectors
            })
        }));
        Console.WriteLine($"TEST_PASS qemu.webroot.inventory report_only=true entries={report.Entries.Count} original_native_process_device_and_http_authority=false");
        return 0;
    }
}
