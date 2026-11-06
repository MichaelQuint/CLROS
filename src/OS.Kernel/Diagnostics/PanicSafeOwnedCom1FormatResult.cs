namespace OS.Kernel.Diagnostics;

public readonly struct PanicSafeOwnedCom1FormatResult
{
    private PanicSafeOwnedCom1FormatResult(bool isInitialized, PanicSafeOwnedCom1FormatStatus status, int requiredBytes, int bytesWritten)
    {
        IsInitialized = isInitialized;
        Status = status;
        RequiredBytes = requiredBytes;
        BytesWritten = bytesWritten;
    }

    public bool IsInitialized
    {
        get;
    }
    public PanicSafeOwnedCom1FormatStatus Status
    {
        get;
    }
    public int RequiredBytes
    {
        get;
    }
    public int BytesWritten
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == PanicSafeOwnedCom1FormatStatus.Success && RequiredBytes > 0 && BytesWritten == RequiredBytes;

    internal static PanicSafeOwnedCom1FormatResult Pass(int bytesWritten)
    {
        return new PanicSafeOwnedCom1FormatResult(true, PanicSafeOwnedCom1FormatStatus.Success, bytesWritten, bytesWritten);
    }

    internal static PanicSafeOwnedCom1FormatResult Fail(PanicSafeOwnedCom1FormatStatus status, int requiredBytes)
    {
        return new PanicSafeOwnedCom1FormatResult(true, status, requiredBytes, 0);
    }
}
