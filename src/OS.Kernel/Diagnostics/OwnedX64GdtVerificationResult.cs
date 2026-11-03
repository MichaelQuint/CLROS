namespace OS.Kernel.Diagnostics;

public readonly struct OwnedX64GdtVerificationResult
{
    private OwnedX64GdtVerificationResult(bool isInitialized, OwnedX64GdtVerificationStatus status, int bytesCompared, int firstMismatchOffset, ulong observedFingerprint, ulong expectedFingerprint)
    {
        IsInitialized = isInitialized;
        Status = status;
        BytesCompared = bytesCompared;
        FirstMismatchOffset = firstMismatchOffset;
        ObservedFingerprint = observedFingerprint;
        ExpectedFingerprint = expectedFingerprint;
    }

    public bool IsInitialized
    {
        get;
    }
    public OwnedX64GdtVerificationStatus Status
    {
        get;
    }
    public int BytesCompared
    {
        get;
    }
    public int FirstMismatchOffset
    {
        get;
    }
    public ulong ObservedFingerprint
    {
        get;
    }
    public ulong ExpectedFingerprint
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == OwnedX64GdtVerificationStatus.Success && BytesCompared == OwnedX64GdtContainingNullN64BitCode.RequiredByteCount && FirstMismatchOffset == -1 && ObservedFingerprint == ExpectedFingerprint;

    internal static OwnedX64GdtVerificationResult Pass(ulong observedFingerprint, ulong expectedFingerprint)
    {
        return new OwnedX64GdtVerificationResult(true, OwnedX64GdtVerificationStatus.Success, OwnedX64GdtContainingNullN64BitCode.RequiredByteCount, -1, observedFingerprint, expectedFingerprint);
    }

    internal static OwnedX64GdtVerificationResult Fail(OwnedX64GdtVerificationStatus status, int bytesCompared, int firstMismatchOffset, ulong observedFingerprint, ulong expectedFingerprint)
    {
        return new OwnedX64GdtVerificationResult(true, status, bytesCompared, firstMismatchOffset, observedFingerprint, expectedFingerprint);
    }
}
