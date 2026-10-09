using System.Runtime.CompilerServices;
using OS.Boot;

internal static class BootInfoTests
{

    [HostTest("bootinfo preserves UEFI firmware contract", HostLane.Fast, 0)]
    private static bool TestUefiContract()
    {
        BootInfo bootInfo = BootInfo.CreateUefi(
            "EDK II",
            0x00020070,
            BootCapabilities.TextOutput | BootCapabilities.Shutdown | BootCapabilities.MemoryMap);

        return HostCheck.Result(bootInfo.Mode == BootMode.Uefi
            && bootInfo.FirmwareVendor == "EDK II"
            && bootInfo.FirmwareRevision == 0x00020070
            && bootInfo.HasCapability(BootCapabilities.TextOutput)
            && bootInfo.HasCapability(BootCapabilities.Shutdown)
            && bootInfo.HasCapability(BootCapabilities.MemoryMap)
            && bootInfo.RequireCapability(BootCapabilities.TextOutput | BootCapabilities.MemoryMap).Succeeded);
    }

    [HostTest("bootinfo exposes host boot contract", HostLane.Fast, 1)]
    private static bool TestHostContract()
    {
        BootInfo bootInfo = BootInfo.CreateHost(BootCapabilities.TextOutput);
        BootInfo missingVendor = BootInfo.CreateUefi("   ", 0, BootCapabilities.None);

        return HostCheck.Result(bootInfo.Mode == BootMode.Host
            && bootInfo.FirmwareVendor == BootInfo.HostFirmwareVendor
            && bootInfo.FirmwareRevision == 0
            && bootInfo.HasCapability(BootCapabilities.TextOutput)
            && !bootInfo.HasCapability(BootCapabilities.Shutdown)
            && missingVendor.FirmwareVendor == BootInfo.UnknownFirmwareVendor);
    }

    [HostTest("bootinfo reports deferred memory map capability as missing", HostLane.Fast, 2)]
    private static bool TestDeferredMemoryMapCapability()
    {
        BootInfo bootInfo = BootInfo.CreateHost(BootCapabilities.TextOutput | BootCapabilities.Shutdown);
        BootCapabilityResult result = bootInfo.RequireCapability(BootCapabilities.MemoryMap);

        return HostCheck.Result(!result.Succeeded
            && result.RequiredCapabilities == BootCapabilities.MemoryMap
            && result.MissingCapabilities == BootCapabilities.MemoryMap
            && result.UnknownCapabilities == BootCapabilities.None
            && result.Message.Contains("Missing boot capability", StringComparison.Ordinal));
    }

    [HostTest("bootinfo reports unknown capability bits", HostLane.Fast, 3)]
    private static bool TestUnknownCapability()
    {
        BootInfo bootInfo = BootInfo.CreateHost(BootCapabilities.TextOutput);
        BootCapabilities unknown = (BootCapabilities)128;
        BootCapabilityResult result = bootInfo.RequireCapability(unknown);

        return HostCheck.Result(!result.Succeeded
            && result.RequiredCapabilities == unknown
            && result.MissingCapabilities == BootCapabilities.None
            && result.UnknownCapabilities == unknown
            && result.Message.Contains("Unknown boot capability", StringComparison.Ordinal));
    }
}
