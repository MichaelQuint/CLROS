namespace OS.Drivers;

using OS.Std.Binary;

public readonly struct EnaAdminControlBuffer
{
    public EnaAdminControlBuffer(uint length, ulong physicalAddress, ushort reserved)
    {
        Length = length;
        PhysicalAddress = physicalAddress;
        Reserved = reserved;
    }

    public uint Length
    {
        get;
    }
    public ulong PhysicalAddress
    {
        get;
    }
    public ushort Reserved
    {
        get;
    }
    public uint AddressLow => (uint)PhysicalAddress;
    public ushort AddressHigh => (ushort)(PhysicalAddress >> 32);
}
