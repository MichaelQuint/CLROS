namespace OS.Drivers;

public readonly struct EnaDmaRegionValidationResult
{
    private EnaDmaRegionValidationResult(bool isInitialized, EnaDmaAddressPlanStatus status, uint physicalAddressWidth, ulong maximumAddress, ulong endExclusive, EnaDmaPhysicalRegion region)
    {
        IsInitialized = isInitialized;
        Status = status;
        PhysicalAddressWidth = physicalAddressWidth;
        MaximumAddress = maximumAddress;
        EndExclusive = endExclusive;
        Region = region;
    }

    public EnaDmaAddressPlanStatus Status
    {
        get;
    }
    public uint PhysicalAddressWidth
    {
        get;
    }
    public ulong MaximumAddress
    {
        get;
    }
    public ulong EndExclusive
    {
        get;
    }
    public EnaDmaPhysicalRegion Region
    {
        get;
    }
    public bool IsInitialized
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == EnaDmaAddressPlanStatus.Success;

    internal static EnaDmaRegionValidationResult Create(EnaDmaAddressPlanStatus status, uint physicalAddressWidth, ulong maximumAddress, ulong endExclusive, EnaDmaPhysicalRegion region)
    {
        return new EnaDmaRegionValidationResult(isInitialized: true, status, physicalAddressWidth, maximumAddress, endExclusive, region);
    }
}
