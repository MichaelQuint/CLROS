namespace OS.Kernel.Diagnostics;

public readonly struct PanicSafeOwnedCom1ReportResult
{
    private PanicSafeOwnedCom1ReportResult(bool isInitialized, PanicSafeOwnedCom1ReportStatus status, PanicSafeOwnedCom1FormatResult format, Com1OwnedPortIoConfigurationResult configuration, Com1OwnedPortIoOperationResult lastOperation, int bytesWritten, int lineStatusPolls, int failedByteIndex, bool ownedBackendUsed, bool firmwareFallbackUsed)
    {
        IsInitialized = isInitialized;
        Status = status;
        Format = format;
        Configuration = configuration;
        LastOperation = lastOperation;
        BytesWritten = bytesWritten;
        LineStatusPolls = lineStatusPolls;
        FailedByteIndex = failedByteIndex;
        OwnedBackendUsed = ownedBackendUsed;
        FirmwareFallbackUsed = firmwareFallbackUsed;
    }

    public bool IsInitialized
    {
        get;
    }
    public PanicSafeOwnedCom1ReportStatus Status
    {
        get;
    }
    public PanicSafeOwnedCom1FormatResult Format
    {
        get;
    }
    public Com1OwnedPortIoConfigurationResult Configuration
    {
        get;
    }
    public Com1OwnedPortIoOperationResult LastOperation
    {
        get;
    }
    public int BytesFormatted => Format.BytesWritten;
    public int BytesWritten
    {
        get;
    }
    public int LineStatusPolls
    {
        get;
    }
    public int FailedByteIndex
    {
        get;
    }
    public bool OwnedBackendUsed
    {
        get;
    }
    public bool FirmwareFallbackUsed
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == PanicSafeOwnedCom1ReportStatus.Success && Format.Succeeded && Configuration.Succeeded && Configuration.WritesCompleted == Com1OwnedPortIoBackend.RequiredConfigurationWriteCount && LastOperation.Succeeded && LastOperation.OperationKind == PortIoOperationKind.Out8 && LastOperation.Port == Com1OwnedPortIoBackend.DataRegisterPort && LastOperation.Value == PanicSafeFormattingSerialReportingOwnedCom1.LineFeed && BytesFormatted > 0 && BytesWritten == BytesFormatted && LineStatusPolls >= BytesWritten && FailedByteIndex == -1 && OwnedBackendUsed && !FirmwareFallbackUsed;

    internal static PanicSafeOwnedCom1ReportResult Pass(PanicSafeOwnedCom1FormatResult format, Com1OwnedPortIoConfigurationResult configuration, Com1OwnedPortIoOperationResult lastOperation, int lineStatusPolls)
    {
        return new PanicSafeOwnedCom1ReportResult(true, PanicSafeOwnedCom1ReportStatus.Success, format, configuration, lastOperation, format.BytesWritten, lineStatusPolls, -1, true, false);
    }

    internal static PanicSafeOwnedCom1ReportResult Fail(PanicSafeOwnedCom1ReportStatus status, PanicSafeOwnedCom1FormatResult format, Com1OwnedPortIoConfigurationResult configuration, Com1OwnedPortIoOperationResult lastOperation, int bytesWritten, int lineStatusPolls, int failedByteIndex, bool ownedBackendUsed)
    {
        return new PanicSafeOwnedCom1ReportResult(true, status, format, configuration, lastOperation, bytesWritten, lineStatusPolls, failedByteIndex, ownedBackendUsed, false);
    }
}
#if !NOSTDLIB_EFI
#endif
