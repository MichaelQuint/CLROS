using System.Globalization;
using System.Text.Json;
using OS.Kernel.Build;

public sealed class ValidationResult
{
    private ValidationResult(bool succeeded, string code, string message)
    {
        this.Succeeded = succeeded;
        this.Code = code;
        this.Message = message;
    }

    public bool Succeeded
    {
        get;
    }
    public string Code
    {
        get;
    }
    public string Message
    {
        get;
    }

    public static ValidationResult Pass()
    {
        return new ValidationResult(true, string.Empty, string.Empty);
    }

    public static ValidationResult Fail(string code, string message)
    {
        return new ValidationResult(false, code, message);
    }
}
