using System.Runtime.CompilerServices;
using OS.Boot;

internal static class BootInfoTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("bootinfo preserves UEFI firmware contract", TestUefiContract, ref failures);
        Run("bootinfo exposes host boot contract", TestHostContract, ref failures);
        Run("bootinfo reports deferred memory map capability as missing", TestDeferredMemoryMapCapability, ref failures);
        Run("bootinfo reports unknown capability bits", TestUnknownCapability, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("BootInfo host tests failed: " + failures);
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

    private static bool TestUefiContract()
    {
        BootInfo bootInfo = BootInfo.CreateUefi(
            "EDK II",
            0x00020070,
            BootCapabilities.TextOutput | BootCapabilities.Shutdown | BootCapabilities.MemoryMap);

        return bootInfo.Mode == BootMode.Uefi
            && bootInfo.FirmwareVendor == "EDK II"
            && bootInfo.FirmwareRevision == 0x00020070
            && bootInfo.HasCapability(BootCapabilities.TextOutput)
            && bootInfo.HasCapability(BootCapabilities.Shutdown)
            && bootInfo.HasCapability(BootCapabilities.MemoryMap)
            && bootInfo.RequireCapability(BootCapabilities.TextOutput | BootCapabilities.MemoryMap).Succeeded;
    }

    private static bool TestHostContract()
    {
        BootInfo bootInfo = BootInfo.CreateHost(BootCapabilities.TextOutput);
        BootInfo missingVendor = BootInfo.CreateUefi("   ", 0, BootCapabilities.None);

        return bootInfo.Mode == BootMode.Host
            && bootInfo.FirmwareVendor == BootInfo.HostFirmwareVendor
            && bootInfo.FirmwareRevision == 0
            && bootInfo.HasCapability(BootCapabilities.TextOutput)
            && !bootInfo.HasCapability(BootCapabilities.Shutdown)
            && missingVendor.FirmwareVendor == BootInfo.UnknownFirmwareVendor;
    }

    private static bool TestDeferredMemoryMapCapability()
    {
        BootInfo bootInfo = BootInfo.CreateHost(BootCapabilities.TextOutput | BootCapabilities.Shutdown);
        BootCapabilityResult result = bootInfo.RequireCapability(BootCapabilities.MemoryMap);

        return !result.Succeeded
            && result.RequiredCapabilities == BootCapabilities.MemoryMap
            && result.MissingCapabilities == BootCapabilities.MemoryMap
            && result.UnknownCapabilities == BootCapabilities.None
            && result.Message.Contains("Missing boot capability", StringComparison.Ordinal);
    }

    private static bool TestUnknownCapability()
    {
        BootInfo bootInfo = BootInfo.CreateHost(BootCapabilities.TextOutput);
        BootCapabilities unknown = (BootCapabilities)128;
        BootCapabilityResult result = bootInfo.RequireCapability(unknown);

        return !result.Succeeded
            && result.RequiredCapabilities == unknown
            && result.MissingCapabilities == BootCapabilities.None
            && result.UnknownCapabilities == unknown
            && result.Message.Contains("Unknown boot capability", StringComparison.Ordinal);
    }
}
