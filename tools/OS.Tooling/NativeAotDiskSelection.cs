namespace OS.Tooling;
/// <summary>Selection for the combined mechanical block/filesystem/fd child.
/// This admits policy only; canonical package, source, native, image and actual
/// completion evidence remain independent obligations of the runner.</summary>

public static partial class NativeAotDiskSelection
{
    public static WritableProgramKind ParseWritableProgram(string name) => name switch
    {
        "Mechanical30" => WritableProgramKind.Mechanical30,
        "LogUpload40" => WritableProgramKind.LogUpload40,
        "BclWrite" => WritableProgramKind.BclWrite,
        "BclVerify" => WritableProgramKind.BclVerify,
        _ => throw new ToolFailure("writable_disk_fixture_required")};
    public static bool IsBcl(WritableProgramKind kind) => kind switch
    {
        WritableProgramKind.Mechanical30 or WritableProgramKind.LogUpload40 => false,
        WritableProgramKind.BclWrite or WritableProgramKind.BclVerify => true,
        _ => throw new ToolFailure("writable_disk_fixture_required")};
    /// <summary>Pre-read bound only. The complete canonical package must still
    /// match the selected phase; this does not raise any global image limit.</summary>

    public static int WritablePackageMaximumBytes(WritableProgramKind kind) => IsBcl(kind) ? NativeAotLogUploadContract.ClrosElfBytes + 4096 : 65536;
    public static bool Selected(IReadOnlyDictionary<string, bool> flags) => flags.TryGetValue("EnableM16025Probe", out bool value) && value;
    public static bool WritableSelected(IReadOnlyDictionary<string, bool> flags) => flags.TryGetValue("EnableM16027Probe", out bool value) && value;
    /// <summary>Require complete canonical bytes for the explicit target. BCL
    /// phases require the fixed original ELF and exact phase manifest; this
    /// copied check admits no prepared index, process, device or guest.</summary>

    public static void RequireWritablePackage(ReadOnlySpan<byte> package, string fixture) => RequireWritablePackage(package, ParseWritableProgram(fixture));
    public static void RequireWritablePackage(ReadOnlySpan<byte> package, WritableProgramKind kind)
    {
        switch (kind)
        {
            case WritableProgramKind.Mechanical30:
                LinuxWritableDiskProofFixture.RequirePackage(package);
                break;
            case WritableProgramKind.LogUpload40:
                LinuxWritableDiskProofFixture.RequireLogUploadPackage(package);
                break;
            case WritableProgramKind.BclWrite:
                _ = NativeAotLogUploadContract.InspectClrosPackage(package, "write");
                break;
            case WritableProgramKind.BclVerify:
                _ = NativeAotLogUploadContract.InspectClrosPackage(package, "verify");
                break;
            default:
                throw new ToolFailure("writable_disk_fixture_required");
        }
    }

    public static bool StaticSelected(IReadOnlyDictionary<string, bool> flags) => Selected(flags) && flags.TryGetValue("EnableM10018Probe", out bool value) && value;
    public static bool FullStaticSelected(IReadOnlyDictionary<string, bool> flags) => StaticSelected(flags) && flags.TryGetValue("EnableM10018FullProbe", out bool value) && value;
    public static bool TimingSelected(IReadOnlyDictionary<string, bool> flags) => FullStaticSelected(flags) && flags.TryGetValue("EnableM10018TimingProbe", out bool value) && value;
    /// <summary>Require complete canonical bytes for one explicitly selected
    /// mechanical original consumer. Empty selection preserves the positive
    /// fixture; Progress checks overlapping reads, CloseReuse checks the detached
    /// old token and later descriptor reads, and ExitPending checks pending reset.
    /// No package, image, process or native authority is granted here.</summary>

    public static void RequireMechanicalPackage(byte[] package, string raceVariant)
    {
        if (raceVariant.Length == 0)
        {
            LinuxDiskFileProofFixture.RequirePackage(package);
            return;
        }

        if (raceVariant is not ("Progress" or "CloseReuse" or "ExitPending"))
            throw new ToolFailure("disk_race_complete_consumer_required");
        if (LinuxDiskRaceProofFixture.PackageVariant(package) != raceVariant)
            throw new ToolFailure("disk_race_selected_package");
    }

    internal static bool Bind(QemuSmokeOptions options, IReadOnlyDictionary<string, bool> flags)
    {
        bool disk = Selected(flags);
        bool writable = WritableSelected(flags);
        bool race = options.HasDiskRaceSelection;
        if (options.WebRootFault is not ("None" or "Truncated"))
            throw new ToolFailure("disk_webroot_fault");
        if (options.WebRootFault == "Truncated" && (!FullStaticSelected(flags) || TimingSelected(flags) || writable || race || !options.BuildEfi || options.SkipImageBuild || options.GptImageVariant != "Intact" || options.PackageImageVariant != "Intact"))
            throw new ToolFailure("disk_truncated_full_original_required");
        bool disassembly = !string.IsNullOrEmpty(options.WritableDiskDisassembly) || options.Explicit.Contains("WritableDiskDisassembly");
        if (!writable && (options.WritableDiskFixture.Length != 0 || options.Explicit.Contains("WritableDiskFixture") || disassembly))
            throw new ToolFailure("writable_disk_fixture_requires_profile");
        if (writable)
        {
            if (!disk)
                throw new ToolFailure("writable_disk_fixture_required");
            bool bcl = IsBcl(ParseWritableProgram(options.WritableDiskFixture));
            if (flags.TryGetValue("EnableM16027TimingProbe", out bool timing) && timing && options.WritableDiskFixture != "Mechanical30")
                throw new ToolFailure("writable_timing_mechanical30_required");
            if (bcl)
            {
                RequireBclDisassembly(options);
                if (options.Explicit.Contains("LinuxHelloDisassembly"))
                    throw new ToolFailure("writable_disk_selection_conflict");
            }
            else if (disassembly)
                throw new ToolFailure("writable_disk_disassembly_requires_bcl");
            if (race || StaticSelected(flags) || options.HasThreadingSelection || options.LinuxHelloDisassembly.Length != 0 || options.SignalEpollRace || options.HasWebSignalRace || options.ServiceLifecycle || options.ServiceQuotaFailure)
                throw new ToolFailure("writable_disk_selection_conflict");
            if (options.WorkloadProfile != "SingleMinimalApi" || options.DiskFormat != "raw" || options.LinuxElfPackage.Length == 0 || options.LinuxElfImageVariant != "Intact" || options.GptImageVariant != "Intact" || options.PackageImageVariant != "Intact")
                throw new ToolFailure("writable_disk_original_raw_required");
            if (options.WebRootFixture is not ("DiskA" or "DiskB") || options.WebRootAssets.Length == 0)
                throw new ToolFailure("writable_disk_webroot_fixture_required");
            if (!options.Explicit.Contains("TimeoutSeconds"))
                options.TimeoutSeconds = 180;
            // The raw-capture owner adds75seconds; the writable device runner
            // retains its separately admitted120..295second execution budget.
            if (options.TimeoutSeconds is < 120 or > 220)
                throw new ToolFailure("writable_disk_timeout");
            // No current runner/consumer handles the BCL4LOAD/TLS/exit0 policy.
            // Fail before any builder, image mutation or child can be invoked.
            if (bcl && !options.DryRun)
                throw new ToolFailure("writable_bcl_complete_consumer_required");
            return true;
        }

        if (race && !LinuxDiskRaceProofFixture.Variants.Contains(options.LinuxDiskRaceVariant, StringComparer.Ordinal))
            throw new ToolFailure("disk_race_variant");
        if (race && !disk)
            throw new ToolFailure("disk_race_requires_mechanical_disk_profile");
        if (!disk)
        {
            if (options.WebRootFixture.Length != 0 || options.WebRootAssets.Length != 0)
                throw new ToolFailure("disk_fixture_requires_disk_profile");
            return false;
        }

        bool web = StaticSelected(flags);
        if (race && (web || options.SignalEpollRace || options.HasThreadingSelection || options.Explicit.Contains("LinuxHelloDisassembly") || options.HasWebSignalRace || options.ServiceLifecycle || options.ServiceQuotaFailure))
            throw new ToolFailure("disk_race_selection_conflict");
        if (race && (options.GptImageVariant != "Intact" || options.PackageImageVariant != "Intact"))
            throw new ToolFailure("disk_race_original_image_required");
        if ((!web && options.HasThreadingSelection) || options.LinuxHelloDisassembly.Length != 0 || options.HasWebSignalRace || options.ServiceLifecycle || options.ServiceQuotaFailure)
            throw new ToolFailure("disk_mechanical_selection_conflict");
        if (options.WorkloadProfile != "SingleMinimalApi" || options.DiskFormat != "raw" || options.LinuxElfImageVariant != "Intact" || options.LinuxElfPackage.Length == 0)
            throw new ToolFailure("disk_mechanical_original_raw_required");
        if (options.WebRootFixture is not ("DiskA" or "DiskB") || options.WebRootAssets.Length == 0)
            throw new ToolFailure("disk_webroot_fixture_required");
        if (TimingSelected(flags))
        {
            if (!options.Explicit.Contains("TimeoutSeconds"))
                options.TimeoutSeconds = 600;
            if (options.TimeoutSeconds is < 240 or > 600)
                throw new ToolFailure("disk_timing_timeout");
            return true;
        }

        if (FullStaticSelected(flags))
        {
            if (!options.Explicit.Contains("TimeoutSeconds"))
                options.TimeoutSeconds = DiskStaticCapturePolicy.DefaultTimeoutSeconds;
            if (options.TimeoutSeconds is < DiskStaticCapturePolicy.MinimumTimeoutSeconds or > DiskStaticCapturePolicy.MaximumTimeoutSeconds)
                throw new ToolFailure("disk_static_full_timeout");
            return true;
        }

        if (!options.Explicit.Contains("TimeoutSeconds"))
            options.TimeoutSeconds = web ? 825 : 180;
        if (web ? options.TimeoutSeconds is < 600 or > 825 : options.TimeoutSeconds is < 120 or > 285)
            throw new ToolFailure(web ? "disk_static_timeout" : "disk_mechanical_timeout");
        return true;
    }

    private static void RequireBclDisassembly(QemuSmokeOptions options)
    {
        string path = options.WritableDiskDisassembly;
        if (string.IsNullOrWhiteSpace(path) || path.IndexOfAny(['\0', '\r', '\n']) >= 0)
            throw new ToolFailure("writable_disk_disassembly_required");
        path = Path.GetFullPath(path);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        foreach (string other in new[]
        {
            options.LinuxElfPackage,
            options.Image,
            options.EfiArtifact,
            options.SerialLog,
            options.SerialLog + ".pcap",
            options.SummaryLog,
            options.SummaryLog + ".stderr.log",
            options.M01RawObservationLog,
            options.Image + ".web.raw",
            options.Image + ".data.raw",
            options.Image + ".working.raw",
            options.Image + ".joined.raw",
            options.Image + ".combined.json"
        }

        )
            if (other.Length != 0 && string.Equals(path, Path.GetFullPath(other), comparison))
                throw new ToolFailure("writable_disk_disassembly_alias");
    // Logical path checks only; the future original owner must independently
    // hold and compare resolved file identities, sources and prepared index.
    }
}
