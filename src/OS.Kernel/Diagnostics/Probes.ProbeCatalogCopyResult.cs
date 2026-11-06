namespace OS.Kernel.Diagnostics;
#if !NOSTDLIB_EFI
public readonly struct ProbeCatalogCopyResult
{
    private readonly bool _isInitialized;
    private ProbeCatalogCopyResult(ProbeCatalogStatus status, int requiredCount, int availableCount, int writtenCount)
    {
        _isInitialized = true;
        Status = status;
        RequiredCount = requiredCount;
        AvailableCount = availableCount;
        WrittenCount = writtenCount;
    }

    public ProbeCatalogStatus Status
    {
        get;
    }
    public int RequiredCount
    {
        get;
    }
    public int AvailableCount
    {
        get;
    }
    public int WrittenCount
    {
        get;
    }
    public bool Succeeded => _isInitialized && Status == ProbeCatalogStatus.Success && RequiredCount == Probes.ProbeCatalogCount && WrittenCount == RequiredCount;

    public static ProbeCatalogCopyResult Success(int requiredCount, int availableCount, int writtenCount)
    {
        return new ProbeCatalogCopyResult(ProbeCatalogStatus.Success, requiredCount, availableCount, writtenCount);
    }

    public static ProbeCatalogCopyResult Fail(ProbeCatalogStatus status, int requiredCount, int availableCount, int writtenCount)
    {
        return new ProbeCatalogCopyResult(status, requiredCount, availableCount, writtenCount);
    }
}
#endif
