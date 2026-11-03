namespace OS.Tooling;

public static class SmokeContracts
{
    public static IReadOnlyList<string> RequiredMarkers
    {
        get;
    } = Array.AsReadOnly<string>(
    [
        "OS_SMOKE_PASS", "UEFI_MEMORY_MAP_OWNED", "BOOT_HANDOFF_READY",
        "UEFI_MEMORY_MAP_NORMALIZED", "BOOT_MEMORY_MAP_ACCEPTED",
        "BOOT_MEMORY_REGIONS_READY", "BOOT_MEMORY_REGION_COUNT",
        "BOOT_ACPI_RSDP_HANDOFF_READY", "UEFI_ACPI_RSDP_READY"
    ]);

    public static string[] SplitMarkers(IEnumerable<string> values) => values
        .SelectMany(value => value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)).ToArray();

    public static void ValidateWorkload(string profile)
    {
        if (profile is not ("SingleMinimalApi" or "MultiMinimalApi"))
            throw new ArgumentException("workload.profile.tooling rejects invalid profile=" + profile + " valid=SingleMinimalApi,MultiMinimalApi");
    }
}
