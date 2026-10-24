namespace OS.Tooling;

public static partial class QemuTranscripts
{
    // The production caller holds its generated image/package/EFI and original
    // native/source closure from before launch. Keep all capture files held
    // through its final verification callback; a replay cannot enter this path.
    internal static LinuxDiskCaptureInspection InspectLinuxDiskFileRun(string root, QemuSmokeOptions options,
        QemuSmokePlan plan, ProcessResult runner, AuthorityFile image, AuthorityFile efi, AuthorityFile package,
        WebRootImageFixture.Receipt receipt, string qemu, string ovmf, Action verifyOwnedInputs)
    {
        Need(!options.HasDiskRaceSelection,"disk_runner_positive_selection");
        return InspectOriginalLinuxDiskRun(root,options,plan,runner,image,efi,package,qemu,ovmf,verifyOwnedInputs,rows=>
        {
            var report=InspectLinuxDiskCaptureValues(rows,package.ReadBytes(65536,"disk_package"),
                image.ReadBytes(WebRootImageFixture.MaximumImageBytes,"disk_image"),receipt,image.Path);
            return(report,SocketNetworkEnvelope(rows,true,mechanical:true,diskFiles:true).Proof);
        });
    }

    internal static LinuxDiskExitPendingCaptureInspection InspectLinuxDiskExitPendingRun(string root,QemuSmokeOptions options,
        QemuSmokePlan plan,ProcessResult runner,AuthorityFile image,AuthorityFile efi,AuthorityFile package,
        WebRootImageFixture.Receipt receipt,string qemu,string ovmf,Action verifyOwnedInputs)
    {
        Need(options.HasDiskRaceSelection && options.LinuxDiskRaceVariant=="ExitPending","disk_runner_exit_selection");
        return InspectOriginalLinuxDiskRun(root,options,plan,runner,image,efi,package,qemu,ovmf,verifyOwnedInputs,rows=>
        {
            var report=InspectLinuxDiskExitPendingCaptureValues(rows,package.ReadBytes(65536,"disk_package"),
                image.ReadBytes(WebRootImageFixture.MaximumImageBytes,"disk_image"),receipt,image.Path);
            return(report,SocketNetworkEnvelope(rows,true,mechanical:true,diskFiles:true,diskExitPending:report.Pending).Proof);
        });
    }

    private static T InspectOriginalLinuxDiskRun<T>(string root,QemuSmokeOptions options,QemuSmokePlan plan,
        ProcessResult runner,AuthorityFile image,AuthorityFile efi,AuthorityFile package,string qemu,string ovmf,
        Action verifyOwnedInputs,Func<string[],(T Report,SocketNetworkProof Network)> inspect)
    {
        RequireCompleteProcess(runner, "disk_runner_incomplete");
        Need(runner.Succeeded && runner.StandardError.Length == 0 && NativeAotDiskSelection.Selected(plan.Flags) &&
            !NativeAotDiskSelection.StaticSelected(plan.Flags) &&
            options.WorkloadProfile == "SingleMinimalApi" && options.FirmwareRngSource == "virtio" &&
            options.LinuxElfImageVariant == "Intact" && !options.SkipImageBuild && plan.BuildEfi, "disk_runner_selection");
        using AuthorityFiles captured = new();
        AuthorityFile Hold(string path, int maximum) => captured.Open(root, Path.GetFullPath(path, root),
            "original disk capture", "disk_capture", maximum, singleLink: true);
        var serial = Hold(options.SerialLog, 8 * 1024 * 1024);
        var summary = Hold(options.SummaryLog, 16 * 1024 * 1024);
        var packets = Hold(options.SerialLog + ".pcap", 4 * 1024 * 1024);
        AuthorityFiles.RequireDistinct(image, efi, package, serial, summary, packets);
        string[] stdout = runner.StandardOutput.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (stdout[^1] == "") stdout = stdout[..^1];
        Need(stdout.Length == 18 && stdout[0] == $"QEMU_DISCOVERY result={qemu} checked={qemu}" &&
            stdout[1] == $"OVMF_DISCOVERY result={ovmf} checked={ovmf}", "disk_runner_discovery");
        AssertStandardRunnerResult(root, options, plan, stdout);
        string[] rows = FirmwareProtocol(serial.Path, "TEST_PASS m16-012\r\n", true, false, "disk_capture", linuxThreads: true);
        var result=inspect(rows);
        AssertSocketCapture(packets.Path,result.Network,mechanical:true);
        ValidateUnselectedRecords(serial.ReadUtf8(8 * 1024 * 1024, "disk_serial"), plan);
        captured.VerifyUnchanged(); verifyOwnedInputs(); captured.VerifyUnchanged();
        return result.Report;
    }

    /// <summary>Inspect the exact finite leaf in a supplied complete row array.
    /// Parent roots are parsed as values only; parent records, native inputs,
    /// device/image/packet joins and original execution remain unchecked.</summary>
    public static LinuxDiskFileLeafReport InspectLinuxDiskCaptureLeafForTests(string[] rows, byte[] package)
    {
        Need(rows is { Length: > 0 and <= 131072 } && rows.All(row => row is { Length: > 0 and <= 1536 } &&
            row.All(value => value is >= ' ' and <= '~')), "disk_capture_input");
        string[] mapped = rows.Where(row => row.StartsWith("M16_004_TABLES ", StringComparison.Ordinal)).ToArray();
        string[] restored = rows.Where(row => row.StartsWith("M16_004_READBACK ", StringComparison.Ordinal)).ToArray();
        const string suffix = " restored=true loads=true bss=true stack=true guard=absent mappings=user-supervisor wx=false entry=false";
        Need(mapped.Length == 1 && restored.Length == 1 && restored[0].EndsWith(suffix, StringComparison.Ordinal), "disk_capture_roots");
        var tables = ReadRecord(mapped[0], "M16_004_TABLES", "root:dec", "used:dec", "leaves:dec", "ranges:dec");
        var kernel = ReadRecord(restored[0][..^suffix.Length], "M16_004_READBACK", "old:dec");
        int first = Array.IndexOf(rows, "TEST_START m16-009"), last = Array.IndexOf(rows, "TEST_PASS m16-009");
        Need(first >= 0 && last > first && Array.LastIndexOf(rows, "TEST_START m16-009") == first &&
            Array.LastIndexOf(rows, "TEST_PASS m16-009") == last, "disk_capture_leaf_extent");
        return InspectLinuxDiskFileLeafForTests(rows[first..(last + 1)], package, tables["root"], kernel["old"]);
    }

    private static readonly string[] DiskFileEndMarkers =
    [ "CLROS_M16_023_READY", "TEST_PASS m16.023", "CLROS_M16_024_READY", "TEST_PASS m16.024",
      "CLROS_M16_025_READY", "TEST_PASS m16.025" ];

    private static Dictionary<string, ulong> ReadDiskFileEnd(string row, LinuxDiskExitPendingDeviceValues? pending = null)
    {
        const string suffix = " device=retired fs=retired dma=zero descriptors=closed reservation=retained";
        Need(row.EndsWith(suffix, StringComparison.Ordinal), "disk_parent_cleanup");
        var end = ReadRecord(row[..^suffix.Length], "M16_025_END", "submitted:dec", "completed:dec", "copied:dec",
            "bootstrapCompleted:dec", "bootstrapCopied:dec", "fsReads:dec", "cacheHits:dec", "cacheMisses:dec");
        Need(end["bootstrapCompleted"] >= 35 && end["bootstrapCopied"] == end["bootstrapCompleted"] &&
            end["submitted"] > end["bootstrapCompleted"] &&
            (pending is null ? end["submitted"] == end["completed"] : end["completed"] < ulong.MaxValue && end["submitted"] == end["completed"] + 1) &&
            end["copied"] == end["completed"] && end["fsReads"] <= ulong.MaxValue - 34 &&
            end["fsReads"] + 34 == end["submitted"] && end["cacheHits"] <= long.MaxValue &&
            end["cacheMisses"] <= end["fsReads"], "disk_parent_counters");
        if (pending is not null)
        {
            var b = pending.Device.Counters; var last = pending.Stages.Records[7].Values;
            Need(pending.Device.Cancelled == 0 && pending.Device.ResetRetirement is not null &&
                end["submitted"] == b.Submitted && end["completed"] == b.Completed && end["copied"] == b.Copied &&
                end["bootstrapCompleted"] == b.BootstrapCompleted && end["bootstrapCopied"] == b.BootstrapCopied &&
                last["submitted"] == end["submitted"] && last["completed"] == end["completed"] &&
                last["copied"] == end["copied"] && last["cancelled"] == 0, "disk_parent_reset_terminal");
        }
        return end;
    }

    /// <summary>Inspect only the copied mechanical network envelope and strict
    /// ExitPending terminal, using a device/stage result built from these same
    /// rows, canonical package and raw image. The canonical leaf/FIFO, firmware,
    /// native/source, image receipt, packets and original runner remain separate
    /// joins. Ordinary/018 callers cannot select this reset-retirement path.</summary>
    public static LinuxDiskExitPendingDeviceValues InspectLinuxDiskExitPendingNetworkForTests(
        string[] rows, byte[] package, ReadOnlySpan<byte> rawImage)
    {
        var pending = InspectLinuxDiskExitPendingDeviceValues(rows, package, rawImage);
        var envelope = SocketNetworkEnvelope(rows, true, mechanical: true, diskFiles: true, diskExitPending: pending);
        Need(pending.Stages.Records[7].Values["clock"] <= envelope.Proof.End["end"], "disk_parent_reset_lifetime");
        return pending;
    }

    /// <summary>Check only the supplied mechanical disk/NIC envelope values,
    /// including exact final disk counters and retired owners. The canonical
    /// leaf, firmware/startup, sector bytes, native code, original runner and
    /// independent packets remain mandatory separate joins. Inputs are borrowed;
    /// malformed, excessive, unknown or incomplete rows throw.</summary>
    public static void InspectLinuxDiskNetworkForTests(string[] rows)
    {
        Need(rows is { Length: > 0 and <= 131072 } && rows.All(row => row is { Length: > 0 and <= 1536 } &&
            row.All(value => value is >= ' ' and <= '~')), "disk_parent_input");
        _ = SocketNetworkEnvelope(rows, true, mechanical: true, diskFiles: true);
    }
}
