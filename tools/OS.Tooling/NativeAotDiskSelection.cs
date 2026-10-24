namespace OS.Tooling;

/// <summary>Selection for the combined mechanical block/filesystem/fd child.
/// This admits policy only; canonical package, source, native, image and actual
/// completion evidence remain independent obligations of the runner.</summary>
public static class NativeAotDiskSelection
{
    public static bool Selected(IReadOnlyDictionary<string, bool> flags) =>
        flags.TryGetValue("EnableM16025Probe", out bool value) && value;

    internal static bool Bind(QemuSmokeOptions options, IReadOnlyDictionary<string, bool> flags)
    {
        bool disk = Selected(flags);
        if (!disk)
        {
            if (options.WebRootFixture.Length != 0 || options.WebRootAssets.Length != 0)
                throw new ToolFailure("disk_fixture_requires_disk_profile");
            return false;
        }
        if (flags.TryGetValue("EnableM10018Probe", out bool web) && web)
            throw new ToolFailure("disk_static_complete_profile_required");
        if (options.HasThreadingSelection || options.LinuxHelloDisassembly.Length != 0 || options.HasWebSignalRace ||
            options.ServiceLifecycle || options.ServiceQuotaFailure)
            throw new ToolFailure("disk_mechanical_selection_conflict");
        if (options.WorkloadProfile != "SingleMinimalApi" || options.DiskFormat != "raw" ||
            options.LinuxElfImageVariant != "Intact" || options.LinuxElfPackage.Length == 0)
            throw new ToolFailure("disk_mechanical_original_raw_required");
        if (options.WebRootFixture is not ("DiskA" or "DiskB") || options.WebRootAssets.Length == 0)
            throw new ToolFailure("disk_webroot_fixture_required");
        if (!options.Explicit.Contains("TimeoutSeconds")) options.TimeoutSeconds = 180;
        if (options.TimeoutSeconds is < 120 or > 285) throw new ToolFailure("disk_mechanical_timeout");
        return true;
    }
}
