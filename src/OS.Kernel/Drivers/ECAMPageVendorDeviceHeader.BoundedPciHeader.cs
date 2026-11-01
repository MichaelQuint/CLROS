namespace OS.Drivers;
public readonly struct BoundedPciHeader
{
    public readonly uint Identity, ClassRevision, CommandStatus, BusNumbers;
    public readonly byte HeaderType, CapabilityPointer;
    internal BoundedPciHeader(uint identity, uint classes, uint commandStatus, uint busNumbers, byte header, byte capability)
    {
        Identity = identity;
        ClassRevision = classes;
        CommandStatus = commandStatus;
        BusNumbers = busNumbers;
        HeaderType = header;
        CapabilityPointer = capability;
    }

    public ushort Vendor => (ushort)Identity;
    public ushort Device => (ushort)(Identity >> 16);
}
