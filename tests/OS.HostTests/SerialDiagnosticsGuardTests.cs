using System.Runtime.CompilerServices;
using OS.Kernel.Diagnostics;

internal static class SerialDiagnosticsGuardTests
{

    [HostTest("diagnostics serial guard starts firmware only", HostLane.Fast, 0)]
    private static bool TestStartsFirmwareOnly()
    {
        SerialDiagnosticsGuard guard = SerialDiagnosticsGuard.CreateFirmwareConsoleOnly();
        SerialDiagnosticsGuardResult postFirmware = guard.RequirePostFirmwareDiagnostics("fault.panic");

        return HostCheck.Result(guard.Status == SerialDiagnosticsGuardStatus.FirmwareConsoleOnly
            && guard.FirmwareConsoleAvailable
            && !guard.FirmwareServicesUnavailable
            && !guard.OwnedSerialReady
            && !guard.CanUsePostFirmwareDiagnostics
            && !postFirmware.Succeeded
            && postFirmware.Status == SerialDiagnosticsGuardStatus.FirmwareConsoleOnly
            && postFirmware.Operation == "fault.panic"
            && postFirmware.Reason.Contains("firmware console", StringComparison.Ordinal));
    }

    [HostTest("diagnostics serial guard blocks post firmware without owned serial", HostLane.Fast, 1)]
    private static bool TestBlocksPostFirmwareWithoutOwnedSerial()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SerialDiagnosticsGuardResult firmwareUnavailable = guard.MarkFirmwareServicesUnavailable();
        SerialDiagnosticsGuardResult postFirmware = guard.RequirePostFirmwareDiagnostics("post-firmware.panic");

        return HostCheck.Result(firmwareUnavailable.Succeeded
            && firmwareUnavailable.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && !firmwareUnavailable.PostFirmwareDiagnosticsAvailable
            && guard.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && !guard.FirmwareConsoleAvailable
            && guard.FirmwareServicesUnavailable
            && !guard.CanUsePostFirmwareDiagnostics
            && !postFirmware.Succeeded
            && postFirmware.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && postFirmware.Operation == "post-firmware.panic"
            && postFirmware.Reason.Contains("owned serial is not ready", StringComparison.Ordinal));
    }

    [HostTest("diagnostics serial guard allows post firmware with owned serial", HostLane.Fast, 2)]
    private static bool TestAllowsPostFirmwareWithOwnedSerial()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SerialDiagnosticsGuardResult ownedReady = guard.MarkOwnedSerialReady();
        SerialDiagnosticsGuardResult firmwareUnavailable = guard.MarkFirmwareServicesUnavailable();
        SerialDiagnosticsGuardResult postFirmware = guard.RequirePostFirmwareDiagnostics("post-firmware.panic");

        return HostCheck.Result(ownedReady.Succeeded
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
            && postFirmware.Reason.Length == 0);
    }

    [HostTest("diagnostics serial guard rejects invalid transition", HostLane.Fast, 3)]
    private static bool TestRejectsInvalidTransition()
    {
        SerialDiagnosticsGuard guard = new SerialDiagnosticsGuard();
        SerialDiagnosticsGuardResult firmwareUnavailable = guard.MarkFirmwareServicesUnavailable();
        SerialDiagnosticsGuardResult ownedAfterFirmwareUnavailable = guard.MarkOwnedSerialReady();
        SerialDiagnosticsGuardResult repeatedFirmwareUnavailable = guard.MarkFirmwareServicesUnavailable();

        return HostCheck.Result(firmwareUnavailable.Succeeded
            && firmwareUnavailable.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && !ownedAfterFirmwareUnavailable.Succeeded
            && ownedAfterFirmwareUnavailable.Status == SerialDiagnosticsGuardStatus.InvalidTransition
            && ownedAfterFirmwareUnavailable.Operation == SerialDiagnosticsGuard.MarkOwnedSerialReadyOperation
            && ownedAfterFirmwareUnavailable.Reason.Contains("FirmwareServicesUnavailable", StringComparison.Ordinal)
            && !repeatedFirmwareUnavailable.Succeeded
            && repeatedFirmwareUnavailable.Status == SerialDiagnosticsGuardStatus.InvalidTransition
            && repeatedFirmwareUnavailable.Operation == SerialDiagnosticsGuard.MarkFirmwareServicesUnavailableOperation
            && guard.Status == SerialDiagnosticsGuardStatus.FirmwareServicesUnavailable
            && !guard.OwnedSerialReady);
    }

    [HostTest("diagnostics serial guard reports serial unavailable", HostLane.Fast, 4)]
    private static bool TestReportsSerialUnavailable()
    {
        SerialDiagnosticsGuard guard = SerialDiagnosticsGuard.CreateSerialUnavailable();
        SerialDiagnosticsGuardResult postFirmware = guard.RequirePostFirmwareDiagnostics("fault.panic");

        return HostCheck.Result(guard.Status == SerialDiagnosticsGuardStatus.SerialUnavailable
            && !guard.FirmwareConsoleAvailable
            && !guard.OwnedSerialReady
            && !postFirmware.Succeeded
            && postFirmware.Status == SerialDiagnosticsGuardStatus.SerialUnavailable
            && postFirmware.Reason.Contains("serial diagnostics are unavailable", StringComparison.Ordinal));
    }
}
