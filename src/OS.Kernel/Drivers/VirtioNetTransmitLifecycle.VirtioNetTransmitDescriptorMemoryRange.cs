namespace OS.Drivers;

using OS.Std.Binary;

public readonly struct VirtioNetTransmitDescriptorMemoryRange
{
    internal VirtioNetTransmitDescriptorMemoryRange(VirtioNetTransmitDescriptorRole role, ulong physicalAddress, uint length, ulong endExclusive)
    {
        Role = role;
        PhysicalAddress = physicalAddress;
        Length = length;
        EndExclusive = endExclusive;
    }

    public VirtioNetTransmitDescriptorRole Role
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
