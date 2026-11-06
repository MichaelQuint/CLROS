namespace OS.Drivers;

public readonly struct EnaPciCandidateValidationResult
{
    private EnaPciCandidateValidationResult(bool isInitialized, EnaPciCandidateValidationStatus status, EnaPciDeviceKind deviceKind, PciEcamScannedDevice device)
    {
        IsInitialized = isInitialized;
        Status = status;
        DeviceKind = deviceKind;
        Device = device;
    }

    public EnaPciCandidateValidationStatus Status
    {
        get;
    }
    public EnaPciDeviceKind DeviceKind
    {
        get;
    }
    public PciEcamScannedDevice Device
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == EnaPciCandidateValidationStatus.Success;

    public static EnaPciCandidateValidationResult Success(EnaPciDeviceKind deviceKind, PciEcamScannedDevice device)
    {
        return new EnaPciCandidateValidationResult(isInitialized: true, EnaPciCandidateValidationStatus.Success, deviceKind, device);
    }

    public static EnaPciCandidateValidationResult Fail(EnaPciCandidateValidationStatus status, EnaPciDeviceKind deviceKind, PciEcamScannedDevice device)
    {
        return new EnaPciCandidateValidationResult(isInitialized: true, status, deviceKind, device);
    }
}
