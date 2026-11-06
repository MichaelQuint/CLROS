namespace OS.Drivers;

public readonly struct EnaRegisterApertureValidationResult
{
    private EnaRegisterApertureValidationResult(bool isInitialized, EnaRegisterApertureValidationStatus status, ulong apertureLength, ulong requiredLength)
    {
        IsInitialized = isInitialized;
        Status = status;
        ApertureLength = apertureLength;
        RequiredLength = requiredLength;
    }

    public EnaRegisterApertureValidationStatus Status
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
    public bool Succeeded => IsInitialized && Status == EnaRegisterApertureValidationStatus.Success;

    public static EnaRegisterApertureValidationResult Success(ulong apertureLength, ulong requiredLength)
    {
        return new EnaRegisterApertureValidationResult(isInitialized: true, EnaRegisterApertureValidationStatus.Success, apertureLength, requiredLength);
    }

    public static EnaRegisterApertureValidationResult Fail(EnaRegisterApertureValidationStatus status, ulong apertureLength, ulong requiredLength)
    {
        return new EnaRegisterApertureValidationResult(isInitialized: true, status, apertureLength, requiredLength);
    }
}
