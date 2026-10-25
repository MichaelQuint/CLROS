namespace OS.Tooling;

/// <summary>Selection for the combined mechanical block/filesystem/fd child.
/// This admits policy only; canonical package, source, native, image and actual
/// completion evidence remain independent obligations of the runner.</summary>
public static class NativeAotDiskSelection
{
    public static bool Selected(IReadOnlyDictionary<string, bool> flags) =>
        flags.TryGetValue("EnableM16025Probe", out bool value) && value;

    public static bool WritableSelected(IReadOnlyDictionary<string, bool> flags) =>
        flags.TryGetValue("EnableM16027Probe", out bool value) && value;

    /// <summary>Require the complete canonical package for the explicit30/40
    /// syscall fixture; neither the old59-call read fixture nor a variant is accepted.</summary>
    public static void RequireWritablePackage(ReadOnlySpan<byte> package, string fixture)
    {
        if (fixture == "Mechanical30") LinuxWritableDiskProofFixture.RequirePackage(package);
        else if (fixture == "LogUpload40") LinuxWritableDiskProofFixture.RequireLogUploadPackage(package);
        else throw new ToolFailure("writable_disk_fixture_required");
    }

    public static bool StaticSelected(IReadOnlyDictionary<string, bool> flags) =>
        Selected(flags) && flags.TryGetValue("EnableM10018Probe", out bool value) && value;

    public static bool FullStaticSelected(IReadOnlyDictionary<string, bool> flags) =>
        StaticSelected(flags) && flags.TryGetValue("EnableM10018FullProbe", out bool value) && value;

    public static bool TimingSelected(IReadOnlyDictionary<string, bool> flags) =>
        FullStaticSelected(flags) && flags.TryGetValue("EnableM10018TimingProbe", out bool value) && value;

    /// <summary>Require complete canonical bytes for one explicitly selected
    /// mechanical original consumer. Empty selection preserves the positive
    /// fixture; only ExitPending has the separate pending-reset consumer.
    /// No package, image, process or native authority is granted here.</summary>
    public static void RequireMechanicalPackage(byte[] package,string raceVariant)
    {
        if(raceVariant.Length==0){LinuxDiskFileProofFixture.RequirePackage(package);return;}
        if(raceVariant!="ExitPending")throw new ToolFailure("disk_race_complete_consumer_required");
        if(LinuxDiskRaceProofFixture.PackageVariant(package)!="ExitPending")throw new ToolFailure("disk_race_selected_package");
    }

    internal static bool Bind(QemuSmokeOptions options, IReadOnlyDictionary<string, bool> flags)
    {
        bool disk = Selected(flags);
        bool writable = WritableSelected(flags);
        bool race = options.HasDiskRaceSelection;
        if (!writable && (options.WritableDiskFixture.Length != 0 || options.Explicit.Contains("WritableDiskFixture")))
            throw new ToolFailure("writable_disk_fixture_requires_profile");
        if (writable)
        {
            if (!disk || options.WritableDiskFixture is not ("Mechanical30" or "LogUpload40"))
                throw new ToolFailure("writable_disk_fixture_required");
            if (race || StaticSelected(flags) || options.HasThreadingSelection || options.LinuxHelloDisassembly.Length != 0 ||
                options.SignalEpollRace || options.HasWebSignalRace || options.ServiceLifecycle || options.ServiceQuotaFailure)
                throw new ToolFailure("writable_disk_selection_conflict");
            if (options.WorkloadProfile != "SingleMinimalApi" || options.DiskFormat != "raw" || options.LinuxElfPackage.Length == 0 ||
                options.LinuxElfImageVariant != "Intact" || options.GptImageVariant != "Intact" || options.PackageImageVariant != "Intact")
                throw new ToolFailure("writable_disk_original_raw_required");
            if (options.WebRootFixture is not ("DiskA" or "DiskB") || options.WebRootAssets.Length == 0)
                throw new ToolFailure("writable_disk_webroot_fixture_required");
            if (!options.Explicit.Contains("TimeoutSeconds")) options.TimeoutSeconds = 180;
            // The raw-capture owner adds75seconds; the writable device runner
            // retains its separately admitted120..295second execution budget.
            if (options.TimeoutSeconds is < 120 or > 220) throw new ToolFailure("writable_disk_timeout");
            return true;
        }
        if (race && !LinuxDiskRaceProofFixture.Variants.Contains(options.LinuxDiskRaceVariant, StringComparer.Ordinal))
            throw new ToolFailure("disk_race_variant");
        if (race && !disk) throw new ToolFailure("disk_race_requires_mechanical_disk_profile");
        if (!disk)
        {
            if (options.WebRootFixture.Length != 0 || options.WebRootAssets.Length != 0)
                throw new ToolFailure("disk_fixture_requires_disk_profile");
            return false;
        }
        bool web = StaticSelected(flags);
        if (race && (web || options.SignalEpollRace || options.HasThreadingSelection ||
            options.Explicit.Contains("LinuxHelloDisassembly") || options.HasWebSignalRace ||
            options.ServiceLifecycle || options.ServiceQuotaFailure))
            throw new ToolFailure("disk_race_selection_conflict");
        if (race && (options.GptImageVariant != "Intact" || options.PackageImageVariant != "Intact"))
            throw new ToolFailure("disk_race_original_image_required");
        if ((!web && options.HasThreadingSelection) || options.LinuxHelloDisassembly.Length != 0 || options.HasWebSignalRace ||
            options.ServiceLifecycle || options.ServiceQuotaFailure)
            throw new ToolFailure("disk_mechanical_selection_conflict");
        if (options.WorkloadProfile != "SingleMinimalApi" || options.DiskFormat != "raw" ||
            options.LinuxElfImageVariant != "Intact" || options.LinuxElfPackage.Length == 0)
            throw new ToolFailure("disk_mechanical_original_raw_required");
        if (options.WebRootFixture is not ("DiskA" or "DiskB") || options.WebRootAssets.Length == 0)
            throw new ToolFailure("disk_webroot_fixture_required");
        if (TimingSelected(flags))
        {
            if (!options.Explicit.Contains("TimeoutSeconds")) options.TimeoutSeconds = 600;
            if (options.TimeoutSeconds is < 240 or > 600) throw new ToolFailure("disk_timing_timeout");
            return true;
        }
        if (FullStaticSelected(flags))
        {
            if (!options.Explicit.Contains("TimeoutSeconds")) options.TimeoutSeconds = DiskStaticCapturePolicy.DefaultTimeoutSeconds;
            if (options.TimeoutSeconds is < DiskStaticCapturePolicy.MinimumTimeoutSeconds or > DiskStaticCapturePolicy.MaximumTimeoutSeconds)
                throw new ToolFailure("disk_static_full_timeout");
            return true;
        }
        if (!options.Explicit.Contains("TimeoutSeconds")) options.TimeoutSeconds = web ? 825 : 180;
        if (web ? options.TimeoutSeconds is < 600 or > 825 : options.TimeoutSeconds is < 120 or > 285)
            throw new ToolFailure(web ? "disk_static_timeout" : "disk_mechanical_timeout");
        return true;
    }
}
