namespace OS.Drivers;

public readonly struct PciDeviceIdentity
{
    public PciDeviceIdentity(ushort vendorId, ushort deviceId)
    {
        VendorId = vendorId;
        DeviceId = deviceId;
    }

    public ushort VendorId
    {
        get;
    }
    public ushort DeviceId
    {
        get;
    }
}
