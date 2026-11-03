namespace OS.Drivers;

public readonly struct PciEcamConfigurationAddressResult
{
    private PciEcamConfigurationAddressResult(bool isInitialized, PciEcamConfigurationSpaceStatus status, PciEcamSegment segment, byte bus, byte device, byte function, ushort registerOffset, byte accessWidth, ulong ecamOffset, ulong physicalAddress)
    {
        IsInitialized = isInitialized;
        Status = status;
        Segment = segment;
        Bus = bus;
        Device = device;
        Function = function;
        RegisterOffset = registerOffset;
        AccessWidth = accessWidth;
        EcamOffset = ecamOffset;
        PhysicalAddress = physicalAddress;
    }

    public bool IsInitialized
    {
        get;
    }
    public PciEcamConfigurationSpaceStatus Status
    {
        get;
    }
    public PciEcamSegment Segment
    {
        get;
    }
    public byte Bus
    {
        get;
    }
    public byte Device
    {
        get;
    }
    public byte Function
    {
        get;
    }
    public ushort RegisterOffset
    {
        get;
    }
    public byte AccessWidth
    {
        get;
    }
    public ulong EcamOffset
    {
        get;
    }
    public ulong PhysicalAddress
    {
        get;
    }
    public bool Succeeded => IsInitialized && Status == PciEcamConfigurationSpaceStatus.Success;

    public static PciEcamConfigurationAddressResult Success(PciEcamSegment segment, byte bus, byte device, byte function, ushort registerOffset, byte accessWidth, ulong ecamOffset, ulong physicalAddress)
    {
        return new PciEcamConfigurationAddressResult(true, PciEcamConfigurationSpaceStatus.Success, segment, bus, device, function, registerOffset, accessWidth, ecamOffset, physicalAddress);
    }

    public static PciEcamConfigurationAddressResult Fail(PciEcamConfigurationSpaceStatus status, PciEcamSegment segment, byte bus, byte device, byte function, ushort registerOffset, byte accessWidth)
    {
        return new PciEcamConfigurationAddressResult(true, status, segment, bus, device, function, registerOffset, accessWidth, ecamOffset: 0, physicalAddress: 0);
    }
}
