namespace OS.Tooling;

public static partial class QemuTranscripts
{
    /// <summary>Copied GC values from one fd2 measurement. These describe
    /// supplied output, including bounded exhaustion and later live recovery,
    /// and confer no allocation, runtime or guest authority.</summary>

    public readonly record struct ServiceGcMeasurement(ulong LimitBytes, ulong BaselineHeapBytes, ulong BaselineCommittedBytes, ulong LiveBytes, ulong LiveHeapBytes, ulong LiveCommittedBytes, ulong PressureBytes, ulong PressureHeapBytes, ulong PressureCommittedBytes, ulong PressureCollections);
}
