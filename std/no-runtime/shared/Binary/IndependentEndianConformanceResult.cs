namespace OS.Std.Binary;

public readonly struct IndependentEndianConformanceResult
{
    private readonly bool _isInitialized;
    private IndependentEndianConformanceResult(IndependentEndianConformanceStatus status, int problemIndex, int conflictIndex, int readVectorCount, int reverseVectorCount, ulong expectedFingerprint, ulong actualFingerprint)
    {
        _isInitialized = true;
        Status = status;
        ProblemIndex = problemIndex;
        ConflictIndex = conflictIndex;
        ReadVectorCount = readVectorCount;
        ReverseVectorCount = reverseVectorCount;
        ExpectedFingerprint = expectedFingerprint;
        ActualFingerprint = actualFingerprint;
    }

    public IndependentEndianConformanceStatus Status
    {
        get;
    }
    public int ProblemIndex
    {
        get;
    }
    public int ConflictIndex
    {
        get;
    }
    public int ReadVectorCount
    {
        get;
    }
    public int ReverseVectorCount
    {
        get;
    }
    public ulong ExpectedFingerprint
    {
        get;
    }
    public ulong ActualFingerprint
    {
        get;
    }
    public bool IsInitialized => _isInitialized;
    public bool Succeeded => _isInitialized && Status == IndependentEndianConformanceStatus.Success && ReadVectorCount == IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa.ReadVectorCount && ReverseVectorCount == IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa.ReverseVectorCount && ExpectedFingerprint == ActualFingerprint && ActualFingerprint == IndependentEndianBinaryPrimitivesConformanceVectorsDerivedMosa.ExpectedCatalogFingerprint;
    public bool SatisfiesRuntimeCompletionGate4 => false;

    internal static IndependentEndianConformanceResult Success(int readVectorCount, int reverseVectorCount, ulong fingerprint)
    {
        return new IndependentEndianConformanceResult(IndependentEndianConformanceStatus.Success, problemIndex: -1, conflictIndex: -1, readVectorCount, reverseVectorCount, fingerprint, fingerprint);
    }

    internal static IndependentEndianConformanceResult Failure(IndependentEndianConformanceStatus status, int problemIndex, int conflictIndex, int readVectorCount, int reverseVectorCount, ulong expectedFingerprint, ulong actualFingerprint)
    {
        return new IndependentEndianConformanceResult(status, problemIndex, conflictIndex, readVectorCount, reverseVectorCount, expectedFingerprint, actualFingerprint);
    }
}
#if !NOSTDLIB_EFI
#else

#endif
#if NOSTDLIB_EFI && M04_INDEPENDENT_ENDIAN_CONFORMANCE_PROBE && !M01_OWNED_PAGING_PROBE

#endif
