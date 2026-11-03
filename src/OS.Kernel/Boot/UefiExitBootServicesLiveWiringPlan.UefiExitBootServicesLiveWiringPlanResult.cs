using OS.Kernel.Diagnostics;

namespace OS.Boot;

public readonly struct UefiExitBootServicesLiveWiringPlanResult
{
    private UefiExitBootServicesLiveWiringPlanResult(UefiExitBootServicesLiveWiringStatus status, UefiExitBootServicesLiveWiringStep nextStep, bool freshMemoryMapCaptured, SerialDiagnosticsGuardStatus diagnosticsStatus, UefiExitBootServicesPlanStatus exitBootServicesPlanStatus, bool firmwareCallEnabled)
    {
        Status = status;
        NextStep = nextStep;
        FreshMemoryMapCaptured = freshMemoryMapCaptured;
        DiagnosticsStatus = diagnosticsStatus;
        ExitBootServicesPlanStatus = exitBootServicesPlanStatus;
        FirmwareCallEnabled = firmwareCallEnabled;
    }

    public UefiExitBootServicesLiveWiringStatus Status
    {
        get;
    }
    public UefiExitBootServicesLiveWiringStep NextStep
    {
        get;
    }
    public bool FreshMemoryMapCaptured
    {
        get;
    }
    public SerialDiagnosticsGuardStatus DiagnosticsStatus
    {
        get;
    }
    public UefiExitBootServicesPlanStatus ExitBootServicesPlanStatus
    {
        get;
    }
    public bool FirmwareCallEnabled
    {
        get;
    }
    public bool ReadyToCallFirmware => Status == UefiExitBootServicesLiveWiringStatus.ReadyToCallFirmware;

    public static UefiExitBootServicesLiveWiringPlanResult Create(UefiExitBootServicesLiveWiringStatus status, UefiExitBootServicesLiveWiringStep nextStep, bool freshMemoryMapCaptured, SerialDiagnosticsGuardStatus diagnosticsStatus, UefiExitBootServicesPlanStatus exitBootServicesPlanStatus, bool firmwareCallEnabled)
    {
        return new UefiExitBootServicesLiveWiringPlanResult(status, nextStep, freshMemoryMapCaptured, diagnosticsStatus, exitBootServicesPlanStatus, firmwareCallEnabled);
    }
}
