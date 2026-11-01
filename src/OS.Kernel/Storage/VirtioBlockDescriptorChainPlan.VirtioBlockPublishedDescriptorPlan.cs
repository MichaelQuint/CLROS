using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockPublishedDescriptorPlan
{
    internal VirtioBlockPublishedDescriptorPlan(
        VirtioBlockDescriptorChainRole role,
        ushort descriptorIndex,
        ulong physicalAddress,
        uint length,
        bool hasNext,
        ushort nextDescriptorIndex,
        bool deviceWritable,
        ushort flags,
        uint descriptorTableOffset,
        VirtioBlockDescriptorMemoryRange memoryRange)
    {
        Role = role;
        DescriptorIndex = descriptorIndex;
        PhysicalAddress = physicalAddress;
        Length = length;
        HasNext = hasNext;
        NextDescriptorIndex = nextDescriptorIndex;
        DeviceWritable = deviceWritable;
        Flags = flags;
        DescriptorTableOffset = descriptorTableOffset;
        MemoryRange = memoryRange;
    }

    public VirtioBlockDescriptorChainRole Role { get; }

    public ushort DescriptorIndex { get; }

    public ulong PhysicalAddress { get; }

    public uint Length { get; }

    public bool HasNext { get; }

    public ushort NextDescriptorIndex { get; }

    public bool DeviceWritable { get; }

    public bool DeviceReadable => !DeviceWritable;

    public ushort Flags { get; }

    public uint DescriptorTableOffset { get; }

    public VirtioBlockDescriptorMemoryRange MemoryRange { get; }
}
