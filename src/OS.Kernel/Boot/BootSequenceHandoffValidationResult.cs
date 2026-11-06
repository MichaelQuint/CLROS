namespace OS.Boot;

public readonly struct BootSequenceHandoffValidationResult
{
    private BootSequenceHandoffValidationResult(bool succeeded, BootSequenceHandoffStatus status, BootCapabilities missingCapabilities, BootCapabilities unknownCapabilities, ulong requiredMapBytes)
    {
        Succeeded = succeeded;
        Status = status;
        MissingCapabilities = missingCapabilities;
        UnknownCapabilities = unknownCapabilities;
        RequiredMapBytes = requiredMapBytes;
    }

    public bool Succeeded
    {
        get;
    }
    public BootSequenceHandoffStatus Status
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
    public ulong RequiredMapBytes
    {
        get;
    }

    public static BootSequenceHandoffValidationResult Success(ulong requiredMapBytes)
    {
        return new BootSequenceHandoffValidationResult(true, BootSequenceHandoffStatus.Success, BootCapabilities.None, BootCapabilities.None, requiredMapBytes);
    }

    public static BootSequenceHandoffValidationResult Fail(BootSequenceHandoffStatus status, BootCapabilities missingCapabilities = BootCapabilities.None, BootCapabilities unknownCapabilities = BootCapabilities.None, ulong requiredMapBytes = 0)
    {
        return new BootSequenceHandoffValidationResult(false, status, missingCapabilities, unknownCapabilities, requiredMapBytes);
    }
}
