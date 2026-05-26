namespace OS.Boot;

public readonly struct BootInfo
{
    public const string UnknownFirmwareVendor = "unknown";
    public const string HostFirmwareVendor = "host";

    private const BootCapabilities KnownCapabilities =
        BootCapabilities.TextOutput
        | BootCapabilities.Shutdown
        | BootCapabilities.MemoryMap;

    private readonly string? _firmwareVendor;

    public BootInfo(
        BootMode mode,
        string? firmwareVendor,
        uint firmwareRevision,
        BootCapabilities capabilities)
    {
        Mode = mode;
        _firmwareVendor = NormalizeFirmwareVendor(firmwareVendor);
        FirmwareRevision = firmwareRevision;
        Capabilities = capabilities;
    }

    public BootMode Mode { get; }

    public string FirmwareVendor => _firmwareVendor ?? UnknownFirmwareVendor;

    public uint FirmwareRevision { get; }

    public BootCapabilities Capabilities { get; }

    public static BootInfo Unknown => new BootInfo(
        BootMode.Unknown,
        UnknownFirmwareVendor,
        0,
        BootCapabilities.None);

    public static BootInfo CreateHost(BootCapabilities capabilities)
    {
        return new BootInfo(BootMode.Host, HostFirmwareVendor, 0, capabilities);
    }

    public static BootInfo CreateUefi(
        string? firmwareVendor,
        uint firmwareRevision,
        BootCapabilities capabilities)
    {
        return new BootInfo(BootMode.Uefi, firmwareVendor, firmwareRevision, capabilities);
    }

    public bool HasCapability(BootCapabilities capability)
    {
        return capability == BootCapabilities.None
            || (IsKnownCapabilitySet(capability) && (Capabilities & capability) == capability);
    }

    public BootCapabilityResult RequireCapability(BootCapabilities capability)
    {
        BootCapabilities unknownCapabilities = capability & ~KnownCapabilities;
        if (unknownCapabilities != BootCapabilities.None)
        {
            return BootCapabilityResult.Unknown(capability, unknownCapabilities);
        }

        BootCapabilities missingCapabilities = capability & ~Capabilities;
        if (missingCapabilities == BootCapabilities.None)
        {
            return BootCapabilityResult.Available(capability);
        }

        return BootCapabilityResult.Unavailable(capability, missingCapabilities);
    }

    private static bool IsKnownCapabilitySet(BootCapabilities capability)
    {
        return (capability & ~KnownCapabilities) == BootCapabilities.None;
    }

    private static string NormalizeFirmwareVendor(string? firmwareVendor)
    {
        if (string.IsNullOrWhiteSpace(firmwareVendor))
        {
            return UnknownFirmwareVendor;
        }

        return firmwareVendor.Trim();
    }
}
