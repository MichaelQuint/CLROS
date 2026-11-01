namespace OS.Kernel.Platform;
public readonly struct PlatformTimerMetadata
{
    public PlatformTimerMetadata(ulong monotonicTicks, ulong frequencyHertz)
    {
        MonotonicTicks = monotonicTicks;
        FrequencyHertz = frequencyHertz;
    }

    public ulong MonotonicTicks { get; }
    public ulong FrequencyHertz { get; }
    public bool IsValid => FrequencyHertz != 0;
}
