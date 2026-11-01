namespace OS.Drivers;
public readonly struct VirtioPciCapabilityMetadata
{
    public VirtioPciCapabilityMetadata(VirtioPciCapabilityType type, byte bar, uint offset, uint length, uint notifyOffsetMultiplier)
    {
        Type = type;
        Bar = bar;
        Offset = offset;
        Length = length;
        NotifyOffsetMultiplier = notifyOffsetMultiplier;
    }

    public VirtioPciCapabilityType Type { get; }
    public byte Bar { get; }
    public uint Offset { get; }
    public uint Length { get; }
    public uint NotifyOffsetMultiplier { get; }
}
