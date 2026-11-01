namespace OS.Kernel.Platform;
public readonly struct PlatformConsoleResult
{
    public const int MaximumLineLength = 256;
    private readonly string? _operation;
    private readonly string? _message;
    private PlatformConsoleResult(bool succeeded, PlatformConsoleStatus status, FirmwareServicesMode mode, string operation, int characterCount, string message)
    {
        Succeeded = succeeded;
        Status = status;
        Mode = mode;
        _operation = NormalizeOperation(operation);
        CharacterCount = characterCount;
        _message = NormalizeMessage(message);
    }

    public bool Succeeded { get; }
    public PlatformConsoleStatus Status { get; }
    public FirmwareServicesMode Mode { get; }
    public string Operation => _operation ?? PlatformConsoleOperations.OutputString;
    public int CharacterCount { get; }
    public string Message => _message ?? string.Empty;

    public static PlatformConsoleResult Written(FirmwareCallResult guardResult, string? line)
    {
        if (!guardResult.Succeeded)
        {
            return FirmwareServicesUnavailable(guardResult);
        }

        if (!IsValidLine(line))
        {
            return InvalidLine(guardResult);
        }

        return new PlatformConsoleResult(true, PlatformConsoleStatus.Success, guardResult.Mode, guardResult.Operation, line!.Length, string.Empty);
    }

    public static PlatformConsoleResult FirmwareServicesUnavailable(FirmwareCallResult guardResult)
    {
        return new PlatformConsoleResult(false, PlatformConsoleStatus.FirmwareServicesUnavailable, guardResult.Mode, guardResult.Operation, 0, guardResult.Reason);
    }

    public static PlatformConsoleResult InvalidLine(FirmwareCallResult guardResult)
    {
        return new PlatformConsoleResult(false, PlatformConsoleStatus.InvalidLine, guardResult.Mode, guardResult.Operation, 0, "Console marker lines must be non-empty printable ASCII without CR/LF or leading/trailing whitespace.");
    }

    public static PlatformConsoleResult SinkUnavailable(FirmwareCallResult guardResult, string? line)
    {
        if (!guardResult.Succeeded)
        {
            return FirmwareServicesUnavailable(guardResult);
        }

        if (!IsValidLine(line))
        {
            return InvalidLine(guardResult);
        }

        return new PlatformConsoleResult(false, PlatformConsoleStatus.SinkUnavailable, guardResult.Mode, guardResult.Operation, 0, "Firmware console sink is unavailable.");
    }

    private static bool IsValidLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line) || line.Length > MaximumLineLength)
        {
            return false;
        }

        if (char.IsWhiteSpace(line[0]) || char.IsWhiteSpace(line[line.Length - 1]))
        {
            return false;
        }

        for (int index = 0; index < line.Length; index++)
        {
            char value = line[index];
            if (value < 0x20 || value > 0x7e)
            {
                return false;
            }
        }

        return true;
    }

    private static string NormalizeOperation(string operation)
    {
        return string.IsNullOrWhiteSpace(operation) ? PlatformConsoleOperations.OutputString : operation;
    }

    private static string NormalizeMessage(string message)
    {
        return string.IsNullOrWhiteSpace(message) ? string.Empty : message;
    }
}
