namespace OS.Drivers;

using OS.Std.Binary;

public readonly struct VirtioNetTransmitPublishedDescriptor
{
    internal VirtioNetTransmitPublishedDescriptor(VirtioNetTransmitDescriptorRole role, ushort descriptorIndex, ulong physicalAddress, uint length, bool hasNext, ushort nextDescriptorIndex, ushort flags, uint descriptorTableOffset, VirtioNetTransmitDescriptorMemoryRange memoryRange)
    {
        Role = role;
        DescriptorIndex = descriptorIndex;
        PhysicalAddress = physicalAddress;
        Length = length;
        HasNext = hasNext;
        NextDescriptorIndex = nextDescriptorIndex;
        Flags = flags;
        DescriptorTableOffset = descriptorTableOffset;
        MemoryRange = memoryRange;
    }

    public VirtioNetTransmitDescriptorRole Role
    {
        get;
    }
    public ushort DescriptorIndex
    {
        get;
    }
    public ulong PhysicalAddress
    {
        get;
    }
    public uint Length
    {
        get;
    }
    public bool HasNext
    {
        get;
    }
    public ushort NextDescriptorIndex
    {
        get;
    }
    public ushort Flags
    {
        get;
    }
    public uint DescriptorTableOffset
    {
        get;
    }
    public bool DeviceReadable => true;
    public bool DeviceWritable => false;
    public VirtioNetTransmitDescriptorMemoryRange MemoryRange
    {
        get;
    }
}
