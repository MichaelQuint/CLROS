using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static class FirmwareServicesGuardTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("firmware guard allows boot services before UEFI gone", TestBootServicesAllowed, ref failures);
        Run("firmware guard rejects boot services in UEFI-gone simulation", TestUefiGoneSimulation, ref failures);
        Run("firmware guard rejects simulation after ExitBootServices", TestExitedMode, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Firmware services guard host tests failed: " + failures);
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

    private static bool TestBootServicesAllowed()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        FirmwareCallResult result = guard.RequireBootServices("GetMemoryMap");

        return guard.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Succeeded
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == "GetMemoryMap"
            && result.Reason.Length == 0;
    }

    private static bool TestUefiGoneSimulation()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        FirmwareCallResult simulation = guard.EnterUefiGoneSimulation();
        FirmwareCallResult denied = guard.RequireBootServices("LocateProtocol");

        return simulation.Succeeded
            && simulation.Mode == FirmwareServicesMode.UefiGoneSimulated
            && denied.Mode == FirmwareServicesMode.UefiGoneSimulated
            && !denied.Succeeded
            && denied.Operation == "LocateProtocol"
            && denied.Reason.Contains("UefiGoneSimulated", StringComparison.Ordinal);
    }

    private static bool TestExitedMode()
    {
        FirmwareServicesGuard guard = new FirmwareServicesGuard();
        FirmwareCallResult exited = guard.MarkBootServicesExited();
        FirmwareCallResult deniedSimulation = guard.EnterUefiGoneSimulation();
        FirmwareCallResult deniedBootService = guard.RequireBootServices("GetMemoryMap");

        return exited.Succeeded
            && exited.Mode == FirmwareServicesMode.BootServicesExited
            && guard.Mode == FirmwareServicesMode.BootServicesExited
            && !deniedSimulation.Succeeded
            && deniedSimulation.Mode == FirmwareServicesMode.BootServicesExited
            && !deniedBootService.Succeeded
            && deniedBootService.Reason.Contains("BootServicesExited", StringComparison.Ordinal);
    }
}
