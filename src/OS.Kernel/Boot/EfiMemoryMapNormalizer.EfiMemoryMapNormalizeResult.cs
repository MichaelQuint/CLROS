namespace OS.Boot;
public readonly struct EfiMemoryMapNormalizeResult
{
    private EfiMemoryMapNormalizeResult(bool succeeded, EfiMemoryMapNormalizeStatus status, EfiMemoryMapSummary summary, ulong invalidDescriptorIndex, ulong requiredSourceBytes)
    {
        Succeeded = succeeded;
        Status = status;
        Summary = summary;
        InvalidDescriptorIndex = invalidDescriptorIndex;
        RequiredSourceBytes = requiredSourceBytes;
    }

    public bool Succeeded { get; }
    public EfiMemoryMapNormalizeStatus Status { get; }
    public EfiMemoryMapSummary Summary { get; }
    public ulong InvalidDescriptorIndex { get; }
    public ulong RequiredSourceBytes { get; }

    public static EfiMemoryMapNormalizeResult Success(EfiMemoryMapSummary summary, ulong requiredSourceBytes)
    {
        return new EfiMemoryMapNormalizeResult(true, EfiMemoryMapNormalizeStatus.Success, summary, 0, requiredSourceBytes);
    }

    public static EfiMemoryMapNormalizeResult Fail(EfiMemoryMapNormalizeStatus status, ulong invalidDescriptorIndex = 0, ulong requiredSourceBytes = 0)
    {
        return new EfiMemoryMapNormalizeResult(false, status, default, invalidDescriptorIndex, requiredSourceBytes);
    }
}
