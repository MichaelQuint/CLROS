using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static partial class PlatformConsoleGuardTests
{
    [HostTest("platform.console allows firmware console before UEFI gone", HostLane.Fast, 0)]
    private static bool TestAllowedBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble sink = new ConsoleSinkDouble(available: true);
        IPlatformConsole console = new GuardedPlatformConsole(firmware, sink);
        PlatformConsoleResult result = console.WriteLine("OS_BOOT_OK");
        return HostCheck.Result(result.Succeeded && result.Status == PlatformConsoleStatus.Success && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.Operation == PlatformConsoleOperations.OutputString && result.CharacterCount == 10 && result.Message.Length == 0 && sink.AvailabilityReadCount == 1 && sink.LineCount == 1 && sink.LastLine == "OS_BOOT_OK");
    }

    [HostTest("platform.console rejects firmware console in UEFI-gone simulation", HostLane.Fast, 1)]
    private static bool TestRejectedAfterUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        ConsoleSinkDouble sink = new ConsoleSinkDouble(available: true);
        IPlatformConsole console = new GuardedPlatformConsole(firmware, sink);
        PlatformConsoleResult result = console.WriteLine("OS_BOOT_OK");
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformConsoleStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.UefiGoneSimulated && result.Operation == PlatformConsoleOperations.OutputString && result.CharacterCount == 0 && result.Message.Contains("UefiGoneSimulated", StringComparison.Ordinal) && sink.AvailabilityReadCount == 0 && sink.LineCount == 0);
    }

    [HostTest("platform.console rejects firmware console after ExitBootServices", HostLane.Fast, 2)]
    private static bool TestRejectedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        ConsoleSinkDouble sink = new ConsoleSinkDouble(available: true);
        IPlatformConsole console = new GuardedPlatformConsole(firmware, sink);
        PlatformConsoleResult result = console.WriteLine("OS_BOOT_OK");
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformConsoleStatus.FirmwareServicesUnavailable && result.Mode == FirmwareServicesMode.BootServicesExited && result.Operation == PlatformConsoleOperations.OutputString && result.CharacterCount == 0 && result.Message.Contains("BootServicesExited", StringComparison.Ordinal) && sink.AvailabilityReadCount == 0 && sink.LineCount == 0);
    }

    [HostTest("platform.console rejects invalid marker input", HostLane.Fast, 3)]
    private static bool TestInvalidMarkerInput()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble sink = new ConsoleSinkDouble(available: true);
        IPlatformConsole console = new GuardedPlatformConsole(firmware, sink);
        PlatformConsoleResult result = console.WriteLine("OS_BOOT_OK\nBAD");
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformConsoleStatus.InvalidLine && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.Operation == PlatformConsoleOperations.OutputString && result.CharacterCount == 0 && result.Message.Contains("printable ASCII", StringComparison.Ordinal) && sink.LineCount == 0);
    }

    [HostTest("platform.console reports sink unavailable", HostLane.Fast, 4)]
    private static bool TestSinkUnavailable()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble sink = new ConsoleSinkDouble(available: false);
        IPlatformConsole console = new GuardedPlatformConsole(firmware, sink);
        PlatformConsoleResult result = console.WriteLine("OS_BOOT_OK");
        return HostCheck.Result(!result.Succeeded && result.Status == PlatformConsoleStatus.SinkUnavailable && result.Mode == FirmwareServicesMode.BootServicesAvailable && result.Operation == PlatformConsoleOperations.OutputString && result.CharacterCount == 0 && result.Message.Contains("sink", StringComparison.Ordinal) && sink.LineCount == 0);
    }
}
