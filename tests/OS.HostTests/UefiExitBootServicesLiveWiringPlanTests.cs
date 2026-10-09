using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;

internal static class UefiExitBootServicesLiveWiringPlanTests
{

    [HostTest("uefi.exitboot live wiring exposes ordered steps", HostLane.Fast, 0)]
    private static bool TestExposesOrderedSteps()
    {
        return HostCheck.Result(UefiExitBootServicesLiveWiringPlan.StepCount == 7
            && UefiExitBootServicesLiveWiringPlan.GetStep(0)
                == UefiExitBootServicesLiveWiringStep.CaptureFreshMemoryMap
            && UefiExitBootServicesLiveWiringPlan.GetStep(1)
                == UefiExitBootServicesLiveWiringStep.ConfirmOwnedSerialDiagnostics
            && UefiExitBootServicesLiveWiringPlan.GetStep(2)
                == UefiExitBootServicesLiveWiringStep.EmitPostFirmwareDiagnosticsReady
            && UefiExitBootServicesLiveWiringPlan.GetStep(3)
                == UefiExitBootServicesLiveWiringStep.ValidateExitBootServicesPlan
            && UefiExitBootServicesLiveWiringPlan.GetStep(4)
                == UefiExitBootServicesLiveWiringStep.CallFirmwareExitBootServices
            && UefiExitBootServicesLiveWiringPlan.GetStep(5)
                == UefiExitBootServicesLiveWiringStep.MarkFirmwareServicesExited
            && UefiExitBootServicesLiveWiringPlan.GetStep(6)
                == UefiExitBootServicesLiveWiringStep.RejectFirmwareBackedCalls
            && UefiExitBootServicesLiveWiringPlan.GetStepName(4) == "CallFirmwareExitBootServices");
    }

    [HostTest("uefi.exitboot live wiring blocks missing diagnostics", HostLane.Fast, 1)]
    private static bool TestBlocksMissingDiagnostics()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        SerialDiagnosticsGuard diagnostics = SerialDiagnosticsGuard.CreateFirmwareConsoleOnly();
        SerialDiagnosticsGuardResult postFirmwareDiagnostics =
            diagnostics.RequirePostFirmwareDiagnostics("exitboot.live");

        UefiExitBootServicesLiveWiringPlanResult result =
            UefiExitBootServicesLiveWiringPlan.Evaluate(
                freshMemoryMapCaptured: true,
                postFirmwareDiagnostics,
                CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.Success),
                firmwareCallEnabled: false);

        return HostCheck.Result(result.Status == UefiExitBootServicesLiveWiringStatus.PostFirmwareDiagnosticsUnavailable
            && result.NextStep == UefiExitBootServicesLiveWiringStep.ConfirmOwnedSerialDiagnostics
            && result.DiagnosticsStatus == SerialDiagnosticsGuardStatus.FirmwareConsoleOnly
            && result.ExitBootServicesPlanStatus == UefiExitBootServicesPlanStatus.Success
            && !result.ReadyToCallFirmware
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable);
    }

    [HostTest("uefi.exitboot live wiring blocks non-success transition plan", HostLane.Fast, 2)]
    private static bool TestBlocksNonSuccessTransitionPlan()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        UefiExitBootServicesLiveWiringPlanResult result =
            UefiExitBootServicesLiveWiringPlan.Evaluate(
                freshMemoryMapCaptured: true,
                CreateReadyPostFirmwareDiagnosticsResult(),
                CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.RetryRequired),
                firmwareCallEnabled: false);

        return HostCheck.Result(result.Status == UefiExitBootServicesLiveWiringStatus.ExitBootServicesPlanBlocked
            && result.NextStep == UefiExitBootServicesLiveWiringStep.ValidateExitBootServicesPlan
            && result.DiagnosticsStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && result.ExitBootServicesPlanStatus == UefiExitBootServicesPlanStatus.RetryRequired
            && !result.ReadyToCallFirmware
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable);
    }

    [HostTest("uefi.exitboot live wiring stops before firmware call when disabled", HostLane.Fast, 3)]
    private static bool TestStopsBeforeFirmwareCallWhenDisabled()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        UefiExitBootServicesLiveWiringPlanResult result =
            UefiExitBootServicesLiveWiringPlan.Evaluate(
                freshMemoryMapCaptured: true,
                CreateReadyPostFirmwareDiagnosticsResult(),
                CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.Success),
                firmwareCallEnabled: false);

        return HostCheck.Result(result.Status == UefiExitBootServicesLiveWiringStatus.FirmwareCallDisabled
            && result.NextStep == UefiExitBootServicesLiveWiringStep.CallFirmwareExitBootServices
            && result.DiagnosticsStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && result.ExitBootServicesPlanStatus == UefiExitBootServicesPlanStatus.Success
            && !result.FirmwareCallEnabled
            && !result.ReadyToCallFirmware
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable);
    }

    [HostTest("uefi.exitboot live wiring can be explicitly enabled for future firmware call", HostLane.Fast, 4)]
    private static bool TestCanBeExplicitlyEnabledForFutureFirmwareCall()
    {
        UefiExitBootServicesLiveWiringPlanResult result =
            UefiExitBootServicesLiveWiringPlan.Evaluate(
                freshMemoryMapCaptured: true,
                CreateReadyPostFirmwareDiagnosticsResult(),
                CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.Success),
                firmwareCallEnabled: true);

        return HostCheck.Result(result.Status == UefiExitBootServicesLiveWiringStatus.ReadyToCallFirmware
            && result.NextStep == UefiExitBootServicesLiveWiringStep.CallFirmwareExitBootServices
            && result.DiagnosticsStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && result.ExitBootServicesPlanStatus == UefiExitBootServicesPlanStatus.Success
            && result.FirmwareCallEnabled
            && result.ReadyToCallFirmware);
    }

    private static SerialDiagnosticsGuardResult CreateReadyPostFirmwareDiagnosticsResult()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        guard.MarkOwnedSerialReady();
        return guard.RequirePostFirmwareDiagnostics("exitboot.live");
    }

    private static UefiExitBootServicesPlanResult CreateExitBootServicesPlan(
        UefiExitBootServicesPlanStatus status)
    {
        return UefiExitBootServicesPlanResult.Create(
            status,
            EfiBootHandoffStatus.Success,
            shouldRetry: status == UefiExitBootServicesPlanStatus.RetryRequired,
            attemptIndex: 1,
            maximumRetryCount: UefiExitBootServicesPlan.DefaultMaximumRetryCount);
    }
}
