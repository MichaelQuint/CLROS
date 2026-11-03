using OS.Boot;
using OS.Kernel.Diagnostics;

internal static class BootSmokeTests
{
    [HostFixture]
    internal static void Run()
    {
        int failures = 0;
        HostCheck.Run("boot.sequence emits machine-readable smoke markers", TestBootSequenceMarkers, ref failures);
        HostCheck.Run("boot event ring buffer preserves newest events", TestBootEventRingBuffer, ref failures);
    }

    static bool TestBootSequenceMarkers()
    {
        StringSerialSink serial = new StringSerialSink();
        BootEventRingBuffer events = new BootEventRingBuffer(16);
        BootResult result = BootSequence.Run(serial, events);
        return result.Succeeded && serial.Contains(SerialSmokeMarkers.BootOk) && serial.Contains(SerialSmokeMarkers.OsSmokePass) && serial.Contains(SerialSmokeMarkers.TestPass + " boot.sequence") && events.Count >= 5;
    }

    static bool TestBootEventRingBuffer()
    {
        BootEventRingBuffer events = new BootEventRingBuffer(3);
        events.Record("one");
        events.Record("two");
        events.Record("three");
        events.Record("four");
        string[] snapshot = events.Snapshot();
        return snapshot.Length == 3 && snapshot[0] == "two" && snapshot[1] == "three" && snapshot[2] == "four";
    }
}
