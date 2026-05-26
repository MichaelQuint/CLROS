namespace OS.Kernel.Memory;

public readonly struct MemoryRegionResult
{
    private readonly string? _message;

    private MemoryRegionResult(
        bool succeeded,
        MemoryMapInfoStatus status,
        MemoryRegion region,
        string message)
    {
        Succeeded = succeeded;
        Status = status;
        Region = region;
        _message = message;
    }

    public bool Succeeded { get; }

    public MemoryMapInfoStatus Status { get; }

    public MemoryRegion Region { get; }

    public string Message => _message ?? string.Empty;

    internal static MemoryRegionResult Valid(MemoryRegion region)
    {
        return new MemoryRegionResult(
            true,
            MemoryMapInfoStatus.Success,
            region,
            string.Empty);
    }

    internal static MemoryRegionResult Invalid(
        MemoryMapInfoStatus status,
        string message)
    {
        return new MemoryRegionResult(
            false,
            status,
            default,
            message);
    }
}
