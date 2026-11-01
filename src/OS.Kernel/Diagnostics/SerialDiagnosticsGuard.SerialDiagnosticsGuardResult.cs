namespace OS.Kernel.Diagnostics;
public readonly struct SerialDiagnosticsGuardResult
{
    private SerialDiagnosticsGuardResult(bool succeeded, SerialDiagnosticsGuardStatus status, string operation, string reason)
    {
        Succeeded = succeeded;
        Status = status;
        Operation = operation;
        Reason = reason;
    }

    public bool Succeeded { get; }
    public SerialDiagnosticsGuardStatus Status { get; }
    public string Operation { get; }
    public string Reason { get; }
    public bool PostFirmwareDiagnosticsAvailable => Succeeded && Status == SerialDiagnosticsGuardStatus.OwnedSerialReady;

    public static SerialDiagnosticsGuardResult Allowed(string operation, SerialDiagnosticsGuardStatus status)
    {
        return new SerialDiagnosticsGuardResult(true, status, Normalize(operation), string.Empty);
    }

    public static SerialDiagnosticsGuardResult Denied(string operation, SerialDiagnosticsGuardStatus status, string reason)
    {
        return new SerialDiagnosticsGuardResult(false, status, Normalize(operation), Normalize(reason));
    }

    public static SerialDiagnosticsGuardResult Invalid(string operation, SerialDiagnosticsGuardStatus currentStatus)
    {
        return new SerialDiagnosticsGuardResult(false, SerialDiagnosticsGuardStatus.InvalidTransition, Normalize(operation), "invalid serial diagnostics transition from " + currentStatus + ".");
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrEmpty(value) ? "unknown" : value;
    }
}
