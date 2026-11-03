namespace OS.Boot;

public readonly struct BootCapabilityResult
{
    private readonly string? _message;

    private BootCapabilityResult(
        bool succeeded,
        BootCapabilities requiredCapabilities,
        BootCapabilities missingCapabilities,
        BootCapabilities unknownCapabilities,
        string message)
    {
        Succeeded = succeeded;
        RequiredCapabilities = requiredCapabilities;
        MissingCapabilities = missingCapabilities;
        UnknownCapabilities = unknownCapabilities;
        _message = message;
    }

    public bool Succeeded
    {
        get;
    }

    public BootCapabilities RequiredCapabilities
    {
        get;
    }

    public BootCapabilities MissingCapabilities
    {
        get;
    }

    public BootCapabilities UnknownCapabilities
    {
        get;
    }

    public string Message => _message ?? string.Empty;

    public static BootCapabilityResult Available(BootCapabilities requiredCapabilities)
    {
        return new BootCapabilityResult(
            true,
            requiredCapabilities,
            BootCapabilities.None,
            BootCapabilities.None,
            string.Empty);
    }

    public static BootCapabilityResult Unavailable(
        BootCapabilities requiredCapabilities,
        BootCapabilities missingCapabilities)
    {
        return new BootCapabilityResult(
            false,
            requiredCapabilities,
            missingCapabilities,
            BootCapabilities.None,
            "Missing boot capability: " + missingCapabilities + ".");
    }

    public static BootCapabilityResult Unknown(
        BootCapabilities requiredCapabilities,
        BootCapabilities unknownCapabilities)
    {
        return new BootCapabilityResult(
            false,
            requiredCapabilities,
            BootCapabilities.None,
            unknownCapabilities,
            "Unknown boot capability: " + unknownCapabilities + ".");
    }
}
