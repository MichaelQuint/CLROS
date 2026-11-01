namespace OS.Boot;
public readonly struct BootMemoryMapAcceptanceResult
{
    private BootMemoryMapAcceptanceResult(bool succeeded, BootMemoryMapAcceptanceStatus status)
    {
        Succeeded = succeeded;
        Status = status;
    }

    public bool Succeeded { get; }
    public BootMemoryMapAcceptanceStatus Status { get; }

    public static BootMemoryMapAcceptanceResult Success()
    {
        return new BootMemoryMapAcceptanceResult(true, BootMemoryMapAcceptanceStatus.Success);
    }

    public static BootMemoryMapAcceptanceResult Fail(BootMemoryMapAcceptanceStatus status)
    {
        return new BootMemoryMapAcceptanceResult(false, status);
    }
}
