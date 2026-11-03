namespace OS.Kernel.Platform;

public readonly struct FirmwareCallResult
{
    private FirmwareCallResult(bool succeeded, FirmwareServicesMode mode, string operation, string reason)
    {
        Succeeded = succeeded;
        Mode = mode;
        Operation = operation;
        Reason = reason;
    }

    public bool Succeeded
    {
        get;
    }

    public FirmwareServicesMode Mode
    {
        get;
    }

    public string Operation
    {
        get;
    }

    public string Reason
    {
        get;
    }

    public static FirmwareCallResult Allowed(string operation, FirmwareServicesMode mode)
    {
        return new FirmwareCallResult(true, mode, Normalize(operation), string.Empty);
    }

    public static FirmwareCallResult Denied(string operation, FirmwareServicesMode mode)
    {
        return new FirmwareCallResult(
            false,
            mode,
            Normalize(operation),
            "UEFI boot services are unavailable in " + mode + " mode.");
    }

    private static string Normalize(string operation)
    {
        return string.IsNullOrWhiteSpace(operation) ? "unknown" : operation;
    }
}
