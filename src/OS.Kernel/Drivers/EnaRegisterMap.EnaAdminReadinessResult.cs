namespace OS.Drivers;
public readonly struct EnaAdminReadinessResult
{
    private EnaAdminReadinessResult(bool isInitialized, EnaAdminReadinessStatus status, EnaRegisterApertureValidationResult apertureValidation, uint capabilities, uint deviceStatus)
    {
        IsInitialized = isInitialized;
        Status = status;
        ApertureValidation = apertureValidation;
        Capabilities = capabilities;
        DeviceStatus = deviceStatus;
    }

    public EnaAdminReadinessStatus Status { get; }
    public EnaRegisterApertureValidationResult ApertureValidation { get; }
    public uint Capabilities { get; }
    public uint DeviceStatus { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == EnaAdminReadinessStatus.Success;
    public bool RequiresContiguousQueueMemory => EnaRegisterMap.Capabilities.RequiresContiguousQueueMemory(Capabilities);
    public uint ResetTimeoutEncoded => EnaRegisterMap.Capabilities.GetResetTimeoutEncoded(Capabilities);
    public uint DmaAddressWidthBits => EnaRegisterMap.Capabilities.GetDmaAddressWidthBits(Capabilities);
    public uint AdminCommandTimeoutEncoded => EnaRegisterMap.Capabilities.GetAdminCommandTimeoutEncoded(Capabilities);
    public bool DeviceReady => EnaRegisterMap.DeviceStatus.IsReady(DeviceStatus);
    public bool DeviceFatalError => EnaRegisterMap.DeviceStatus.HasFatalError(DeviceStatus);
    public bool DeviceResetInProgress => EnaRegisterMap.DeviceStatus.IsResetInProgress(DeviceStatus);
    public bool AdminQueueRestartInProgress => EnaRegisterMap.DeviceStatus.IsAdminQueueRestartInProgress(DeviceStatus);
    public bool DeviceQuiescentStateInProgress => EnaRegisterMap.DeviceStatus.IsQuiescentStateInProgress(DeviceStatus);

    public static EnaAdminReadinessResult Success(EnaRegisterApertureValidationResult apertureValidation, uint capabilities, uint deviceStatus)
    {
        return new EnaAdminReadinessResult(isInitialized: true, EnaAdminReadinessStatus.Success, apertureValidation, capabilities, deviceStatus);
    }

    public static EnaAdminReadinessResult Fail(EnaAdminReadinessStatus status, EnaRegisterApertureValidationResult apertureValidation, uint capabilities, uint deviceStatus)
    {
        return new EnaAdminReadinessResult(isInitialized: true, status, apertureValidation, capabilities, deviceStatus);
    }
}
