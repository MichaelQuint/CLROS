using OS.Kernel.Diagnostics;

namespace OS.Boot;

public enum UefiExitBootServicesLiveWiringStep
{
    CaptureFreshMemoryMap = 0,
    ConfirmOwnedSerialDiagnostics = 1,
    EmitPostFirmwareDiagnosticsReady = 2,
    ValidateExitBootServicesPlan = 3,
    CallFirmwareExitBootServices = 4,
    MarkFirmwareServicesExited = 5,
    RejectFirmwareBackedCalls = 6
}

public enum UefiExitBootServicesLiveWiringStatus
{
    ReadyToCallFirmware = 0,
    FreshMemoryMapMissing = 1,
    PostFirmwareDiagnosticsUnavailable = 2,
    ExitBootServicesPlanBlocked = 3,
    FirmwareCallDisabled = 4
}

public readonly struct UefiExitBootServicesLiveWiringPlanResult
{
    private UefiExitBootServicesLiveWiringPlanResult(
        UefiExitBootServicesLiveWiringStatus status,
        UefiExitBootServicesLiveWiringStep nextStep,
        bool freshMemoryMapCaptured,
        SerialDiagnosticsGuardStatus diagnosticsStatus,
        UefiExitBootServicesPlanStatus exitBootServicesPlanStatus,
        bool firmwareCallEnabled)
    {
        Status = status;
        NextStep = nextStep;
        FreshMemoryMapCaptured = freshMemoryMapCaptured;
        DiagnosticsStatus = diagnosticsStatus;
        ExitBootServicesPlanStatus = exitBootServicesPlanStatus;
        FirmwareCallEnabled = firmwareCallEnabled;
    }

    public UefiExitBootServicesLiveWiringStatus Status { get; }

    public UefiExitBootServicesLiveWiringStep NextStep { get; }

    public bool FreshMemoryMapCaptured { get; }

    public SerialDiagnosticsGuardStatus DiagnosticsStatus { get; }

    public UefiExitBootServicesPlanStatus ExitBootServicesPlanStatus { get; }

    public bool FirmwareCallEnabled { get; }

    public bool ReadyToCallFirmware => Status == UefiExitBootServicesLiveWiringStatus.ReadyToCallFirmware;

    public static UefiExitBootServicesLiveWiringPlanResult Create(
        UefiExitBootServicesLiveWiringStatus status,
        UefiExitBootServicesLiveWiringStep nextStep,
        bool freshMemoryMapCaptured,
        SerialDiagnosticsGuardStatus diagnosticsStatus,
        UefiExitBootServicesPlanStatus exitBootServicesPlanStatus,
        bool firmwareCallEnabled)
    {
        return new UefiExitBootServicesLiveWiringPlanResult(
            status,
            nextStep,
            freshMemoryMapCaptured,
            diagnosticsStatus,
            exitBootServicesPlanStatus,
            firmwareCallEnabled);
    }
}

public static class UefiExitBootServicesLiveWiringPlan
{
    public const int StepCount = 7;

    public static UefiExitBootServicesLiveWiringStep GetStep(int index)
    {
        return index switch
        {
            0 => UefiExitBootServicesLiveWiringStep.CaptureFreshMemoryMap,
            1 => UefiExitBootServicesLiveWiringStep.ConfirmOwnedSerialDiagnostics,
            2 => UefiExitBootServicesLiveWiringStep.EmitPostFirmwareDiagnosticsReady,
            3 => UefiExitBootServicesLiveWiringStep.ValidateExitBootServicesPlan,
            4 => UefiExitBootServicesLiveWiringStep.CallFirmwareExitBootServices,
            5 => UefiExitBootServicesLiveWiringStep.MarkFirmwareServicesExited,
            6 => UefiExitBootServicesLiveWiringStep.RejectFirmwareBackedCalls,
            _ => UefiExitBootServicesLiveWiringStep.CaptureFreshMemoryMap
        };
    }

    public static string GetStepName(int index)
    {
        return GetStep(index).ToString();
    }

    public static UefiExitBootServicesLiveWiringPlanResult Evaluate(
        bool freshMemoryMapCaptured,
        SerialDiagnosticsGuardResult postFirmwareDiagnostics,
        UefiExitBootServicesPlanResult exitBootServicesPlan,
        bool firmwareCallEnabled)
    {
        if (!freshMemoryMapCaptured)
        {
            return UefiExitBootServicesLiveWiringPlanResult.Create(
                UefiExitBootServicesLiveWiringStatus.FreshMemoryMapMissing,
                UefiExitBootServicesLiveWiringStep.CaptureFreshMemoryMap,
                freshMemoryMapCaptured,
                postFirmwareDiagnostics.Status,
                exitBootServicesPlan.Status,
                firmwareCallEnabled);
        }

        if (!postFirmwareDiagnostics.PostFirmwareDiagnosticsAvailable)
        {
            return UefiExitBootServicesLiveWiringPlanResult.Create(
                UefiExitBootServicesLiveWiringStatus.PostFirmwareDiagnosticsUnavailable,
                UefiExitBootServicesLiveWiringStep.ConfirmOwnedSerialDiagnostics,
                freshMemoryMapCaptured,
                postFirmwareDiagnostics.Status,
                exitBootServicesPlan.Status,
                firmwareCallEnabled);
        }

        if (exitBootServicesPlan.Status != UefiExitBootServicesPlanStatus.Success)
        {
            return UefiExitBootServicesLiveWiringPlanResult.Create(
                UefiExitBootServicesLiveWiringStatus.ExitBootServicesPlanBlocked,
                UefiExitBootServicesLiveWiringStep.ValidateExitBootServicesPlan,
                freshMemoryMapCaptured,
                postFirmwareDiagnostics.Status,
                exitBootServicesPlan.Status,
                firmwareCallEnabled);
        }

        if (!firmwareCallEnabled)
        {
            return UefiExitBootServicesLiveWiringPlanResult.Create(
                UefiExitBootServicesLiveWiringStatus.FirmwareCallDisabled,
                UefiExitBootServicesLiveWiringStep.CallFirmwareExitBootServices,
                freshMemoryMapCaptured,
                postFirmwareDiagnostics.Status,
                exitBootServicesPlan.Status,
                firmwareCallEnabled);
        }

        return UefiExitBootServicesLiveWiringPlanResult.Create(
            UefiExitBootServicesLiveWiringStatus.ReadyToCallFirmware,
            UefiExitBootServicesLiveWiringStep.CallFirmwareExitBootServices,
            freshMemoryMapCaptured,
            postFirmwareDiagnostics.Status,
            exitBootServicesPlan.Status,
            firmwareCallEnabled);
    }
}
