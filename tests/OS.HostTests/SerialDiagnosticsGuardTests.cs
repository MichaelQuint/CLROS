using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class SerialDiagnosticsGuardTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("diagnostics serial guard starts firmware only", TestStartsFirmwareOnly, ref failures);
        Run("diagnostics serial guard blocks post firmware without owned serial", TestBlocksPostFirmwareWithoutOwnedSerial, ref failures);
        Run("diagnostics serial guard allows post firmware with owned serial", TestAllowsPostFirmwareWithOwnedSerial, ref failures);
        Run("diagnostics serial guard rejects invalid transition", TestRejectsInvalidTransition, ref failures);
        Run("diagnostics serial guard reports serial unavailable", TestReportsSerialUnavailable, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Serial diagnostics guard host tests failed: " + failures);
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

    private static bool TestStartsFirmwareOnly()
    {
        SerialDiagnosticsGuard guard = SerialDiagnosticsGuard.CreateFirmwareConsoleOnly();
        SerialDiagnosticsGuardResult postFirmware = guard.RequirePostFirmwareDiagnostics("fault.panic");

        return guard.Status == SerialDiagnosticsGuardStatus.FirmwareConsoleOnly
            && guard.FirmwareConsoleAvailable
            && !guard.FirmwareServicesUnavailable
            && !guard.OwnedSerialReady
            && !guard.CanUsePostFirmwareDiagnostics
            && !postFirmware.Succeeded
            && postFirmware.Status == SerialDiagnosticsGuardStatus.FirmwareConsoleOnly
            && postFirmware.Operation == "fault.panic"
            && postFirmware.Reason.Contains("firmware console", StringComparison.Ordinal);
    }

    private static bool TestBlocksPostFirmwareWithoutOwnedSerial()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SerialDiagnosticsGuardResult firmwareUnavailable = guard.MarkFirmwareServicesUnavailable();
        SerialDiagnosticsGuardResult postFirmware = guard.RequirePostFirmwareDiagnostics("post-firmware.panic");

        return firmwareUnavailable.Succeeded
            && firmwareUnavailable.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && !firmwareUnavailable.PostFirmwareDiagnosticsAvailable
            && guard.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && !guard.FirmwareConsoleAvailable
            && guard.FirmwareServicesUnavailable
            && !guard.CanUsePostFirmwareDiagnostics
            && !postFirmware.Succeeded
            && postFirmware.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && postFirmware.Operation == "post-firmware.panic"
            && postFirmware.Reason.Contains("owned serial is not ready", StringComparison.Ordinal);
    }

    private static bool TestAllowsPostFirmwareWithOwnedSerial()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SerialDiagnosticsGuardResult ownedReady = guard.MarkOwnedSerialReady();
        SerialDiagnosticsGuardResult firmwareUnavailable = guard.MarkFirmwareServicesUnavailable();
        SerialDiagnosticsGuardResult postFirmware = guard.RequirePostFirmwareDiagnostics("post-firmware.panic");

        return ownedReady.Succeeded
            && ownedReady.Status == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && ownedReady.PostFirmwareDiagnosticsAvailable
            && firmwareUnavailable.Succeeded
            && firmwareUnavailable.Status == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && firmwareUnavailable.PostFirmwareDiagnosticsAvailable
            && guard.Status == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && guard.FirmwareServicesUnavailable
            && guard.OwnedSerialReady
            && guard.CanUsePostFirmwareDiagnostics
            && postFirmware.Succeeded
            && postFirmware.Status == SerialDiagnosticsGuardStatus.OwnedSerialReady
            && postFirmware.PostFirmwareDiagnosticsAvailable
            && postFirmware.Reason.Length == 0;
    }

    private static bool TestRejectsInvalidTransition()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SerialDiagnosticsGuardResult firmwareUnavailable = guard.MarkFirmwareServicesUnavailable();
        SerialDiagnosticsGuardResult ownedAfterFirmwareUnavailable = guard.MarkOwnedSerialReady();
        SerialDiagnosticsGuardResult repeatedFirmwareUnavailable = guard.MarkFirmwareServicesUnavailable();

        return firmwareUnavailable.Succeeded
            && firmwareUnavailable.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && !ownedAfterFirmwareUnavailable.Succeeded
            && ownedAfterFirmwareUnavailable.Status == SerialDiagnosticsGuardStatus.InvalidTransition
            && ownedAfterFirmwareUnavailable.Operation == SerialDiagnosticsGuard.MarkOwnedSerialReadyOperation
            && ownedAfterFirmwareUnavailable.Reason.Contains("FirmwareServicesUnavailable", StringComparison.Ordinal)
            && !repeatedFirmwareUnavailable.Succeeded
            && repeatedFirmwareUnavailable.Status == SerialDiagnosticsGuardStatus.InvalidTransition
            && repeatedFirmwareUnavailable.Operation == SerialDiagnosticsGuard.MarkFirmwareServicesUnavailableOperation
            && guard.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && !guard.OwnedSerialReady;
    }

    private static bool TestReportsSerialUnavailable()
    {
        SerialDiagnosticsGuard guard = SerialDiagnosticsGuard.CreateSerialUnavailable();
        SerialDiagnosticsGuardResult postFirmware = guard.RequirePostFirmwareDiagnostics("fault.panic");

        return guard.Status == SerialDiagnosticsGuardStatus.SerialUnavailable
            && !guard.FirmwareConsoleAvailable
            && !guard.OwnedSerialReady
            && !postFirmware.Succeeded
            && postFirmware.Status == SerialDiagnosticsGuardStatus.SerialUnavailable
            && postFirmware.Reason.Contains("serial diagnostics are unavailable", StringComparison.Ordinal);
    }
}
