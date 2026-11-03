namespace OS.Kernel.Diagnostics;

public readonly struct NormalizeInterruptFramePageResult
{
    private readonly byte _isInitialized;
    private readonly byte _destinationMutated;
    private readonly byte _readBackPerformed;
    private NormalizeInterruptFramePageResult(NormalizeInterruptFramePageOperation operation, NormalizeInterruptFramePageStatus status, ulong modeledPageAddress, ulong commonTargetAddress, ulong pageCapacity, int bytesWritten, int bytesCompared, int firstMismatchOffset, ulong fingerprint, bool destinationMutated, bool readBackPerformed)
    {
        _isInitialized = 1;
        _destinationMutated = destinationMutated ? (byte)1 : (byte)0;
        _readBackPerformed = readBackPerformed ? (byte)1 : (byte)0;
        Operation = operation;
        Status = status;
        ModeledPageAddress = modeledPageAddress;
        CommonTargetAddress = commonTargetAddress;
        PageCapacity = pageCapacity;
        BytesWritten = bytesWritten;
        BytesCompared = bytesCompared;
        FirstMismatchOffset = firstMismatchOffset;
        HardwareErrorCodeVectorCount = NormalizeErrorCodeNonErrorCodeInterruptFrame.HardwareErrorCodeVectorCount;
        SyntheticErrorCodeVectorCount = NormalizeErrorCodeNonErrorCodeInterruptFrame.SyntheticErrorCodeVectorCount;
        Fingerprint = fingerprint;
    }

    public bool IsInitialized => _isInitialized == 1;
    public NormalizeInterruptFramePageOperation Operation
    {
        get;
    }
    public NormalizeInterruptFramePageStatus Status
    {
        get;
    }
    public ulong ModeledPageAddress
    {
        get;
    }
    public ulong CommonTargetAddress
    {
        get;
    }
    public ulong PageCapacity
    {
        get;
    }
    public int RequiredBytes => NormalizeErrorCodeNonErrorCodeInterruptFrame.PageByteCount;
    public int BytesWritten
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
    public int HardwareErrorCodeVectorCount
    {
        get;
    }
    public int SyntheticErrorCodeVectorCount
    {
        get;
    }
    public ulong Fingerprint
    {
        get;
    }
    public bool DestinationMutated => _destinationMutated == 1;
    public bool ReadBackPerformed => _readBackPerformed == 1;
    public bool Materialized => IsInitialized && Operation == NormalizeInterruptFramePageOperation.Materialize && Status == NormalizeInterruptFramePageStatus.Success && BytesWritten == RequiredBytes && BytesCompared == RequiredBytes && FirstMismatchOffset == -1 && HardwareErrorCodeVectorCount == NormalizeErrorCodeNonErrorCodeInterruptFrame.HardwareErrorCodeVectorCount && SyntheticErrorCodeVectorCount == NormalizeErrorCodeNonErrorCodeInterruptFrame.SyntheticErrorCodeVectorCount && DestinationMutated && ReadBackPerformed;
    public bool ReadBackValidated => IsInitialized && Operation == NormalizeInterruptFramePageOperation.ValidateReadBack && Status == NormalizeInterruptFramePageStatus.Success && BytesWritten == 0 && BytesCompared == RequiredBytes && FirstMismatchOffset == -1 && HardwareErrorCodeVectorCount == NormalizeErrorCodeNonErrorCodeInterruptFrame.HardwareErrorCodeVectorCount && SyntheticErrorCodeVectorCount == NormalizeErrorCodeNonErrorCodeInterruptFrame.SyntheticErrorCodeVectorCount && !DestinationMutated && ReadBackPerformed;
    public bool Succeeded => Materialized || ReadBackValidated;

    internal static NormalizeInterruptFramePageResult Pass(NormalizeInterruptFramePageOperation operation, ulong modeledPageAddress, ulong commonTargetAddress, ulong pageCapacity, int bytesWritten, ulong fingerprint, bool destinationMutated)
    {
        return new NormalizeInterruptFramePageResult(operation, NormalizeInterruptFramePageStatus.Success, modeledPageAddress, commonTargetAddress, pageCapacity, bytesWritten, NormalizeErrorCodeNonErrorCodeInterruptFrame.PageByteCount, -1, fingerprint, destinationMutated, true);
    }

    internal static NormalizeInterruptFramePageResult Fail(NormalizeInterruptFramePageOperation operation, NormalizeInterruptFramePageStatus status, ulong modeledPageAddress, ulong commonTargetAddress, ulong pageCapacity, int bytesWritten = 0, int bytesCompared = 0, int firstMismatchOffset = -1, ulong fingerprint = 0, bool destinationMutated = false, bool readBackPerformed = false)
    {
        return new NormalizeInterruptFramePageResult(operation, status, modeledPageAddress, commonTargetAddress, pageCapacity, bytesWritten, bytesCompared, firstMismatchOffset, fingerprint, destinationMutated, readBackPerformed);
    }
}
#if !NOSTDLIB_EFI
#endif
