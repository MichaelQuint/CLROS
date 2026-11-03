namespace OS.Std.Memory;

public readonly struct SpanMemoryExecutionResult
{
    private readonly bool _isInitialized;
    private SpanMemoryExecutionResult(SpanMemoryExecutionStatus status, SpanMemoryConformanceResult catalogValidation, int requiredScratchBytes, int availableScratchBytes, int requiredOutputCount, int availableOutputCount, int executedVectorCount, int problemIndex, ulong catalogFingerprint)
    {
        _isInitialized = true;
        Status = status;
        CatalogValidation = catalogValidation;
        RequiredScratchBytes = requiredScratchBytes;
        AvailableScratchBytes = availableScratchBytes;
        RequiredOutputCount = requiredOutputCount;
        AvailableOutputCount = availableOutputCount;
        ExecutedVectorCount = executedVectorCount;
        ProblemIndex = problemIndex;
        CatalogFingerprint = catalogFingerprint;
    }

    public SpanMemoryExecutionStatus Status
    {
        get;
    }
    public SpanMemoryConformanceResult CatalogValidation
    {
        get;
    }
    public int RequiredScratchBytes
    {
        get;
    }
    public int AvailableScratchBytes
    {
        get;
    }
    public int RequiredOutputCount
    {
        get;
    }
    public int AvailableOutputCount
    {
        get;
    }
    public int ExecutedVectorCount
    {
        get;
    }
    public int ProblemIndex
    {
        get;
    }
    public ulong CatalogFingerprint
    {
        get;
    }
    public bool IsInitialized => _isInitialized;
    public bool Succeeded => _isInitialized && Status == SpanMemoryExecutionStatus.Success && CatalogValidation.Succeeded && RequiredScratchBytes == SpanOverlapMemoryCastUnsafeWidthConformanceVectors.RequiredScratchBytes && RequiredOutputCount == SpanOverlapMemoryCastUnsafeWidthConformanceVectors.RequiredExecutionRecordCount && ExecutedVectorCount == RequiredOutputCount && ProblemIndex == -1 && CatalogFingerprint == SpanOverlapMemoryCastUnsafeWidthConformanceVectors.ExpectedCatalogFingerprint;

    internal static SpanMemoryExecutionResult Success(SpanMemoryConformanceResult catalogValidation, int availableScratchBytes, int availableOutputCount)
    {
        return new SpanMemoryExecutionResult(SpanMemoryExecutionStatus.Success, catalogValidation, SpanOverlapMemoryCastUnsafeWidthConformanceVectors.RequiredScratchBytes, availableScratchBytes, SpanOverlapMemoryCastUnsafeWidthConformanceVectors.RequiredExecutionRecordCount, availableOutputCount, SpanOverlapMemoryCastUnsafeWidthConformanceVectors.TotalVectorCount, -1, catalogValidation.ActualFingerprint);
    }

    internal static SpanMemoryExecutionResult Failure(SpanMemoryExecutionStatus status, SpanMemoryConformanceResult catalogValidation, int availableScratchBytes, int availableOutputCount, int executedVectorCount, int problemIndex)
    {
        return new SpanMemoryExecutionResult(status, catalogValidation, SpanOverlapMemoryCastUnsafeWidthConformanceVectors.RequiredScratchBytes, availableScratchBytes, SpanOverlapMemoryCastUnsafeWidthConformanceVectors.RequiredExecutionRecordCount, availableOutputCount, executedVectorCount, problemIndex, catalogValidation.ActualFingerprint);
    }
}
