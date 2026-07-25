namespace OS.Kernel.Diagnostics;

public enum SerialDiagnosticsGuardStatus
{
    FirmwareConsoleOnly = 0,
    OwnedSerialReady = 1,
    FirmwareServicesUnavailable = 2,
    SerialUnavailable = 3,
    InvalidTransition = 4
}

public readonly struct SerialDiagnosticsGuardResult
{
    private SerialDiagnosticsGuardResult(
        bool succeeded,
        SerialDiagnosticsGuardStatus status,
        string operation,
        string reason)
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

    public static SerialDiagnosticsGuardResult Denied(
        string operation,
        SerialDiagnosticsGuardStatus status,
        string reason)
    {
        return new SerialDiagnosticsGuardResult(false, status, Normalize(operation), Normalize(reason));
    }

    public static SerialDiagnosticsGuardResult Invalid(string operation, SerialDiagnosticsGuardStatus currentStatus)
    {
        return new SerialDiagnosticsGuardResult(
            false,
            SerialDiagnosticsGuardStatus.InvalidTransition,
            Normalize(operation),
            "invalid serial diagnostics transition from " + currentStatus + ".");
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrEmpty(value) ? "unknown" : value;
    }
}

public sealed class SerialDiagnosticsGuard
{
    public const string MarkOwnedSerialReadyOperation = "serial.owned.ready";

    public const string MarkOwnedSerialReadyFromCom1Operation = "serial.owned.com1.ready";

    public const string MarkFirmwareServicesUnavailableOperation = "firmware.services.unavailable";

    public const string MarkFirmwareConsoleUnavailableOperation = "firmware.console.unavailable";

    private bool _firmwareConsoleAvailable;
    private bool _firmwareServicesUnavailable;
    private bool _ownedSerialReady;

    public SerialDiagnosticsGuard()
        : this(firmwareConsoleAvailable: true)
    {
    }

    private SerialDiagnosticsGuard(bool firmwareConsoleAvailable)
    {
        _firmwareConsoleAvailable = firmwareConsoleAvailable;
    }

    public SerialDiagnosticsGuardStatus Status => GetCurrentStatus();

    public bool FirmwareConsoleAvailable => _firmwareConsoleAvailable && !_firmwareServicesUnavailable;

    public bool FirmwareServicesUnavailable => _firmwareServicesUnavailable;

    public bool OwnedSerialReady => _ownedSerialReady;

    public bool CanUsePostFirmwareDiagnostics => _ownedSerialReady;

    public static SerialDiagnosticsGuard CreateFirmwareConsoleOnly()
    {
        return new SerialDiagnosticsGuard(firmwareConsoleAvailable: true);
    }

    public static SerialDiagnosticsGuard CreateSerialUnavailable()
    {
        return new SerialDiagnosticsGuard(firmwareConsoleAvailable: false);
    }

    public SerialDiagnosticsGuardResult MarkOwnedSerialReady()
    {
        return MarkOwnedSerialReadyCore(MarkOwnedSerialReadyOperation);
    }

    public SerialDiagnosticsGuardResult MarkOwnedSerialReady(
        OwnedSerialDiagnosticsReadinessResult readiness)
    {
        if (!readiness.Succeeded)
        {
            return SerialDiagnosticsGuardResult.Denied(
                MarkOwnedSerialReadyFromCom1Operation,
                GetCurrentStatus(),
                "owned serial diagnostics readiness failed with status " + readiness.Status + ".");
        }

        return MarkOwnedSerialReadyCore(MarkOwnedSerialReadyFromCom1Operation);
    }

    private SerialDiagnosticsGuardResult MarkOwnedSerialReadyCore(string operation)
    {
        SerialDiagnosticsGuardStatus currentStatus = GetCurrentStatus();

        if (_ownedSerialReady || _firmwareServicesUnavailable)
        {
            return SerialDiagnosticsGuardResult.Invalid(operation, currentStatus);
        }

        _ownedSerialReady = true;
        return SerialDiagnosticsGuardResult.Allowed(operation, GetCurrentStatus());
    }

    public SerialDiagnosticsGuardResult MarkFirmwareServicesUnavailable()
    {
        SerialDiagnosticsGuardStatus currentStatus = GetCurrentStatus();

        if (_firmwareServicesUnavailable)
        {
            return SerialDiagnosticsGuardResult.Invalid(MarkFirmwareServicesUnavailableOperation, currentStatus);
        }

        _firmwareServicesUnavailable = true;
        return SerialDiagnosticsGuardResult.Allowed(MarkFirmwareServicesUnavailableOperation, GetCurrentStatus());
    }

    public SerialDiagnosticsGuardResult MarkFirmwareConsoleUnavailable()
    {
        SerialDiagnosticsGuardStatus currentStatus = GetCurrentStatus();

        if (!_firmwareConsoleAvailable)
        {
            return SerialDiagnosticsGuardResult.Invalid(MarkFirmwareConsoleUnavailableOperation, currentStatus);
        }

        _firmwareConsoleAvailable = false;
        return SerialDiagnosticsGuardResult.Allowed(MarkFirmwareConsoleUnavailableOperation, GetCurrentStatus());
    }

    public SerialDiagnosticsGuardResult RequirePostFirmwareDiagnostics(string operation)
    {
        SerialDiagnosticsGuardStatus currentStatus = GetCurrentStatus();

        if (currentStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady)
        {
            return SerialDiagnosticsGuardResult.Allowed(operation, currentStatus);
        }

        if (currentStatus == SerialDiagnosticsGuardStatus.SerialUnavailable)
        {
            return SerialDiagnosticsGuardResult.Denied(
                operation,
                currentStatus,
                "OS-owned serial diagnostics are unavailable.");
        }

        if (currentStatus == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable)
        {
            return SerialDiagnosticsGuardResult.Denied(
                operation,
                currentStatus,
                "firmware-backed diagnostics are unavailable after firmware services shutdown and owned serial is not ready.");
        }

        return SerialDiagnosticsGuardResult.Denied(
            operation,
            currentStatus,
            "firmware console output is not safe for post-firmware diagnostics.");
    }

    private SerialDiagnosticsGuardStatus GetCurrentStatus()
    {
        if (_ownedSerialReady)
        {
            return SerialDiagnosticsGuardStatus.OwnedSerialReady;
        }

        if (_firmwareServicesUnavailable)
        {
            return SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable;
        }

        if (!_firmwareConsoleAvailable)
        {
            return SerialDiagnosticsGuardStatus.SerialUnavailable;
        }

        return SerialDiagnosticsGuardStatus.FirmwareConsoleOnly;
    }
}
