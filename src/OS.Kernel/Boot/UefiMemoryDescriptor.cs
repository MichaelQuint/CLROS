namespace OS.Boot;

public readonly struct UefiMemoryDescriptor
{
    public UefiMemoryDescriptor(
        UefiMemoryType type,
        ulong physicalStart,
        ulong virtualStart,
        ulong numberOfPages,
        ulong attribute)
    {
        Type = type;
        PhysicalStart = physicalStart;
        VirtualStart = virtualStart;
        NumberOfPages = numberOfPages;
        Attribute = attribute;
    }

    public UefiMemoryType Type { get; }

    public ulong PhysicalStart { get; }

    public ulong VirtualStart { get; }

    public ulong NumberOfPages { get; }

    public ulong Attribute { get; }
}
