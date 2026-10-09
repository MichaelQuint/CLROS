using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static class FirmwareServicesGuardTests
{

    [HostTest("firmware guard allows boot services before UEFI gone", HostLane.Fast, 0)]
    private static bool TestBootServicesAllowed()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        FirmwareCallResult result = guard.RequireBootServices("GetMemoryMap");

        return HostCheck.Result(guard.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Succeeded
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == "GetMemoryMap"
            && result.Reason.Length == 0);
    }

    [HostTest("firmware guard rejects boot services in UEFI-gone simulation", HostLane.Fast, 1)]
    private static bool TestUefiGoneSimulation()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        FirmwareCallResult simulation = guard.EnterUefiGoneSimulation();
        FirmwareCallResult denied = guard.RequireBootServices("LocateProtocol");

        return HostCheck.Result(simulation.Succeeded
            && simulation.Mode == FirmwareServicesMode.UefiGoneSimulated
            && denied.Mode == FirmwareServicesMode.UefiGoneSimulated
            && !denied.Succeeded
            && denied.Operation == "LocateProtocol"
            && denied.Reason.Contains("UefiGoneSimulated", StringComparison.Ordinal));
    }

    [HostTest("firmware guard rejects simulation after ExitBootServices", HostLane.Fast, 2)]
    private static bool TestExitedMode()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        FirmwareCallResult exited = guard.MarkBootServicesExited();
        FirmwareCallResult deniedSimulation = guard.EnterUefiGoneSimulation();
        FirmwareCallResult deniedBootService = guard.RequireBootServices("GetMemoryMap");

        return HostCheck.Result(exited.Succeeded
            && exited.Mode == FirmwareServicesMode.BootServicesExited
            && guard.Mode == FirmwareServicesMode.BootServicesExited
            && !deniedSimulation.Succeeded
            && deniedSimulation.Mode == FirmwareServicesMode.BootServicesExited
            && !deniedBootService.Succeeded
            && deniedBootService.Reason.Contains("BootServicesExited", StringComparison.Ordinal));
    }
}
