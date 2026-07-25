using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;

internal static class UefiExitBootServicesLiveWiringPlanTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("uefi.exitboot live wiring exposes ordered steps", TestExposesOrderedSteps, ref failures);
        Run("uefi.exitboot live wiring blocks missing diagnostics", TestBlocksMissingDiagnostics, ref failures);
        Run("uefi.exitboot live wiring blocks non-success transition plan", TestBlocksNonSuccessTransitionPlan, ref failures);
        Run("uefi.exitboot live wiring stops before firmware call when disabled", TestStopsBeforeFirmwareCallWhenDisabled, ref failures);
        Run("uefi.exitboot live wiring can be explicitly enabled for future firmware call", TestCanBeExplicitlyEnabledForFutureFirmwareCall, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("UEFI ExitBootServices live wiring host tests failed: " + failures);
        }
    }

    private static void Run(string name, Func<bool> test, ref int failures)
    {
        Console.WriteLine("TEST_START " + name);

        try
        {
            if (test())
            {
                Console.WriteLine("TEST_PASS " + name);
                return;
            }

            failures++;
            Console.WriteLine("TEST_FAIL " + name);
        }
        catch (Exception ex)
        {
            failures++;
            Console.WriteLine("TEST_FAIL " + name + " " + ex.GetType().Name + ": " + ex.Message);
        }
    }

    private static bool TestExposesOrderedSteps()
    {
        return UefiExitBootServicesLiveWiringPlan.StepCount == 7
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
            && UefiExitBootServicesLiveWiringPlan.GetStepName(4) == "CallFirmwareExitBootServices";
    }

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

        return result.Status == UefiExitBootServicesLiveWiringStatus.PostFirmwareDiagnosticsUnavailable
            && result.NextStep == UefiExitBootServicesLiveWiringStep.ConfirmOwnedSerialDiagnostics
            && result.DiagnosticsStatus == SerialDiagnosticsGuardStatus.FirmwareConsoleOnly
            && result.ExitBootServicesPlanStatus == UefiExitBootServicesPlanStatus.Success
            && !result.ReadyToCallFirmware
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable;
    }

    private static bool TestBlocksNonSuccessTransitionPlan()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        UefiExitBootServicesLiveWiringPlanResult result =
            UefiExitBootServicesLiveWiringPlan.Evaluate(
                freshMemoryMapCaptured: true,
                CreateReadyPostFirmwareDiagnosticsResult(),
                CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.RetryRequired),
                firmwareCallEnabled: false);

        return result.Status == UefiExitBootServicesLiveWiringStatus.ExitBootServicesPlanBlocked
            && result.NextStep == UefiExitBootServicesLiveWiringStep.ValidateExitBootServicesPlan
            && result.DiagnosticsStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && result.ExitBootServicesPlanStatus == UefiExitBootServicesPlanStatus.RetryRequired
            && !result.ReadyToCallFirmware
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable;
    }

    private static bool TestStopsBeforeFirmwareCallWhenDisabled()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();

        UefiExitBootServicesLiveWiringPlanResult result =
            UefiExitBootServicesLiveWiringPlan.Evaluate(
                freshMemoryMapCaptured: true,
                CreateReadyPostFirmwareDiagnosticsResult(),
                CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.Success),
                firmwareCallEnabled: false);

        return result.Status == UefiExitBootServicesLiveWiringStatus.FirmwareCallDisabled
            && result.NextStep == UefiExitBootServicesLiveWiringStep.CallFirmwareExitBootServices
            && result.DiagnosticsStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && result.ExitBootServicesPlanStatus == UefiExitBootServicesPlanStatus.Success
            && !result.FirmwareCallEnabled
            && !result.ReadyToCallFirmware
            && firmware.Mode == FirmwareServicesMode.BootServicesAvailable;
    }

    private static bool TestCanBeExplicitlyEnabledForFutureFirmwareCall()
    {
        UefiExitBootServicesLiveWiringPlanResult result =
            UefiExitBootServicesLiveWiringPlan.Evaluate(
                freshMemoryMapCaptured: true,
                CreateReadyPostFirmwareDiagnosticsResult(),
                CreateExitBootServicesPlan(UefiExitBootServicesPlanStatus.Success),
                firmwareCallEnabled: true);

        return result.Status == UefiExitBootServicesLiveWiringStatus.ReadyToCallFirmware
            && result.NextStep == UefiExitBootServicesLiveWiringStep.CallFirmwareExitBootServices
            && result.DiagnosticsStatus == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && result.ExitBootServicesPlanStatus == UefiExitBootServicesPlanStatus.Success
            && result.FirmwareCallEnabled
            && result.ReadyToCallFirmware;
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
