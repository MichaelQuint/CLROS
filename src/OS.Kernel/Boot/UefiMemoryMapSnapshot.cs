namespace OS.Boot;

public readonly struct UefiMemoryMapSnapshot
{
    private readonly UefiMemoryDescriptor[]? _descriptorEntries;

    private UefiMemoryMapSnapshot(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        UefiMemoryDescriptor[] descriptorEntries)
    {
        MapKey = mapKey;
        DescriptorSize = descriptorSize;
        DescriptorVersion = descriptorVersion;
        _descriptorEntries = descriptorEntries;
    }

    public ulong MapKey { get; }

    public ulong DescriptorSize { get; }

    public uint DescriptorVersion { get; }

    public int DescriptorCount => _descriptorEntries is null ? 0 : _descriptorEntries.Length;

    public ReadOnlySpan<UefiMemoryDescriptor> DescriptorEntries
    {
        get
        {
            UefiMemoryDescriptor[]? entries = _descriptorEntries;
            return entries is null ? default : entries;
        }
    }

    public bool HasDescriptors => DescriptorCount != 0;

    public bool TryGetDescriptor(int index, out UefiMemoryDescriptor descriptor)
    {
        UefiMemoryDescriptor[]? entries = _descriptorEntries;
        if (entries is null || (uint)index >= (uint)entries.Length)
        {
            descriptor = default;
            return false;
        }

        descriptor = entries[index];
        return true;
    }

    internal static UefiMemoryMapSnapshot CreateUnchecked(
        ulong mapKey,
        ulong descriptorSize,
        uint descriptorVersion,
        UefiMemoryDescriptor[] descriptorEntries)
    {
        return new UefiMemoryMapSnapshot(
            mapKey,
            descriptorSize,
            descriptorVersion,
            descriptorEntries);
    }
}
