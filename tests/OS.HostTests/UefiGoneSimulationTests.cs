using System.Runtime.CompilerServices;
using OS.Boot;
using OS.Kernel.Diagnostics;
using OS.Kernel.Platform;

internal static class UefiGoneSimulationTests
{
    [ModuleInitializer]
    internal static void RunAll()
    {
        int failures = 0;

        Run("uefi gone simulation emits pass markers and denies firmware calls", TestSimulationPass, ref failures);
        Run("uefi gone simulation blocks missing owned diagnostics", TestSimulationBlocksMissingDiagnostics, ref failures);
        Run("uefi gone simulation rejects already-exited firmware mode", TestSimulationAlreadyExited, ref failures);

        if (failures != 0)
        {
            throw new InvalidOperationException("UEFI-gone simulation host tests failed: " + failures);
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

    private static bool TestSimulationPass()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        SimulationSerialSink serial = new SimulationSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunUefiGoneSimulation(firmware, serial, events);
        FirmwareCallResult deniedAfterSimulation = firmware.RequireBootServices("LocateProtocol");
        string[] eventSnapshot = events.Snapshot();

        return result.Succeeded
            && firmware.Mode == FirmwareServicesMode.UefiGoneSimulated
            && !deniedAfterSimulation.Succeeded
            && serial.Count == 4
            && serial.LineAt(0) == BootSequence.UefiGoneSimulationStart
            && serial.LineAt(1) == BootSequence.UefiGoneSimulationPlatformDenied
            && serial.LineAt(2) == BootSequence.PostFirmwareDiagnosticsReady
            && serial.LineAt(3) == BootSequence.UefiGoneSimulationPass
            && eventSnapshot.Length == 4
            && eventSnapshot[0] == BootSequence.UefiGoneSimulationStart
            && eventSnapshot[1] == BootSequence.UefiGoneSimulationPlatformDenied
            && eventSnapshot[2] == BootSequence.PostFirmwareDiagnosticsReady
            && eventSnapshot[3] == BootSequence.UefiGoneSimulationPass;
    }

    private static bool TestSimulationBlocksMissingDiagnostics()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        SerialDiagnosticsGuard diagnostics = SerialDiagnosticsGuard.CreateFirmwareConsoleOnly();
        SerialDiagnosticsGuardResult readiness =
            diagnostics.RequirePostFirmwareDiagnostics("uefi.gone.simulation.diagnostics");
        SimulationSerialSink serial = new SimulationSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunUefiGoneSimulation(
            firmware,
            readiness,
            serial,
            events);

        string blockedMarker = BootSequence.CreatePostFirmwareDiagnosticsBlockedMarker(
            SerialDiagnosticsGuardStatus.FirmwareConsoleOnly);
        string[] eventSnapshot = events.Snapshot();

        return !result.Succeeded
            && result.Reason.Contains(
                nameof(SerialDiagnosticsGuardStatus.FirmwareConsoleOnly),
                StringComparison.Ordinal)
            && firmware.Mode == FirmwareServicesMode.UefiGoneSimulated
            && serial.Count == 3
            && serial.LineAt(0) == BootSequence.UefiGoneSimulationStart
            && serial.LineAt(1) == BootSequence.UefiGoneSimulationPlatformDenied
            && serial.LineAt(2) == blockedMarker
            && eventSnapshot.Length == 3
            && eventSnapshot[0] == BootSequence.UefiGoneSimulationStart
            && eventSnapshot[1] == BootSequence.UefiGoneSimulationPlatformDenied
            && eventSnapshot[2] == blockedMarker;
    }

    private static bool TestSimulationAlreadyExited()
    {
        FirmwareServicesGuard firmware = new FirmwareServicesGuard();
        _ = firmware.MarkBootServicesExited();
        SimulationSerialSink serial = new SimulationSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(4);

        BootResult result = BootSequence.RunUefiGoneSimulation(firmware, serial, events);

        return !result.Succeeded
            && result.Reason == "simulation-denied"
            && firmware.Mode == FirmwareServicesMode.BootServicesExited
            && serial.Count == 2
            && serial.LineAt(0) == BootSequence.UefiGoneSimulationStart
            && serial.LineAt(1) == "UEFI_GONE_SIM_FAIL reason=simulation-denied";
    }

    private sealed class SimulationSerialSink : ISerialSink
    {
        private readonly List<string> _lines = new List<string>();

        public int Count => _lines.Count;

        public void WriteLine(string line)
        {
            _lines.Add(line);
        }

        public string LineAt(int index)
        {
            return _lines[index];
        }
    }
}
