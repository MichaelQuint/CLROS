namespace OS.Drivers;
public readonly struct PciConfigDwordReadResult
{
    public PciConfigDwordReadResult(PciConfigDwordAccessStatus status, uint value) : this(status, value, isInitialized: true)
    {
    }

    private PciConfigDwordReadResult(PciConfigDwordAccessStatus status, uint value, bool isInitialized)
    {
        Status = status;
        Value = value;
        IsInitialized = isInitialized;
    }

    public PciConfigDwordAccessStatus Status { get; }
    public uint Value { get; }
    public bool IsInitialized { get; }
    public bool Succeeded => IsInitialized && Status == PciConfigDwordAccessStatus.Success;
}
