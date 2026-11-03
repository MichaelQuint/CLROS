namespace OS.Drivers;

public static partial class PciBusScanner
{
    private enum FunctionScanOutcome
    {
        Present = 0,
        Absent = 1,
        Failed = 2
    }
}
