using OS.Boot;

namespace OS.Kernel.Memory;

public readonly struct MemoryRegion
{
    public const ulong PageSize = 4096;

    private MemoryRegion(
        MemoryRegionType type,
        UefiMemoryType uefiType,
        ulong physicalStart,
        ulong virtualStart,
        ulong pageCount,
        ulong attribute,
        ulong byteLength,
        ulong physicalEndExclusive)
    {
        Type = type;
        UefiType = uefiType;
        PhysicalStart = physicalStart;
        VirtualStart = virtualStart;
        PageCount = pageCount;
        Attribute = attribute;
        ByteLength = byteLength;
        PhysicalEndExclusive = physicalEndExclusive;
    }

    public MemoryRegionType Type
    {
        get;
    }

    public UefiMemoryType UefiType
    {
        get;
    }

    public uint UefiTypeValue => (uint)UefiType;

    public ulong PhysicalStart
    {
        get;
    }

    public ulong VirtualStart
    {
        get;
    }

    public ulong PageCount
    {
        get;
    }

    public ulong Attribute
    {
        get;
    }

    public ulong ByteLength
    {
        get;
    }

    public ulong PhysicalEndExclusive
    {
        get;
    }

    public bool HasVirtualAddress => VirtualStart != 0;

    public static MemoryRegionResult FromUefiDescriptor(UefiMemoryDescriptor descriptor)
    {
        if (descriptor.NumberOfPages == 0)
        {
            return MemoryRegionResult.Invalid(
                MemoryMapInfoStatus.InvalidPageCount,
                "UEFI memory descriptor page count is required.");
        }

        if (descriptor.NumberOfPages > ulong.MaxValue / PageSize)
        {
            return MemoryRegionResult.Invalid(
                MemoryMapInfoStatus.PageByteCountOverflow,
                "UEFI memory descriptor byte length overflows.");
        }

        ulong byteLength = descriptor.NumberOfPages * PageSize;
        if (descriptor.PhysicalStart > ulong.MaxValue - byteLength)
        {
            return MemoryRegionResult.Invalid(
                MemoryMapInfoStatus.AddressRangeOverflow,
                "UEFI memory descriptor physical range overflows.");
        }

        return MemoryRegionResult.Valid(new MemoryRegion(
            MapUefiType(descriptor.Type),
            descriptor.Type,
            descriptor.PhysicalStart,
            descriptor.VirtualStart,
            descriptor.NumberOfPages,
            descriptor.Attribute,
            byteLength,
            descriptor.PhysicalStart + byteLength));
    }

    private static MemoryRegionType MapUefiType(UefiMemoryType type)
    {
        return (uint)type switch
        {
            (uint)UefiMemoryType.Reserved => MemoryRegionType.Reserved,
            (uint)UefiMemoryType.LoaderCode => MemoryRegionType.LoaderCode,
            (uint)UefiMemoryType.LoaderData => MemoryRegionType.LoaderData,
            (uint)UefiMemoryType.BootServicesCode => MemoryRegionType.BootServicesCode,
            (uint)UefiMemoryType.BootServicesData => MemoryRegionType.BootServicesData,
            (uint)UefiMemoryType.RuntimeServicesCode => MemoryRegionType.RuntimeServicesCode,
            (uint)UefiMemoryType.RuntimeServicesData => MemoryRegionType.RuntimeServicesData,
            (uint)UefiMemoryType.ConventionalMemory => MemoryRegionType.ConventionalMemory,
            (uint)UefiMemoryType.UnusableMemory => MemoryRegionType.UnusableMemory,
            (uint)UefiMemoryType.AcpiReclaimMemory => MemoryRegionType.AcpiReclaimMemory,
            (uint)UefiMemoryType.AcpiMemoryNvs => MemoryRegionType.AcpiMemoryNvs,
            (uint)UefiMemoryType.MemoryMappedIo => MemoryRegionType.MemoryMappedIo,
            (uint)UefiMemoryType.MemoryMappedIoPortSpace => MemoryRegionType.MemoryMappedIoPortSpace,
            (uint)UefiMemoryType.PalCode => MemoryRegionType.PalCode,
            (uint)UefiMemoryType.PersistentMemory => MemoryRegionType.PersistentMemory,
            _ => MemoryRegionType.Unknown
        };
    }
}
