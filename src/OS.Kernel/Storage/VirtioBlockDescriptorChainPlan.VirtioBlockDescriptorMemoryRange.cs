using OS.Drivers;

namespace OS.Storage;

public readonly struct VirtioBlockDescriptorMemoryRange
{
    internal VirtioBlockDescriptorMemoryRange(
        VirtioBlockDescriptorChainRole role,
        ulong physicalAddress,
        uint length,
        ulong endExclusive)
    {
        Role = role;
        PhysicalAddress = physicalAddress;
        Length = length;
        EndExclusive = endExclusive;
    }

    public VirtioBlockDescriptorChainRole Role
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

    public ulong EndExclusive
    {
        get;
    }
}
