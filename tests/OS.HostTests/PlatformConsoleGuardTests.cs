using System.Runtime.CompilerServices;
using OS.Kernel.Platform;

internal static class PlatformConsoleGuardTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("platform.console allows firmware console before UEFI gone", TestAllowedBeforeUefiGone, ref failures);
        Run("platform.console rejects firmware console in UEFI-gone simulation", TestRejectedAfterUefiGoneSimulation, ref failures);
        Run("platform.console rejects firmware console after ExitBootServices", TestRejectedAfterExitBootServices, ref failures);
        Run("platform.console rejects invalid marker input", TestInvalidMarkerInput, ref failures);
        Run("platform.console reports sink unavailable", TestSinkUnavailable, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("Platform console guard host tests failed: " + failures);
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

    private static bool TestAllowedBeforeUefiGone()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble sink = new ConsoleSinkDouble(available: true);
        IPlatformConsole console = new GuardedConsoleDouble(firmware, sink);

        PlatformConsoleResult result = console.WriteLine("OS_BOOT_OK");

        return result.Succeeded
            && result.Status == PlatformConsoleStatus.Success
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformConsoleOperations.OutputString
            && result.CharacterCount == 10
            && result.Message.Length == 0
            && sink.LineCount == 1
            && sink.LastLine == "OS_BOOT_OK";
    }

    private static bool TestRejectedAfterUefiGoneSimulation()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.EnterUefiGoneSimulation();
        ConsoleSinkDouble sink = new ConsoleSinkDouble(available: true);
        IPlatformConsole console = new GuardedConsoleDouble(firmware, sink);

        PlatformConsoleResult result = console.WriteLine("OS_BOOT_OK");

        return !result.Succeeded
            && result.Status == PlatformConsoleStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.UefiGoneSimulated
            && result.Operation == PlatformConsoleOperations.OutputString
            && result.CharacterCount == 0
            && result.Message.Contains("UefiGoneSimulated", StringComparison.Ordinal)
            && sink.LineCount == 0;
    }

    private static bool TestRejectedAfterExitBootServices()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        ConsoleSinkDouble sink = new ConsoleSinkDouble(available: true);
        IPlatformConsole console = new GuardedConsoleDouble(firmware, sink);

        PlatformConsoleResult result = console.WriteLine("OS_BOOT_OK");

        return !result.Succeeded
            && result.Status == PlatformConsoleStatus.FirmwareServicesUnavailable
            && result.Mode == FirmwareServicesMode.BootServicesExited
            && result.Operation == PlatformConsoleOperations.OutputString
            && result.CharacterCount == 0
            && result.Message.Contains("BootServicesExited", StringComparison.Ordinal)
            && sink.LineCount == 0;
    }

    private static bool TestInvalidMarkerInput()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble sink = new ConsoleSinkDouble(available: true);
        IPlatformConsole console = new GuardedConsoleDouble(firmware, sink);

        PlatformConsoleResult result = console.WriteLine("OS_BOOT_OK\nBAD");

        return !result.Succeeded
            && result.Status == PlatformConsoleStatus.InvalidLine
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformConsoleOperations.OutputString
            && result.CharacterCount == 0
            && result.Message.Contains("printable ASCII", StringComparison.Ordinal)
            && sink.LineCount == 0;
    }

    private static bool TestSinkUnavailable()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        ConsoleSinkDouble sink = new ConsoleSinkDouble(available: false);
        IPlatformConsole console = new GuardedConsoleDouble(firmware, sink);

        PlatformConsoleResult result = console.WriteLine("OS_BOOT_OK");

        return !result.Succeeded
            && result.Status == PlatformConsoleStatus.SinkUnavailable
            && result.Mode == FirmwareServicesMode.BootServicesAvailable
            && result.Operation == PlatformConsoleOperations.OutputString
            && result.CharacterCount == 0
            && result.Message.Contains("sink", StringComparison.Ordinal)
            && sink.LineCount == 0;
    }

    private sealed class GuardedConsoleDouble : IPlatformConsole
    {
        private readonly IPlatformFirmware _firmware;
        private readonly ConsoleSinkDouble _sink;

        public GuardedConsoleDouble(IPlatformFirmware firmware, ConsoleSinkDouble sink)
        {
            _firmware = firmware;
            _sink = sink;
        }

        public PlatformConsoleResult WriteLine(string line)
        {
            FirmwareCallResult guardResult = _firmware.RequireBootServices(
                PlatformConsoleOperations.OutputString);

            PlatformConsoleResult result = _sink.Available
                ? PlatformConsoleResult.Written(guardResult, line)
                : PlatformConsoleResult.SinkUnavailable(guardResult, line);

            if (result.Succeeded)
            {
                _sink.WriteLine(line);
            }

            return result;
        }
    }

    private sealed class ConsoleSinkDouble
    {
        public ConsoleSinkDouble(bool available)
        {
            Available = available;
        }

        public bool Available { get; }

        public int LineCount { get; private set; }

        public string LastLine { get; private set; } = string.Empty;

        public void WriteLine(string line)
        {
            LineCount++;
            LastLine = line;
        }
    }
}
