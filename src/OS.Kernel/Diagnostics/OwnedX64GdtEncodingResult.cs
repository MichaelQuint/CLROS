namespace OS.Kernel.Diagnostics;

public readonly struct OwnedX64GdtEncodingResult
{
    private OwnedX64GdtEncodingResult(bool isInitialized, OwnedX64GdtEncodingStatus status, int bytesWritten, int requiredBytes, ushort tableLimit, ulong fingerprint, bool destinationMutated, OwnedX64GdtVerificationResult verification)
    {
        IsInitialized = isInitialized;
        Status = status;
        BytesWritten = bytesWritten;
        RequiredBytes = requiredBytes;
        TableLimit = tableLimit;
        Fingerprint = fingerprint;
        DestinationMutated = destinationMutated;
        Verification = verification;
    }

    public bool IsInitialized
    {
        get;
    }
    public OwnedX64GdtEncodingStatus Status
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public ushort TableLimit
    {
        get;
    }
    public ulong Fingerprint
    {
        get;
    }
    public bool DestinationMutated
    {
        get;
    }
    public OwnedX64GdtVerificationResult Verification
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == OwnedX64GdtEncodingStatus.Success && BytesWritten == OwnedX64GdtContainingNullN64BitCode.RequiredByteCount && RequiredBytes == OwnedX64GdtContainingNullN64BitCode.RequiredByteCount && TableLimit == OwnedX64GdtContainingNullN64BitCode.TableLimit && DestinationMutated && Verification.Succeeded && Verification.ObservedFingerprint == Fingerprint;

    internal static OwnedX64GdtEncodingResult Pass(ulong fingerprint, OwnedX64GdtVerificationResult verification)
    {
        return new OwnedX64GdtEncodingResult(true, OwnedX64GdtEncodingStatus.Success, OwnedX64GdtContainingNullN64BitCode.RequiredByteCount, OwnedX64GdtContainingNullN64BitCode.RequiredByteCount, OwnedX64GdtContainingNullN64BitCode.TableLimit, fingerprint, true, verification);
    }

    internal static OwnedX64GdtEncodingResult Fail(OwnedX64GdtEncodingStatus status, int bytesWritten, bool destinationMutated, OwnedX64GdtVerificationResult verification)
    {
        return new OwnedX64GdtEncodingResult(true, status, bytesWritten, OwnedX64GdtContainingNullN64BitCode.RequiredByteCount, OwnedX64GdtContainingNullN64BitCode.TableLimit, 0, destinationMutated, verification);
    }
}
#if !NOSTDLIB_EFI
#endif
