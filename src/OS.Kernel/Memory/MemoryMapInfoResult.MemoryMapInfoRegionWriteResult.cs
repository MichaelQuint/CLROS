using OS.Boot;

namespace OS.Kernel.Memory;

public readonly struct MemoryMapInfoRegionWriteResult
{
    private readonly string? _message;
    private MemoryMapInfoRegionWriteResult(MemoryMapInfoRegionWriteStatus status, ulong mapKey, ulong descriptorSize, uint descriptorVersion, int descriptorCount, int regionCount, int requiredRegionCapacity, int availableRegionCapacity, int invalidDescriptorIndex, ulong totalPages, ulong totalBytes, ulong conventionalMemoryPages, ulong conventionalMemoryBytes, string message)
    {
        Status = status;
        MapKey = mapKey;
        DescriptorSize = descriptorSize;
        DescriptorVersion = descriptorVersion;
        DescriptorCount = descriptorCount;
        RegionCount = regionCount;
        RequiredRegionCapacity = requiredRegionCapacity;
        AvailableRegionCapacity = availableRegionCapacity;
        InvalidDescriptorIndex = invalidDescriptorIndex;
        TotalPages = totalPages;
        TotalBytes = totalBytes;
        ConventionalMemoryPages = conventionalMemoryPages;
        ConventionalMemoryBytes = conventionalMemoryBytes;
        _message = message;
    }

    public MemoryMapInfoRegionWriteStatus Status
    {
        get;
    }
    public bool Succeeded => Status == MemoryMapInfoRegionWriteStatus.Success;
    public ulong MapKey
    {
        get;
    }
    public ulong DescriptorSize
    {
        get;
    }
    public uint DescriptorVersion
    {
        get;
    }
    public int DescriptorCount
    {
        get;
    }
    public int RegionCount
    {
        get;
    }
    public int RequiredRegionCapacity
    {
        get;
    }
    public int AvailableRegionCapacity
    {
        get;
    }
    public int InvalidDescriptorIndex
    {
        get;
    }
    public ulong TotalPages
    {
        get;
    }
    public ulong TotalBytes
    {
        get;
    }
    public ulong ConventionalMemoryPages
    {
        get;
    }
    public ulong ConventionalMemoryBytes
    {
        get;
    }
    public string Message => _message ?? string.Empty;

    internal static MemoryMapInfoRegionWriteResult Success(ulong mapKey, ulong descriptorSize, uint descriptorVersion, int descriptorCount, int regionCount, int requiredRegionCapacity, int availableRegionCapacity, ulong totalPages, ulong totalBytes, ulong conventionalMemoryPages, ulong conventionalMemoryBytes)
    {
        return new MemoryMapInfoRegionWriteResult(MemoryMapInfoRegionWriteStatus.Success, mapKey, descriptorSize, descriptorVersion, descriptorCount, regionCount, requiredRegionCapacity, availableRegionCapacity, invalidDescriptorIndex: -1, totalPages, totalBytes, conventionalMemoryPages, conventionalMemoryBytes, string.Empty);
    }

    internal static MemoryMapInfoRegionWriteResult Fail(MemoryMapInfoRegionWriteStatus status, ulong mapKey, ulong descriptorSize, uint descriptorVersion, int descriptorCount, int invalidDescriptorIndex, int requiredRegionCapacity, int availableRegionCapacity, string message)
    {
        return Fail(status, mapKey, descriptorSize, descriptorVersion, descriptorCount, invalidDescriptorIndex, requiredRegionCapacity, availableRegionCapacity, totalPages: 0, totalBytes: 0, conventionalMemoryPages: 0, conventionalMemoryBytes: 0, message);
    }

    internal static MemoryMapInfoRegionWriteResult Fail(MemoryMapInfoRegionWriteStatus status, ulong mapKey, ulong descriptorSize, uint descriptorVersion, int descriptorCount, int invalidDescriptorIndex, int requiredRegionCapacity, int availableRegionCapacity, ulong totalPages, ulong totalBytes, ulong conventionalMemoryPages, ulong conventionalMemoryBytes, string message)
    {
        return new MemoryMapInfoRegionWriteResult(status, mapKey, descriptorSize, descriptorVersion, descriptorCount, regionCount: 0, requiredRegionCapacity, availableRegionCapacity, invalidDescriptorIndex, totalPages, totalBytes, conventionalMemoryPages, conventionalMemoryBytes, message);
    }
}
