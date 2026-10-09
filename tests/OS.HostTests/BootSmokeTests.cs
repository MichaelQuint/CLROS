using OS.Boot;
using OS.Kernel.Diagnostics;

internal static class BootSmokeTests
{
[HostFixture]
internal static void RunAll()
{
int failures = 0;

Run("boot.sequence emits machine-readable smoke markers", TestBootSequenceMarkers, ref failures);
Run("boot event ring buffer preserves newest events", TestBootEventRingBuffer, ref failures);

if (failures != 0) throw new InvalidOperationException("Boot smoke failures=" + failures);
}

static void Run(string name, Func<bool> test, ref int failures)
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

static bool TestBootSequenceMarkers()
{
    StringSerialSink serial = new StringSerialSink();
    BootEventRingBuffer events = new BootEventRingBuffer(16);
    BootResult result = BootSequence.Run(serial, events);

    return result.Succeeded
        && serial.Contains(SerialSmokeMarkers.BootOk)
        && serial.Contains(SerialSmokeMarkers.OsSmokePass)
        && serial.Contains(SerialSmokeMarkers.TestPass + " boot.sequence")
        && events.Count >= 5;
}

static bool TestBootEventRingBuffer()
{
    BootEventRingBuffer events = new BootEventRingBuffer(3);
    events.Record("one");
    events.Record("two");
    events.Record("three");
    events.Record("four");

    string[] snapshot = events.Snapshot();
    return snapshot.Length == 3
        && snapshot[0] == "two"
        && snapshot[1] == "three"
        && snapshot[2] == "four";
}

sealed class StringSerialSink : ISerialSink
{
    private readonly List<string> _lines = new List<string>();

    public void WriteLine(string line)
    {
        _lines.Add(line);
    }

    public bool Contains(string line)
    {
        return _lines.Contains(line);
    }
}
}
