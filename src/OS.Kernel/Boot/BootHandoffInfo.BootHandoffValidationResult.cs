using OS.Kernel.Platform;

namespace OS.Boot;

public readonly struct BootHandoffValidationResult
{
    private readonly string? _message;
    private BootHandoffValidationResult(bool succeeded, BootHandoffStatus status, string message)
    {
        Succeeded = succeeded;
        Status = status;
        _message = message;
    }

    public bool Succeeded
    {
        get;
    }
    public BootHandoffStatus Status
    {
        get;
    }
    public string Message => _message ?? string.Empty;

    public static BootHandoffValidationResult Success()
    {
        return new BootHandoffValidationResult(true, BootHandoffStatus.Success, string.Empty);
    }

    public static BootHandoffValidationResult Invalid(BootHandoffStatus status, string message)
    {
        return new BootHandoffValidationResult(false, status, message);
    }
}
