namespace OS.Boot;

public struct WorkloadServiceLoopRequest
{
    public ulong FrequencyHz;
    public uint IntervalMilliseconds, MinimumIterations, MaximumIterations, PollLimit;
    public bool FirmwareExited, RetainedPointersCleared, OwnedMappingsValidated;
}
