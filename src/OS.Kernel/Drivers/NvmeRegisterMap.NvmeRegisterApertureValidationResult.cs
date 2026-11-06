namespace OS.Drivers;

public readonly struct NvmeRegisterApertureValidationResult
{
    private NvmeRegisterApertureValidationResult(bool isInitialized, NvmeRegisterApertureValidationStatus status, ulong apertureLength, ulong requiredLength)
    {
        IsInitialized = isInitialized;
        Status = status;
        ApertureLength = apertureLength;
        RequiredLength = requiredLength;
    }

    public NvmeRegisterApertureValidationStatus Status
    {
        get;
    }
    public ulong ApertureLength
    {
        get;
    }
    public ulong RequiredLength
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == NvmeRegisterApertureValidationStatus.Success;

    public static NvmeRegisterApertureValidationResult Success(ulong apertureLength, ulong requiredLength)
    {
        return new NvmeRegisterApertureValidationResult(isInitialized: true, NvmeRegisterApertureValidationStatus.Success, apertureLength, requiredLength);
    }

    public static NvmeRegisterApertureValidationResult Fail(NvmeRegisterApertureValidationStatus status, ulong apertureLength, ulong requiredLength)
    {
        return new NvmeRegisterApertureValidationResult(isInitialized: true, status, apertureLength, requiredLength);
    }
}
