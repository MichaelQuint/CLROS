namespace OS.Boot;
public readonly struct BootMemoryRegionMaterializationResult
{
    private BootMemoryRegionMaterializationResult(BootMemoryRegionMaterializationStatus status, ulong descriptorCount, ulong regionCount, ulong requiredRegionCapacity, ulong availableRegionCapacity, ulong invalidDescriptorIndex, ulong totalPages, ulong totalBytes, ulong conventionalMemoryPages, ulong conventionalMemoryBytes)
    {
        Status = status;
        DescriptorCount = descriptorCount;
        RegionCount = regionCount;
        RequiredRegionCapacity = requiredRegionCapacity;
        AvailableRegionCapacity = availableRegionCapacity;
        InvalidDescriptorIndex = invalidDescriptorIndex;
        TotalPages = totalPages;
        TotalBytes = totalBytes;
        ConventionalMemoryPages = conventionalMemoryPages;
        ConventionalMemoryBytes = conventionalMemoryBytes;
    }

    public BootMemoryRegionMaterializationStatus Status { get; }
    public bool Succeeded => Status == BootMemoryRegionMaterializationStatus.Success;
    public ulong DescriptorCount { get; }
    public ulong RegionCount { get; }
    public ulong RequiredRegionCapacity { get; }
    public ulong AvailableRegionCapacity { get; }
    public ulong InvalidDescriptorIndex { get; }
    public ulong TotalPages { get; }
    public ulong TotalBytes { get; }
    public ulong ConventionalMemoryPages { get; }
    public ulong ConventionalMemoryBytes { get; }

    public static BootMemoryRegionMaterializationResult Success(ulong descriptorCount, ulong availableRegionCapacity, ulong totalPages, ulong totalBytes, ulong conventionalMemoryPages, ulong conventionalMemoryBytes)
    {
        return new BootMemoryRegionMaterializationResult(BootMemoryRegionMaterializationStatus.Success, descriptorCount, descriptorCount, descriptorCount, availableRegionCapacity, invalidDescriptorIndex: 0, totalPages, totalBytes, conventionalMemoryPages, conventionalMemoryBytes);
    }

    public static BootMemoryRegionMaterializationResult Fail(BootMemoryRegionMaterializationStatus status, ulong descriptorCount = 0, ulong requiredRegionCapacity = 0, ulong availableRegionCapacity = 0, ulong invalidDescriptorIndex = 0, ulong totalPages = 0, ulong totalBytes = 0, ulong conventionalMemoryPages = 0, ulong conventionalMemoryBytes = 0)
    {
        return new BootMemoryRegionMaterializationResult(status, descriptorCount, regionCount: 0, requiredRegionCapacity, availableRegionCapacity, invalidDescriptorIndex, totalPages, totalBytes, conventionalMemoryPages, conventionalMemoryBytes);
    }
}
#if !NOSTDLIB_EFI
#endif
