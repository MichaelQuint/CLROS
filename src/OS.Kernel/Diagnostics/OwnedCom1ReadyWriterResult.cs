namespace OS.Kernel.Diagnostics;

public readonly struct OwnedCom1ReadyWriterResult
{
    private OwnedCom1ReadyWriterResult(bool isInitialized, OwnedCom1ReadyWriterStatus status, Com1OwnedPortIoConfigurationResult configuration, Com1OwnedPortIoOperationResult lastOperation, int markerBytesWritten, int lineEndingBytesWritten, int lineStatusPolls, int failedByteIndex, bool ownedBackendUsed, bool firmwareFallbackUsed)
    {
        IsInitialized = isInitialized;
        Status = status;
        Configuration = configuration;
        LastOperation = lastOperation;
        MarkerBytesWritten = markerBytesWritten;
        LineEndingBytesWritten = lineEndingBytesWritten;
        LineStatusPolls = lineStatusPolls;
        FailedByteIndex = failedByteIndex;
        OwnedBackendUsed = ownedBackendUsed;
        FirmwareFallbackUsed = firmwareFallbackUsed;
    }

    public bool IsInitialized
    {
        get;
    }
    public OwnedCom1ReadyWriterStatus Status
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
    public int MarkerBytesWritten
    {
        get;
    }
    public int LineEndingBytesWritten
    {
        get;
    }
    public int SerialBytesWritten => MarkerBytesWritten + LineEndingBytesWritten;
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
    public bool Succeeded => IsInitialized && Status == OwnedCom1ReadyWriterStatus.Success && Configuration.Succeeded && Configuration.WritesCompleted == Com1OwnedPortIoBackend.RequiredConfigurationWriteCount && LastOperation.Succeeded && LastOperation.OperationKind == PortIoOperationKind.Out8 && LastOperation.Port == Com1OwnedPortIoBackend.DataRegisterPort && LastOperation.Value == OwnedCom1ReadyExclusivelyOwnedWriter.LineFeed && MarkerBytesWritten == OwnedCom1ReadyExclusivelyOwnedWriter.MarkerByteCount && LineEndingBytesWritten == OwnedCom1ReadyExclusivelyOwnedWriter.LineEndingByteCount && SerialBytesWritten == OwnedCom1ReadyExclusivelyOwnedWriter.RequiredSerialByteCount && LineStatusPolls >= SerialBytesWritten && FailedByteIndex == -1 && OwnedBackendUsed && !FirmwareFallbackUsed;

    internal static OwnedCom1ReadyWriterResult Pass(Com1OwnedPortIoConfigurationResult configuration, Com1OwnedPortIoOperationResult lastOperation, int lineStatusPolls)
    {
        return new OwnedCom1ReadyWriterResult(true, OwnedCom1ReadyWriterStatus.Success, configuration, lastOperation, OwnedCom1ReadyExclusivelyOwnedWriter.MarkerByteCount, OwnedCom1ReadyExclusivelyOwnedWriter.LineEndingByteCount, lineStatusPolls, -1, true, false);
    }

    internal static OwnedCom1ReadyWriterResult Fail(OwnedCom1ReadyWriterStatus status, Com1OwnedPortIoConfigurationResult configuration, Com1OwnedPortIoOperationResult lastOperation, int markerBytesWritten, int lineEndingBytesWritten, int lineStatusPolls, int failedByteIndex, bool ownedBackendUsed)
    {
        return new OwnedCom1ReadyWriterResult(true, status, configuration, lastOperation, markerBytesWritten, lineEndingBytesWritten, lineStatusPolls, failedByteIndex, ownedBackendUsed, false);
    }
}
