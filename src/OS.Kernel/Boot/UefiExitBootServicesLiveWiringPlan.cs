using OS.Kernel.Diagnostics;

namespace OS.Boot;
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

    public static UefiExitBootServicesLiveWiringPlanResult Evaluate(bool freshMemoryMapCaptured, SerialDiagnosticsGuardResult postFirmwareDiagnostics, UefiExitBootServicesPlanResult exitBootServicesPlan, bool firmwareCallEnabled)
    {
        if (!freshMemoryMapCaptured)
        {
            return UefiExitBootServicesLiveWiringPlanResult.Create(UefiExitBootServicesLiveWiringStatus.FreshMemoryMapMissing, UefiExitBootServicesLiveWiringStep.CaptureFreshMemoryMap, freshMemoryMapCaptured, postFirmwareDiagnostics.Status, exitBootServicesPlan.Status, firmwareCallEnabled);
        }

        if (!postFirmwareDiagnostics.PostFirmwareDiagnosticsAvailable)
        {
            return UefiExitBootServicesLiveWiringPlanResult.Create(UefiExitBootServicesLiveWiringStatus.PostFirmwareDiagnosticsUnavailable, UefiExitBootServicesLiveWiringStep.ConfirmOwnedSerialDiagnostics, freshMemoryMapCaptured, postFirmwareDiagnostics.Status, exitBootServicesPlan.Status, firmwareCallEnabled);
        }

        if (exitBootServicesPlan.Status != UefiExitBootServicesPlanStatus.Success)
        {
            return UefiExitBootServicesLiveWiringPlanResult.Create(UefiExitBootServicesLiveWiringStatus.ExitBootServicesPlanBlocked, UefiExitBootServicesLiveWiringStep.ValidateExitBootServicesPlan, freshMemoryMapCaptured, postFirmwareDiagnostics.Status, exitBootServicesPlan.Status, firmwareCallEnabled);
        }

        if (!firmwareCallEnabled)
        {
            return UefiExitBootServicesLiveWiringPlanResult.Create(UefiExitBootServicesLiveWiringStatus.FirmwareCallDisabled, UefiExitBootServicesLiveWiringStep.CallFirmwareExitBootServices, freshMemoryMapCaptured, postFirmwareDiagnostics.Status, exitBootServicesPlan.Status, firmwareCallEnabled);
        }

        return UefiExitBootServicesLiveWiringPlanResult.Create(UefiExitBootServicesLiveWiringStatus.ReadyToCallFirmware, UefiExitBootServicesLiveWiringStep.CallFirmwareExitBootServices, freshMemoryMapCaptured, postFirmwareDiagnostics.Status, exitBootServicesPlan.Status, firmwareCallEnabled);
    }
}
