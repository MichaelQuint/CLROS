using System.Collections.ObjectModel;
using System.Globalization;

namespace OS.Tooling;

public sealed class QemuSmokeOptions
{
    public string BootPolicy { get; set; } = "";
    public string Image { get; set; } = "artifacts/images/os.raw";
    public string EfiArtifact { get; set; } = "artifacts/efi/BOOTX64.EFI";
    public string SerialLog { get; set; } = "artifacts/logs/qemu-smoke.log";
    public string SummaryLog { get; set; } = "artifacts/logs/qemu-smoke.summary.log";
    public string M01RawObservationLog { get; set; } = "";
    public string Qemu { get; set; } = "";
    public string OvmfCode { get; set; } = "";
    public string FirmwareRngSource { get; set; } = "";
    public string WorkloadProfile { get; set; } = "SingleMinimalApi";
    public string DiskFormat { get; set; } = "raw";
    public int TimeoutSeconds { get; set; } = 30;
    public bool DryRun { get; set; }
    public bool BuildEfi { get; set; }
    public bool SkipImageBuild { get; set; }
    public bool RequireNativeAppMarkers { get; set; }
    public List<string> RequireMarker { get; } = [.. DefaultRequiredMarkers];
    public List<string> RequireOrderedMarker { get; } = [];
    public List<string> RequireUniqueMarker { get; } = [];
    public List<string> ForbidMarker { get; } = ["OS_SMOKE_FAIL"];
    public List<string> MarkerPreset { get; } = [];
    public Dictionary<string, bool> Flags { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Explicit { get; } = new(StringComparer.Ordinal);
    public static IReadOnlyList<string> DefaultRequiredMarkers { get; } = Array.AsReadOnly<string>(
        ["OS_BOOT_OK", "TEST_PASS boot.sequence", "OS_SMOKE_PASS", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY",
         "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED", "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT",
         "BOOT_PAGE_ALLOCATOR_READY", "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY"]);

    internal static string Key(string value) => value.TrimStart('-').Replace("-", "", StringComparison.Ordinal).ToLowerInvariant();
    public static QemuSmokeOptions Parse(params string[] arguments)
    {
        QemuSmokeOptions result = new();
        var names = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["bootpolicy"] = "BootPolicy", ["image"] = "Image", ["efiartifact"] = "EfiArtifact", ["seriallog"] = "SerialLog",
            ["summarylog"] = "SummaryLog", ["m01rawobservationlog"] = "M01RawObservationLog", ["qemu"] = "Qemu",
            ["ovmfcode"] = "OvmfCode", ["firmwarerngsource"] = "FirmwareRngSource", ["workloadprofile"] = "WorkloadProfile",
            ["diskformat"] = "DiskFormat", ["timeoutseconds"] = "TimeoutSeconds", ["timeout"] = "TimeoutSeconds",
            ["dryrun"] = "DryRun", ["buildefi"] = "BuildEfi", ["skipimagebuild"] = "SkipImageBuild",
            ["requirenativeappmarkers"] = "RequireNativeAppMarkers", ["requiremarker"] = "RequireMarker",
            ["requireorderedmarker"] = "RequireOrderedMarker", ["requireuniquemarker"] = "RequireUniqueMarker",
            ["forbidmarker"] = "ForbidMarker", ["markerpreset"] = "MarkerPreset"
        };
        foreach (string flag in EfiContracts.DiagnosticProbes) names.Add(Key(flag), flag);
        for (int i = 0; i < arguments.Length; i++)
        {
            string input = arguments[i];
            int separator = input.IndexOfAny(['=', ':']);
            string raw = separator < 0 ? input : input[..separator];
            if (!raw.StartsWith('-') || !names.TryGetValue(Key(raw), out string? name)) throw new ArgumentException("qemu_unknown_option " + raw);
            bool first = result.Explicit.Add(name);
            bool flag = name.StartsWith("Enable", StringComparison.Ordinal) || name is "DryRun" or "BuildEfi" or "SkipImageBuild" or "RequireNativeAppMarkers";
            string value = separator >= 0 ? input[(separator + 1)..] : flag ? "true" : ++i < arguments.Length ? arguments[i] : throw new ArgumentException("qemu_missing_value " + name);
            bool bit = false;
            if (flag && !bool.TryParse(value.TrimStart('$'), out bit)) throw new ArgumentException("qemu_invalid_boolean " + name);
            if (name.StartsWith("Enable", StringComparison.Ordinal)) { result.Flags[name] = bit; continue; }
            switch (name)
            {
                case "BootPolicy": result.BootPolicy = value; break;
                case "Image": result.Image = value; break;
                case "EfiArtifact": result.EfiArtifact = value; break;
                case "SerialLog": result.SerialLog = value; break;
                case "SummaryLog": result.SummaryLog = value; break;
                case "M01RawObservationLog": result.M01RawObservationLog = value; break;
                case "Qemu": result.Qemu = value; break;
                case "OvmfCode": result.OvmfCode = value; break;
                case "FirmwareRngSource": result.FirmwareRngSource = value; break;
                case "WorkloadProfile": result.WorkloadProfile = value; break;
                case "DiskFormat": result.DiskFormat = value.ToLowerInvariant(); break;
                case "TimeoutSeconds": result.TimeoutSeconds = int.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture); break;
                case "DryRun": result.DryRun = bit; break;
                case "BuildEfi": result.BuildEfi = bit; break;
                case "SkipImageBuild": result.SkipImageBuild = bit; break;
                case "RequireNativeAppMarkers": result.RequireNativeAppMarkers = bit; break;
                case "RequireMarker": if (first) result.RequireMarker.Clear(); result.RequireMarker.Add(value); break;
                case "RequireOrderedMarker": result.RequireOrderedMarker.Add(value); break;
                case "RequireUniqueMarker": result.RequireUniqueMarker.Add(value); break;
                case "ForbidMarker": if (first) result.ForbidMarker.Clear(); result.ForbidMarker.Add(value); break;
                case "MarkerPreset": result.MarkerPreset.Add(value); break;
            }
        }
        return result;
    }
}

public sealed record QemuSmokePlan(string BootPolicy, IReadOnlyDictionary<string, bool> Flags,
    IReadOnlyList<string> Required, IReadOnlyList<string> Ordered, IReadOnlyList<string> Unique,
    IReadOnlyList<string> Forbidden, IReadOnlyList<string> Presets, bool BuildEfi, bool ReplaceDefaultRequiredMarkers)
{
    public bool IsEnabled(string name) => Flags.TryGetValue(name, out bool enabled) && enabled;
    public bool IsNormalBoot => BootPolicy == "VerifiedExit";
}
