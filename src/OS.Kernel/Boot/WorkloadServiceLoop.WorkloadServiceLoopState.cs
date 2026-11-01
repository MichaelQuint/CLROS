namespace OS.Boot;
public struct WorkloadServiceLoopState
{
    internal WorkloadServiceStatus StatusValue;
    internal uint IterationsValue, ReadsValue, MinimumValue;
    internal ulong FirstValue, LastValue, IntervalValue;
    public readonly WorkloadServiceStatus Status => StatusValue;
    public readonly uint Iterations => IterationsValue;
    public readonly uint ClockReads => ReadsValue;
    public readonly ulong FirstTicks => FirstValue;
    public readonly ulong LastTicks => LastValue;
    public readonly ulong IntervalTicks => IntervalValue;
    public readonly bool IsStopped => StatusValue == WorkloadServiceStatus.Stopped;
}
